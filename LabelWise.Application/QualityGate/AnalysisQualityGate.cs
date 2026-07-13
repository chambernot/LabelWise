using System;
using LabelWise.Application.DTOs;
using LabelWise.Application.Parsing;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.QualityGate
{
    /// <summary>
    /// Quality Gate que ajusta confiança, classificação, score e resumo baseado na qualidade do OCR e parsing.
    /// Garante coerência entre os resultados e evita respostas otimistas quando a análise está incompleta.
    /// </summary>
    public class AnalysisQualityGate
    {
        private readonly OcrQualityAssessor _ocrAssessor;
        private readonly ParsingQualityAssessor _parsingAssessor;

        public AnalysisQualityGate()
        {
            _ocrAssessor = new OcrQualityAssessor();
            _parsingAssessor = new ParsingQualityAssessor();
        }

        /// <summary>
        /// Aplica quality gate no resultado da análise, ajustando confiança, classificação, score e resumo.
        /// </summary>
        public QualityGateResult ApplyQualityGate(
            ProductAnalysisResultDto analysisResult,
            string extractedText,
            double ocrConfidence,
            IngredientAllergenParseResult parseResult)
        {
            // 1. Avaliar qualidade do OCR
            var ocrQuality = _ocrAssessor.AssessQuality(extractedText, ocrConfidence);

            // 2. Avaliar qualidade do parsing
            var parsingQuality = _parsingAssessor.AssessQuality(parseResult);

            // 3. Determinar confiança final baseada em ambos
            var finalConfidence = DetermineFinalConfidence(ocrQuality, parsingQuality);

            // 4. Ajustar classificação baseada na confiança
            var adjustedClassification = AdjustClassification(
                analysisResult.Classification ?? "Safe", 
                finalConfidence,
                parsingQuality);

            // 5. Aplicar penalização no score se necessário
            var (adjustedGeneralScore, adjustedPersonalizedScore) = AdjustScores(
                analysisResult.GeneralScore,
                analysisResult.PersonalizedScore,
                finalConfidence,
                ocrQuality,
                parsingQuality);

            // 6. Gerar resumo coerente
            var adjustedSummary = GenerateCoherentSummary(
                analysisResult,
                adjustedClassification,
                finalConfidence,
                ocrQuality,
                parsingQuality);

            // 7. Gerar short summary coerente
            var adjustedShortSummary = GenerateCoherentShortSummary(
                adjustedClassification,
                finalConfidence,
                adjustedPersonalizedScore,
                parsingQuality);

            // 8. Criar resultado do quality gate
            return new QualityGateResult
            {
                Passed = finalConfidence != "Baixo" || parsingQuality.OverallCompleteness >= ParsingCompletenessLevel.Partial,
                
                OriginalConfidence = analysisResult.ConfidenceLevel ?? "Médio",
                AdjustedConfidence = finalConfidence,
                
                OriginalClassification = analysisResult.Classification ?? "Safe",
                AdjustedClassification = adjustedClassification,
                
                OriginalGeneralScore = analysisResult.GeneralScore,
                AdjustedGeneralScore = adjustedGeneralScore,
                
                OriginalPersonalizedScore = analysisResult.PersonalizedScore,
                AdjustedPersonalizedScore = adjustedPersonalizedScore,
                
                OriginalSummary = analysisResult.Summary ?? string.Empty,
                AdjustedSummary = adjustedSummary,
                
                OriginalShortSummary = analysisResult.ShortSummary ?? string.Empty,
                AdjustedShortSummary = adjustedShortSummary,
                
                OcrQuality = ocrQuality,
                ParsingQuality = parsingQuality,
                
                QualityMessage = GenerateQualityMessage(ocrQuality, parsingQuality)
            };
        }

        private string DetermineFinalConfidence(OcrQualityMetrics ocrQuality, ParsingQualityMetrics parsingQuality)
        {
            // Confiança final é o MENOR entre OCR e parsing (mais conservador)
            var ocrConfidence = ocrQuality.RecommendedConfidenceLevel;
            var parsingConfidence = parsingQuality.RecommendedConfidenceLevel;

            // Ordem: Alto > Médio > Baixo
            if (ocrConfidence == "Baixo" || parsingConfidence == "Baixo")
                return "Baixo";

            if (ocrConfidence == "Médio" || parsingConfidence == "Médio")
                return "Médio";

            return "Alto";
        }

        private string AdjustClassification(
            string originalClassification, 
            string finalConfidence,
            ParsingQualityMetrics parsingQuality)
        {
            // REGRA 1: Se confiança não for Alta, classificação não pode ser Safe
            if (finalConfidence != "Alto" && originalClassification == "Safe")
            {
                return "Caution";
            }

            // REGRA 2: Se produto não identificado (nome "Produto Desconhecido"), usar Incomplete ou Caution
            if (!parsingQuality.HasProductName)
            {
                return "Incomplete";
            }

            // REGRA 3: Se parsing muito incompleto, não ser otimista
            if (parsingQuality.OverallCompleteness == ParsingCompletenessLevel.Incomplete)
            {
                return "Incomplete";
            }

            if (parsingQuality.OverallCompleteness == ParsingCompletenessLevel.Partial 
                && (originalClassification == "Safe" || originalClassification == "Excellent"))
            {
                return "Caution";
            }

            // REGRA 4: Se há alérgenos declarados mas parsing incompleto, ser conservador
            if (parsingQuality.HasAllergens && !parsingQuality.HasValidIngredients)
            {
                return "Caution";
            }

            // Se passou por todos os filtros, manter classificação original
            return originalClassification;
        }

        private (double adjustedGeneral, double adjustedPersonalized) AdjustScores(
            double generalScore,
            double personalizedScore,
            string finalConfidence,
            OcrQualityMetrics ocrQuality,
            ParsingQualityMetrics parsingQuality)
        {
            // Penalização baseada na confiança e qualidade
            double penalty = 0.0;

            // Penalização por confiança baixa
            if (finalConfidence == "Baixo")
            {
                penalty += 0.3; // -30%
            }
            else if (finalConfidence == "Médio")
            {
                penalty += 0.15; // -15%
            }

            // Penalização adicional por OCR de baixa qualidade
            if (ocrQuality.OverallQuality == OcrQualityLevel.VeryLow)
            {
                penalty += 0.2;
            }
            else if (ocrQuality.OverallQuality == OcrQualityLevel.Low)
            {
                penalty += 0.1;
            }

            // Penalização adicional por parsing incompleto
            if (parsingQuality.OverallCompleteness == ParsingCompletenessLevel.Incomplete)
            {
                penalty += 0.25;
            }
            else if (parsingQuality.OverallCompleteness == ParsingCompletenessLevel.Partial)
            {
                penalty += 0.1;
            }

            // Aplicar penalização (máximo 50% de redução)
            penalty = Math.Min(penalty, 0.5);

            var adjustedGeneral = Math.Max(0, generalScore * (1 - penalty));
            var adjustedPersonalized = Math.Max(0, personalizedScore * (1 - penalty));

            return (adjustedGeneral, adjustedPersonalized);
        }

        private string GenerateCoherentSummary(
            ProductAnalysisResultDto analysisResult,
            string adjustedClassification,
            string finalConfidence,
            OcrQualityMetrics ocrQuality,
            ParsingQualityMetrics parsingQuality)
        {
            // Se análise está muito incompleta, usar mensagem apropriada
            if (parsingQuality.OverallCompleteness == ParsingCompletenessLevel.Incomplete)
            {
                return $"**Análise Parcial** - {parsingQuality.RecommendedMessage} " +
                       $"{ocrQuality.RecommendedMessage}";
            }

            // Se OCR está ruim, avisar
            if (ocrQuality.OverallQuality <= OcrQualityLevel.Low)
            {
                return $"**Leitura Incompleta** - {ocrQuality.RecommendedMessage} " +
                       $"Análise baseada em informações parciais.";
            }

            // Se confiança é baixa mas parsing teve algum sucesso
            if (finalConfidence == "Baixo")
            {
                return $"**Análise com Ressalvas** - Algumas informações foram identificadas, mas a leitura não está completa. " +
                       $"{parsingQuality.RecommendedMessage}";
            }

            // Se confiança é média, ajustar o tom do summary original
            if (finalConfidence == "Médio")
            {
                var summary = analysisResult.Summary ?? string.Empty;
                
                // Remover termos muito otimistas
                summary = summary
                    .Replace("Excelente Escolha", "Escolha Razoável")
                    .Replace("Boa Escolha", "Opção Aceitável")
                    .Replace("adequado para consumo regular", "pode ser consumido com moderação")
                    .Replace("Pode consumir regularmente", "Pode consumir ocasionalmente");

                // Adicionar disclaimer sobre completude
                if (parsingQuality.OverallCompleteness == ParsingCompletenessLevel.Partial)
                {
                    summary += " • ⚠️ Análise baseada em leitura parcial do rótulo.";
                }

                return summary;
            }

            // Se confiança é alta e parsing está ok, usar summary original
            return analysisResult.Summary ?? "Análise concluída.";
        }

        private string GenerateCoherentShortSummary(
            string adjustedClassification,
            string finalConfidence,
            double adjustedPersonalizedScore,
            ParsingQualityMetrics parsingQuality)
        {
            var scoreDisplay = (int)Math.Round(adjustedPersonalizedScore * 100);

            // Se produto não foi identificado
            if (!parsingQuality.HasProductName)
            {
                return $"Produto não identificado ({scoreDisplay}/100). Tire outra foto do rótulo.";
            }

            // Se parsing está muito incompleto
            if (parsingQuality.OverallCompleteness == ParsingCompletenessLevel.Incomplete)
            {
                return $"Análise incompleta ({scoreDisplay}/100). Tire outra foto mais próxima do rótulo nutricional.";
            }

            // Se confiança é baixa
            if (finalConfidence == "Baixo")
            {
                return $"Leitura com dificuldades ({scoreDisplay}/100). Recomenda-se tirar nova foto.";
            }

            // Se confiança é média
            if (finalConfidence == "Médio")
            {
                return adjustedClassification switch
                {
                    "Safe" => $"Opção aceitável ({scoreDisplay}/100). Análise baseada em leitura parcial.",
                    "Caution" => $"Consumir com atenção ({scoreDisplay}/100). Informações parciais identificadas.",
                    "Incomplete" => $"Análise parcial ({scoreDisplay}/100). {parsingQuality.RecommendedMessage}",
                    _ => $"Produto analisado ({scoreDisplay}/100). Leitura parcial do rótulo."
                };
            }

            // Se confiança é alta, usar classificação normal
            return adjustedClassification switch
            {
                "Safe" => $"Boa escolha ({scoreDisplay}/100). Pode consumir com tranquilidade.",
                "Moderate" => $"Opção moderada ({scoreDisplay}/100). Consumir com moderação.",
                "Caution" => $"Atenção necessária ({scoreDisplay}/100). Verifique ingredientes.",
                "Unsafe" => $"Não recomendado ({scoreDisplay}/100). Contém ingredientes não adequados.",
                "Avoid" => $"Evitar este produto ({scoreDisplay}/100).",
                "Incomplete" => $"Análise parcial ({scoreDisplay}/100). {parsingQuality.RecommendedMessage}",
                _ => $"Produto analisado ({scoreDisplay}/100)."
            };
        }

        private string GenerateQualityMessage(OcrQualityMetrics ocrQuality, ParsingQualityMetrics parsingQuality)
        {
            var messages = new System.Collections.Generic.List<string>();

            // Mensagem sobre OCR
            messages.Add($"OCR: {ocrQuality.OverallQuality} ({ocrQuality.RecommendedMessage})");

            // Mensagem sobre Parsing
            messages.Add($"Parsing: {parsingQuality.OverallCompleteness} ({parsingQuality.RecommendedMessage})");

            return string.Join(" | ", messages);
        }
    }

    /// <summary>
    /// Resultado do Quality Gate
    /// </summary>
    public class QualityGateResult
    {
        public bool Passed { get; set; }
        
        public string OriginalConfidence { get; set; } = string.Empty;
        public string AdjustedConfidence { get; set; } = string.Empty;
        
        public string OriginalClassification { get; set; } = string.Empty;
        public string AdjustedClassification { get; set; } = string.Empty;
        
        public double OriginalGeneralScore { get; set; }
        public double AdjustedGeneralScore { get; set; }
        
        public double OriginalPersonalizedScore { get; set; }
        public double AdjustedPersonalizedScore { get; set; }
        
        public string OriginalSummary { get; set; } = string.Empty;
        public string AdjustedSummary { get; set; } = string.Empty;
        
        public string OriginalShortSummary { get; set; } = string.Empty;
        public string AdjustedShortSummary { get; set; } = string.Empty;
        
        public OcrQualityMetrics OcrQuality { get; set; } = new();
        public ParsingQualityMetrics ParsingQuality { get; set; } = new();
        
        public string QualityMessage { get; set; } = string.Empty;
    }
}
