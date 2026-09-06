namespace LabelWise.Application.DTOs.Nutrition;

public record MealAnalysisResponseDto(
    string MealType,
    string DishName,
    List <FoodItemDto> Items,
    MacroSummaryDto TotalMeal,
    bool RequiresUserClarification,
    string? ClarificationQuestion
);

public record FoodItemDto(
    string FoodName,
    string PortionDescription,
    decimal EstimatedWeightG,
    int Calories,
    decimal ProteinG,
    decimal CarbsG,
    decimal FatG,
    decimal ConfidenceScore
);

public record MacroSummaryDto(
    int Calories,
    decimal ProteinG,
    decimal CarbsG,
    decimal FatG
);