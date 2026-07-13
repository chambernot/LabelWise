using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Application.Interfaces;

public interface INutritionConfidenceEngine
{
    /// <summary>
    /// Avalia a confiabilidade dos nutrientes combinando consistência matemática
    /// (porção vs 100g), limites físicos e heurísticas por nutriente.
    /// </summary>
    NutritionConfidenceResult Evaluate(OpenAiNutritionExtractionResult result);

    /// <summary>
    /// Detecta a categoria provável do produto a partir do perfil nutricional
    /// normalizado (heurística genérica, sem depender de produto específico).
    /// Retorna: "snack", "cereal", "bebida" ou "generic".
    /// </summary>
    string DetectCategory(EstimatedNutritionProfileDto profile);
}
