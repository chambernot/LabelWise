using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.DTOs.IngredientAnalysis;

namespace LabelWise.Application.Models.NutritionConversation;

public sealed class NutritionConversationContext
{
    public NutritionConversationSummary NutritionSummary { get; set; } = new();
    public UnifiedNutritionScore? Score { get; set; }
    public UserProfileInsightsDto? Profiles { get; set; }
    public IngredientAnalysisResponse? IngredientAnalysis { get; set; }
    public string Disclaimer { get; set; } = "Análise informativa baseada no rótulo. Não substitui orientação de profissional de saúde.";

    public static NutritionConversationContext FromAnalysis(UnifiedNutritionAnalysisResponse analysis) =>
        new()
        {
            NutritionSummary = NutritionConversationSummary.FromAnalysis(analysis),
            Score = analysis.Score,
            Profiles = analysis.Profiles,
            Disclaimer = "Análise informativa baseada no rótulo. Não substitui orientação de profissional de saúde."
        };

    public static NutritionConversationContext FromAnalysis(UnifiedNutritionAnalysisResponse analysis, IngredientAnalysisResponse? ingredientAnalysis) =>
        new()
        {
            NutritionSummary = NutritionConversationSummary.FromAnalysis(analysis, ingredientAnalysis),
            Score = analysis.Score,
            Profiles = analysis.Profiles,
            IngredientAnalysis = BuildIngredientAnalysisSnapshot(ingredientAnalysis),
            Disclaimer = "Análise informativa baseada no rótulo. Não substitui orientação de profissional de saúde."
        };

    private static IngredientAnalysisResponse? BuildIngredientAnalysisSnapshot(IngredientAnalysisResponse? source)
    {
        if (source is null)
            return null;

        return new IngredientAnalysisResponse
        {
            Success = source.Success,
            Message = source.Message,
            ProductName = source.ProductName,
            Brand = source.Brand,
            IngredientsDetected = source.IngredientsDetected.ToList(),
            Allergens = source.Allergens.ToList(),
            Claims = source.Claims.ToList(),
            IngredientConfidence = source.IngredientConfidence.ToList(),
            NormalizedIngredients = source.NormalizedIngredients.ToList(),
            AllergenRisks = source.AllergenRisks.ToList(),
            ClaimsDetected = source.ClaimsDetected.ToList(),
            DietProfiles = source.DietProfiles,
            ProcessingLevel = source.ProcessingLevel,
            ProcessingClassification = source.ProcessingClassification,
            IngredientContext = source.IngredientContext,
            PositiveIngredients = source.PositiveIngredients.ToList(),
            ReasonSources = source.ReasonSources.ToList(),
            AnalysisCompleteness = source.AnalysisCompleteness,
            CrossContaminationRisk = source.CrossContaminationRisk.ToList(),
            SemanticProfile = source.SemanticProfile,
            AssistantSummary = source.AssistantSummary,
            SummaryCards = source.SummaryCards.ToList(),
            QuickInsights = source.QuickInsights.ToList(),
            QuickFlags = source.QuickFlags.ToList(),
            PresentationHints = source.PresentationHints.ToList(),
            OverallFoodRating = source.OverallFoodRating,
            PresentationSummary = source.PresentationSummary,
            ConfirmedFacts = source.ConfirmedFacts.ToList(),
            InferredFacts = source.InferredFacts.ToList(),
            CriticalAlerts = source.CriticalAlerts.ToList(),
            ConfidenceSummary = source.ConfidenceSummary,
            ProcessingAnalysis = source.ProcessingAnalysis,
            NutritionAnalysis = source.NutritionAnalysis,
            CategorizedIngredients = source.CategorizedIngredients,
            ProductionSafeModeApplied = source.ProductionSafeModeApplied,
            Trust = source.Trust,
            StructuredFoodEntities = source.StructuredFoodEntities.ToList(),
            UnifiedSemanticState = source.UnifiedSemanticState,
            Warnings = source.Warnings.ToList(),
            AnalysisQuality = source.AnalysisQuality,
            SafeForPreciseNutritionAnalysis = source.SafeForPreciseNutritionAnalysis,
            RetryRecommended = source.RetryRecommended,
            Recommendations = source.Recommendations.ToList(),
            TechnicalWarnings = source.TechnicalWarnings.ToList()
        };
    }
}
