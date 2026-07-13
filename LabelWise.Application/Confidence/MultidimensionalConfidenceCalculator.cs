using LabelWise.Application.Parsing;
using LabelWise.Application.QualityGate;

namespace LabelWise.Application.Confidence
{
    /// <summary>
    /// Motor de cálculo de confiança multidimensional.
    /// Calcula ProductIdentificationConfidence, LabelReadingConfidence e FinalAnalysisConfidence
    /// seguindo regras específicas de negócio.
    /// </summary>
    public class MultidimensionalConfidenceCalculator
    {
        // ═══════════════════════════════════════════════════════════════════
        // PESOS PARA CÁLCULO DE CONFIANÇA
        // ═══════════════════════════════════════════════════════════════════

        private const double ProductNameWeight = 0.50;    // Nome é mais importante
        private const double BrandWeight = 0.30;
        private const double BarcodeWeight = 0.20;

        private const double OcrWeight = 0.25;
        private const double IngredientsWeight = 0.30;
        private const double NutrientsWeight = 0.30;
        private const double AllergensWeight = 0.15;

        private const double IdentificationWeight = 0.30;
        private const double LabelReadingWeight = 0.50;   // Leitura é o mais importante
        private const double AnalysisWeight = 0.20;

        // ═══════════════════════════════════════════════════════════════════
        // THRESHOLDS PARA PENALIZAÇÃO
        // ═══════════════════════════════════════════════════════════════════

        private const double ExcessiveNoiseThreshold = 0.25;
        private const double IncompleteNutrientsThreshold = 0.50;
        private const double InvalidIngredientsThreshold = 0.40;
        private const double MinimumSafeConfidence = 0.70;

        /// <summary>
        /// Calcula a confiança multidimensional completa com base nos dados de entrada.
        /// </summary>
        public MultiDimensionalConfidence Calculate(
            IngredientAllergenParseResult parseResult,
            OcrQualityMetrics ocrMetrics,
            ParsingQualityMetrics parsingMetrics,
            string? barcode = null)
        {
            var result = new MultiDimensionalConfidence();

            // 1. Calcular confiança na identificação do produto
            result.ProductIdentification = CalculateProductIdentificationConfidence(parseResult, barcode);

            // 2. Calcular confiança na leitura do rótulo
            result.LabelReading = CalculateLabelReadingConfidence(parseResult, ocrMetrics, parsingMetrics);

            // 3. Calcular confiança na análise final
            result.FinalAnalysis = CalculateFinalAnalysisConfidence(
                result.ProductIdentification,
                result.LabelReading,
                parsingMetrics);

            // 4. Calcular confiança geral consolidada
            result.OverallConfidence = CalculateOverallConfidence(
                result.ProductIdentification.Score,
                result.LabelReading.Score,
                result.FinalAnalysis.Score);

            // 5. Determinar se passou no quality gate
            result.QualityGatePassed = DetermineQualityGatePassed(result);

            // 6. Gerar resumo de qualidade
            result.QualitySummary = GenerateQualitySummary(result);

            return result;
        }

        /// <summary>
        /// Calcula a confiança na identificação do produto.
        /// </summary>
        private ProductIdentificationConfidence CalculateProductIdentificationConfidence(
            IngredientAllergenParseResult parseResult,
            string? barcode)
        {
            var confidence = new ProductIdentificationConfidence();

            // Avaliar nome do produto
            var productName = parseResult.ProductName ?? string.Empty;
            confidence.ProductNameIdentified = !string.IsNullOrWhiteSpace(productName)
                && productName != "Produto Desconhecido"
                && !productName.Contains("???")
                && productName.Length >= 3;

            confidence.ProductNameScore = confidence.ProductNameIdentified
                ? CalculateProductNameScore(productName)
                : 0.0;

            // Avaliar marca
            confidence.BrandIdentified = !string.IsNullOrWhiteSpace(parseResult.Brand);
            confidence.BrandScore = confidence.BrandIdentified ? 0.85 : 0.0;

            // Avaliar código de barras
            confidence.BarcodeIdentified = !string.IsNullOrWhiteSpace(barcode);
            confidence.BarcodeScore = confidence.BarcodeIdentified ? 0.95 : 0.0;

            // Definir fonte de identificação
            confidence.IdentificationSource = DetermineIdentificationSource(
                confidence.BarcodeIdentified, 
                confidence.ProductNameIdentified, 
                confidence.BrandIdentified);

            // Calcular score composto
            var compositeScore = CalculateWeightedScore(
                (confidence.ProductNameScore, ProductNameWeight),
                (confidence.BrandScore, BrandWeight),
                (confidence.BarcodeScore, BarcodeWeight));

            // REGRA: Se nome não identificado, score não pode ser alto
            if (!confidence.ProductNameIdentified)
            {
                compositeScore = Math.Min(compositeScore, 0.50);
                confidence.Details = "Nome do produto não identificado com segurança.";
            }
            else if (!confidence.BrandIdentified && !confidence.BarcodeIdentified)
            {
                compositeScore *= 0.85; // Pequena penalização
                confidence.Details = "Identificação baseada apenas no nome do produto.";
            }
            else
            {
                confidence.Details = "Produto identificado adequadamente.";
            }

            confidence.Score = new ConfidenceScore(compositeScore, confidence.Details);

            return confidence;
        }

