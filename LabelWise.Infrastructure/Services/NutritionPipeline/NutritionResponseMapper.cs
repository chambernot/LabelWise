using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services.NutritionPipeline;

/// <summary>
/// Mapeia o NutritionAnalysisContext para o NutritionAnalysisResponseDto final.
/// Centraliza toda a lógica de conversão do pipeline interno para o DTO público.
/// </summary>
public sealed class NutritionResponseMapper : INutritionResponseMapper
{
    private readonly ILogger<NutritionResponseMapper> _logger;

    public NutritionResponseMapper(ILogger<NutritionResponseMapper> logger)
    {
        _logger = logger;
    }

    public NutritionAnalysisResponseDto Map(NutritionAnalysisContext context)
    {
        var response = new NutritionAnalysisResponseDto
        {
            AnalysisId = context.AnalysisId,
            Success = context.Success,
            ProductName = context.ProductName,
            Brand = context.Brand,
            Category = context.CategoryNormalized ?? context.CategoryRaw,
            PackageWeight = context.PackageWeight,
            AnalysisMode = context.PublicAnalysisMode,
            VisibleClaims = context.VisibleClaims,
            EstimatedNutritionProfile = context.FinalNutritionProfile,
            // Score/Classification/Profiles bloqueados quando nenhum dado nutricional válido foi extraído.
            // Nunca expor score ou perfis gerados a partir de categoria ou estimativas.
            Classification = context.BlockScoreAndProfiles ? null : context.HealthProfiles,
            Summary = context.Summary,
            ConfidenceDetails = context.ConfidenceDetails,
            Warnings = context.Warnings,
            ErrorMessage = context.ErrorMessage,
            ProcessingTimeSeconds = context.ProcessingTimeSeconds,
            Alerts = context.Alerts,
            PrincipalOffender = context.BlockScoreAndProfiles ? string.Empty : context.PrincipalOffender,
            Profiles = context.BlockScoreAndProfiles
                ? null
                : AdvancedNutritionProfileEvaluator.Evaluate(
                    context.FinalNutritionProfile,
                    context.ScoreAdjusted,
                    context.PrincipalOffender,
                    context.CategoryNormalized ?? context.CategoryRaw),
            ResumoRapido = context.BlockScoreAndProfiles ? [] : context.ResumoRapido,
            ExplicacaoScore = context.BlockScoreAndProfiles ? null : context.ExplicacaoScore,
            PontoPrincipal = context.BlockScoreAndProfiles ? null : context.PontoPrincipal,
            Tom = context.Tom,
            HasReliableNutritionData = context.HasReliableNutritionData,
            DataSource = context.NutritionDataSource.ToString(),
            ProductForm = context.ProductForm.ToString(),
            IsInconsistent = context.IsInconsistent,
            IsNutritionLocked = context.IsNutritionLocked,
            NutritionFlags = context.NutritionFlags,
            FallbackType = context.FallbackType,
            InferredRisks = context.InferredRisks,
            Ingredients = ExtractIngredients(context),
        };

        // Score nulo quando não há dados nutricionais válidos — nunca produzir score enganoso.
        response.Score = context.BlockScoreAndProfiles ? null : BuildScoreDto(context);

        _logger.LogInformation(
            "[ResponseMapper] Success={Success}, Score={Score}, Label={Label}, Offender={Offender}",
            response.Success, response.Score?.Value, response.Score?.Label, response.PrincipalOffender);

        return response;
    }

