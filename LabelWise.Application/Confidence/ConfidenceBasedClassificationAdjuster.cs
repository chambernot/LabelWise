using LabelWise.Application.Parsing;
using LabelWise.Application.QualityGate;

namespace LabelWise.Application.Confidence
{
    /// <summary>
    /// Aplicador de regras de classificação baseadas na confiança multidimensional.
    /// Garante que classificações finais sejam coerentes com a confiança calculada.
    /// </summary>
    public class ConfidenceBasedClassificationAdjuster
    {
        /// <summary>
        /// Ajusta a classificação do produto baseado nas regras de confiança.
        /// </summary>
        /// <param name="originalClassification">Classificação original do motor de regras</param>
        /// <param name="confidence">Confiança multidimensional calculada</param>
        /// <returns>Resultado do ajuste com classificação final e razão</returns>
        public ClassificationAdjustmentResult AdjustClassification(
            string originalClassification,
            MultiDimensionalConfidence confidence)
        {
            var result = new ClassificationAdjustmentResult
            {
                OriginalClassification = originalClassification,
                AdjustedClassification = originalClassification,
                WasAdjusted = false
            };

            // ═══════════════════════════════════════════════════════════════
            // REGRA 1: Se produto não identificado com segurança, 
            //          classificação final não pode ser "Safe"
            // ═══════════════════════════════════════════════════════════════
            if (!confidence.ProductIdentification.ProductNameIdentified)
            {
                if (originalClassification == "Safe" || originalClassification == "Excellent")
                {
                    result.AdjustedClassification = "Incomplete";
                    result.WasAdjusted = true;
                    result.AdjustmentReason = "Produto não identificado com segurança";
                    result.AdjustmentRule = ClassificationAdjustmentRule.ProductNotIdentified;
                    return result;
                }
            }

            // ═══════════════════════════════════════════════════════════════
            // REGRA 2: Se confiança geral é VeryLow ou Low, 
            //          classificação não pode ser "Safe"
            // ═══════════════════════════════════════════════════════════════
            if (confidence.OverallConfidence.Level <= ConfidenceLevel.Low)
            {
                if (originalClassification == "Safe" || originalClassification == "Excellent")
                {
                    result.AdjustedClassification = "Caution";
                    result.WasAdjusted = true;
                    result.AdjustmentReason = $"Confiança geral {confidence.OverallConfidence.Level} insuficiente para classificação Safe";
                    result.AdjustmentRule = ClassificationAdjustmentRule.LowOverallConfidence;
                    return result;
                }
            }

            // ═══════════════════════════════════════════════════════════════
            // REGRA 3: Se análise final não é confiável, 
            //          não permitir classificações extremas positivas
            // ═══════════════════════════════════════════════════════════════
            if (!confidence.FinalAnalysis.ClassificationReliable)
            {
                if (originalClassification == "Safe" || originalClassification == "Excellent")
                {
                    result.AdjustedClassification = "Caution";
                    result.WasAdjusted = true;
                    result.AdjustmentReason = "Análise não suficientemente confiável";
                    result.AdjustmentRule = ClassificationAdjustmentRule.UnreliableAnalysis;
                    return result;
                }
            }

            // ═══════════════════════════════════════════════════════════════
            // REGRA 4: Se nutrientes estão incompletos e ingredientes têm ruído,
            //          não pode ser "Safe"
            // ═══════════════════════════════════════════════════════════════
            if (confidence.LabelReading.NutrientsIncomplete && 
                confidence.LabelReading.IngredientsHaveExcessiveNoise)
            {
                if (originalClassification == "Safe" || originalClassification == "Excellent")
                {
                    result.AdjustedClassification = "Incomplete";
                    result.WasAdjusted = true;
                    result.AdjustmentReason = "Leitura de nutrientes e ingredientes incompleta";
                    result.AdjustmentRule = ClassificationAdjustmentRule.IncompleteLabelReading;
                    return result;
                }
            }

            // ═══════════════════════════════════════════════════════════════
            // REGRA 5: Se confiança média mas identificação baixa, 
            //          rebaixar de Safe para Moderate
            // ═══════════════════════════════════════════════════════════════
            if (confidence.OverallConfidence.Level == ConfidenceLevel.Medium &&
                confidence.ProductIdentification.Score.Level == ConfidenceLevel.Low)
            {
                if (originalClassification == "Safe")
                {
                    result.AdjustedClassification = "Moderate";
                    result.WasAdjusted = true;
                    result.AdjustmentReason = "Identificação do produto com baixa confiança";
                    result.AdjustmentRule = ClassificationAdjustmentRule.LowIdentificationConfidence;
                    return result;
                }
            }

            // ═══════════════════════════════════════════════════════════════
            // REGRA 6: Se alérgenos claramente detectados mas classificação não reflete,
            //          manter Caution no mínimo
            // ═══════════════════════════════════════════════════════════════
            if (confidence.LabelReading.AllergensClearlyDetected && 
                confidence.LabelReading.AllergensCount >= 3 &&
                originalClassification == "Safe")
            {
                result.AdjustedClassification = "Caution";
                result.WasAdjusted = true;
                result.AdjustmentReason = $"Múltiplos alérgenos detectados ({confidence.LabelReading.AllergensCount})";
                result.AdjustmentRule = ClassificationAdjustmentRule.AllergensDetected;
                return result;
            }

            return result;
        }

