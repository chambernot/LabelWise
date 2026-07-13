using LabelWise.Application.DTOs.IngredientAnalysis;

namespace LabelWise.Application.Interfaces;

public interface IIngredientAnalysisService
{
    Task<IngredientAnalysisResponse> AnalyzeAsync(
        byte[] imageBytes,
        string? mimeType,
        CancellationToken cancellationToken = default);
}
