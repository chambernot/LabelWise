using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Application.Interfaces;

public interface ICategoryDecisionEngine
{
    CategoryDecisionResult Decide(NutritionAnalysisContext context);
}
