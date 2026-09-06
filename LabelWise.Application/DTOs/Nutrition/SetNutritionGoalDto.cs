namespace LabelWise.Application.DTOs.Nutrition;

public record SetNutritionGoalDto(
    string UserId,
    DateTime Date,
    int TargetCalories,
    decimal TargetProteinG,
    decimal TargetCarbsG,
    decimal TargetFatG
);