// ========================================
// EXEMPLO DE USO: TesseractOcrProvider
// LabelWise - Extração de Texto OCR
// ========================================

using LabelWise.Application.DTOs;
using LabelWise.Application.Interfaces;
using LabelWise.Infrastructure.Ocr;
using Microsoft.Extensions.Logging;

namespace LabelWise.Examples
{
    /// <summary>
    /// Exemplos de uso do TesseractOcrProvider para extração de texto de imagens.
    /// </summary>
    public class TesseractUsageExamples
    {
        // ========================================
        // EXEMPLO 1: Uso Básico
        // ========================================
        
        public static async Task BasicUsageExample()
        {
            Console.WriteLine("=== EXEMPLO 1: Uso Básico ===\n");

            // Criar provider (usa configuração padrão: ./tessdata, por+eng)
            var ocrProvider = new TesseractOcrProvider();

            // Preparar requisição
            var request = new OcrRequestDto
            {
                ImagePath = @"C:\temp\rotulo-biscoito.jpg",
                FileName = "rotulo-biscoito.jpg",
                ContentType = "image/jpeg"
            };

            // Extrair texto
            var result = await ocrProvider.ExtractTextAsync(request);

            // Verificar resultado
            if (result.Success)
            {
                Console.WriteLine($"✓ OCR bem-sucedido!");
                Console.WriteLine($"  Confiança: {result.Confidence * 100:F2}%");
                Console.WriteLine($"  Texto extraído ({result.RawText.Length} caracteres):");
                Console.WriteLine($"  ─────────────────────────────────────");
                Console.WriteLine(result.RawText);
                Console.WriteLine($"  ─────────────────────────────────────");
                Console.WriteLine($"  Blocos detectados: {result.TextBlocks.Count}");
            }
            else
            {
                Console.WriteLine($"✗ Erro no OCR: {result.ErrorMessage}");
            }
        }

        // ========================================
        // EXEMPLO 2: Uso com Logging
        // ========================================
        
