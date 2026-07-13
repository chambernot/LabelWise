using System;
using System.Collections.Generic;
using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace LabelWise.Application.Orchestration
{
    /// <summary>
    /// Contexto compartilhado durante o processamento de capturas.
    /// Contém services, configurações e estado temporário.
    /// </summary>
    public class CaptureProcessingContext
    {
        /// <summary>
        /// ID do usuário que está fazendo a requisição.
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// ID do pipeline de execução.
        /// </summary>
        public Guid PipelineId { get; set; }

        /// <summary>
        /// Provider de OCR a ser usado.
        /// </summary>
        public required IOcrProvider OcrProvider { get; set; }

        /// <summary>
        /// Parser de ingredientes e alergênicos.
        /// </summary>
        public required Parsing.IIngredientAllergenParser Parser { get; set; }

        /// <summary>
        /// Motor de análise nutricional.
        /// </summary>
        public required IProductAnalysisEngine AnalysisEngine { get; set; }

        /// <summary>
        /// Logger para diagnóstico.
        /// </summary>
        public ILogger? Logger { get; set; }

        /// <summary>
        /// Configurações de OCR.
        /// </summary>
        public OcrProcessingOptions OcrOptions { get; set; } = new();

        /// <summary>
        /// Threshold de confiança mínima do OCR (0.0 a 1.0).
        /// </summary>
        public double OcrConfidenceThreshold { get; set; } = 0.85;

        /// <summary>
        /// Idioma preferido para OCR.
        /// </summary>
        public string LanguageCode { get; set; } = "pt";

        /// <summary>
        /// Informações acumuladas durante o processamento.
        /// Usado para compartilhar dados entre diferentes capturas.
        /// </summary>
        public Dictionary<string, object> SharedData { get; set; } = new();

        /// <summary>
        /// Metadata de execução.
        /// </summary>
        public Dictionary<string, string> ExecutionMetadata { get; set; } = new();
    }

    /// <summary>
    /// Opções de processamento de OCR.
    /// </summary>
    public class OcrProcessingOptions
    {
        public bool EnableMultiProvider { get; set; } = true;
        public bool EnablePreprocessing { get; set; } = true;
        public bool EnableQualityGate { get; set; } = true;
        public int MaxRetries { get; set; } = 2;
    }
}
