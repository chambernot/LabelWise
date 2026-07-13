using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Infrastructure.Services;

/// <summary>
/// Motor de confiabilidade nutricional.
///
/// Para cada nutriente combina três pilares:
///   1. Consistência matemática entre os valores por porção e por 100g
///   2. Limites físicos (ex.: gordura > 100g/100g é impossível)
///   3. Heurísticas inter-nutrientes (ex.: muita fibra + muito açúcar é incomum)
///
/// O resultado por campo é o MENOR (mais conservador) entre os pilares,
/// garantindo que qualquer anomalia rebaixe a confiança.
/// </summary>
public sealed class NutritionConfidenceEngine : INutritionConfidenceEngine
{
    public NutritionConfidenceResult Evaluate(OpenAiNutritionExtractionResult result)
    {
        var confidence = new NutritionConfidenceResult();

        var per100 = result.NutritionPer100g;
        var perServing = result.NutritionPerServing;
        var servingAmount = result.Serving?.Amount ?? 0;
        var factor = servingAmount > 0 ? 100.0 / servingAmount : 0;

        // Quando a porção não foi declarada (ou foi anulada por validação),
        // tentamos derivar o fator a partir das próprias colunas. Se as razões
        // per100/perServing dos diferentes nutrientes convergem para um único
        // valor, isso é evidência forte de que a tabela é internamente
        // consistente — e nos permite avaliar a consistência cruzada
        // normalmente, em vez de penalizar todos os campos com Low.
        if (factor <= 0 && per100 != null && perServing != null)
        {
            factor = InferFactor(per100, perServing);
        }

        // Valores efetivos em base 100g (per100 quando disponível, senão derivados da porção).
        var calories = Effective(per100?.CaloriesKcal, perServing?.CaloriesKcal, factor);
        var carbs    = Effective(per100?.Carbohydrates, perServing?.Carbohydrates, factor);
        var protein  = Effective(per100?.Proteins, perServing?.Proteins, factor);
        var fat      = Effective(per100?.TotalFats, perServing?.TotalFats, factor);
        var satFat   = Effective(per100?.SaturatedFats, perServing?.SaturatedFats, factor);
        var fiber    = Effective(per100?.Fiber, perServing?.Fiber, factor);
        var sodium   = Effective(per100?.SodiumMg, perServing?.SodiumMg, factor);
        var sugar    = Effective(per100?.Sugar, perServing?.Sugar, factor);
        var addedSug = Effective(per100?.AddedSugar, perServing?.AddedSugar, factor);

        confidence.Fields["calories"] = Min(
            EvaluateConsistency(per100?.CaloriesKcal, perServing?.CaloriesKcal, factor, toleranceAbs: 15, toleranceRel: 0.10),
            EvaluateCalories(calories, carbs, protein, fat));

        confidence.Fields["carbs"] = Min(
            EvaluateConsistency(per100?.Carbohydrates, perServing?.Carbohydrates, factor),
            EvaluateCarbs(carbs));

        confidence.Fields["protein"] = Min(
            EvaluateConsistency(per100?.Proteins, perServing?.Proteins, factor),
            EvaluateProtein(protein));

        confidence.Fields["fat"] = Min(
            EvaluateConsistency(per100?.TotalFats, perServing?.TotalFats, factor),
            EvaluateFat(fat));

        confidence.Fields["satFat"] = Min(
            EvaluateConsistency(per100?.SaturatedFats, perServing?.SaturatedFats, factor),
            EvaluateSaturatedFat(satFat, fat));

        confidence.Fields["fiber"] = Min(
            EvaluateConsistency(per100?.Fiber, perServing?.Fiber, factor),
            EvaluateFiber(fiber));

        confidence.Fields["sodium"] = Min(
            EvaluateConsistency(per100?.SodiumMg, perServing?.SodiumMg, factor, toleranceAbs: 50, toleranceRel: 0.10),
            EvaluateSodium(sodium));

        confidence.Fields["sugar"] = Min(
            EvaluateConsistency(per100?.Sugar, perServing?.Sugar, factor),
            EvaluateSugar(sugar, fiber, carbs));

        confidence.Fields["addedSugar"] = Min(
            EvaluateConsistency(per100?.AddedSugar, perServing?.AddedSugar, factor),
            EvaluateAddedSugar(addedSug, sugar));

        if (confidence.Fields.Count > 0)
        {
            // GlobalScore ponderado:
            //   High   → 1.0
            //   Medium → 0.7
            //   Low    → 0.3
            //   None   → ignorado (campo ausente não deve depreciar o score global)
            var weighted = confidence.Fields.Values
                .Where(v => v != FieldConfidence.None)
                .Select(MapConfidenceWeight)
                .ToList();

            confidence.GlobalScore = weighted.Count > 0 ? weighted.Average() : 0d;
        }

        return confidence;
    }

