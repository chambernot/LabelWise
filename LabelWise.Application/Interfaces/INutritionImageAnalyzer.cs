using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// Extrai informações nutricionais de imagens usando Vision AI.
/// 
/// Responsabilidades:
///   - Enviar imagem para API de visão (OpenAI Vision)
///   - Receber dados estruturados (JSON)
///   - Mapear para EstimatedNutritionProfileDto
/// 
/// NÃO valida, NÃO calcula, NÃO infere, NÃO aplica fallback.
/// Apenas extração pura de dados visíveis na imagem.
/// </summary>
public interface INutritionImageAnalyzer
{
    /// <summary>
    /// Analisa imagem e extrai perfil nutricional visível.
    /// </summary>
    /// <param name="imageBytes">Imagem em bytes (JPEG/PNG)</param>
    /// <param name="mimeType">MIME type original da imagem</param>
    /// <param name="precomputedExactHash">
    /// Hash exato (SHA-256 hex lower-case) já calculado pelo chamador sobre
    /// os MESMOS bytes que estão sendo passados em <paramref name="imageBytes"/>.
    /// Quando informado, evita re-hashear e garante que o cache é consultado
    /// com o mesmo identificador usado em logs/observabilidade externa.
    /// </param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Perfil nutricional extraído ou null se falhar</returns>
    Task<EstimatedNutritionProfileDto?> AnalyzeAsync(
        byte[] imageBytes,
        string? mimeType,
        string? precomputedExactHash = null,
        CancellationToken cancellationToken = default);
}
