namespace LabelWise.Infrastructure.Services.LabelReading
{
    /// <summary>
    /// Interface para estratégias de parsing específicas por tipo de captura.
    /// </summary>
    public interface ICaptureReadingStrategy
    {
        /// <summary>
        /// Faz o parsing do texto OCR bruto e retorna dados estruturados.
        /// </summary>
        /// <param name="rawOcrText">Texto bruto extraído por OCR</param>
        /// <param name="ocrConfidence">Confiança do OCR (0.0 a 1.0)</param>
        /// <returns>Resultado estruturado do parsing</returns>
        CaptureReadingStrategyResult Parse(string rawOcrText, double ocrConfidence);
    }

    /// <summary>
    /// Resultado de uma estratégia de parsing.
    /// </summary>
    public class CaptureReadingStrategyResult
    {
        /// <summary>
        /// Indica se o parsing foi bem-sucedido.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Confiança do parsing (0.0 a 1.0).
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Dados estruturados em formato JSON.
        /// </summary>
        public string? StructuredData { get; set; }

        /// <summary>
        /// Mensagem de erro (se Success = false).
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Metadados adicionais.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
