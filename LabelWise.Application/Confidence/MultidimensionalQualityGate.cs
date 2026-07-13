using System;
using LabelWise.Application.DTOs;
using LabelWise.Application.Parsing;

namespace LabelWise.Application.Confidence
{
    /// <summary>
    /// Quality Gate atualizado que utiliza o sistema de confiança multidimensional.
    /// Coordena o cálculo de confiança, ajuste de classificação e geração de resumos.
    /// </summary>
    public class MultidimensionalQualityGate
    {
        private readonly MultidimensionalConfidenceCalculator _confidenceCalculator;
        private readonly ConfidenceBasedClassificationAdjuster _classificationAdjuster;

        public MultidimensionalQualityGate()
        {
            _confidenceCalculator = new MultidimensionalConfidenceCalculator();
            _classificationAdjuster = new ConfidenceBasedClassificationAdjuster();
        }

        /// <summary>
        /// Aplica o Quality Gate completo no resultado da análise.
        /// </summary>
        public MultidimensionalQualityGateResult ApplyQualityGate(
            ProductAnalysisResultDto analysisResult,
            string extractedText,
            double ocrConfidence,
            IngredientAllergenParseResult parseResult,
            string? barcode = null)
        {
            // ═══════════════════════════════════════════════════════════════════════════════
            // TRATAMENTO ESPECIAL PARA ANÁLISES PARCIAIS
            // ═══════════════════════════════════════════════════════════════════════════════
            if (parseResult.IsPartialAnalysis)
            {
                return ApplyPartialAnalysisQualityGate(analysisResult, extractedText, ocrConfidence, parseResult);
            }

            // ═══════════════════════════════════════════════════════════════════════════════
            // FLUXO NORMAL PARA ANÁLISES COMPLETAS
            // ═══════════════════════════════════════════════════════════════════════════════

            // 1. Criar métricas do OCR
            var ocrAssessor = new QualityGate.OcrQualityAssessor();
            var ocrMetrics = ocrAssessor.AssessQuality(extractedText, ocrConfidence);

            // 2. Criar métricas do parsing
            var parsingAssessor = new QualityGate.ParsingQualityAssessor();
            var parsingMetrics = parsingAssessor.AssessQuality(parseResult);

            // 3. Calcular confiança multidimensional
            var confidence = _confidenceCalculator.Calculate(
                parseResult, ocrMetrics, parsingMetrics, barcode);

            // 4. Ajustar classificação baseada na confiança
            var classificationAdjustment = _classificationAdjuster.AdjustClassification(
                analysisResult.Classification ?? "Safe", confidence);

            // 5. Ajustar scores baseados na confiança
            var scoreAdjustment = _classificationAdjuster.AdjustScores(
                analysisResult.GeneralScore, analysisResult.PersonalizedScore, confidence);

            // 6. Atualizar confiança final com informações de ajuste
            confidence.FinalAnalysis.OriginalClassification = classificationAdjustment.OriginalClassification;
            confidence.FinalAnalysis.AdjustedClassification = classificationAdjustment.AdjustedClassification;
            confidence.FinalAnalysis.ClassificationAdjustmentReason = classificationAdjustment.AdjustmentReason;

            // 7. Gerar resumos coerentes
            var adjustedSummary = GenerateCoherentSummary(
                analysisResult, confidence, classificationAdjustment);

            var adjustedShortSummary = GenerateCoherentShortSummary(
                confidence, classificationAdjustment, scoreAdjustment.AdjustedPersonalizedScore);

            // 8. Criar DTO de confiança para resposta
            var confidenceDto = ConfidenceDetailsDto.FromMultiDimensional(
                confidence, classificationAdjustment, scoreAdjustment);

            // 9. Criar resultado do quality gate
            return new MultidimensionalQualityGateResult
            {
                Passed = confidence.QualityGatePassed,

                // Confiança multidimensional
                Confidence = confidence,
                ConfidenceDto = confidenceDto,

                // Ajustes de classificação
                ClassificationAdjustment = classificationAdjustment,
                ScoreAdjustment = scoreAdjustment,

                // Valores finais ajustados
                AdjustedClassification = classificationAdjustment.AdjustedClassification,
                AdjustedGeneralScore = scoreAdjustment.AdjustedGeneralScore,
                AdjustedPersonalizedScore = scoreAdjustment.AdjustedPersonalizedScore,
                AdjustedSummary = adjustedSummary,
                AdjustedShortSummary = adjustedShortSummary,

                // Para compatibilidade com sistema legado
                LegacyConfidenceLevel = MapToLegacyConfidenceLevel(confidence.OverallConfidence.Level),

                // Mensagem de qualidade
                QualityMessage = confidence.QualitySummary
            };
        }

