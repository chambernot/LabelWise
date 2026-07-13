using System;

namespace LabelWise.Application.DTOs
{
    /// <summary>
    /// Resultado completo do pipeline de análise de produto.
    /// Inclui metadados de cada etapa do processo.
    /// </summary>
    public class ProductAnalysisPipelineResultDto
    {
        // Resultado final da análise
        public ProductAnalysisResultDto AnalysisResult { get; set; } = new();

        // Metadados do pipeline
        public PipelineMetadataDto Metadata { get; set; } = new();
    }

    public class PipelineMetadataDto
    {
        public Guid PipelineId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double TotalDurationMs { get; set; }

        // Provider Information - Visible at top level for easy debugging
        public string? OcrProviderName { get; set; }
        public string? OcrProviderVersion { get; set; }

        // Etapas do pipeline
        public StepMetadata UploadStep { get; set; } = new();
        public StepMetadata OcrStep { get; set; } = new();
        public StepMetadata ParsingStep { get; set; } = new();
        public StepMetadata AnalysisStep { get; set; } = new();
    }

    public class StepMetadata
    {
        public string StepName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public double DurationMs { get; set; }
        public string? ErrorMessage { get; set; }
        public object? AdditionalData { get; set; }
    }
}
