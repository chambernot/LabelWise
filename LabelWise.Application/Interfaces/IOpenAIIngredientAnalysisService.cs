using LabelWise.Application.Models.IngredientAnalysis;

namespace LabelWise.Application.Interfaces;

public interface IOpenAIIngredientAnalysisService
{
    Task<IngredientExtractionResult?> AnalyzeAsync(
        byte[] imageBytes,
        string? mimeType,
        string? ocrContext,
        CancellationToken cancellationToken = default);
}