        public static async Task UsageWithLoggingExample()
        {
            Console.WriteLine("=== EXEMPLO 2: Uso com Logging ===\n");

            // Criar logger factory
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Debug);
            });

            var logger = loggerFactory.CreateLogger<TesseractOcrProvider>();

            // Criar provider com logger
            var ocrProvider = new TesseractOcrProvider(logger);

            // Verificar disponibilidade
            var isAvailable = await ocrProvider.IsAvailableAsync();
            Console.WriteLine($"OCR disponível: {isAvailable}\n");

            if (!isAvailable)
            {
                Console.WriteLine("Tessdata não configurado. Execute setup-tesseract.ps1");
                return;
            }

            // Extrair texto
            var request = new OcrRequestDto
            {
                ImagePath = @"C:\temp\rotulo-iogurte.png",
                FileName = "rotulo-iogurte.png",
                ContentType = "image/png"
            };

            var result = await ocrProvider.ExtractTextAsync(request);
            Console.WriteLine($"\nResultado: {(result.Success ? "✓ Sucesso" : "✗ Falha")}");
        }

        // ========================================
        // EXEMPLO 3: Configuração Customizada
        // ========================================
        
        public static async Task CustomConfigurationExample()
        {
            Console.WriteLine("=== EXEMPLO 3: Configuração Customizada ===\n");

            // Caminho customizado para tessdata
            var customTessdataPath = @"C:\my-custom-tessdata";
            
            // Idioma customizado (apenas português, sem fallback)
            var customLanguage = "por";

            var ocrProvider = new TesseractOcrProvider(
                logger: null,
                tessdataPath: customTessdataPath,
                language: customLanguage
            );

            Console.WriteLine($"Provider: {ocrProvider.ProviderName}");
            Console.WriteLine($"Tessdata: {customTessdataPath}");
            Console.WriteLine($"Idioma: {customLanguage}");
        }

        // ========================================
        // EXEMPLO 4: Processar Múltiplas Imagens
        // ========================================
        
        public static async Task ProcessMultipleImagesExample()
        {
            Console.WriteLine("=== EXEMPLO 4: Processar Múltiplas Imagens ===\n");

            var ocrProvider = new TesseractOcrProvider();

            var imagePaths = new[]
            {
                @"C:\temp\rotulo1.jpg",
                @"C:\temp\rotulo2.jpg",
                @"C:\temp\rotulo3.jpg"
            };

            foreach (var imagePath in imagePaths)
            {
                if (!File.Exists(imagePath))
                {
                    Console.WriteLine($"⚠ Imagem não encontrada: {imagePath}");
                    continue;
                }

                var request = new OcrRequestDto
                {
                    ImagePath = imagePath,
                    FileName = Path.GetFileName(imagePath),
                    ContentType = "image/jpeg"
                };

                var result = await ocrProvider.ExtractTextAsync(request);

                Console.WriteLine($"Imagem: {Path.GetFileName(imagePath)}");
                Console.WriteLine($"  Status: {(result.Success ? "✓" : "✗")}");
                Console.WriteLine($"  Confiança: {result.Confidence * 100:F2}%");
                Console.WriteLine($"  Texto: {result.RawText.Length} caracteres");
                Console.WriteLine();
            }
        }

        // ========================================
        // EXEMPLO 5: Analisar Blocos de Texto
        // ========================================
        
        public static async Task AnalyzeTextBlocksExample()
        {
            Console.WriteLine("=== EXEMPLO 5: Analisar Blocos de Texto ===\n");

            var ocrProvider = new TesseractOcrProvider();

            var request = new OcrRequestDto
            {
                ImagePath = @"C:\temp\rotulo-completo.jpg",
                FileName = "rotulo-completo.jpg",
                ContentType = "image/jpeg"
            };

            var result = await ocrProvider.ExtractTextAsync(request);

            if (!result.Success)
            {
                Console.WriteLine($"Erro: {result.ErrorMessage}");
                return;
            }

            Console.WriteLine($"Total de blocos detectados: {result.TextBlocks.Count}\n");

            // Agrupar por tipo de bloco
            var headings = result.TextBlocks.Where(b => b.BlockType == "HEADING").ToList();
            var subheadings = result.TextBlocks.Where(b => b.BlockType == "SUBHEADING").ToList();
            var text = result.TextBlocks.Where(b => b.BlockType == "TEXT").ToList();

            Console.WriteLine($"Cabeçalhos (HEADING): {headings.Count}");
            foreach (var heading in headings)
            {
                Console.WriteLine($"  • {heading.Text} (confiança: {heading.Confidence * 100:F1}%)");
            }

            Console.WriteLine($"\nSubcabeçalhos (SUBHEADING): {subheadings.Count}");
            foreach (var subheading in subheadings)
            {
                Console.WriteLine($"  • {subheading.Text}");
            }

            Console.WriteLine($"\nTexto normal: {text.Count}");
        }

        // ========================================
        // EXEMPLO 6: Tratamento de Erros
        // ========================================
        
        public static async Task ErrorHandlingExample()
        {
            Console.WriteLine("=== EXEMPLO 6: Tratamento de Erros ===\n");

            var ocrProvider = new TesseractOcrProvider();

            // Cenário 1: Arquivo não existe
            var request1 = new OcrRequestDto
            {
                ImagePath = @"C:\temp\arquivo-inexistente.jpg",
                FileName = "arquivo-inexistente.jpg"
            };

            var result1 = await ocrProvider.ExtractTextAsync(request1);
            Console.WriteLine($"Teste 1 - Arquivo inexistente:");
            Console.WriteLine($"  Success: {result1.Success}");
            Console.WriteLine($"  Error: {result1.ErrorMessage}\n");

            // Cenário 2: Tessdata não configurado
            var ocrProviderSemTessdata = new TesseractOcrProvider(
                tessdataPath: @"C:\pasta-inexistente"
            );

            var isAvailable = await ocrProviderSemTessdata.IsAvailableAsync();
            Console.WriteLine($"Teste 2 - Tessdata inexistente:");
            Console.WriteLine($"  Available: {isAvailable}");

            var request2 = new OcrRequestDto
            {
                ImagePath = @"C:\temp\imagem-valida.jpg",
                FileName = "imagem-valida.jpg"
            };

            var result2 = await ocrProviderSemTessdata.ExtractTextAsync(request2);
            Console.WriteLine($"  Success: {result2.Success}");
            Console.WriteLine($"  Error: {result2.ErrorMessage}\n");
        }

        // ========================================
        // EXEMPLO 7: Integração com Pipeline
        // ========================================
        
        public static async Task PipelineIntegrationExample()
        {
            Console.WriteLine("=== EXEMPLO 7: Integração com Pipeline ===\n");

            // Simula o uso dentro do ProductAnalysisPipelineOrchestrator
            
            var ocrProvider = new TesseractOcrProvider();

            // 1. Upload já foi feito (imagePath disponível)
            var imagePath = @"C:\temp\uploads\produto-123.jpg";

            // 2. Executar OCR
            var ocrRequest = new OcrRequestDto
            {
                ImagePath = imagePath,
                FileName = Path.GetFileName(imagePath),
                ContentType = "image/jpeg"
            };

            Console.WriteLine("Executando OCR...");
            var ocrResult = await ocrProvider.ExtractTextAsync(ocrRequest);

            if (!ocrResult.Success)
            {
                Console.WriteLine($"✗ Erro no OCR: {ocrResult.ErrorMessage}");
                return;
            }

            Console.WriteLine($"✓ OCR concluído (confiança: {ocrResult.Confidence * 100:F2}%)");

            // 3. Passar texto extraído para o Parser
            var rawText = ocrResult.RawText;
            Console.WriteLine($"\nTexto para parsing ({rawText.Length} caracteres):");
            Console.WriteLine(rawText.Substring(0, Math.Min(200, rawText.Length)) + "...");

            // 4. O texto estará disponível no resultado final
            // analysisResult.ExtractedText = ocrResult.RawText;
        }

        // ========================================
        // EXEMPLO 8: Benchmarking
        // ========================================
        
        public static async Task BenchmarkExample()
        {
            Console.WriteLine("=== EXEMPLO 8: Benchmarking ===\n");

            var ocrProvider = new TesseractOcrProvider();

            var request = new OcrRequestDto
            {
                ImagePath = @"C:\temp\rotulo-teste.jpg",
                FileName = "rotulo-teste.jpg",
                ContentType = "image/jpeg"
            };

            // Executar múltiplas vezes para medir performance
            var iterations = 5;
            var times = new List<double>();

            for (int i = 0; i < iterations; i++)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var result = await ocrProvider.ExtractTextAsync(request);
                stopwatch.Stop();

                if (result.Success)
                {
                    times.Add(stopwatch.Elapsed.TotalMilliseconds);
                    Console.WriteLine($"Iteração {i + 1}: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
                }
            }

            if (times.Any())
            {
                Console.WriteLine($"\nEstatísticas:");
                Console.WriteLine($"  Média: {times.Average():F2} ms");
                Console.WriteLine($"  Mínimo: {times.Min():F2} ms");
                Console.WriteLine($"  Máximo: {times.Max():F2} ms");
            }
        }
    }
}
