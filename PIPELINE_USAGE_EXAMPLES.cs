using System;
using System.IO;
using System.Threading.Tasks;
using LabelWise.Application.DTOs;
using LabelWise.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace LabelWise.Examples
{
    /// <summary>
    /// Exemplos de uso do pipeline de análise de produtos com OCR.
    /// </summary>
    public class PipelineUsageExamples
    {
        private readonly IProductAnalysisPipelineOrchestrator _orchestrator;
        private readonly IOcrProvider _ocrProvider;
        private readonly IImageUploadService _uploadService;

        public PipelineUsageExamples(
            IProductAnalysisPipelineOrchestrator orchestrator,
            IOcrProvider ocrProvider,
            IImageUploadService uploadService)
        {
            _orchestrator = orchestrator;
            _ocrProvider = ocrProvider;
            _uploadService = uploadService;
        }

        /// <summary>
        /// Exemplo 1: Análise completa de uma imagem
        /// </summary>
        public async Task Example1_FullPipelineAnalysis()
        {
            Console.WriteLine("=== Exemplo 1: Pipeline Completo ===\n");

            // Simular upload de arquivo
            var imagePath = "caminho/para/rotulo.jpg";
            
            using var stream = File.OpenRead(imagePath);
            var fileName = Path.GetFileName(imagePath);

            // Executar pipeline completo
            var result = await _orchestrator.ExecutePipelineAsync(stream, fileName, userId: null);

            // Exibir metadados
            Console.WriteLine($"Pipeline ID: {result.Metadata.PipelineId}");
            Console.WriteLine($"Duração total: {result.Metadata.TotalDurationMs}ms\n");

            // Metadados por etapa
            Console.WriteLine($"Upload: {result.Metadata.UploadStep.DurationMs}ms - " +
                            $"{(result.Metadata.UploadStep.Success ? "✓" : "✗")}");
            Console.WriteLine($"OCR: {result.Metadata.OcrStep.DurationMs}ms - " +
                            $"{(result.Metadata.OcrStep.Success ? "✓" : "✗")}");
            Console.WriteLine($"Parsing: {result.Metadata.ParsingStep.DurationMs}ms - " +
                            $"{(result.Metadata.ParsingStep.Success ? "✓" : "✗")}");
            Console.WriteLine($"Analysis: {result.Metadata.AnalysisStep.DurationMs}ms - " +
                            $"{(result.Metadata.AnalysisStep.Success ? "✓" : "✗")}\n");

            // Resultado da análise
            Console.WriteLine($"Produto: {result.AnalysisResult.ProductName}");
            Console.WriteLine($"Marca: {result.AnalysisResult.Brand}");
            Console.WriteLine($"Score Geral: {result.AnalysisResult.GeneralScore:P0}");
            Console.WriteLine($"Score Personalizado: {result.AnalysisResult.PersonalizedScore:P0}");
            
            Console.WriteLine($"\nAlertas ({result.AnalysisResult.Alerts.Count}):");
            foreach (var alert in result.AnalysisResult.Alerts)
            {
                Console.WriteLine($"  ⚠ {alert}");
            }

            Console.WriteLine($"\nRecomendações ({result.AnalysisResult.Recommendations.Count}):");
            foreach (var rec in result.AnalysisResult.Recommendations)
            {
                Console.WriteLine($"  💡 {rec}");
            }
        }

        /// <summary>
        /// Exemplo 2: Apenas OCR (sem análise)
        /// </summary>
        public async Task Example2_OcrOnly()
        {
            Console.WriteLine("\n=== Exemplo 2: Apenas OCR ===\n");

            var request = new OcrRequestDto
            {
                ImagePath = "caminho/para/rotulo.jpg",
                FileName = "rotulo.jpg",
                ContentType = "image/jpeg"
            };

            var result = await _ocrProvider.ExtractTextAsync(request);

            Console.WriteLine($"Provider: {_ocrProvider.ProviderName}");
            Console.WriteLine($"Sucesso: {result.Success}");
            Console.WriteLine($"Confiança: {result.Confidence:P0}");
            Console.WriteLine($"Blocos de texto: {result.TextBlocks.Count}\n");

            Console.WriteLine("Texto extraído:");
            Console.WriteLine("─────────────────");
            Console.WriteLine(result.RawText);
            Console.WriteLine("─────────────────\n");

            Console.WriteLine("Blocos detalhados:");
            foreach (var block in result.TextBlocks)
            {
                Console.WriteLine($"\nTipo: {block.BlockType}");
                Console.WriteLine($"Confiança: {block.Confidence:P0}");
                Console.WriteLine($"Texto: {block.Text.Substring(0, Math.Min(50, block.Text.Length))}...");
                
                if (block.BoundingBox != null)
                {
                    Console.WriteLine($"Posição: ({block.BoundingBox.Left}, {block.BoundingBox.Top})");
                }
            }
        }

        /// <summary>
        /// Exemplo 3: Upload e validação
        /// </summary>
        public async Task Example3_UploadValidation()
        {
            Console.WriteLine("\n=== Exemplo 3: Upload e Validação ===\n");

            var testFiles = new[]
            {
                ("valido.jpg", 1024 * 1024),      // 1MB - válido
                ("grande.jpg", 10 * 1024 * 1024), // 10MB - inválido
                ("invalido.txt", 100)              // .txt - inválido
            };

            foreach (var (fileName, fileSize) in testFiles)
            {
                Console.WriteLine($"\nTestando: {fileName} ({fileSize / 1024}KB)");
                
                var isValid = _uploadService.ValidateImage(fileName, fileSize);
                Console.WriteLine($"Válido: {(isValid ? "✓ Sim" : "✗ Não")}");

                if (isValid)
                {
                    // Simular upload
                    using var stream = new MemoryStream(new byte[fileSize]);
                    var result = await _uploadService.UploadImageAsync(stream, fileName);
                    
                    if (result.Success)
                    {
                        Console.WriteLine($"✓ Upload realizado: {result.ImagePath}");
                        Console.WriteLine($"  Content-Type: {result.ContentType}");
                    }
                    else
                    {
                        Console.WriteLine($"✗ Erro: {result.ErrorMessage}");
                    }
                }
            }
        }

        /// <summary>
        /// Exemplo 4: Análise com perfil de usuário
        /// </summary>
        public async Task Example4_PersonalizedAnalysis()
        {
            Console.WriteLine("\n=== Exemplo 4: Análise Personalizada ===\n");

            // Simular usuário com restrições
            var userId = Guid.NewGuid();
            Console.WriteLine($"Analisando para usuário: {userId}");
            Console.WriteLine("Restrições: Glúten, Lactose");
            Console.WriteLine("Objetivo: Perda de peso\n");

            using var stream = File.OpenRead("caminho/para/rotulo.jpg");
            var result = await _orchestrator.ExecutePipelineAsync(
                stream, 
                "rotulo.jpg", 
                userId);

            Console.WriteLine("Resultado Personalizado:");
            Console.WriteLine($"Score Geral: {result.AnalysisResult.GeneralScore:P0}");
            Console.WriteLine($"Score Personalizado: {result.AnalysisResult.PersonalizedScore:P0}");
            
            var difference = result.AnalysisResult.PersonalizedScore - result.AnalysisResult.GeneralScore;
            Console.WriteLine($"Diferença: {difference:+0.00;-0.00} " +
                            $"({(difference < 0 ? "pior" : "melhor")} para seu perfil)\n");

            Console.WriteLine("Alertas personalizados:");
            foreach (var alert in result.AnalysisResult.Alerts)
            {
                Console.WriteLine($"  ⚠ {alert}");
            }
        }

        /// <summary>
        /// Exemplo 5: Verificar disponibilidade do OCR
        /// </summary>
        public async Task Example5_CheckOcrAvailability()
        {
            Console.WriteLine("\n=== Exemplo 5: Verificar OCR ===\n");

            Console.WriteLine($"Provider: {_ocrProvider.ProviderName}");
            
            var isAvailable = await _ocrProvider.IsAvailableAsync();
            Console.WriteLine($"Disponível: {(isAvailable ? "✓ Sim" : "✗ Não")}");

            if (isAvailable)
            {
                Console.WriteLine("✓ OCR está pronto para uso!");
            }
            else
            {
                Console.WriteLine("✗ OCR não está disponível.");
                Console.WriteLine("  Verifique as configurações ou use o MockOcrProvider.");
            }
        }

        /// <summary>
        /// Exemplo 6: Monitoramento de performance
        /// </summary>
        public async Task Example6_PerformanceMonitoring()
        {
            Console.WriteLine("\n=== Exemplo 6: Monitoramento de Performance ===\n");

            var iterations = 5;
            var totalDurations = new
            {
                Upload = 0.0,
                Ocr = 0.0,
                Parsing = 0.0,
                Analysis = 0.0,
                Total = 0.0
            };

            Console.WriteLine($"Executando {iterations} análises...\n");

            for (int i = 0; i < iterations; i++)
            {
                using var stream = File.OpenRead("caminho/para/rotulo.jpg");
                var result = await _orchestrator.ExecutePipelineAsync(stream, "rotulo.jpg");

                totalDurations = new
                {
                    Upload = totalDurations.Upload + result.Metadata.UploadStep.DurationMs,
                    Ocr = totalDurations.Ocr + result.Metadata.OcrStep.DurationMs,
                    Parsing = totalDurations.Parsing + result.Metadata.ParsingStep.DurationMs,
                    Analysis = totalDurations.Analysis + result.Metadata.AnalysisStep.DurationMs,
                    Total = totalDurations.Total + result.Metadata.TotalDurationMs
                };

                Console.WriteLine($"Iteração {i + 1}: {result.Metadata.TotalDurationMs:F0}ms");
            }

            Console.WriteLine("\n═══ Médias ═══");
            Console.WriteLine($"Upload:   {totalDurations.Upload / iterations:F0}ms");
            Console.WriteLine($"OCR:      {totalDurations.Ocr / iterations:F0}ms");
            Console.WriteLine($"Parsing:  {totalDurations.Parsing / iterations:F0}ms");
            Console.WriteLine($"Analysis: {totalDurations.Analysis / iterations:F0}ms");
            Console.WriteLine($"─────────────────────");
            Console.WriteLine($"Total:    {totalDurations.Total / iterations:F0}ms");
        }
    }

    /// <summary>
    /// Classe principal para executar os exemplos
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Configurar DI (simplificado para exemplo)
            var services = new ServiceCollection();
            
            // Adicionar serviços da infraestrutura
            // services.AddInfrastructureServices(configuration);
            // services.AddApplicationServices(configuration);

            var serviceProvider = services.BuildServiceProvider();

            // Criar instância dos exemplos
            var examples = serviceProvider.GetRequiredService<PipelineUsageExamples>();

            // Executar exemplos
            Console.WriteLine("╔════════════════════════════════════════════╗");
            Console.WriteLine("║  LabelWise OCR Pipeline - Exemplos de Uso ║");
            Console.WriteLine("╚════════════════════════════════════════════╝\n");

            try
            {
                await examples.Example1_FullPipelineAnalysis();
                await examples.Example2_OcrOnly();
                await examples.Example3_UploadValidation();
                await examples.Example4_PersonalizedAnalysis();
                await examples.Example5_CheckOcrAvailability();
                await examples.Example6_PerformanceMonitoring();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Erro: {ex.Message}");
            }

            Console.WriteLine("\n✓ Exemplos concluídos!");
        }
    }
}
