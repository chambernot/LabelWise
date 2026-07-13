using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using LabelWise.Application.Models.Nutrition;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.AI.Parsers;

/// <summary>
/// Estratégia flexível: varre todas as células de todas as tabelas detectadas
/// e extrai valores nutricionais via regex, sem exigir estrutura de grid perfeita.
///
/// Útil para tabelas com células mescladas, colunas irregulares ou layouts
/// onde o modelo estruturado falha.
/// </summary>
internal sealed class TableFlexibleParser : INutritionParser
{
    private readonly ILogger _logger;

    public string Name => "TableFlexible";

    // ── Label → field key (PT + ES + EN), do mais específico ao mais genérico ──
    private static readonly (string Key, string Field)[] LabelMap =
    [
        ("valor energetico",     "calories"),
        ("valor calorico",       "calories"),
        ("energia total",        "calories"),
        ("energia",              "calories"),
        ("calorias",             "calories"),
        ("calories",             "calories"),
        ("energy",               "calories"),
        ("gorduras saturadas",   "saturatedFat"),
        ("gordura saturada",     "saturatedFat"),
        ("grasas saturadas",     "saturatedFat"),
        ("grasa saturada",       "saturatedFat"),
        ("saturated fat",        "saturatedFat"),
        ("gorduras totais",      "fat"),
        ("gordura total",        "fat"),
        ("grasas totales",       "fat"),
        ("gorduras",             "fat"),
        ("gordura",              "fat"),
        ("grasas",               "fat"),
        ("total fat",            "fat"),
        ("lipidos",              "fat"),
        ("acucares adicionados", "addedSugar"),
        ("acucar adicionado",    "addedSugar"),
        ("azucares anadidos",    "addedSugar"),
        ("added sugars",         "addedSugar"),
        ("acucares totais",      "sugar"),
        ("acucares",             "sugar"),
        ("azucares totales",     "sugar"),
        ("azucares",             "sugar"),
        ("sugars",               "sugar"),
        ("carboidratos",         "carbs"),
        ("carbohidratos",        "carbs"),
        ("hidratos de carbono",  "carbs"),
        ("carbohydrates",        "carbs"),
        ("carbohydrate",         "carbs"),
        ("proteinas",            "protein"),
        ("proteina",             "protein"),
        ("protein",              "protein"),
        ("fibras alimentares",   "fiber"),
        ("fibra alimentar",      "fiber"),
        ("fibra dietetica",      "fiber"),
        ("fibras",               "fiber"),
        ("fibra",                "fiber"),
        ("dietary fiber",        "fiber"),
        ("sodio",                "sodium"),
        ("sodium",               "sodium"),
    ];

    private static readonly Regex NumberRegex =
        new(@"(?<![.\d])(\d+(?:[.,]\d+)?)(?![.\d])\s*(?:g|mg|kcal|kj)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public TableFlexibleParser(ILogger logger)
    {
        _logger = logger;
    }

    public DocumentIntelligenceNutritionResult? Parse(AnalyzeResult result)
    {
        if (result.Tables.Count == 0)
            return null;

        var raw = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in result.Tables)
        {
            // Flatten all cells into a row-ordered list
            var cells = table.Cells.OrderBy(c => c.RowIndex).ThenBy(c => c.ColumnIndex).ToList();

            // Try to extract by scanning each cell as potential label
            foreach (var cell in cells)
            {
                string norm = NormalizeCell(cell.Content);
                string? fieldKey = ResolveFieldKey(norm);
                if (fieldKey is null || raw.ContainsKey(fieldKey)) continue;

                // Look for a number in the same cell first, then same-row cells
                double? value = ExtractNumberFromCell(cell.Content, fieldKey)
                             ?? FindValueInSameRow(table, cell, fieldKey);

                if (value.HasValue)
                {
                    raw[fieldKey] = value.Value;
                    _logger.LogDebug(
                        "[TableFlexible] campo={Field} valor={Value} (linha={Row})",
                        fieldKey, value.Value, cell.RowIndex);
                }
            }
        }

        if (raw.Count == 0)
            return null;

        var output = new DocumentIntelligenceNutritionResult
        {
            ExtractionMode    = "TABLE_FLEXIBLE",
            HasNutritionTable = true
        };

        MapRawToResult(raw, output);
        return output;
    }

    public int GetConfidenceScore(DocumentIntelligenceNutritionResult result)
        => NutritionParserScoring.Calculate(result);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string? ResolveFieldKey(string normalizedLabel)
    {
        foreach (var (key, field) in LabelMap)
            if (normalizedLabel.Contains(key))
                return field;
        return null;
    }

    private static double? FindValueInSameRow(DocumentTable table, DocumentTableCell labelCell, string fieldKey)
    {
        var sameRowCells = table.Cells
            .Where(c => c.RowIndex == labelCell.RowIndex && c.ColumnIndex > labelCell.ColumnIndex)
            .OrderBy(c => c.ColumnIndex);

        foreach (var cell in sameRowCells)
        {
            var v = ExtractNumberFromCell(cell.Content, fieldKey);
            if (v.HasValue) return v;
        }
        return null;
    }

    private static double? ExtractNumberFromCell(string content, string fieldKey)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        foreach (Match m in NumberRegex.Matches(content))
        {
            string raw = m.Groups[1].Value.Replace(',', '.');
            if (!double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double v))
                continue;
            if (IsPlausible(fieldKey, v))
                return v;
        }
        return null;
    }

    private static string NormalizeCell(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        string decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);

        return sb.ToString()
                 .Normalize(NormalizationForm.FormC)
                 .ToLowerInvariant()
                 .Trim();
    }

    private static bool IsPlausible(string fieldKey, double value) => fieldKey switch
    {
        "calories"     => value >= 10  && value <= 900,
        "fat"          => value >= 0   && value <= 100,
        "saturatedFat" => value >= 0   && value <= 100,
        "protein"      => value >= 0   && value <= 100,
        "carbs"        => value >= 0   && value <= 100,
        "sugar"        => value >= 0   && value <= 100,
        "addedSugar"   => value >= 0   && value <= 100,
        "sodium"       => value >= 0   && value <= 5000,
        "fiber"        => value >= 0   && value <= 100,
        _              => true
    };

    private static void MapRawToResult(Dictionary<string, double> raw, DocumentIntelligenceNutritionResult r)
    {
        NutritionField? Field(string key) =>
            raw.TryGetValue(key, out double v) ? new NutritionField { Value = v } : null;

        r.Calories     = Field("calories");
        r.Carbs        = Field("carbs");
        r.Sugar        = Field("sugar");
        r.AddedSugar   = Field("addedSugar");
        r.Protein      = Field("protein");
        r.Fat          = Field("fat");
        r.SaturatedFat = Field("saturatedFat");
        r.Sodium       = Field("sodium");
        r.Fiber        = Field("fiber");
    }
}
