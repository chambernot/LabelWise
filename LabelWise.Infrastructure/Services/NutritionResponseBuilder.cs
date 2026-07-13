using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Infrastructure.Services
{
    /// <summary>
    /// Composição passiva da UnifiedNutritionAnalysisResponse.
    ///
    /// Recebe todos os dados já computados pelo pipeline e apenas os mapeia
    /// para o contrato da API. Não calcula, não decide, não infere.
    ///
    ///   analysis  → extraído de NutritionAnalysisResponseDto (imutável)
    ///   enriched  → recebido pronto do INutritionEnricher
    ///   score     → recebido pronto do INutritionScoringService
    ///   profiles  → recebido pronto do AdvancedNutritionProfileEvaluator
    /// </summary>
    public class NutritionResponseBuilder : INutritionResponseBuilder
    {
        public UnifiedNutritionAnalysisResponse Build(
            NutritionAnalysisResponseDto pipelineResult,
            NutritionEnrichedData enriched,
            UnifiedNutritionScore score,
            UserProfileInsightsDto profiles)
        {
            enriched.PrincipalOffender = string.IsNullOrWhiteSpace(score.PrincipalOffender)
                ? "nenhum relevante"
                : score.PrincipalOffender;

            bool hasNutritionTable = pipelineResult.NutritionFlags
                .Any(f => string.Equals(f, "NutritionTable:detected", StringComparison.OrdinalIgnoreCase));

            string dataQuality = ExtractDataQuality(
                pipelineResult.NutritionFlags, hasNutritionTable, pipelineResult.EstimatedNutritionProfile);

            return new UnifiedNutritionAnalysisResponse
            {
                AnalysisId              = pipelineResult.AnalysisId,
                Success                 = pipelineResult.Success,
                ErrorMessage            = pipelineResult.ErrorMessage,
                ProcessingTimeSeconds   = pipelineResult.ProcessingTimeSeconds,
                HasNutritionTable       = hasNutritionTable,
                HasMinimumNutritionData = pipelineResult.HasReliableNutritionData,
                NutritionDataQuality    = dataQuality,
                Analysis  = ExtractAnalysisData(pipelineResult),
                Enriched  = enriched,
                Score     = score,
                Profiles  = profiles
            };
        }

        public UnifiedNutritionAnalysisResponse BuildEmpty(NutritionAnalysisResponseDto pipelineResult)
        {
            bool hasNutritionTable = pipelineResult.NutritionFlags
                .Any(f => string.Equals(f, "NutritionTable:detected", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(f, "NutritionTable:template_only", StringComparison.OrdinalIgnoreCase));

            string dataQuality = ExtractDataQuality(pipelineResult.NutritionFlags, hasNutritionTable, null);

            return new UnifiedNutritionAnalysisResponse
            {
                AnalysisId            = pipelineResult.AnalysisId,
                Success               = pipelineResult.Success,
                ErrorMessage          = pipelineResult.ErrorMessage,
                ProcessingTimeSeconds = pipelineResult.ProcessingTimeSeconds,
                HasNutritionTable       = hasNutritionTable,
                HasMinimumNutritionData = false,
                NutritionDataQuality    = dataQuality,
                Analysis = ExtractAnalysisData(pipelineResult),
                Enriched = new NutritionEnrichedData
                {
                    Confidence         = "baixa",
                    FallbackUsed       = false,
                    ProcessingLevel    = "desconhecido",
                    PrincipalOffender  = string.Empty,
                    ValidationWarnings = pipelineResult.Warnings ?? []
                },
                Score = new UnifiedNutritionScore
                {
                    Value             = 60,
                    Label             = "Dados insuficientes",
                    Color             = "gray",
                    PrincipalOffender = string.Empty
                },
                Profiles = null
            };
        }

        private static string ExtractDataQuality(
            List<string> nutritionFlags,
            bool hasNutritionTable,
            EstimatedNutritionProfileDto? profile)
        {
            var qualityFlag = nutritionFlags
                .FirstOrDefault(f => f.StartsWith("DataQuality:", StringComparison.OrdinalIgnoreCase));

            if (qualityFlag != null)
            {
                var parts = qualityFlag.Split(':');
                if (parts.Length == 2) return parts[1];
            }

            if (hasNutritionTable && profile != null)
            {
                int count = new[]
                {
                    (profile.CaloriesPer100g ?? profile.CaloriesPer100ml).HasValue,
                    profile.EstimatedProteinPer100g.HasValue,
                    profile.EstimatedFatPer100g.HasValue,
                    profile.EstimatedCarbsPer100g.HasValue,
                    profile.EstimatedSodiumPer100g.HasValue
                }.Count(v => v);

                return count >= 4 ? "full" : count >= 2 ? "partial" : "insufficient";
            }

            return "insufficient";
        }

        private static AnalysisData ExtractAnalysisData(NutritionAnalysisResponseDto result) =>
            new()
            {
                ProductName       = result.ProductName,
                Brand             = result.Brand,
                Category          = result.Category,
                PackageWeight     = result.PackageWeight,
                AnalysisMode      = result.AnalysisMode,
                VisibleClaims     = result.VisibleClaims ?? [],
                Ingredients       = result.Ingredients  ?? [],
                NutritionProfile  = result.EstimatedNutritionProfile,
                ConfidenceDetails = result.ConfidenceDetails
            };
    }
}
