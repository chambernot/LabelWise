using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Interfaces;

public interface INutritionConsistencyValidator
{
    NutritionValidationResult Validate(EstimatedNutritionProfileDto profile);
    NutritionValidationResult Validate(EstimatedNutritionProfileDto profile, string? categoryHint);
}
