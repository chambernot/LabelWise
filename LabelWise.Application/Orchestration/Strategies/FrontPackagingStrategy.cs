using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LabelWise.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelWise.Application.Orchestration.Strategies
{
    /// <summary>
    /// Estratégia para processar capturas da embalagem frontal.
    /// 
    /// COMPORTAMENTO:
    /// - Foca em identificação do produto (nome, marca, categoria)
    /// - Extrai claims nutricionais ("sem açúcar", "integral", etc.)
    /// - Detecta informações de marketing
    /// - Pode usar OCR + reconhecimento visual futuro
    /// 
    /// CARACTERÍSTICAS:
    /// - Tenta extrair nome do produto
    /// - Identifica marca
    /// - Detecta categoria (ex: biscoito, bebida, etc.)
    /// - Captura claims de saúde
    /// - Pode ser usado para busca por similaridade no futuro
    /// </summary>
    public class FrontPackagingStrategy : CaptureTypeStrategy
    {
        public override CaptureType CaptureType => CaptureType.FrontPackaging;
        public override string StrategyName => "Front Packaging Recognition Strategy";
        public override bool RequiresOcr => true;
        public override bool CanIdentifyProduct => true;

        public override async Task<CaptureProcessingResult> ProcessAsync(
            byte[] imageData,
            string? fileName,
            CaptureProcessingContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new CaptureProcessingResult
            {
                CaptureType = CaptureType.FrontPackaging,
                Metadata =
                {
                    ["Strategy"] = StrategyName,
                    ["ProcessingType"] = "Front Package OCR + Product Recognition"
                }
            };

            context.Logger?.LogInformation("╔═══════════════════════════════════════════════════════════╗");
            context.Logger?.LogInformation("║ 📦 ESTRATÉGIA: {Strategy}", StrategyName);
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

                // ETAPA 2: OCR da embalagem frontal
                context.Logger?.LogInformation("🔍 ETAPA 1: OCR da embalagem frontal...");

                var ocrRequest = new Application.DTOs.OcrRequestDto
                {
                    ImagePath = tempImagePath,
                    FileName = fileName ?? "front_packaging.jpg",
                    ContentType = contentType
                };

                var ocrResult = await context.OcrProvider.ExtractTextAsync(ocrRequest);

                if (!ocrResult.Success)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Falha no OCR da embalagem: {ocrResult.ErrorMessage}";
                    result.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;
                    context.Logger?.LogWarning("❌ Falha no OCR: {Error}", ocrResult.ErrorMessage);
                    return result;
                }

                context.Logger?.LogInformation("✅ OCR concluído - Confiança: {Confidence:P2}", ocrResult.Confidence);
                context.Logger?.LogInformation("   Caracteres extraídos: {Chars}", ocrResult.RawText.Length);

                result.RawText = ocrResult.RawText;
                result.Confidence = ocrResult.Confidence;

                // ETAPA 2: Extração de informações do produto
                context.Logger?.LogInformation("🔍 ETAPA 2: Identificação do produto...");

                var productInfo = ExtractProductInformation(ocrResult.RawText);

                if (productInfo == null || string.IsNullOrWhiteSpace(productInfo.ProductName))
                {
                    result.Success = false;
                    result.ErrorMessage = "Não foi possível identificar o produto na embalagem";
                    result.Warnings.Add("Texto detectado, mas nenhuma informação de produto clara");
                    result.Warnings.Add("Tente capturar a embalagem com foco no nome do produto");
                    result.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;
                    context.Logger?.LogWarning("⚠️ Produto não identificado");
                    return result;
                }

                context.Logger?.LogInformation("✅ Produto identificado:");
                context.Logger?.LogInformation("   Nome: {Name}", productInfo.ProductName);
                if (!string.IsNullOrWhiteSpace(productInfo.Brand))
                    context.Logger?.LogInformation("   Marca: {Brand}", productInfo.Brand);
                if (!string.IsNullOrWhiteSpace(productInfo.Category))
                    context.Logger?.LogInformation("   Categoria: {Category}", productInfo.Category);

                // ETAPA 3: Extração de claims nutricionais
                var claims = ExtractNutritionalClaims(ocrResult.RawText);

                if (claims.Count > 0)
                {
                    context.Logger?.LogInformation("✅ Claims encontrados: {Count}", claims.Count);
                    foreach (var claim in claims)
                    {
                        context.Logger?.LogInformation("   • {Claim}", claim);
                    }
                }

                result.Success = true;
                result.ProductIdentification = productInfo;
                result.ExtractedData["ProductInfo"] = productInfo;
                result.ExtractedData["Claims"] = claims;
                result.ExtractedData["ClaimCount"] = claims.Count;

                result.Metadata["OcrProvider"] = ocrResult.ProviderMetadata?.GetValueOrDefault("SelectedProvider", "Unknown") ?? "Unknown";
                result.Metadata["HasBrand"] = (!string.IsNullOrWhiteSpace(productInfo.Brand)).ToString();
                result.Metadata["HasCategory"] = (!string.IsNullOrWhiteSpace(productInfo.Category)).ToString();
                result.Metadata["ClaimCount"] = claims.Count.ToString();

                result.QualityLevel = AssessQuality(ocrResult.Confidence, productInfo);

                if (result.QualityLevel == "LOW")
                {
                    result.Warnings.Add("Qualidade da identificação está abaixo do ideal");
                    result.Warnings.Add("Alguns dados podem estar incompletos");
                }

                result.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;

                context.Logger?.LogInformation("╔═══════════════════════════════════════════════════════════╗");
                context.Logger?.LogInformation("║ ✅ FRONT PACKAGING STRATEGY - SUCESSO");
                context.Logger?.LogInformation("║ Produto: {Name}", productInfo.ProductName);
                context.Logger?.LogInformation("║ Claims: {Claims}", claims.Count);
                context.Logger?.LogInformation("║ Confiança: {Confidence:P2}", result.Confidence);
                context.Logger?.LogInformation("║ Qualidade: {Quality}", result.QualityLevel);
                context.Logger?.LogInformation("║ Tempo: {Time:F2}s", result.ProcessingTimeSeconds);
                context.Logger?.LogInformation("╚═══════════════════════════════════════════════════════════╝");

                return result;
            }
            catch (Exception ex)
            {
                context.Logger?.LogError(ex, "❌ Erro fatal na estratégia de embalagem frontal");
                result.Success = false;
                result.ErrorMessage = $"Erro ao processar embalagem: {ex.Message}";
                result.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;
                return result;
            }
        }

        private ProductIdentificationData? ExtractProductInformation(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(l => l.Trim())
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .ToList();

            if (lines.Count == 0)
                return null;

            var productInfo = new ProductIdentificationData();

            // Heurística: primeira linha maior geralmente é o nome do produto
            var productNameLine = lines
                .Where(l => l.Length > 5 && l.Length < 100) // Tamanho razoável
                .OrderByDescending(l => l.Length)
                .FirstOrDefault();

            if (productNameLine != null)
            {
                productInfo.ProductName = productNameLine;
            }

            // Tentar identificar marca (palavras em maiúsculo ou marcas conhecidas)
            var brandLine = lines
                .Where(l => l.Length > 2 && l.Length < 30)
                .FirstOrDefault(l => l.All(c => char.IsUpper(c) || char.IsWhiteSpace(c) || char.IsPunctuation(c)));

            if (brandLine != null)
            {
                productInfo.Brand = brandLine;
            }

            // Tentar identificar categoria (palavras-chave comuns)
            productInfo.Category = IdentifyCategory(text);

            // Confiança baseada em quantas informações conseguimos extrair
            int fieldsFound = 0;
            if (!string.IsNullOrWhiteSpace(productInfo.ProductName)) fieldsFound++;
            if (!string.IsNullOrWhiteSpace(productInfo.Brand)) fieldsFound++;
            if (!string.IsNullOrWhiteSpace(productInfo.Category)) fieldsFound++;

            productInfo.Confidence = fieldsFound / 3.0;

            return productInfo;
        }

        private string? IdentifyCategory(string text)
        {
            var lowerText = text.ToLowerInvariant();

            // Categorias comuns
            var categories = new Dictionary<string[], string>
            {
                { new[] { "biscoito", "cookie", "wafer" }, "Biscoito" },
                { new[] { "chocolate", "cacau" }, "Chocolate" },
                { new[] { "leite", "milk", "iogurte", "yogurt" }, "Laticínio" },
                { new[] { "suco", "juice", "bebida" }, "Bebida" },
                { new[] { "cereal", "granola", "aveia" }, "Cereal" },
                { new[] { "macarrão", "pasta", "massa" }, "Massa" },
                { new[] { "molho", "sauce", "ketchup" }, "Molho/Condimento" },
                { new[] { "snack", "salgadinho", "chips" }, "Snack" }
            };

            foreach (var category in categories)
            {
                if (category.Key.Any(keyword => lowerText.Contains(keyword)))
                {
                    return category.Value;
                }
            }

            return null;
        }

        private System.Collections.Generic.List<string> ExtractNutritionalClaims(string text)
        {
            var claims = new System.Collections.Generic.List<string>();
            var lowerText = text.ToLowerInvariant();

            // Claims nutricionais comuns
            var claimPatterns = new Dictionary<string[], string>
            {
                { new[] { "sem açúcar", "sugar free", "zero açúcar" }, "Sem Açúcar" },
                { new[] { "sem gordura", "fat free", "0% gordura" }, "Sem Gordura" },
                { new[] { "integral", "whole grain", "grão integral" }, "Integral" },
                { new[] { "light", "leve" }, "Light" },
                { new[] { "sem glúten", "gluten free" }, "Sem Glúten" },
                { new[] { "sem lactose", "lactose free" }, "Sem Lactose" },
                { new[] { "orgânico", "organic" }, "Orgânico" },
                { new[] { "natural", "100% natural" }, "Natural" },
                { new[] { "rico em fibra", "high fiber", "fonte de fibra" }, "Rico em Fibras" },
                { new[] { "rico em proteína", "high protein" }, "Rico em Proteína" },
                { new[] { "baixo sódio", "low sodium" }, "Baixo Sódio" },
                { new[] { "diet" }, "Diet" }
            };

            foreach (var pattern in claimPatterns)
            {
                if (pattern.Key.Any(keyword => lowerText.Contains(keyword)))
                {
                    claims.Add(pattern.Value);
                }
            }

            return claims.Distinct().ToList();
        }

        private string AssessQuality(double ocrConfidence, ProductIdentificationData productInfo)
        {
            var hasName = !string.IsNullOrWhiteSpace(productInfo.ProductName);
            var hasBrand = !string.IsNullOrWhiteSpace(productInfo.Brand);
            var hasCategory = !string.IsNullOrWhiteSpace(productInfo.Category);

            // Alta qualidade: boa confiança + todos os campos
            if (ocrConfidence >= 0.90 && hasName && hasBrand && hasCategory)
                return "HIGH";

            // Média qualidade: confiança razoável + nome do produto
            if (ocrConfidence >= 0.75 && hasName)
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

        private string GetImageExtension(string contentType)
        {
            return contentType.ToLowerInvariant() switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                _ => ".jpg"
            };
        }
    }
}
