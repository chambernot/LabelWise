using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;
using LabelWise.Domain.Models;
using LabelWise.Infrastructure.Services.IngredientAnalysis;

namespace LabelWise.Infrastructure.Services.FoodAnalysis;

/// <summary>
/// Engine NOVA determinística baseada em sinais industriais e confiança da evidência.
/// </summary>
public sealed class FoodProcessingEngine
{
    internal static readonly string[] UltraProcessedSignals =
    [
        "emulsificante", "conservante", "aromatizante", "aroma artificial", "flavorizante", "estabilizante",
        "edulcorante", "corante", "realcador de sabor", "glutamato monossodico", "gordura hidrogenada",
        "gordura vegetal hidrogenada", "oleo interesterificado", "xarope de milho", "xarope de glicose",
        "maltodextrina", "dextrose", "aspartame", "sucralose", "acessulfame", "carragena",
        "goma xantana", "goma guar", "mono e diglicerideos", "proteina isolada", "proteina texturizada"
    ];

    internal static readonly string[] ProcessedSignals =
    [
        "sal", "acucar", "oleo vegetal", "vinagre", "fermento", "queijo", "conserva", "defumado"
    ];

    private readonly ProcessingConfidenceGate _confidenceGate;

    public FoodProcessingEngine(ProcessingConfidenceGate confidenceGate)
    {
        _confidenceGate = confidenceGate;
    }

    public FoodProcessingDecision Evaluate(DecisionInput input, IReadOnlyList<string> ingredients, double confidence)
    {
        var gate = _confidenceGate.Evaluate(input, ingredients, confidence);
        if (!gate.CanClassify)
            return FoodProcessingDecision.Blocked(gate.Reasons);

        var normalizedIngredients = ingredients.Select(IngredientTextNormalizer.Normalize).ToList();
        var ultraSignals = FindSignals(normalizedIngredients, UltraProcessedSignals).ToList();
        var processedSignals = FindSignals(normalizedIngredients, ProcessedSignals).ToList();
        var industrialDensity = Math.Min(1d, (ultraSignals.Count * 0.22) + (processedSignals.Count * 0.08) + Math.Max(0, ingredients.Count - 5) * 0.03);

        if (ultraSignals.Count >= 2 || industrialDensity >= 0.42)
        {
            return new FoodProcessingDecision(
                ProcessingLevel.UltraProcessed,
                15,
                confidence >= 0.70 ? "high" : "medium",
                ultraSignals,
                ["Foram detectados múltiplos sinais industriais típicos de ultraprocessados."]);
        }

        if (ultraSignals.Count == 1)
        {
            return new FoodProcessingDecision(
                ProcessingLevel.UltraProcessed,
                30,
                "medium",
                ultraSignals,
                ["Foi detectado sinal industrial forte de ultraprocessamento."]);
        }

        if (processedSignals.Count > 0 || ingredients.Count > 5)
        {
            return new FoodProcessingDecision(
                ProcessingLevel.Processed,
                55,
                confidence >= 0.70 ? "high" : "medium",
                processedSignals,
                ["Composição indica alimento processado, sem sinais fortes suficientes de ultraprocessamento."]);
        }

        return new FoodProcessingDecision(
            ProcessingLevel.MinimallyProcessed,
            85,
            confidence >= 0.70 ? "high" : "medium",
            [],
            ["Foram detectados poucos ingredientes e nenhum sinal industrial relevante."]);
    }

    private static IEnumerable<string> FindSignals(IReadOnlyList<string> ingredients, IReadOnlyList<string> signals)
    {
        foreach (var signal in signals)
        {
            if (ingredients.Any(ingredient => ingredient.Contains(signal, StringComparison.OrdinalIgnoreCase)))
                yield return signal;
        }
    }
}

public sealed record FoodProcessingDecision(
    ProcessingLevel Level,
    int ProcessingScore,
    string Confidence,
    IReadOnlyList<string> IndustrialSignals,
    IReadOnlyList<string> Reasons,
    bool Hidden = false,
    bool SafeModeBlocked = false)
{
    public static FoodProcessingDecision Blocked(IReadOnlyList<string> reasons) => new(
        ProcessingLevel.Unknown,
        0,
        "low",
        [],
        reasons,
        Hidden: true,
        SafeModeBlocked: true);
}

public sealed class ProcessingConfidenceGate
{
    private const double MinimumDecisionConfidence = 0.60;
    private const int MinimumIngredientCountForLowRiskClassification = 2;

    public ProcessingConfidenceGateResult Evaluate(DecisionInput input, IReadOnlyList<string> ingredients, double confidence)
    {
        var reasons = new List<string>();

        if (confidence < MinimumDecisionConfidence)
            reasons.Add("Confiança insuficiente para classificar o nível de processamento.");

        if (input.AnalysisQuality is AnalysisQuality.Insufficient or AnalysisQuality.Inconsistent or AnalysisQuality.Partial)
            reasons.Add("Leitura parcial ou inconsistente bloqueia classificação NOVA definitiva.");

        if (ingredients.Count == 0)
            reasons.Add("Lista de ingredientes ausente ou ilegível.");
        else if (ingredients.Count < MinimumIngredientCountForLowRiskClassification && !HasIndustrialSignal(ingredients))
            reasons.Add("Ingredientes insuficientes para diferenciar produto simples de formulação industrial.");

        if (input.Conflicts.Any(conflict => conflict.Severity is ConflictSeverity.Moderate or ConflictSeverity.Critical))
            reasons.Add("Conflitos semânticos impedem classificação de processamento segura.");

        if (input.SemanticInferences.Count > input.ExplicitIngredients.Count && input.SemanticInferences.Count > 0)
            reasons.Add("Incerteza semântica alta: há mais inferências que ingredientes confirmados.");

        return new ProcessingConfidenceGateResult(reasons.Count == 0, reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static bool HasIndustrialSignal(IReadOnlyList<string> ingredients)
    {
        var normalizedIngredients = ingredients.Select(IngredientTextNormalizer.Normalize).ToList();
        return FoodProcessingEngine.UltraProcessedSignals.Concat(FoodProcessingEngine.ProcessedSignals)
            .Any(signal => normalizedIngredients.Any(ingredient => ingredient.Contains(signal, StringComparison.OrdinalIgnoreCase)));
    }
}

public sealed record ProcessingConfidenceGateResult(bool CanClassify, IReadOnlyList<string> Reasons);
