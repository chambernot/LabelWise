using LabelWise.Api.Models;
using LabelWise.Application.DTOs.Access;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.DTOs.OpenFoodFacts;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Infrastructure.Helpers;
using LabelWise.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Security.Cryptography;

namespace LabelWise.Api.Controllers;

/// <summary>
/// Controller para análise nutricional de produtos alimentícios.
/// Responsabilidades exclusivas: HTTP concerns (validação de arquivo, acesso, resposta).
/// Todo o pipeline de análise é delegado ao INutritionAnalysisOrchestrator.
/// </summary>
[ApiController]
[Route("api/nutrition")]
[Authorize]
public class NutritionController : ControllerBase
{
    private readonly IAppAccessService               _appAccessService;
    private readonly INutritionAnalysisOrchestrator   _orchestrator;
    private readonly IBarcodeDetectorService          _barcodeDetector;
    private readonly IOpenFoodFactsService            _openFoodFacts;
    private readonly INutritionImageAnalyzer          _visionAnalyzer;
    private readonly IIntelligentAnalysisScoreService _scoreService;
    private readonly ILogger<NutritionController>    _logger;

    public NutritionController(
        IAppAccessService appAccessService,
        INutritionAnalysisOrchestrator orchestrator,
        IBarcodeDetectorService barcodeDetector,
        IOpenFoodFactsService openFoodFacts,
        INutritionImageAnalyzer visionAnalyzer,
        IIntelligentAnalysisScoreService scoreService,
        ILogger<NutritionController> logger)
    {
        _appAccessService = appAccessService ?? throw new ArgumentNullException(nameof(appAccessService));
        _orchestrator     = orchestrator     ?? throw new ArgumentNullException(nameof(orchestrator));
        _barcodeDetector  = barcodeDetector  ?? throw new ArgumentNullException(nameof(barcodeDetector));
        _openFoodFacts    = openFoodFacts    ?? throw new ArgumentNullException(nameof(openFoodFacts));
        _visionAnalyzer   = visionAnalyzer   ?? throw new ArgumentNullException(nameof(visionAnalyzer));
        _scoreService     = scoreService     ?? throw new ArgumentNullException(nameof(scoreService));
        _logger           = logger           ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Analisa uma imagem de produto alimentício e retorna avaliação nutricional unificada.
    /// </summary>
    [HttpPost("analyze-simple-image")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UnifiedNutritionAnalysisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UnifiedNutritionAnalysisResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AnalyzeSimpleImage(
        [FromForm] NutritionAnalysisFormModel model,
        CancellationToken cancellationToken = default)
    {
        var deviceId = ResolveDeviceId(model.DeviceId);

        _logger.LogInformation(
            "POST analyze-simple-image — File={File}, Size={Size}B, Device={Device}",
            model.File?.FileName ?? "N/A", model.File?.Length ?? 0, deviceId ?? "anon");

        try
        {
            // ── Verificação de acesso ──────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                var accessState = await _appAccessService.GetAccessStateAsync(deviceId);
                if (!accessState.CanUseAnalysis)
                {
                    _logger.LogWarning("Acesso negado. DeviceId={DeviceId}", deviceId);
                    return StatusCode(StatusCodes.Status403Forbidden, CreateAccessDeniedResponse(accessState));
                }
            }

            // ── Validação do arquivo ───────────────────────────────────────
            var fileError = ValidateFile(model.File);
            if (fileError != null) return BadRequest(fileError);

            byte[] imageBytes;
            using (var ms = new MemoryStream())
            {
                await model.File!.CopyToAsync(ms, cancellationToken);
                imageBytes = ms.ToArray();
            }

            // ── Delegar ao orquestrador ────────────────────────────────────
            var response = await _orchestrator.AnalyzeAsync(imageBytes, model.File!.ContentType, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar análise nutricional");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                error   = "Erro interno ao processar análise",
                details = ex.Message
            });
        }
    }

