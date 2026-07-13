using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Models.Nutrition;

/// <summary>
/// Resultado da validação de sanidade pós-parser do perfil nutricional.
/// Produzido por INutritionParseSanityValidator antes do cálculo de score.
/// </summary>
public sealed class NutritionSanityResult
{
    /// <summary>Perfil nutricional após correções seguras aplicadas pelo validador.</summary>
    public EstimatedNutritionProfileDto ValidatedNutrition { get; init; } = new();

    /// <summary>Modo de análise interno corrigido (se necessário).</summary>
    public NutritionDecisionMode? CorrectedAnalysisMode { get; init; }

    /// <summary>Modo de análise público corrigido (se necessário).</summary>
    public AnalysisMode? CorrectedPublicAnalysisMode { get; init; }

    /// <summary>Confiança recalculada do parser: "high", "medium" ou "low".</summary>
    public string ParserConfidence { get; init; } = "low";

    /// <summary>Avisos sobre campos ausentes, confiança reduzida ou inconsistências leves.</summary>
    public List<string> Warnings { get; init; } = [];

    /// <summary>Conflitos fortes: valores deslocados, modo incorreto, porção incompatível.</summary>
    public List<string> Inconsistencies { get; init; } = [];

    /// <summary>Indica se o pipeline deveria tentar reprocessar a imagem para melhorar a extração.</summary>
    public bool ShouldReprocess { get; init; }

    /// <summary>Indica se há base nutricional suficiente para calcular um score confiável.</summary>
    public bool CanScoreReliably { get; init; }
}
