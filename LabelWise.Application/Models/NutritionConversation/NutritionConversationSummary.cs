using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.DTOs.IngredientAnalysis;

namespace LabelWise.Application.Models.NutritionConversation;

public sealed class NutritionConversationSummary
{
    public string? ProductName { get; set; }
    public string? Brand { get; set; }
    public string? Category { get; set; }
    public string? State { get; set; }
    public string? NutritionDataQuality { get; set; }
    public ImageQualityInfo ImageQuality { get; set; } = new();
    public NutritionAnalysisQualityDto AnalysisQuality { get; set; } = new();
    public int NutritionReliabilityScore { get; set; }
    public IngredientContextDto IngredientContext { get; set; } = new();
    public bool HasNutritionTable { get; set; }
    public bool HasMinimumNutritionData { get; set; }
    public string? ProcessingLevel { get; set; }
    public EstimatedNutritionProfileDto? NutritionProfile { get; set; }
    public List<string> Ingredients { get; set; } = new();
    public List<string> VisibleClaims { get; set; } = new();
    public List<string> AllergenWarnings { get; set; } = new();
    public List<string> ProcessingReasons { get; set; } = new();
    public List<QuickFlagDto> FoodQuickFlags { get; set; } = new();
    public List<string> Strengths { get; set; } = new();
    public List<string> Weaknesses { get; set; } = new();

    public static NutritionConversationSummary FromAnalysis(UnifiedNutritionAnalysisResponse analysis)
    {
        var nutritionProfile = analysis.Enriched?.NormalizedProfile
                            ?? analysis.Analysis?.NutritionProfile;

        return new NutritionConversationSummary
        {
            ProductName = analysis.Analysis?.ProductName ?? nutritionProfile?.ProductName,
            Brand = analysis.Analysis?.Brand ?? nutritionProfile?.Brand,
            Category = analysis.Analysis?.Category,
            State = analysis.State,
            NutritionDataQuality = analysis.NutritionDataQuality,
            ImageQuality = analysis.ImageQuality,
            AnalysisQuality = analysis.AnalysisQuality,
            NutritionReliabilityScore = analysis.NutritionReliabilityScore,
            IngredientContext = analysis.IngredientContext,
            HasNutritionTable = analysis.HasNutritionTable,
            HasMinimumNutritionData = analysis.HasMinimumNutritionData,
            ProcessingLevel = analysis.Enriched?.ProcessingLevel,
            NutritionProfile = nutritionProfile,
            Ingredients = analysis.Analysis?.Ingredients ?? new List<string>(),
            VisibleClaims = analysis.Analysis?.VisibleClaims ?? new List<string>(),
            Strengths = analysis.Score?.Highlights ?? new List<string>(),
            Weaknesses = analysis.Score?.Warnings ?? new List<string>()
        };
    }

    public static NutritionConversationSummary FromAnalysis(UnifiedNutritionAnalysisResponse analysis, IngredientAnalysisResponse? ingredientAnalysis)
    {
        var summary = FromAnalysis(analysis);
        if (ingredientAnalysis is null)
            return summary;

        summary.ProductName ??= ingredientAnalysis.ProductName;
        summary.Brand ??= ingredientAnalysis.Brand;
        summary.ProcessingLevel ??= ingredientAnalysis.ProcessingClassification.Level;
        summary.Ingredients = Merge(summary.Ingredients, ingredientAnalysis.IngredientsDetected);
        summary.VisibleClaims = Merge(summary.VisibleClaims, ingredientAnalysis.Claims);
        summary.AllergenWarnings = ingredientAnalysis.AllergenRisks
            .Select(risk => risk.RiskType == "may_contain" ? $"Pode conter traços de {risk.Name}" : $"Contém {risk.Name}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        summary.ProcessingReasons = ingredientAnalysis.ProcessingClassification.Reasons;
        summary.FoodQuickFlags = ingredientAnalysis.QuickFlags;
        summary.Strengths = Merge(summary.Strengths, ingredientAnalysis.AssistantSummary.Highlights);
        summary.Weaknesses = Merge(summary.Weaknesses, ingredientAnalysis.AssistantSummary.Warnings);

        return summary;
    }

    private static List<string> Merge(IReadOnlyList<string> first, IReadOnlyList<string> second) =>
        first
            .Concat(second)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
