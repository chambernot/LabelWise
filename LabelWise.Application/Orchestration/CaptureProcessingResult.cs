using System;
using System.Collections.Generic;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Orchestration
{
    /// <summary>
    /// Resultado do processamento de uma captura específica.
    /// </summary>
    public class CaptureProcessingResult
    {
        /// <summary>
        /// Tipo de captura processada.
        /// </summary>
        public CaptureType CaptureType { get; set; }

        /// <summary>
        /// Indica se o processamento foi bem-sucedido.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Mensagem de erro, se houver.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Confiança geral do processamento (0.0 a 1.0).
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Tempo de processamento em segundos.
        /// </summary>
        public double ProcessingTimeSeconds { get; set; }

        /// <summary>
        /// Texto bruto extraído via OCR (se aplicável).
        /// </summary>
        public string? RawText { get; set; }

        /// <summary>
        /// Dados estruturados extraídos.
        /// </summary>
        public Dictionary<string, object> ExtractedData { get; set; } = new();

        /// <summary>
        /// Metadata de execução.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>
        /// Warnings ou observações durante o processamento.
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Qualidade do processamento (LOW, MEDIUM, HIGH).
        /// </summary>
        public string? QualityLevel { get; set; }

        /// <summary>
        /// Informações do produto identificadas (se aplicável).
        /// </summary>
        public ProductIdentificationData? ProductIdentification { get; set; }

        /// <summary>
        /// Dados nutricionais extraídos (se aplicável).
        /// </summary>
        public NutritionalData? NutritionalData { get; set; }

        /// <summary>
        /// Ingredientes extraídos (se aplicável).
        /// </summary>
        public List<string>? Ingredients { get; set; }

        /// <summary>
        /// Alergênicos extraídos (se aplicável).
        /// </summary>
        public List<string>? Allergens { get; set; }
    }

    /// <summary>
    /// Dados de identificação do produto.
    /// </summary>
    public class ProductIdentificationData
    {
        public string? Barcode { get; set; }
        public string? ProductName { get; set; }
        public string? Brand { get; set; }
        public string? Category { get; set; }
        public double Confidence { get; set; }
    }

    /// <summary>
    /// Dados nutricionais estruturados.
    /// </summary>
    public class NutritionalData
    {
        public double? Calories { get; set; }
        public double? Protein { get; set; }
        public double? Carbohydrates { get; set; }
        public double? Fats { get; set; }
        public double? Fiber { get; set; }
        public double? Sodium { get; set; }
        public Dictionary<string, double> AdditionalNutrients { get; set; } = new();
        public string? ServingSize { get; set; }
        public double Confidence { get; set; }
    }
}
