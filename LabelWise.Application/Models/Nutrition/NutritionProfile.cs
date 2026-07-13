namespace LabelWise.Application.Models.Nutrition;

/// <summary>
/// Perfil nutricional determinístico extraído via OCR.
/// Usado exclusivamente pelo pipeline de parsing/fixing local, sem fallbacks estimados.
/// </summary>
public class NutritionProfile
{
    public string? ProductName { get; set; }

    public double? Calories { get; set; }
    public double? Carbs { get; set; }
    public double? Sugar { get; set; }
    public double? AddedSugar { get; set; }
    public double? Protein { get; set; }
    public double? Fat { get; set; }
    public double? SaturatedFat { get; set; }
    public double? Fiber { get; set; }
    public double? Sodium { get; set; }
}
