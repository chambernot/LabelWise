using LabelWise.Application.DTOs.ProductIdentification;
using LabelWise.Application.DTOs.LabelReading;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.DTOs.Analysis
{
    /// <summary>
    /// Resposta completa da análise de imagem capturada.
    /// Consolida resultados de identificação, leitura de rótulo e análise nutricional.
    /// </summary>
    public class CapturedImageAnalysisResponse
    {
        /// <summary>
        /// Indica se o processamento foi bem-sucedido.
        /// </summary>
        /// <example>true</example>
        public bool Success { get; set; }

        /// <summary>
        /// Tipo de captura processada.
        /// </summary>
        /// <example>NutritionTable</example>
        public CaptureType CaptureType { get; set; }

        /// <summary>
        /// Nível de confiança geral do processamento (0.0 a 1.0).
        /// </summary>
        /// <example>0.92</example>
        public double OverallConfidence { get; set; }

        /// <summary>
        /// Resultado da identificação do produto.
        /// Inclui código de barras, nome, marca e fonte dos dados.
        /// </summary>
        public ProductIdentificationResultDto? IdentificationResult { get; set; }

        /// <summary>
        /// Resultado da leitura do rótulo.
        /// Inclui texto extraído, ingredientes, alérgenos e informações nutricionais.
        /// </summary>
        public LabelReadingResultDto? LabelReadingResult { get; set; }

        /// <summary>
        /// Resultado da análise nutricional final.
        /// Inclui scores, classificações, alertas e recomendações.
        /// </summary>
        public ProductAnalysisResultDto? FinalAnalysis { get; set; }

        /// <summary>
        /// Metadados do processamento.
        /// </summary>
        public AnalysisMetadataDto Metadata { get; set; } = new();

        /// <summary>
        /// Mensagem de erro (se Success = false).
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Warnings sobre qualidade ou problemas não-críticos.
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Recomendações para melhorar a qualidade do processo.
        /// </summary>
        public List<string> Recommendations { get; set; } = new();
    }

    /// <summary>
    /// Resultado simplificado da identificação do produto para a resposta da API.
    /// </summary>
    public class ProductIdentificationResultDto
    {
        /// <summary>
        /// Indica se a identificação foi bem-sucedida.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Método utilizado para identificação.
        /// </summary>
        public string Method { get; set; } = string.Empty;

        /// <summary>
        /// Nível de confiança da identificação (0.0 a 1.0).
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Código de barras identificado.
        /// </summary>
        public string? Barcode { get; set; }

        /// <summary>
        /// Nome do produto.
        /// </summary>
        public string? ProductName { get; set; }

        /// <summary>
        /// Marca do produto.
        /// </summary>
        public string? Brand { get; set; }

        /// <summary>
        /// Categoria do produto.
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Fonte dos dados (ex: "Open Food Facts", "OCR Local").
        /// </summary>
        public string? DataSource { get; set; }

        /// <summary>
        /// Indica se os dados vieram de uma base externa confiável.
        /// </summary>
        public bool IsFromExternalDatabase { get; set; }

        /// <summary>
        /// Mensagem de erro (se Success = false).
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Resultado simplificado da leitura de rótulo para a resposta da API.
    /// </summary>
    public class LabelReadingResultDto
    {
        /// <summary>
        /// Indica se a leitura foi bem-sucedida.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Nível de confiança da leitura (0.0 a 1.0).
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Texto bruto extraído via OCR.
        /// </summary>
        public string? RawText { get; set; }

        /// <summary>
        /// Lista de ingredientes extraídos.
        /// </summary>
        public List<string> Ingredients { get; set; } = new();

        /// <summary>
        /// Lista de alérgenos identificados.
        /// </summary>
        public List<string> Allergens { get; set; } = new();

        /// <summary>
        /// Informações nutricionais extraídas.
        /// </summary>
        public NutritionalInfoDto? NutritionalInfo { get; set; }

        /// <summary>
        /// Claims nutricionais identificados (ex: "Sem glúten", "Rico em fibras").
        /// </summary>
        public List<string> NutritionalClaims { get; set; } = new();

        /// <summary>
        /// Provider de OCR utilizado.
        /// </summary>
        public string? OcrProvider { get; set; }

        /// <summary>
        /// Tempo de processamento do OCR em milissegundos.
        /// </summary>
        public double OcrProcessingTimeMs { get; set; }

        /// <summary>
        /// Mensagem de erro (se Success = false).
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Informações nutricionais extraídas do rótulo.
    /// </summary>
    public class NutritionalInfoDto
    {
        /// <summary>
        /// Tamanho da porção.
        /// </summary>
        public string? ServingSize { get; set; }

        /// <summary>
        /// Calorias por porção.
        /// </summary>
        public double? Calories { get; set; }

        /// <summary>
        /// Gorduras totais (g).
        /// </summary>
        public double? TotalFat { get; set; }

        /// <summary>
        /// Gorduras saturadas (g).
        /// </summary>
        public double? SaturatedFat { get; set; }

        /// <summary>
        /// Gorduras trans (g).
        /// </summary>
        public double? TransFat { get; set; }

        /// <summary>
        /// Colesterol (mg).
        /// </summary>
        public double? Cholesterol { get; set; }

        /// <summary>
        /// Sódio (mg).
        /// </summary>
        public double? Sodium { get; set; }

        /// <summary>
        /// Carboidratos totais (g).
        /// </summary>
        public double? TotalCarbohydrates { get; set; }

        /// <summary>
        /// Fibra alimentar (g).
        /// </summary>
        public double? DietaryFiber { get; set; }

        /// <summary>
        /// Açúcares totais (g).
        /// </summary>
        public double? Sugars { get; set; }

        /// <summary>
        /// Proteínas (g).
        /// </summary>
        public double? Protein { get; set; }

        /// <summary>
        /// Valores adicionais não padronizados.
        /// </summary>
        public Dictionary<string, double> AdditionalNutrients { get; set; } = new();
    }

    /// <summary>
    /// Metadados do processamento da análise.
    /// </summary>
    public class AnalysisMetadataDto
    {
        /// <summary>
        /// ID único do processamento.
        /// </summary>
        public Guid ProcessingId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Data/hora de início do processamento.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Data/hora de término do processamento.
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Tempo total de processamento em milissegundos.
        /// </summary>
        public double TotalProcessingTimeMs { get; set; }

        /// <summary>
        /// Nome do arquivo processado.
        /// </summary>
        public string? FileName { get; set; }

        /// <summary>
        /// Tamanho do arquivo em bytes.
        /// </summary>
        public long? FileSizeBytes { get; set; }

        /// <summary>
        /// Content type do arquivo.
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// Provider de OCR utilizado.
        /// </summary>
        public string? OcrProvider { get; set; }

        /// <summary>
        /// Versão do provider de OCR.
        /// </summary>
        public string? OcrProviderVersion { get; set; }

        /// <summary>
        /// Etapas executadas no pipeline.
        /// </summary>
        public List<PipelineStepMetadataDto> Steps { get; set; } = new();

        /// <summary>
        /// ID do histórico de análise salvo (se aplicável).
        /// </summary>
        public int? AnalysisHistoryId { get; set; }

        /// <summary>
        /// Informações adicionais.
        /// </summary>
        public Dictionary<string, string> AdditionalInfo { get; set; } = new();
    }

    /// <summary>
    /// Metadados de uma etapa do pipeline.
    /// </summary>
    public class PipelineStepMetadataDto
    {
        /// <summary>
        /// Nome da etapa.
        /// </summary>
        public string StepName { get; set; } = string.Empty;

        /// <summary>
        /// Indica se a etapa foi executada com sucesso.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Duração da etapa em milissegundos.
        /// </summary>
        public double DurationMs { get; set; }

        /// <summary>
        /// Mensagem de erro (se Success = false).
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Dados adicionais da etapa.
        /// </summary>
        public Dictionary<string, object>? AdditionalData { get; set; }
    }
}
