using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Application.Interfaces;

public interface IAnalysisConsistencyValidator
{
    void ValidateAndCorrect(NutritionAnalysisContext context);
}
