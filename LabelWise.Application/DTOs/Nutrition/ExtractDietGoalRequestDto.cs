namespace LabelWise.Application.DTOs.Nutrition;

public record ExtractDietGoalRequestDto(string? Base64Image, string? TextInput);

public record ExtractDietGoalResponseDto(
    int TargetCalories,
    decimal TargetProteinG,
    decimal TargetCarbsG,
    decimal TargetFatG,
    string? DietaryRestrictions,
    string? FavoriteFoods,
    string? PrescribedMealPlan // NOVO
);