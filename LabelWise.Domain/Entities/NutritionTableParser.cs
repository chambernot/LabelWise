using System.Globalization;
using System.Text.RegularExpressions;

namespace LabelWise.Domain.Entities;

/// <summary>
/// Evidência de extração por linha para auditoria e debug.
/// Expõe o texto exato da linha utilizada, o valor selecionado,
/// todos os candidatos encontrados e o índice de coluna inferido.
/// </summary>
public sealed record NutrientRowEvidence(
    string RowText,
    double ExtractedValue,
    IReadOnlyList<double> Candidates,
    int? ColumnIndex = null);

public sealed class ParsedNutritionResult
{
    public double? Calories { get; set; }
    public double? Carbs { get; set; }
    public double? Sugar { get; set; }
    public double? AddedSugar { get; set; }
    public double? Protein { get; set; }
    public double? Fat { get; set; }
    public double? SaturatedFat { get; set; }
    public double? Fiber { get; set; }
    public double? Sodium { get; set; }
    public string Unit { get; set; } = "g";
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Mapeamento nutriente → evidência de extração.
    /// Chaves: "calories", "carbs", "sugar", "added_sugar", "protein",
    ///         "fat", "saturated_fat", "fiber", "sodium".
    /// </summary>
    public Dictionary<string, NutrientRowEvidence> RowMatches { get; set; } = new();

    public bool HasAnyValue =>
        Calories.HasValue || Carbs.HasValue || Sugar.HasValue || AddedSugar.HasValue ||
        Protein.HasValue || Fat.HasValue || SaturatedFat.HasValue || Fiber.HasValue || Sodium.HasValue;
}