        /// <summary>
        /// Calcula a confiança na leitura do rótulo.
        /// </summary>
        private LabelReadingConfidence CalculateLabelReadingConfidence(
            IngredientAllergenParseResult parseResult,
            OcrQualityMetrics ocrMetrics,
            ParsingQualityMetrics parsingMetrics)
        {
            var confidence = new LabelReadingConfidence();

            // ═══════════════════════════════════════════════════════════════
            // OCR CONFIDENCE
            // ═══════════════════════════════════════════════════════════════
            confidence.OcrReportedConfidence = ocrMetrics.OcrReportedConfidence;
            confidence.ValidWordRatio = ocrMetrics.ValidWordRatio;
            confidence.NoiseRatio = ocrMetrics.NoiseRatio;
            confidence.HasExcessiveNoise = ocrMetrics.NoiseRatio > ExcessiveNoiseThreshold;

            confidence.OcrScore = CalculateOcrScore(ocrMetrics);

            // ═══════════════════════════════════════════════════════════════
            // INGREDIENTS CONFIDENCE
            // ═══════════════════════════════════════════════════════════════
            confidence.IngredientsExtracted = parsingMetrics.HasIngredients;
            confidence.ValidIngredientsCount = parsingMetrics.IngredientsCount - parsingMetrics.InvalidIngredientsCount;
            confidence.InvalidIngredientsRatio = parsingMetrics.InvalidIngredientsRatio;
            confidence.IngredientsHaveExcessiveNoise = parsingMetrics.InvalidIngredientsRatio > InvalidIngredientsThreshold;

            confidence.IngredientsScore = CalculateIngredientsScore(parsingMetrics);

            // ═══════════════════════════════════════════════════════════════
            // NUTRIENTS CONFIDENCE
            // ═══════════════════════════════════════════════════════════════
            confidence.NutrientsExtracted = parsingMetrics.HasNutritionalInfo;
            confidence.NutritionalFieldsCount = parsingMetrics.NutritionalFieldsPopulated;
            confidence.NutritionalCompletenessRatio = parsingMetrics.NutritionalCompletenessRatio;
            confidence.NutrientsIncomplete = parsingMetrics.NutritionalCompletenessRatio < IncompleteNutrientsThreshold;

            confidence.NutrientsScore = CalculateNutrientsScore(parsingMetrics);

            // ═══════════════════════════════════════════════════════════════
            // ALLERGENS CONFIDENCE
            // ═══════════════════════════════════════════════════════════════
            confidence.AllergensCount = parsingMetrics.AllergensCount;
            confidence.AllergensClearlyDetected = parsingMetrics.HasAllergens && parsingMetrics.AllergensCount >= 1;

            confidence.AllergensScore = CalculateAllergensScore(parsingMetrics);

            // ═══════════════════════════════════════════════════════════════
            // SCORE COMPOSTO
            // ═══════════════════════════════════════════════════════════════
            var compositeScore = CalculateWeightedScore(
                (confidence.OcrScore, OcrWeight),
                (confidence.IngredientsScore, IngredientsWeight),
                (confidence.NutrientsScore, NutrientsWeight),
                (confidence.AllergensScore, AllergensWeight));

            // REGRA: Se nutrientes incompletos, reduzir confiança
            if (confidence.NutrientsIncomplete)
            {
                compositeScore *= 0.85;
            }

            // REGRA: Se ingredientes têm ruído excessivo, reduzir confiança
            if (confidence.IngredientsHaveExcessiveNoise)
            {
                compositeScore *= 0.80;
            }

            // REGRA: Se alérgenos claramente detectados, aumentar confiança nessa dimensão
            if (confidence.AllergensClearlyDetected)
            {
                compositeScore = Math.Min(1.0, compositeScore * 1.05);
            }

            confidence.Details = GenerateLabelReadingDetails(confidence);
            confidence.Score = new ConfidenceScore(compositeScore, confidence.Details);

            return confidence;
        }

