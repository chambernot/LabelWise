using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LabelWise.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelWise.Application.Orchestration.Strategies
{
    /// <summary>
    /// Estratégia para processar capturas de código de barras.
    /// 
    /// COMPORTAMENTO:
    /// - Foca em leitura de código de barras (EAN, UPC, etc.)
    /// - Não executa OCR completo de ingredientes/tabela
    /// - Pode buscar informações em bases externas (Open Food Facts)
    /// - Retorna identificação do produto
    /// 
    /// OTIMIZAÇÃO:
    /// - Usa algoritmos específicos de leitura de barcode
    /// - Mais rápido que OCR completo
    /// - Alta taxa de sucesso em imagens de boa qualidade
    /// </summary>
    public class BarcodeStrategy : CaptureTypeStrategy
    {
        public override CaptureType CaptureType => CaptureType.Barcode;
        public override string StrategyName => "Barcode Recognition Strategy";
        public override bool RequiresOcr => false; // Usa leitura específica de barcode
        public override bool CanIdentifyProduct => true;

        public override async Task<CaptureProcessingResult> ProcessAsync(
            byte[] imageData,
            string? fileName,
            CaptureProcessingContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new CaptureProcessingResult
            {
                CaptureType = CaptureType.Barcode,
                Metadata =
                {
                    ["Strategy"] = StrategyName,
                    ["ProcessingType"] = "Barcode Recognition"
                }
            };

            context.Logger?.LogInformation("╔═══════════════════════════════════════════════════════════╗");
            context.Logger?.LogInformation("║ 📊 ESTRATÉGIA: {Strategy}", StrategyName);
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

                // ETAPA 2: Tentar leitura de código de barras via OCR
                context.Logger?.LogInformation("🔍 ETAPA 1: Leitura de código de barras...");

                var ocrRequest = new Application.DTOs.OcrRequestDto
                {
                    ImagePath = tempImagePath,
                    FileName = fileName ?? "barcode.jpg",
                    ContentType = contentType
                };

                var ocrResult = await context.OcrProvider.ExtractTextAsync(ocrRequest);

                if (!ocrResult.Success)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Falha na leitura do código de barras: {ocrResult.ErrorMessage}";
                    result.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;
                    context.Logger?.LogWarning("❌ Falha na leitura: {Error}", ocrResult.ErrorMessage);
                    return result;
                }

                context.Logger?.LogInformation("✅ OCR concluído - Confiança: {Confidence:P2}", ocrResult.Confidence);

                // ETAPA 2: Extrair código de barras do texto OCR
                var barcode = ExtractBarcodeFromText(ocrResult.RawText);

                if (string.IsNullOrWhiteSpace(barcode))
                {
                    result.Success = false;
                    result.ErrorMessage = "Nenhum código de barras válido detectado na imagem";
                    result.RawText = ocrResult.RawText;
                    result.Confidence = ocrResult.Confidence;
                    result.Warnings.Add("Texto detectado, mas nenhum padrão de código de barras encontrado");
                    result.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;
                    context.Logger?.LogWarning("⚠️ Nenhum código de barras detectado no texto");
                    return result;
                }

                context.Logger?.LogInformation("✅ Código de barras detectado: {Barcode}", barcode);

                // ETAPA 3: Criar resultado de identificação
                result.Success = true;
                result.RawText = ocrResult.RawText;
                result.Confidence = ocrResult.Confidence;
                result.ProductIdentification = new ProductIdentificationData
                {
                    Barcode = barcode,
                    Confidence = ocrResult.Confidence
                };

                result.ExtractedData["Barcode"] = barcode;
                result.ExtractedData["BarcodeType"] = DetermineBarcodeType(barcode);
                result.Metadata["BarcodeLength"] = barcode.Length.ToString();
                result.Metadata["OcrProvider"] = ocrResult.ProviderMetadata?.GetValueOrDefault("SelectedProvider", "Unknown") ?? "Unknown";

                // ETAPA 4: Quality assessment
                result.QualityLevel = AssessQuality(ocrResult.Confidence, barcode);

                result.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;

                context.Logger?.LogInformation("╔═══════════════════════════════════════════════════════════╗");
                context.Logger?.LogInformation("║ ✅ BARCODE STRATEGY - SUCESSO");
                context.Logger?.LogInformation("║ Código: {Barcode}", barcode);
                context.Logger?.LogInformation("║ Tipo: {Type}", result.ExtractedData["BarcodeType"]);
                context.Logger?.LogInformation("║ Confiança: {Confidence:P2}", result.Confidence);
                context.Logger?.LogInformation("║ Qualidade: {Quality}", result.QualityLevel);
                context.Logger?.LogInformation("║ Tempo: {Time:F2}s", result.ProcessingTimeSeconds);
                context.Logger?.LogInformation("╚═══════════════════════════════════════════════════════════╝");

                return result;
            }
            catch (Exception ex)
            {
                context.Logger?.LogError(ex, "❌ Erro fatal na estratégia de barcode");
                result.Success = false;
                result.ErrorMessage = $"Erro ao processar código de barras: {ex.Message}";
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

        private string GetImageExtension(string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType)) return ".jpg";
            return contentType.ToLowerInvariant() switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                _ => ".jpg"
            };
        }

        private string? ExtractBarcodeFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            // Remover espaços e caracteres especiais
            var cleaned = new string(text.Where(c => char.IsDigit(c)).ToArray());

            // Padrões comuns de código de barras
            // EAN-13: 13 dígitos
            // EAN-8: 8 dígitos
            // UPC-A: 12 dígitos
            // UPC-E: 6 ou 8 dígitos

            if (cleaned.Length == 13 || cleaned.Length == 12 || cleaned.Length == 8)
            {
                return cleaned;
            }

            // Se não encontrou um padrão exato, procura por sequências de 8+ dígitos
            var matches = System.Text.RegularExpressions.Regex.Matches(text, @"\d{8,13}");
            if (matches.Count > 0)
            {
                return matches[0].Value;
            }

            return null;
        }

        private string DetermineBarcodeType(string barcode)
        {
            return barcode.Length switch
            {
                13 => "EAN-13",
                12 => "UPC-A",
                8 => "EAN-8",
                6 => "UPC-E",
                _ => "Unknown"
            };
        }

        private string AssessQuality(double confidence, string barcode)
        {
            if (confidence >= 0.95 && IsValidChecksum(barcode))
                return "HIGH";
            if (confidence >= 0.80)
                return "MEDIUM";
            return "LOW";
        }

        private bool IsValidChecksum(string barcode)
        {
            // Implementação simplificada - pode ser expandida
            if (barcode.Length != 13 && barcode.Length != 12)
                return true; // Não validamos outros formatos

            try
            {
                var digits = barcode.Select(c => int.Parse(c.ToString())).ToArray();
                var sum = 0;

                for (int i = 0; i < digits.Length - 1; i++)
                {
                    sum += digits[i] * (i % 2 == 0 ? 1 : 3);
                }

                var checkDigit = (10 - (sum % 10)) % 10;
                return checkDigit == digits[^1];
            }
            catch
            {
                return false;
            }
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
