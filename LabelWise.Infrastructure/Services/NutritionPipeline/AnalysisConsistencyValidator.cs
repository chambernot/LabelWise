using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services.NutritionPipeline;

/// <summary>
/// Valida e corrige inconsistências no NutritionAnalysisContext após cálculo de score e interpretação.
/// Garante coerência entre PrincipalOffender, labels, score e métricas.
/// </summary>
public sealed class AnalysisConsistencyValidator : IAnalysisConsistencyValidator
{
    private readonly ILogger<AnalysisConsistencyValidator> _logger;

    public AnalysisConsistencyValidator(ILogger<AnalysisConsistencyValidator> logger)
    {
        _logger = logger;
    }

    public void ValidateAndCorrect(NutritionAnalysisContext context)
    {
        EnforcePrincipalOffenderCoherence(context);
        EnforceScoreLabelCoherence(context);
        EnforceClassificationCoherence(context);
        EnforceConservativeModeWhenNoData(context);
        EnforceUltraProcessedCoherence(context);

        _logger.LogInformation(
            "[ConsistencyValidator] Score={Score}, Label={Label}, Offender={Offender}, Issues={Issues}",
            context.ScoreAdjusted, context.ScoreLabel, context.PrincipalOffender, context.ConsistencyIssues.Count);
    }

    private static void EnforcePrincipalOffenderCoherence(NutritionAnalysisContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.PrincipalOffender))
            return;

        // Se score calculation produziu um offender, propagar
        if (!string.IsNullOrWhiteSpace(context.ScoreCalculation?.ProbableOffender))
        {
            context.PrincipalOffender = context.ScoreCalculation.ProbableOffender;
            return;
        }

        // Se category decision produziu um offender, propagar
        if (!string.IsNullOrWhiteSpace(context.CategoryDecision.PreferredOffender)
            && context.CategoryDecision.PreferredOffender != "dados insuficientes")
        {
            context.PrincipalOffender = context.CategoryDecision.PreferredOffender;
            return;
        }

        // Inferir do perfil nutricional se possível
        if (context.HasReliableNutritionData && context.FinalNutritionProfile != null)
        {
            var nutrition = context.FinalNutritionProfile;
            var sugar = nutrition.EstimatedSugarPer100g ?? 0;
            var sodium = nutrition.EstimatedSodiumPer100g ?? 0;
            var fat = nutrition.EstimatedFatPer100g ?? 0;

            if (sugar >= 10) context.PrincipalOffender = "açúcar";
            else if (sodium >= 400) context.PrincipalOffender = "sódio";
            else if (fat >= 10) context.PrincipalOffender = "gordura";
            else context.PrincipalOffender = "dados insuficientes";
        }
        else
        {
            context.PrincipalOffender = "dados insuficientes";
        }
    }

    private static void EnforceScoreLabelCoherence(NutritionAnalysisContext context)
    {
        var score = context.ScoreAdjusted;

        // Se label está vazio, derivar do score
        if (string.IsNullOrWhiteSpace(context.ScoreLabel))
        {
            context.ScoreLabel = score switch
            {
                >= 85 => "Excelente escolha",
                >= 70 => "Boa escolha",
                >= 50 => "Consumo moderado",
                >= 30 => "Atenção",
                _ => "Não recomendado"
            };
        }

        // Score vs RequiresModeration
        if (score < 70 && !context.RequiresModeration)
        {
            context.RequiresModeration = true;
            context.ConsistencyIssues.Add("RequiresModeration corrigido: score < 70");
        }

        // Score muito alto + ultraprocessado = moderação obrigatória
        if (score >= 70 && context.IsUltraProcessed && !context.RequiresModeration)
        {
            context.RequiresModeration = true;
            context.ConsistencyIssues.Add("RequiresModeration forçado: ultraprocessado com score alto");
        }
    }

    private static void EnforceClassificationCoherence(NutritionAnalysisContext context)
    {
        if (!context.HasReliableNutritionData)
        {
            // Sem dados confiáveis: não permitir status positivos
            EnforceConservativeProfile(context.HealthProfiles.Diabetic, "açúcar");
            EnforceConservativeProfile(context.HealthProfiles.BloodPressure, "sódio");
            EnforceConservativeProfile(context.HealthProfiles.WeightLoss, "calorias");
            EnforceConservativeProfile(context.HealthProfiles.MuscleGain, "proteína");
        }
    }

    private static void EnforceConservativeProfile(HealthProfileResult? profile, string nutrient)
    {
        if (profile == null) return;

        var positiveStatuses = new[] { "adequado", "bom", "recomendado", "favoravel" };
        if (positiveStatuses.Any(s => profile.Status?.Contains(s, StringComparison.OrdinalIgnoreCase) == true))
        {
            profile.Status = "indeterminado";
            profile.Reason = $"Sem tabela nutricional visível, não foi possível confirmar o teor de {nutrient}.";
        }
    }

    private static void EnforceConservativeModeWhenNoData(NutritionAnalysisContext context)
    {
        if (context.HasReliableNutritionData)
            return;

        // Cap score em 55 quando não há dados confiáveis
        if (context.ScoreAdjusted > 55)
        {
            context.ConsistencyIssues.Add($"Score reduzido de {context.ScoreAdjusted} para 55: sem dados confiáveis");
            context.ScoreAdjusted = 55;
        }

        // Garantir RecommendationLevel é conservador
        if (context.RecommendationLevel is "excelente_escolha" or "boa_escolha" or "escolha_segura")
        {
            context.RecommendationLevel = "consumo_moderado";
            context.ConsistencyIssues.Add("RecommendationLevel rebaixado: sem dados confiáveis");
        }
    }

    private static void EnforceUltraProcessedCoherence(NutritionAnalysisContext context)
    {
        if (!context.IsUltraProcessed)
            return;

        // Ultraprocessado não pode ter label "Excelente escolha"
        if (context.ScoreLabel == "Excelente escolha")
        {
            context.ScoreLabel = "Boa escolha";
            context.SafeLabel = "Boa escolha";
            context.ConsistencyIssues.Add("Label rebaixado de Excelente para Boa escolha: ultraprocessado");
        }
    }
}
