using LabelWise.Application.DTOs.AI;
using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// Validador de sanidade pós-parser: detecta inconsistências, corrige o que for seguro,
/// recalcula confiança e decide se o score pode ser calculado de forma confiável.
/// Deve ser executado ANTES do cálculo de score (Stage 7 do pipeline).
/// </summary>
public interface INutritionParseSanityValidator
{
    /// <summary>
    /// Valida o estado atual do contexto de análise e retorna um resultado com correções,
    /// warnings, inconsistências e flags de reprocessamento/score.
    /// </summary>
    NutritionSanityResult Validate(NutritionAnalysisContext context, VisualInterpretationResult visionResult);
}