        /// <summary>
        /// Calcula a confiança na análise final.
        /// </summary>
        private FinalAnalysisConfidence CalculateFinalAnalysisConfidence(
            ProductIdentificationConfidence identification,
            LabelReadingConfidence labelReading,
            ParsingQualityMetrics parsingMetrics)
        {
            var confidence = new FinalAnalysisConfidence();
            var alerts = new List<string>();

            // Base: média ponderada das outras confianças
            var baseScore = (identification.Score.Value * 0.40) + (labelReading.Score.Value * 0.60);

            // ═══════════════════════════════════════════════════════════════
            // APLICAR REGRAS DE PENALIZAÇÃO
            // ═══════════════════════════════════════════════════════════════

            double penalty = 0.0;

            // REGRA 1: Se produto não identificado com segurança, penalizar significativamente
            if (!identification.ProductNameIdentified)
            {
                penalty += 0.25;
                alerts.Add("Produto não identificado com segurança");
            }

            // REGRA 2: Se OCR tem muito ruído, penalizar
            if (labelReading.HasExcessiveNoise)
            {
                penalty += 0.15;
                alerts.Add("OCR com ruído excessivo");
            }

            // REGRA 3: Se ingredientes têm ruído excessivo, penalizar
            if (labelReading.IngredientsHaveExcessiveNoise)
            {
                penalty += 0.15;
                alerts.Add("Ingredientes com ruído excessivo");
            }

            // REGRA 4: Se nutrientes incompletos, penalizar moderadamente
            if (labelReading.NutrientsIncomplete)
            {
                penalty += 0.10;
                alerts.Add("Informações nutricionais incompletas");
            }

            // REGRA 5: Se parsing muito incompleto, penalizar fortemente
            if (parsingMetrics.OverallCompleteness == ParsingCompletenessLevel.Incomplete)
            {
                penalty += 0.20;
                alerts.Add("Leitura muito incompleta do rótulo");
            }

            // Aplicar penalidade (máximo 50%)
            penalty = Math.Min(penalty, ConfidenceThresholds.MaxScorePenalty);
            var adjustedScore = baseScore * (1.0 - penalty);

            confidence.OriginalScore = baseScore;
            confidence.AdjustedScore = adjustedScore;
            confidence.PenaltyApplied = penalty;
            confidence.ConfidenceAlerts = alerts;

            // Determinar se classificação pode ser confiável
            confidence.ClassificationReliable = adjustedScore >= MinimumSafeConfidence;

            confidence.Details = $"Score base: {baseScore:P0}, Penalização: {penalty:P0}, Score ajustado: {adjustedScore:P0}";
            confidence.Score = new ConfidenceScore(adjustedScore, confidence.Details);

            return confidence;
        }

        /// <summary>
        /// Calcula a confiança geral consolidada.
        /// </summary>
        private ConfidenceScore CalculateOverallConfidence(
            ConfidenceScore identification,
            ConfidenceScore labelReading,
            ConfidenceScore analysis)
        {
            var overall = CalculateWeightedScore(
                (identification.Value, IdentificationWeight),
                (labelReading.Value, LabelReadingWeight),
                (analysis.Value, AnalysisWeight));

            return new ConfidenceScore(overall);
        }

        /// <summary>
        /// Determina se a análise passou no quality gate.
        /// </summary>
        private bool DetermineQualityGatePassed(MultiDimensionalConfidence confidence)
        {
            // Passou se:
            // 1. Confiança geral é pelo menos Low
            // 2. OU se tem alguma informação útil (ingredientes ou nutrientes)
            return confidence.OverallConfidence.Level >= ConfidenceLevel.Low
                || confidence.LabelReading.IngredientsExtracted
                || confidence.LabelReading.NutrientsExtracted;
        }

        /// <summary>
        /// Gera um resumo textual da qualidade.
        /// </summary>
        private string GenerateQualitySummary(MultiDimensionalConfidence confidence)
        {
            var overall = confidence.OverallConfidence.Level;

            return overall switch
            {
                ConfidenceLevel.High =>
                    "✅ Análise completa e confiável",
                ConfidenceLevel.Medium =>
                    "⚠️ Análise com algumas limitações - verifique os detalhes",
                ConfidenceLevel.Low =>
                    "⚠️ Análise parcial - recomenda-se nova foto do rótulo",
                ConfidenceLevel.VeryLow =>
                    "❌ Leitura muito limitada - tire uma nova foto com melhor qualidade",
                _ =>
                    "Qualidade desconhecida"
            };
        }

