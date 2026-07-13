using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Interfaces;

public interface INutritionAutoCorrector
{
    EstimatedNutritionProfileDto AutoCorrect(EstimatedNutritionProfileDto profile, List<string> rawText);
}
