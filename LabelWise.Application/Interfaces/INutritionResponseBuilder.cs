using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Monta a resposta unificada da API — responsabilidade exclusivamente de composição.
    /// Não calcula, não decide, não infere. Apenas mapeia dados já computados.
    /// </summary>
    public interface INutritionResponseBuilder
    {
        /// <summary>
        /// Constrói a resposta unificada a partir das quatro camadas já computadas pelo pipeline.
        /// </summary>
        UnifiedNutritionAnalysisResponse Build(
            NutritionAnalysisResponseDto pipelineResult,
            NutritionEnrichedData enriched,
            UnifiedNutritionScore score,
            UserProfileInsightsDto profiles);

        /// <summary>
        /// Constrói uma resposta mínima quando não há dados nutricionais válidos.
        /// Score e Profiles são fixos — nunca produz dados estimados.
        /// </summary>
        UnifiedNutritionAnalysisResponse BuildEmpty(NutritionAnalysisResponseDto pipelineResult);
    }
}
