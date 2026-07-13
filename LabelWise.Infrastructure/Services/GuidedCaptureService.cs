using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LabelWise.Application.DTOs.GuidedCapture;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Parsing;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Enums;
using LabelWise.Infrastructure.Storage;

namespace LabelWise.Infrastructure.Services
{
    /// <summary>
    /// Implementação do serviço de captura guiada para apps mobile.
    /// Orquestra o fluxo de múltiplas capturas até a análise final.
    /// </summary>
    public class GuidedCaptureService : IGuidedCaptureService
    {
        private readonly ICapturePersistenceService _capturePersistence;
        private readonly IProductAnalysisPipelineOrchestrator _pipelineOrchestrator;
        private readonly IOcrProvider _ocrProvider;
        private readonly IIngredientAllergenParser _parser;
        private readonly IFileStorage _fileStorage;
        private readonly IProductAnalysisSessionRepository _sessionRepository;
        private readonly IProductCaptureRepository _captureRepository;
        private readonly IProductRepository _productRepository;
        private readonly IAnalysisWriteRepository _analysisWriteRepository;
        private readonly ILogger<GuidedCaptureService> _logger;

        private static readonly Dictionary<CaptureType, CaptureStepDefinition> _stepDefinitions = new()
        {
            [CaptureType.FrontPackaging] = new CaptureStepDefinition
            {
                CaptureType = CaptureType.FrontPackaging,
                Order = 1,
                IsRequired = false,
                IconName = "package",
                NamePt = "Embalagem Frontal",
                NameEn = "Front Packaging",
                DescriptionPt = "Fotografe a parte frontal da embalagem com o nome e marca do produto",
                DescriptionEn = "Take a photo of the front packaging with product name and brand",
                TipsPt = ["Certifique-se de que o nome do produto esteja legível", "Inclua a marca se visível", "Evite reflexos"],
                TipsEn = ["Make sure the product name is readable", "Include the brand if visible", "Avoid reflections"]
            },
            [CaptureType.IngredientsList] = new CaptureStepDefinition
            {
                CaptureType = CaptureType.IngredientsList,
                Order = 2,
                IsRequired = true,
                IconName = "list",
                NamePt = "Lista de Ingredientes",
                NameEn = "Ingredients List",
                DescriptionPt = "Fotografe a lista de ingredientes completa",
                DescriptionEn = "Take a photo of the complete ingredients list",
                TipsPt = ["Enquadre toda a lista de ingredientes", "Mantenha a câmera paralela ao rótulo", "Use boa iluminação"],
                TipsEn = ["Frame the entire ingredients list", "Keep the camera parallel to the label", "Use good lighting"]
            },
            [CaptureType.NutritionTable] = new CaptureStepDefinition
            {
                CaptureType = CaptureType.NutritionTable,
                Order = 3,
                IsRequired = true,
                IconName = "table",
                NamePt = "Tabela Nutricional",
                NameEn = "Nutrition Table",
                DescriptionPt = "Fotografe a tabela de informação nutricional",
                DescriptionEn = "Take a photo of the nutrition facts table",
                TipsPt = ["Enquadre toda a tabela nutricional", "Certifique-se de que os valores estejam legíveis", "Evite sombras"],
                TipsEn = ["Frame the entire nutrition table", "Make sure values are readable", "Avoid shadows"]
            },
            [CaptureType.AllergenStatement] = new CaptureStepDefinition
            {
                CaptureType = CaptureType.AllergenStatement,
                Order = 4,
                IsRequired = false,
                IconName = "warning",
                NamePt = "Declaração de Alérgenos",
                NameEn = "Allergen Statement",
                DescriptionPt = "Fotografe a declaração de alérgenos (se houver)",
                DescriptionEn = "Take a photo of the allergen statement (if present)",
                TipsPt = ["Procure por 'Contém:' ou 'Pode conter:'", "Inclua todo o texto de alérgenos", "Se não houver, pode pular esta etapa"],
                TipsEn = ["Look for 'Contains:' or 'May contain:'", "Include all allergen text", "If none present, you can skip this step"]
            },
            [CaptureType.Barcode] = new CaptureStepDefinition
            {
                CaptureType = CaptureType.Barcode,
                Order = 5,
                IsRequired = false,
                IconName = "barcode",
                NamePt = "Código de Barras",
                NameEn = "Barcode",
                DescriptionPt = "Escaneie o código de barras do produto (opcional)",
                DescriptionEn = "Scan the product barcode (optional)",
                TipsPt = ["Posicione o código de barras no centro", "Mantenha o celular estável", "O código pode ser inserido manualmente"],
                TipsEn = ["Position the barcode in the center", "Keep the phone steady", "The code can be entered manually"]
            }
        };

        public GuidedCaptureService(
            ICapturePersistenceService capturePersistence,
            IProductAnalysisPipelineOrchestrator pipelineOrchestrator,
            IOcrProvider ocrProvider,
            IIngredientAllergenParser parser,
            IFileStorage fileStorage,
            IProductAnalysisSessionRepository sessionRepository,
            IProductCaptureRepository captureRepository,
            IProductRepository productRepository,
            IAnalysisWriteRepository analysisWriteRepository,
            ILogger<GuidedCaptureService> logger)
        {
            _capturePersistence = capturePersistence;
            _pipelineOrchestrator = pipelineOrchestrator;
            _ocrProvider = ocrProvider;
            _parser = parser;
            _fileStorage = fileStorage;
            _sessionRepository = sessionRepository;
            _captureRepository = captureRepository;
            _productRepository = productRepository;
            _analysisWriteRepository = analysisWriteRepository;
            _logger = logger;
        }