    private static NutritionalScore BuildScoreDto(NutritionAnalysisContext context)
    {
        var interpretation = context.ScoreInterpretation;

        var score = new NutritionalScore
        {
            Value = context.ScoreAdjusted,
            Label = interpretation?.Label ?? context.ScoreLabel,
            SafeLabel = interpretation?.SafeLabel ?? context.SafeLabel,
            Status = interpretation?.Status ?? DeriveStatus(context.ScoreAdjusted),
            Color = interpretation?.Color ?? DeriveColor(context.ScoreAdjusted),
            Reason = BuildReason(context),
            RecommendationLevel = interpretation?.RecommendationLevel ?? context.RecommendationLevel,
            SemanticRecommendation = interpretation?.SemanticRecommendation ?? interpretation?.AbsoluteRecommendation ?? string.Empty,
            AbsoluteRecommendation = interpretation?.AbsoluteRecommendation ?? string.Empty,
            ComparativeRecommendation = interpretation?.ComparativeRecommendation ?? string.Empty,
            ScoreInterpretation = interpretation?.ScoreInterpretation ?? $"Score {context.ScoreAdjusted}/100.",
            AbsoluteLabel = interpretation?.AbsoluteLabel ?? DeriveStatus(context.ScoreAdjusted),
            ComparativeLabel = string.Empty,
            ProcessingLevel = context.ProcessingLevel,
            RequiresModeration = context.RequiresModeration,
            IsUltraProcessed = context.IsUltraProcessed,
            Confidence = context.NutritionDataSource == DataSource.Real && !context.IsInconsistent ? "alta" : context.HasReliableNutritionData ? "media" : "baixa",
            PrincipalOffender = context.PrincipalOffender
        };

        // Ajustar confidence se dados completos
        if (context.HasReliableNutritionData && context.FinalNutritionProfile != null)
        {
            var presentFields = new[]
            {
                context.FinalNutritionProfile.EstimatedSugarPer100g,
                context.FinalNutritionProfile.EstimatedFatPer100g,
                context.FinalNutritionProfile.EstimatedSodiumPer100g,
                context.FinalNutritionProfile.EstimatedProteinPer100g,
                context.FinalNutritionProfile.EstimatedFiberPer100g
            }.Count(v => v.HasValue);

            score.Confidence = presentFields >= 5 && context.PublicAnalysisMode == AnalysisMode.FullNutritionLabel
                ? "alta"
                : presentFields >= 3 ? "media" : "baixa";
        }

        return score;
    }

    private static string BuildReason(NutritionAnalysisContext context)
    {
        if (!context.HasReliableNutritionData)
        {
            var categoryName = context.CategoryNormalized ?? context.CategoryRaw ?? "produto";
            return $"Pontuação calculada qualitativamente pelo perfil típico de {categoryName}, com baixa confiança (sem extração quantitativa da tabela nutricional).";
        }

        var calc = context.ScoreCalculation;
        if (calc == null)
            return "Pontuação calculada a partir do equilíbrio geral do perfil nutricional disponível.";

        var parts = new List<string>();
        if (calc.Penalties.Count > 0)
            parts.Add($"Pontuação reduzida por {string.Join(", ", calc.Penalties.Take(3).Select(p => p.Reason))}");
        if (calc.Bonuses.Count > 0)
            parts.Add($"com ganho por {string.Join(" e ", calc.Bonuses.Select(b => b.Reason))}");
        if (!string.IsNullOrWhiteSpace(calc.ProbableOffender))
            parts.Add($"principal ponto de atenção: {calc.ProbableOffender}");

        return parts.Count > 0
            ? string.Join(", ", parts) + "."
            : "Pontuação calculada a partir do equilíbrio geral do perfil nutricional disponível.";
    }

    private static string DeriveStatus(int score) => score switch
    {
        >= 85 => "excelente",
        >= 70 => "bom",
        >= 50 => "consumo_moderado",
        >= 30 => "atencao",
        _ => "nao_recomendado"
    };

    private static string DeriveColor(int score) => score switch
    {
        >= 70 => "green",
        >= 50 => "yellow",
        >= 30 => "orange",
        _ => "red"
    };

    private static List<string> ExtractIngredients(NutritionAnalysisContext context)
    {
        // Tenta extrair ingredientes da lista de sinais qualitativos ou do VisionResult
        if (context.VisionResult?.VisibleClaims is { Count: > 0 })
        {
            // Claims não são ingredientes; retorna lista vazia por enquanto
            // No futuro, o VisionResult pode expor uma propriedade Ingredients
        }

        return new List<string>();
    }
}
