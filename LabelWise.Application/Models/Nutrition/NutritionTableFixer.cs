using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Models.Nutrition;

public static class NutritionTableFixer
{
    private static readonly Regex NumberRegex = new(@"\d+(?:[\.,]\d+)?", RegexOptions.Compiled);

    public static void Fix(EstimatedNutritionProfileDto nutrition, List<string> rawText, Action<string>? log = null)
    {
        if (nutrition == null || rawText == null || rawText.Count == 0)
            return;

        try
        {
            var columns = DetectColumns(rawText);
            var parsed = ParseRows(rawText);
            if (parsed.Count == 0)
                return;

            var selectedColumn = SelectColumn(columns, parsed);
            if (selectedColumn < 0)
                return;

            var selectedUnit = DetectUnit(rawText);
            var hasMinimumConfidence = CountValues(parsed, selectedColumn) >= 4;
            if (!hasMinimumConfidence)
                return;

            var mixed = IsColumnMixed(nutrition, parsed);
            var aligned = IsMostlyAlignedWithColumn(nutrition, parsed, selectedColumn);

            if (!mixed && aligned)
                return;

            log?.Invoke($"[NutritionTableFixer] Column mixing detected - applying global correction using column {selectedColumn} ({selectedUnit})");

            var changed = ApplyGlobalCorrection(nutrition, parsed, selectedColumn, selectedUnit);
            if (!changed)
                return;

            nutrition.IsCorrectedByOcr = true;
            nutrition.NutritionUnit = selectedUnit;
            nutrition.Basis = selectedUnit == "ml"
                ? "100 ml (corrigido via OCR)"
                : "100 g (corrigido via OCR)";

            log?.Invoke($"[NutritionTableFixer] Correction applied successfully - {CountValues(parsed, selectedColumn)} values updated");
        }
        catch (Exception ex)
        {
            log?.Invoke($"[NutritionTableFixer] Error during fix: {ex.Message}");
        }
    }

    private static (int col100, int colPortion) DetectColumns(List<string> rawText)
    {
        foreach (var line in rawText)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var normalized = Normalize(line);
            var compact = Compact(normalized);
            if (!compact.Contains("100g") && !compact.Contains("100ml"))
                continue;

            var tokens = Regex.Split(normalized, @"\s+|\|")
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            var valueColumn = 0;
            var col100 = -1;
            var colPortion = -1;

            foreach (var token in tokens)
            {
                if (token.Contains("vd"))
                    continue;

                var compactToken = Compact(token);

                if (compactToken.Contains("100g") || compactToken.Contains("100ml"))
                {
                    if (col100 < 0)
                        col100 = valueColumn;

                    valueColumn++;
                    continue;
                }

                if (Regex.IsMatch(compactToken, @"^\d+(?:[\.,]\d+)?g$"))
                {
                    if (colPortion < 0)
                        colPortion = valueColumn;

                    valueColumn++;
                }
            }

            if (col100 >= 0 || colPortion >= 0)
                return (col100, colPortion);
        }