        public async Task<StartGuidedSessionResponse> StartSessionAsync(
            StartGuidedSessionRequest request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting new guided capture session for user: {UserId}", request.UserId);

            var session = await _capturePersistence.StartSessionAsync(request.UserId, cancellationToken);

            var steps = GetCaptureStepDefinitions(request.LanguageCode);
            var firstStep = GetNextStepRecommendation([], request.LanguageCode);

            return new StartGuidedSessionResponse
            {
                SessionId = session.Id,
                Status = session.Status.ToString(),
                StartedAt = session.StartedAt,
                FirstStep = firstStep,
                AllSteps = steps,
                SessionTimeoutMinutes = 30,
                WelcomeMessage = request.LanguageCode.StartsWith("pt")
                    ? "Vamos analisar o produto! Siga as etapas para capturar as informações do rótulo."
                    : "Let's analyze the product! Follow the steps to capture the label information."
            };
        }

        public async Task<GuidedCaptureSessionDto?> GetSessionStatusAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            var session = await _sessionRepository.GetByIdWithCapturesAsync(sessionId, cancellationToken);

            if (session is null)
                return null;

            return MapToSessionDto(session);
        }

        public async Task<AddCaptureResponse> AddCaptureAsync(
            Guid sessionId,
            CaptureType captureType,
            Stream? imageStream,
            string? fileName,
            string? barcode,
            string languageCode = "pt",
            bool enableMultiProviderOcr = true,
            bool enableExternalLookup = true,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = new AddCaptureResponse
            {
                CaptureType = captureType,
                Warnings = []
            };

            try
            {
                // Validate session exists and is active
                var session = await _sessionRepository.GetByIdWithCapturesAsync(sessionId, cancellationToken);

                if (session is null)
                {
                    response.ErrorMessage = "Sessão não encontrada";
                    return response;
                }

                if (session.Status is SessionStatus.Completed or SessionStatus.Failed or SessionStatus.Cancelled)
                {
                    response.ErrorMessage = "Sessão não está ativa";
                    return response;
                }

                // Validate input based on capture type
                if (captureType == CaptureType.Barcode)
                {
                    if (string.IsNullOrWhiteSpace(barcode))
                    {
                        response.ErrorMessage = "Código de barras é obrigatório para este tipo de captura";
                        return response;
                    }
                }
                else
                {
                    if (imageStream is null || imageStream.Length == 0)
                    {
                        response.ErrorMessage = "Arquivo de imagem é obrigatório";
                        return response;
                    }
                }

                // Process the capture
                string imagePath = string.Empty;
                string extractedText = string.Empty;
                decimal confidence = 0m;
                CaptureExtractedDataDto? extractedData = null;
                string? parsedDataJson = null;

                if (captureType == CaptureType.Barcode)
                {
                    // Barcode capture - no OCR needed
                    extractedText = barcode!;
                    confidence = 1.0m;
                    extractedData = new CaptureExtractedDataDto
                    {
                        Barcode = barcode
                    };

                    // Update session with detected barcode
                    session.SetDetectedBarcode(barcode!);
                }
                else
                {
                    // Image capture - save image first, then run OCR
                    imagePath = await SaveImageAsync(imageStream!, sessionId, captureType, fileName, cancellationToken);

                    var ocrRequest = new Application.DTOs.OcrRequestDto
                    {
                        ImagePath = imagePath,
                        FileName = fileName ?? "image.jpg",
                        ContentType = GetContentType(fileName)
                    };
                    var ocrResult = await _ocrProvider.ExtractTextAsync(ocrRequest);

                    extractedText = ocrResult.RawText ?? string.Empty;
                    confidence = (decimal)ocrResult.Confidence;

                    // Parse based on capture type
                    extractedData = await ParseCaptureDataAsync(captureType, extractedText, languageCode);
                    parsedDataJson = JsonSerializer.Serialize(extractedData);

                    // Quality checks
                    if (confidence < 0.5m)
                    {
                        response.Warnings.Add("Qualidade da imagem pode afetar a precisão da análise");
                        response.ImprovementSuggestion = "Tente capturar a imagem com melhor iluminação e mais próximo do rótulo";
                    }

                    if (string.IsNullOrWhiteSpace(extractedText))
                    {
                        response.Warnings.Add("Não foi possível extrair texto da imagem");
                        response.ImprovementSuggestion = "Certifique-se de que o texto está visível e legível na imagem";
                    }
                }

                // Save the capture
                var capture = await _capturePersistence.SaveCaptureAsync(
                    new SaveCaptureRequest(
                        sessionId,
                        captureType,
                        imagePath,
                        _ocrProvider.GetType().Name,
                        extractedText,
                        confidence,
                        (int)stopwatch.ElapsedMilliseconds,
                        parsedDataJson),
                    cancellationToken);

                stopwatch.Stop();

                response.Success = true;
                response.CaptureId = capture.Id;
                response.Confidence = confidence;
                response.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;
                response.ExtractedData = extractedData;
                response.SessionStatus = MapToSessionDto(session);

                _logger.LogInformation(
                    "Capture added successfully. Session: {SessionId}, Type: {CaptureType}, Confidence: {Confidence}",
                    sessionId, captureType, confidence);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding capture to session {SessionId}", sessionId);
                response.ErrorMessage = "Erro ao processar captura: " + ex.Message;
            }

            return response;
        }

