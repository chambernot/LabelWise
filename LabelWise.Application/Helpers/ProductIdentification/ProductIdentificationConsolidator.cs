using LabelWise.Application.DTOs.AI;
using LabelWise.Application.DTOs.ProductIdentification;
using LabelWise.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelWise.Application.Helpers.ProductIdentification
{
    /// <summary>
    /// Consolida resultados de múltiplas fontes (OCR, OpenAI Vision, etc.)
    /// para produzir a melhor identificação de produto possível.
    /// </summary>
    public static class ProductIdentificationConsolidator
    {
        // Palavras-chave comuns que não são nome de produto
        private static readonly HashSet<string> NoisyKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "INFORMAÇÃO NUTRICIONAL", "INFORMACAO NUTRICIONAL", "NUTRITION FACTS", "INGREDIENTES",
            "INGREDIENTS", "TABELA NUTRICIONAL", "NUTRITION TABLE", "DECLARAÇÃO NUTRICIONAL",
            "NUTRITIONAL DECLARATION", "ALÉRGICOS", "ALLERGENS", "CONTÉM", "CONTAINS",
            "PODE CONTER", "MAY CONTAIN", "VALORES NUTRICIONAIS", "NUTRITIONAL VALUES",
            "PORÇÃO", "SERVING", "SERVING SIZE", "CALORIAS", "CALORIES", "KCAL",
            "CARBOIDRATOS", "CARBOHYDRATES", "PROTEÍNAS", "PROTEINS", "GORDURAS", "FATS"
        };

        /// <summary>
        /// Consolida resultado OCR + OpenAI Vision para identificação de produto.
        /// </summary>
        public static ProductIdentificationResult ConsolidateOcrAndVision(
            Application.DTOs.OcrResultDto ocrResult,
            VisualInterpretationResult visionResult,
            double ocrMatchConfidence,
            ILogger logger)
        {
            logger.LogInformation("🔀 Consolidando OCR + OpenAI Vision");

            // Extrair dados de ambas as fontes
            var ocrName = ExtractCleanProductName(ocrResult.RawText);
            var ocrBrand = ExtractCleanBrand(ocrResult.RawText);

            var visionName = CleanProductName(visionResult.ProbableProductName);
            var visionBrand = CleanBrand(visionResult.ProbableBrand);

            logger.LogInformation("   OCR: Name={OcrName}, Brand={OcrBrand}", ocrName ?? "N/A", ocrBrand ?? "N/A");
            logger.LogInformation("   Vision: Name={VisionName}, Brand={VisionBrand}", visionName ?? "N/A", visionBrand ?? "N/A");

            // Consolidar nome (preferir Vision se OCR for ruído)
            string? finalName = ChooseBestProductName(ocrName, visionName, logger);
            
            // Consolidar marca (preferir Vision se OCR for ruído)
            string? finalBrand = ChooseBestBrand(ocrBrand, visionBrand, logger);

            // Calcular confiança consolidada
            double consolidatedConfidence = CalculateConsolidatedConfidence(
                ocrResult.Confidence,
                visionResult.InterpretationConfidence,
                finalName != null,
                finalBrand != null);

            // Determinar fonte do match
            MatchSource matchSource = DetermineMatchSource(ocrName, visionName, ocrBrand, visionBrand);

            logger.LogInformation("   ✅ Consolidated: Name={Name}, Brand={Brand}, Confidence={Confidence:P2}, Source={Source}",
                finalName ?? "N/A", finalBrand ?? "N/A", consolidatedConfidence, matchSource);

            return new ProductIdentificationResult
            {
                Success = finalName != null,
                Method = IdentificationMethod.Composite,
                MatchSource = matchSource,
                Confidence = consolidatedConfidence,
                MatchConfidence = consolidatedConfidence,
                MatchedProductName = finalName,
                MatchedBrand = finalBrand,
                ProductName = finalName,
                Brand = finalBrand,
                IsFromExternalDatabase = false,
                IsReliableMatch = consolidatedConfidence >= 0.70,
                Metadata = new Dictionary<string, string>
                {
                    ["OcrConfidence"] = ocrResult.Confidence.ToString("F4"),
                    ["VisionConfidence"] = visionResult.InterpretationConfidence.ToString(),
                    ["ConsolidatedConfidence"] = consolidatedConfidence.ToString("F4"),
                    ["MatchSource"] = matchSource.ToString(),
                    ["OcrName"] = ocrName ?? "N/A",
                    ["VisionName"] = visionName ?? "N/A",
                    ["OcrBrand"] = ocrBrand ?? "N/A",
                    ["VisionBrand"] = visionBrand ?? "N/A"
                },
                Details = new List<string>
                {
                    $"Nome final: {finalName ?? "Não identificado"}",
                    $"Marca final: {finalBrand ?? "Não identificada"}",
                    $"Confiança consolidada: {consolidatedConfidence:P2}",
                    $"Fonte: {matchSource}",
                    $"OCR Confidence: {ocrResult.Confidence:P2}",
                    $"Vision Confidence: {visionResult.InterpretationConfidence}"
                }
            };
        }

        /// <summary>
        /// Escolhe o melhor nome de produto entre OCR e Vision.
        /// </summary>
        private static string? ChooseBestProductName(string? ocrName, string? visionName, ILogger logger)
        {
            // Se ambos ausentes
            if (ocrName == null && visionName == null)
            {
                logger.LogWarning("   ⚠️ Nenhum nome de produto identificado");
                return null;
            }

            // Se apenas um disponível
            if (ocrName == null) return visionName;
            if (visionName == null) return ocrName;

            // Se OCR contém ruído, preferir Vision
            if (IsNoisyText(ocrName))
            {
                logger.LogInformation("   🔄 OCR contém ruído, usando Vision: {VisionName}", visionName);
                return visionName;
            }

            // Se ambos presentes e válidos, usar o mais longo (mais informativo)
            if (visionName.Length > ocrName.Length)
            {
                logger.LogInformation("   🔄 Vision name mais informativo: {VisionName}", visionName);
                return visionName;
            }

            logger.LogInformation("   ✅ Usando OCR name: {OcrName}", ocrName);
            return ocrName;
        }

        /// <summary>
        /// Escolhe a melhor marca entre OCR e Vision.
        /// </summary>
        private static string? ChooseBestBrand(string? ocrBrand, string? visionBrand, ILogger logger)
        {
            // Se ambos ausentes
            if (ocrBrand == null && visionBrand == null)
            {
                logger.LogWarning("   ⚠️ Nenhuma marca identificada");
                return null;
            }

            // Se apenas um disponível
            if (ocrBrand == null) return visionBrand;
            if (visionBrand == null) return ocrBrand;

            // Se OCR é ruído, preferir Vision
            if (IsNoisyText(ocrBrand))
            {
                logger.LogInformation("   🔄 OCR brand é ruído, usando Vision: {VisionBrand}", visionBrand);
                return visionBrand;
            }

            // Se ambos presentes, preferir o mais curto (marcas são geralmente curtas)
            if (visionBrand.Length < ocrBrand.Length && visionBrand.Length >= 2)
            {
                logger.LogInformation("   🔄 Vision brand mais conciso: {VisionBrand}", visionBrand);
                return visionBrand;
            }

            logger.LogInformation("   ✅ Usando OCR brand: {OcrBrand}", ocrBrand);
            return ocrBrand;
        }

        /// <summary>
        /// Determina a fonte do match baseado em qual fonte contribuiu.
        /// </summary>
        private static MatchSource DetermineMatchSource(
            string? ocrName, 
            string? visionName, 
            string? ocrBrand, 
            string? visionBrand)
        {
            bool hasOcrData = ocrName != null || ocrBrand != null;
            bool hasVisionData = visionName != null || visionBrand != null;

            if (hasOcrData && hasVisionData)
                return MatchSource.OcrPlusOpenAiVision;

            if (hasVisionData)
                return MatchSource.OpenAiVision;

            if (hasOcrData)
                return MatchSource.FrontOcr;

            return MatchSource.Unknown;
        }

        /// <summary>
        /// Calcula confiança consolidada baseada em OCR + Vision.
        /// </summary>
        private static double CalculateConsolidatedConfidence(
            double ocrConfidence,
            ConfidenceLevel visionConfidence,
            bool hasName,
            bool hasBrand)
        {
            // Mapear ConfidenceLevel para double
            double visionConfidenceValue = visionConfidence switch
            {
                ConfidenceLevel.High => 0.85,
                ConfidenceLevel.Medium => 0.65,
                ConfidenceLevel.Low => 0.40,
                _ => 0.20
            };

            // Média ponderada (Vision tem peso maior por ser mais avançado)
            double baseConfidence = (ocrConfidence * 0.35) + (visionConfidenceValue * 0.65);

            // Bonus se tiver ambos nome e marca
            if (hasName && hasBrand)
                baseConfidence = Math.Min(1.0, baseConfidence * 1.10);

            // Penalty se não tiver nome
            if (!hasName)
                baseConfidence *= 0.50;

            return Math.Round(baseConfidence, 4);
        }

        /// <summary>
        /// Verifica se o texto é ruído (cabeçalhos, etc.).
        /// </summary>
        private static bool IsNoisyText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return true;

            return NoisyKeywords.Any(keyword => 
                text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Extrai e limpa nome do produto de texto OCR.
        /// </summary>
        private static string? ExtractCleanProductName(string ocrText)
        {
            if (string.IsNullOrWhiteSpace(ocrText))
                return null;

            var lines = ocrText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length >= 3 && !IsNoisyText(l))
                .Take(5)
                .ToList();

            return lines.FirstOrDefault();
        }

        /// <summary>
        /// Extrai e limpa marca de texto OCR.
        /// </summary>
        private static string? ExtractCleanBrand(string ocrText)
        {
            if (string.IsNullOrWhiteSpace(ocrText))
                return null;

            var lines = ocrText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length >= 2 && !IsNoisyText(l))
                .Skip(1)
                .Take(3)
                .ToList();

            return lines.FirstOrDefault();
        }

        /// <summary>
        /// Limpa nome do produto removendo ruídos.
        /// </summary>
        public static string? CleanProductName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            name = name.Trim();

            // Remover se for ruído
            if (IsNoisyText(name))
                return null;

            // Remover caracteres especiais excessivos
            name = System.Text.RegularExpressions.Regex.Replace(name, @"[^\w\s\-\.áàãâéêíóôõúçÁÀÃÂÉÊÍÓÔÕÚÇ]", " ");
            name = System.Text.RegularExpressions.Regex.Replace(name, @"\s+", " ");

            return string.IsNullOrWhiteSpace(name) || name.Length < 3 ? null : name.Trim();
        }

        /// <summary>
        /// Limpa marca removendo ruídos.
        /// </summary>
        public static string? CleanBrand(string? brand)
        {
            if (string.IsNullOrWhiteSpace(brand))
                return null;

            brand = brand.Trim();

            // Remover se for ruído
            if (IsNoisyText(brand))
                return null;

            // Remover caracteres especiais
            brand = System.Text.RegularExpressions.Regex.Replace(brand, @"[^\w\s\-\.áàãâéêíóôõúçÁÀÃÂÉÊÍÓÔÕÚÇ]", " ");
            brand = System.Text.RegularExpressions.Regex.Replace(brand, @"\s+", " ");

            return string.IsNullOrWhiteSpace(brand) || brand.Length < 2 ? null : brand.Trim();
        }
    }
}
