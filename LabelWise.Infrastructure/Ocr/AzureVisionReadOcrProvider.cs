using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.AI.Vision.ImageAnalysis;
using LabelWise.Application.DTOs;
using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Ocr
{
    /// <summary>
    /// Implementação de OCR usando Azure AI Vision Read API.
    /// 
    /// VANTAGENS:
    /// ✅ Alta precisão em fotos de celular (baixa qualidade, ângulo, iluminação)
    /// ✅ Excelente para texto impresso em rótulos de alimentos
    /// ✅ Suporta múltiplos idiomas sem configuração adicional
    /// ✅ API gerenciada (sem instalação local)
    /// ✅ Retorna confidence score por palavra
    /// ✅ Detecta layout e estrutura do texto
    /// 
    /// CASOS DE USO IDEAIS:
    /// - Fotos de rótulos tiradas com celular
    /// - Imagens com baixa iluminação ou ângulo
    /// - Texto pequeno ou com baixo contraste
    /// - Múltiplos idiomas na mesma imagem
    /// 
    /// REQUISITOS:
    /// 1. Recurso "Azure AI Vision" criado no portal Azure
    /// 2. Pacote NuGet: Azure.AI.Vision.ImageAnalysis
    /// 
    /// CUSTOS (2024):
    /// - Free Tier: 5.000 transações/mês
    /// - Standard S1: $1.00 por 1.000 transações
    /// - Veja: https://azure.microsoft.com/pricing/details/cognitive-services/computer-vision/
    /// </summary>
    public class AzureVisionReadOcrProvider : IOcrProvider
    {
        private readonly string _endpoint;
        private readonly string _apiKey;
        private readonly string _language;
        private readonly int _timeoutSeconds;
        private readonly bool _enableDetailedLogging;
        private readonly ILogger<AzureVisionReadOcrProvider>? _logger;
        private readonly ImageAnalysisClient _client;

        public string ProviderName => "Azure AI Vision Read OCR";

        /// <summary>
        /// Inicializa o provider Azure Vision Read OCR.
        /// </summary>
        public AzureVisionReadOcrProvider(
            string endpoint,
            string apiKey,
            string language = "pt",
            int timeoutSeconds = 30,
            bool enableDetailedLogging = false,
            ILogger<AzureVisionReadOcrProvider>? logger = null)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Azure Vision endpoint não pode ser vazio", nameof(endpoint));

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("Azure Vision API Key não pode ser vazia", nameof(apiKey));

            _endpoint = endpoint.TrimEnd('/');
            _apiKey = apiKey;
            _language = language;
            _timeoutSeconds = timeoutSeconds;
            _enableDetailedLogging = enableDetailedLogging;
            _logger = logger;

            _logger?.LogInformation("🔧 Inicializando {ProviderName}", ProviderName);
            _logger?.LogInformation("   Endpoint: {Endpoint}", _endpoint);
            _logger?.LogInformation("   Language: {Language}", _language);
            _logger?.LogInformation("   Timeout: {Timeout}s", _timeoutSeconds);

            try
            {
                _client = new ImageAnalysisClient(
                    new Uri(_endpoint),
                    new AzureKeyCredential(_apiKey));

                _logger?.LogInformation("✅ Azure Vision Client inicializado com sucesso");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Erro ao inicializar Azure Vision Client");
                throw new InvalidOperationException(
                    "Falha ao inicializar Azure AI Vision. Verifique endpoint e API key.", ex);
            }
        }

        public async Task<OcrResultDto> ExtractTextAsync(OcrRequestDto request)
        {
            if (request == null)
                return CreateErrorResult("Request não pode ser nulo");

            if (string.IsNullOrWhiteSpace(request.ImagePath))
                return CreateErrorResult("Caminho da imagem não pode ser vazio");

            if (!File.Exists(request.ImagePath))
                return CreateErrorResult($"Arquivo não encontrado: {request.ImagePath}");

            _logger?.LogInformation("🚀 Iniciando OCR com {ProviderName}", ProviderName);
            _logger?.LogInformation("   Arquivo: {FileName} ({Size:N0} bytes)",
                request.FileName,
                new FileInfo(request.ImagePath).Length);

            try
            {
                // Ler imagem
                byte[] imageBytes = await File.ReadAllBytesAsync(request.ImagePath);
                BinaryData imageData = BinaryData.FromBytes(imageBytes);

                if (_enableDetailedLogging)
                {
                    _logger?.LogDebug("📤 Enviando {Size:N0} bytes para Azure Vision...", imageBytes.Length);
                }

                // Chamar Azure Vision Read API
                var analysisResult = await _client.AnalyzeAsync(
                    imageData,
                    VisualFeatures.Read,
                    new ImageAnalysisOptions
                    {
                        Language = _language
                    });

                if (_enableDetailedLogging)
                {
                    _logger?.LogDebug("📥 Resposta recebida do Azure Vision");
                }

                // Processar resultado
                return ProcessAzureVisionResult(analysisResult);
            }
            catch (RequestFailedException ex)
            {
                var errorMsg = $"Erro na chamada Azure Vision: {ex.Message} (Status: {ex.Status}, Code: {ex.ErrorCode})";
                _logger?.LogError(ex, "❌ {ErrorMsg}", errorMsg);
                return CreateErrorResult(errorMsg);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Erro inesperado ao processar OCR: {ex.Message}";
                _logger?.LogError(ex, "❌ {ErrorMsg}", errorMsg);
                return CreateErrorResult(errorMsg);
            }
        }

        private OcrResultDto ProcessAzureVisionResult(ImageAnalysisResult analysisResult)
        {
            if (analysisResult.Read == null || analysisResult.Read.Blocks == null)
            {
                _logger?.LogWarning("⚠️ Azure Vision não retornou blocos de texto (imagem sem texto?)");
                return CreateErrorResult("Nenhum texto encontrado na imagem");
            }

            var blocks = new List<OcrTextBlock>();
            var allText = new List<string>();
            var allConfidences = new List<double>();

            foreach (var block in analysisResult.Read.Blocks)
            {
                foreach (var line in block.Lines)
                {
                    var lineText = line.Text;
                    allText.Add(lineText);

                    // Calcular confidence média da linha baseada nas palavras
                    double lineConfidence = 1.0; // Default se não houver palavras
                    if (line.Words != null && line.Words.Count > 0)
                    {
                        var wordConfidences = line.Words
                            .Where(w => w.Confidence > 0)
                            .Select(w => (double)w.Confidence)
                            .ToList();

                        if (wordConfidences.Any())
                        {
                            lineConfidence = wordConfidences.Average();
                        }
                    }

                    allConfidences.Add(lineConfidence);

                    // Criar bounding box da linha
                    BoundingBox? boundingBox = null;
                    if (line.BoundingPolygon != null && line.BoundingPolygon.Count >= 4)
                    {
                        var points = line.BoundingPolygon;
                        var minX = points.Min(p => p.X);
                        var minY = points.Min(p => p.Y);
                        var maxX = points.Max(p => p.X);
                        var maxY = points.Max(p => p.Y);

                        boundingBox = new BoundingBox
                        {
                            Left = minX,
                            Top = minY,
                            Width = maxX - minX,
                            Height = maxY - minY
                        };
                    }

                    blocks.Add(new OcrTextBlock
                    {
                        Text = lineText,
                        Confidence = lineConfidence,
                        BoundingBox = boundingBox,
                        BlockType = DetermineBlockType(lineText)
                    });
                }
            }

            var rawText = string.Join("\n", allText);
            var overallConfidence = allConfidences.Any() ? allConfidences.Average() : 0.0;

            _logger?.LogInformation("✅ OCR concluído com sucesso");
            _logger?.LogInformation("   Confiança média: {Confidence:P2}", overallConfidence);
            _logger?.LogInformation("   Caracteres extraídos: {Chars:N0}", rawText.Length);
            _logger?.LogInformation("   Linhas de texto: {Lines:N0}", allText.Count);
            _logger?.LogInformation("   Blocos estruturados: {Blocks:N0}", blocks.Count);

            if (_enableDetailedLogging && rawText.Length > 0)
            {
                var preview = rawText.Length > 200 ? rawText.Substring(0, 200) + "..." : rawText;
                _logger?.LogDebug("   Preview: {Preview}", preview.Replace("\n", " | "));
            }

            return new OcrResultDto
            {
                RawText = rawText,
                Confidence = overallConfidence,
                Success = !string.IsNullOrWhiteSpace(rawText),
                TextBlocks = blocks,
                ProviderMetadata = GetMetadata(rawText.Length, blocks.Count, overallConfidence)
            };
        }

        private string DetermineBlockType(string text)
        {
            var upperText = text.ToUpperInvariant();

            // Identifica cabeçalhos típicos de rótulos
            if (upperText.Contains("INFORMAÇÃO NUTRICIONAL") ||
                upperText.Contains("INGREDIENTES") ||
                upperText.Contains("ALÉRGICOS") ||
                upperText.Contains("CONTÉM") ||
                upperText.Contains("NUTRITION") ||
                upperText.Contains("INGREDIENTS"))
            {
                return "HEADING";
            }

            if (upperText.Contains("PORÇÃO") ||
                upperText.Contains("QUANTIDADE") ||
                upperText.Contains("SERVING") ||
                upperText.Contains("AMOUNT"))
            {
                return "SUBHEADING";
            }

            return "TEXT";
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                // Validação simples: verifica se o client foi inicializado
                // Uma validação mais completa faria uma chamada real à API
                var isAvailable = _client != null;

                _logger?.LogDebug("IsAvailableAsync: {IsAvailable}", isAvailable);

                return await Task.FromResult(isAvailable);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Erro ao verificar disponibilidade do Azure Vision");
                return false;
            }
        }

        public Dictionary<string, string> GetMetadata()
        {
            return GetMetadata(0, 0, 0);
        }

        private Dictionary<string, string> GetMetadata(int textLength = 0, int blocksCount = 0, double confidence = 0)
        {
            return new Dictionary<string, string>
            {
                ["ProviderName"] = ProviderName,
                ["ProviderType"] = GetType().FullName ?? "AzureVisionReadOcrProvider",
                ["Endpoint"] = _endpoint,
                ["Language"] = _language,
                ["TimeoutSeconds"] = _timeoutSeconds.ToString(),
                ["IsMock"] = "false",
                ["IsAzure"] = "true",
                ["ApiVersion"] = "Azure.AI.Vision.ImageAnalysis",
                ["TextLength"] = textLength.ToString(),
                ["BlocksCount"] = blocksCount.ToString(),
                ["Confidence"] = confidence.ToString("F4")
            };
        }

        private OcrResultDto CreateErrorResult(string errorMessage)
        {
            return new OcrResultDto
            {
                Success = false,
                ErrorMessage = errorMessage,
                RawText = string.Empty,
                Confidence = 0,
                TextBlocks = new List<OcrTextBlock>(),
                ProviderMetadata = GetMetadata()
            };
        }
    }
}