        /// <summary>
        /// Quality Gate especializado para análises parciais (NutritionTable, IngredientsList, etc.)
        /// Não penaliza por falta de identificação do produto.
        /// </summary>
        private MultidimensionalQualityGateResult ApplyPartialAnalysisQualityGate(
            ProductAnalysisResultDto analysisResult,
            string extractedText,
            double ocrConfidence,
            IngredientAllergenParseResult parseResult)
        {
            // Para análises parciais, a confiança é baseada principalmente no OCR e no parsing
            // Não penalizamos por falta de ProductName/Brand

            var ocrAssessor = new QualityGate.OcrQualityAssessor();
            var ocrMetrics = ocrAssessor.AssessQuality(extractedText, ocrConfidence);

            // Calcular confiança focada no conteúdo disponível
            var hasNutritionData = parseResult.Nutrition?.HasData ?? false;
            var nutritionalFieldsCount = parseResult.Nutrition?.FilledFieldsCount ?? 0;
            var hasIngredients = parseResult.HasIngredients;
            var hasAllergens = parseResult.HasAllergens;
            var allergensCount = parseResult.Allergens?.Count ?? 0;

            // ═══════════════════════════════════════════════════════════════════
            // CÁLCULO DE SCORE PARA ANÁLISE PARCIAL
            // ═══════════════════════════════════════════════════════════════════
            double partialConfidenceScore = 0.5; // Base

            // Bônus por dados nutricionais (até +0.35 por 14 campos)
            if (hasNutritionData)
            {
                partialConfidenceScore += Math.Min(0.35, nutritionalFieldsCount * 0.025);
            }

            // Bônus por ingredientes (até +0.15)
            if (hasIngredients)
            {
                partialConfidenceScore += Math.Min(0.15, parseResult.Ingredients.Count * 0.01);
            }

            // Bônus por alérgenos detectados
            if (hasAllergens)
            {
                partialConfidenceScore += 0.05;
            }

            // Ajustar pela confiança do OCR (peso 50%)
            partialConfidenceScore *= (0.5 + ocrConfidence * 0.5);

            // Cap máximo para análise parcial: 85%
            partialConfidenceScore = Math.Min(0.85, partialConfidenceScore);

            // ═══════════════════════════════════════════════════════════════════
            // DETERMINAR NÍVEL DE CONFIANÇA CONSISTENTE
            // ═══════════════════════════════════════════════════════════════════
            // Usar os mesmos thresholds que ConfidenceScore para consistência
            var confidenceLevel = partialConfidenceScore >= ConfidenceThresholds.High ? ConfidenceLevel.High :
                                  partialConfidenceScore >= ConfidenceThresholds.Medium ? ConfidenceLevel.Medium :
                                  partialConfidenceScore >= ConfidenceThresholds.Low ? ConfidenceLevel.Low :
                                  ConfidenceLevel.VeryLow;

            // ═══════════════════════════════════════════════════════════════════
            // CALCULAR SCORES INDIVIDUAIS PARA CADA DIMENSÃO
            // ═══════════════════════════════════════════════════════════════════
            // Score de leitura de rótulo para análise parcial
            double labelReadingScore = 0.4; // Base para análise parcial
            if (hasNutritionData)
            {
                // Completude nutricional contribui até 0.5 adicional
                var completenessRatio = Math.Min(1.0, nutritionalFieldsCount / 12.0); // 12 campos = 100%
                labelReadingScore += completenessRatio * 0.5;
            }
            if (hasIngredients)
            {
                labelReadingScore += 0.1;
            }
            labelReadingScore *= ocrConfidence; // Ajustar pela qualidade do OCR

            // Nutrient score
            double nutrientsScore = hasNutritionData 
                ? Math.Min(1.0, 0.5 + (nutritionalFieldsCount / 24.0)) // 12 campos = 1.0
                : 0.0;

            // Gerar descrição baseada no tipo de captura
            var captureTypeDescription = parseResult.SourceCaptureType switch
            {
                Domain.Enums.CaptureType.NutritionTable => hasNutritionData 
                    ? $"Tabela nutricional lida com sucesso ({nutritionalFieldsCount} campos extraídos)"
                    : "Tabela nutricional detectada mas dados incompletos",
                Domain.Enums.CaptureType.IngredientsList => hasIngredients
                    ? $"Lista de ingredientes lida ({parseResult.Ingredients.Count} ingredientes)"
                    : "Lista de ingredientes detectada mas dados incompletos",
                Domain.Enums.CaptureType.AllergenStatement => hasAllergens
                    ? $"Declaração de alérgenos lida ({allergensCount} alérgenos)"
                    : "Declaração de alérgenos detectada",
                _ => "Captura parcial processada"
            };

            // ═══════════════════════════════════════════════════════════════════
            // CRIAR RESULTADO DE CONFIANÇA MULTIDIMENSIONAL
            // ═══════════════════════════════════════════════════════════════════
            var confidence = new MultiDimensionalConfidence
            {
                ProductIdentification = new ProductIdentificationConfidence
                {
                    ProductNameIdentified = false, // Esperado para análise parcial
                    BrandIdentified = false,
                    ProductNameScore = 0,
                    BrandScore = 0,
                    BarcodeIdentified = !string.IsNullOrEmpty(parseResult.Barcode),
                    BarcodeScore = !string.IsNullOrEmpty(parseResult.Barcode) ? 1.0 : 0,
                    IdentificationSource = "Partial Capture",
                    Score = new ConfidenceScore(0.3, "Análise parcial - identificação não requerida"),
                    Details = "Captura parcial não inclui identificação do produto"
                },
                LabelReading = new LabelReadingConfidence
                {
                    OcrReportedConfidence = ocrConfidence,
                    OcrScore = ocrConfidence,
                    ValidWordRatio = ocrMetrics?.ValidWordRatio ?? 0.8,
                    NoiseRatio = ocrMetrics?.NoiseRatio ?? 0.1,
                    HasExcessiveNoise = ocrMetrics?.HasSignificantNoise ?? false,

                    // ══════ DADOS DE INGREDIENTES ══════
                    IngredientsExtracted = hasIngredients,
                    IngredientsScore = hasIngredients ? 0.8 : 0,
                    ValidIngredientsCount = parseResult.Ingredients?.Count ?? 0,
                    IngredientsHaveExcessiveNoise = false,
                    InvalidIngredientsRatio = 0,

                    // ══════ DADOS NUTRICIONAIS - CHAVE PARA CONSISTÊNCIA ══════
                    NutrientsExtracted = hasNutritionData,
                    NutrientsScore = nutrientsScore,
                    NutritionalFieldsCount = nutritionalFieldsCount,
                    NutritionalCompletenessRatio = Math.Min(1.0, nutritionalFieldsCount / 12.0),
                    NutrientsIncomplete = nutritionalFieldsCount < 5,

                    // ══════ DADOS DE ALÉRGENOS ══════
                    AllergensClearlyDetected = hasAllergens,
                    AllergensScore = hasAllergens ? 0.9 : 0,
                    AllergensCount = allergensCount,

                    Score = new ConfidenceScore(labelReadingScore, captureTypeDescription),
                    Details = captureTypeDescription
                },
                FinalAnalysis = new FinalAnalysisConfidence
                {
                    Score = new ConfidenceScore(partialConfidenceScore, "Análise parcial válida"),
                    ClassificationReliable = partialConfidenceScore >= 0.5,
                    OriginalScore = analysisResult.GeneralScore,
                    AdjustedScore = analysisResult.GeneralScore,
                    PenaltyApplied = 0,
                    OriginalClassification = "Partial",
                    AdjustedClassification = "Partial",
                    ClassificationAdjustmentReason = "Análise parcial - aguardando capturas adicionais"
                },
                OverallConfidence = new ConfidenceScore(partialConfidenceScore, captureTypeDescription),
                QualityGatePassed = partialConfidenceScore >= 0.40, // Threshold mais baixo para análise parcial
                QualitySummary = captureTypeDescription
            };

            var classificationAdjustment = new ClassificationAdjustmentResult
            {
                OriginalClassification = "Partial",
                AdjustedClassification = "Partial",
                WasAdjusted = false,
                AdjustmentReason = "Análise parcial válida"
            };

            var scoreAdjustment = new ScoreAdjustmentResult
            {
                OriginalGeneralScore = analysisResult.GeneralScore,
                AdjustedGeneralScore = analysisResult.GeneralScore,
                OriginalPersonalizedScore = analysisResult.PersonalizedScore,
                AdjustedPersonalizedScore = analysisResult.PersonalizedScore,
                PenaltyApplied = 0
            };

            var confidenceDto = ConfidenceDetailsDto.FromMultiDimensional(
                confidence, classificationAdjustment, scoreAdjustment);

            return new MultidimensionalQualityGateResult
            {
                Passed = confidence.QualityGatePassed,
                Confidence = confidence,
                ConfidenceDto = confidenceDto,
                ClassificationAdjustment = classificationAdjustment,
                ScoreAdjustment = scoreAdjustment,
                AdjustedClassification = "Partial",
                AdjustedGeneralScore = analysisResult.GeneralScore,
                AdjustedPersonalizedScore = analysisResult.PersonalizedScore,
                AdjustedSummary = analysisResult.Summary,
                AdjustedShortSummary = analysisResult.ShortSummary ?? "",
                LegacyConfidenceLevel = MapToLegacyConfidenceLevel(confidenceLevel),
                QualityMessage = confidence.QualitySummary
            };
        }

