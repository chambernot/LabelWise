using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using LabelWise.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelWise.Application.Orchestration.Strategies
{
    /// <summary>
    /// Estratégia para processar capturas de declaração de alergênicos.
    /// 
    /// COMPORTAMENTO:
    /// - Foca em OCR e identificação de alergênicos
    /// - Detecta frases como "Contém:", "Pode conter:", "Traços de:"
    /// - Identifica os 14 principais alérgenos
    /// - Alta sensibilidade (segurança alimentar)
    /// 
    /// CARACTERÍSTICAS:
    /// - Prioriza recall (detectar todos) sobre precision
    /// - Usa lista predefinida de alérgenos comuns
    /// - Reconhece variações de escrita
    /// - Diferencia presença confirmada vs traços
    /// </summary>
    public class AllergenStatementStrategy : CaptureTypeStrategy
    {
        public override CaptureType CaptureType => CaptureType.AllergenStatement;
        public override string StrategyName => "Allergen Statement Detection Strategy";
        public override bool RequiresOcr => true;
        public override bool CanIdentifyProduct => false;
        public override bool HasAllergens => true;

        private static readonly string[] KnownAllergens = new[]
        {
            "leite", "lactose", "milk", "dairy",
            "glúten", "gluten", "trigo", "wheat",
            "soja", "soy",
            "amendoim", "peanut",
            "nozes", "nuts", "castanhas",
            "ovo", "egg",
            "peixe", "fish",
            "crustáceos", "shellfish", "camarão",
            "mostarda", "mustard",
            "sésamo", "sesame",
            "sulfito", "sulfite",
            "centeio", "rye",
            "cevada", "barley",
            "aveia", "oat"
        };

        public override async Task<CaptureProcessingResult> ProcessAsync(
            byte[] imageData,
            string? fileName,
            CaptureProcessingContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new CaptureProcessingResult
            {
                CaptureType = CaptureType.AllergenStatement,
                Metadata =
                {
                    ["Strategy"] = StrategyName,
                    ["ProcessingType"] = "Allergen Detection OCR + Parsing"
                }
            };

            context.Logger?.LogInformation("╔═══════════════════════════════════════════════════════════╗");
            context.Logger?.LogInformation("║ ⚠️  ESTRATÉGIA: {Strategy}", StrategyName);
            context.Logger?.LogInformation("║ Tipo: {Type}", CaptureType);
            context.Logger?.LogInformation("╚═══════════════════════════════════════════════════════════╝");

            string? tempImagePath = null;
            try
            {
                // Detectar contentType baseado no fileName
                var contentType = DetectContentType(fileName);

                // ETAPA 1: Salvar imagem temporariamente
                tempImagePath = Path.Combine(Path.GetTempPath(), $"ocr_{Guid.NewGuid()}{GetImageExtension(contentType)}");
                await File.WriteAllBytesAsync(tempImagePath, imageData);

                // ETAPA 2: OCR da declaração de alérgenos
                context.Logger?.LogInformation("🔍 ETAPA 1: OCR da declaração de alérgenos...");

                var ocrRequest = new Application.DTOs.OcrRequestDto
                {
                    ImagePath = tempImagePath,
                    FileName = fileName ?? "allergens.jpg",
                    ContentType = contentType
                };

                var ocrResult = await context.OcrProvider.ExtractTextAsync(ocrRequest);

                if (!ocrResult.Success)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Falha no OCR da declaração de alérgenos: {ocrResult.ErrorMessage}";
                    result.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;
                    context.Logger?.LogWarning("❌ Falha no OCR: {Error}", ocrResult.ErrorMessage);
                    return result;
                }

                context.Logger?.LogInformation("✅ OCR concluído - Confiança: {Confidence:P2}", ocrResult.Confidence);
                context.Logger?.LogInformation("   Caracteres extraídos: {Chars}", ocrResult.RawText.Length);

                result.RawText = ocrResult.RawText;
                result.Confidence = ocrResult.Confidence;

                // ETAPA 3: Parsing de alérgenos via parser especializado
                context.Logger?.LogInformation("🔍 ETAPA 2: Parsing de alérgenos...");

                var parseResult = context.Parser.Parse(ocrResult.RawText);

                // ETAPA 3: Detecção adicional de alérgenos no texto bruto
                var detectedAllergens = DetectAllergensInText(ocrResult.RawText);

                // Combinar alérgenos do parser + detecção manual
                var allAllergens = parseResult.Allergens
                    .Union(detectedAllergens)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (allAllergens.Count == 0)
                {
                    // Pode ser que não haja alérgenos (válido) ou que a detecção falhou
                    result.Success = true; // Não é erro - pode ser produto sem alérgenos
                    result.Allergens = new System.Collections.Generic.List<string>();
                    result.ExtractedData["AllergenStatus"] = "NONE_DETECTED";
                    result.Warnings.Add("Nenhum alérgeno detectado - verifique se a imagem contém declaração de alérgenos");
                    context.Logger?.LogInformation("ℹ️  Nenhum alérgeno detectado no texto");
                }
                else
                {
                    result.Success = true;
                    result.Allergens = allAllergens;
                    result.ExtractedData["AllergenStatus"] = "ALLERGENS_FOUND";

                    context.Logger?.LogInformation("✅ Alérgenos detectados: {Count}", allAllergens.Count);
                    foreach (var allergen in allAllergens)
                    {
                        context.Logger?.LogInformation("   ⚠️  {Allergen}", allergen);
                    }
                }

                result.ExtractedData["Allergens"] = allAllergens;
                result.ExtractedData["AllergenCount"] = allAllergens.Count;
                result.ExtractedData["HasTraces"] = ContainsTracesStatement(ocrResult.RawText);
                result.ExtractedData["HasContains"] = ContainsConfirmedStatement(ocrResult.RawText);

                result.Metadata["OcrProvider"] = ocrResult.ProviderMetadata?.GetValueOrDefault("SelectedProvider", "Unknown") ?? "Unknown";
                result.Metadata["AllergenCount"] = allAllergens.Count.ToString();
                result.Metadata["ParserConfidence"] = parseResult.ParsingConfidence.ToString();

                result.QualityLevel = AssessQuality(ocrResult.Confidence, allAllergens.Count);

                if (result.QualityLevel == "LOW" && allAllergens.Count > 0)
                {
                    result.Warnings.Add("⚠️  ATENÇÃO: Confiança baixa na detecção de alérgenos");
                    result.Warnings.Add("⚠️  Verifique manualmente - segurança alimentar crítica");
                }

                result.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;

                context.Logger?.LogInformation("╔═══════════════════════════════════════════════════════════╗");
                context.Logger?.LogInformation("║ ✅ ALLERGEN STATEMENT STRATEGY - SUCESSO");
                context.Logger?.LogInformation("║ Alérgenos detectados: {Count}", allAllergens.Count);
                context.Logger?.LogInformation("║ Status: {Status}", result.ExtractedData["AllergenStatus"]);
                context.Logger?.LogInformation("║ Confiança: {Confidence:P2}", result.Confidence);
                context.Logger?.LogInformation("║ Qualidade: {Quality}", result.QualityLevel);
                context.Logger?.LogInformation("║ Tempo: {Time:F2}s", result.ProcessingTimeSeconds);
                context.Logger?.LogInformation("╚═══════════════════════════════════════════════════════════╝");

                return result;
            }
            catch (Exception ex)
            {
                context.Logger?.LogError(ex, "❌ Erro fatal na estratégia de alérgenos");
                result.Success = false;
                result.ErrorMessage = $"Erro ao processar declaração de alérgenos: {ex.Message}";
                result.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;
                return result;
            }
            finally
            {
                // Limpar arquivo temporário
                if (!string.IsNullOrEmpty(tempImagePath) && File.Exists(tempImagePath))
                {
                    try
                    {
                        File.Delete(tempImagePath);
                    }
                    catch (Exception ex)
                    {
                        context.Logger?.LogWarning(ex, "⚠️ Erro ao remover arquivo temporário: {Path}", tempImagePath);
                    }
                }
            }
        }

        private System.Collections.Generic.List<string> DetectAllergensInText(string text)
        {
            var detected = new System.Collections.Generic.List<string>();
            var lowerText = text.ToLowerInvariant();

            foreach (var allergen in KnownAllergens)
            {
                if (lowerText.Contains(allergen.ToLowerInvariant()))
                {
                    // Adicionar forma normalizada
                    var normalized = NormalizeAllergenName(allergen);
                    if (!detected.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    {
                        detected.Add(normalized);
                    }
                }
            }

            return detected;
        }

        private string NormalizeAllergenName(string allergen)
        {
            // Mapeia variações para nomes padrão
            var normalized = allergen.ToLowerInvariant();

            if (normalized.Contains("leite") || normalized.Contains("lactose") || normalized.Contains("milk"))
                return "Leite";
            if (normalized.Contains("glúten") || normalized.Contains("gluten"))
                return "Glúten";
            if (normalized.Contains("soja") || normalized.Contains("soy"))
                return "Soja";
            if (normalized.Contains("amendoim") || normalized.Contains("peanut"))
                return "Amendoim";
            if (normalized.Contains("ovo") || normalized.Contains("egg"))
                return "Ovo";
            if (normalized.Contains("peixe") || normalized.Contains("fish"))
                return "Peixe";
            if (normalized.Contains("nozes") || normalized.Contains("nuts") || normalized.Contains("castanhas"))
                return "Nozes";

            // Capitalizar primeira letra
            return char.ToUpper(allergen[0]) + allergen.Substring(1).ToLower();
        }

        private bool ContainsTracesStatement(string text)
        {
            var lowerText = text.ToLowerInvariant();
            return lowerText.Contains("pode conter") ||
                   lowerText.Contains("traços de") ||
                   lowerText.Contains("may contain") ||
                   lowerText.Contains("traces of");
        }

        private bool ContainsConfirmedStatement(string text)
        {
            var lowerText = text.ToLowerInvariant();
            return lowerText.Contains("contém") ||
                   lowerText.Contains("contains") ||
                   lowerText.Contains("alérgicos:");
        }

        private string AssessQuality(double ocrConfidence, int allergenCount)
        {
            // Alta qualidade: boa confiança OCR
            if (ocrConfidence >= 0.90)
                return "HIGH";

            // Média qualidade
            if (ocrConfidence >= 0.75)
                return "MEDIUM";

            // Baixa qualidade - CRÍTICO para alérgenos
            return "LOW";
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
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };
        }

        private string GetImageExtension(string contentType)
        {
            return contentType.ToLowerInvariant() switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
        }
    }
}
