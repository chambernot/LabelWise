using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using LabelWise.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelWise.Application.Orchestration.Strategies
{
    /// <summary>
    /// Estratégia para processar capturas da lista de ingredientes.
    /// 
    /// COMPORTAMENTO:
    /// - Foca em OCR e parsing de ingredientes
    /// - Identifica lista ordenada por quantidade
    /// - Detecta aditivos e conservantes
    /// - Classifica processamento (ultra-processado, etc.)
    /// 
    /// CARACTERÍSTICAS:
    /// - Usa parser especializado de ingredientes
    /// - Reconhece padrões de formatação (vírgulas, parênteses)
    /// - Identifica números INS (International Numbering System)
    /// - Detecta termos suspeitos
    /// </summary>
    public class IngredientsListStrategy : CaptureTypeStrategy
    {
        public override CaptureType CaptureType => CaptureType.IngredientsList;
        public override string StrategyName => "Ingredients List Parsing Strategy";
        public override bool RequiresOcr => true;
        public override bool CanIdentifyProduct => false;
        public override bool HasIngredients => true;

        public override async Task<CaptureProcessingResult> ProcessAsync(
            byte[] imageData,
            string? fileName,
            CaptureProcessingContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new CaptureProcessingResult
            {
                CaptureType = CaptureType.IngredientsList,
                Metadata =
                {
                    ["Strategy"] = StrategyName,
                    ["ProcessingType"] = "Ingredients OCR + Parsing"
                }
            };

            context.Logger?.LogInformation("╔═══════════════════════════════════════════════════════════╗");
            context.Logger?.LogInformation("║ 🥗 ESTRATÉGIA: {Strategy}", StrategyName);
            context.Logger?.LogInformation("║ Tipo: {Type}", CaptureType);
            context.Logger?.LogInformation("╚═══════════════════════════════════════════════════════════╝");

            string? tempImagePath = null;
            try
            {
                // Detectar contentType baseado no fileName
                var contentType = DetectContentType(fileName);

                // ETAPA 1: Salvar imagem temporariamente
                tempImagePath = Path.Combine(Path.GetTempPath(), $"ocr_{Guid.NewGuid()}.jpg");
                await File.WriteAllBytesAsync(tempImagePath, imageData);

                // ETAPA 2: OCR da lista de ingredientes
                context.Logger?.LogInformation("🔍 ETAPA 1: OCR da lista de ingredientes...");

                var ocrRequest = new Application.DTOs.OcrRequestDto
                {
                    ImagePath = tempImagePath,
                    FileName = fileName ?? "ingredients.jpg",
                    ContentType = contentType
                };

                var ocrResult = await context.OcrProvider.ExtractTextAsync(ocrRequest);

                if (!ocrResult.Success)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Falha no OCR dos ingredientes: {ocrResult.ErrorMessage}";
                    result.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;
                    context.Logger?.LogWarning("❌ Falha no OCR: {Error}", ocrResult.ErrorMessage);
                    return result;
                }

                context.Logger?.LogInformation("✅ OCR concluído - Confiança: {Confidence:P2}", ocrResult.Confidence);
                context.Logger?.LogInformation("   Caracteres extraídos: {Chars}", ocrResult.RawText.Length);

                result.RawText = ocrResult.RawText;
                result.Confidence = ocrResult.Confidence;

                // ETAPA 2: Parsing da lista de ingredientes
                context.Logger?.LogInformation("🔍 ETAPA 2: Parsing de ingredientes...");

                var parseResult = context.Parser.Parse(ocrResult.RawText);

                if (!parseResult.HasIngredients || parseResult.Ingredients.Count == 0)
                {
                    result.Success = false;
                    result.ErrorMessage = "Não foi possível identificar ingredientes no texto";
                    result.Warnings.Add("Texto detectado, mas nenhum ingrediente válido encontrado");
                    result.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;
                    context.Logger?.LogWarning("⚠️ Nenhum ingrediente identificado");
                    return result;
                }

                context.Logger?.LogInformation("✅ Ingredientes extraídos: {Count}", parseResult.Ingredients.Count);

                foreach (var ingredient in parseResult.Ingredients.Take(5))
                {
                    context.Logger?.LogInformation("   • {Name}", ingredient);
                }

                if (parseResult.Ingredients.Count > 5)
                {
                    context.Logger?.LogInformation("   ... e mais {Count} ingredientes", parseResult.Ingredients.Count - 5);
                }

                // ETAPA 3: Análise de qualidade dos ingredientes
                result.Success = true;
                result.Ingredients = parseResult.Ingredients.ToList();
                result.ExtractedData["Ingredients"] = parseResult.Ingredients;
                result.ExtractedData["IngredientCount"] = parseResult.Ingredients.Count;
                result.ExtractedData["HasAdditives"] = HasAdditives(parseResult.Ingredients);
                result.ExtractedData["AdditiveCount"] = CountAdditives(parseResult.Ingredients);

                result.Metadata["OcrProvider"] = ocrResult.ProviderMetadata?.GetValueOrDefault("SelectedProvider", "Unknown") ?? "Unknown";
                result.Metadata["IngredientCount"] = parseResult.Ingredients.Count.ToString();
                result.Metadata["ParserConfidence"] = parseResult.ParsingConfidence.ToString();

                // Classificação de processamento
                var processingLevel = DetermineProcessingLevel(parseResult.Ingredients);
                result.ExtractedData["ProcessingLevel"] = processingLevel;
                result.Metadata["ProcessingLevel"] = processingLevel;

                result.QualityLevel = AssessQuality(ocrResult.Confidence, parseResult);

                if (result.QualityLevel == "LOW")
                {
                    result.Warnings.Add("Qualidade da extração de ingredientes está abaixo do ideal");
                    result.Warnings.Add("Alguns ingredientes podem não ter sido detectados corretamente");
                }

                result.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;

                context.Logger?.LogInformation("╔═══════════════════════════════════════════════════════════╗");
                context.Logger?.LogInformation("║ ✅ INGREDIENTS LIST STRATEGY - SUCESSO");
                context.Logger?.LogInformation("║ Ingredientes: {Count}", parseResult.Ingredients.Count);
                context.Logger?.LogInformation("║ Aditivos: {Additives}", result.ExtractedData["AdditiveCount"]);
                context.Logger?.LogInformation("║ Processamento: {Level}", processingLevel);
                context.Logger?.LogInformation("║ Confiança: {Confidence:P2}", result.Confidence);
                context.Logger?.LogInformation("║ Qualidade: {Quality}", result.QualityLevel);
                context.Logger?.LogInformation("║ Tempo: {Time:F2}s", result.ProcessingTimeSeconds);
                context.Logger?.LogInformation("╚═══════════════════════════════════════════════════════════╝");

                return result;
            }
            catch (Exception ex)
            {
                context.Logger?.LogError(ex, "❌ Erro fatal na estratégia de ingredientes");
                result.Success = false;
                result.ErrorMessage = $"Erro ao processar lista de ingredientes: {ex.Message}";
                result.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;
                return result;
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempImagePath) && File.Exists(tempImagePath))
                {
                    try { File.Delete(tempImagePath); }
                    catch { }
                }
            }
        }

        private string DetermineProcessingLevel(System.Collections.Generic.List<string> ingredients)
        {
            // Simplificado - detectar aditivos por padrões comuns
            var additivePatterns = new[] { "E", "INS", "corante", "conservante", "estabilizante" };
            var additiveCount = ingredients.Count(i => 
                additivePatterns.Any(p => i.Contains(p, StringComparison.OrdinalIgnoreCase)));

            var totalCount = ingredients.Count;

            if (additiveCount == 0)
                return "MINIMALLY_PROCESSED";

            var additiveRatio = (double)additiveCount / totalCount;

            if (additiveRatio >= 0.3)
                return "ULTRA_PROCESSED";
            if (additiveRatio >= 0.15)
                return "HIGHLY_PROCESSED";
            return "PROCESSED";
        }

        private string AssessQuality(double ocrConfidence, Parsing.IngredientAllergenParseResult parseResult)
        {
            var ingredientCount = parseResult.Ingredients.Count;

            // Converter ConfidenceLevel para double
            var parseConfidence = parseResult.ParsingConfidence switch
            {
                LabelWise.Domain.Enums.ConfidenceLevel.High => 0.9,
                LabelWise.Domain.Enums.ConfidenceLevel.Medium => 0.7,
                LabelWise.Domain.Enums.ConfidenceLevel.Low => 0.4,
                _ => 0.5
            };

            // Alta qualidade: boa confiança do OCR + parsing bem-sucedido + múltiplos ingredientes
            if (ocrConfidence >= 0.90 && parseConfidence >= 0.85 && ingredientCount >= 3)
                return "HIGH";

            // Média qualidade: confiança razoável + pelo menos 2 ingredientes
            if (ocrConfidence >= 0.75 && parseConfidence >= 0.70 && ingredientCount >= 2)
                return "MEDIUM";

            return "LOW";
        }

        private bool HasAdditives(System.Collections.Generic.List<string> ingredients)
        {
            var additivePatterns = new[] { "E", "INS", "corante", "conservante", "estabilizante" };
            return ingredients.Any(i => additivePatterns.Any(p => i.Contains(p, StringComparison.OrdinalIgnoreCase)));
        }

        private int CountAdditives(System.Collections.Generic.List<string> ingredients)
        {
            var additivePatterns = new[] { "E", "INS", "corante", "conservante", "estabilizante" };
            return ingredients.Count(i => additivePatterns.Any(p => i.Contains(p, StringComparison.OrdinalIgnoreCase)));
        }

        private string DetectContentType(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "image/jpeg";

            var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => "image/jpeg"
            };
        }
    }
}
