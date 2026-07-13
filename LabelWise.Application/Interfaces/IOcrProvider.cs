using System.Threading.Tasks;
using LabelWise.Application.DTOs;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Interface para provedores de OCR (Optical Character Recognition).
    /// Permite extrair texto de imagens de rótulos de produtos.
    /// </summary>
    public interface IOcrProvider
    {
        /// <summary>
        /// Extrai texto de uma imagem usando OCR.
        /// </summary>
        /// <param name="request">Informações da imagem a ser processada</param>
        /// <returns>Resultado do OCR com texto extraído e metadados</returns>
        Task<OcrResultDto> ExtractTextAsync(OcrRequestDto request);

        /// <summary>
        /// Verifica se o provedor está disponível e configurado corretamente.
        /// </summary>
        Task<bool> IsAvailableAsync();

        /// <summary>
        /// Nome do provedor (ex: "Azure Computer Vision", "Google Vision", "Tesseract").
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Retorna metadados sobre a configuração atual do provider.
        /// </summary>
        Dictionary<string, string> GetMetadata();
    }
}
