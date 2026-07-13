// ═══════════════════════════════════════════════════════════════════════════
// AZURE OPENAI VISION INTEGRATION - USAGE EXAMPLES
// ═══════════════════════════════════════════════════════════════════════════

using LabelWise.Application.DTOs.AI;
using LabelWise.Application.DTOs.ProductIdentification;
using LabelWise.Application.Helpers.ProductIdentification;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelWise.Examples
{
    /// <summary>
    /// Exemplos de uso da integração Azure OpenAI Vision para identificação de produtos.
    /// </summary>
    public class AzureOpenAiVisionIntegrationExamples
    {
        private readonly IProductIdentificationService _identificationService;
        private readonly ILogger<AzureOpenAiVisionIntegrationExamples> _logger;

        public AzureOpenAiVisionIntegrationExamples(
            IProductIdentificationService identificationService,
            ILogger<AzureOpenAiVisionIntegrationExamples> logger)
        {
            _identificationService = identificationService;
            _logger = logger;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 1: Identificação com Barcode (Máxima Prioridade)
        // ═══════════════════════════════════════════════════════════════════════

        public async Task Example1_IdentificationWithBarcode()
        {
            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            _logger.LogInformation("EXEMPLO 1: Identificação com Barcode");
            _logger.LogInformation("═══════════════════════════════════════════════════════════");

            var request = new ProductIdentificationRequest
            {
                UserId = 1,
                ImageData = LoadImageBytes("barcode_image.jpg"),
                CaptureType = CaptureType.Barcode,
                ManualBarcode = "7891234567890",  // ✅ Barcode fornecido
                EnableOcrFallback = false
            };

            var result = await _identificationService.IdentifyProductAsync(request);

            // ✅ Resultado esperado:
            // - MatchSource = Barcode
            // - Confidence = 0.85+
            // - Vision NÃO é usado (barcode tem prioridade máxima)

            _logger.LogInformation("✅ Resultado:");
            _logger.LogInformation("   MatchSource: {Source} (Prioridade: 100)", result.MatchSource);
            _logger.LogInformation("   Confidence: {Confidence:P2}", result.MatchConfidence);
            _logger.LogInformation("   Vision usado? NÃO (barcode tem prioridade)");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 2: OCR Suficiente (Não Precisa Vision)
        // ═══════════════════════════════════════════════════════════════════════

        public async Task Example2_SufficientOcr()
        {
            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            _logger.LogInformation("EXEMPLO 2: OCR Suficiente");
            _logger.LogInformation("═══════════════════════════════════════════════════════════");

            var request = new ProductIdentificationRequest
            {
                UserId = 1,
                ImageData = LoadImageBytes("clear_front_packaging.jpg"),  // ✅ Imagem nítida
                CaptureType = CaptureType.FrontPackaging,
                EnableOcrFallback = true
            };

            var result = await _identificationService.IdentifyProductAsync(request);

            // ✅ Resultado esperado:
            // - MatchSource = FrontOcr
            // - OCR Confidence >= 0.75
            // - Nome e marca extraídos com sucesso
            // - Vision NÃO é usado (OCR suficiente)

            _logger.LogInformation("✅ Resultado:");
            _logger.LogInformation("   MatchSource: {Source} (Prioridade: 60)", result.MatchSource);
            _logger.LogInformation("   Confidence: {Confidence:P2}", result.MatchConfidence);
            _logger.LogInformation("   ProductName: {Name}", result.MatchedProductName);
            _logger.LogInformation("   Brand: {Brand}", result.MatchedBrand);
            _logger.LogInformation("   Vision usado? NÃO (OCR suficiente)");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 3: OCR Insuficiente → OCR + Vision (Consolidado)
        // ═══════════════════════════════════════════════════════════════════════

        public async Task Example3_OcrPlusVision()
        {
            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            _logger.LogInformation("EXEMPLO 3: OCR Insuficiente → OCR + Vision");
            _logger.LogInformation("═══════════════════════════════════════════════════════════");

            var request = new ProductIdentificationRequest
            {
                UserId = 1,
                ImageData = LoadImageBytes("blurry_front_packaging.jpg"),  // ⚠️ Imagem borrada
                CaptureType = CaptureType.FrontPackaging,
                EnableOcrFallback = true
            };

            var result = await _identificationService.IdentifyProductAsync(request);

            // ✅ Resultado esperado:
            // - MatchSource = OcrPlusOpenAiVision ✨
            // - OCR Confidence < 0.75 (insuficiente)
            // - Vision complementa OCR
            // - Resultado consolidado (média ponderada)

            _logger.LogInformation("✅ Resultado:");
            _logger.LogInformation("   MatchSource: {Source} (Prioridade: 90) ✨", result.MatchSource);
            _logger.LogInformation("   Confidence: {Confidence:P2}", result.MatchConfidence);
            _logger.LogInformation("   ProductName: {Name}", result.MatchedProductName);
            _logger.LogInformation("   Brand: {Brand}", result.MatchedBrand);
            _logger.LogInformation("   Vision usado? SIM (OCR insuficiente)");

            // Verificar metadata
            if (result.Metadata.TryGetValue("OcrConfidence", out var ocrConf))
                _logger.LogInformation("   OCR Confidence: {Conf}", ocrConf);
            if (result.Metadata.TryGetValue("VisionConfidence", out var visionConf))
                _logger.LogInformation("   Vision Confidence: {Conf}", visionConf);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 4: OCR Falhou → Vision Standalone
        // ═══════════════════════════════════════════════════════════════════════

        public async Task Example4_VisionStandalone()
        {
            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            _logger.LogInformation("EXEMPLO 4: OCR Falhou → Vision Standalone");
            _logger.LogInformation("═══════════════════════════════════════════════════════════");

            var request = new ProductIdentificationRequest
            {
                UserId = 1,
                ImageData = LoadImageBytes("very_bad_quality.jpg"),  // ❌ Qualidade muito ruim
                CaptureType = CaptureType.FrontPackaging,
                EnableOcrFallback = true
            };

            var result = await _identificationService.IdentifyProductAsync(request);

            // ✅ Resultado esperado:
            // - MatchSource = OpenAiVision ✨
            // - OCR falhou completamente (Confidence = 0.0)
            // - Apenas Vision foi usado
            // - Vision pode salvar a situação

            _logger.LogInformation("✅ Resultado:");
            _logger.LogInformation("   MatchSource: {Source} (Prioridade: 80) ✨", result.MatchSource);
            _logger.LogInformation("   Confidence: {Confidence:P2}", result.MatchConfidence);
            _logger.LogInformation("   ProductName: {Name}", result.MatchedProductName ?? "N/A");
            _logger.LogInformation("   Brand: {Brand}", result.MatchedBrand ?? "N/A");
            _logger.LogInformation("   Vision usado? SIM (OCR falhou)");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 5: OCR com Ruído → Vision Corrige
        // ═══════════════════════════════════════════════════════════════════════

        public async Task Example5_NoiseFiltering()
        {
            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            _logger.LogInformation("EXEMPLO 5: OCR com Ruído → Vision Corrige");
            _logger.LogInformation("═══════════════════════════════════════════════════════════");

            var request = new ProductIdentificationRequest
            {
                UserId = 1,
                ImageData = LoadImageBytes("nutrition_table_visible.jpg"),  // Tabela nutricional visível
                CaptureType = CaptureType.FrontPackaging,
                EnableOcrFallback = true
            };

            var result = await _identificationService.IdentifyProductAsync(request);

            // ✅ Resultado esperado:
            // - MatchSource = OcrPlusOpenAiVision
            // - OCR capturou "INFORMAÇÃO NUTRICIONAL" (ruído)
            // - Vision identificou o produto correto
            // - Consolidador filtrou o ruído

            _logger.LogInformation("✅ Resultado:");
            _logger.LogInformation("   MatchSource: {Source}", result.MatchSource);
            
            if (result.Metadata.TryGetValue("OcrName", out var ocrName))
                _logger.LogInformation("   OCR Name (filtrado): {Name}", ocrName);
            if (result.Metadata.TryGetValue("VisionName", out var visionName))
                _logger.LogInformation("   Vision Name (usado): {Name}", visionName);

            _logger.LogInformation("   ProductName Final: {Name}", result.MatchedProductName);
            _logger.LogInformation("   ✅ Ruído filtrado com sucesso!");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 6: Comparação de Resultados (Prioritizer)
        // ═══════════════════════════════════════════════════════════════════════

        public void Example6_ResultPrioritization()
        {
            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            _logger.LogInformation("EXEMPLO 6: Priorização de Resultados");
            _logger.LogInformation("═══════════════════════════════════════════════════════════");

            // Simular dois resultados
            var ocrResult = new ProductIdentificationResult
            {
                Success = true,
                MatchSource = MatchSource.FrontOcr,
                MatchConfidence = 0.68,
                MatchedProductName = "INFORMAÇÃO NUTRICIONAL"  // ❌ Ruído
            };

            var visionResult = new ProductIdentificationResult
            {
                Success = true,
                MatchSource = MatchSource.OpenAiVision,
                MatchConfidence = 0.85,
                MatchedProductName = "Biscoito Recheado",       // ✅ Correto
                MatchedBrand = "Bauducco"
            };

            // Comparar e escolher melhor
            var bestResult = ProductIdentificationPrioritizer.ChooseBestResult(
                ocrResult, visionResult, _logger);

            _logger.LogInformation("✅ Melhor Resultado:");
            _logger.LogInformation("   Escolhido: {Source} (Prioridade: {Priority})", 
                bestResult.MatchSource,
                ProductIdentificationPrioritizer.GetSourcePriority(bestResult.MatchSource));
            _logger.LogInformation("   ProductName: {Name}", bestResult.MatchedProductName);
            _logger.LogInformation("   Confidence: {Confidence:P2}", bestResult.MatchConfidence);

            // Verificar threshold
            bool meetsThreshold = ProductIdentificationPrioritizer.MeetsConfidenceThreshold(
                bestResult, _logger);

            _logger.LogInformation("   Meets Threshold? {Result}", meetsThreshold ? "✅ YES" : "❌ NO");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 7: Consolidação Manual (Consolidator)
        // ═══════════════════════════════════════════════════════════════════════

        public void Example7_ManualConsolidation()
        {
            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            _logger.LogInformation("EXEMPLO 7: Consolidação Manual OCR + Vision");
            _logger.LogInformation("═══════════════════════════════════════════════════════════");

            // Simular resultados OCR e Vision
            var ocrResult = new Application.DTOs.OcrResultDto
            {
                Success = true,
                Confidence = 0.62,
                RawText = "INFORMAÇÃO NUTRICIONAL\nBauducco\nBiscoito Recheado"
            };

            var visionResult = new VisualInterpretationResult
            {
                ProbableProductName = "Biscoito Recheado Chocolate",
                ProbableBrand = "Bauducco",
                ProbableCategory = "Biscoitos",
                InterpretationConfidence = ConfidenceLevel.High,
                InterpretationSummary = "Image shows a Bauducco chocolate cookie package"
            };

            // Consolidar
            var consolidated = ProductIdentificationConsolidator.ConsolidateOcrAndVision(
                ocrResult,
                visionResult,
                ocrMatchConfidence: 0.62,
                _logger);

            _logger.LogInformation("✅ Resultado Consolidado:");
            _logger.LogInformation("   MatchSource: {Source}", consolidated.MatchSource);
            _logger.LogInformation("   ProductName: {Name}", consolidated.MatchedProductName);
            _logger.LogInformation("   Brand: {Brand}", consolidated.MatchedBrand);
            _logger.LogInformation("   Category: {Category}", consolidated.Category);
            _logger.LogInformation("   Confidence: {Confidence:P2}", consolidated.MatchConfidence);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 8: Verificação de Suficiência OCR
        // ═══════════════════════════════════════════════════════════════════════

        public void Example8_OcrSufficiencyCheck()
        {
            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            _logger.LogInformation("EXEMPLO 8: Verificação de Suficiência OCR");
            _logger.LogInformation("═══════════════════════════════════════════════════════════");

            // Cenário 1: OCR suficiente
            var goodOcr = new Application.DTOs.OcrResultDto
            {
                Success = true,
                Confidence = 0.85,  // ✅ Alta confiança
                RawText = "Biscoito Recheado\nBauducco\nChocolate"
            };

            bool isSufficient1 = ProductIdentificationPrioritizer.IsOcrResultSufficient(
                goodOcr,
                extractedName: "Biscoito Recheado",
                extractedBrand: "Bauducco",
                _logger);

            _logger.LogInformation("Cenário 1 - OCR Bom:");
            _logger.LogInformation("   OCR Confidence: {Conf:P2}", goodOcr.Confidence);
            _logger.LogInformation("   É suficiente? {Result}", isSufficient1 ? "✅ SIM" : "❌ NÃO");
            _logger.LogInformation("   Precisa Vision? {Need}", isSufficient1 ? "NÃO" : "SIM");

            // Cenário 2: OCR insuficiente
            var badOcr = new Application.DTOs.OcrResultDto
            {
                Success = true,
                Confidence = 0.62,  // ⚠️ Baixa confiança
                RawText = "INFORMAÇÃO NUTRICIONAL\nValores..."
            };

            bool isSufficient2 = ProductIdentificationPrioritizer.IsOcrResultSufficient(
                badOcr,
                extractedName: null,  // ❌ Nome não extraído
                extractedBrand: null,
                _logger);

            _logger.LogInformation("\nCenário 2 - OCR Ruim:");
            _logger.LogInformation("   OCR Confidence: {Conf:P2}", badOcr.Confidence);
            _logger.LogInformation("   É suficiente? {Result}", isSufficient2 ? "✅ SIM" : "❌ NÃO");
            _logger.LogInformation("   Precisa Vision? {Need}", isSufficient2 ? "NÃO" : "✅ SIM");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 9: Thresholds de Confiança por Fonte
        // ═══════════════════════════════════════════════════════════════════════

        public void Example9_ConfidenceThresholds()
        {
            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            _logger.LogInformation("EXEMPLO 9: Thresholds de Confiança por Fonte");
            _logger.LogInformation("═══════════════════════════════════════════════════════════");

            var sources = new[]
            {
                MatchSource.Barcode,
                MatchSource.OcrPlusOpenAiVision,
                MatchSource.OpenAiVision,
                MatchSource.Combined,
                MatchSource.FrontOcr,
                MatchSource.Similarity,
                MatchSource.Unknown
            };

            _logger.LogInformation("Thresholds mínimos de confiança:\n");

            foreach (var source in sources)
            {
                var priority = ProductIdentificationPrioritizer.GetSourcePriority(source);
                var threshold = ProductIdentificationPrioritizer.GetMinimumConfidenceThreshold(source);

                _logger.LogInformation("   {Source}:", source);
                _logger.LogInformation("      Prioridade: {Priority}", priority);
                _logger.LogInformation("      Threshold: {Threshold:P0}", threshold);
                _logger.LogInformation("");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 10: Pipeline Completo (Barcode → OCR → Vision → Candidatos)
        // ═══════════════════════════════════════════════════════════════════════

        public async Task Example10_FullPipeline()
        {
            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            _logger.LogInformation("EXEMPLO 10: Pipeline Completo");
            _logger.LogInformation("═══════════════════════════════════════════════════════════");

            var request = new ProductIdentificationRequest
            {
                UserId = 1,
                ImageData = LoadImageBytes("front_packaging.jpg"),
                CaptureType = CaptureType.FrontPackaging,
                ManualBarcode = null,              // ❌ Sem barcode
                EnableOcrFallback = true           // ✅ Vision habilitado
            };

            _logger.LogInformation("🔄 Pipeline de Identificação:");
            _logger.LogInformation("   1️⃣ Tentar Barcode... ❌ (não disponível)");
            _logger.LogInformation("   2️⃣ Tentar OCR... 🔄");

            var result = await _identificationService.IdentifyProductAsync(request);

            string pipelineStep = result.MatchSource switch
            {
                MatchSource.Barcode => "1️⃣ Barcode",
                MatchSource.FrontOcr => "2️⃣ OCR",
                MatchSource.OcrPlusOpenAiVision => "3️⃣ OCR + Vision",
                MatchSource.OpenAiVision => "4️⃣ Vision",
                _ => "5️⃣ Candidatos"
            };

            _logger.LogInformation("   ✅ Sucesso no passo: {Step}", pipelineStep);
            _logger.LogInformation("");
            _logger.LogInformation("📊 Resultado Final:");
            _logger.LogInformation("   ProductName: {Name}", result.MatchedProductName ?? "N/A");
            _logger.LogInformation("   Brand: {Brand}", result.MatchedBrand ?? "N/A");
            _logger.LogInformation("   MatchSource: {Source}", result.MatchSource);
            _logger.LogInformation("   Confidence: {Confidence:P2}", result.MatchConfidence);
            _logger.LogInformation("   IsReliableMatch: {Reliable}", result.IsReliableMatch ? "✅ YES" : "⚠️ NO");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════════

        private byte[] LoadImageBytes(string filename)
        {
            // Simular carregamento de imagem
            // Em produção, carregar do disco ou receber do cliente
            return new byte[1024]; // Mock
        }
    }
}
