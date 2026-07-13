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
    /// Implementação de OCR usando Azure Computer Vision para extração de texto de imagens.
    /// 
    /// VANTAGENS:
    /// - Alta precisão mesmo em imagens de baixa qualidade
    /// - Suporte nativo para múltiplos idiomas
    /// - Não requer instalação local de bibliotecas
    /// - API gerenciada pela Microsoft
    /// - Excelente para texto impresso em rótulos
    /// 
    /// REQUISITOS:
    /// 1. Recurso Azure Computer Vision criado
    /// 2. Endpoint e API Key configurados em appsettings.json
    /// 3. Pacote NuGet: Azure.AI.Vision.ImageAnalysis
    /// 
    /// CONFIGURAÇÃO:
    /// {
    ///   "OCR": {
    ///     "Provider": "AzureComputerVision",
    ///     "Azure": {
    ///       "Endpoint": "https://your-resource.cognitiveservices.azure.com/",
    ///       "ApiKey": "your-api-key-here"
    ///     }
    ///   }
    /// }
    /// 
    /// CUSTOS:
    /// Free Tier: 5.000 transações/mês
    /// Standard: $1 por 1.000 transações
    /// Veja: https://azure.microsoft.com/pricing/details/cognitive-services/computer-vision/
    /// </summary>
    public class AzureComputerVisionOcrProvider : IOcrProvider
    {
        private readonly string _endpoint;
        private readonly string _apiKey;
        private readonly ILogger<AzureComputerVisionOcrProvider>? _logger;
        private readonly ImageAnalysisClient _client;

        public string ProviderName => "Azure Computer Vision OCR";

        /// <summary>
        /// Inicializa o provider do Azure Computer Vision.
        /// </summary>
        /// <param name="endpoint">Endpoint do recurso Azure (ex: https://your-resource.cognitiveservices.azure.com/)</param>
        /// <param name="apiKey">Chave de API do recurso Azure</param>
        /// <param name="logger">Logger opcional para diagnósticos</param>
        /// <exception cref="ArgumentException">Se endpoint ou apiKey estiverem vazios</exception>
        public AzureComputerVisionOcrProvider(
            string endpoint, 
            string apiKey, 
            ILogger<AzureComputerVisionOcrProvider>? logger = null)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentException("Azure endpoint não pode ser vazio", nameof(endpoint));
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("Azure API Key não pode ser vazia", nameof(apiKey));
            }

            _endpoint = endpoint.TrimEnd('/');
            _apiKey = apiKey;
            _logger = logger;

            _logger?.LogInformation("Inicializando AzureComputerVisionOcrProvider");
            _logger?.LogInformation("Endpoint: {Endpoint}", _endpoint);

            try
            {
                _client = new ImageAnalysisClient(
                    new Uri(_endpoint),
                    new AzureKeyCredential(_apiKey));

                _logger?.LogInformation("✅ Azure Computer Vision Client inicializado com sucesso");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Erro ao inicializar Azure Computer Vision Client");
                throw new InvalidOperationException(
                    "Falha ao inicializar Azure Computer Vision. " +
                    "Verifique o endpoint e API key.", ex);
            }
        }

        public async Task<OcrResultDto> ExtractTextAsync(OcrRequestDto request)
        {
            if (request == null)
            {
                return CreateErrorResult("Request não pode ser nulo");
            }

            if (string.IsNullOrWhiteSpace(request.ImagePath))
            {
                return CreateErrorResult("Caminho da imagem não pode ser vazio");
            }

            if (!File.Exists(request.ImagePath))
            {
                return CreateErrorResult($"Arquivo de imagem não encontrado: {request.ImagePath}");
            }

            _logger?.LogInformation("🚀 Iniciando extração OCR com Azure Computer Vision");
            _logger?.LogInformation("   Arquivo: {FileName}", request.FileName);
            _logger?.LogInformation("   Tamanho: {Size} bytes", new FileInfo(request.ImagePath).Length);

            try
            {
                // Ler imagem como BinaryData
                byte[] imageBytes = await File.ReadAllBytesAsync(request.ImagePath);
                BinaryData imageData = BinaryData.FromBytes(imageBytes);

                _logger?.LogDebug("📤 Enviando imagem para Azure ({Size} bytes)...", imageBytes.Length);

                // Chamar Azure Computer Vision
                var result = await _client.AnalyzeAsync(
                    imageData,
                    VisualFeatures.Read, // Feature para OCR/leitura de texto
                    new ImageAnalysisOptions
                    {
                        Language = "pt" // Português - pode ser configurável
                    });

                _logger?.LogDebug("📥 Resposta recebida do Azure");

                // Processar resultado
                return ProcessAzureResult(result, request.FileName);
            }
            catch (RequestFailedException ex)
            {
                var errorMsg = $"❌ Erro na chamada Azure Computer Vision: {ex.Message}";
                _logger?.LogError(ex, errorMsg);
                _logger?.LogError("Status Code: {StatusCode}", ex.Status);
                _logger?.LogError("Error Code: {ErrorCode}", ex.ErrorCode);

                return CreateErrorResult(
                    $"Erro Azure Computer Vision (Status {ex.Status}): {ex.Message}\n" +
                    $"Verifique se o recurso está ativo e a API key está válida.");
            }
            catch (Exception ex)
            {
                var errorMsg = $"❌ Erro inesperado ao processar OCR: {ex.Message}";
                _logger?.LogError(ex, errorMsg);
                return CreateErrorResult(errorMsg);
            }
        }

        private OcrResultDto ProcessAzureResult(ImageAnalysisResult result, string fileName)
        {
            if (result.Read == null || result.Read.Blocks == null || result.Read.Blocks.Count == 0)
            {
                _logger?.LogWarning("⚠️ Azure não encontrou texto na imagem");
                return new OcrResultDto
                {
                    Success = true,
                    RawText = string.Empty,
                    Confidence = 0,
                    TextBlocks = new List<OcrTextBlock>(),
                    ProviderMetadata = GetMetadata()
                };
            }

            var textBlocks = new List<OcrTextBlock>();
            var fullText = new System.Text.StringBuilder();
            var confidences = new List<double>();

            _logger?.LogInformation("📄 Processando {BlockCount} blocos de texto do Azure", result.Read.Blocks.Count);

            foreach (var block in result.Read.Blocks)
            {
                if (block.Lines == null) continue;

                foreach (var line in block.Lines)
                {
                    if (string.IsNullOrWhiteSpace(line.Text)) continue;

                    fullText.AppendLine(line.Text);

                    // Calcular confiança média das palavras na linha
                    double lineConfidence = 0;
                    if (line.Words != null && line.Words.Count > 0)
                    {
                        lineConfidence = line.Words.Average(w => w.Confidence);
                        confidences.Add(lineConfidence);
                    }

                    // Criar bloco de texto estruturado
                    textBlocks.Add(new OcrTextBlock
                    {
                        Text = line.Text.Trim(),
                        Confidence = lineConfidence,
                        BlockType = DetermineBlockType(line.Text),
                        BoundingBox = ConvertBoundingBox(line.BoundingPolygon)
                    });

                    _logger?.LogDebug("   Linha: '{Text}' (Confiança: {Confidence:F2}%)", 
                        line.Text.Trim(), lineConfidence * 100);
                }
            }

            var overallConfidence = confidences.Any() ? confidences.Average() : 0;
            var rawText = fullText.ToString().TrimEnd();

            _logger?.LogInformation("✅ OCR Azure concluído com sucesso");
            _logger?.LogInformation("   Confiança geral: {Confidence:F2}%", overallConfidence * 100);
            _logger?.LogInformation("   Total de linhas: {Lines}", textBlocks.Count);
            _logger?.LogInformation("   Caracteres extraídos: {Chars}", rawText.Length);

            return new OcrResultDto
            {
                Success = true,
                RawText = rawText,
                Confidence = overallConfidence,
                TextBlocks = textBlocks,
                ProviderMetadata = GetMetadata()
            };
        }

        /// <summary>
        /// Converte polígono de bounding box do Azure para formato simplificado.
        /// Azure retorna um polígono de 4 pontos; convertemos para retângulo.
        /// </summary>
        private BoundingBox? ConvertBoundingBox(IReadOnlyList<Azure.AI.Vision.ImageAnalysis.ImagePoint>? polygon)
        {
            if (polygon == null || polygon.Count < 2)
            {
                return null;
            }

            // Pega os pontos extremos do polígono
            var minX = polygon.Min(p => p.X);
            var minY = polygon.Min(p => p.Y);
            var maxX = polygon.Max(p => p.X);
            var maxY = polygon.Max(p => p.Y);

            return new BoundingBox
            {
                Left = minX,
                Top = minY,
                Width = maxX - minX,
                Height = maxY - minY
            };
        }

        /// <summary>
        /// Determina o tipo de bloco de texto baseado em palavras-chave.
        /// Útil para destacar informações importantes em rótulos.
        /// </summary>
        private string DetermineBlockType(string text)
        {
            var upperText = text.ToUpperInvariant();

            // Cabeçalhos principais
            if (upperText.Contains("INFORMAÇÃO NUTRICIONAL") ||
                upperText.Contains("TABELA NUTRICIONAL") ||
                upperText.Contains("INGREDIENTES") ||
                upperText.Contains("ALÉRGICOS") ||
                upperText.Contains("CONTÉM") ||
                upperText.Contains("NUTRITION FACTS") ||
                upperText.Contains("INGREDIENTS"))
            {
                return "HEADING";
            }

            // Sub-cabeçalhos
            if (upperText.Contains("PORÇÃO") ||
                upperText.Contains("QUANTIDADE") ||
                upperText.Contains("SERVING SIZE") ||
                upperText.Contains("AMOUNT PER"))
            {
                return "SUBHEADING";
            }

            // Valores nutricionais (geralmente contém números e %)
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\d+\s*(g|mg|mcg|ml|%|kcal)"))
            {
                return "NUTRITIONAL_VALUE";
            }

            return "TEXT";
        }

        public Task<bool> IsAvailableAsync()
        {
            // Verifica se as credenciais foram configuradas
            var isAvailable = !string.IsNullOrWhiteSpace(_endpoint) && 
                             !string.IsNullOrWhiteSpace(_apiKey) &&
                             _client != null;

            _logger?.LogDebug("IsAvailableAsync: {IsAvailable}", isAvailable);

            return Task.FromResult(isAvailable);
        }

        public Dictionary<string, string> GetMetadata()
        {
            return new Dictionary<string, string>
            {
                ["ProviderName"] = ProviderName,
                ["ProviderType"] = GetType().FullName ?? "AzureComputerVisionOcrProvider",
                ["Endpoint"] = _endpoint,
                ["HasApiKey"] = (!string.IsNullOrWhiteSpace(_apiKey)).ToString(),
                ["IsConfigured"] = (_client != null).ToString(),
                ["IsMock"] = "false",
                ["SupportsMultipleLanguages"] = "true",
                ["DefaultLanguage"] = "pt",
                ["PricingModel"] = "Pay-per-use (Free: 5k/month, Paid: $1/1k transactions)",
                ["Documentation"] = "https://learn.microsoft.com/azure/ai-services/computer-vision/overview-ocr"
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
