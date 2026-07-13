using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Infrastructure.Services.OcrParsing;

/// <summary>
/// Camada 3 — OcrNormalizer (CRÍTICO).
/// Responsável por:
///   1. Mesclar os resultados do LineParser (prioridade) e ProximityParser (fallback leve).
///   2. Detectar se os valores extraídos estão em base "por porção" e convertê-los para 100 g.
///   3. Nunca tomar decisão unilateral — registra warnings para toda conversão realizada.
///
/// Regra de ouro: nenhum parser decide sozinho. O Normalizer é a única fonte de verdade.
/// </summary>
internal sealed class OcrNormalizer
{
    /// <summary>
    /// Mescla os resultados das duas camadas e aplica conversão de porção → 100 g quando necessário.
    /// </summary>
    /// <param name="lineResult">Resultado do LineParser — prioridade máxima.</param>
    /// <param name="proximityResult">Resultado do ProximityParser — preenche apenas nulos.</param>
    /// <param name="rawLines">Linhas brutas originais — usadas para detectar contexto de porção.</param>
    /// <param name="warnings">Lista de warnings a ser populada com conversões realizadas.</param>
    public NutritionProfile Normalize(
        LayerParseResult lineResult,
        LayerParseResult proximityResult,
        IReadOnlyList<string> rawLines,
        List<string> warnings)
    {
        // ── Passo 1: Mescla (LineParser tem prioridade) ─────────────────────────
        var merged = Merge(lineResult.Profile, proximityResult.Profile);

        // ── Passo 2: Detectar contexto de porção vs 100 g ────────────────────────
        var (isPerPortion, servingSizeG) = DetectPortionContext(rawLines);

        // ── Passo 3: Converter se necessário ─────────────────────────────────────
        if (isPerPortion && servingSizeG is > 0 and <= 500)
        {
            var factor = 100.0 / servingSizeG.Value;
            warnings.Add($"[OcrNormalizer] Valores convertidos de porção ({servingSizeG:0.#} g) para 100 g (fator {factor:0.##x}).");
            ScaleProfile(merged, factor);
        }

        return merged;
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Preenche campos nulos do alvo (line) com valores da fonte (proximity).
    /// Nunca sobrescreve valores já preenchidos.
    /// </summary>
    private static NutritionProfile Merge(NutritionProfile line, NutritionProfile proximity)
    {
        return new NutritionProfile
        {
            ProductName  = line.ProductName  ?? proximity.ProductName,
            Calories     = line.Calories     ?? proximity.Calories,
            Carbs        = line.Carbs        ?? proximity.Carbs,
            Sugar        = line.Sugar        ?? proximity.Sugar,
            AddedSugar   = line.AddedSugar   ?? proximity.AddedSugar,
            Protein      = line.Protein      ?? proximity.Protein,
            Fat          = line.Fat          ?? proximity.Fat,
            SaturatedFat = line.SaturatedFat ?? proximity.SaturatedFat,
            Fiber        = line.Fiber        ?? proximity.Fiber,
            Sodium       = line.Sodium       ?? proximity.Sodium,
        };
    }

    /// <summary>
    /// Detecta se as linhas indicam um contexto de "por porção" sem coluna "100 g" explícita.
    /// Retorna (isPerPortion, servingSizeG).
    /// </summary>
    private static (bool IsPerPortion, double? ServingSizeG) DetectPortionContext(IReadOnlyList<string> rawLines)
    {
        bool has100g         = false;
        bool hasPerPortion   = false;
        double? servingSize  = null;

        foreach (var rawLine in rawLines)
        {
            if (string.IsNullOrWhiteSpace(rawLine)) continue;
            var line = NutrientPatternBank.Normalize(rawLine);

            if (NutrientPatternBank.Per100gMarker.IsMatch(line))
                has100g = true;

            var servingMatch = NutrientPatternBank.ServingSizeDetector.Match(line);
            if (servingMatch.Success)
            {
                hasPerPortion = true;
                var raw = servingMatch.Groups[1].Value.Replace(',', '.');
                if (double.TryParse(raw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var sv))
                    servingSize = sv;
            }

            if (NutrientPatternBank.PerPortionColumnMarker.IsMatch(line))
                hasPerPortion = true;
        }

        // Só converte se: porção detectada, 100g NÃO detectado como coluna de referência,
        // e o tamanho da porção é um valor razoável (< 500 g).
        bool shouldConvert = hasPerPortion && !has100g && servingSize is > 0 and < 500;
        return (shouldConvert, servingSize);
    }

    /// <summary>
    /// Multiplica todos os campos nutricionais pelo fator de conversão.
    /// Sódio é preservado em mg (não escala de forma diferente — já está em mg/100g).
    /// </summary>
    private static void ScaleProfile(NutritionProfile p, double factor)
    {
        static double? Scale(double? v, double f) =>
            v.HasValue ? Math.Round(v.Value * f, 2) : null;

        p.Calories     = Scale(p.Calories,    factor);
        p.Carbs        = Scale(p.Carbs,       factor);
        p.Sugar        = Scale(p.Sugar,       factor);
        p.AddedSugar   = Scale(p.AddedSugar,  factor);
        p.Protein      = Scale(p.Protein,     factor);
        p.Fat          = Scale(p.Fat,         factor);
        p.SaturatedFat = Scale(p.SaturatedFat,factor);
        p.Fiber        = Scale(p.Fiber,       factor);
        p.Sodium       = Scale(p.Sodium,      factor);
    }
}