    private static double MapConfidenceWeight(FieldConfidence c) => c switch
    {
        FieldConfidence.High   => 1.0,
        FieldConfidence.Medium => 0.7,
        FieldConfidence.Low    => 0.3,
        _                      => 0.0
    };

    public string DetectCategory(EstimatedNutritionProfileDto profile)
    {
        var fat    = profile.EstimatedFatPer100g ?? 0;
        var carbs  = profile.EstimatedCarbsPer100g ?? 0;
        var sodium = profile.EstimatedSodiumPer100g ?? 0;

        // Snack salgado/doce denso: muita gordura + muito carboidrato
        if (fat > 20 && carbs > 30)
            return "snack";

        // Cereal/farináceo: predominância de carboidrato com pouca gordura
        if (carbs > 70 && fat < 5)
            return "cereal";

        // Bebida: muito pouco sódio e muito pouca gordura
        if (sodium < 50 && fat < 2)
            return "bebida";

        return "generic";
    }

    // ── Consistência matemática (porção vs 100g) ──────────────────────────
    private static FieldConfidence EvaluateConsistency(
        double? per100,
        double? perServing,
        double factor,
        double toleranceAbs = 5,
        double toleranceRel = 0.10)
    {
        if (!per100.HasValue && !perServing.HasValue)
            return FieldConfidence.None;

        // Apenas um lado disponível: a consistência cruzada é NEUTRA, não penaliza.
        // O valor já passa pelos demais pilares (limites físicos + sinais cruzados),
        // que são suficientes para classificar como confiável quando consistentes.
        if (per100.HasValue ^ perServing.HasValue)
            return FieldConfidence.High;

        if (factor <= 0)
            return FieldConfidence.High;
            // Sem fator conhecido (e sem fator inferível das próprias colunas),
            // a checagem cruzada é inconclusiva — não penalizamos.
            // Os outros pilares (limites físicos + cruzamentos de nutrientes)
            // são suficientes para classificar a confiabilidade do campo.

        var expected = perServing!.Value * factor;
        var actual = per100!.Value;
        var diff = Math.Abs(expected - actual);
        var relTol = Math.Max(toleranceAbs, Math.Abs(actual) * toleranceRel);

        if (diff <= relTol)
            return FieldConfidence.High;
        if (diff <= relTol * 3)
            return FieldConfidence.Medium;

        return FieldConfidence.Low;
    }

    // ── Heurísticas por nutriente (limites físicos + sinais cruzados) ────

    private static FieldConfidence EvaluateCalories(double? cal, double? carbs, double? protein, double? fat)
    {
        if (!cal.HasValue) return FieldConfidence.None;
        if (cal < 0 || cal > 900) return FieldConfidence.Low;

        if (carbs.HasValue && protein.HasValue && fat.HasValue)
        {
            var calc = carbs.Value * 4 + protein.Value * 4 + fat.Value * 9;
            var diff = Math.Abs(calc - cal.Value);
            if (diff > 100) return FieldConfidence.Low;
            if (diff > 40)  return FieldConfidence.Medium;
        }

        return FieldConfidence.High;
    }

    private static FieldConfidence EvaluateCarbs(double? carbs)
    {
        if (!carbs.HasValue) return FieldConfidence.None;
        if (carbs < 0 || carbs > 100) return FieldConfidence.Low;
        return FieldConfidence.High;
    }

    private static FieldConfidence EvaluateProtein(double? protein)
    {
        if (!protein.HasValue) return FieldConfidence.None;
        if (protein < 0 || protein > 90) return FieldConfidence.Low;
        if (protein > 40) return FieldConfidence.Medium;
        return FieldConfidence.High;
    }

    private static FieldConfidence EvaluateFat(double? fat)
    {
        if (!fat.HasValue) return FieldConfidence.None;
        if (fat < 0 || fat > 100) return FieldConfidence.Low;
        return FieldConfidence.High;
    }

    private static FieldConfidence EvaluateSaturatedFat(double? sat, double? fat)
    {
        if (!sat.HasValue) return FieldConfidence.None;
        if (sat < 0 || sat > 100) return FieldConfidence.Low;

        // Saturada não pode exceder a gordura total (com pequena tolerância).
        if (fat.HasValue && sat > fat.Value + 0.5) return FieldConfidence.Low;

        return FieldConfidence.High;
    }

