using LabelWise.Application.Models.IngredientAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace LabelWise.Application.Services
{
    public interface IGeminiIngredientAnalysisService
    {
        Task<IngredientExtractionResult?> AnalyzeImageAsync(
            byte[] imageBytes,
            string? mimeType,
            CancellationToken cancellationToken = default);
    }
}
