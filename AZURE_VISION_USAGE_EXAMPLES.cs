// ═══════════════════════════════════════════════════════════════════════════
// AZURE VISION READ OCR - EXEMPLOS DE USO
// ═══════════════════════════════════════════════════════════════════════════
// 
// Este arquivo contém exemplos práticos de como usar os diferentes providers
// de OCR implementados no projeto LabelWise.
//
// ═══════════════════════════════════════════════════════════════════════════

using LabelWise.Application.DTOs;
using LabelWise.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading.Tasks;

namespace LabelWise.Examples
{
    /// <summary>
    /// Exemplos de uso dos providers de OCR.
    /// </summary>
    public class OcrProviderExamples
    {
        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 1: USO BÁSICO VIA DEPENDENCY INJECTION
        // ═══════════════════════════════════════════════════════════════════════

        public class ProductAnalysisService
        {
            private readonly IOcrProvider _ocrProvider;

            public ProductAnalysisService(IOcrProvider ocrProvider)
            {
                _ocrProvider = ocrProvider;
            }

            public async Task<string> AnalyzeProductLabel(string imagePath)
            {
                // Criar request
                var request = new OcrRequestDto
                {
                    ImagePath = imagePath,
                    FileName = Path.GetFileName(imagePath),
                    ContentType = "image/jpeg"
                };

                // Executar OCR (provider é resolvido automaticamente pela DI)
                var result = await _ocrProvider.ExtractTextAsync(request);

                if (result.Success)
                {
                    Console.WriteLine($"✅ OCR concluído!");
                    Console.WriteLine($"Provider: {result.ProviderMetadata?["ProviderName"]}");
                    Console.WriteLine($"Confiança: {result.Confidence:P2}");
                    Console.WriteLine($"Texto extraído ({result.RawText.Length} chars):");
                    Console.WriteLine(result.RawText);

                    return result.RawText;
                }
                else
                {
                    Console.WriteLine($"❌ Erro no OCR: {result.ErrorMessage}");
                    return string.Empty;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 2: VERIFICAR QUAL PROVIDER FOI USADO (SELECTOR)
        // ═══════════════════════════════════════════════════════════════════════

        public async Task CheckWhichProviderWasUsed(IOcrProvider ocrProvider, string imagePath)
        {
            var request = new OcrRequestDto
            {
                ImagePath = imagePath,
                FileName = Path.GetFileName(imagePath)
            };

            var result = await ocrProvider.ExtractTextAsync(request);

            if (result.Success && result.ProviderMetadata != null)
            {
                // Verificar se usou Selector
                if (result.ProviderMetadata.ContainsKey("SelectorUsed"))
                {
                    var selectedProvider = result.ProviderMetadata["SelectedProvider"];
                    var selectionReason = result.ProviderMetadata["SelectionReason"];
                    var tesseractExecuted = result.ProviderMetadata["TesseractExecuted"];
                    var azureExecuted = result.ProviderMetadata["AzureExecuted"];

                    Console.WriteLine("═══════════════════════════════════════════════════════");
                    Console.WriteLine("SMART SELECTOR - RESULTADO");
                    Console.WriteLine("═══════════════════════════════════════════════════════");
                    Console.WriteLine($"Provider selecionado: {selectedProvider}");
                    Console.WriteLine($"Razão: {selectionReason}");
                    Console.WriteLine($"Tesseract executado: {tesseractExecuted}");
                    Console.WriteLine($"Azure executado: {azureExecuted}");

                    if (result.ProviderMetadata.ContainsKey("TesseractConfidence"))
                    {
                        Console.WriteLine($"Confiança Tesseract: {result.ProviderMetadata["TesseractConfidence"]}");
                    }

                    if (result.ProviderMetadata.ContainsKey("AzureConfidence"))
                    {
                        Console.WriteLine($"Confiança Azure: {result.ProviderMetadata["AzureConfidence"]}");
                    }

                    Console.WriteLine("═══════════════════════════════════════════════════════");
                }
                else
                {
                    // Provider standalone (não é selector)
                    var providerName = result.ProviderMetadata.GetValueOrDefault("ProviderName", "Unknown");
                    Console.WriteLine($"Provider usado: {providerName}");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 3: PROCESSAR MÚLTIPLAS IMAGENS COM ESTATÍSTICAS
        // ═══════════════════════════════════════════════════════════════════════

        public async Task ProcessMultipleImages(IOcrProvider ocrProvider, string[] imagePaths)
        {
            int totalImages = imagePaths.Length;
            int successCount = 0;
            int tesseractCount = 0;
            int azureCount = 0;
            double totalConfidence = 0;
            var startTime = DateTime.UtcNow;

            Console.WriteLine($"Processando {totalImages} imagens...\n");

            foreach (var imagePath in imagePaths)
            {
                Console.WriteLine($"📸 Processando: {Path.GetFileName(imagePath)}");

                var request = new OcrRequestDto
                {
                    ImagePath = imagePath,
                    FileName = Path.GetFileName(imagePath)
                };

                var result = await ocrProvider.ExtractTextAsync(request);

                if (result.Success)
                {
                    successCount++;
                    totalConfidence += result.Confidence;

                    // Contar qual provider foi usado
                    if (result.ProviderMetadata != null)
                    {
                        var selectedProvider = result.ProviderMetadata.GetValueOrDefault("SelectedProvider", "Unknown");

                        if (selectedProvider.Contains("Tesseract"))
                        {
                            tesseractCount++;
                            Console.WriteLine($"   ✅ Tesseract | Confiança: {result.Confidence:P2} | {result.RawText.Length} chars");
                        }
                        else if (selectedProvider.Contains("Azure"))
                        {
                            azureCount++;
                            Console.WriteLine($"   ✅ Azure Vision | Confiança: {result.Confidence:P2} | {result.RawText.Length} chars");
                        }
                        else
                        {
                            Console.WriteLine($"   ✅ {selectedProvider} | Confiança: {result.Confidence:P2}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"   ❌ Erro: {result.ErrorMessage}");
                }

                Console.WriteLine();
            }

            var totalTime = (DateTime.UtcNow - startTime).TotalSeconds;
            var avgConfidence = successCount > 0 ? totalConfidence / successCount : 0;

            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine("ESTATÍSTICAS FINAIS");
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine($"Total de imagens: {totalImages}");
            Console.WriteLine($"Sucesso: {successCount} ({(double)successCount / totalImages:P0})");
            Console.WriteLine($"Falhas: {totalImages - successCount}");
            Console.WriteLine($"Confiança média: {avgConfidence:P2}");
            Console.WriteLine($"Tempo total: {totalTime:F2}s");
            Console.WriteLine($"Tempo médio/imagem: {totalTime / totalImages:F2}s");
            Console.WriteLine();
            Console.WriteLine("USAGE POR PROVIDER:");
            Console.WriteLine($"   Tesseract (grátis): {tesseractCount} imagens ({(double)tesseractCount / totalImages:P0})");
            Console.WriteLine($"   Azure Vision (pago): {azureCount} imagens ({(double)azureCount / totalImages:P0})");
            Console.WriteLine();
            Console.WriteLine("CUSTO ESTIMADO:");
            Console.WriteLine($"   Azure: {azureCount} transações × $0.001 = ${azureCount * 0.001:F4}");
            Console.WriteLine("═══════════════════════════════════════════════════════");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 4: ANÁLISE DETALHADA DE BLOCOS DE TEXTO
        // ═══════════════════════════════════════════════════════════════════════

        public async Task AnalyzeTextBlocks(IOcrProvider ocrProvider, string imagePath)
        {
            var request = new OcrRequestDto
            {
                ImagePath = imagePath,
                FileName = Path.GetFileName(imagePath)
            };

            var result = await ocrProvider.ExtractTextAsync(request);

            if (result.Success && result.TextBlocks != null && result.TextBlocks.Count > 0)
            {
                Console.WriteLine("═══════════════════════════════════════════════════════");
                Console.WriteLine("BLOCOS DE TEXTO DETECTADOS");
                Console.WriteLine("═══════════════════════════════════════════════════════");

                for (int i = 0; i < result.TextBlocks.Count; i++)
                {
                    var block = result.TextBlocks[i];

                    Console.WriteLine($"\nBloco {i + 1}:");
                    Console.WriteLine($"   Tipo: {block.BlockType}");
                    Console.WriteLine($"   Confiança: {block.Confidence:P2}");
                    Console.WriteLine($"   Texto: {block.Text}");

                    if (block.BoundingBox != null)
                    {
                        Console.WriteLine($"   Posição: ({block.BoundingBox.Left:F0}, {block.BoundingBox.Top:F0})");
                        Console.WriteLine($"   Tamanho: {block.BoundingBox.Width:F0} × {block.BoundingBox.Height:F0}");
                    }
                }

                Console.WriteLine("\n═══════════════════════════════════════════════════════");

                // Agrupar por tipo de bloco
                var headings = result.TextBlocks.Where(b => b.BlockType == "HEADING").ToList();
                var subheadings = result.TextBlocks.Where(b => b.BlockType == "SUBHEADING").ToList();
                var textBlocks = result.TextBlocks.Where(b => b.BlockType == "TEXT").ToList();

                Console.WriteLine("\nRESUMO POR TIPO:");
                Console.WriteLine($"   Headings: {headings.Count}");
                Console.WriteLine($"   Subheadings: {subheadings.Count}");
                Console.WriteLine($"   Text: {textBlocks.Count}");
                Console.WriteLine("═══════════════════════════════════════════════════════");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 5: COMPARAR RESULTADOS TESSERACT VS AZURE
        // ═══════════════════════════════════════════════════════════════════════

        public async Task CompareTesseractVsAzure(
            IServiceProvider serviceProvider,
            string imagePath)
        {
            // Criar providers manualmente para comparação
            var tesseractLogger = serviceProvider.GetService<ILogger<TesseractOcrProvider>>();
            var tesseractProvider = new TesseractOcrProvider(tesseractLogger);

            var azureLogger = serviceProvider.GetService<ILogger<AzureVisionReadOcrProvider>>();
            var azureProvider = new AzureVisionReadOcrProvider(
                "https://your-resource.cognitiveservices.azure.com/",
                "your-api-key",
                "pt",
                30,
                false,
                azureLogger);

            var request = new OcrRequestDto
            {
                ImagePath = imagePath,
                FileName = Path.GetFileName(imagePath)
            };

            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine("COMPARAÇÃO: TESSERACT vs AZURE VISION");
            Console.WriteLine("═══════════════════════════════════════════════════════");

            // Executar Tesseract
            Console.WriteLine("\n🔧 Executando Tesseract...");
            var tesseractStart = DateTime.UtcNow;
            var tesseractResult = await tesseractProvider.ExtractTextAsync(request);
            var tesseractTime = (DateTime.UtcNow - tesseractStart).TotalSeconds;

            // Executar Azure
            Console.WriteLine("☁️  Executando Azure Vision...");
            var azureStart = DateTime.UtcNow;
            var azureResult = await azureProvider.ExtractTextAsync(request);
            var azureTime = (DateTime.UtcNow - azureStart).TotalSeconds;

            // Comparar resultados
            Console.WriteLine("\n═══════════════════════════════════════════════════════");
            Console.WriteLine("RESULTADOS:");
            Console.WriteLine("═══════════════════════════════════════════════════════");

            Console.WriteLine("\nTESSERACT:");
            Console.WriteLine($"   Success: {tesseractResult.Success}");
            Console.WriteLine($"   Confiança: {tesseractResult.Confidence:P2}");
            Console.WriteLine($"   Caracteres: {tesseractResult.RawText.Length}");
            Console.WriteLine($"   Tempo: {tesseractTime:F2}s");
            Console.WriteLine($"   Custo: $0.00 (local)");

            Console.WriteLine("\nAZURE VISION:");
            Console.WriteLine($"   Success: {azureResult.Success}");
            Console.WriteLine($"   Confiança: {azureResult.Confidence:P2}");
            Console.WriteLine($"   Caracteres: {azureResult.RawText.Length}");
            Console.WriteLine($"   Tempo: {azureTime:F2}s");
            Console.WriteLine($"   Custo: ~$0.001");

            Console.WriteLine("\nDIFERENÇAS:");
            Console.WriteLine($"   Δ Confiança: {(azureResult.Confidence - tesseractResult.Confidence):+0.00;-0.00}");
            Console.WriteLine($"   Δ Caracteres: {azureResult.RawText.Length - tesseractResult.RawText.Length:+0;-0}");
            Console.WriteLine($"   Δ Tempo: {(azureTime - tesseractTime):+0.00;-0.00}s");

            // Similaridade de texto (simplificado)
            var similarity = CalculateSimilarity(tesseractResult.RawText, azureResult.RawText);
            Console.WriteLine($"   Similaridade: {similarity:P2}");

            Console.WriteLine("═══════════════════════════════════════════════════════");
        }

        private double CalculateSimilarity(string text1, string text2)
        {
            if (string.IsNullOrEmpty(text1) && string.IsNullOrEmpty(text2))
                return 1.0;

            if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2))
                return 0.0;

            // Levenshtein distance simplificado
            var longer = text1.Length > text2.Length ? text1 : text2;
            var shorter = text1.Length > text2.Length ? text2 : text1;

            if (longer.Length == 0)
                return 1.0;

            // Simplificação: comparar primeiros 100 chars
            var sample1 = text1.Length > 100 ? text1.Substring(0, 100) : text1;
            var sample2 = text2.Length > 100 ? text2.Substring(0, 100) : text2;

            var commonChars = sample1.Intersect(sample2).Count();
            return (double)commonChars / Math.Max(sample1.Length, sample2.Length);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EXEMPLO 6: CONFIGURAÇÃO PROGRAMÁTICA (SEM appsettings.json)
        // ═══════════════════════════════════════════════════════════════════════

        public static void ConfigureOcrProgrammatically(IServiceCollection services)
        {
            // Opção 1: Apenas Tesseract
            services.AddSingleton<IOcrProvider>(sp =>
            {
                var logger = sp.GetService<ILogger<TesseractOcrProvider>>();
                return new TesseractOcrProvider(logger, tessdataPath: null, language: "por+eng");
            });

            // Opção 2: Apenas Azure Vision
            services.AddSingleton<IOcrProvider>(sp =>
            {
                var logger = sp.GetService<ILogger<AzureVisionReadOcrProvider>>();
                return new AzureVisionReadOcrProvider(
                    endpoint: "https://your-resource.cognitiveservices.azure.com/",
                    apiKey: "your-api-key",
                    language: "pt",
                    timeoutSeconds: 30,
                    enableDetailedLogging: false,
                    logger: logger);
            });

            // Opção 3: Selector (Tesseract → Azure fallback)
            services.AddSingleton<IOcrProvider>(sp =>
            {
                var tesseractLogger = sp.GetService<ILogger<TesseractOcrProvider>>();
                var tesseractProvider = new TesseractOcrProvider(tesseractLogger);

                var azureLogger = sp.GetService<ILogger<AzureVisionReadOcrProvider>>();
                var azureProvider = new AzureVisionReadOcrProvider(
                    "https://your-resource.cognitiveservices.azure.com/",
                    "your-api-key",
                    "pt",
                    30,
                    false,
                    azureLogger);

                var selectorLogger = sp.GetService<ILogger<OcrProviderSelector>>();
                return new OcrProviderSelector(
                    tesseractProvider,
                    azureProvider,
                    confidenceThreshold: 0.85,
                    selectorLogger);
            });
        }
    }
}