        private string GenerateCoherentSummary(
            ProductAnalysisResultDto analysisResult,
            MultiDimensionalConfidence confidence,
            ClassificationAdjustmentResult classificationAdjustment)
        {
            var overall = confidence.OverallConfidence.Level;
            var originalSummary = analysisResult.Summary ?? string.Empty;

            // Se confiança muito baixa, mensagem especial
            if (overall == ConfidenceLevel.VeryLow)
            {
                return "**Leitura Incompleta** - Não foi possível analisar o rótulo adequadamente. " +
                       "Tire uma nova foto mais próxima, com boa iluminação e sem reflexo.";
            }

            // Se confiança baixa
            if (overall == ConfidenceLevel.Low)
            {
                var reasons = string.Join(", ", confidence.FinalAnalysis.ConfidenceAlerts);
                return $"**Análise Parcial** - {reasons}. " +
                       "Recomenda-se tirar nova foto do rótulo para análise completa.";
            }

            // Se classificação foi ajustada
            if (classificationAdjustment.WasAdjusted)
            {
                var adjustedSummary = originalSummary
                    .Replace("Excelente Escolha", "Opção a Verificar")
                    .Replace("Boa Escolha", "Opção Moderada")
                    .Replace("adequado para consumo regular", "verificar antes de consumir regularmente")
                    .Replace("Pode consumir regularmente", "Consumir com atenção");

                return $"{adjustedSummary} • ⚠️ {classificationAdjustment.AdjustmentReason}";
            }

            // Se confiança média
            if (overall == ConfidenceLevel.Medium)
            {
                return $"{originalSummary} • ℹ️ Análise baseada em leitura parcial do rótulo.";
            }

            // Confiança alta - manter original
            return originalSummary;
        }

