using LabelWise.Application.DTOs.ProductIdentification;
using LabelWise.Application.DTOs.LabelReading;

namespace LabelWise.Application.DTOs.Orchestration
{
    /// <summary>
    /// Resultado do pipeline completo de análise de produtos.
    /// Consolida os resultados de todas as etapas: identificação, leitura e análise.
    /// </summary>
    public class ProductAnalysisOrchestrationResult
    {
        /// <summary>
        /// Indica se o pipeline completo foi bem-sucedido.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Nível de confiança geral do pipeline (0.0 a 1.0).
        /// Calculado como média ponderada das confianças de cada etapa.
        /// </summary>
        public double OverallConfidence { get; set; }

        /// <summary>
        /// Status de execução de cada etapa.
        /// </summary>
        public PipelineExecutionStatus ExecutionStatus { get; set; } = new();

        /// <summary>
        /// Resultado da etapa de identificação do produto.
        /// </summary>
        public ProductIdentificationResult? IdentificationResult { get; set; }

        /// <summary>
        /// Resultado da etapa de leitura do rótulo.
        /// </summary>
        public LabelReadingResult? LabelReadingResult { get; set; }

        /// <summary>
        /// Resultado da etapa de análise nutricional.
        /// </summary>
        public ProductAnalysisResultDto? NutritionalAnalysisResult { get; set; }

        /// <summary>
        /// Resultado da validação de qualidade (quality gate).
        /// </summary>
        public QualityGateResult? QualityGateResult { get; set; }

        /// <summary>
        /// Mensagem de erro geral (se Success = false).
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

        /// <summary>
        /// Metadados do pipeline completo.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>
        /// Tempo total de processamento do pipeline em segundos.
        /// </summary>
        public double TotalProcessingTimeSeconds { get; set; }

        /// <summary>
        /// ID do histórico de análise salvo (se aplicável).
        /// </summary>
        public int? AnalysisHistoryId { get; set; }
    }

    /// <summary>
    /// Status de execução de cada etapa do pipeline.
    /// </summary>
    public class PipelineExecutionStatus
    {
        /// <summary>
        /// Status da etapa de identificação.
        /// </summary>
        public StepStatus Identification { get; set; } = new();

        /// <summary>
        /// Status da etapa de leitura do rótulo.
        /// </summary>
        public StepStatus LabelReading { get; set; } = new();

        /// <summary>
        /// Status da etapa de análise nutricional.
        /// </summary>
        public StepStatus NutritionalAnalysis { get; set; } = new();

        /// <summary>
        /// Status da etapa de validação de qualidade.
        /// </summary>
        public StepStatus QualityValidation { get; set; } = new();
    }

    /// <summary>
    /// Status de uma etapa individual do pipeline.
    /// </summary>
    public class StepStatus
    {
        /// <summary>
        /// Indica se a etapa foi executada.
        /// </summary>
        public bool Executed { get; set; }

        /// <summary>
        /// Indica se a etapa foi bem-sucedida.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Confiança desta etapa (0.0 a 1.0).
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Tempo de processamento desta etapa em segundos.
        /// </summary>
        public double ProcessingTimeSeconds { get; set; }

        /// <summary>
        /// Mensagem de erro (se aplicável).
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Resultado da validação de qualidade (quality gate).
    /// </summary>
    public class QualityGateResult
    {
        /// <summary>
        /// Indica se passou no quality gate.
        /// </summary>
        public bool Passed { get; set; }

        /// <summary>
        /// Score de qualidade geral (0.0 a 1.0).
        /// </summary>
        public double QualityScore { get; set; }

        /// <summary>
        /// Avaliação da qualidade do OCR.
        /// </summary>
        public QualityAssessment OcrQuality { get; set; } = new();

        /// <summary>
        /// Avaliação da qualidade do parsing.
        /// </summary>
        public QualityAssessment ParsingQuality { get; set; } = new();

        /// <summary>
        /// Avaliação da qualidade da análise.
        /// </summary>
        public QualityAssessment AnalysisQuality { get; set; } = new();

        /// <summary>
        /// Problemas identificados no quality gate.
        /// </summary>
        public List<string> Issues { get; set; } = new();

        /// <summary>
        /// Sugestões para melhorar a qualidade.
        /// </summary>
        public List<string> Suggestions { get; set; } = new();
    }

    /// <summary>
    /// Avaliação de qualidade de uma dimensão específica.
    /// </summary>
    public class QualityAssessment
    {
        /// <summary>
        /// Score de qualidade (0.0 a 1.0).
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// Nível de qualidade: "Excellent", "Good", "Fair", "Poor".
        /// </summary>
        public string Level { get; set; } = "Unknown";

        /// <summary>
        /// Detalhes da avaliação.
        /// </summary>
        public Dictionary<string, double> Details { get; set; } = new();
    }
}
