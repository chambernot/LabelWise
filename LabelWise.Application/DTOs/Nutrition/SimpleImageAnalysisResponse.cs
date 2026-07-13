namespace LabelWise.Application.DTOs.Nutrition;

/// <summary>
/// Resposta do endpoint POST /api/nutrition/analyze-simple-image.
/// </summary>
public sealed class SimpleImageAnalysisResponse
{
    public bool   Success    { get; set; }

    /// <summary>
    /// Fonte dos dados: "OpenFoodFacts", "DocumentIntelligence" ou "Fallback".
    /// </summary>
    public string Source     { get; set; } = string.Empty;

    public SimpleNutritionProfile? NutritionProfile { get; set; }

    /// <summary>
    /// Nível de confiança geral: "high", "medium" ou "low".
    /// </summary>
    public string Confidence { get; set; } = "low";

    public List<string> Warnings { get; set; } = [];

    public string? ErrorMessage { get; set; }

    /// <summary>Tempo total de processamento em milissegundos.</summary>
    public long ProcessingTimeMs { get; set; }
}

public sealed class SimpleNutritionProfile
{
    public double? CaloriesPer100g   { get; set; }
    public double? Carbs             { get; set; }
    public double? Sugar             { get; set; }
    public double? Protein           { get; set; }
    public double? Fat               { get; set; }
    public double? SaturatedFat      { get; set; }
    public double? Sodium            { get; set; }
    public double? Fiber             { get; set; }
}