        public async Task<GuidedCaptureSessionDto?> RemoveCaptureAsync(
            Guid sessionId,
            Guid captureId,
            CancellationToken cancellationToken = default)
        {
            var capture = await _captureRepository.GetByIdAsync(captureId, cancellationToken);

            if (capture is null || capture.SessionId != sessionId)
                return null;

            // Delete image file if exists
            if (!string.IsNullOrEmpty(capture.ImagePath))
            {
                await _fileStorage.DeleteAsync(capture.ImagePath);
            }

            await _captureRepository.DeleteAsync(capture.Id, cancellationToken);

            return await GetSessionStatusAsync(sessionId, cancellationToken);
        }

        public async Task<FinalizeAnalysisResponse> FinalizeAnalysisAsync(
            FinalizeAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = new FinalizeAnalysisResponse
            {
                SessionId = request.SessionId,
                Metadata = new Application.DTOs.Analysis.AnalysisMetadataDto { StartTime = DateTime.UtcNow },
                Warnings = []
            };

            try
            {
                var session = await _sessionRepository.GetByIdWithCapturesAsync(request.SessionId, cancellationToken);

                if (session is null)
                {
                    response.ErrorMessage = "Sessão não encontrada";
                    return response;
                }

                var progress = CalculateProgress(session.Captures.ToList());

                // Check if required steps are complete
                if (!request.ForceAnalysis && !progress.RequiredStepsComplete)
                {
                    response.ErrorMessage = "Etapas obrigatórias não foram completadas. Capture a tabela nutricional e lista de ingredientes.";
                    response.Warnings.Add($"Progresso atual: {progress.PercentComplete}%");
                    return response;
                }

                // Mark session as processing
                session.StartProcessing();
                await _sessionRepository.UpdateAsync(session, cancellationToken);

                // Consolidate data from all captures
                var consolidatedProduct = await ConsolidateProductDataAsync(session.Captures.ToList(), cancellationToken);

                // Generate nutritional analysis
                var nutritionalAnalysis = await GenerateNutritionalAnalysisAsync(consolidatedProduct, cancellationToken);

                // Generate summary
                var summary = GenerateAnalysisSummary(nutritionalAnalysis, consolidatedProduct);

                // Generate alerts and recommendations
                var alerts = GenerateAlerts(nutritionalAnalysis, consolidatedProduct);
                var recommendations = GenerateRecommendations(nutritionalAnalysis, consolidatedProduct, request.IncludePersonalizedRecommendations);

                // Calculate overall confidence
                var overallConfidence = CalculateOverallConfidence(session.Captures.ToList());

                // Create or update product in database
                var productId = await SaveProductAsync(consolidatedProduct, cancellationToken);

                // Create analysis record
                var analysisId = await SaveAnalysisAsync(productId, nutritionalAnalysis, session.Id, cancellationToken);

                // Complete session
                await _capturePersistence.CompleteSessionAsync(
                    session.Id,
                    productId,
                    analysisId,
                    overallConfidence,
                    cancellationToken);

                stopwatch.Stop();

                response.Success = true;
                response.AnalysisId = analysisId;
                response.ProductId = productId;
                response.Product = consolidatedProduct;
                response.NutritionalAnalysis = nutritionalAnalysis;
                response.Summary = summary;
                response.Alerts = alerts;
                response.Recommendations = recommendations;
                response.OverallConfidence = overallConfidence;
                response.ConfidenceBreakdown = CalculateConfidenceBreakdown(session.Captures.ToList());
                response.Metadata.TotalProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                response.Metadata.EndTime = DateTime.UtcNow;

                _logger.LogInformation(
                    "Analysis finalized successfully. Session: {SessionId}, ProductId: {ProductId}, Score: {Score}",
                    request.SessionId, productId, nutritionalAnalysis.OverallScore);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finalizing analysis for session {SessionId}", request.SessionId);
                response.ErrorMessage = "Erro ao finalizar análise: " + ex.Message;

                // Mark session as failed
                await _capturePersistence.FailSessionAsync(request.SessionId, ex.Message, cancellationToken);
            }

            return response;
        }

        public async Task<bool> CancelSessionAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            var session = await _sessionRepository.GetByIdWithCapturesAsync(sessionId, cancellationToken);

            if (session is null)
                return false;

            // Delete all capture images
            foreach (var capture in session.Captures)
            {
                if (!string.IsNullOrEmpty(capture.ImagePath))
                {
                    await _fileStorage.DeleteAsync(capture.ImagePath);
                }
            }

            session.Cancel();
            await _sessionRepository.UpdateAsync(session, cancellationToken);

