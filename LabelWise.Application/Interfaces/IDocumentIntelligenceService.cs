using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// Extrai dados nutricionais estruturados de uma imagem usando Azure Document Intelligence.
/// </summary>
public interface IDocumentIntelligenceService
{
    /// <summary>
    /// Analisa os bytes de uma imagem e retorna os campos nutricionais mapeados
    /// a partir das tabelas detectadas pelo modelo "prebuilt-layout".
    /// </summary>
    Task<DocumentIntelligenceNutritionResult?> AnalyzeAsync(byte[] imageBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extrai texto bruto da imagem usando o modelo de layout, sem exigir tabela nutricional.
    /// </summary>
    Task<string?> ExtractTextAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
}
