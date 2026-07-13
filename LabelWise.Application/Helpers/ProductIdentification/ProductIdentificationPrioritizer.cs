using LabelWise.Application.DTOs.ProductIdentification;
using LabelWise.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelWise.Application.Helpers.ProductIdentification
{
    /// <summary>
    /// Define regras de priorização entre diferentes fontes de identificação.
    /// </summary>
    public static class ProductIdentificationPrioritizer
    {
        /// <summary>
        /// Avalia se OCR frontal é suficientemente confiável para não precisar de fallback.
        /// </summary>
        public static bool IsOcrResultSufficient(
            Application.DTOs.OcrResultDto ocrResult,
            string? extractedName,
            string? extractedBrand,
            ILogger logger)
        {
            logger.LogInformation("🔍 Avaliando suficiência do resultado OCR");

            // Critério 1: Confiança OCR >= 0.75
            bool hasGoodOcrConfidence = ocrResult.Confidence >= 0.75;
            logger.LogInformation("   OCR Confidence: {Confidence:P2} - {Status}",
                ocrResult.Confidence,
                hasGoodOcrConfidence ? "✅ Suficiente" : "⚠️ Baixa");

            // Critério 2: Nome válido extraído
            bool hasValidName = !string.IsNullOrWhiteSpace(extractedName) && extractedName.Length >= 3;
            logger.LogInformation("   Nome extraído: {Name} - {Status}",
                extractedName ?? "N/A",
                hasValidName ? "✅ Válido" : "❌ Inválido");

            // Critério 3: Marca extraída (opcional mas desejável)
            bool hasValidBrand = !string.IsNullOrWhiteSpace(extractedBrand) && extractedBrand.Length >= 2;
            logger.LogInformation("   Marca extraída: {Brand} - {Status}",
                extractedBrand ?? "N/A",
                hasValidBrand ? "✅ Válida" : "⚠️ Ausente");

            // Decisão: OCR é suficiente se tem confiança alta E nome válido
            bool isSufficient = hasGoodOcrConfidence && hasValidName;

            logger.LogInformation("   📊 Resultado: OCR é {Status}",
                isSufficient ? "✅ SUFICIENTE (não precisa fallback)" : "⚠️ INSUFICIENTE (usar Vision fallback)");

            return isSufficient;
        }

        /// <summary>
        /// Determina se deve usar OpenAI Vision como fallback.
        /// </summary>
        public static bool ShouldUseVisionFallback(
            ProductIdentificationRequest request,
            Application.DTOs.OcrResultDto? ocrResult,
            string? extractedName,
            ILogger logger)
        {
            logger.LogInformation("🤔 Avaliando necessidade de Vision fallback");

            // Regra 1: Se não é FrontPackaging, não usar Vision
            if (request.CaptureType != CaptureType.FrontPackaging)
            {
                logger.LogInformation("   ❌ CaptureType={Type} - Vision fallback não aplicável", request.CaptureType);
                return false;
            }

            // Regra 2: Se OCR falhou completamente
            if (ocrResult == null || !ocrResult.Success)
            {
                logger.LogInformation("   ✅ OCR falhou - Vision fallback necessário");
                return true;
            }

            // Regra 3: Se OCR foi bem-sucedido mas não suficiente
            bool isSufficient = IsOcrResultSufficient(ocrResult, extractedName, null, logger);
            
            if (!isSufficient)
            {
                logger.LogInformation("   ✅ OCR insuficiente - Vision fallback necessário");
                return true;
            }

            logger.LogInformation("   ❌ OCR suficiente - Vision fallback não necessário");
            return false;
        }

        /// <summary>
        /// Calcula a prioridade de uma fonte de identificação (maior = melhor).
        /// </summary>
        public static int GetSourcePriority(MatchSource source)
        {
            return source switch
            {
                MatchSource.Barcode => 100,                      // Máxima prioridade
                MatchSource.OcrPlusOpenAiVision => 90,           // Muito alta
                MatchSource.OpenAiVision => 80,                  // Alta
                MatchSource.Combined => 70,                      // Boa
                MatchSource.FrontOcr => 60,                      // Média
                MatchSource.Similarity => 40,                    // Baixa
                MatchSource.Unknown => 0,                        // Sem prioridade
                _ => 0
            };
        }

        /// <summary>
        /// Compara dois resultados e retorna o de maior prioridade.
        /// </summary>
        public static ProductIdentificationResult ChooseBestResult(
            ProductIdentificationResult result1,
            ProductIdentificationResult result2,
            ILogger logger)
        {
            logger.LogInformation("⚖️ Comparando resultados para escolher o melhor");

            // Se apenas um é bem-sucedido
            if (result1.Success && !result2.Success)
            {
                logger.LogInformation("   ✅ Escolhendo resultado 1 (único bem-sucedido)");
                return result1;
            }
            if (result2.Success && !result1.Success)
            {
                logger.LogInformation("   ✅ Escolhendo resultado 2 (único bem-sucedido)");
                return result2;
            }

            // Se ambos falharam
            if (!result1.Success && !result2.Success)
            {
                logger.LogInformation("   ⚠️ Ambos falharam, escolhendo resultado 1");
                return result1;
            }

            // Ambos bem-sucedidos: comparar por prioridade de fonte
            int priority1 = GetSourcePriority(result1.MatchSource);
            int priority2 = GetSourcePriority(result2.MatchSource);

            logger.LogInformation("   Resultado 1: Source={Source}, Priority={Priority}, Confidence={Confidence:P2}",
                result1.MatchSource, priority1, result1.MatchConfidence);
            logger.LogInformation("   Resultado 2: Source={Source}, Priority={Priority}, Confidence={Confidence:P2}",
                result2.MatchSource, priority2, result2.MatchConfidence);

            if (priority1 > priority2)
            {
                logger.LogInformation("   ✅ Escolhendo resultado 1 (maior prioridade de fonte)");
                return result1;
            }
            if (priority2 > priority1)
            {
                logger.LogInformation("   ✅ Escolhendo resultado 2 (maior prioridade de fonte)");
                return result2;
            }

            // Mesma prioridade: comparar por confiança
            if (result1.MatchConfidence > result2.MatchConfidence)
            {
                logger.LogInformation("   ✅ Escolhendo resultado 1 (maior confiança)");
                return result1;
            }

            logger.LogInformation("   ✅ Escolhendo resultado 2 (maior ou igual confiança)");
            return result2;
        }

        /// <summary>
        /// Determina o threshold mínimo de confiança baseado na fonte.
        /// </summary>
        public static double GetMinimumConfidenceThreshold(MatchSource source)
        {
            return source switch
            {
                MatchSource.Barcode => 0.60,                      // Baixo (barcode é confiável por natureza)
                MatchSource.OcrPlusOpenAiVision => 0.70,         // Moderado
                MatchSource.OpenAiVision => 0.75,                // Moderado-alto
                MatchSource.Combined => 0.70,                    // Moderado
                MatchSource.FrontOcr => 0.80,                    // Alto (OCR sozinho precisa alta confiança)
                MatchSource.Similarity => 0.85,                  // Muito alto
                _ => 0.90                                        // Extremamente alto para desconhecidos
            };
        }

        /// <summary>
        /// Valida se um resultado atende o threshold mínimo de confiança.
        /// </summary>
        public static bool MeetsConfidenceThreshold(ProductIdentificationResult result, ILogger logger)
        {
            double threshold = GetMinimumConfidenceThreshold(result.MatchSource);
            bool meets = result.MatchConfidence >= threshold;

            logger.LogInformation("   Threshold check: {Confidence:P2} >= {Threshold:P2} = {Result}",
                result.MatchConfidence, threshold, meets ? "✅ PASS" : "❌ FAIL");

            return meets;
        }
    }
}
