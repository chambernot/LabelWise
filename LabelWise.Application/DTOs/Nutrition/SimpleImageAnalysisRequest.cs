namespace LabelWise.Application.DTOs.Nutrition;

/// <summary>
/// Entrada para o endpoint POST /api/nutrition/analyze-di-image.
/// A imagem é enviada como arquivo multipart/form-data via NutritionAnalysisFormModel.
/// </summary>
public sealed class SimpleImageAnalysisRequest
{
    /// <summary>
    /// Imagem codificada em base64 (alternativa ao upload multipart).
    /// </summary>
    public string? ImageBase64 { get; set; }
}