public sealed class NutritionTableParser
{
    private static readonly Regex NumberRegex   = new(@"\d+(?:[\.,]\d+)?", RegexOptions.Compiled);
    private static readonly Regex VdTokenRegex  = new(@"\d+(?:[.,]\d+)?\s*%", RegexOptions.Compiled);
    private static readonly Regex SodiumMgRegex = new(@"(\d+(?:[.,]\d+)?)\s*mg", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Dictionary<string, (double Min, double Max)> NutrientRanges = new()
    {
        ["calories"]      = (0,   900),
        ["carbs"]         = (0,   100),
        ["sugar"]         = (0,   100),
        ["added_sugar"]   = (0,   100),
        ["protein"]       = (0,   100),
        ["fat"]           = (0,   100),
        ["saturated_fat"] = (0,   100),
        ["fiber"]         = (0,   100),
        ["sodium"]        = (0,  5000),
    };

    public ParsedNutritionResult Parse(List<string> rawText)
    {
        var parsed = new ParsedNutritionResult();
        if (rawText == null || rawText.Count == 0) return parsed;

        Console.WriteLine("[NutritionTableParser] Trying TRADITIONAL...");
        var r1 = ParseTraditional(rawText);
        if (r1.HasAnyValue) { Console.WriteLine("TRADITIONAL OK"); return PostProcess(ApplyCalorieCorrection(r1), rawText); }

        Console.WriteLine("[NutritionTableParser] Trying CONTEXTUAL...");
        var r2 = ParseContextual(rawText);
        if (r2.HasAnyValue) { Console.WriteLine("CONTEXTUAL OK"); return PostProcess(ApplyCalorieCorrection(r2), rawText); }

        Console.WriteLine("[NutritionTableParser] Trying STRUCTURAL...");
        var r3 = ParseStructural(rawText);
        return PostProcess(ApplyCalorieCorrection(r3), rawText);
    }

    /// <summary>
    /// Pós-processamento aplicado a todos os caminhos de parsing:
    ///   1. Garante extração de sódio quando linha detectada mas valor nulo
    ///   2. Valida relação gordura total ≥ gordura saturada (swap se violada)
    /// </summary>
    private static ParsedNutritionResult PostProcess(ParsedNutritionResult parsed, List<string> rawText)
    {
        ForceSodiumIfMissing(parsed, rawText);
        ValidateFatRelationship(parsed);
        return parsed;
    }

    /// <summary>
    /// Se a linha de sódio foi detectada mas nenhum valor foi extraído,
    /// varre explicitamente a linha e as duas seguintes em busca do valor em mg.
    /// </summary>
    private static void ForceSodiumIfMissing(ParsedNutritionResult parsed, List<string> rawText)
    {
        if (parsed.Sodium.HasValue) return;

        for (int i = 0; i < rawText.Count; i++)
        {
            var token = NutritionColumnParser.NormalizeToken(rawText[i]);
            if (!token.Contains("sodio", StringComparison.Ordinal)) continue;

            // Tentar extrair valor "mg" explícito primeiro
            var mgMatch = SodiumMgRegex.Match(rawText[i]);
            if (mgMatch.Success && double.TryParse(
                    mgMatch.Groups[1].Value.Replace(',', '.'),
                    NumberStyles.Any, CultureInfo.InvariantCulture, out var mg)
                && mg is >= 0 and <= 5000)
            {
                parsed.Sodium = mg;
                parsed.RowMatches["sodium_forced"] =
                    new NutrientRowEvidence(rawText[i].Trim(), mg, [mg]);
                Console.WriteLine($"[NutritionTableParser] 🔧 Sódio forçado (mg): {mg} — \"{rawText[i].Trim()}\"");
                return;
            }

            // Fallback: maior número plausível na linha ou na seguinte
            var nums = ExtractNonVdNumbers(rawText[i])
                .Where(n => n is >= 0 and <= 5000)
                .ToList();

            for (int j = i + 1; j <= Math.Min(i + 2, rawText.Count - 1) && nums.Count == 0; j++)
            {
                if (IdentifyNutrient(NutritionColumnParser.NormalizeToken(rawText[j])) != null) break;
                nums = ExtractNonVdNumbers(rawText[j])
                    .Where(n => n is >= 0 and <= 5000)
                    .ToList();
            }

            if (nums.Count > 0)
            {
                var value = nums.Max();
                parsed.Sodium = value;
                parsed.RowMatches["sodium_forced"] =
                    new NutrientRowEvidence(rawText[i].Trim(), value, nums.AsReadOnly());
                Console.WriteLine($"[NutritionTableParser] 🔧 Sódio forçado (max): {value} — \"{rawText[i].Trim()}\"");
            }
            return;
        }
    }

    /// <summary>
    /// Gordura saturada ≤ gordura total é uma lei física — não pode ser violada.
    /// Quando violada, os valores foram extraídos de linhas trocadas:
    ///   • se a diferença é grande → swap
    ///   • se são iguais e ambas > 20g → caso legítimo (ex: coco, ricota)
    /// </summary>
    private static void ValidateFatRelationship(ParsedNutritionResult parsed)
    {
        if (!parsed.Fat.HasValue || !parsed.SaturatedFat.HasValue) return;

        double fat = parsed.Fat.Value;
        double satFat = parsed.SaturatedFat.Value;

        if (satFat <= fat) return;

        // Caso especial: fat > 20 e satFat == fat → possível produto rico em gordura saturada (coco)
        if (fat > 20 && Math.Abs(fat - satFat) < 0.01)
        {
            Console.WriteLine($"[NutritionTableParser] ℹ️ Fat ≈ SatFat ({fat:F1}g) — caso legítimo (produto altamente saturado)");
            return;
        }

        Console.WriteLine(
            $"[NutritionTableParser] ⚠️ SatFat ({satFat:F1}g) > Fat ({fat:F1}g) — valores trocados (possível erro de linha OCR)");

        (parsed.Fat, parsed.SaturatedFat) = (satFat, fat);
        parsed.Warnings.Add(
            $"Gordura total ({satFat:F1}g) e saturada ({fat:F1}g) estavam invertidas — corrigido automaticamente.");
    }

    private ParsedNutritionResult ParseTraditional(List<string> rawText)
    {
        var parsed = new ParsedNutritionResult();
        var cols = NutritionColumnParser.DetectColumns(rawText);
        var idx = cols.Per100gIndex ?? cols.Per100mlIndex ?? cols.PortionIndex;
        if (!idx.HasValue) return parsed;
        parsed.Unit = cols.Per100mlIndex.HasValue && !cols.Per100gIndex.HasValue ? "ml" : "g";
        foreach (var line in rawText)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var token = NutritionColumnParser.NormalizeToken(line);
            if (string.IsNullOrWhiteSpace(token)) continue;
            var nutrient = IdentifyNutrient(token);
            if (nutrient == null) continue;
            var val = ExtractValueFromColumn(line, idx.Value);
            if (!val.HasValue) continue;
            AssignValueByType(parsed, nutrient, val.Value);
            // Record evidence for debug
            var allNums = ExtractNonVdNumbers(line);
            parsed.RowMatches.TryAdd(nutrient,
                new NutrientRowEvidence(line.Trim(), val.Value, allNums.AsReadOnly(), idx.Value));
        }
        return parsed;
    }

