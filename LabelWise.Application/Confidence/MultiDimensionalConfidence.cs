namespace LabelWise.Application.Confidence
{
    /// <summary>
    /// Representa as três dimensões de confiança da análise de produto.
    /// </summary>
    public class MultiDimensionalConfidence
    {
        /// <summary>
        /// Confiança na identificação do produto (nome, marca, código de barras)
        /// </summary>
        public ProductIdentificationConfidence ProductIdentification { get; set; } = new();

        /// <summary>
        /// Confiança na leitura do rótulo (OCR, parsing de ingredientes e nutrientes)
        /// </summary>
        public LabelReadingConfidence LabelReading { get; set; } = new();

        /// <summary>
        /// Confiança na análise final (scoring, classificação, alertas)
        /// </summary>
        public FinalAnalysisConfidence FinalAnalysis { get; set; } = new();

        /// <summary>
        /// Confiança geral consolidada (combinação ponderada das três dimensões)
        /// </summary>
        public ConfidenceScore OverallConfidence { get; set; }

        /// <summary>
        /// Indica se a análise passou no quality gate
        /// </summary>
        public bool QualityGatePassed { get; set; }

        /// <summary>
        /// Mensagem resumida sobre a qualidade geral
        /// </summary>
        public string QualitySummary { get; set; } = string.Empty;
    }

    /// <summary>
    /// Confiança na identificação do produto
    /// </summary>
    public class ProductIdentificationConfidence
    {
        /// <summary>
        /// Score geral de identificação do produto
        /// </summary>
        public ConfidenceScore Score { get; set; }

        /// <summary>
        /// Nome do produto foi identificado com segurança?
        /// </summary>
        public bool ProductNameIdentified { get; set; }

        /// <summary>
        /// Score de confiança no nome do produto (0.0-1.0)
        /// </summary>
        public double ProductNameScore { get; set; }

        /// <summary>
        /// Marca foi identificada?
        /// </summary>
        public bool BrandIdentified { get; set; }

        /// <summary>
        /// Score de confiança na marca (0.0-1.0)
        /// </summary>
        public double BrandScore { get; set; }

        /// <summary>
        /// Código de barras foi identificado e validado?
        /// </summary>
        public bool BarcodeIdentified { get; set; }

        /// <summary>
        /// Score de confiança no código de barras (0.0-1.0)
        /// </summary>
        public double BarcodeScore { get; set; }

        /// <summary>
        /// Fonte da identificação (OCR, banco de dados, Open Food Facts, etc.)
        /// </summary>
        public string IdentificationSource { get; set; } = "OCR";

        /// <summary>
        /// Detalhes adicionais sobre a identificação
        /// </summary>
        public string Details { get; set; } = string.Empty;
    }

    /// <summary>
    /// Confiança na leitura do rótulo
    /// </summary>
    public class LabelReadingConfidence
    {
        /// <summary>
        /// Score geral da leitura do rótulo
        /// </summary>
        public ConfidenceScore Score { get; set; }

        // ═══════════════════════════════════════════════════════════════════
        // CONFIANÇA NO OCR
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Score de qualidade do OCR (0.0-1.0)
        /// </summary>
        public double OcrScore { get; set; }

        /// <summary>
        /// Confiança reportada pelo provedor de OCR
        /// </summary>
        public double OcrReportedConfidence { get; set; }

        /// <summary>
        /// Ratio de palavras válidas extraídas
        /// </summary>
        public double ValidWordRatio { get; set; }

        /// <summary>
        /// Ratio de ruído no texto extraído
        /// </summary>
        public double NoiseRatio { get; set; }

        /// <summary>
        /// OCR possui ruído excessivo?
        /// </summary>
        public bool HasExcessiveNoise { get; set; }

        // ═══════════════════════════════════════════════════════════════════
        // CONFIANÇA NOS INGREDIENTES
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Score de confiança nos ingredientes (0.0-1.0)
        /// </summary>
        public double IngredientsScore { get; set; }

        /// <summary>
        /// Ingredientes foram extraídos?
        /// </summary>
        public bool IngredientsExtracted { get; set; }

        /// <summary>
        /// Número de ingredientes válidos identificados
        /// </summary>
        public int ValidIngredientsCount { get; set; }

        /// <summary>
        /// Ingredientes possuem ruído excessivo (tokens inválidos)?
        /// </summary>
        public bool IngredientsHaveExcessiveNoise { get; set; }

        /// <summary>
        /// Ratio de ingredientes inválidos
        /// </summary>
        public double InvalidIngredientsRatio { get; set; }

        // ═══════════════════════════════════════════════════════════════════
        // CONFIANÇA NOS NUTRIENTES
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Score de confiança nos nutrientes (0.0-1.0)
        /// </summary>
        public double NutrientsScore { get; set; }

        /// <summary>
        /// Informações nutricionais foram extraídas?
        /// </summary>
        public bool NutrientsExtracted { get; set; }

        /// <summary>
        /// Número de campos nutricionais preenchidos
        /// </summary>
        public int NutritionalFieldsCount { get; set; }

        /// <summary>
        /// Ratio de completude dos campos nutricionais (0.0-1.0)
        /// </summary>
        public double NutritionalCompletenessRatio { get; set; }

        /// <summary>
        /// Informações nutricionais estão incompletas?
        /// </summary>
        public bool NutrientsIncomplete { get; set; }

        // ═══════════════════════════════════════════════════════════════════
        // CONFIANÇA NOS ALÉRGENOS
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Score de confiança nos alérgenos (0.0-1.0)
        /// </summary>
        public double AllergensScore { get; set; }

        /// <summary>
        /// Alérgenos foram claramente detectados?
        /// </summary>
        public bool AllergensClearlyDetected { get; set; }

        /// <summary>
        /// Número de alérgenos identificados
        /// </summary>
        public int AllergensCount { get; set; }

        /// <summary>
        /// Detalhes sobre a leitura
        /// </summary>
        public string Details { get; set; } = string.Empty;
    }

    /// <summary>
    /// Confiança na análise final
    /// </summary>
    public class FinalAnalysisConfidence
    {
        /// <summary>
        /// Score geral da análise final
        /// </summary>
        public ConfidenceScore Score { get; set; }

        /// <summary>
        /// Classificação de risco pode ser confiável?
        /// </summary>
        public bool ClassificationReliable { get; set; }

        /// <summary>
        /// Score original antes de ajustes
        /// </summary>
        public double OriginalScore { get; set; }

        /// <summary>
        /// Score ajustado após quality gate
        /// </summary>
        public double AdjustedScore { get; set; }

        /// <summary>
        /// Penalização aplicada ao score
        /// </summary>
        public double PenaltyApplied { get; set; }

        /// <summary>
        /// Classificação original (antes do quality gate)
        /// </summary>
        public string OriginalClassification { get; set; } = string.Empty;

        /// <summary>
        /// Classificação ajustada (após quality gate)
        /// </summary>
        public string AdjustedClassification { get; set; } = string.Empty;

        /// <summary>
        /// Motivo do ajuste da classificação
        /// </summary>
        public string ClassificationAdjustmentReason { get; set; } = string.Empty;

        /// <summary>
        /// Alertas sobre a confiabilidade da análise
        /// </summary>
        public List<string> ConfidenceAlerts { get; set; } = [];

        /// <summary>
        /// Detalhes sobre a análise
        /// </summary>
        public string Details { get; set; } = string.Empty;
    }
}
