// ═══════════════════════════════════════════════════════════════════════════
// AZURE COMPUTER VISION OCR - EXEMPLOS DE USO
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Threading.Tasks;
using LabelWise.Application.DTOs;
using LabelWise.Application.Interfaces;
using LabelWise.Infrastructure.Ocr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LabelWise.Examples
{
    /// <summary>
    /// Exemplos de uso dos diferentes providers de OCR.
    /// </summary>
    public class AzureOcrExamples
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // EXEMPLO 1: Usar Azure Computer Vision Diretamente
        // ═══════════════════════════════════════════════════════════════════════════
        public static async Task Example1_AzureOcrDirect()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 1: Azure Computer Vision (Direct)");
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");

            // Configurar provider Azure
            var endpoint = "https://labelwise-ocr-cv.cognitiveservices.azure.com/";
            var apiKey = "1a2b3c4d5e6f7g8h9i0j1k2l3m4n5o6p";

            var logger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<AzureComputerVisionOcrProvider>();

            var azureProvider = new AzureComputerVisionOcrProvider(
                endpoint,
                apiKey,
                logger);

            // Verificar disponibilidade
            var isAvailable = await azureProvider.IsAvailableAsync();
            Console.WriteLine($"Provider disponível: {isAvailable}");
            Console.WriteLine($"Provider name: {azureProvider.ProviderName}\n");

            // Processar imagem
            var imagePath = @"C:\temp\rotulo-produto.jpg";
            var request = new OcrRequestDto
            {
                ImagePath = imagePath,
                FileName = Path.GetFileName(imagePath)
            };

            Console.WriteLine($"Processando: {request.FileName}...\n");

            var result = await azureProvider.ExtractTextAsync(request);

            if (result.Success)
            {
                Console.WriteLine("✅ OCR Concluído com Sucesso!");
                Console.WriteLine($"📊 Confiança: {result.Confidence:P}");
                Console.WriteLine($"📝 Caracteres extraídos: {result.RawText.Length}");
                Console.WriteLine($"📦 Blocos de texto: {result.TextBlocks.Count}");
                Console.WriteLine();
                Console.WriteLine("📄 Texto extraído:");
                Console.WriteLine("─────────────────────────────────────────────────────────");
                Console.WriteLine(result.RawText);
                Console.WriteLine("─────────────────────────────────────────────────────────");
            }
            else
            {
                Console.WriteLine($"❌ Erro: {result.ErrorMessage}");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // EXEMPLO 2: Usar Composite Provider (Azure + Tesseract)
        // ═══════════════════════════════════════════════════════════════════════════
        public static async Task Example2_CompositeProvider()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 2: Composite Provider (Azure + Tesseract)");
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");

            // Configurar Azure provider
            var azureEndpoint = "https://labelwise-ocr-cv.cognitiveservices.azure.com/";
            var azureApiKey = "1a2b3c4d5e6f7g8h9i0j1k2l3m4n5o6p";

            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

            var azureLogger = loggerFactory.CreateLogger<AzureComputerVisionOcrProvider>();
            var azureProvider = new AzureComputerVisionOcrProvider(
                azureEndpoint,
                azureApiKey,
                azureLogger);

            // Configurar Tesseract provider (fallback)
            var tesseractLogger = loggerFactory.CreateLogger<TesseractOcrProvider>();
            var tesseractProvider = new TesseractOcrProvider(
                tesseractLogger,
                tessdataPath: null,  // Auto-detect
                language: "por+eng");

            // Configurar Composite provider
            var compositeLogger = loggerFactory.CreateLogger<CompositeOcrProvider>();
            var compositeProvider = new CompositeOcrProvider(
                primaryProvider: azureProvider,
                fallbackProvider: tesseractProvider,
                confidenceThreshold: 0.85,
                logger: compositeLogger);

            // Processar imagem
            var imagePath = @"C:\temp\rotulo-baixa-qualidade.jpg";
            var request = new OcrRequestDto
            {
                ImagePath = imagePath,
                FileName = Path.GetFileName(imagePath)
            };

            Console.WriteLine($"Processando: {request.FileName}...\n");

            var result = await compositeProvider.ExtractTextAsync(request);

            if (result.Success)
            {
                Console.WriteLine("✅ OCR Concluído com Sucesso!");
                Console.WriteLine($"📊 Confiança: {result.Confidence:P}");
                Console.WriteLine($"📝 Caracteres extraídos: {result.RawText.Length}");
                Console.WriteLine();
                Console.WriteLine("🔍 Metadata do Provider:");
                Console.WriteLine("─────────────────────────────────────────────────────────");
                foreach (var kvp in result.ProviderMetadata!)
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
                Console.WriteLine("─────────────────────────────────────────────────────────");
                Console.WriteLine();
                Console.WriteLine("📄 Texto extraído:");
                Console.WriteLine("─────────────────────────────────────────────────────────");
                Console.WriteLine(result.RawText);
                Console.WriteLine("─────────────────────────────────────────────────────────");
            }
            else
            {
                Console.WriteLine($"❌ Erro: {result.ErrorMessage}");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // EXEMPLO 3: Injeção de Dependência com ASP.NET Core
        // ═══════════════════════════════════════════════════════════════════════════
        public class MeuController
        {
            private readonly IOcrProvider _ocrProvider;
            private readonly ILogger<MeuController> _logger;

            public MeuController(IOcrProvider ocrProvider, ILogger<MeuController> logger)
            {
                _ocrProvider = ocrProvider;
                _logger = logger;
            }

            public async Task<string> ProcessarImagem(string caminhoImagem)
            {
                _logger.LogInformation("Processando imagem com OCR: {Path}", caminhoImagem);

                // O provider injetado pode ser Azure, Tesseract ou Composite
                // dependendo da configuração no appsettings.json
                var request = new OcrRequestDto
                {
                    ImagePath = caminhoImagem,
                    FileName = Path.GetFileName(caminhoImagem)
                };

                var result = await _ocrProvider.ExtractTextAsync(request);

                if (!result.Success)
                {
                    _logger.LogError("OCR falhou: {Error}", result.ErrorMessage);
                    throw new Exception($"Falha no OCR: {result.ErrorMessage}");
                }

                _logger.LogInformation(
                    "OCR concluído. Provider: {Provider}, Confiança: {Confidence:P}",
                    result.ProviderMetadata?["UsedProvider"] ?? _ocrProvider.ProviderName,
                    result.Confidence);

                return result.RawText;
            }

            public async Task<object> ObterInformacoesProvider()
            {
                var metadata = _ocrProvider.GetMetadata();
                var isAvailable = await _ocrProvider.IsAvailableAsync();

                return new
                {
                    ProviderName = _ocrProvider.ProviderName,
                    IsAvailable = isAvailable,
                    Metadata = metadata
                };
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // EXEMPLO 4: Comparar Resultados de Múltiplos Providers
        // ═══════════════════════════════════════════════════════════════════════════
        public static async Task Example4_CompareProviders()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 4: Comparar Azure vs Tesseract");
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");

            var imagePath = @"C:\temp\rotulo-teste.jpg";
            var request = new OcrRequestDto
            {
                ImagePath = imagePath,
                FileName = Path.GetFileName(imagePath)
            };

            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

            // ─────────────────────────────────────────────────────────────────────
            // 1. Processar com Azure
            // ─────────────────────────────────────────────────────────────────────
            Console.WriteLine("🔵 Processando com Azure Computer Vision...\n");

            var azureLogger = loggerFactory.CreateLogger<AzureComputerVisionOcrProvider>();
            var azureProvider = new AzureComputerVisionOcrProvider(
                "https://labelwise-ocr-cv.cognitiveservices.azure.com/",
                "1a2b3c4d5e6f7g8h9i0j1k2l3m4n5o6p",
                azureLogger);

            var azureResult = await azureProvider.ExtractTextAsync(request);

            Console.WriteLine($"✅ Azure Concluído");
            Console.WriteLine($"   Confiança: {azureResult.Confidence:P}");
            Console.WriteLine($"   Caracteres: {azureResult.RawText.Length}");
            Console.WriteLine();

            // ─────────────────────────────────────────────────────────────────────
            // 2. Processar com Tesseract
            // ─────────────────────────────────────────────────────────────────────
            Console.WriteLine("🟢 Processando com Tesseract...\n");

            var tesseractLogger = loggerFactory.CreateLogger<TesseractOcrProvider>();
            var tesseractProvider = new TesseractOcrProvider(
                tesseractLogger,
                tessdataPath: null,
                language: "por+eng");

            var tesseractResult = await tesseractProvider.ExtractTextAsync(request);

            Console.WriteLine($"✅ Tesseract Concluído");
            Console.WriteLine($"   Confiança: {tesseractResult.Confidence:P}");
            Console.WriteLine($"   Caracteres: {tesseractResult.RawText.Length}");
            Console.WriteLine();

            // ─────────────────────────────────────────────────────────────────────
            // 3. Comparar Resultados
            // ─────────────────────────────────────────────────────────────────────
            Console.WriteLine("📊 COMPARAÇÃO DE RESULTADOS");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine($"Azure Confidence:      {azureResult.Confidence:P}");
            Console.WriteLine($"Tesseract Confidence:  {tesseractResult.Confidence:P}");
            Console.WriteLine();
            Console.WriteLine($"Azure Text Length:     {azureResult.RawText.Length} chars");
            Console.WriteLine($"Tesseract Text Length: {tesseractResult.RawText.Length} chars");
            Console.WriteLine();

            var azureScore = CalculateScore(azureResult);
            var tesseractScore = CalculateScore(tesseractResult);

            Console.WriteLine($"Azure Score:           {azureScore:F2}");
            Console.WriteLine($"Tesseract Score:       {tesseractScore:F2}");
            Console.WriteLine();

            if (azureScore > tesseractScore)
            {
                Console.WriteLine("🏆 Azure Computer Vision venceu!");
            }
            else if (tesseractScore > azureScore)
            {
                Console.WriteLine("🏆 Tesseract venceu!");
            }
            else
            {
                Console.WriteLine("🤝 Empate!");
            }

            Console.WriteLine("═══════════════════════════════════════════════════════════");

            // Função auxiliar de score
            static double CalculateScore(OcrResultDto result)
            {
                if (!result.Success || string.IsNullOrWhiteSpace(result.RawText))
                {
                    return 0;
                }

                var confidenceScore = result.Confidence * 0.7;
                var textScore = Math.Min(result.RawText.Length / 500.0, 1.0) * 0.3;
                return confidenceScore + textScore;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // EXEMPLO 5: Processar Batch de Imagens
        // ═══════════════════════════════════════════════════════════════════════════
        public static async Task Example5_BatchProcessing()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 5: Processar Batch de Imagens");
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");

            var imagens = new[]
            {
                @"C:\temp\rotulo1.jpg",
                @"C:\temp\rotulo2.jpg",
                @"C:\temp\rotulo3.jpg",
                @"C:\temp\rotulo4.jpg",
                @"C:\temp\rotulo5.jpg"
            };

            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

            // Usar Composite para máxima precisão
            var azureProvider = new AzureComputerVisionOcrProvider(
                "https://labelwise-ocr-cv.cognitiveservices.azure.com/",
                "1a2b3c4d5e6f7g8h9i0j1k2l3m4n5o6p",
                loggerFactory.CreateLogger<AzureComputerVisionOcrProvider>());

            var tesseractProvider = new TesseractOcrProvider(
                loggerFactory.CreateLogger<TesseractOcrProvider>(),
                tessdataPath: null,
                language: "por+eng");

            var compositeProvider = new CompositeOcrProvider(
                azureProvider,
                tesseractProvider,
                confidenceThreshold: 0.85,
                loggerFactory.CreateLogger<CompositeOcrProvider>());

            // Estatísticas
            var totalImages = 0;
            var successCount = 0;
            var failureCount = 0;
            var azureUsedCount = 0;
            var tesseractUsedCount = 0;
            var totalConfidence = 0.0;

            Console.WriteLine($"Processando {imagens.Length} imagens...\n");

            foreach (var imagePath in imagens)
            {
                if (!File.Exists(imagePath))
                {
                    Console.WriteLine($"⚠️  Arquivo não encontrado: {Path.GetFileName(imagePath)}");
                    continue;
                }

                totalImages++;
                var fileName = Path.GetFileName(imagePath);

                Console.WriteLine($"[{totalImages}] Processando: {fileName}...");

                var request = new OcrRequestDto
                {
                    ImagePath = imagePath,
                    FileName = fileName
                };

                var result = await compositeProvider.ExtractTextAsync(request);

                if (result.Success)
                {
                    successCount++;
                    totalConfidence += result.Confidence;

                    var usedProvider = result.ProviderMetadata?["UsedProvider"] ?? "Unknown";
                    if (usedProvider.Contains("Azure"))
                    {
                        azureUsedCount++;
                    }
                    else if (usedProvider.Contains("Tesseract"))
                    {
                        tesseractUsedCount++;
                    }

                    Console.WriteLine($"    ✅ Sucesso - Confiança: {result.Confidence:P} - Provider: {usedProvider}");
                }
                else
                {
                    failureCount++;
                    Console.WriteLine($"    ❌ Falha: {result.ErrorMessage}");
                }

                Console.WriteLine();
            }

            // Relatório final
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("📊 RELATÓRIO FINAL");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine($"Total de imagens:         {totalImages}");
            Console.WriteLine($"Sucessos:                 {successCount} ({successCount * 100.0 / totalImages:F1}%)");
            Console.WriteLine($"Falhas:                   {failureCount} ({failureCount * 100.0 / totalImages:F1}%)");
            Console.WriteLine($"Confiança média:          {(successCount > 0 ? totalConfidence / successCount : 0):P}");
            Console.WriteLine();
            Console.WriteLine($"Azure usado:              {azureUsedCount} vezes");
            Console.WriteLine($"Tesseract usado:          {tesseractUsedCount} vezes");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // EXEMPLO 6: Lidar com Erros e Retry
        // ═══════════════════════════════════════════════════════════════════════════
        public static async Task Example6_ErrorHandlingAndRetry()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 6: Error Handling e Retry");
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");

            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

            var azureProvider = new AzureComputerVisionOcrProvider(
                "https://labelwise-ocr-cv.cognitiveservices.azure.com/",
                "1a2b3c4d5e6f7g8h9i0j1k2l3m4n5o6p",
                loggerFactory.CreateLogger<AzureComputerVisionOcrProvider>());

            var imagePath = @"C:\temp\rotulo-produto.jpg";
            var request = new OcrRequestDto
            {
                ImagePath = imagePath,
                FileName = Path.GetFileName(imagePath)
            };

            const int maxRetries = 3;
            var retryCount = 0;
            OcrResultDto? result = null;

            while (retryCount < maxRetries)
            {
                try
                {
                    Console.WriteLine($"Tentativa {retryCount + 1}/{maxRetries}...");

                    result = await azureProvider.ExtractTextAsync(request);

                    if (result.Success)
                    {
                        Console.WriteLine($"✅ Sucesso na tentativa {retryCount + 1}!");
                        break;
                    }
                    else
                    {
                        Console.WriteLine($"⚠️  Erro: {result.ErrorMessage}");
                        retryCount++;

                        if (retryCount < maxRetries)
                        {
                            var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount)); // Exponential backoff
                            Console.WriteLine($"Aguardando {delay.TotalSeconds}s antes de tentar novamente...\n");
                            await Task.Delay(delay);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Exception: {ex.Message}");
                    retryCount++;

                    if (retryCount < maxRetries)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
                        Console.WriteLine($"Aguardando {delay.TotalSeconds}s antes de tentar novamente...\n");
                        await Task.Delay(delay);
                    }
                }
            }

            if (result?.Success == true)
            {
                Console.WriteLine("\n✅ Processamento concluído com sucesso!");
                Console.WriteLine($"📊 Confiança: {result.Confidence:P}");
                Console.WriteLine($"📝 Caracteres: {result.RawText.Length}");
            }
            else
            {
                Console.WriteLine($"\n❌ Falha após {maxRetries} tentativas.");
            }
        }
    }
}
