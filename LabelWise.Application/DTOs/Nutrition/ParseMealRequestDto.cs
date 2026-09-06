namespace LabelWise.Application.DTOs.Nutrition;

public record ParseMealRequestDto(
    string UserId,
    string? TextInput,
    string? Base64Image,
    string? AudioUrl,
    DateTime LocalTime
);