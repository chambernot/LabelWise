using LabelWise.Domain.Enums;

namespace LabelWise.Application.DTOs.ProductIdentification
{
    /// <summary>
    /// Request para o serviço de identificação de produtos.
    /// Contém a imagem e metadados necessários para identificar um produto.
    /// </summary>
    public class ProductIdentificationRequest
    {
        /// <summary>
        /// ID do usuário que está fazendo a requisição.
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Tipo de captura da imagem fornecida.
        /// </summary>
        public CaptureType CaptureType { get; set; }

        /// <summary>
        /// Dados da imagem em bytes (pode ser foto do código de barras ou embalagem).
        /// </summary>
        public required byte[] ImageData { get; set; }

        /// <summary>
        /// Nome do arquivo original (opcional).
        /// </summary>
        public string? FileName { get; set; }

        /// <summary>
        /// Content type da imagem (ex: "image/jpeg", "image/png").
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// Código de barras fornecido manualmente (opcional).
        /// Se fornecido, evita a necessidade de OCR/leitura do código.
        /// </summary>
        public string? ManualBarcode { get; set; }

        /// <summary>
        /// Idioma preferido para busca de informações (ISO 639-1: "pt", "en", "es").
        /// </summary>
        public string LanguageCode { get; set; } = "pt";

        /// <summary>
        /// Indica se deve tentar buscar informações em bases externas (Open Food Facts, etc.).
        /// </summary>
        public bool EnableExternalDatabaseLookup { get; set; } = true;

        /// <summary>
        /// Indica se deve usar OCR como fallback caso a leitura do código de barras falhe.
        /// </summary>
        public bool EnableOcrFallback { get; set; } = true;
    }
}
