using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

public static class NutritionColumnParser
{
    private static readonly Regex NumberRegex = new(@"\d+(?:[\.,]\d+)?", RegexOptions.Compiled);

    public class NutritionColumns
    {
        public int? Per100gIndex { get; set; }
        public int? Per100mlIndex { get; set; }
        public int? PortionIndex { get; set; }
        public string? HeaderLine { get; set; }

        public bool HasAnyColumn => Per100gIndex.HasValue || Per100mlIndex.HasValue || PortionIndex.HasValue;

        public int? GetPreferredIndex()
        {
            return Per100mlIndex ?? Per100gIndex ?? PortionIndex;
        }
    }

    public sealed class ColumnDetectionResult
    {
        public int? Per100gIndex { get; set; }
        public int? Per100mlIndex { get; set; }
        public int? PortionIndex { get; set; }
        public string? HeaderLine { get; set; }

        public bool HasAnyColumn => Per100gIndex.HasValue || Per100mlIndex.HasValue || PortionIndex.HasValue;
    }

    public static NutritionColumns DetectColumns(List<string> rawText)
    {
        var best = new NutritionColumns();
        var bestScore = -1;

        foreach (var line in rawText.Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            var candidate = DetectColumnsFromHeader(line);
            var score = (candidate.Per100mlIndex.HasValue ? 2 : 0)
                      + (candidate.Per100gIndex.HasValue ? 2 : 0)
                      + (candidate.PortionIndex.HasValue ? 1 : 0);

            if (score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    public static double? ExtractValue(string line, int index)
    {
        if (string.IsNullOrWhiteSpace(line) || index < 0)
            return null;

        var values = ExtractNumericValues(line);
        if (values.Count <= index)
            return null;

        return values[index];
    }

    public static string NormalizeToken(string input)
    {
        return NormalizeText(input);
    }

    private static NutritionColumns DetectColumnsFromHeader(string headerLine)
    {
        var normalized = NormalizeText(headerLine);
        var result = new NutritionColumns { HeaderLine = headerLine };

        if (string.IsNullOrWhiteSpace(normalized))
            return result;

        var labels = new List<(int Position, string Type)>();

        labels.AddRange(Regex.Matches(normalized, @"\b100\s*m[l1]\b")
            .Select(m => (m.Index, "100ml")));

        labels.AddRange(Regex.Matches(normalized, @"\b100\s*g\b")
            .Select(m => (m.Index, "100g")));

        labels.AddRange(Regex.Matches(normalized, @"\bporc(?:ao)?\b")
            .Select(m => (m.Index, "portion")));

        labels.AddRange(Regex.Matches(normalized, @"%\s*vd\b|\bvd\b")
            .Select(m => (m.Index, "vd")));

        if (labels.Count == 0)
            return result;

        var ordered = labels.OrderBy(l => l.Position).ToList();
        var valueColumnIndex = 0;

        foreach (var label in ordered)
        {
            if (label.Type == "vd")
                continue;

            switch (label.Type)
            {
                case "100ml" when !result.Per100mlIndex.HasValue:
                    result.Per100mlIndex = valueColumnIndex;
                    break;
                case "100g" when !result.Per100gIndex.HasValue:
                    result.Per100gIndex = valueColumnIndex;
                    break;
                case "portion" when !result.PortionIndex.HasValue:
                    result.PortionIndex = valueColumnIndex;
                    break;
            }

            valueColumnIndex++;
        }

        return result;
    }

    private static List<double> ExtractNumericValues(string line)
    {
        var normalized = NormalizeText(line);

        return NumberRegex
            .Matches(normalized)
            .Select(m => ParseDouble(m.Value))
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();
    }

    private static double? ParseDouble(string raw)
    {
        var cleaned = raw.Replace(",", ".").Trim();
        if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            return value;

        return null;
    }

    private static string NormalizeText(string value)
    {
        var noMarks = value
            .Replace("**", " ")
            .Replace("*", " ")
            .Replace("%", " ")
            .Replace("|", " ")
            .Replace("•", " ");

        var formD = noMarks.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);

        foreach (var ch in formD)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        var normalized = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[^a-z0-9\.,\s]", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        return normalized;
    }
}
