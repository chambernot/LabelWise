using LabelWise.Application.DTOs.LabelReading;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Serviço responsável pela leitura e extração de informações de rótulos.
    /// 
    /// RESPONSABILIDADES:
    /// - OCR de múltiplas partes do rótulo (tabela nutricional, ingredientes, alérgenos)
    /// - Estruturação de dados extraídos
    /// - Validação de qualidade da leitura
    /// - Estratégia de fallback entre provedores OCR
    /// - Parsing inteligente de diferentes formatos
    /// 
    /// FLUXO TÍPICO:
    /// 1. Recebe múltiplas capturas (nutrition table, ingredients, allergens)
    /// 2. Para cada captura, executa OCR (Tesseract → Azure se confiança baixa)
    /// 3. Parse o texto bruto em dados estruturados
    /// 4. Valida qualidade da extração
    /// 5. Consolida informações de todas as capturas
    /// 6. Retorna resultado estruturado com confiança
    /// </summary>
    public interface ILabelReadingService
    {
        /// <summary>
        /// Lê e extrai informações de múltiplas capturas de rótulo.
        /// Coordena OCR + parsing + estruturação de dados.
        /// </summary>
        /// <param name="request">Request contendo capturas e configurações</param>
        /// <returns>Resultado estruturado com todas as informações extraídas</returns>
        Task<LabelReadingResult> ReadLabelAsync(LabelReadingRequest request);

        /// <summary>
        /// Lê especificamente a tabela nutricional de uma imagem.
        /// </summary>
        /// <param name="imageData">Dados da imagem</param>
        /// <param name="languageCode">Idioma (ISO 639-1)</param>
        /// <returns>Informações nutricionais estruturadas</returns>
        Task<NutritionalInformationDto?> ReadNutritionTableAsync(byte[] imageData, string languageCode = "pt");

        /// <summary>
        /// Lê especificamente a lista de ingredientes de uma imagem.
        /// </summary>
        /// <param name="imageData">Dados da imagem</param>
        /// <param name="languageCode">Idioma (ISO 639-1)</param>
        /// <returns>Lista de ingredientes extraídos</returns>
        Task<List<string>> ReadIngredientsAsync(byte[] imageData, string languageCode = "pt");

        /// <summary>
        /// Lê especificamente a declaração de alérgenos de uma imagem.
        /// </summary>
        /// <param name="imageData">Dados da imagem</param>
        /// <param name="languageCode">Idioma (ISO 639-1)</param>
        /// <returns>Lista de alérgenos identificados</returns>
        Task<List<string>> ReadAllergensAsync(byte[] imageData, string languageCode = "pt");

        /// <summary>
        /// Valida a qualidade da leitura realizada.
        /// </summary>
        /// <param name="result">Resultado da leitura a ser validado</param>
        /// <returns>
        /// Dictionary com avaliação de qualidade:
        /// - "OverallQuality": "Excellent" | "Good" | "Fair" | "Poor"
        /// - "OcrConfidence": confiança média do OCR
        /// - "DataCompleteness": completude dos dados (0.0 a 1.0)
        /// - "Issues": lista de problemas identificados (JSON)
        /// </returns>
        Task<Dictionary<string, string>> ValidateReadingQualityAsync(LabelReadingResult result);

        /// <summary>
        /// Verifica disponibilidade do serviço e seus componentes.
        /// </summary>
        /// <returns>
        /// Dictionary com status de cada componente:
        /// - "OcrProviders": lista de providers disponíveis (JSON)
        /// - "ParsingEngine": "Available" | "Unavailable"
        /// - "QualityValidator": "Available" | "Unavailable"
        /// </returns>
        Task<Dictionary<string, string>> GetServiceStatusAsync();

        /// <summary>
        /// Obtém estatísticas de uso do serviço.
        /// </summary>
        /// <returns>
        /// Dictionary com estatísticas:
        /// - "TotalReadings": número total de leituras
        /// - "SuccessRate": taxa de sucesso (0.0 a 1.0)
        /// - "AverageConfidence": confiança média
        /// - "ProviderUsage": uso de cada provider OCR (JSON)
        /// </returns>
        Task<Dictionary<string, string>> GetUsageStatisticsAsync();
    }
}