            _logger.LogInformation("Session {SessionId} cancelled", sessionId);
            return true;
        }

        public List<CaptureStepDefinitionDto> GetCaptureStepDefinitions(string languageCode = "pt")
        {
            var isPt = languageCode.StartsWith("pt", StringComparison.OrdinalIgnoreCase);

            return _stepDefinitions.Values
                .OrderBy(s => s.Order)
                .Select(s => new CaptureStepDefinitionDto
                {
                    CaptureType = s.CaptureType,
                    Name = isPt ? s.NamePt : s.NameEn,
                    Description = isPt ? s.DescriptionPt : s.DescriptionEn,
                    Order = s.Order,
                    IsRequired = s.IsRequired,
                    IconName = s.IconName,
                    Tips = isPt ? s.TipsPt : s.TipsEn
                })
                .ToList();
        }

        public async Task<NextStepRecommendationDto?> GetNextStepRecommendationAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            var session = await _sessionRepository.GetByIdWithCapturesAsync(sessionId, cancellationToken);

            if (session is null)
                return null;

            var completedTypes = session.Captures.Select(c => c.CaptureType).ToHashSet();
            return GetNextStepRecommendation(completedTypes);
        }

        #region Private Methods

        private GuidedCaptureSessionDto MapToSessionDto(ProductAnalysisSession session)
        {
            var captures = session.Captures.ToList();
            var progress = CalculateProgress(captures);
            var completedTypes = captures.Select(c => c.CaptureType).ToHashSet();

            return new GuidedCaptureSessionDto
            {
                SessionId = session.Id,
                Status = session.Status.ToString(),
                StartedAt = session.StartedAt,
                CompletedAt = session.CompletedAt,
                Progress = progress,
                NextStep = GetNextStepRecommendation(completedTypes),
                DetectedBarcode = session.DetectedBarcode,
                ProductFromCache = session.ProductFromCache,
                CurrentConfidence = session.OverallConfidence > 0 ? session.OverallConfidence : CalculateCurrentConfidence(captures),
                CompletedCaptures = captures.Select(MapToCaptureStepResult).ToList(),
                ErrorMessage = session.ErrorMessage
            };
        }

        private static CaptureStepsProgressDto CalculateProgress(List<ProductCapture> captures)
        {
            var capturedTypes = captures.Select(c => c.CaptureType).ToHashSet();

            var progress = new CaptureStepsProgressDto
            {
                TotalSteps = 5,
                FrontPackagingCaptured = capturedTypes.Contains(CaptureType.FrontPackaging),
                IngredientsListCaptured = capturedTypes.Contains(CaptureType.IngredientsList),
                NutritionTableCaptured = capturedTypes.Contains(CaptureType.NutritionTable),
                AllergenStatementCaptured = capturedTypes.Contains(CaptureType.AllergenStatement),
                BarcodeCaptured = capturedTypes.Contains(CaptureType.Barcode)
            };

            progress.CompletedSteps = capturedTypes.Count;

            return progress;
        }

        private static CaptureStepResultDto MapToCaptureStepResult(ProductCapture capture)
        {
            var def = _stepDefinitions.GetValueOrDefault(capture.CaptureType);

            return new CaptureStepResultDto
            {
                CaptureId = capture.Id,
                CaptureType = capture.CaptureType,
                CaptureTypeName = def?.NamePt ?? capture.CaptureType.ToString(),
                Success = !string.IsNullOrEmpty(capture.ExtractedText) || capture.CaptureType == CaptureType.Barcode,
                Confidence = capture.Confidence,
                CapturedAt = capture.CapturedAt,
                ProcessingTimeMs = capture.ProcessingTimeMs,
                ExtractedSummary = GetExtractedSummary(capture),
                CanRetry = true
            };
        }

        private static string? GetExtractedSummary(ProductCapture capture)
        {
            if (string.IsNullOrEmpty(capture.ExtractedText))
                return null;

            var text = capture.ExtractedText;
            return text.Length > 100 ? text[..100] + "..." : text;
        }

        private NextStepRecommendationDto GetNextStepRecommendation(
            HashSet<CaptureType> completedTypes,
            string languageCode = "pt")
        {
            var isPt = languageCode.StartsWith("pt", StringComparison.OrdinalIgnoreCase);

            // Priority order: Required steps first, then optional
            var orderedSteps = _stepDefinitions.Values
                .OrderByDescending(s => s.IsRequired)
                .ThenBy(s => s.Order)
                .ToList();

            foreach (var step in orderedSteps)
            {
                if (!completedTypes.Contains(step.CaptureType))
                {
                    return new NextStepRecommendationDto
                    {
                        CaptureType = step.CaptureType,
                        StepName = isPt ? step.NamePt : step.NameEn,
                        Description = isPt ? step.DescriptionPt : step.DescriptionEn,
                        Tips = isPt ? step.TipsPt : step.TipsEn,
                        IsRequired = step.IsRequired,
                        SuggestedOrder = step.Order
                    };
                }
            }

            // All steps completed
            return new NextStepRecommendationDto
            {
                CaptureType = CaptureType.NutritionTable, // Default
                StepName = isPt ? "Análise Pronta" : "Ready for Analysis",
                Description = isPt ? "Todas as etapas foram completadas! Finalize a análise." : "All steps completed! Finalize the analysis.",
                Tips = [],
                IsRequired = false,
                SuggestedOrder = 99
            };
        }

        private async Task<string> SaveImageAsync(
            Stream stream,
            Guid sessionId,
            CaptureType captureType,
            string? originalFileName,
            CancellationToken cancellationToken)
        {
            var extension = Path.GetExtension(originalFileName ?? ".jpg");
            var fileName = $"{sessionId}/{captureType}_{Guid.NewGuid()}{extension}";

            return await _fileStorage.SaveTempAsync(stream, fileName);
        }

        private static string GetContentType(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "image/jpeg";

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                _ => "image/jpeg"
            };
        }

        private async Task<CaptureExtractedDataDto> ParseCaptureDataAsync(
            CaptureType captureType,
            string extractedText,
            string languageCode)
        {
            var data = new CaptureExtractedDataDto
            {
                RawTextSummary = extractedText.Length > 200 ? extractedText[..200] + "..." : extractedText
            };

            try
            {
                switch (captureType)
                {
                    case CaptureType.IngredientsList:
                        var parseResult = _parser.Parse(extractedText);
                        data.IngredientsCount = parseResult.Ingredients.Count;
                        data.MainIngredients = parseResult.Ingredients.Take(5).ToList();
                        data.Allergens = parseResult.Allergens.ToList();
                        break;

                    case CaptureType.NutritionTable:
                        data.NutritionPreview = ParseNutritionPreview(extractedText);
                        break;

                    case CaptureType.AllergenStatement:
                        var allergenParse = _parser.Parse(extractedText);
                        data.Allergens = allergenParse.Allergens.ToList();
                        break;

                    case CaptureType.FrontPackaging:
                        data.Claims = ExtractClaims(extractedText);
                        data.ProductName = ExtractProductName(extractedText);
                        data.Brand = ExtractBrand(extractedText);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing capture data for type {CaptureType}", captureType);
            }

            return await Task.FromResult(data);
        }

        private static NutritionPreviewDto ParseNutritionPreview(string text)
        {
            var preview = new NutritionPreviewDto();

            // Simple regex-based extraction for preview
            var patterns = new Dictionary<string, Action<decimal>>
            {
                [@"calorias?\s*[:\-]?\s*(\d+(?:[,\.]\d+)?)\s*(?:kcal)?"] = v => preview.Calories = v,
                [@"carboidratos?\s*[:\-]?\s*(\d+(?:[,\.]\d+)?)\s*g"] = v => preview.Carbohydrates = v,
                [@"prote[ií]nas?\s*[:\-]?\s*(\d+(?:[,\.]\d+)?)\s*g"] = v => preview.Proteins = v,
                [@"gorduras?\s+totai?s?\s*[:\-]?\s*(\d+(?:[,\.]\d+)?)\s*g"] = v => preview.TotalFat = v,
                [@"s[oó]dio\s*[:\-]?\s*(\d+(?:[,\.]\d+)?)\s*mg"] = v => preview.Sodium = v,
                [@"a[çc][uú]cares?\s*[:\-]?\s*(\d+(?:[,\.]\d+)?)\s*g"] = v => preview.Sugars = v
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    text,
                    pattern.Key,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (match.Success && decimal.TryParse(
                    match.Groups[1].Value.Replace(',', '.'),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var value))
                {
                    pattern.Value(value);
                }
            }

            // Extract serving size
            var servingMatch = System.Text.RegularExpressions.Regex.Match(
                text,
                @"por[çc][ãa]o\s*[:\-]?\s*(\d+(?:[,\.]\d+)?\s*(?:g|ml))",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (servingMatch.Success)
            {
                preview.ServingSize = servingMatch.Groups[1].Value;
            }

            return preview;
        }

        private static List<string> ExtractClaims(string text)
        {
            var claims = new List<string>();
            var claimPatterns = new[]
            {
                "sem glúten", "gluten free", "sem lactose", "lactose free",
                "zero açúcar", "zero sugar", "light", "diet", "integral",
                "orgânico", "organic", "natural", "sem conservantes",
                "fonte de fibras", "rico em proteína", "baixo teor de sódio",
                "sem gordura trans", "vegano", "vegan", "vegetariano"
            };

            foreach (var claim in claimPatterns)
            {
                if (text.Contains(claim, StringComparison.OrdinalIgnoreCase))
                {
                    claims.Add(claim);
                }
            }

            return claims;
        }

        private static string? ExtractProductName(string text)
        {
            // Simple heuristic: first line that looks like a product name
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines.Take(3))
            {
                var trimmed = line.Trim();
                if (trimmed.Length is > 3 and < 100 && !trimmed.All(char.IsDigit))
                {
                    return trimmed;
                }
            }
            return null;
        }

        private static string? ExtractBrand(string text)
        {
            // Look for common brand indicators
            var brandPatterns = new[] { @"marca\s*[:\-]?\s*(\w+)", @"by\s+(\w+)", @"®\s*(\w+)" };

            foreach (var pattern in brandPatterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            return null;
        }

        private static decimal CalculateCurrentConfidence(List<ProductCapture> captures)
        {
            if (captures.Count == 0)
                return 0m;

            return captures.Average(c => c.Confidence);
        }

        private static decimal CalculateOverallConfidence(List<ProductCapture> captures)
        {
            if (captures.Count == 0)
                return 0m;

            // Weight required captures more heavily
            var weightedSum = 0m;
            var totalWeight = 0m;

            foreach (var capture in captures)
            {
                var weight = capture.CaptureType is CaptureType.NutritionTable or CaptureType.IngredientsList
                    ? 2m
                    : 1m;

                weightedSum += capture.Confidence * weight;
                totalWeight += weight;
            }

            return totalWeight > 0 ? weightedSum / totalWeight : 0m;
        }

        private static ConfidenceBreakdownDto CalculateConfidenceBreakdown(List<ProductCapture> captures)
        {
            var ocrConfidences = captures.Select(c => c.Confidence).ToList();

            return new ConfidenceBreakdownDto
            {
                OcrConfidence = ocrConfidences.Count > 0 ? ocrConfidences.Average() : 0m,
                ParsingConfidence = ocrConfidences.Count > 0 ? ocrConfidences.Average() * 0.9m : 0m, // Slightly lower
                DataCompletenessConfidence = CalculateCompletenessConfidence(captures),
                AnalysisConfidence = 0.85m // Will be updated after analysis
            };
        }

        private static decimal CalculateCompletenessConfidence(List<ProductCapture> captures)
        {
            var capturedTypes = captures.Select(c => c.CaptureType).ToHashSet();
            var score = 0m;

            if (capturedTypes.Contains(CaptureType.NutritionTable)) score += 0.35m;
            if (capturedTypes.Contains(CaptureType.IngredientsList)) score += 0.35m;
            if (capturedTypes.Contains(CaptureType.FrontPackaging)) score += 0.1m;
            if (capturedTypes.Contains(CaptureType.AllergenStatement)) score += 0.1m;
            if (capturedTypes.Contains(CaptureType.Barcode)) score += 0.1m;

            return score;
        }

        private async Task<ConsolidatedProductDto> ConsolidateProductDataAsync(
            List<ProductCapture> captures,
            CancellationToken cancellationToken)
        {
            var product = new ConsolidatedProductDto
            {
                ProductId = Guid.NewGuid(),
                DataSource = "OCR",
                Ingredients = [],
                Allergens = [],
                Claims = []
            };

            foreach (var capture in captures)
            {
                if (string.IsNullOrEmpty(capture.ParsedDataJson))
                    continue;

                try
                {
                    var data = JsonSerializer.Deserialize<CaptureExtractedDataDto>(capture.ParsedDataJson);
                    if (data is null) continue;

                    switch (capture.CaptureType)
                    {
                        case CaptureType.FrontPackaging:
                            product.Name = data.ProductName ?? product.Name;
                            product.Brand = data.Brand ?? product.Brand;
                            if (data.Claims is not null)
                                product.Claims.AddRange(data.Claims);
                            break;

                        case CaptureType.IngredientsList:
                            if (data.MainIngredients is not null)
                                product.Ingredients.AddRange(data.MainIngredients);
                            if (data.Allergens is not null)
                                product.Allergens.AddRange(data.Allergens);
                            break;

                        case CaptureType.NutritionTable:
                            if (data.NutritionPreview is not null)
                            {
                                product.NutritionalInfo = new GuidedCaptureNutritionalInfoDto
                                {
                                    ServingSize = data.NutritionPreview.ServingSize,
                                    Calories = data.NutritionPreview.Calories,
                                    TotalCarbohydrates = data.NutritionPreview.Carbohydrates,
                                    Proteins = data.NutritionPreview.Proteins,
                                    TotalFat = data.NutritionPreview.TotalFat,
                                    Sodium = data.NutritionPreview.Sodium,
                                    Sugars = data.NutritionPreview.Sugars
                                };
                            }
                            break;

                        case CaptureType.AllergenStatement:
                            if (data.Allergens is not null)
                                product.Allergens.AddRange(data.Allergens);
                            break;

                        case CaptureType.Barcode:
                            product.Barcode = data.Barcode;
                            break;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Error deserializing parsed data for capture {CaptureId}", capture.Id);
                }
            }

            // Deduplicate
            product.Ingredients = product.Ingredients.Distinct().ToList();
            product.Allergens = product.Allergens.Distinct().ToList();
            product.Claims = product.Claims.Distinct().ToList();

            // Set default name if not found
            if (string.IsNullOrEmpty(product.Name))
            {
                product.Name = "Produto não identificado";
            }

            return await Task.FromResult(product);
        }

        private Task<NutritionalAnalysisResultDto> GenerateNutritionalAnalysisAsync(
            ConsolidatedProductDto product,
            CancellationToken cancellationToken)
        {
            var analysis = new NutritionalAnalysisResultDto
            {
                CategoryScores = new CategoryScoresDto(),
                TrafficLight = new NutritionalTrafficLightDto()
            };

            var info = product.NutritionalInfo;
            if (info is null)
            {
                analysis.OverallScore = 50;
                analysis.Classification = "Indeterminado";
                analysis.IndicatorColor = "gray";
                return Task.FromResult(analysis);
            }

            // Calculate scores based on nutritional values
            var scores = new List<int>();

            // Sugar score (lower is better)
            if (info.Sugars.HasValue)
            {
                var sugarScore = info.Sugars.Value switch
                {
                    <= 5 => 90,
                    <= 10 => 70,
                    <= 15 => 50,
                    <= 22.5m => 30,
                    _ => 10
                };
                analysis.CategoryScores.SugarScore = sugarScore;
                analysis.TrafficLight.SugarsLevel = sugarScore >= 70 ? "Green" : sugarScore >= 40 ? "Yellow" : "Red";
                scores.Add(sugarScore);
            }

            // Sodium score (lower is better)
            if (info.Sodium.HasValue)
            {
                var sodiumScore = info.Sodium.Value switch
                {
                    <= 120 => 90,
                    <= 300 => 70,
                    <= 600 => 50,
                    <= 1000 => 30,
                    _ => 10
                };
                analysis.CategoryScores.SodiumScore = sodiumScore;
                analysis.TrafficLight.SaltLevel = sodiumScore >= 70 ? "Green" : sodiumScore >= 40 ? "Yellow" : "Red";
                scores.Add(sodiumScore);
            }

            // Saturated fat score
            if (info.SaturatedFat.HasValue)
            {
                var satFatScore = info.SaturatedFat.Value switch
                {
                    <= 1.5m => 90,
                    <= 3 => 70,
                    <= 5 => 50,
                    _ => 30
                };
                analysis.CategoryScores.SaturatedFatScore = satFatScore;
                analysis.TrafficLight.SaturatesLevel = satFatScore >= 70 ? "Green" : satFatScore >= 40 ? "Yellow" : "Red";
                scores.Add(satFatScore);
            }

            // Total fat
            if (info.TotalFat.HasValue)
            {
                analysis.TrafficLight.FatLevel = info.TotalFat.Value switch
                {
                    <= 3 => "Green",
                    <= 17.5m => "Yellow",
                    _ => "Red"
                };
            }

            // Fiber score (higher is better)
            if (info.DietaryFiber.HasValue)
            {
                var fiberScore = info.DietaryFiber.Value switch
                {
                    >= 6 => 90,
                    >= 3 => 70,
                    >= 1 => 50,
                    _ => 30
                };
                analysis.CategoryScores.FiberScore = fiberScore;
                scores.Add(fiberScore);
            }

            // Protein score (higher is better)
            if (info.Proteins.HasValue)
            {
                var proteinScore = info.Proteins.Value switch
                {
                    >= 10 => 90,
                    >= 5 => 70,
                    >= 2 => 50,
                    _ => 30
                };
                analysis.CategoryScores.ProteinScore = proteinScore;
                scores.Add(proteinScore);
            }

            // Calculate overall score
            analysis.OverallScore = scores.Count > 0 ? (int)scores.Average() : 50;

            // Classification and color
            analysis.Classification = analysis.OverallScore switch
            {
                >= 80 => "Excelente",
                >= 60 => "Bom",
                >= 40 => "Moderado",
                _ => "Ruim"
            };

            analysis.IndicatorColor = analysis.OverallScore switch
            {
                >= 80 => "green",
                >= 60 => "yellow",
                >= 40 => "orange",
                _ => "red"
            };

            // Nutri-Score approximation
            analysis.NutriScore = analysis.OverallScore switch
            {
                >= 80 => "A",
                >= 60 => "B",
                >= 40 => "C",
                >= 20 => "D",
                _ => "E"
            };

            return Task.FromResult(analysis);
        }

        private static AnalysisSummaryDto GenerateAnalysisSummary(
            NutritionalAnalysisResultDto analysis,
            ConsolidatedProductDto product)
        {
            var summary = new AnalysisSummaryDto
            {
                Title = $"Análise de {product.Name}",
                Positives = [],
                Concerns = []
            };

            // Generate short description
            summary.ShortDescription = analysis.OverallScore switch
            {
                >= 80 => "Este produto apresenta um excelente perfil nutricional.",
                >= 60 => "Este produto tem um bom perfil nutricional com alguns pontos de atenção.",
                >= 40 => "Este produto requer moderação no consumo.",
                _ => "Este produto deve ser consumido com cautela."
            };

            // Positives
            if (analysis.CategoryScores?.SugarScore >= 70)
                summary.Positives.Add("Baixo teor de açúcar");
            if (analysis.CategoryScores?.SodiumScore >= 70)
                summary.Positives.Add("Baixo teor de sódio");
            if (analysis.CategoryScores?.FiberScore >= 70)
                summary.Positives.Add("Boa fonte de fibras");
            if (analysis.CategoryScores?.ProteinScore >= 70)
                summary.Positives.Add("Rico em proteínas");
            if (product.Claims.Any(c => c.Contains("integral", StringComparison.OrdinalIgnoreCase)))
                summary.Positives.Add("Contém ingredientes integrais");

            // Concerns
            if (analysis.CategoryScores?.SugarScore < 40)
                summary.Concerns.Add("Alto teor de açúcar");
            if (analysis.CategoryScores?.SodiumScore < 40)
                summary.Concerns.Add("Alto teor de sódio");
            if (analysis.CategoryScores?.SaturatedFatScore < 40)
                summary.Concerns.Add("Alto teor de gordura saturada");
            if (product.Allergens.Count > 0)
                summary.Concerns.Add($"Contém alérgenos: {string.Join(", ", product.Allergens.Take(3))}");

            // Verdict
            summary.Verdict = analysis.OverallScore switch
            {
                >= 80 => "Ótima escolha para uma alimentação saudável!",
                >= 60 => "Pode ser consumido como parte de uma dieta equilibrada.",
                >= 40 => "Consuma com moderação e atenção às porções.",
                _ => "Considere alternativas mais saudáveis quando possível."
            };

            // Visual indicator
            summary.VisualIndicator = analysis.OverallScore switch
            {
                >= 80 => "✅",
                >= 60 => "👍",
                >= 40 => "⚠️",
                _ => "❌"
            };

            return summary;
        }

        private static List<AlertDto> GenerateAlerts(
            NutritionalAnalysisResultDto analysis,
            ConsolidatedProductDto product)
        {
            var alerts = new List<AlertDto>();

            // Allergen alerts (always critical)
            foreach (var allergen in product.Allergens)
            {
                alerts.Add(new AlertDto
                {
                    Type = "Allergen",
                    Severity = "Critical",
                    Title = $"Contém {allergen}",
                    Description = $"Este produto contém {allergen}. Atenção se você tem restrições alimentares.",
                    IconName = "warning"
                });
            }

            // High sugar alert
            if (analysis.CategoryScores?.SugarScore < 40)
            {
                alerts.Add(new AlertDto
                {
                    Type = "Nutrition",
                    Severity = "Warning",
                    Title = "Alto teor de açúcar",
                    Description = "Este produto contém uma quantidade elevada de açúcar por porção.",
                    IconName = "sugar"
                });
            }

            // High sodium alert
            if (analysis.CategoryScores?.SodiumScore < 40)
            {
                alerts.Add(new AlertDto
                {
                    Type = "Nutrition",
                    Severity = "Warning",
                    Title = "Alto teor de sódio",
                    Description = "Este produto contém uma quantidade elevada de sódio por porção.",
                    IconName = "salt"
                });
            }

            return alerts;
        }

        private static List<RecommendationDto> GenerateRecommendations(
            NutritionalAnalysisResultDto analysis,
            ConsolidatedProductDto product,
            bool includePersonalized)
        {
            var recommendations = new List<RecommendationDto>();

            if (analysis.OverallScore < 60)
            {
                recommendations.Add(new RecommendationDto
                {
                    Type = "Alternative",
                    Priority = "High",
                    Title = "Considere alternativas",
                    Description = "Procure versões com menos açúcar, sódio ou gordura saturada.",
                    ActionText = "Ver alternativas",
                    IsPersonalized = false
                });
            }

            if (analysis.CategoryScores?.FiberScore < 50)
            {
                recommendations.Add(new RecommendationDto
                {
                    Type = "Nutrition",
                    Priority = "Medium",
                    Title = "Aumente o consumo de fibras",
                    Description = "Combine este produto com alimentos ricos em fibras.",
                    IsPersonalized = false
                });
            }

            // Portion control recommendation
            if (analysis.OverallScore is >= 40 and < 80)
            {
                recommendations.Add(new RecommendationDto
                {
                    Type = "Portion",
                    Priority = "Medium",
                    Title = "Controle as porções",
                    Description = "Atente-se ao tamanho da porção indicada na embalagem.",
                    IsPersonalized = false
                });
            }

            return recommendations;
        }

        private async Task<Guid> SaveProductAsync(
            ConsolidatedProductDto product,
            CancellationToken cancellationToken)
        {
            // Check if product already exists (by barcode)
            Product? existingProduct = null;
            if (!string.IsNullOrEmpty(product.Barcode))
            {
                existingProduct = await _productRepository.GetByBarcodeAsync(product.Barcode, cancellationToken);
            }

            if (existingProduct is not null)
            {
                product.ProductId = existingProduct.Id;
                return existingProduct.Id;
            }

            // Create new product
            var newProduct = new Product(product.Name, product.Brand, product.Barcode);

            await _productRepository.AddAsync(newProduct, cancellationToken);

            product.ProductId = newProduct.Id;
            return newProduct.Id;
        }

        private async Task<Guid> SaveAnalysisAsync(
            Guid productId,
            NutritionalAnalysisResultDto analysis,
            Guid sessionId,
            CancellationToken cancellationToken)
        {
            // Map score to classification
            var classification = analysis.OverallScore switch
            {
                >= 80 => AnalysisClassification.Excellent,
                >= 60 => AnalysisClassification.Safe,
                >= 40 => AnalysisClassification.Moderate,
                _ => AnalysisClassification.Caution
            };

            // Map score to confidence
            var confidence = ConfidenceLevel.Medium;

            var productAnalysis = new ProductAnalysis(
                productId,
                null, // userId
                classification,
                confidence,
                analysis.Classification);

            var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
            if (product != null)
            {
                productAnalysis.AttachProduct(product);
            }

            await _analysisWriteRepository.AddAsync(productAnalysis, cancellationToken);

            return productAnalysis.Id;
        }

        #endregion

        #region Helper Classes

        private class CaptureStepDefinition
        {
            public CaptureType CaptureType { get; init; }
            public int Order { get; init; }
            public bool IsRequired { get; init; }
            public string IconName { get; init; } = string.Empty;
            public string NamePt { get; init; } = string.Empty;
            public string NameEn { get; init; } = string.Empty;
            public string DescriptionPt { get; init; } = string.Empty;
            public string DescriptionEn { get; init; } = string.Empty;
            public List<string> TipsPt { get; init; } = [];
            public List<string> TipsEn { get; init; } = [];
        }

        #endregion
    }
}
