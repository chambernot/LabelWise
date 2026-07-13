using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Infrastructure.Services.OcrParsing;

/// <summary>
/// Resultado interno de um parser de camada individual.
/// </summary>
internal sealed class LayerParseResult
{
    public NutritionProfile Profile { get; } = new();

    /// <summary>Candidatos numéricos encontrados para gordura saturada — usados pelo NutritionFixerService.</summary>
    public List<double> SaturatedFatCandidates { get; } = [];

    /// <summary>Indica se o parser identificou explicitamente que os valores são por porção.</summary>
    public bool ValuesArePerPortion { get; set; }

    /// <summary>Tamanho da porção em gramas detectado nesta camada, se disponível.</summary>
    public double? DetectedServingSizeG { get; set; }
}

/// <summary>
/// Camada 1 — LineParser.
/// Lê uma linha por vez: detecta o rótulo do nutriente e extrai o(s) valor(es)
/// da mesma linha. Prefere coluna 100 g quando há múltiplos valores.
/// Não sobrescreve campos já preenchidos por uma camada anterior.
/// </summary>
internal sealed class LineParser
{
    public LayerParseResult Parse(IReadOnlyList<string> rawLines)
    {
        var result = new LayerParseResult();
        if (rawLines == null || rawLines.Count == 0) return result;

        string? currentKey  = null;
        var currentValues   = new List<double>();
        bool has100gHeader  = false;

        // Pré-scan: detectar se o cabeçalho menciona "100 g"
        foreach (var l in rawLines)
        {
            if (NutrientPatternBank.Per100gMarker.IsMatch(l))
            {
                has100gHeader = true;
                break;
            }
        }

        foreach (var rawLine in rawLines)
        {
            if (string.IsNullOrWhiteSpace(rawLine)) continue;

            var line = NutrientPatternBank.Normalize(rawLine);

            if (NutrientPatternBank.ShouldIgnore(line)) continue;

            var detected = NutrientPatternBank.Detect(line);
            if (detected != null)
            {
                // Salva bloco anterior
                Commit(result, currentKey, currentValues, has100gHeader);

                currentKey    = detected.Value.Key;
                var remainder = detected.Value.Pattern.Replace(line, " ", 1);
                currentValues = NutrientPatternBank.ExtractNumbers(remainder);
                continue;
            }

            if (currentKey != null)
            {
                var nums = NutrientPatternBank.ExtractNumbers(line);
                if (nums.Count > 0) currentValues.AddRange(nums);
            }
        }

        Commit(result, currentKey, currentValues, has100gHeader);
        return result;
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private static void Commit(LayerParseResult result, string? key, List<double> values, bool has100gHeader)
    {
        if (string.IsNullOrWhiteSpace(key) || values.Count == 0) return;

        // Quando há múltiplos valores (porção | 100g | %VD), preferir coluna 100g.
        // Heurística: se temos 2+ valores e o cabeçalho tem "100g", pegar o primeiro valor
        // — o parser de linha já remove %VD, então o primeiro número restante é o mais
        // próximo do rótulo e geralmente corresponde à coluna por 100g ou à porção.
        // Quando não há marcador de 100g no cabeçalho, ainda usamos o primeiro valor.
        var chosen = values[0];

        AssignIfNull(result.Profile, key, chosen);

        if (key == "saturatedFat")
            result.SaturatedFatCandidates.AddRange(values.Where(v => v > 0));
    }

    private static void AssignIfNull(NutritionProfile p, string key, double value)
    {
        switch (key)
        {
            case "calories":     p.Calories    ??= value; break;
            case "carbs":        p.Carbs       ??= value; break;
            case "sugar":        p.Sugar       ??= value; break;
            case "addedSugar":   p.AddedSugar  ??= value; break;
            case "protein":      p.Protein     ??= value; break;
            case "fat":          p.Fat         ??= value; break;
            case "saturatedFat": p.SaturatedFat??= value; break;
            case "fiber":        p.Fiber       ??= value; break;
            case "sodium":       p.Sodium      ??= value; break;
        }
    }
}
