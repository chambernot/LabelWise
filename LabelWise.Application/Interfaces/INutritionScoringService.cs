using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Calcula o score nutricional unificado a partir dos dados enriquecidos pelo backend.
    /// Esta é a única fonte de verdade para pontuação — substitui nutritionalScore e advancedScore.
    /// </summary>
    public interface INutritionScoringService
    {
        /// <summary>
        /// Calcula o score nutricional baseado no perfil normalizado e no nível de processamento.
        /// Nunca usa dados da IA sem validação prévia.
        /// </summary>
        /// <param name="enriched">Dados enriquecidos produzidos pelo INutritionDataValidatorService.</param>
        UnifiedNutritionScore Calculate(NutritionEnrichedData enriched);
    }
}
