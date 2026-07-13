namespace LabelWise.Application.Confidence
{
    /// <summary>
    /// DTO para representar a confiança multidimensional na resposta da API.
    /// Versão simplificada para serialização JSON.
    /// </summary>
    public class ConfidenceDetailsDto
    {
        /// <summary>
        /// Confiança na identificação do produto
        /// </summary>
        public ConfidenceDimensionDto ProductIdentification { get; set; } = new();

        /// <summary>
        /// Confiança na leitura do rótulo
        /// </summary>
        public ConfidenceDimensionDto LabelReading { get; set; } = new();

        /// <summary>
        /// Confiança na análise final
        /// </summary>
        public ConfidenceDimensionDto FinalAnalysis { get; set; } = new();

        /// <summary>
        /// Confiança geral consolidada
        /// </summary>
        public ConfidenceSummaryDto Overall { get; set; } = new();

        /// <summary>
        /// Detalhes da leitura do rótulo
        /// </summary>
        public LabelReadingDetailsDto LabelReadingDetails { get; set; } = new();

        /// <summary>
        /// Informações sobre ajustes aplicados
        /// </summary>
        public AdjustmentsInfoDto Adjustments { get; set; } = new();

        /// <summary>
        /// Converte de MultiDimensionalConfidence para DTO
        /// </summary>
        public static ConfidenceDetailsDto FromMultiDimensional(
            MultiDimensionalConfidence confidence,
            ClassificationAdjustmentResult? classificationAdjustment = null,
            ScoreAdjustmentResult? scoreAdjustment = null)
        {
            var dto = new ConfidenceDetailsDto
            {
                ProductIdentification = new ConfidenceDimensionDto
                {
                    Score = confidence.ProductIdentification.Score.Value,
                    Level = confidence.ProductIdentification.Score.Level.ToString(),
                    Details = confidence.ProductIdentification.Details,
                    Factors = new Dictionary<string, object>
                    {
                        ["productNameIdentified"] = confidence.ProductIdentification.ProductNameIdentified,
                        ["productNameScore"] = confidence.ProductIdentification.ProductNameScore,
                        ["brandIdentified"] = confidence.ProductIdentification.BrandIdentified,
                        ["brandScore"] = confidence.ProductIdentification.BrandScore,
                        ["barcodeIdentified"] = confidence.ProductIdentification.BarcodeIdentified,
                        ["identificationSource"] = confidence.ProductIdentification.IdentificationSource
                    }
                },
                LabelReading = new ConfidenceDimensionDto
                {
                    Score = confidence.LabelReading.Score.Value,
                    Level = confidence.LabelReading.Score.Level.ToString(),
                    Details = confidence.LabelReading.Details,
                    Factors = new Dictionary<string, object>
                    {
                        ["ocrScore"] = confidence.LabelReading.OcrScore,
                        ["ingredientsScore"] = confidence.LabelReading.IngredientsScore,
                        ["nutrientsScore"] = confidence.LabelReading.NutrientsScore,
                        ["allergensScore"] = confidence.LabelReading.AllergensScore
                    }
                },
                FinalAnalysis = new ConfidenceDimensionDto
                {
                    Score = confidence.FinalAnalysis.Score.Value,
                    Level = confidence.FinalAnalysis.Score.Level.ToString(),
                    Details = confidence.FinalAnalysis.Details,
                    Factors = new Dictionary<string, object>
                    {
                        ["classificationReliable"] = confidence.FinalAnalysis.ClassificationReliable,
                        ["originalScore"] = confidence.FinalAnalysis.OriginalScore,
                        ["adjustedScore"] = confidence.FinalAnalysis.AdjustedScore,
                        ["penaltyApplied"] = confidence.FinalAnalysis.PenaltyApplied
                    }
                },
                Overall = new ConfidenceSummaryDto
                {
                    Score = confidence.OverallConfidence.Value,
                    Level = confidence.OverallConfidence.Level.ToString(),
                    QualityGatePassed = confidence.QualityGatePassed,
                    Summary = confidence.QualitySummary
                },
                LabelReadingDetails = new LabelReadingDetailsDto
                {
                    OcrConfidence = confidence.LabelReading.OcrReportedConfidence,
                    HasExcessiveNoise = confidence.LabelReading.HasExcessiveNoise,
                    NoiseRatio = confidence.LabelReading.NoiseRatio,
                    IngredientsExtracted = confidence.LabelReading.IngredientsExtracted,
                    ValidIngredientsCount = confidence.LabelReading.ValidIngredientsCount,
                    IngredientsHaveExcessiveNoise = confidence.LabelReading.IngredientsHaveExcessiveNoise,
                    NutrientsExtracted = confidence.LabelReading.NutrientsExtracted,
                    NutritionalFieldsCount = confidence.LabelReading.NutritionalFieldsCount,
                    NutritionalCompletenessRatio = confidence.LabelReading.NutritionalCompletenessRatio,
                    NutrientsIncomplete = confidence.LabelReading.NutrientsIncomplete,
                    AllergensClearlyDetected = confidence.LabelReading.AllergensClearlyDetected,
                    AllergensCount = confidence.LabelReading.AllergensCount
                }
            };

            // Preencher ajustes se fornecidos
            if (classificationAdjustment != null || scoreAdjustment != null)
            {
                dto.Adjustments = new AdjustmentsInfoDto
                {
                    ClassificationWasAdjusted = classificationAdjustment?.WasAdjusted ?? false,
                    OriginalClassification = classificationAdjustment?.OriginalClassification ?? string.Empty,
                    AdjustedClassification = classificationAdjustment?.AdjustedClassification ?? string.Empty,
                    ClassificationAdjustmentReason = classificationAdjustment?.AdjustmentReason ?? string.Empty,
                    OriginalGeneralScore = scoreAdjustment?.OriginalGeneralScore ?? 0,
                    AdjustedGeneralScore = scoreAdjustment?.AdjustedGeneralScore ?? 0,
                    OriginalPersonalizedScore = scoreAdjustment?.OriginalPersonalizedScore ?? 0,
                    AdjustedPersonalizedScore = scoreAdjustment?.AdjustedPersonalizedScore ?? 0,
                    ScorePenaltyApplied = scoreAdjustment?.PenaltyApplied ?? 0,
                    ConfidenceAlerts = confidence.FinalAnalysis.ConfidenceAlerts
                };
            }

            return dto;
        }
    }

    /// <summary>
    /// Dimensão individual de confiança
    /// </summary>
    public class ConfidenceDimensionDto
    {
        public double Score { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public Dictionary<string, object> Factors { get; set; } = [];
    }

    /// <summary>
    /// Resumo geral de confiança
    /// </summary>
    public class ConfidenceSummaryDto
    {
        public double Score { get; set; }
        public string Level { get; set; } = string.Empty;
        public bool QualityGatePassed { get; set; }
        public string Summary { get; set; } = string.Empty;
    }

    /// <summary>
    /// Detalhes da leitura do rótulo
    /// </summary>
    public class LabelReadingDetailsDto
    {
        // OCR
        public double OcrConfidence { get; set; }
        public bool HasExcessiveNoise { get; set; }
        public double NoiseRatio { get; set; }

        // Ingredientes
        public bool IngredientsExtracted { get; set; }
        public int ValidIngredientsCount { get; set; }
        public bool IngredientsHaveExcessiveNoise { get; set; }

        // Nutrientes
        public bool NutrientsExtracted { get; set; }
        public int NutritionalFieldsCount { get; set; }
        public double NutritionalCompletenessRatio { get; set; }
        public bool NutrientsIncomplete { get; set; }

        // Alérgenos
        public bool AllergensClearlyDetected { get; set; }
        public int AllergensCount { get; set; }
    }

    /// <summary>
    /// Informações sobre ajustes aplicados
    /// </summary>
    public class AdjustmentsInfoDto
    {
        public bool ClassificationWasAdjusted { get; set; }
        public string OriginalClassification { get; set; } = string.Empty;
        public string AdjustedClassification { get; set; } = string.Empty;
        public string ClassificationAdjustmentReason { get; set; } = string.Empty;

        public double OriginalGeneralScore { get; set; }
        public double AdjustedGeneralScore { get; set; }
        public double OriginalPersonalizedScore { get; set; }
        public double AdjustedPersonalizedScore { get; set; }
        public double ScorePenaltyApplied { get; set; }

        public List<string> ConfidenceAlerts { get; set; } = [];
    }
}
