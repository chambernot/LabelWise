using LabelWise.Application.Confidence;
using ConfLevel = LabelWise.Application.Confidence.ConfidenceLevel;

namespace LabelWise.Application.SummaryGeneration
{
    /// <summary>
    /// Contexto de análise contendo informações de completude e confiança.
    /// Utilizado para gerar resumos seguros que refletem a qualidade dos dados.
    /// </summary>
    public class AnalysisContext
    {
        /// <summary>
        /// Indica se o produto foi identificado com sucesso.
        /// </summary>
        public bool ProductIdentified { get; set; }

        /// <summary>
        /// Indica se o OCR foi completo (texto extraído com qualidade suficiente).
        /// </summary>
        public bool OcrComplete { get; set; }

        /// <summary>
        /// Indica se a análise está completa (ingredientes, nutrientes e alérgenos disponíveis).
        /// </summary>
        public bool AnalysisComplete { get; set; }

        /// <summary>
        /// Indica se alérgenos foram declarados no produto.
        /// </summary>
        public bool HasDeclaredAllergens { get; set; }

        /// <summary>
        /// Indica se há alérgenos que coincidem com restrições do usuário.
        /// </summary>
        public bool HasMatchingUserAllergens { get; set; }

        /// <summary>
        /// Confiança multidimensional detalhada.
        /// </summary>
        public MultiDimensionalConfidence? Confidence { get; set; }

        /// <summary>
        /// Nível de confiança geral simplificado.
        /// </summary>
        public ConfLevel OverallConfidenceLevel { get; set; }

        /// <summary>
        /// Indica se a análise passou no quality gate.
        /// </summary>
        public bool QualityGatePassed { get; set; }

        /// <summary>
        /// Score de qualidade do OCR (0.0 a 1.0).
        /// </summary>
        public double OcrQualityScore { get; set; }

        /// <summary>
        /// Score de completude dos ingredientes (0.0 a 1.0).
        /// </summary>
        public double IngredientsCompletenessScore { get; set; }

        /// <summary>
        /// Score de completude nutricional (0.0 a 1.0).
        /// </summary>
        public double NutritionalCompletenessScore { get; set; }

        /// <summary>
        /// Número de ingredientes válidos identificados.
        /// </summary>
        public int ValidIngredientsCount { get; set; }

        /// <summary>
        /// Número de alérgenos identificados.
        /// </summary>
        public int AllergensCount { get; set; }

        /// <summary>
        /// Número de campos nutricionais preenchidos.
        /// </summary>
        public int NutritionalFieldsCount { get; set; }

        /// <summary>
        /// Cria um contexto de análise a partir de confiança multidimensional.
        /// </summary>
        public static AnalysisContext FromMultiDimensionalConfidence(MultiDimensionalConfidence confidence)
        {
            return new AnalysisContext
            {
                ProductIdentified = confidence.ProductIdentification.ProductNameIdentified ||
                                   confidence.ProductIdentification.BarcodeIdentified,
                OcrComplete = confidence.LabelReading.OcrScore >= 0.6 &&
                             !confidence.LabelReading.HasExcessiveNoise,
                AnalysisComplete = confidence.QualityGatePassed &&
                                  confidence.OverallConfidence.Level >= ConfLevel.Medium,
                HasDeclaredAllergens = confidence.LabelReading.AllergensCount > 0,
                Confidence = confidence,
                OverallConfidenceLevel = confidence.OverallConfidence.Level,
                QualityGatePassed = confidence.QualityGatePassed,
                OcrQualityScore = confidence.LabelReading.OcrScore,
                IngredientsCompletenessScore = confidence.LabelReading.IngredientsScore,
                NutritionalCompletenessScore = confidence.LabelReading.NutrientsScore,
                ValidIngredientsCount = confidence.LabelReading.ValidIngredientsCount,
                AllergensCount = confidence.LabelReading.AllergensCount,
                NutritionalFieldsCount = confidence.LabelReading.NutritionalFieldsCount
            };
        }

        /// <summary>
        /// Cria um contexto padrão para análises sem informação de confiança.
        /// </summary>
        public static AnalysisContext CreateDefault() => new()
        {
            ProductIdentified = false,
            OcrComplete = false,
            AnalysisComplete = false,
            HasDeclaredAllergens = false,
            OverallConfidenceLevel = ConfLevel.Low,
            QualityGatePassed = false
        };

        /// <summary>
        /// Indica se a análise é parcial (não completa).
        /// </summary>
        public bool IsPartialAnalysis => !AnalysisComplete || !OcrComplete || !ProductIdentified;

        /// <summary>
        /// Indica se a confiança é alta o suficiente para mensagens afirmativas.
        /// </summary>
        public bool CanUseAffirmativeMessages =>
            OverallConfidenceLevel == ConfLevel.High &&
            AnalysisComplete &&
            QualityGatePassed;

        /// <summary>
        /// Indica se deve usar classificação conservadora.
        /// </summary>
        public bool RequiresConservativeClassification =>
            !ProductIdentified ||
            !AnalysisComplete ||
            HasDeclaredAllergens ||
            OverallConfidenceLevel < ConfLevel.Medium;
    }
}