    /// <summary>
    /// Análise nutricional inteligente: tenta primeiro código de barras (Open Food Facts).
    /// Se não encontrar, faz fallback para OpenAI Vision usando a foto da tabela nutricional.
    /// Retorna os dados crus em um contrato unificado pensado para o front.
    /// Garante que a imagem chegue à OpenAI em ótima resolução e colorida.
    /// </summary>
    [HttpPost("nutricao-analise-inteligente")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(IntelligentAnalysisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IntelligentAnalysisResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> NutricaoAnaliseInteligente(
        [FromForm] NutritionAnalysisFormModel model,
        CancellationToken cancellationToken = default)
    {
        var deviceId = ResolveDeviceId(model.DeviceId);
        var response = new IntelligentAnalysisResponse();
        var totalStartedAt = Stopwatch.GetTimestamp();

        void LogPerfStep(string step, long startedAt) =>
            _logger.LogInformation(
                "[Inteligente.Perf] Step={Step}, ElapsedMs={ElapsedMs}",
                step,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

        IActionResult ReturnWithPerf(IActionResult result)
        {
            _logger.LogInformation(
                "[Inteligente.Perf] Step=total, Source={Source}, Success={Success}, OpenAiVisionUsed={OpenAiVisionUsed}, BarcodeFound={BarcodeFound}, OpenFoodFactsHit={OpenFoodFactsHit}, ElapsedMs={ElapsedMs}",
                response.Source ?? "none",
                response.Success,
                response.Diagnostics.OpenAiVisionUsed,
                response.Diagnostics.BarcodeFound,
                response.Diagnostics.OpenFoodFactsHit,
                Stopwatch.GetElapsedTime(totalStartedAt).TotalMilliseconds);

            return result;
        }

        _logger.LogInformation(
            "POST nutricao-analise-inteligente — File={File}, Size={Size}B, Device={Device}",
            model.File?.FileName ?? "N/A", model.File?.Length ?? 0, deviceId ?? "anon");

        try
        {
            // ── Acesso ─────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                var accessStartedAt = Stopwatch.GetTimestamp();
                var accessState = await _appAccessService.GetAccessStateAsync(deviceId);
                LogPerfStep("access_check", accessStartedAt);

                if (!accessState.CanUseAnalysis)
                {
                    _logger.LogWarning("Acesso negado. DeviceId={DeviceId}", deviceId);
                    return ReturnWithPerf(StatusCode(StatusCodes.Status403Forbidden, CreateAccessDeniedResponse(accessState)));
                }
            }

            // ── Validação básica do arquivo ────────────────────────────────
            var validationStartedAt = Stopwatch.GetTimestamp();
            var fileError = ValidateFile(model.File);
            LogPerfStep("file_validation", validationStartedAt);

            if (fileError != null)
            {
                response.Success = false;
                response.Message = fileError.GetType().GetProperty("error")?.GetValue(fileError) as string;
                return ReturnWithPerf(BadRequest(response));
            }

            byte[] originalBytes;
            var imageCopyStartedAt = Stopwatch.GetTimestamp();
            await using (var ms = new MemoryStream())
            {
                await model.File!.CopyToAsync(ms, cancellationToken);
                originalBytes = ms.ToArray();
            }
            LogPerfStep("image_copy", imageCopyStartedAt);

            // ── Preparar imagem para Vision (cor + resolução ótima) ────────
            var imagePrepareStartedAt = Stopwatch.GetTimestamp();
            var prepared = VisionImagePreparer.Prepare(originalBytes);
            LogPerfStep("image_prepare", imagePrepareStartedAt);

            if (!prepared.Success)
            {
                response.Success = false;
                response.Message = prepared.ErrorMessage;
                return ReturnWithPerf(BadRequest(response));
            }

            response.Diagnostics.ImageWidth = prepared.Width;
            response.Diagnostics.ImageHeight = prepared.Height;
            response.Diagnostics.PreparedSizeBytes = prepared.Bytes.Length;

            // ── 1) Código de barras → Open Food Facts ──────────────────────
            response.Diagnostics.BarcodeAttempted = true;
            var barcodeStartedAt = Stopwatch.GetTimestamp();
            var barcode = _barcodeDetector.DetectBarcode(originalBytes);
            LogPerfStep("barcode_detect", barcodeStartedAt);

            if (!string.IsNullOrWhiteSpace(barcode))
            {
                response.Diagnostics.BarcodeFound = true;
                response.Product.Barcode = barcode;

                _logger.LogInformation("[Inteligente] Barcode detectado: {Barcode}", barcode);

                var openFoodFactsStartedAt = Stopwatch.GetTimestamp();
                var product = await _openFoodFacts.GetByBarcodeAsync(barcode);
                LogPerfStep("openfoodfacts", openFoodFactsStartedAt);

                if (product != null && product.HasUsableNutritionData())
                {
                    response.Diagnostics.OpenFoodFactsHit = true;
                    response.Source = "openfoodfacts";
                    response.Success = true;
                    response.Message = "Produto encontrado na base Open Food Facts.";

                    var mapStartedAt = Stopwatch.GetTimestamp();
                    MapOpenFoodFactsToResponse(product, response);
                    LogPerfStep("map_openfoodfacts", mapStartedAt);

                    var scoreStartedAt = Stopwatch.GetTimestamp();
                    _scoreService.Apply(response, confidence: null);
                    ApplyTrustedDatabaseQuality(response);
                    LogPerfStep("score", scoreStartedAt);

                    return ReturnWithPerf(Ok(response));
                }

                response.Diagnostics.Warnings.Add(
                    "Código de barras encontrado, mas Open Food Facts não tem dados nutricionais. Caindo para análise por imagem.");
            }
            else
            {
                response.Diagnostics.Warnings.Add("Nenhum código de barras detectado na imagem.");
            }

            // ── 2) Fallback OpenAI Vision ──────────────────────────────────
            response.Diagnostics.OpenAiVisionUsed = true;

            // Hash exato calculado sobre os bytes JÁ PREPARADOS (determinístico:
            // Lanczos3 + JPEG q=92 + auto-orient). Garante que duas requisições
            // com o mesmo arquivo de origem batam no cache, e mantém o mesmo
            // identificador entre logs do controller e logs do analyzer.
            var hashStartedAt = Stopwatch.GetTimestamp();
            var preparedHash = ComputeSha256Hex(prepared.Bytes);
            LogPerfStep("prepared_hash", hashStartedAt);

            _logger.LogInformation(
                "[Inteligente] Acionando OpenAI Vision (fallback). PreparedHash={Hash}",
                preparedHash);

            var visionAnalyzerStartedAt = Stopwatch.GetTimestamp();
            var profile = await _visionAnalyzer.AnalyzeAsync(
                prepared.Bytes,
                prepared.MimeType,
                preparedHash,
                cancellationToken);
            LogPerfStep("vision_analyzer", visionAnalyzerStartedAt);

            if (profile == null)
            {
                response.Success = false;
                response.Source = "none";
                response.Message = "Não foi possível extrair dados nutricionais nem por código de barras nem por imagem. Envie uma foto mais nítida da tabela nutricional.";
                response.ImageQuality = new ImageQualityInfo
                {
                    RetryRequested = true,
                    ReasonCode = "extraction_failed",
                    Reason = "Não foi possível ler a tabela nutricional. Tente aproximar mais a câmera, manter a tabela reta e garantir boa iluminação.",
                    ConfidenceScore = null,
                    CompletenessPercent = null
                };
                return ReturnWithPerf(Ok(response));
            }

            response.Source = "openai-vision";
            response.Success = true;
            response.Message = "Tabela nutricional extraída a partir da foto.";
            response.Diagnostics.Confidence = profile.NutritionConfidence?.GlobalScore;

            var mapProfileStartedAt = Stopwatch.GetTimestamp();
            MapEstimatedProfileToResponse(profile, response);

            if (response.Nutrition.PerServing is not null && profile.RawPerServing is null
                && profile.NutritionConfidence?.GlobalScore is double confidence)
            {
                profile.NutritionConfidence.GlobalScore = Math.Min(confidence, 0.85);
                response.Diagnostics.Confidence = profile.NutritionConfidence.GlobalScore;
            }

            if (profile.DataSource.TryGetValue("ServingColumnDiscardReason", out var servingDiscardReason)
                && !string.IsNullOrWhiteSpace(servingDiscardReason))
            {
                response.Diagnostics.Warnings.Add(servingDiscardReason);

                if (profile.NutritionConfidence?.GlobalScore is double currentConfidence)
                {
                    profile.NutritionConfidence.GlobalScore = Math.Min(currentConfidence, 0.85);
                    response.Diagnostics.Confidence = profile.NutritionConfidence.GlobalScore;
                }
            }
            LogPerfStep("map_vision_profile", mapProfileStartedAt);

            // ── Validação de qualidade da extração ─────────────────────────
            var qualityStartedAt = Stopwatch.GetTimestamp();
            var qualityWarnings = response.Diagnostics.Warnings.Concat(profile.DataSource.Values).ToList();
            var qualityCheck = NutritionQualityEvaluator.EvaluateImageQuality(prepared.Bytes, profile, qualityWarnings);
            response.ImageQuality = qualityCheck;
            response.AnalysisQuality = NutritionQualityEvaluator.EvaluateAnalysisQuality(profile, qualityCheck, qualityWarnings);
            response.NutritionReliabilityScore = NutritionQualityEvaluator.CalculateReliabilityScore(profile, qualityWarnings, qualityCheck.SafeForPreciseNutritionAnalysis ? 0 : 10);
            response.IngredientContext = NutritionQualityEvaluator.BuildIngredientContext([], response.ProcessingLevel);
            LogPerfStep("quality_check", qualityStartedAt);

            if (qualityCheck.RetryRequested)
            {
                _logger.LogWarning(
                    "[Inteligente] Extração descartada por baixa qualidade. Code={Code}, Confidence={Conf}, Completeness={Comp}%",
                    qualityCheck.ReasonCode, qualityCheck.ConfidenceScore, qualityCheck.CompletenessPercent);

                response.Success = false;
                response.Source  = "none";
                response.Message = qualityCheck.Reason;
                response.Score   = null;
                return ReturnWithPerf(Ok(response));
            }

            var scoreVisionStartedAt = Stopwatch.GetTimestamp();
            _scoreService.Apply(response, profile.NutritionConfidence);
            NutritionQualityEvaluator.ApplyScoreReliability(response.Score, response.AnalysisQuality, response.NutritionReliabilityScore);
            LogPerfStep("score", scoreVisionStartedAt);

            // Quando per100 é derivado, ajusta o resumoRapido para referenciar
            // a porção real em vez dos valores calculados por 100g.
            var summaryAdjustmentStartedAt = Stopwatch.GetTimestamp();
            if (response.Per100Source == "derived" && response.Score is not null
                && response.Nutrition.Serving is { Amount: > 0 } srv)
            {
                var calPorcao = response.Nutrition.AsLabel?.CaloriesKcal;
                if (calPorcao.HasValue)
                {
                    var suffix = $"Contém {calPorcao.Value:F0} kcal por porção de {srv.Amount:G}{srv.Unit} " +
                                 $"(base 100{srv.Unit}: estimada).";
                    response.Score.ResumoRapido = response.Score.ResumoRapido
                        .Replace($"Contém {response.Nutrition.Per100?.CaloriesKcal:F0} kcal por 100g.", suffix)
                        .Replace($"Contém {response.Nutrition.Per100?.CaloriesKcal:F0} kcal por 100ml.", suffix);
                }
            }
            LogPerfStep("summary_adjustment", summaryAdjustmentStartedAt);

            return ReturnWithPerf(Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro em nutricao-analise-inteligente");
            response.Success = false;
            response.Source = "none";
            response.Message = "Erro interno ao processar a análise.";
            response.Diagnostics.Warnings.Add(ex.Message);
            return ReturnWithPerf(StatusCode(StatusCodes.Status500InternalServerError, response));
        }
    }

    // ── Mappers (locais ao endpoint inteligente) ──────────────────────────

    private static void MapOpenFoodFactsToResponse(OpenFoodFactsProduct product, IntelligentAnalysisResponse response)
    {
        response.Product.Name = product.ProductName;
        response.Product.Brand = product.Brands;
        response.Product.Category = product.Categories;

        var n = product.Nutriments;
        if (n == null) return;

        // OFF entrega tudo em base 100 g/ml (campo *_100g).
        // Sem indicação explícita de unidade, assumimos "g" como padrão genérico.
        response.Nutrition.Unit = "g";
        response.ScoreBasis = "per100g";

        var values = new NutritionValues
        {
            CaloriesKcal = n.EnergyKcal100g,
            Carbohydrates = n.Carbohydrates100g,
            Sugars = n.Sugars100g,
            Proteins = n.Proteins100g,
            TotalFats = n.Fat100g,
            SaturatedFats = n.SaturatedFat100g,
            Fiber = n.Fiber100g,
            // OFF entrega sódio em g/100g; convertemos para mg para alinhar
            // com a base usada pelo motor de score (ANVISA usa mg/100 g).
            SodiumMg = n.Sodium100g.HasValue ? n.Sodium100g.Value * 1000.0 : null
        };

        response.Nutrition.Per100  = values;
        response.Nutrition.AsLabel = values; // OFF não distingue rótulo vs base

        // OFF entrega apenas base 100g — front usa per100 como referência primária.
        response.DisplayBasis = "per100";
        response.Per100Source  = "direct";
    }

    private static void MapEstimatedProfileToResponse(EstimatedNutritionProfileDto profile, IntelligentAnalysisResponse response)
    {
        var unit = IsMlUnit(profile.NutritionUnit) || IsMlUnit(profile.ServingUnit) ? "ml" : "g";
        response.Nutrition.Unit = unit;
        response.ScoreBasis = unit == "ml" ? "per100ml" : "per100g";

        // Identificação do produto vinda do rótulo (quando a IA conseguiu ler).
        response.Product.Name ??= profile.ProductName;
        response.Product.Brand ??= profile.Brand;

        // Porção declarada no rótulo.
        if (profile.ServingAmount.HasValue || !string.IsNullOrWhiteSpace(profile.ServingDescription))
        {
            response.Nutrition.Serving = new ServingDescriptor
            {
                Amount = profile.ServingAmount,
                Unit = profile.ServingUnit,
                Description = profile.ServingDescription
            };
        }

        // Bloco [2] — base 100 g/ml (alimenta o score).
        var per100 = new NutritionValues
        {
            CaloriesKcal = unit == "ml" ? profile.CaloriesPer100ml ?? profile.CaloriesPer100g : profile.CaloriesPer100g,
            Carbohydrates = profile.EstimatedCarbsPer100g,
            Sugars = profile.EstimatedSugarPer100g,
            AddedSugars = profile.EstimatedAddedSugarPer100g,
            Polyols = profile.EstimatedPolyolsPer100g,
            Proteins = profile.EstimatedProteinPer100g,
            TotalFats = profile.EstimatedFatPer100g,
            SaturatedFats = profile.EstimatedSaturatedFatPer100g,
            TransFats = profile.EstimatedTransFatPer100g,
            Fiber = profile.EstimatedFiberPer100g,
            SodiumMg = profile.EstimatedSodiumPer100g
        };
        NormalizeZeroFatSubfields(per100);
        response.Nutrition.Per100 = per100;

        // DisplayBasis: quando há porção declarada e valores por porção disponíveis,
        // o front exibe asLabel. Sem porção, a única referência disponível é per100.
        response.DisplayBasis = profile.RawPerServing is not null ? "asLabel" : "per100";

        // Per100Source: "direct" quando a coluna 100g/100ml foi lida do rótulo;
        // "derived" quando calculado matematicamente a partir da porção declarada.
        response.Per100Source = profile.IsPer100Derived ? "derived" : "direct";

        // Bloco [1] — fiel ao rótulo (por porção, sem normalização).
        // Bloco [3] — perServing (mesmo conteúdo, mantido por contrato).
        if (profile.RawPerServing is { } raw)
        {
            var perServing = new NutritionValues
            {
                CaloriesKcal = raw.CaloriesKcal,
                Carbohydrates = raw.Carbohydrates,
                Sugars = raw.Sugar,
                AddedSugars = raw.AddedSugar,
                Polyols = raw.Polyols,
                Proteins = raw.Proteins,
                TotalFats = raw.TotalFats,
                SaturatedFats = raw.SaturatedFats,
                TransFats = raw.TransFats,
                Fiber = raw.Fiber,
                SodiumMg = raw.SodiumMg
            };
            NormalizeZeroFatSubfields(perServing);
            response.Nutrition.AsLabel = perServing;
            response.Nutrition.PerServing = perServing;
        }
        else
        {
            var derivedServing = TryDeriveServingForDisplay(profile, response, per100, unit);
            if (derivedServing is not null)
            {
                response.Nutrition.AsLabel = derivedServing;
                response.Nutrition.PerServing = derivedServing;
                response.DisplayBasis = "asLabel";
                response.Diagnostics.Warnings.Add(
                    "Valores por porção calculados a partir da coluna 100g/100ml e da porção declarada.");
            }
            else
            {
                // Sem porção: o rótulo já era em 100 g/ml.
                response.Nutrition.AsLabel = per100;
            }
        }
    }

    private static NutritionValues? TryDeriveServingForDisplay(
        EstimatedNutritionProfileDto profile,
        IntelligentAnalysisResponse response,
        NutritionValues per100,
        string unit)
    {
        if (profile.IsPer100Derived)
            return null;

        if (response.Nutrition.Serving?.Amount is not double amount || amount <= 0)
            return null;

        if (!string.Equals(response.Nutrition.Serving.Unit, unit, StringComparison.OrdinalIgnoreCase))
            return null;

        if (!HasAnyNutritionValue(per100))
            return null;

        var factor = amount / 100.0;
        var derived = new NutritionValues
        {
            CaloriesKcal = Round1(per100.CaloriesKcal * factor),
            Carbohydrates = Round1(per100.Carbohydrates * factor),
            Sugars = Round1(per100.Sugars * factor),
            AddedSugars = Round1(per100.AddedSugars * factor),
            Polyols = Round1(per100.Polyols * factor),
            Proteins = Round1(per100.Proteins * factor),
            TotalFats = Round1(per100.TotalFats * factor),
            SaturatedFats = Round1(per100.SaturatedFats * factor),
            TransFats = Round1(per100.TransFats * factor),
            Fiber = Round1(per100.Fiber * factor),
            SodiumMg = Round1(per100.SodiumMg * factor)
        };
        NormalizeZeroFatSubfields(derived);
        return derived;
    }

    private static void ApplyTrustedDatabaseQuality(IntelligentAnalysisResponse response)
    {
        response.ImageQuality = new ImageQualityInfo
        {
            OverallConfidence = "high",
            TableVisible = true,
            TextLegibility = "high",
            SafeForPreciseNutritionAnalysis = true,
            ReasonCode = "ok",
            CompletenessPercent = 100
        };
        response.AnalysisQuality = new NutritionAnalysisQualityDto
        {
            Mode = "complete",
            Confidence = "high",
            Reason = "Dados nutricionais obtidos de base estruturada."
        };
        response.NutritionReliabilityScore = 90;
        response.IngredientContext = NutritionQualityEvaluator.BuildIngredientContext([], response.ProcessingLevel);
        NutritionQualityEvaluator.ApplyScoreReliability(response.Score, response.AnalysisQuality, response.NutritionReliabilityScore);
    }

    private static bool IsMlUnit(string? unit) =>
        string.Equals(unit, "ml", StringComparison.OrdinalIgnoreCase);

    private static void NormalizeZeroFatSubfields(NutritionValues values)
    {
        if (values.TotalFats != 0)
            return;

        values.SaturatedFats ??= 0;
        values.TransFats ??= 0;
    }

    private static bool HasAnyNutritionValue(NutritionValues values) =>
        values.CaloriesKcal.HasValue ||
        values.Carbohydrates.HasValue ||
        values.Sugars.HasValue ||
        values.AddedSugars.HasValue ||
        values.Proteins.HasValue ||
        values.TotalFats.HasValue ||
        values.SodiumMg.HasValue;

    private static double? Round1(double? value) =>
        value.HasValue ? Math.Round(value.Value, 1, MidpointRounding.AwayFromZero) : null;

    // ── HTTP helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Avalia se a extração retornada pela OpenAI Vision tem qualidade suficiente
    /// para ser entregue ao front. Três critérios independentes:
    ///   1. GlobalScore de confiança ≥ 0.40 (limiar mínimo para dados utilizáveis)
    ///   2. Completeness ≥ 40% (ao menos 3–4 campos de 8 preenchidos)
    ///   3. Ao menos um campo crítico presente (calorias, carboidratos ou proteínas)
    /// </summary>
    private static ImageQualityInfo EvaluateVisionQuality(EstimatedNutritionProfileDto profile)
    {
        const double MinConfidence   = 0.40;
        const int    MinCompleteness = 40;

        var confidence   = profile.NutritionConfidence?.GlobalScore;
        var completeness = NutritionCompletenessCalculator.Calculate(profile);
        var hasCritical  = profile.CaloriesPer100g.HasValue
                        || profile.EstimatedCarbsPer100g.HasValue
                        || profile.EstimatedProteinPer100g.HasValue;

        // 1. Confiança global muito baixa
        if (confidence.HasValue && confidence.Value < MinConfidence)
        {
            return new ImageQualityInfo
            {
                RetryRequested    = true,
                ReasonCode        = "low_confidence",
                Reason            = "A tabela nutricional não ficou legível na foto. Tente aproximar mais a câmera e garantir boa iluminação.",
                ConfidenceScore   = confidence,
                CompletenessPercent = completeness
            };
        }

        // 2. Poucos campos preenchidos
        if (completeness < MinCompleteness)
        {
            return new ImageQualityInfo
            {
                RetryRequested      = true,
                ReasonCode          = "insufficient_fields",
                Reason              = "Poucos dados foram lidos na tabela nutricional. Certifique-se de que a tabela esteja totalmente enquadrada e bem iluminada.",
                ConfidenceScore     = confidence,
                CompletenessPercent = completeness
            };
        }

        // 3. Nenhum campo crítico presente (calorias, carbs ou proteínas)
        if (!hasCritical)
        {
            return new ImageQualityInfo
            {
                RetryRequested      = true,
                ReasonCode          = "no_critical_fields",
                Reason              = "Não foi possível identificar os valores principais da tabela nutricional. Envie uma foto mais nítida com a tabela completa visível.",
                ConfidenceScore     = confidence,
                CompletenessPercent = completeness
            };
        }

        return new ImageQualityInfo
        {
            RetryRequested      = false,
            ReasonCode          = "ok",
            ConfidenceScore     = confidence,
            CompletenessPercent = completeness
        };
    }

    private static string ComputeSha256Hex(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static object? ValidateFile(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return new { success = false, error = "Arquivo de imagem é obrigatório" };

        if (file.Length < 20_000)
            return new { success = false, error = "Imagem com baixa qualidade para OCR. Envie a imagem original, sem compressão agressiva." };

        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext))
            return new { success = false, error = $"Tipo de arquivo não suportado. Use: {string.Join(", ", allowed)}" };

        const long maxSize = 10 * 1024 * 1024;
        if (file.Length > maxSize)
            return new { success = false, error = "Arquivo muito grande. Tamanho máximo: 10MB" };

        return null;
    }

    private string? ResolveDeviceId(string? formDeviceId)
    {
        if (!string.IsNullOrWhiteSpace(formDeviceId)) return formDeviceId.Trim();

        if (Request.Headers.TryGetValue("X-Device-Id", out var headerValue))
        {
            var id = headerValue.FirstOrDefault();
            return string.IsNullOrWhiteSpace(id) ? null : id.Trim();
        }

        return null;
    }

    private static AccessDeniedResponse CreateAccessDeniedResponse(AppAccessStateResponse accessState) =>
        new()
        {
            Success      = false,
            AccessDenied = true,
            Reason       = accessState.IsPremium || accessState.IsTrialActive ? "access_denied" : "trial_expired",
            Message      = accessState.Message,
            AccessState  = accessState
        };
}
