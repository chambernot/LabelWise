namespace LabelWise.Application.DTOs.Nutrition;

public record MealSuggestionDto(
    string Title,
    string Description,
    int EstimatedCalories,
    decimal EstimatedProteinG
);