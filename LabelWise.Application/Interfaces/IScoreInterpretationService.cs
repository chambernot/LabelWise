using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Application.Interfaces
{
    public interface IScoreInterpretationService
    {
        string DetermineProcessingLevel(string? category, IEnumerable<string>? visibleClaims, string? productName);
        ScoreInterpretationResult Interpret(NutritionAnalysisContext context);
        NutritionalScore BuildSafeScoreLabel(ScoreInterpretationContext context);
        string BuildAbsoluteRecommendation(ScoreInterpretationContext context);
        string BuildComparativeRecommendation(ScoreInterpretationContext primary, ScoreInterpretationContext secondary, bool isPrimaryWinner, bool isTie);
        bool ShouldCapPositiveLabel(ScoreInterpretationContext context, string processingLevel);
        string BuildScoreReason(ScoreInterpretationContext context);
    }
}
