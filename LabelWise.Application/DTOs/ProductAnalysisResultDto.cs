using System;
using System.Collections.Generic;
using LabelWise.Application.Confidence;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.DTOs
{
    public class ProductAnalysisResultDto
    {
        // Identifiers
        public Guid? AnalysisId { get; set; }
        public Guid? ProductId { get; set; }

        // Product Info
        public string ProductName { get; set; } = string.Empty;
        public string? Brand { get; set; }

        // Analysis Summary
        public string Summary { get; set; } = string.Empty;
        public string? ShortSummary { get; set; }

        // Scores
        public double GeneralScore { get; set; }
        public double PersonalizedScore { get; set; }

        // Classification
        public string Classification { get; set; } = string.Empty;

        /// <summary>
        /// Nível de confiança simplificado (legado - para compatibilidade)
        /// </summary>
        public string ConfidenceLevel { get; set; } = string.Empty;

        /// <summary>
        /// Confiança multidimensional detalhada (novo sistema)
        /// Inclui ProductIdentificationConfidence, LabelReadingConfidence e FinalAnalysisConfidence
        /// </summary>
        public ConfidenceDetailsDto? ConfidenceDetails { get; set; }

        // Alerts and Recommendations
        public List<string> Alerts { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();

        // Extracted Data
        public List<string> ExtractedIngredients { get; set; } = new();
        public List<string> ExtractedAllergens { get; set; } = new();
        public string? ExtractedText { get; set; }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // PARTIAL ANALYSIS SUPPORT
        // ═══════════════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Indica se esta é uma análise parcial (apenas parte do rótulo foi capturada).
        /// </summary>
        public bool IsPartialAnalysis { get; set; }

        /// <summary>
        /// Tipo de captura que gerou esta análise (NutritionTable, IngredientsList, etc.).
        /// </summary>
        public CaptureType? CaptureType { get; set; }

        /// <summary>
        /// Lista de passos/capturas faltantes para completar a análise.
        /// Ex: ["IngredientsList", "FrontPackaging"]
        /// </summary>
        public List<string> MissingSteps { get; set; } = new();

        /// <summary>
        /// Dados nutricionais estruturados extraídos (quando CaptureType = NutritionTable).
        /// </summary>
        public NutritionalFactsDto? NutritionalFacts { get; set; }

        // ═══════════════════════════════════════════════════════════════════════════════════════

        // Metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Dados nutricionais estruturados extraídos de uma tabela nutricional.
    /// </summary>
    public class NutritionalFactsDto
    {
        /// <summary>
        /// Tamanho da porção (ex: "30 g", "200 ml").
        /// </summary>
        public string? ServingSize { get; set; }

        /// <summary>
        /// Número de porções por embalagem.
        /// </summary>
        public int? ServingsPerContainer { get; set; }

        /// <summary>
        /// Valor energético em kcal.
        /// </summary>
        public double? Calories { get; set; }

        /// <summary>
        /// Carboidratos totais em gramas.
        /// </summary>
        public double? TotalCarbohydrate { get; set; }

        /// <summary>
        /// Açúcares totais em gramas.
        /// </summary>
        public double? Sugars { get; set; }

        /// <summary>
        /// Açúcares adicionados em gramas.
        /// </summary>
        public double? AddedSugars { get; set; }

        /// <summary>
        /// Lactose em gramas.
        /// </summary>
        public double? Lactose { get; set; }

        /// <summary>
        /// Proteínas em gramas.
        /// </summary>
        public double? Protein { get; set; }

        /// <summary>
        /// Gorduras totais em gramas.
        /// </summary>
        public double? TotalFat { get; set; }

        /// <summary>
        /// Gorduras saturadas em gramas.
        /// </summary>
        public double? SaturatedFat { get; set; }

        /// <summary>
        /// Gorduras trans em gramas.
        /// </summary>
        public double? TransFat { get; set; }

        /// <summary>
        /// Colesterol em mg.
        /// </summary>
        public double? Cholesterol { get; set; }

        /// <summary>
        /// Sódio em mg.
        /// </summary>
        public double? Sodium { get; set; }

        /// <summary>
        /// Fibra alimentar em gramas.
        /// </summary>
        public double? DietaryFiber { get; set; }

        /// <summary>
        /// Cálcio em mg.
        /// </summary>
        public double? Calcium { get; set; }

        /// <summary>
        /// Ferro em mg.
        /// </summary>
        public double? Iron { get; set; }

        /// <summary>
        /// % Valores Diários para cada nutriente.
        /// </summary>
        public Dictionary<string, double> DailyValuePercentages { get; set; } = new();

        /// <summary>
        /// Contagem de campos nutricionais preenchidos.
        /// </summary>
        public int ExtractedFieldsCount { get; set; }

        /// <summary>
        /// Indica se a tabela está completa (macros principais + sódio).
        /// </summary>
        public bool IsComplete { get; set; }
    }
}
