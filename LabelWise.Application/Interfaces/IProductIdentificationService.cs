using LabelWise.Application.DTOs.ProductIdentification;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Serviço responsável pela identificação de produtos.
    /// 
    /// RESPONSABILIDADES:
    /// - Leitura de códigos de barras (EAN, UPC, QR Code)
    /// - Busca em bases de dados externas (Open Food Facts, etc.)
    /// - OCR de embalagem frontal (nome + marca)
    /// - Reconhecimento visual (AI/ML)
    /// - Estratégia de fallback entre métodos
    /// 
    /// FLUXO TÍPICO:
    /// 1. Tentar ler código de barras da imagem
    /// 2. Se bem-sucedido, buscar produto em base externa
    /// 3. Se falhar, tentar OCR da embalagem frontal
    /// 4. Se falhar, tentar reconhecimento visual (futuro)
    /// 5. Retornar melhor resultado com confiança calculada
    /// </summary>
    public interface IProductIdentificationService
    {
        /// <summary>
        /// Identifica um produto a partir de uma imagem.
        /// Usa estratégia inteligente de fallback entre múltiplos métodos.
        /// </summary>
        /// <param name="request">Request contendo imagem e configurações</param>
        /// <returns>Resultado da identificação com confiança e metadata</returns>
        Task<ProductIdentificationResult> IdentifyProductAsync(ProductIdentificationRequest request);

        /// <summary>
        /// Identifica um produto a partir de código de barras já conhecido.
        /// Busca informações em bases de dados externas.
        /// </summary>
        /// <param name="barcode">Código de barras (EAN, UPC, etc.)</param>
        /// <param name="languageCode">Idioma preferido (ISO 639-1)</param>
        /// <returns>Resultado da identificação</returns>
        Task<ProductIdentificationResult> IdentifyByBarcodeAsync(string barcode, string languageCode = "pt");

        /// <summary>
        /// Verifica disponibilidade do serviço e seus componentes.
        /// </summary>
        /// <returns>
        /// Dictionary com status de cada componente:
        /// - "BarcodeReader": "Available" | "Unavailable"
        /// - "ExternalDatabase": "Available" | "Unavailable"
        /// - "OcrEngine": "Available" | "Unavailable"
        /// </returns>
        Task<Dictionary<string, string>> GetServiceStatusAsync();

        /// <summary>
        /// Obtém estatísticas de uso do serviço.
        /// </summary>
        /// <returns>
        /// Dictionary com estatísticas:
        /// - "TotalIdentifications": número total
        /// - "SuccessRate": taxa de sucesso (0.0 a 1.0)
        /// - "AverageConfidence": confiança média
        /// - "MethodDistribution": distribuição de métodos usados (JSON)
        /// </returns>
        Task<Dictionary<string, string>> GetUsageStatisticsAsync();
    }
}
