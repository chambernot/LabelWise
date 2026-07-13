using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services.LabelReading
{
    /// <summary>
    /// Estratégia para "ler" código de barras.
    /// 
    /// OBJETIVO:
    /// Esta estratégia existe para completude, mas códigos de barras
    /// não são processados por OCR textual - são lidos por bibliotecas
    /// específicas de leitura de códigos de barras (ZXing, etc).
    /// 
    /// COMPORTAMENTO:
    /// - Retorna sucesso com confiança 1.0 se o texto parece um código de barras
    /// - Retorna erro se não parece um código de barras
    /// </summary>
    public class BarcodeReadingStrategy : ICaptureReadingStrategy
    {
        private readonly ILogger _logger;

        public BarcodeReadingStrategy(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public CaptureReadingStrategyResult Parse(string rawOcrText, double ocrConfidence)
        {
            _logger.LogDebug("📊 Processando captura de código de barras...");

            var result = new CaptureReadingStrategyResult
            {
                Success = false,
                Confidence = 0
            };

            if (string.IsNullOrWhiteSpace(rawOcrText))
            {
                result.ErrorMessage = "Texto OCR vazio - código de barras deve ser lido com biblioteca especializada";
                return result;
            }

            try
            {
                // Verificar se parece um código de barras (apenas números, 8-14 dígitos)
                var cleanedText = rawOcrText.Trim().Replace(" ", "").Replace("-", "");

                if (cleanedText.Length >= 8 && cleanedText.Length <= 14 && IsAllDigits(cleanedText))
                {
                    var barcodeData = new { Barcode = cleanedText };
                    result.StructuredData = JsonSerializer.Serialize(barcodeData);
                    result.Success = true;
                    result.Confidence = 1.0; // Se é válido, confiança é alta

                    result.Metadata["BarcodeType"] = DetermineBarcodeType(cleanedText.Length);
                    result.Metadata["BarcodeLength"] = cleanedText.Length.ToString();

                    _logger.LogDebug("   ✅ Código de barras detectado: {Barcode} ({Type})",
                        cleanedText, result.Metadata["BarcodeType"]);
                }
                else
                {
                    result.ErrorMessage = "Texto não parece um código de barras válido. Use biblioteca especializada (ZXing, etc)";
                    result.Confidence = 0;

                    _logger.LogWarning("   ⚠️ Texto não é um código de barras válido: {Text}",
                        rawOcrText.Substring(0, Math.Min(50, rawOcrText.Length)));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao processar código de barras");
                result.ErrorMessage = $"Erro no processamento: {ex.Message}";
                result.Success = false;
            }

            return result;
        }

        private bool IsAllDigits(string text)
        {
            foreach (var c in text)
            {
                if (!char.IsDigit(c))
                    return false;
            }
            return true;
        }

        private string DetermineBarcodeType(int length)
        {
            return length switch
            {
                8 => "EAN-8",
                12 => "UPC-A",
                13 => "EAN-13",
                14 => "GTIN-14",
                _ => $"Unknown ({length} digits)"
            };
        }
    }
}