        // ═══════════════════════════════════════════════════════════════════
        // MÉTODOS AUXILIARES DE CÁLCULO
        // ═══════════════════════════════════════════════════════════════════

        private double CalculateProductNameScore(string productName)
        {
            if (string.IsNullOrWhiteSpace(productName)) return 0.0;

            // Base score
            double score = 0.60;

            // Aumentar por características positivas
            if (productName.Length >= 5) score += 0.10;
            if (productName.Length >= 10) score += 0.10;
            if (!productName.Any(c => char.IsDigit(c))) score += 0.05;
            if (!productName.Contains('?')) score += 0.10;
            if (productName.All(c => char.IsLetter(c) || char.IsWhiteSpace(c) || c == '-' || c == '\''))
                score += 0.05;

            return Math.Min(1.0, score);
        }

        private double CalculateOcrScore(OcrQualityMetrics metrics)
        {
            var qualityScore = metrics.OverallQuality switch
            {
                OcrQualityLevel.High => 0.95,
                OcrQualityLevel.Medium => 0.75,
                OcrQualityLevel.Low => 0.50,
                OcrQualityLevel.VeryLow => 0.25,
                _ => 0.50
            };

            // Ajustar com base na confiança reportada
            qualityScore = (qualityScore + metrics.OcrReportedConfidence) / 2.0;

            // Ajustar com base no valid word ratio
            qualityScore = (qualityScore + metrics.ValidWordRatio) / 2.0;

            // Penalizar por ruído
            if (metrics.HasSignificantNoise)
            {
                qualityScore *= 0.80;
            }

            return qualityScore;
        }

        private double CalculateIngredientsScore(ParsingQualityMetrics metrics)
        {
            if (!metrics.HasIngredients) return 0.30; // Score mínimo

            var score = 0.50;

            // Bônus por quantidade de ingredientes
            if (metrics.IngredientsCount >= 10) score += 0.20;
            else if (metrics.IngredientsCount >= 5) score += 0.15;
            else if (metrics.IngredientsCount >= 3) score += 0.10;

            // Bônus por ter ingredientes válidos
            if (metrics.HasValidIngredients) score += 0.15;

            // Penalização por ingredientes inválidos
            score *= (1.0 - (metrics.InvalidIngredientsRatio * 0.5));

            return Math.Clamp(score, 0.0, 1.0);
        }

        private double CalculateNutrientsScore(ParsingQualityMetrics metrics)
        {
            if (!metrics.HasNutritionalInfo) return 0.20; // Score mínimo

            var score = 0.40;

            // Bônus baseado na completude
            score += metrics.NutritionalCompletenessRatio * 0.50;

            // Bônus por ter dados mínimos
            if (metrics.HasMinimalNutritionalData) score += 0.10;

            return Math.Clamp(score, 0.0, 1.0);
        }

        private double CalculateAllergensScore(ParsingQualityMetrics metrics)
        {
            // REGRA: Se alérgenos claramente detectados, score alto
            if (metrics.HasAllergens && metrics.AllergensCount >= 1)
            {
                // Quanto mais alérgenos identificados claramente, mais confiável
                var bonus = Math.Min(metrics.AllergensCount * 0.05, 0.15);
                return 0.85 + bonus;
            }

            // Se não tem alérgenos, score médio (pode ser que não tenha mesmo)
            return 0.60;
        }

        private string DetermineIdentificationSource(bool hasBarcode, bool hasName, bool hasBrand)
        {
            if (hasBarcode) return "Código de Barras";
            if (hasName && hasBrand) return "OCR (Nome + Marca)";
            if (hasName) return "OCR (Nome)";
            return "Não identificado";
        }

        private string GenerateLabelReadingDetails(LabelReadingConfidence confidence)
        {
            var parts = new List<string>();

            if (confidence.HasExcessiveNoise)
                parts.Add("OCR com ruído");

            if (confidence.IngredientsExtracted)
                parts.Add($"{confidence.ValidIngredientsCount} ingredientes");
            else
                parts.Add("Sem ingredientes");

            if (confidence.NutrientsExtracted)
                parts.Add($"{confidence.NutritionalFieldsCount}/10 campos nutricionais");
            else
                parts.Add("Sem nutrientes");

            if (confidence.AllergensClearlyDetected)
                parts.Add($"{confidence.AllergensCount} alérgenos");

            return string.Join(" | ", parts);
        }

        private double CalculateWeightedScore(params (double score, double weight)[] components)
        {
            var totalWeight = components.Sum(c => c.weight);
            var weightedSum = components.Sum(c => c.score * c.weight);
            return totalWeight > 0 ? weightedSum / totalWeight : 0.0;
        }
    }
}
