using System.Text.Json.Serialization;

namespace LabelWise.Infrastructure.AI.DTOs;

/// <summary>
/// DTO interno para desserialização da resposta JSON do modelo Azure OpenAI Vision
/// para análise nutricional detalhada.
/// </summary>
internal class NutritionVisionModelResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("productName")]
    public string? ProductName { get; set; }

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("packageWeight")]
    public string? PackageWeight { get; set; }

    [JsonPropertyName("analysisMode")]
    public string? AnalysisMode { get; set; }

    [JsonPropertyName("visibleClaims")]
    public List<string> VisibleClaims { get; set; } = new();

    [JsonPropertyName("estimatedNutritionProfile")]
    public NutritionProfileResponse? EstimatedNutritionProfile { get; set; }

    [JsonPropertyName("classification")]
    public ClassificationResponse? Classification { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("confidenceDetails")]
    public ConfidenceDetailsResponse? ConfidenceDetails { get; set; }

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    [JsonPropertyName("rawExtractedText")]
    public List<string>? RawExtractedText { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// DTO para perfil nutricional na resposta do modelo.
/// Inclui todos os campos ANVISA necessários para o motor de score.
/// </summary>
internal class NutritionProfileResponse
{
    [JsonPropertyName("caloriesPer100g")]
    public double? CaloriesPer100g { get; set; }

    [JsonPropertyName("caloriesPer100ml")]
    public double? CaloriesPer100ml { get; set; }

    [JsonPropertyName("estimatedPackageCalories")]
    public double? EstimatedPackageCalories { get; set; }

    [JsonPropertyName("estimatedCarbsPer100g")]
    public double? EstimatedCarbsPer100g { get; set; }

    [JsonPropertyName("estimatedSugarPer100g")]
    public double? EstimatedSugarPer100g { get; set; }

    [JsonPropertyName("estimatedAddedSugarPer100g")]
    public double? EstimatedAddedSugarPer100g { get; set; }

    [JsonPropertyName("estimatedProteinPer100g")]
    public double? EstimatedProteinPer100g { get; set; }

    [JsonPropertyName("estimatedSodiumPer100g")]
    public double? EstimatedSodiumPer100g { get; set; }

    [JsonPropertyName("estimatedFiberPer100g")]
    public double? EstimatedFiberPer100g { get; set; }

    [JsonPropertyName("estimatedFatPer100g")]
    public double? EstimatedFatPer100g { get; set; }

    [JsonPropertyName("estimatedSaturatedFatPer100g")]
    public double? EstimatedSaturatedFatPer100g { get; set; }

    /// <summary>
    /// Peso da porção em gramas conforme declarado no rótulo ANVISA.
    /// Exemplo: linha "Porção: 20 g" → 20. Usado para validação cruzada.
    /// </summary>
    [JsonPropertyName("portionWeightG")]
    public double? PortionWeightG { get; set; }

    /// <summary>
    /// Calorias POR PORÇÃO lidas diretamente da coluna de porção na linha "Valor energético".
    /// Exemplo: "Valor energético 69 76 4" → caloriesPerPortion = 76.
    /// Campo distinto de caloriesPer100g. Nunca calcular — ler direto da tabela.
    /// </summary>
    [JsonPropertyName("caloriesPerPortion")]
    public double? CaloriesPerPortion { get; set; }

    [JsonPropertyName("nutritionUnit")]
    public string? NutritionUnit { get; set; }

    [JsonPropertyName("basis")]
    public string? Basis { get; set; }
}

/// <summary>
/// DTO para classificação por perfil na resposta do modelo.
/// </summary>
internal class ClassificationResponse
{
    [JsonPropertyName("diabetic")]
    public ProfileClassificationResponse? Diabetic { get; set; }

    [JsonPropertyName("bloodPressure")]
    public ProfileClassificationResponse? BloodPressure { get; set; }

    [JsonPropertyName("weightLoss")]
    public ProfileClassificationResponse? WeightLoss { get; set; }

    [JsonPropertyName("muscleGain")]
    public ProfileClassificationResponse? MuscleGain { get; set; }
}

/// <summary>
/// DTO para classificação de perfil individual na resposta do modelo.
/// </summary>
internal class ProfileClassificationResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

/// <summary>
/// DTO para detalhes de confiança na resposta do modelo.
/// </summary>
internal class ConfidenceDetailsResponse
{
    [JsonPropertyName("productIdentification")]
    public double ProductIdentification { get; set; }

    [JsonPropertyName("visibleClaimsExtraction")]
    public double VisibleClaimsExtraction { get; set; }

    [JsonPropertyName("estimatedNutritionProfile")]
    public double EstimatedNutritionProfile { get; set; }

    [JsonPropertyName("classification")]
    public double Classification { get; set; }
}