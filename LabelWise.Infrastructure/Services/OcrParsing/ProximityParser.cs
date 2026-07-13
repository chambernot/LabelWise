using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Infrastructure.Services.OcrParsing;

/// <summary>
/// Camada 2 — ProximityParser.
/// Trata textos quebrados onde o rótulo e o valor estão em linhas separadas.
/// Para cada linha que contém apenas um rótulo (sem números), procura o valor
/// nas até 2 linhas seguintes.
/// Só preenche campos que ainda estão nulos — nunca sobrescreve.
/// </summary>
internal sealed class ProximityParser
{
    /// <summary>
    /// Número máximo de linhas após o rótulo a serem inspecionadas.
    /// </summary>
    private const int LookAheadLines = 2;

    public LayerParseResult Parse(IReadOnlyList<string> rawLines)
    {
        var result = new LayerParseResult();
        if (rawLines == null || rawLines.Count == 0) return result;

        // Pré-processa todas as linhas normalizadas para facilitar o look-ahead
        var lines = rawLines
            .Select(NutrientPatternBank.Normalize)
            .ToArray();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (NutrientPatternBank.ShouldIgnore(line)) continue;

            var detected = NutrientPatternBank.Detect(line);
            if (detected == null) continue;

            // Verifica se há número na mesma linha — nesse caso o LineParser já capturou
            var remainder = detected.Value.Pattern.Replace(line, " ", 1);
            var inlineNums = NutrientPatternBank.ExtractNumbers(remainder);
            if (inlineNums.Count > 0) continue; // LineParser resolve

            // Procura nas próximas LookAheadLines linhas
            for (int j = i + 1; j <= Math.Min(i + LookAheadLines, lines.Length - 1); j++)
            {
                var nextLine = lines[j];
                if (NutrientPatternBank.ShouldIgnore(nextLine)) continue;

                // Se a próxima linha é outro rótulo, parar o look-ahead
                if (NutrientPatternBank.Detect(nextLine) != null) break;

                var nums = NutrientPatternBank.ExtractNumbers(nextLine);
                if (nums.Count == 0) continue;

                AssignIfNull(result.Profile, detected.Value.Key, nums[0]);

                if (detected.Value.Key == "saturatedFat")
                    result.SaturatedFatCandidates.AddRange(nums.Where(v => v > 0));

                break; // primeiro valor encontrado no look-ahead é suficiente
            }
        }

        return result;
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
