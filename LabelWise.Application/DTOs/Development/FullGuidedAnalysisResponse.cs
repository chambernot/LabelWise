using System;
using System.Collections.Generic;
using LabelWise.Application.Confidence;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.DTOs.Development
{
    /// <summary>
    /// Response completo da análise guiada de desenvolvimento.
    /// </summary>
    public class FullGuidedAnalysisResponse
    {
        /// <summary>
        /// ID da sessão criada.
        /// </summary>
        public Guid SessionId { get; set; }

        /// <summary>
        /// Timestamp do processamento.
        /// </summary>
        public DateTime ProcessedAt { get; set; }

        /// <summary>
        /// Duração total do processamento.
        /// </summary>
        public TimeSpan TotalDuration { get; set; }

        /// <summary>
        /// Sucesso geral do processamento.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Identificação do produto consolidada.
        /// </summary>
        public ProductIdentificationSummary? ProductIdentification { get; set; }

        /// <summary>
        /// Ingredientes detectados.
        /// </summary>
        public IngredientsDetectionSummary? Ingredients { get; set; }

        /// <summary>
        /// Alérgenos detectados.
        /// </summary>
        public AllergensDetectionSummary? Allergens { get; set; }

        /// <summary>
        /// Informações nutricionais extraídas.
        /// </summary>
        public NutritionalFactsSummary? NutritionalFacts { get; set; }

        /// <summary>
        /// Análise final consolidada.
        /// </summary>
        public FinalAnalysisSummary? FinalAnalysis { get; set; }

        /// <summary>
        /// Detalhes de confiança multidimensional.
        /// </summary>
        public ConfidenceDetailsDto? ConfidenceDetails { get; set; }

        /// <summary>
        /// Etapas que foram processadas.
        /// </summary>
        public List<ProcessedStepMetadata> ProcessedSteps { get; set; } = new();

        /// <summary>
        /// Etapas obrigatórias que estão faltando.
        /// </summary>
        public List<string> MissingRequiredSteps { get; set; } = new();

        /// <summary>
        /// Warnings e avisos gerais.
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Erros ocorridos durante o processamento.
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Metadados adicionais de debug.
        /// </summary>
        public Dictionary<string, object> DebugMetadata { get; set; } = new();
    }

    /// <summary>
    /// Sumário de identificação do produto.
    /// </summary>
    public class ProductIdentificationSummary
    {
        public string? ProductName { get; set; }
        public string? Brand { get; set; }
        public string? Barcode { get; set; }
        public string? Category { get; set; }
        public IdentificationMethod Method { get; set; }
        public double Confidence { get; set; }
        public List<string> AlternativeCandidates { get; set; } = new();
    }

    /// <summary>
    /// Sumário de detecção de ingredientes.
    /// </summary>
    public class IngredientsDetectionSummary
    {
        public List<string> DetectedIngredients { get; set; } = new();
        public int TotalCount { get; set; }
        public double ParseConfidence { get; set; }
        public string? RawText { get; set; }
        public List<string> ProcessingWarnings { get; set; } = new();
    }

    /// <summary>
    /// Sumário de detecção de alérgenos.
    /// </summary>
    public class AllergensDetectionSummary
    {
        public List<string> DeclaredAllergens { get; set; } = new();
        public List<string> MayContainAllergens { get; set; } = new();
        public List<string> InferredFromIngredients { get; set; } = new();
        public double DetectionConfidence { get; set; }
        public string? RawText { get; set; }
    }

    /// <summary>
    /// Sumário de informações nutricionais.
    /// </summary>
    public class NutritionalFactsSummary
    {
        public Dictionary<string, NutrientValue> Nutrients { get; set; } = new();
        public string? ServingSize { get; set; }
        public double? Calories { get; set; }
        public double ParseConfidence { get; set; }
        public int NutrientsDetected { get; set; }
        public List<string> ParsingIssues { get; set; } = new();
    }

    /// <summary>
    /// Valor de nutriente extraído.
    /// </summary>
    public class NutrientValue
    {
        public string Name { get; set; } = string.Empty;
        public double? ValuePer100g { get; set; }
        public double? ValuePerServing { get; set; }
        public string? Unit { get; set; }
        public double? DailyValuePercent { get; set; }
    }

    /// <summary>
    /// Sumário da análise final.
    /// </summary>
    public class FinalAnalysisSummary
    {
        public int? ProductAnalysisId { get; set; }
        public AnalysisClassification Classification { get; set; }
        public double OverallScore { get; set; }
        public List<string> Alerts { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public Domain.Enums.ConfidenceLevel OverallConfidence { get; set; }
    }

    /// <summary>
    /// Metadados de cada etapa processada.
    /// </summary>
    public class ProcessedStepMetadata
    {
        public CaptureType CaptureType { get; set; }
        public string StepName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public TimeSpan Duration { get; set; }
        public long FileSizeBytes { get; set; }
        public string? FileStoragePath { get; set; }
        public OcrStepResult? OcrResult { get; set; }
        public ParsingStepResult? ParsingResult { get; set; }
        public List<string> StepWarnings { get; set; } = new();
        public List<string> StepErrors { get; set; } = new();
    }

    /// <summary>
    /// Resultado da etapa de OCR.
    /// </summary>
    public class OcrStepResult
    {
        public bool Success { get; set; }
        public double Confidence { get; set; }
        public int TextLength { get; set; }
        public string? PreviewText { get; set; }
        public TimeSpan OcrDuration { get; set; }
        public string? Provider { get; set; }
    }

    /// <summary>
    /// Resultado da etapa de parsing.
    /// </summary>
    public class ParsingStepResult
    {
        public bool Success { get; set; }
        public double Confidence { get; set; }
        public int ItemsExtracted { get; set; }
        public Dictionary<string, object> ExtractedData { get; set; } = new();
        public TimeSpan ParsingDuration { get; set; }
    }
}
