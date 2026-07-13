using LabelWise.Domain.Enums;

namespace LabelWise.Application.DTOs.LabelReading
{
    /// <summary>
    /// Request para o serviço de leitura de rótulos.
    /// Contém as imagens das diferentes partes do rótulo para extração de informações.
    /// </summary>
    public class LabelReadingRequest
    {
        /// <summary>
        /// ID do usuário que está fazendo a requisição.
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Código de barras do produto (obtido na etapa de identificação).
        /// </summary>
        public string? Barcode { get; set; }

        /// <summary>
        /// Nome do produto (obtido na etapa de identificação).
        /// </summary>
        public string? ProductName { get; set; }

        /// <summary>
        /// Capturas das diferentes partes do rótulo.
        /// </summary>
        public List<LabelCapture> Captures { get; set; } = new();

        /// <summary>
        /// Idioma preferido para OCR (ISO 639-1: "pt", "en", "es").
        /// </summary>
        public string LanguageCode { get; set; } = "pt";

        /// <summary>
        /// Indica se deve usar múltiplos provedores de OCR (fallback strategy).
        /// </summary>
        public bool EnableMultiProviderOcr { get; set; } = true;

        /// <summary>
        /// Threshold mínimo de confiança do OCR (0.0 a 1.0).
        /// Se a confiança for menor, pode acionar fallback para outro provider.
        /// </summary>
        public double OcrConfidenceThreshold { get; set; } = 0.85;
    }

    /// <summary>
    /// Representa uma captura de imagem de uma parte específica do rótulo.
    /// </summary>
    public class LabelCapture
    {
        /// <summary>
        /// Tipo de captura (tabela nutricional, ingredientes, etc.).
        /// </summary>
        public CaptureType CaptureType { get; set; }

        /// <summary>
        /// Dados da imagem em bytes.
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
        /// Ordem de prioridade (menor número = maior prioridade).
        /// Usado quando há múltiplas capturas do mesmo tipo.
        /// </summary>
        public int Priority { get; set; } = 1;
    }
}