    private static FieldConfidence EvaluateFiber(double? fiber)
    {
        if (!fiber.HasValue) return FieldConfidence.None;
        if (fiber < 0) return FieldConfidence.Low;
        if (fiber > 30) return FieldConfidence.Low;       // praticamente impossível em alimentos comuns
        if (fiber > 10) return FieldConfidence.Medium;    // possível, mas raro
        return FieldConfidence.High;
    }

    private static FieldConfidence EvaluateSodium(double? sodiumMg)
    {
        if (!sodiumMg.HasValue) return FieldConfidence.None;
        if (sodiumMg < 0) return FieldConfidence.Low;
        if (sodiumMg > 5000) return FieldConfidence.Low;  // limite físico em alimentos regulares
        return FieldConfidence.High;
    }

    /// <summary>
    /// Açúcar é nutriente CRÍTICO — qualquer sinal anômalo rebaixa a confiança.
    /// </summary>
    private static FieldConfidence EvaluateSugar(double? sugar, double? fiber, double? carbs)
    {
        if (!sugar.HasValue) return FieldConfidence.None;
        if (sugar < 0) return FieldConfidence.Low;

        // Limite físico
        if (sugar > 100) return FieldConfidence.Low;

        // Açúcar não pode exceder o total de carboidratos (com pequena tolerância).
        if (carbs.HasValue && sugar > carbs.Value + 0.5) return FieldConfidence.Low;

        // Sinal cruzado: alta fibra + alto açúcar é incomum.
        if (fiber > 6 && sugar > 10) return FieldConfidence.Low;

        // Valores extremos pedem confirmação (regra forte do RuleEngine).
        if (sugar > 50) return FieldConfidence.Low;
        if (sugar > 30) return FieldConfidence.Medium;

        return FieldConfidence.High;
    }

    private static FieldConfidence EvaluateAddedSugar(double? added, double? totalSugar)
    {
        if (!added.HasValue) return FieldConfidence.None;
        if (added < 0) return FieldConfidence.Low;
        if (added > 100) return FieldConfidence.Low;

        // Adicionado não pode exceder o açúcar total (quando ambos disponíveis).
        if (totalSugar.HasValue && added > totalSugar.Value + 0.5) return FieldConfidence.Low;

        // Valores extremos pedem confirmação.
        if (added > 50) return FieldConfidence.Medium;

        return FieldConfidence.High;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static double? Effective(double? per100, double? perServing, double factor)
    {
        if (per100.HasValue) return per100;
        if (perServing.HasValue && factor > 0) return perServing.Value * factor;
        return null;
    }

    /// <summary>
    /// Infere o fator (100 / porção) a partir das razões per100/perServing
    /// dos nutrientes presentes em ambas as colunas.
    ///
    /// Se a maioria das razões converge para um único valor (mediana com
    /// dispersão pequena), aceita esse valor como fator. Caso contrário,
    /// devolve 0 (sem inferência confiável) e o pilar de consistência será
    /// neutralizado pelos demais.
    /// </summary>
    private static double InferFactor(OpenAiNutritionInfo per100, OpenAiNutritionInfo perServing)
    {
        var ratios = new List<double>();

        void TryAdd(double? a, double? b)
        {
            // Ignoramos zeros e valores triviais para evitar razões instáveis.
            if (a is > 1 && b is > 0.1)
                ratios.Add(a.Value / b.Value);
        }

        TryAdd(per100.Carbohydrates, perServing.Carbohydrates);
        TryAdd(per100.Proteins,      perServing.Proteins);
        TryAdd(per100.TotalFats,     perServing.TotalFats);
        TryAdd(per100.SaturatedFats, perServing.SaturatedFats);
        TryAdd(per100.CaloriesKcal,  perServing.CaloriesKcal);
        TryAdd(per100.SodiumMg,      perServing.SodiumMg);
        TryAdd(per100.Fiber,         perServing.Fiber);
        TryAdd(per100.Sugar,         perServing.Sugar);

        if (ratios.Count < 2)
            return 0;

        ratios.Sort();
        var median = ratios[ratios.Count / 2];

        // Considera convergente se 70%+ das razões caem dentro de ±15% da mediana.
        var inBand = ratios.Count(r => Math.Abs(r - median) / median <= 0.15);
        if (inBand * 1.0 / ratios.Count < 0.70)
            return 0;

        return median;
    }

    private static FieldConfidence Min(FieldConfidence a, FieldConfidence b)
        => (FieldConfidence)Math.Min((int)a, (int)b);
}
