namespace LabelWise.Application.DTOs.Nutrition;

public class NutritionValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();

    public bool HasCriticalError() => Errors.Count > 0;
}
