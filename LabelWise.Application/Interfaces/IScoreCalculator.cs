using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Application.Interfaces;

public interface IScoreCalculator
{
    ScoreCalculationResult Calculate(NutritionAnalysisContext context);
}