        return (-1, -1);
    }

    private static Dictionary<string, (double? col100, double? colPortion)> ParseRows(List<string> rawText)
    {
        var result = new Dictionary<string, (double? col100, double? colPortion)>(StringComparer.OrdinalIgnoreCase);
        var columns = DetectColumns(rawText);

        foreach (var line in rawText)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var key = MapNutrientKey(line);
            if (key == null)
                continue;

            var numbers = ExtractNumbers(line);
            if (numbers.Count == 0)
                continue;

            var col100 = ValueByIndex(numbers, columns.col100);
            var colPortion = ValueByIndex(numbers, columns.colPortion);

            if (!col100.HasValue)
                col100 = ValueByIndex(numbers, 0);

            if (!colPortion.HasValue)
                colPortion = ValueByIndex(numbers, 1);

            result[key] = (col100, colPortion);
        }

        return result;
    }

    private static int SelectColumn(
        (int col100, int colPortion) columns,
        Dictionary<string, (double? col100, double? colPortion)> parsed)
    {
        if (columns.col100 >= 0)
            return 0;

        if (columns.colPortion >= 0)
            return 1;

        var sum100 = parsed.Values.Sum(v => v.col100 ?? 0);
        var sumPortion = parsed.Values.Sum(v => v.colPortion ?? 0);

        return sum100 >= sumPortion ? 0 : 1;
    }

    private static bool IsColumnMixed(
        EstimatedNutritionProfileDto nutrition,
        Dictionary<string, (double? col100, double? colPortion)> parsed)
    {
        var votes100 = 0;
        var votesPortion = 0;

        VoteClosest(nutrition.CaloriesPer100g ?? nutrition.CaloriesPer100ml, parsed, "calories", ref votes100, ref votesPortion);
        VoteClosest(nutrition.EstimatedCarbsPer100g, parsed, "carbs", ref votes100, ref votesPortion);
        VoteClosest(nutrition.EstimatedSugarPer100g, parsed, "sugar", ref votes100, ref votesPortion);
        VoteClosest(nutrition.EstimatedAddedSugarPer100g, parsed, "added_sugar", ref votes100, ref votesPortion);
        VoteClosest(nutrition.EstimatedProteinPer100g, parsed, "protein", ref votes100, ref votesPortion);
        VoteClosest(nutrition.EstimatedFatPer100g, parsed, "fat", ref votes100, ref votesPortion);
        VoteClosest(nutrition.EstimatedSaturatedFatPer100g, parsed, "saturated_fat", ref votes100, ref votesPortion);
        VoteClosest(nutrition.EstimatedFiberPer100g, parsed, "fiber", ref votes100, ref votesPortion);
        VoteClosest(nutrition.EstimatedSodiumPer100g, parsed, "sodium", ref votes100, ref votesPortion);

        return votes100 > 0 && votesPortion > 0;
    }

    private static bool IsMostlyAlignedWithColumn(
        EstimatedNutritionProfileDto nutrition,
        Dictionary<string, (double? col100, double? colPortion)> parsed,
        int selectedColumn)
    {
        var checks = 0;
        var matches = 0;

        Compare(nutrition.CaloriesPer100g ?? nutrition.CaloriesPer100ml, parsed, "calories", selectedColumn, ref checks, ref matches);
        Compare(nutrition.EstimatedCarbsPer100g, parsed, "carbs", selectedColumn, ref checks, ref matches);
        Compare(nutrition.EstimatedProteinPer100g, parsed, "protein", selectedColumn, ref checks, ref matches);
        Compare(nutrition.EstimatedFatPer100g, parsed, "fat", selectedColumn, ref checks, ref matches);

        if (checks == 0)
            return false;

        return matches >= Math.Max(2, checks - 1);
    }

    private static bool ApplyGlobalCorrection(
        EstimatedNutritionProfileDto nutrition,
        Dictionary<string, (double? col100, double? colPortion)> parsed,
        int selectedColumn,
        string unit)
    {
        var changed = false;
        nutrition.DataSource ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        changed |= ApplyField(nutrition, parsed, "carbs", selectedColumn, nutrition.EstimatedCarbsPer100g, v => nutrition.EstimatedCarbsPer100g = v, "EstimatedCarbsPer100g");
        changed |= ApplyField(nutrition, parsed, "sugar", selectedColumn, nutrition.EstimatedSugarPer100g, v => nutrition.EstimatedSugarPer100g = v, "EstimatedSugarPer100g");
        changed |= ApplyField(nutrition, parsed, "added_sugar", selectedColumn, nutrition.EstimatedAddedSugarPer100g, v => nutrition.EstimatedAddedSugarPer100g = v, "EstimatedAddedSugarPer100g");
        changed |= ApplyField(nutrition, parsed, "protein", selectedColumn, nutrition.EstimatedProteinPer100g, v => nutrition.EstimatedProteinPer100g = v, "EstimatedProteinPer100g");
        changed |= ApplyField(nutrition, parsed, "fat", selectedColumn, nutrition.EstimatedFatPer100g, v => nutrition.EstimatedFatPer100g = v, "EstimatedFatPer100g");
        changed |= ApplyField(nutrition, parsed, "saturated_fat", selectedColumn, nutrition.EstimatedSaturatedFatPer100g, v => nutrition.EstimatedSaturatedFatPer100g = v, "EstimatedSaturatedFatPer100g");
        changed |= ApplyField(nutrition, parsed, "fiber", selectedColumn, nutrition.EstimatedFiberPer100g, v => nutrition.EstimatedFiberPer100g = v, "EstimatedFiberPer100g");
        changed |= ApplyField(nutrition, parsed, "sodium", selectedColumn, nutrition.EstimatedSodiumPer100g, v => nutrition.EstimatedSodiumPer100g = v, "EstimatedSodiumPer100g");

        if (parsed.TryGetValue("calories", out var calories))
        {
            var value = selectedColumn == 0 ? calories.col100 : calories.colPortion;
            if (value.HasValue)
            {
                if (unit == "ml")
                {
                    changed |= !AreClose(nutrition.CaloriesPer100ml, value);
                    nutrition.CaloriesPer100ml = value;
                    nutrition.CaloriesPer100g = null;
                    nutrition.DataSource["CaloriesPer100ml"] = "OCR";
                }
                else
                {
                    changed |= !AreClose(nutrition.CaloriesPer100g, value);
                    nutrition.CaloriesPer100g = value;
                    nutrition.CaloriesPer100ml = null;
                    nutrition.DataSource["CaloriesPer100g"] = "OCR";
                }
            }
        }

        return changed;
    }

    private static bool ApplyField(
        EstimatedNutritionProfileDto nutrition,
        Dictionary<string, (double? col100, double? colPortion)> parsed,
        string key,
        int selectedColumn,
        double? current,
        Action<double?> assign,
        string sourceKey)
    {
        if (!parsed.TryGetValue(key, out var tuple))
            return false;

        var next = selectedColumn == 0 ? tuple.col100 : tuple.colPortion;
        if (!next.HasValue)
            return false;

        var changed = !AreClose(current, next);
        assign(next);
        nutrition.DataSource[sourceKey] = "OCR";
        return changed;
    }

    private static int CountValues(Dictionary<string, (double? col100, double? colPortion)> parsed, int selectedColumn)
    {
        var count = 0;
        foreach (var value in parsed.Values)
        {
            var v = selectedColumn == 0 ? value.col100 : value.colPortion;
            if (v.HasValue)
                count++;
        }

        return count;
    }

    private static void VoteClosest(
        double? current,
        Dictionary<string, (double? col100, double? colPortion)> parsed,
        string key,
        ref int votes100,
        ref int votesPortion)
    {
        if (!current.HasValue || !parsed.TryGetValue(key, out var pair))
            return;

        if (!pair.col100.HasValue || !pair.colPortion.HasValue)
            return;

        var d100 = Math.Abs(current.Value - pair.col100.Value);
        var dPortion = Math.Abs(current.Value - pair.colPortion.Value);

        if (d100 < dPortion)
            votes100++;
        else if (dPortion < d100)
            votesPortion++;
    }

    private static void Compare(
        double? current,
        Dictionary<string, (double? col100, double? colPortion)> parsed,
        string key,
        int selectedColumn,
        ref int checks,
        ref int matches)
    {
        if (!current.HasValue || !parsed.TryGetValue(key, out var pair))
            return;

        var selected = selectedColumn == 0 ? pair.col100 : pair.colPortion;
        if (!selected.HasValue)
            return;

        checks++;
        if (Math.Abs(current.Value - selected.Value) <= Math.Max(1.0, selected.Value * 0.20))
            matches++;
    }

    private static string DetectUnit(List<string> rawText)
    {
        foreach (var line in rawText)
        {
            var normalized = Compact(Normalize(line));
            if (normalized.Contains("100ml"))
                return "ml";

            if (normalized.Contains("100g"))
                return "g";
        }

        return "g";
    }

    private static string? MapNutrientKey(string line)
    {
        var n = Compact(Normalize(line));

        if (n.Contains("valorenergetico") || n.Contains("energetico") || n.Contains("energ")) return "calories";
        if (n.Contains("carbo")) return "carbs";
        if ((n.Contains("acucar") || n.Contains("acucares")) && (n.Contains("adicionado") || n.Contains("adicionados"))) return "added_sugar";
        if (n.Contains("acucar") || n.Contains("acucares")) return "sugar";
        if (n.Contains("prote")) return "protein";
        if ((n.Contains("gordura") || n.Contains("gorduras")) && n.Contains("saturad")) return "saturated_fat";
        if ((n.Contains("gordura") || n.Contains("gorduras")) && (n.Contains("total") || n.Contains("totais"))) return "fat";
        if (n.Contains("fibra")) return "fiber";
        if (n.Contains("sodio")) return "sodium";

        return null;
    }

    private static List<double> ExtractNumbers(string line)
    {
        var sanitized = Regex.Replace(Normalize(line), @"\([^\)]*\)", " ");
        var values = new List<double>();

        foreach (Match match in NumberRegex.Matches(sanitized))
        {
            var raw = match.Value.Replace(',', '.');
            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static double? ValueByIndex(IReadOnlyList<double> values, int index)
    {
        if (index < 0 || index >= values.Count)
            return null;

        return values[index];
    }

    private static bool AreClose(double? left, double? right)
    {
        if (!left.HasValue && !right.HasValue)
            return true;

        if (!left.HasValue || !right.HasValue)
            return false;

        return Math.Abs(left.Value - right.Value) <= 0.001;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var formD = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);

        foreach (var ch in formD)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) || ch == '|' || ch == ',' || ch == '.')
                    sb.Append(char.ToLowerInvariant(ch));
            }
        }

        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    private static string Compact(string value)
    {
        return value.Replace(" ", string.Empty);
    }
}
