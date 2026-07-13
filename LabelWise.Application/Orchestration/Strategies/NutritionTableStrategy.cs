using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using LabelWise.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelWise.Application.Orchestration.Strategies
{
    /// <summary>
    /// Estratégia para processar capturas da tabela nutricional.
    /// 
    /// COMPORTAMENTO:
    /// - Foca em OCR e extração de valores nutricionais
    /// - Estrutura dados da tabela (calorias, proteínas, carboidratos, etc.)
    /// - Não tenta identificar o produto pela tabela
    /// - Alta precisão em extração de números
    /// 
    /// CARACTERÍSTICAS:
    /// - Usa pré-processamento específico para tabelas
    /// - Reconhece padrões de layout de tabelas nutricionais
    /// - Extrai valores e unidades de medida
    /// - Valida consistência dos dados
    /// </summary>
    public class NutritionTableStrategy : CaptureTypeStrategy
    {
        public override CaptureType CaptureType => CaptureType.NutritionTable;
        public override string StrategyName => "Nutrition Table Extraction Strategy";
        public override bool RequiresOcr => true;
        public override bool CanIdentifyProduct => false;
        public override bool HasNutritionalData => true;

        public override async Task<CaptureProcessingResult> ProcessAsync(
            byte[] imageData,
            string? fileName,
            CaptureProcessingContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new CaptureProcessingResult
            {
                CaptureType = CaptureType.NutritionTable,
                Metadata =
                {
                    ["Strategy"] = StrategyName,
                    ["ProcessingType"] = "Nutritional Table OCR + Parsing"
                }
            };

            context.Logger?.LogInformation("╔═══════════════════════════════════════════════════════════╗");
            context.Logger?.LogInformation("║ 🍎 ESTRATÉGIA: {Strategy}", StrategyName);
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

                // ETAPA 2: OCR da tabela nutricional
                context.Logger?.LogInformation("🔍 ETAPA 1: OCR da tabela nutricional...");

                var ocrRequest = new Application.DTOs.OcrRequestDto
                {
                    ImagePath = tempImagePath,
                    FileName = fileName ?? "nutrition_table.jpg",
                    ContentType = contentType
                };

                var ocrResult = await context.OcrProvider.ExtractTextAsync(ocrRequest);

                if (!ocrResult.Success)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Falha no OCR da tabela nutricional: {ocrResult.ErrorMessage}";
                    result.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;
                    context.Logger?.LogWarning("❌ Falha no OCR: {Error}", ocrResult.ErrorMessage);
                    return result;
                }

                context.Logger?.LogInformation("✅ OCR concluído - Confiança: {Confidence:P2}", ocrResult.Confidence);
                context.Logger?.LogInformation("   Caracteres extraídos: {Chars}", ocrResult.RawText.Length);

                result.RawText = ocrResult.RawText;
                result.Confidence = ocrResult.Confidence;

                // ETAPA 2: Parsing da tabela nutricional
                context.Logger?.LogInformation("🔍 ETAPA 2: Parsing de dados nutricionais...");

                var nutritionalData = ParseNutritionalTable(ocrResult.RawText, context);

                if (nutritionalData == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Não foi possível extrair dados nutricionais válidos da tabela";
                    result.Warnings.Add("Texto detectado, mas estrutura de tabela nutricional não identificada");
                    result.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;
                    context.Logger?.LogWarning("⚠️ Nenhum dado nutricional válido encontrado");
                    return result;
                }

                context.Logger?.LogInformation("✅ Dados nutricionais extraídos:");
                context.Logger?.LogInformation("   Calorias: {Cal} kcal", nutritionalData.Calories);
                context.Logger?.LogInformation("   Proteínas: {Prot}g", nutritionalData.Protein);
                context.Logger?.LogInformation("   Carboidratos: {Carb}g", nutritionalData.Carbohydrates);
                context.Logger?.LogInformation("   Gorduras: {Fat}g", nutritionalData.Fats);

                // ETAPA 3: Validação e quality assessment
                result.Success = true;
                result.NutritionalData = nutritionalData;
                result.ExtractedData["NutritionalData"] = nutritionalData;
                result.Metadata["OcrProvider"] = ocrResult.ProviderMetadata?.GetValueOrDefault("SelectedProvider", "Unknown") ?? "Unknown";
                result.Metadata["DataCompleteness"] = CalculateDataCompleteness(nutritionalData).ToString("P0");

                result.QualityLevel = AssessQuality(ocrResult.Confidence, nutritionalData);

                if (result.QualityLevel == "LOW")
                {
                    result.Warnings.Add("Qualidade dos dados nutricionais está abaixo do ideal");
                    result.Warnings.Add("Considere recapturar a imagem com melhor iluminação");
                }

                result.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;

                context.Logger?.LogInformation("╔═══════════════════════════════════════════════════════════╗");
                context.Logger?.LogInformation("║ ✅ NUTRITION TABLE STRATEGY - SUCESSO");
                context.Logger?.LogInformation("║ Dados extraídos: {Count} nutrientes", GetNutrientCount(nutritionalData));
                context.Logger?.LogInformation("║ Completude: {Completeness:P0}", CalculateDataCompleteness(nutritionalData));
                context.Logger?.LogInformation("║ Confiança: {Confidence:P2}", result.Confidence);
                context.Logger?.LogInformation("║ Qualidade: {Quality}", result.QualityLevel);
                context.Logger?.LogInformation("║ Tempo: {Time:F2}s", result.ProcessingTimeSeconds);
                context.Logger?.LogInformation("╚═══════════════════════════════════════════════════════════╝");

                return result;
            }
            catch (Exception ex)
            {
                context.Logger?.LogError(ex, "❌ Erro fatal na estratégia de tabela nutricional");
                result.Success = false;
                result.ErrorMessage = $"Erro ao processar tabela nutricional: {ex.Message}";
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

        private NutritionalData? ParseNutritionalTable(string text, CaptureProcessingContext context)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var data = new NutritionalData();

            // Padrões de expressão regular para extrair valores nutricionais
            // Formato comum: "Nutriente: XXX unidade" ou "Nutriente XXX unidade"

            data.Calories = ExtractNutrientValue(text, new[] { "calorias", "energia", "energy", "calories", "kcal" }, "kcal");
            data.Protein = ExtractNutrientValue(text, new[] { "proteínas", "proteinas", "protein" }, "g");
            data.Carbohydrates = ExtractNutrientValue(text, new[] { "carboidratos", "carbohidratos", "carbohydrates", "carbs" }, "g");
            data.Fats = ExtractNutrientValue(text, new[] { "gorduras", "gordura", "fats", "fat", "lipídeos", "lipideos" }, "g");
            data.Fiber = ExtractNutrientValue(text, new[] { "fibras", "fibra", "fiber" }, "g");
            data.Sodium = ExtractNutrientValue(text, new[] { "sódio", "sodio", "sodium" }, "mg");

            // Extrair porção/serving size
            data.ServingSize = ExtractServingSize(text);

            // Calcular confiança baseada em quantos campos foram extraídos
            int fieldsExtracted = 0;
            if (data.Calories.HasValue) fieldsExtracted++;
            if (data.Protein.HasValue) fieldsExtracted++;
            if (data.Carbohydrates.HasValue) fieldsExtracted++;
            if (data.Fats.HasValue) fieldsExtracted++;

            // Mínimo de 2 campos obrigatórios para considerar válido
            if (fieldsExtracted < 2)
                return null;

            data.Confidence = fieldsExtracted / 6.0; // 6 campos principais

            return data;
        }

        private double? ExtractNutrientValue(string text, string[] keywords, string unit)
        {
            foreach (var keyword in keywords)
            {
                // Padrão: keyword seguido por número (pode ter : ou espaços)
                var pattern = $@"{keyword}[:\s]+(\d+[.,]?\d*)\s*{unit}?";
                var match = System.Text.RegularExpressions.Regex.Match(
                    text,
                    pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (match.Success && double.TryParse(match.Groups[1].Value.Replace(",", "."), out var value))
                {
                    return value;
                }
            }

            return null;
        }

        private string? ExtractServingSize(string text)
        {
            var pattern = @"porção[:\s]+(\d+\s*g|\d+\s*ml)";
            var match = System.Text.RegularExpressions.Regex.Match(
                text,
                pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return match.Success ? match.Groups[1].Value : null;
        }

        private double CalculateDataCompleteness(NutritionalData data)
        {
            int total = 6; // Campos principais
            int filled = 0;

            if (data.Calories.HasValue) filled++;
            if (data.Protein.HasValue) filled++;
            if (data.Carbohydrates.HasValue) filled++;
            if (data.Fats.HasValue) filled++;
            if (data.Fiber.HasValue) filled++;
            if (data.Sodium.HasValue) filled++;

            return (double)filled / total;
        }

        private int GetNutrientCount(NutritionalData data)
        {
            int count = 0;
            if (data.Calories.HasValue) count++;
            if (data.Protein.HasValue) count++;
            if (data.Carbohydrates.HasValue) count++;
            if (data.Fats.HasValue) count++;
            if (data.Fiber.HasValue) count++;
            if (data.Sodium.HasValue) count++;
            return count;
        }

        private string AssessQuality(double ocrConfidence, NutritionalData data)
        {
            var completeness = CalculateDataCompleteness(data);

            if (ocrConfidence >= 0.90 && completeness >= 0.80)
                return "HIGH";
            if (ocrConfidence >= 0.75 && completeness >= 0.50)
                return "MEDIUM";
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
                _ => "image/jpeg"
            };
        }
    }
}