    private ParsedNutritionResult ParseContextual(List<string> rawText)
    {
        var parsed = new ParsedNutritionResult { Unit = DetectUnit(rawText) };
        for (int i = 0; i < rawText.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(rawText[i])) continue;
            var token = NutritionColumnParser.NormalizeToken(rawText[i]);
            var nt = IdentifyNutrient(token);
            if (nt == null) continue;

            // Sodium: prioritise explicit "mg" annotation
            if (nt == "sodium")
            {
                var mg = ExtractSodiumMg(rawText[i]);
                if (mg.HasValue)
                {
                    AssignValueByType(parsed, nt, mg.Value);
                    parsed.RowMatches.TryAdd(nt,
                        new NutrientRowEvidence(rawText[i].Trim(), mg.Value, [mg.Value]));
                    continue;
                }
            }

            var nums = ExtractNonVdNumbers(rawText[i]);
            double? val;
            string rowUsed;

            if (nums.Count > 0)
            {
                val = SelectBestValue(nums, nt);
                rowUsed = rawText[i];
            }
            else
            {
                // Look ahead — but STOP at lines that belong to another nutrient
                (val, rowUsed) = FindValueStrictlyInNextLines(rawText, i + 1, nt);
            }

            if (!val.HasValue) continue;
            AssignValueByType(parsed, nt, val.Value);
            var evidence = ExtractNonVdNumbers(rowUsed);
            parsed.RowMatches.TryAdd(nt,
                new NutrientRowEvidence(rowUsed.Trim(), val.Value, evidence.AsReadOnly()));
        }
        return parsed;
    }

    private ParsedNutritionResult ParseStructural(List<string> rawText)
    {
        var parsed = new ParsedNutritionResult { Unit = DetectUnit(rawText) };
        var nutrientLines = new List<(int Index, string Nutrient)>();
        for (int i = 0; i < rawText.Count; i++)
        {
            var nt = IdentifyNutrient(NutritionColumnParser.NormalizeToken(rawText[i]));
            if (nt != null) nutrientLines.Add((i, nt));
        }
        foreach (var (index, nutrient) in nutrientLines)
        {
            if (nutrient == "sodium")
            {
                var mg = ExtractSodiumMg(rawText[index]);
                if (mg.HasValue)
                {
                    AssignValueByType(parsed, nutrient, mg.Value);
                    parsed.RowMatches.TryAdd(nutrient,
                        new NutrientRowEvidence(rawText[index].Trim(), mg.Value, [mg.Value]));
                    continue;
                }
            }

            var nums = ExtractNonVdNumbers(rawText[index]);
            double? val;
            string rowUsed;

            if (nums.Count > 0)
            {
                val = SelectBestValue(nums, nutrient);
                rowUsed = rawText[index];
            }
            else
            {
                (val, rowUsed) = FindValueStrictlyInNextLines(rawText, index + 1, nutrient);
            }

            if (!val.HasValue) continue;
            AssignValueByType(parsed, nutrient, val.Value);
            var evidence = ExtractNonVdNumbers(rowUsed);
            parsed.RowMatches.TryAdd(nutrient,
                new NutrientRowEvidence(rowUsed.Trim(), val.Value, evidence.AsReadOnly()));
        }
        return parsed;
    }

    private static ParsedNutritionResult ApplyCalorieCorrection(ParsedNutritionResult parsed)
    {
        if (!parsed.Fat.HasValue || !parsed.Carbs.HasValue || !parsed.Protein.HasValue) return parsed;
        var calc = (parsed.Fat.Value * 9) + (parsed.Carbs.Value * 4) + (parsed.Protein.Value * 4);
        if (!parsed.Calories.HasValue)
        {
            if (calc > 0) { parsed.Calories = Math.Round(calc, 0); parsed.Warnings.Add($"Calorias inferidas pelos macros: {parsed.Calories} kcal"); }
            return parsed;
        }
        if (Math.Abs(parsed.Calories.Value - calc) > 100)
        {
            var orig = parsed.Calories.Value;
            parsed.Calories = Math.Round(calc, 0);
            parsed.Warnings.Add($"Calorias corrigidas de {orig:F0} para {parsed.Calories:F0} kcal (calculado pelos macros)");
        }
        return parsed;
    }

    private static List<double> ExtractNonVdNumbers(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return [];
        var stripped = VdTokenRegex.Replace(line, " ");
        var numbers = new List<double>();
        foreach (Match m in NumberRegex.Matches(stripped))
        {
            var raw = m.Value.Replace(',', '.');
            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) numbers.Add(v);
        }
        return numbers;
    }

    private static double? ExtractSodiumMg(string line)
    {
        var m = SodiumMgRegex.Match(line);
        if (!m.Success) return null;
        var raw = m.Groups[1].Value.Replace(',', '.');
        return double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static double? FindNonVdValueInNextLines(List<string> rawText, int startIndex, int maxLines, string nutrientType)
    {
        var (val, _) = FindValueStrictlyInNextLines(rawText, startIndex, nutrientType);
        return val;
    }

    private static double? SelectBestValue(List<double> numbers, string nutrientType)
    {
        if (numbers.Count == 0) return null;
        var (min, max) = NutrientRanges.GetValueOrDefault(nutrientType, (0, 999));
        var valid = numbers.Where(n => n >= min && n <= max).ToList();
        if (valid.Count == 0) return null;
        // Per-100g column always contains the LARGEST value for the same nutrient —
        // any portion < 100g yields a proportionally smaller amount.
        // Using Max removes the "portion column read first" error for all nutrient types.
        return valid.Max();
    }

    private static double? ExtractValueFromColumn(string line, int selectedIndex)
    {
        var stripped = VdTokenRegex.Replace(line, " ");
        var values = new List<double>();
        foreach (Match m in NumberRegex.Matches(stripped))
        {
            var raw = m.Value.Replace(',', '.');
            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) values.Add(v);
        }
        if (values.Count == 0) return null;
        return values[Math.Min(selectedIndex, values.Count - 1)];
    }

    private static (double? Value, string RowUsed) FindValueStrictlyInNextLines(
        List<string> rawText, int startIndex, string nutrientType)
    {
        const int MaxLookAhead = 4;
        for (int i = startIndex; i < Math.Min(startIndex + MaxLookAhead, rawText.Count); i++)
        {
            // Strict boundary: stop when another nutrient label is found
            var lineToken = NutritionColumnParser.NormalizeToken(rawText[i]);
            if (IdentifyNutrient(lineToken) != null) break;

            var nums = ExtractNonVdNumbers(rawText[i]);
            if (nums.Count > 0)
            {
                var val = SelectBestValue(nums, nutrientType);
                if (val.HasValue) return (val, rawText[i]);
            }
        }
        return (null, string.Empty);
    }

    private static string DetectUnit(List<string> rawText)
    {
        var allText = string.Join(" ", rawText).ToLowerInvariant();
        return (allText.Contains("100 ml") || allText.Contains("100ml")) ? "ml" : "g";
    }

    private static string? IdentifyNutrient(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        if (token.Contains("energ",      StringComparison.Ordinal)) return "calories";
        if (token.Contains("adicionado", StringComparison.Ordinal)) return "added_sugar";
        if (token.Contains("acucar",     StringComparison.Ordinal) ||
            token.Contains("acucares",   StringComparison.Ordinal)) return "sugar";
        if (token.Contains("carbo",      StringComparison.Ordinal)) return "carbs";
        if (token.Contains("prote",      StringComparison.Ordinal)) return "protein";
        // "gorduras saturadas" must match before the generic "gordura" guard
        if (token.Contains("satur",      StringComparison.Ordinal)) return "saturated_fat";
        // Strict: only total fat — explicitly exclude saturated and trans subtypes
        if (token.Contains("gordura",    StringComparison.Ordinal)
            && !token.Contains("satur",  StringComparison.Ordinal)
            && !token.Contains("trans",  StringComparison.Ordinal)) return "fat";
        if (token.Contains("fibra",      StringComparison.Ordinal)) return "fiber";
        if (token.Contains("sodio",      StringComparison.Ordinal)) return "sodium";
        return null;
    }

    private static void AssignValueByType(ParsedNutritionResult parsed, string nt, double v)
    {
        switch (nt)
        {
            case "calories":      parsed.Calories     ??= v; break;
            case "carbs":         parsed.Carbs        ??= v; break;
            case "sugar":         parsed.Sugar        ??= v; break;
            case "added_sugar":   parsed.AddedSugar   ??= v; break;
            case "protein":       parsed.Protein      ??= v; break;
            case "fat":           parsed.Fat          ??= v; break;
            case "saturated_fat": parsed.SaturatedFat ??= v; break;
            case "fiber":         parsed.Fiber        ??= v; break;
            case "sodium":        parsed.Sodium       ??= v; break;
        }
    }

    public static double? ExtractValue(string line, int columnIndex)
        => NutritionColumnParser.ExtractValue(line, columnIndex);
}
