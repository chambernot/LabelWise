using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Interfaces;

public interface INutritionOcrCrossValidator
{
    EstimatedNutritionProfileDto ValidateAndCorrect(EstimatedNutritionProfileDto profile, List<string> rawText);
}