        /// <summary>
        /// Ajusta os scores baseado na confiança.
        /// </summary>
        public ScoreAdjustmentResult AdjustScores(
            double originalGeneralScore,
            double originalPersonalizedScore,
            MultiDimensionalConfidence confidence)
        {
            var result = new ScoreAdjustmentResult
            {
                OriginalGeneralScore = originalGeneralScore,
                OriginalPersonalizedScore = originalPersonalizedScore
            };

            // Penalização baseada na confiança
            var penalty = confidence.FinalAnalysis.PenaltyApplied;

            // Penalização adicional por dimensões específicas
            if (confidence.ProductIdentification.Score.Level == ConfidenceLevel.VeryLow)
                penalty += 0.10;

            if (confidence.LabelReading.Score.Level == ConfidenceLevel.VeryLow)
                penalty += 0.15;

            // Limitar penalização total
            penalty = Math.Min(penalty, ConfidenceThresholds.MaxScorePenalty);

            result.PenaltyApplied = penalty;
            result.AdjustedGeneralScore = Math.Max(0, originalGeneralScore * (1 - penalty));
            result.AdjustedPersonalizedScore = Math.Max(0, originalPersonalizedScore * (1 - penalty));

            return result;
        }
    }

    /// <summary>
    /// Resultado do ajuste de classificação
    /// </summary>
    public class ClassificationAdjustmentResult
    {
        public string OriginalClassification { get; set; } = string.Empty;
        public string AdjustedClassification { get; set; } = string.Empty;
        public bool WasAdjusted { get; set; }
        public string AdjustmentReason { get; set; } = string.Empty;
        public ClassificationAdjustmentRule AdjustmentRule { get; set; }
    }

    /// <summary>
    /// Resultado do ajuste de scores
    /// </summary>
    public class ScoreAdjustmentResult
    {
        public double OriginalGeneralScore { get; set; }
        public double OriginalPersonalizedScore { get; set; }
        public double AdjustedGeneralScore { get; set; }
        public double AdjustedPersonalizedScore { get; set; }
        public double PenaltyApplied { get; set; }
    }

    /// <summary>
    /// Regras de ajuste de classificação aplicadas
    /// </summary>
    public enum ClassificationAdjustmentRule
    {
        None = 0,
        ProductNotIdentified = 1,
        LowOverallConfidence = 2,
        UnreliableAnalysis = 3,
        IncompleteLabelReading = 4,
        LowIdentificationConfidence = 5,
        AllergensDetected = 6
    }
}
