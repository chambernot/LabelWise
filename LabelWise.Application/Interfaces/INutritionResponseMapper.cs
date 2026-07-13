using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Application.Interfaces;

public interface INutritionResponseMapper
{
    NutritionAnalysisResponseDto Map(NutritionAnalysisContext context);
}
