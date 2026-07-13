using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Application.Interfaces
{
    public interface INutritionSanitizer
    {
        SanitizationResult<NutritionAnalysisResponseDto> Sanitize(
            NutritionAnalysisResponseDto response,
            NutritionSanitizationContext? context = null);
    }
}
