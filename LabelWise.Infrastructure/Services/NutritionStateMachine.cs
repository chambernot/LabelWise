using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Domain.Enums;

namespace LabelWise.Infrastructure.Services;

/// <inheritdoc />
public sealed class NutritionStateMachine : INutritionStateMachine
{
    private const string InsufficientProfileText = "Informação insuficiente";

    public NutritionAnalysisState DetermineState(NutritionContext ctx)
    {
        if (ctx is null) return NutritionAnalysisState.Invalid;

        if (!ctx.HasAnyNutritionData)
            return NutritionAnalysisState.NoData;

        if (!ctx.HasNutritionTable && ctx.HasCaloriesOnly)
            return NutritionAnalysisState.PartialData;

        if (ctx.HasNutritionTable && !ctx.HasMinimumData)
            return NutritionAnalysisState.StructuredData;

        if (ctx.HasMinimumData && ctx.Confidence < 0.5)
            return NutritionAnalysisState.LowConfidence;

        if (ctx.HasMinimumData && ctx.Confidence >= 0.5)
            return NutritionAnalysisState.CompleteData;

        return NutritionAnalysisState.Invalid;
    }

    public void Apply(NutritionAnalysisState state, UnifiedNutritionAnalysisResponse response)
    {
        if (response is null) return;

        response.State = state.ToString();

        switch (state)
        {
            case NutritionAnalysisState.NoData:
                response.HasNutritionTable = false;
                response.HasMinimumNutritionData = false;
                response.NutritionDataQuality = "none";
                response.Analysis.AnalysisMode = AnalysisMode.Unknown;
                response.Enriched.IsLowConfidenceMode = false;
                ForceScore(response, value: 0, label: "Sem dados", color: "gray");
                StampProfilesAsInsufficient(response);
                break;

            case NutritionAnalysisState.PartialData:
                response.HasNutritionTable = false;
                response.HasMinimumNutritionData = false;
                response.NutritionDataQuality = "partial";
                response.Analysis.AnalysisMode = AnalysisMode.FrontOfPackageOnly;
                response.Enriched.IsLowConfidenceMode = false;
                ForceScore(response, value: 0, label: "Dados insuficientes", color: "gray");
                StampProfilesAsInsufficient(response);
                break;

            case NutritionAnalysisState.StructuredData:
                response.HasNutritionTable = true;
                response.HasMinimumNutritionData = false;
                response.NutritionDataQuality = "structured_partial";
                response.Analysis.AnalysisMode = AnalysisMode.FullNutritionLabel;
                response.Enriched.IsLowConfidenceMode = false;
                if (response.Score != null)
                {
                    response.Score.Label = "Dados insuficientes";
                    response.Score.Color = "gray";
                }
                StampProfilesAsInsufficient(response);
                break;

            case NutritionAnalysisState.LowConfidence:
                response.HasNutritionTable = true;
                response.HasMinimumNutritionData = true;
                response.NutritionDataQuality = "low_confidence";
                response.Analysis.AnalysisMode = AnalysisMode.FullNutritionLabel;
                response.Enriched.IsLowConfidenceMode = true;
                if (response.Score != null)
                {
                    response.Score.Label = "Dados incertos";
                    response.Score.Color = "yellow";
                }
                StampProfilesAsInsufficient(response);
                break;

            case NutritionAnalysisState.CompleteData:
                response.HasNutritionTable = true;
                response.HasMinimumNutritionData = true;
                response.NutritionDataQuality = "full";
                response.Analysis.AnalysisMode = AnalysisMode.FullNutritionLabel;
                response.Enriched.IsLowConfidenceMode = false;
                // Score e perfis preservam o cálculo realizado pelos serviços normais.
                break;

            case NutritionAnalysisState.Invalid:
                response.HasNutritionTable = false;
                response.HasMinimumNutritionData = false;
                response.NutritionDataQuality = "invalid";
                response.Analysis.AnalysisMode = AnalysisMode.Unknown;
                response.Enriched.IsLowConfidenceMode = false;
                ForceScore(response, value: 0, label: "Análise inválida", color: "gray");
                StampProfilesAsInsufficient(response);
                break;
        }
    }

    private static void ForceScore(
        UnifiedNutritionAnalysisResponse response,
        int value,
        string label,
        string color)
    {
        response.Score ??= new UnifiedNutritionScore();
        response.Score.Value = value;
        response.Score.Label = label;
        response.Score.Color = color;
        response.Score.PrincipalOffender = "dados insuficientes";
    }

    private static void StampProfilesAsInsufficient(UnifiedNutritionAnalysisResponse response)
    {
        response.Profiles ??= new UserProfileInsightsDto();
        response.Profiles.Diabetic     = InsufficientProfileText;
        response.Profiles.Hypertension = InsufficientProfileText;
        response.Profiles.WeightLoss   = InsufficientProfileText;
        response.Profiles.MuscleGain   = InsufficientProfileText;
        if (string.IsNullOrWhiteSpace(response.Profiles.Summary))
            response.Profiles.Summary = InsufficientProfileText;
    }
}
