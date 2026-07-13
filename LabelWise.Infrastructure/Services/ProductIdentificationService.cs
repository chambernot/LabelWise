using System.Diagnostics;
using LabelWise.Application.DTOs.AI;
using LabelWise.Application.DTOs.ProductIdentification;
using LabelWise.Application.Helpers.ProductIdentification;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services
{
    /// <summary>
    /// Implementação do serviço de identificação de produtos.
    /// 
    /// ESTRATÉGIA DE IDENTIFICAÇÃO (PRIORIDADE DECRESCENTE):
    /// 1. Barcode (se fornecido ou detectável) → Busca em base externa [Prioridade: 100]
    /// 2. OCR da embalagem frontal (se CaptureType = FrontPackaging e confiança alta) [Prioridade: 60]
    /// 3. OCR + OpenAI Vision (fallback se OCR insuficiente) [Prioridade: 90]
    /// 4. OpenAI Vision (fallback se OCR falhar) [Prioridade: 80]
    /// 5. Sugestão de candidatos (última tentativa) [Prioridade: 0]
    /// 
    /// REGRAS:
    /// - Código de barras sempre tem prioridade máxima
    /// - Tabela nutricional NUNCA é usada para identificar produto
    /// - OpenAI Vision é usado quando OCR falha ou é insuficiente
    /// - "INFORMAÇÃO NUTRICIONAL" e cabeçalhos são filtrados como ruído
    /// - Múltiplas fontes aumentam confiança
    /// - NUNCA inventa um nome de produto
    /// </summary>
    public class ProductIdentificationService : IProductIdentificationService
    {
        private readonly IOcrProvider _ocrProvider;
        private readonly IVisualInterpreter _visualInterpreter;
        private readonly ICandidateSuggestionService _candidateSuggestionService;
        private readonly IKnownProductSearchService _knownProductSearchService;
        private readonly ILogger<ProductIdentificationService> _logger;

        // Threshold mínimo de confiança para identificação automática
        private const double MinConfidenceThreshold = 0.60;

        public ProductIdentificationService(
            IOcrProvider ocrProvider,
            IVisualInterpreter visualInterpreter,
            ICandidateSuggestionService candidateSuggestionService,
            IKnownProductSearchService knownProductSearchService,
            ILogger<ProductIdentificationService> logger)
        {
            _ocrProvider = ocrProvider ?? throw new ArgumentNullException(nameof(ocrProvider));
            _visualInterpreter = visualInterpreter ?? throw new ArgumentNullException(nameof(visualInterpreter));
            _candidateSuggestionService = candidateSuggestionService ?? throw new ArgumentNullException(nameof(candidateSuggestionService));
            _knownProductSearchService = knownProductSearchService ?? throw new ArgumentNullException(nameof(knownProductSearchService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ProductIdentificationResult> IdentifyProductAsync(ProductIdentificationRequest request)
        {
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            _logger.LogInformation("🔍 Iniciando identificação de produto");
            _logger.LogInformation("   UserId: {UserId}", request.UserId);
            _logger.LogInformation("   CaptureType: {CaptureType}", request.CaptureType);
            _logger.LogInformation("   ManualBarcode: {Barcode}", request.ManualBarcode ?? "N/A");
            _logger.LogInformation("   ImageSize: {Size} bytes", request.ImageData.Length);
            _logger.LogInformation("═══════════════════════════════════════════════════════════");

            try
            {
                // ═══════════════════════════════════════════════════════════════════
                // ETAPA 1: Tentativa de identificação por código de barras
                // ═══════════════════════════════════════════════════════════════════
                if (!string.IsNullOrWhiteSpace(request.ManualBarcode))
                {
                    _logger.LogInformation("📍 ETAPA 1: Código de barras manual fornecido");
                    
                    var barcodeResult = await IdentifyByBarcodeAsync(
                        request.ManualBarcode, 
                        request.LanguageCode);

                    if (barcodeResult.Success && barcodeResult.MatchConfidence >= 0.60)
                    {
                        stopwatch.Stop();
                        barcodeResult.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;

                        _logger.LogInformation("✅ Produto identificado por código de barras manual");
                        LogFinalResult(barcodeResult);

                        return barcodeResult;
                    }

                    _logger.LogWarning("⚠️ Código de barras manual não resultou em identificação confiável");
                }

                // TODO: Implementar leitura automática de barcode da imagem
                // if (request.CaptureType == CaptureType.Barcode)
                // {
                //     var detectedBarcode = await _barcodeReader.ReadBarcodeAsync(request.ImageData);
                //     if (detectedBarcode != null) { ... }
                // }

                // ═══════════════════════════════════════════════════════════════════
                // ETAPA 2: Tentativa de identificação por OCR frontal
                // ═══════════════════════════════════════════════════════════════════
                if (request.EnableOcrFallback && 
                    request.CaptureType == CaptureType.FrontPackaging)
                {
                    _logger.LogInformation("📍 ETAPA 2: Tentando identificação por OCR frontal");

                    var ocrResult = await IdentifyByFrontPackagingOcrAsync(request);

                    if (ocrResult.Success && ocrResult.MatchConfidence >= 0.60)
                    {
                        stopwatch.Stop();
                        ocrResult.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;

                        _logger.LogInformation("✅ Produto identificado por OCR frontal");
                        LogFinalResult(ocrResult);

                        return ocrResult;
                    }

                    _logger.LogWarning("⚠️ OCR frontal não resultou em identificação confiável");
                }

                // ═══════════════════════════════════════════════════════════════════
                // ETAPA 3: Fallback - Busca em Produtos Conhecidos
                // ═══════════════════════════════════════════════════════════════════
                _logger.LogInformation("📍 ETAPA 3: Buscando em catálogo de produtos conhecidos");

                var knownProductResult = await SearchInKnownProductsAsync(request);

                if (knownProductResult != null && knownProductResult.MatchConfidence >= 0.60)
                {
                    stopwatch.Stop();
                    knownProductResult.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;

                    _logger.LogInformation("✅ Produto identificado no catálogo de produtos conhecidos");
                    LogFinalResult(knownProductResult);

                    return knownProductResult;
                }

                // ═══════════════════════════════════════════════════════════════════
                // ETAPA 4: Fallback Final - Sugestão de Candidatos
                // ═══════════════════════════════════════════════════════════════════
                _logger.LogInformation("📍 ETAPA 4: Buscando candidatos sugeridos (fallback final)");

                var candidateSuggestionResult = await GetCandidateSuggestionsAsync(request);

                stopwatch.Stop();

                // Criar resultado com ProductUnknown + topCandidates
                var unknownResult = CreateUnknownProductResultWithCandidates(
                    "Produto não identificado com confiança suficiente. Candidatos sugeridos disponíveis.",
                    stopwatch.Elapsed.TotalSeconds,
                    candidateSuggestionResult);

                LogFinalResult(unknownResult);

                return unknownResult;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(ex, "❌ Erro durante identificação do produto");

                return new ProductIdentificationResult
                {
                    Success = false,
                    Method = IdentificationMethod.Composite,
                    MatchSource = MatchSource.Unknown,
                    Confidence = 0.0,
                    MatchConfidence = 0.0,
                    ErrorMessage = $"Erro durante identificação: {ex.Message}",
                    ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds,
                    Details = new List<string>
                    {
                        $"Erro: {ex.GetType().Name}",
                        $"Mensagem: {ex.Message}"
                    }
                };
            }
        }

        public async Task<ProductIdentificationResult> IdentifyByBarcodeAsync(
            string barcode, 
            string languageCode = "pt")
        {
            _logger.LogInformation("🔍 Identificando produto por código de barras: {Barcode}", barcode);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Validar código de barras
                if (!BarcodeValidator.IsValid(barcode, out var validationError))
                {
                    _logger.LogWarning("⚠️ Código de barras inválido: {Error}", validationError);

                    return new ProductIdentificationResult
                    {
                        Success = false,
                        Method = IdentificationMethod.BarcodeScanning,
                        MatchSource = MatchSource.Barcode,
                        Confidence = 0.0,
                        MatchConfidence = 0.0,
                        Barcode = barcode,
                        ErrorMessage = $"Código de barras inválido: {validationError}",
                        ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds,
                        Details = new List<string>
                        {
                            $"Formato: {BarcodeValidator.GetBarcodeType(barcode)}",
                            $"Validação: {validationError}"
                        }
                    };
                }

                // Normalizar código de barras
                var normalizedBarcode = BarcodeValidator.Normalize(barcode);
                var barcodeType = BarcodeValidator.GetBarcodeType(normalizedBarcode);

                _logger.LogInformation("✅ Código de barras válido: {Type}", barcodeType);

                // ═══════════════════════════════════════════════════════════
                // Buscar em produtos conhecidos (catálogo local)
                // ═══════════════════════════════════════════════════════════
                _logger.LogInformation("🔍 Buscando código de barras no catálogo de produtos conhecidos");

                var knownProductResult = await _knownProductSearchService.SearchByBarcodeAsync(normalizedBarcode);

                if (knownProductResult != null)
                {
                    _logger.LogInformation("✅ Produto encontrado no catálogo local: {Name} - {Brand}",
                        knownProductResult.Name, knownProductResult.Brand);

                    stopwatch.Stop();

                    return new ProductIdentificationResult
                    {
                        Success = true,
                        Method = IdentificationMethod.BarcodeScanning,
                        MatchSource = MatchSource.Barcode,
                        Confidence = 0.95,
                        MatchConfidence = knownProductResult.RelevanceScore,
                        Barcode = normalizedBarcode,
                        MatchedProductName = knownProductResult.Name,
                        MatchedBrand = knownProductResult.Brand,
                        ProductName = knownProductResult.Name,
                        Brand = knownProductResult.Brand,
                        Category = knownProductResult.Category,
                        IsFromExternalDatabase = false,
                        IsReliableMatch = true,
                        ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds,
                        Metadata = new Dictionary<string, string>
                        {
                            ["BarcodeType"] = barcodeType,
                            ["BarcodeValidated"] = "true",
                            ["KnownProductId"] = knownProductResult.ProductId.ToString(),
                            ["Source"] = "KnownProductsCatalog",
                            ["IsValidated"] = knownProductResult.IsValidated.ToString(),
                            ["IdentificationCount"] = knownProductResult.IdentificationCount.ToString()
                        },
                        Details = new List<string>
                        {
                            $"Código de barras: {normalizedBarcode} ({barcodeType})",
                            $"Produto: {knownProductResult.Name}",
                            $"Marca: {knownProductResult.Brand}",
                            $"Categoria: {knownProductResult.Category}",
                            $"Confiança: {knownProductResult.RelevanceScore:P2}",
                            $"Fonte: Catálogo de Produtos Conhecidos"
                        }
                    };
                }

                _logger.LogInformation("ℹ️ Produto não encontrado no catálogo local");

                // TODO: Buscar em base externa (Open Food Facts, etc.)
                // var externalProduct = await _externalDatabase.SearchByBarcodeAsync(normalizedBarcode, languageCode);

                // Simulação: produto não encontrado em base externa
                // Retorna resultado indicando barcode válido mas produto desconhecido
                stopwatch.Stop();

                _logger.LogWarning("⚠️ Produto não encontrado em bases externas");

                return new ProductIdentificationResult
                {
                    Success = true,
                    Method = IdentificationMethod.BarcodeScanning,
                    MatchSource = MatchSource.Barcode,
                    Confidence = 0.85, // Barcode lido com sucesso
                    MatchConfidence = 0.50, // Produto não encontrado em base externa
                    Barcode = normalizedBarcode,
                    IsFromExternalDatabase = false,
                    IsReliableMatch = false,
                    ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds,
                    Metadata = new Dictionary<string, string>
                    {
                        ["BarcodeType"] = barcodeType,
                        ["BarcodeValidated"] = "true",
                        ["ExternalDatabaseSearched"] = "false (not implemented)",
                        ["Note"] = "Código de barras válido, mas produto não encontrado em base externa"
                    },
                    Details = new List<string>
                    {
                        $"Código de barras: {normalizedBarcode} ({barcodeType})",
                        "Produto não encontrado em bases externas",
                        "Integração com Open Food Facts: pendente de implementação"
                    }
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(ex, "❌ Erro ao identificar produto por código de barras");

                return new ProductIdentificationResult
                {
                    Success = false,
                    Method = IdentificationMethod.BarcodeScanning,
                    MatchSource = MatchSource.Barcode,
                    Confidence = 0.0,
                    MatchConfidence = 0.0,
                    Barcode = barcode,
                    ErrorMessage = $"Erro ao processar código de barras: {ex.Message}",
                    ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds
                };
            }
        }

        public async Task<Dictionary<string, string>> GetServiceStatusAsync()
        {
            var status = new Dictionary<string, string>();

            try
            {
                // OCR Engine
                var ocrAvailable = await _ocrProvider.IsAvailableAsync();
                status["OcrEngine"] = ocrAvailable ? "Available" : "Unavailable";

                // Barcode Reader (futuro)
                status["BarcodeReader"] = "Not Implemented";

                // External Database (futuro)
                status["ExternalDatabase"] = "Not Implemented";

                // Visual Recognition (futuro)
                status["VisualRecognition"] = "Not Implemented";

                status["OverallStatus"] = ocrAvailable ? "Partial" : "Limited";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar status do serviço");
                status["Error"] = ex.Message;
                status["OverallStatus"] = "Error";
            }

            return status;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // MÉTODOS PRIVADOS - ESTRATÉGIAS DE IDENTIFICAÇÃO
        // ═══════════════════════════════════════════════════════════════════════

        private async Task<ProductIdentificationResult> IdentifyByFrontPackagingOcrAsync(
            ProductIdentificationRequest request)
        {
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("📖 Executando OCR na embalagem frontal");

            string? tempImagePath = null;
            try
            {
                // Salvar imagem temporariamente
                tempImagePath = Path.Combine(Path.GetTempPath(), $"ocr_{Guid.NewGuid()}.jpg");
                await File.WriteAllBytesAsync(tempImagePath, request.ImageData);

                // Executar OCR
                var ocrRequest = new Application.DTOs.OcrRequestDto
                {
                    ImagePath = tempImagePath,
                    FileName = request.FileName ?? "front_packaging.jpg",
                    ContentType = request.ContentType ?? "image/jpeg"
                };

                var ocrResult = await _ocrProvider.ExtractTextAsync(ocrRequest);

                if (!ocrResult.Success)
                {
                    _logger.LogWarning("⚠️ OCR falhou: {Error}", ocrResult.ErrorMessage);

                    // ═══════════════════════════════════════════════════════════
                    // FALLBACK 1: OCR falhou → tentar OpenAI Vision
                    // ═══════════════════════════════════════════════════════════
                    if (ProductIdentificationPrioritizer.ShouldUseVisionFallback(request, null, null, _logger))
                    {
                        _logger.LogInformation("🤖 OCR falhou → Tentando OpenAI Vision como fallback");
                        var visionResult = await IdentifyByVisionAsync(tempImagePath);

                        if (visionResult.Success)
                        {
                            stopwatch.Stop();
                            visionResult.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;
                            return visionResult;
                        }
                    }

                    return new ProductIdentificationResult
                    {
                        Success = false,
                        Method = IdentificationMethod.OcrFrontPackaging,
                        MatchSource = MatchSource.FrontOcr,
                        Confidence = 0.0,
                        MatchConfidence = 0.0,
                        ErrorMessage = $"OCR falhou: {ocrResult.ErrorMessage}",
                        ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds
                    };
                }

                _logger.LogInformation("✅ OCR concluído. Confiança: {Confidence:P2}", ocrResult.Confidence);
                _logger.LogInformation("   Texto extraído: {Length} caracteres", ocrResult.RawText.Length);

                // Extrair nome e marca do texto OCR
                var (productName, brand) = ExtractProductNameAndBrand(ocrResult.RawText);

                // Validar extração
                bool hasValidName = MatchConfidenceCalculator.IsValidProductName(productName);
                bool hasValidBrand = MatchConfidenceCalculator.IsValidBrand(brand);

                // ═══════════════════════════════════════════════════════════
                // VERIFICAR SE OCR É SUFICIENTE OU PRECISA DE VISION FALLBACK
                // ═══════════════════════════════════════════════════════════
                bool isOcrSufficient = ProductIdentificationPrioritizer.IsOcrResultSufficient(
                    ocrResult, productName, brand, _logger);

                if (!isOcrSufficient)
                {
                    _logger.LogInformation("⚠️ OCR insuficiente (baixa confiança ou dados incompletos)");

                    // ═══════════════════════════════════════════════════════════
                    // FALLBACK 2: OCR insuficiente → tentar OCR + OpenAI Vision
                    // ═══════════════════════════════════════════════════════════
                    if (ProductIdentificationPrioritizer.ShouldUseVisionFallback(request, ocrResult, productName, _logger))
                    {
                        _logger.LogInformation("🤖 Tentando OpenAI Vision para complementar OCR");

                        var visionResult = await IdentifyByVisionAsync(tempImagePath);

                        if (visionResult.Success || visionResult.MatchedProductName != null)
                        {
                            _logger.LogInformation("✅ Vision forneceu dados adicionais → Consolidando");

                            // Consolidar OCR + Vision
                            var consolidatedResult = ProductIdentificationConsolidator.ConsolidateOcrAndVision(
                                ocrResult,
                                await GetVisionInterpretationResult(tempImagePath),
                                ocrResult.Confidence,
                                _logger);

                            stopwatch.Stop();
                            consolidatedResult.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;

                            _logger.LogInformation("✅ Produto identificado por OCR + OpenAI Vision");
                            LogFinalResult(consolidatedResult);

                            return consolidatedResult;
                        }
                    }
                }

                // OCR suficiente ou Vision fallback falhou → usar apenas OCR
                if (!hasValidName)
                {
                    _logger.LogWarning("⚠️ Nome do produto não identificado ou inválido");

                    return CreateUnknownProductResult(
                        "Nome do produto não identificado no OCR frontal",
                        stopwatch.Elapsed.TotalSeconds);
                }

                _logger.LogInformation("✅ Nome extraído: {Name}", productName);
                if (hasValidBrand)
                    _logger.LogInformation("✅ Marca extraída: {Brand}", brand);

                // Calcular confiança do match
                double matchConfidence = MatchConfidenceCalculator.CalculateMatchConfidence(
                    MatchSource.FrontOcr,
                    isFromExternalDatabase: false,
                    ocrConfidence: ocrResult.Confidence,
                    hasMultipleSources: false);

                stopwatch.Stop();

                return new ProductIdentificationResult
                {
                    Success = true,
                    Method = IdentificationMethod.OcrFrontPackaging,
                    MatchSource = MatchSource.FrontOcr,
                    Confidence = ocrResult.Confidence,
                    MatchConfidence = matchConfidence,
                    MatchedProductName = productName,
                    MatchedBrand = brand,
                    ProductName = productName,
                    Brand = brand,
                    IsFromExternalDatabase = false,
                    IsReliableMatch = MatchConfidenceCalculator.IsReliableMatch(matchConfidence, MatchSource.FrontOcr),
                    ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds,
                    Metadata = new Dictionary<string, string>
                    {
                        ["OcrConfidence"] = ocrResult.Confidence.ToString("F4"),
                        ["MatchConfidenceLabel"] = MatchConfidenceCalculator.GetConfidenceLabel(matchConfidence),
                        ["ExtractedFromOcr"] = "true",
                        ["HasValidName"] = hasValidName.ToString(),
                        ["HasValidBrand"] = hasValidBrand.ToString()
                    },
                    Details = new List<string>
                    {
                        $"Nome extraído: {productName}",
                        hasValidBrand ? $"Marca extraída: {brand}" : "Marca não identificada",
                        $"Confiança OCR: {ocrResult.Confidence:P2}",
                        $"Confiança do match: {matchConfidence:P2} ({MatchConfidenceCalculator.GetConfidenceLabel(matchConfidence)})"
                    }
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(ex, "❌ Erro ao processar OCR frontal");

                return new ProductIdentificationResult
                {
                    Success = false,
                    Method = IdentificationMethod.OcrFrontPackaging,
                    MatchSource = MatchSource.FrontOcr,
                    Confidence = 0.0,
                    MatchConfidence = 0.0,
                    ErrorMessage = $"Erro no OCR frontal: {ex.Message}",
                    ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds
                };
            }
            finally
            {
                // Limpar arquivo temporário
                if (!string.IsNullOrEmpty(tempImagePath) && File.Exists(tempImagePath))
                {
                    try { File.Delete(tempImagePath); }
                    catch { }
                }
            }
        }

        public async Task<Dictionary<string, string>> GetUsageStatisticsAsync()
        {
            await Task.CompletedTask;

            // TODO: Implementar rastreamento de métricas
            return new Dictionary<string, string>
            {
                ["TotalIdentifications"] = "0",
                ["SuccessRate"] = "0.0",
                ["MethodDistribution"] = "{}"
            };
        }

        /// <summary>
        /// Extrai nome do produto e marca de texto OCR.
        /// Implementação simplificada: primeiras linhas são geralmente nome/marca.
        /// </summary>
        private (string? productName, string? brand) ExtractProductNameAndBrand(string ocrText)
        {
            if (string.IsNullOrWhiteSpace(ocrText))
                return (null, null);

            var lines = ocrText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 2)
                .Take(5) // Primeiras 5 linhas
                .ToList();

            if (lines.Count == 0)
                return (null, null);

            // Heurística simples:
            // - Primeira linha com pelo menos 5 caracteres = nome do produto
            // - Segunda linha com pelo menos 3 caracteres = marca (opcional)

            string? productName = lines
                .FirstOrDefault(l => l.Length >= 5 && MatchConfidenceCalculator.IsValidProductName(l));

            string? brand = lines
                .Skip(1)
                .FirstOrDefault(l => l.Length >= 3 && MatchConfidenceCalculator.IsValidBrand(l));

            return (productName, brand);
        }

        /// <summary>
        /// Obtém sugestões de candidatos quando a identificação primária falha.
        /// </summary>
        private async Task<CandidateSuggestionResult> GetCandidateSuggestionsAsync(
            ProductIdentificationRequest request)
        {
            try
            {
                // Tentar extrair texto para usar na sugestão
                string? extractedText = null;
                if (request.CaptureType == CaptureType.FrontPackaging && request.ImageData.Length > 0)
                {
                    var tempPath = Path.Combine(Path.GetTempPath(), $"suggestion_{Guid.NewGuid()}.jpg");
                    try
                    {
                        await File.WriteAllBytesAsync(tempPath, request.ImageData);
                        var ocrResult = await _ocrProvider.ExtractTextAsync(new Application.DTOs.OcrRequestDto
                        {
                            ImagePath = tempPath,
                            FileName = "suggestion_image.jpg",
                            ContentType = "image/jpeg"
                        });
                        if (ocrResult.Success)
                            extractedText = ocrResult.RawText;
                    }
                    finally
                    {
                        if (File.Exists(tempPath))
                            try { File.Delete(tempPath); } catch { }
                    }
                }

                var suggestionRequest = new CandidateSuggestionRequest
                {
                    ExtractedText = extractedText,
                    UserId = request.UserId,
                    LanguageCode = request.LanguageCode,
                    MaxCandidates = 5,
                    MinConfidence = 0.30,
                    ImageData = request.ImageData
                };

                return await _candidateSuggestionService.SuggestCandidatesAsync(suggestionRequest);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao obter sugestões de candidatos");
                return CandidateSuggestionResult.CreateUnknown("Erro ao buscar candidatos");
            }
        }

        private ProductIdentificationResult CreateUnknownProductResult(
            string reason, 
            double processingTime)
        {
            return new ProductIdentificationResult
            {
                Success = false,
                Method = IdentificationMethod.Composite,
                MatchSource = MatchSource.Unknown,
                Confidence = 0.0,
                MatchConfidence = 0.0,
                IsReliableMatch = false,
                ErrorMessage = reason,
                ProcessingTimeSeconds = processingTime,
                Details = new List<string>
                {
                    reason,
                    "Sugestões:",
                    "- Tente capturar o código de barras",
                    "- Tente uma foto mais nítida da embalagem frontal",
                    "- Digite as informações manualmente"
                }
            };
        }

        /// <summary>
        /// Cria resultado de produto desconhecido com candidatos sugeridos.
        /// </summary>
        private ProductIdentificationResult CreateUnknownProductResultWithCandidates(
            string reason,
            double processingTime,
            CandidateSuggestionResult candidateResult)
        {
            var topCandidates = candidateResult.TopCandidates
                .Select(c => new ProductCandidate
                {
                    // ProductId é int? mas candidatos podem ter Guid - deixar null
                    ProductId = null,
                    ProductName = c.CandidateName,
                    Brand = c.CandidateBrand,
                    Category = c.Category,
                    ConfidenceScore = c.CandidateConfidence,
                    MatchSource = c.MatchStrategy switch
                    {
                        CandidateMatchStrategy.TextSimilarity => MatchSource.FrontOcr,
                        CandidateMatchStrategy.VisualSimilarity => MatchSource.Similarity,
                        CandidateMatchStrategy.Combined => MatchSource.Combined,
                        _ => MatchSource.Unknown
                    },
                    MatchReason = c.MatchReason,
                    Barcode = c.Barcode,
                    // Armazenar o GUID no metadata para referência
                    Metadata = c.ProductId.HasValue 
                        ? new Dictionary<string, string> { ["ValidatedProductGuid"] = c.ProductId.Value.ToString() }
                        : new Dictionary<string, string>()
                })
                .ToList();

            var details = new List<string>
            {
                reason,
                candidateResult.HasCandidates
                    ? $"Encontrados {topCandidates.Count} candidato(s) sugerido(s)"
                    : "Nenhum candidato similar encontrado",
                "",
                "Próximos passos:"
            };

            if (candidateResult.HasCandidates)
            {
                details.Add("- Selecione um dos produtos sugeridos");
                details.Add("- Ou capture o código de barras para identificação precisa");
            }
            else
            {
                details.Add("- Tente capturar o código de barras");
                details.Add("- Tente uma foto mais nítida da embalagem frontal");
                details.Add("- Digite as informações manualmente");
            }

            return new ProductIdentificationResult
            {
                Success = false,
                Method = IdentificationMethod.Composite,
                MatchSource = MatchSource.Unknown,
                Confidence = 0.0,
                MatchConfidence = 0.0,
                IsReliableMatch = false,
                ErrorMessage = candidateResult.UserMessage ?? reason,
                ProcessingTimeSeconds = processingTime,
                TopCandidates = topCandidates,
                Details = details,
                Metadata = new Dictionary<string, string>
                {
                    ["HasCandidates"] = candidateResult.HasCandidates.ToString(),
                    ["CandidateCount"] = topCandidates.Count.ToString(),
                    ["StrategiesUsed"] = string.Join(",", candidateResult.StrategiesUsed),
                    ["FallbackReason"] = candidateResult.FallbackReason ?? "Unknown"
                }
            };
        }

        private void LogFinalResult(ProductIdentificationResult result)
        {
            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            _logger.LogInformation("📊 RESULTADO DA IDENTIFICAÇÃO");
            _logger.LogInformation("   Success: {Success}", result.Success);
            _logger.LogInformation("   Method: {Method}", result.Method);
            _logger.LogInformation("   MatchSource: {MatchSource}", result.MatchSource);
            _logger.LogInformation("   Confidence: {Confidence:P2}", result.Confidence);
            _logger.LogInformation("   MatchConfidence: {MatchConfidence:P2}", result.MatchConfidence);
            _logger.LogInformation("   IsReliableMatch: {IsReliable}", result.IsReliableMatch);
            _logger.LogInformation("   TopCandidates: {Count}", result.TopCandidates.Count);

            if (result.Success)
            {
                _logger.LogInformation("   ProductName: {Name}", result.MatchedProductName ?? "N/A");
                _logger.LogInformation("   Brand: {Brand}", result.MatchedBrand ?? "N/A");
                _logger.LogInformation("   Barcode: {Barcode}", result.Barcode ?? "N/A");
            }
            else
            {
                _logger.LogInformation("   Error: {Error}", result.ErrorMessage);
                if (result.TopCandidates.Count > 0)
                {
                    _logger.LogInformation("   Candidatos sugeridos:");
                    foreach (var candidate in result.TopCandidates.Take(3))
                    {
                        _logger.LogInformation("     - {Name} ({Brand}) - {Confidence:P0}",
                            candidate.ProductName, candidate.Brand ?? "N/A", candidate.ConfidenceScore);
                    }
                }
            }

            _logger.LogInformation("   ProcessingTime: {Time:F2}s", result.ProcessingTimeSeconds);
            _logger.LogInformation("═══════════════════════════════════════════════════════════");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // MÉTODOS PRIVADOS - AZURE OPENAI VISION INTEGRATION
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Identifica produto usando Azure OpenAI Vision (GPT-4 Vision).
        /// </summary>
        private async Task<ProductIdentificationResult> IdentifyByVisionAsync(string imagePath)
        {
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("🤖 Executando Azure OpenAI Vision");

            try
            {
                var visionRequest = new VisualInterpretationRequest
                {
                    ImagePath = imagePath
                };

                var visionResult = await _visualInterpreter.InterpretImageAsync(visionRequest);

                stopwatch.Stop();

                // Limpar nome e marca
                var cleanName = ProductIdentificationConsolidator.CleanProductName(visionResult.ProbableProductName);
                var cleanBrand = ProductIdentificationConsolidator.CleanBrand(visionResult.ProbableBrand);

                // Mapear confiança
                double confidence = visionResult.InterpretationConfidence switch
                {
                    ConfidenceLevel.High => 0.85,
                    ConfidenceLevel.Medium => 0.65,
                    ConfidenceLevel.Low => 0.40,
                    _ => 0.20
                };

                bool hasValidName = !string.IsNullOrWhiteSpace(cleanName) && cleanName.Length >= 3;

                _logger.LogInformation("   Vision Name: {Name}", cleanName ?? "N/A");
                _logger.LogInformation("   Vision Brand: {Brand}", cleanBrand ?? "N/A");
                _logger.LogInformation("   Vision Confidence: {Confidence:P2}", confidence);

                if (!hasValidName)
                {
                    _logger.LogWarning("⚠️ Vision não identificou nome válido do produto");

                    return new ProductIdentificationResult
                    {
                        Success = false,
                        Method = IdentificationMethod.VisualRecognition,
                        MatchSource = MatchSource.OpenAiVision,
                        Confidence = confidence,
                        MatchConfidence = 0.0,
                        ErrorMessage = "OpenAI Vision não identificou o produto com confiança suficiente",
                        ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds,
                        Metadata = new Dictionary<string, string>
                        {
                            ["VisionConfidence"] = visionResult.InterpretationConfidence.ToString(),
                            ["Summary"] = visionResult.InterpretationSummary ?? "N/A"
                        }
                    };
                }

                return new ProductIdentificationResult
                {
                    Success = true,
                    Method = IdentificationMethod.VisualRecognition,
                    MatchSource = MatchSource.OpenAiVision,
                    Confidence = confidence,
                    MatchConfidence = confidence,
                    MatchedProductName = cleanName,
                    MatchedBrand = cleanBrand,
                    ProductName = cleanName,
                    Brand = cleanBrand,
                    Category = visionResult.ProbableCategory,
                    IsFromExternalDatabase = false,
                    IsReliableMatch = confidence >= 0.75,
                    ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds,
                    Metadata = new Dictionary<string, string>
                    {
                        ["VisionConfidence"] = visionResult.InterpretationConfidence.ToString(),
                        ["VisionSummary"] = visionResult.InterpretationSummary ?? "N/A",
                        ["Category"] = visionResult.ProbableCategory ?? "N/A"
                    },
                    Details = new List<string>
                    {
                        $"Nome identificado: {cleanName}",
                        cleanBrand != null ? $"Marca identificada: {cleanBrand}" : "Marca não identificada",
                        $"Confiança Vision: {confidence:P2}",
                        $"Resumo: {visionResult.InterpretationSummary ?? "N/A"}"
                    }
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(ex, "❌ Erro ao executar Azure OpenAI Vision");

                return new ProductIdentificationResult
                {
                    Success = false,
                    Method = IdentificationMethod.VisualRecognition,
                    MatchSource = MatchSource.OpenAiVision,
                    Confidence = 0.0,
                    MatchConfidence = 0.0,
                    ErrorMessage = $"Erro no Vision: {ex.Message}",
                    ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds
                };
            }
        }

        /// <summary>
        /// Obtém resultado de interpretação visual (helper para consolidação).
        /// </summary>
        private async Task<VisualInterpretationResult> GetVisionInterpretationResult(string imagePath)
        {
            try
            {
                var visionRequest = new VisualInterpretationRequest
                {
                    ImagePath = imagePath
                };

                return await _visualInterpreter.InterpretImageAsync(visionRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter interpretação visual");
                return new VisualInterpretationResult
                {
                    InterpretationConfidence = ConfidenceLevel.Low
                };
            }
        }

        /// <summary>
        /// Busca o produto no catálogo de produtos conhecidos usando texto extraído por OCR.
        /// Esta é a camada de fallback antes da sugestão de candidatos.
        /// </summary>
        private async Task<ProductIdentificationResult?> SearchInKnownProductsAsync(
            ProductIdentificationRequest request)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Extrair texto para busca (se ainda não feito)
                string? searchQuery = null;

                if (request.CaptureType == CaptureType.FrontPackaging && request.ImageData.Length > 0)
                {
                    var tempPath = Path.Combine(Path.GetTempPath(), $"known_search_{Guid.NewGuid()}.jpg");
                    try
                    {
                        await File.WriteAllBytesAsync(tempPath, request.ImageData);
                        var ocrResult = await _ocrProvider.ExtractTextAsync(new Application.DTOs.OcrRequestDto
                        {
                            ImagePath = tempPath,
                            FileName = "search_image.jpg",
                            ContentType = "image/jpeg"
                        });

                        if (ocrResult.Success && !string.IsNullOrWhiteSpace(ocrResult.RawText))
                        {
                            // Extrair primeiras linhas como query (nome/marca potenciais)
                            var lines = ocrResult.RawText
                                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                .Take(3)
                                .Select(l => l.Trim())
                                .Where(l => l.Length >= 3);

                            searchQuery = string.Join(" ", lines);

                            _logger.LogInformation("🔍 Query extraída para busca: '{Query}'", searchQuery);
                        }
                    }
                    finally
                    {
                        if (File.Exists(tempPath))
                            try { File.Delete(tempPath); } catch { }
                    }
                }

                if (string.IsNullOrWhiteSpace(searchQuery))
                {
                    _logger.LogWarning("⚠️ Não foi possível extrair texto para busca em produtos conhecidos");
                    return null;
                }

                // Buscar no catálogo de produtos conhecidos
                var searchRequest = new Application.DTOs.KnownProducts.KnownProductSearchRequest
                {
                    SearchQuery = searchQuery,
                    MaxResults = 5,
                    MinConfidence = 0.40,
                    EnableFuzzySearch = true,
                    Language = request.LanguageCode
                };

                var searchResponse = await _knownProductSearchService.SearchAsync(searchRequest);

                if (!searchResponse.Success || searchResponse.Results.Count == 0)
                {
                    _logger.LogInformation("ℹ️ Nenhum produto conhecido encontrado para '{Query}'", searchQuery);
                    return null;
                }

                // Pegar o melhor resultado
                var bestMatch = searchResponse.Results.FirstOrDefault();
                if (bestMatch == null)
                    return null;

                _logger.LogInformation(
                    "✅ Produto conhecido encontrado: {Name} - {Brand} (Score: {Score:P2})",
                    bestMatch.Name, bestMatch.Brand, bestMatch.RelevanceScore);

                stopwatch.Stop();

                return new ProductIdentificationResult
                {
                    Success = true,
                    Method = IdentificationMethod.Composite,
                    MatchSource = MatchSource.LocalCatalog,
                    Confidence = 0.75, // Confiança padrão para match em catálogo local
                    MatchConfidence = bestMatch.RelevanceScore,
                    MatchedProductName = bestMatch.Name,
                    MatchedBrand = bestMatch.Brand,
                    ProductName = bestMatch.Name,
                    Brand = bestMatch.Brand,
                    Category = bestMatch.Category,
                    Barcode = bestMatch.Barcode,
                    IsFromExternalDatabase = false,
                    IsReliableMatch = bestMatch.RelevanceScore >= 0.70,
                    ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds,
                    Metadata = new Dictionary<string, string>
                    {
                        ["KnownProductId"] = bestMatch.ProductId.ToString(),
                        ["Source"] = "KnownProductsCatalog",
                        ["SearchQuery"] = searchQuery,
                        ["RelevanceScore"] = bestMatch.RelevanceScore.ToString("F4"),
                        ["MatchReason"] = bestMatch.MatchReason,
                        ["MatchSource"] = bestMatch.MatchSource.ToString(),
                        ["IsValidated"] = bestMatch.IsValidated.ToString(),
                        ["IdentificationCount"] = bestMatch.IdentificationCount.ToString(),
                        ["SearchTimeSeconds"] = searchResponse.SearchTimeSeconds.ToString("F3")
                    },
                    Details = new List<string>
                    {
                        $"Produto encontrado no catálogo local",
                        $"Nome: {bestMatch.Name}",
                        $"Marca: {bestMatch.Brand}",
                        $"Categoria: {bestMatch.Category}",
                        bestMatch.Barcode != null ? $"Código de barras: {bestMatch.Barcode}" : "Código de barras: não disponível",
                        $"Score de relevância: {bestMatch.RelevanceScore:P2}",
                        $"Razão do match: {bestMatch.MatchReason}",
                        $"Produto validado: {(bestMatch.IsValidated ? "Sim" : "Não")}",
                        $"Identificações anteriores: {bestMatch.IdentificationCount}",
                        $"Tempo de busca: {searchResponse.SearchTimeSeconds:F3}s"
                    },
                    // Incluir outros candidatos como TopCandidates
                    TopCandidates = searchResponse.Results
                        .Skip(1) // Pular o primeiro (já é o match principal)
                        .Select(r => new ProductCandidate
                        {
                            ProductName = r.Name,
                            Brand = r.Brand,
                            Category = r.Category,
                            Barcode = r.Barcode,
                            ConfidenceScore = r.RelevanceScore,
                            MatchSource = MatchSource.LocalCatalog,
                            MatchReason = r.MatchReason,
                            Metadata = new Dictionary<string, string>
                            {
                                ["ValidatedProductGuid"] = r.ProductId.ToString(),
                                ["IsValidated"] = r.IsValidated.ToString()
                            }
                        })
                        .ToList()
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(ex, "❌ Erro ao buscar em produtos conhecidos");
                return null;
            }
        }
    }
}