        private string GenerateCoherentShortSummary(
            MultiDimensionalConfidence confidence,
            ClassificationAdjustmentResult classificationAdjustment,
            double adjustedScore)
        {
            var scoreDisplay = (int)Math.Round(adjustedScore * 100);
            var overall = confidence.OverallConfidence.Level;

            // Produto não identificado
            if (!confidence.ProductIdentification.ProductNameIdentified)
            {
                return $"Produto não identificado ({scoreDisplay}/100). Tire outra foto do rótulo.";
            }

            // Confiança muito baixa
            if (overall == ConfidenceLevel.VeryLow)
            {
                return $"Leitura com problemas ({scoreDisplay}/100). Tire nova foto com melhor qualidade.";
            }

            // Confiança baixa
            if (overall == ConfidenceLevel.Low)
            {
                return $"Análise limitada ({scoreDisplay}/100). Recomenda-se nova foto.";
            }

            // Classificação ajustada
            if (classificationAdjustment.WasAdjusted)
            {
                return classificationAdjustment.AdjustedClassification switch
                {
                    "Incomplete" => $"Análise incompleta ({scoreDisplay}/100). {classificationAdjustment.AdjustmentReason}.",
                    "Caution" => $"Consumir com atenção ({scoreDisplay}/100). {classificationAdjustment.AdjustmentReason}.",
                    "Moderate" => $"Opção moderada ({scoreDisplay}/100). Verificar ingredientes.",
                    _ => $"Produto analisado ({scoreDisplay}/100)."
                };
            }

            // Confiança média
            if (overall == ConfidenceLevel.Medium)
            {
                return classificationAdjustment.AdjustedClassification switch
                {
                    "Safe" => $"Opção aceitável ({scoreDisplay}/100). Análise parcial.",
                    "Caution" => $"Consumir com atenção ({scoreDisplay}/100).",
                    _ => $"Produto analisado ({scoreDisplay}/100). Leitura parcial."
                };
            }

            // Confiança alta
            return classificationAdjustment.AdjustedClassification switch
            {
                "Safe" or "Excellent" => $"✅ Boa escolha ({scoreDisplay}/100). Pode consumir com tranquilidade.",
                "Moderate" => $"Opção moderada ({scoreDisplay}/100). Consumir com moderação.",
                "Caution" => $"⚠️ Atenção necessária ({scoreDisplay}/100). Verifique ingredientes.",
                "Unsafe" or "Avoid" => $"❌ Não recomendado ({scoreDisplay}/100).",
                _ => $"Produto analisado ({scoreDisplay}/100)."
            };
        }

        private string MapToLegacyConfidenceLevel(ConfidenceLevel level)
        {
            return level switch
            {
                ConfidenceLevel.High => "Alto",
                ConfidenceLevel.Medium => "Médio",
                ConfidenceLevel.Low => "Baixo",
                ConfidenceLevel.VeryLow => "Muito Baixo",
                _ => "Desconhecido"
            };
        }
    }

    /// <summary>
    /// Resultado do Quality Gate multidimensional
    /// </summary>
    public class MultidimensionalQualityGateResult
    {
        public bool Passed { get; set; }

        // Confiança multidimensional completa
        public MultiDimensionalConfidence Confidence { get; set; } = new();
        public ConfidenceDetailsDto ConfidenceDto { get; set; } = new();

        // Ajustes aplicados
        public ClassificationAdjustmentResult ClassificationAdjustment { get; set; } = new();
        public ScoreAdjustmentResult ScoreAdjustment { get; set; } = new();

        // Valores finais ajustados
        public string AdjustedClassification { get; set; } = string.Empty;
        public double AdjustedGeneralScore { get; set; }
        public double AdjustedPersonalizedScore { get; set; }
        public string AdjustedSummary { get; set; } = string.Empty;
        public string AdjustedShortSummary { get; set; } = string.Empty;

        // Compatibilidade legado
        public string LegacyConfidenceLevel { get; set; } = string.Empty;

        // Mensagem de qualidade
        public string QualityMessage { get; set; } = string.Empty;
    }
}
