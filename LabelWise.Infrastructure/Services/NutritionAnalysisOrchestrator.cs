using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.DTOs.FoodAnalysisTrust;
using LabelWise.Application.DTOs.OpenFoodFacts;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services;

/// <summary>
/// Orquestra o pipeline completo de análise nutricional.
///
/// Pipeline:
///   1. Pré-processamento da imagem
///   2. Barcode detection → OpenFoodFacts (caminho rápido quando disponível)
///   3. OpenAI Vision → extração da tabela nutricional
///   4. INutritionValidator.Validate()
///   5. INutritionEnricher.Enrich()
///   6. INutritionScoringService.Calculate()
///   7. AdvancedNutritionProfileEvaluator.Evaluate()
///   8. INutritionResponseBuilder.Build()
///
/// O Controller apenas passa imageBytes e retorna Ok(response).
/// </summary>
public sealed class NutritionAnalysisOrchestrator : INutritionAnalysisOrchestrator
{
    private readonly IImagePreprocessingService  _imagePreprocessing;
    private readonly IBarcodeDetectorService     _barcodeDetector;
    private readonly IOpenFoodFactsService       _openFoodFacts;
    private readonly INutritionImageAnalyzer     _imageAnalyzer;
    private readonly INutritionValidator         _validator;
    private readonly INutritionEnricher          _enricher;
    private readonly INutritionScoringService    _scoringService;
    private readonly INutritionResponseBuilder   _responseBuilder;
    private readonly INutritionStateMachine      _stateMachine;
    private readonly IFoodAnalysisTrustEngine    _trustEngine;
    private readonly FoodAnalysisQualityGate     _qualityGate;
    private readonly ILogger<NutritionAnalysisOrchestrator> _logger;

    public NutritionAnalysisOrchestrator(
        IImagePreprocessingService imagePreprocessing,
        IBarcodeDetectorService barcodeDetector,
        IOpenFoodFactsService openFoodFacts,
        INutritionImageAnalyzer imageAnalyzer,
        INutritionValidator validator,
        INutritionEnricher enricher,
        INutritionScoringService scoringService,
        INutritionResponseBuilder responseBuilder,
        INutritionStateMachine stateMachine,
        IFoodAnalysisTrustEngine trustEngine,
        FoodAnalysisQualityGate qualityGate,
        ILogger<NutritionAnalysisOrchestrator> logger)
    {
        _imagePreprocessing   = imagePreprocessing;
        _barcodeDetector      = barcodeDetector;
        _openFoodFacts        = openFoodFacts;
        _imageAnalyzer        = imageAnalyzer;
        _validator            = validator;
        _enricher             = enricher;
        _scoringService       = scoringService;
        _responseBuilder      = responseBuilder;
        _stateMachine         = stateMachine;
        _trustEngine          = trustEngine;
        _qualityGate          = qualityGate;
        _logger               = logger;
    }

    public async Task<UnifiedNutritionAnalysisResponse> AnalyzeAsync(
        byte[] rawImageBytes,
        string? mimeType,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Pré-processamento seguro para OpenAI Vision ───────────────────
        var processedImageBytes = _imagePreprocessing.EnhanceForOcr(rawImageBytes);
        var processedMimeType = mimeType;

        _logger.LogInformation(
            "[Orchestrator] Original size: {OriginalSize} bytes | Processed size: {ProcessedSize} bytes",
            rawImageBytes.Length,
            processedImageBytes.Length);

        // ── 2. Barcode → OpenFoodFacts ────────────────────────────────────────
        var barcode = _barcodeDetector.DetectBarcode(rawImageBytes);

        if (!string.IsNullOrWhiteSpace(barcode))
        {
            _logger.LogInformation("[Orchestrator] Barcode={Barcode} — consultando OpenFoodFacts.", barcode);

            var offProduct = await _openFoodFacts.GetByBarcodeAsync(barcode);
            if (offProduct != null)
            {
                var pipeline = BuildPipelineFromOpenFoodFacts(offProduct);

                if (pipeline.HasReliableNutritionData)
                {
                    _logger.LogInformation(
                        "[Orchestrator] OpenFoodFacts hit — produto={Name}, confiável=true.",
                        offProduct.ProductName ?? "N/A");

                    return RunPipeline(pipeline, cancellationToken, rawImageBytes);
                }

                _logger.LogInformation(
                    "[Orchestrator] OpenFoodFacts dados insuficientes para {Barcode} — seguindo para Vision AI.", barcode);
            }
            else
            {
                _logger.LogInformation("[Orchestrator] OpenFoodFacts miss para {Barcode} — seguindo para Vision AI.", barcode);
            }
        }

        // ── 3. OpenAI Vision ──────────────────────────────────────────────────
        _logger.LogInformation("[Orchestrator] Iniciando OpenAI Vision.");
        var visionProfile = await _imageAnalyzer.AnalyzeAsync(processedImageBytes, processedMimeType, cancellationToken: cancellationToken);

        if (visionProfile is null)
        {
            _logger.LogWarning("[Orchestrator] OpenAI Vision falhou — retornando resposta vazia.");
            return _responseBuilder.BuildEmpty(BuildEmptyPipeline());
        }

        // ── 4. Montar pipeline DTO ─────────────────────────────────────────────
        var hasNutritionData = visionProfile.CaloriesPer100g.HasValue 
                            || visionProfile.EstimatedProteinPer100g.HasValue 
                            || visionProfile.EstimatedCarbsPer100g.HasValue;

        var visionPipeline = BuildPipelineFromVision(visionProfile, hasNutritionData);

        return RunPipeline(visionPipeline, cancellationToken, processedImageBytes);
    }

    // ── Pipeline central (shared entre OpenFoodFacts e DI paths) ─────────────

    private UnifiedNutritionAnalysisResponse RunPipeline(
    NutritionAnalysisResponseDto pipeline,
    CancellationToken _,
    byte[]? imageBytes = null)
    {
        // STEP 4 — Validação
        var validated = _validator.Validate(pipeline.EstimatedNutritionProfile);
        var isFromOpenAi = pipeline.EstimatedNutritionProfile.IsFromOpenAI;

        // STEP 5 — Enriquecimento
        var enriched = _enricher.Enrich(
            validated,
            pipeline.Category,
            pipeline.AnalysisMode,
            null
        );

        enriched.ConfidenceDetails = enriched.NormalizedProfile.NutritionConfidence;

        // STEP 6 — Scoring
        var score = _scoringService.Calculate(enriched);

        // STEP 7 — Perfis
        var profiles = AdvancedNutritionProfileEvaluator.Evaluate(
            enriched.NormalizedProfile,
            score.Value,
            score.PrincipalOffender,
            pipeline.Category);

        // STEP 8 — Response
        var response = _responseBuilder.Build(pipeline, enriched, score, profiles);
        response.ProcessingClassification = BuildProcessingClassification(enriched.ProcessingLevel, score.Warnings);
        response.QuickFlags = BuildQuickFlags(score, response.ProcessingClassification);
        ApplyQuality(response, pipeline, enriched, imageBytes);
        var trust = _trustEngine.Evaluate(BuildTrustInput(response, pipeline, enriched));
        _qualityGate.Apply(response, trust);
        NutritionQualityEvaluator.ApplyScoreReliability(response.Score, response.AnalysisQuality, response.NutritionReliabilityScore);

        // STEP 9 — State Machine determinística (única fonte de verdade)
        var ctx = BuildStateContext(pipeline, enriched);
        var state = _stateMachine.DetermineState(ctx);
        _stateMachine.Apply(state, response);

        _logger.LogInformation(
            "[Orchestrator] Pipeline concluído — State={State}, Score={Score}, Label={Label}, Confidence={Conf}, Reliability={Reliability}, Mode={Mode}, FromOpenAI={FromAI}, Rules={Rules}, QualityWarnings={QualityWarnings}",
            state, response.Score?.Value, response.Score?.Label, response.Score?.Confidence, response.Score?.Reliability, response.Analysis.AnalysisMode, isFromOpenAi, string.Join(" | ", response.ProcessingClassification.Reasons), string.Join(" | ", response.ImageQuality.Warnings));

        return response;
    }

    private static NutritionContext BuildStateContext(
        NutritionAnalysisResponseDto pipeline,
        NutritionEnrichedData enriched)
    {
        var profile = enriched.NormalizedProfile;
        var hasCalories = profile.CaloriesPer100g.HasValue || profile.CaloriesPer100ml.HasValue;
        var hasMacros   = profile.EstimatedProteinPer100g.HasValue
                       || profile.EstimatedCarbsPer100g.HasValue
                       || profile.EstimatedFatPer100g.HasValue;
        var hasAny      = hasCalories || hasMacros
                       || profile.EstimatedSugarPer100g.HasValue
                       || profile.EstimatedSodiumPer100g.HasValue
                       || profile.EstimatedFiberPer100g.HasValue;

        // Mínimo para score confiável: calorias + ao menos um macro.
        var hasMinimum = hasCalories && hasMacros;

        // Tabela considerada detectada quando o pipeline reporta dados confiáveis
        // OU quando há sinais consistentes de tabela (calorias + macros).
        var hasTable = pipeline.HasReliableNutritionData || hasMinimum;

        var confidence = enriched.ConfidenceDetails?.GlobalScore
                       ?? profile.NutritionConfidence?.GlobalScore
                       ?? 0d;

        return new NutritionContext
        {
            HasNutritionTable    = hasTable,
            HasMinimumData       = hasMinimum,
            HasAnyNutritionData  = hasAny,
            HasCaloriesOnly      = hasCalories && !hasMacros,
            Confidence           = confidence
        };
    }

    // ── Mapeadores de pipeline DTO ────────────────────────────────────────────

    private static NutritionAnalysisResponseDto BuildPipelineFromVision(
        EstimatedNutritionProfileDto profile,
        bool hasNutritionData)
    {
        var mode = hasNutritionData
            ? AnalysisMode.FullNutritionLabel
            : AnalysisMode.FrontOfPackageOnly;

        return new NutritionAnalysisResponseDto
        {
            Success                   = true,
            AnalysisMode              = mode,
            EstimatedNutritionProfile = profile,
            HasReliableNutritionData =
    profile.CaloriesPer100g.HasValue &&
    profile.EstimatedCarbsPer100g.HasValue,
            DataSource                = "OPENAI_VISION",
            FallbackType              = hasNutritionData ? "real" : "unknown",
            NutritionFlags            = hasNutritionData ? ["NutritionTable:detected"] : [],
            Warnings                  = []
        };
    }

    private static NutritionAnalysisResponseDto BuildPipelineFromOpenFoodFacts(OpenFoodFactsProduct product)
    {
        var n = product.Nutriments;

        var profile = new EstimatedNutritionProfileDto
        {
            CaloriesPer100g              = n?.EnergyKcal100g,
            EstimatedCarbsPer100g        = n?.Carbohydrates100g,
            EstimatedSugarPer100g        = n?.Sugars100g,
            EstimatedProteinPer100g      = n?.Proteins100g,
            EstimatedFatPer100g          = n?.Fat100g,
            EstimatedSaturatedFatPer100g = n?.SaturatedFat100g,
            EstimatedFiberPer100g        = n?.Fiber100g,
            EstimatedSodiumPer100g       = n?.Sodium100g,
            NutritionUnit                = "g",
            Basis                        = "OpenFoodFacts",
            IsCorrectedByOcr             = false,
            DataSource                   = new Dictionary<string, string> { ["source"] = "OPENFOODFACTS" }
        };

        bool hasData = profile.CaloriesPer100g.HasValue
                    || profile.EstimatedProteinPer100g.HasValue
                    || profile.EstimatedCarbsPer100g.HasValue;

        return new NutritionAnalysisResponseDto
        {
            Success                   = true,
            ProductName               = product.ProductName,
            Brand                     = product.Brands,
            Category                  = product.Categories,
            AnalysisMode              = AnalysisMode.FullNutritionLabel,
            EstimatedNutritionProfile = profile,
            HasReliableNutritionData  = hasData,
            DataSource                = "OPENFOODFACTS",
            FallbackType              = hasData ? "real" : "unknown",
            NutritionFlags            = hasData ? ["NutritionTable:detected"] : []
        };
    }

    private static NutritionAnalysisResponseDto BuildEmptyPipeline() =>
        new()
        {
            Success                  = true,
            HasReliableNutritionData = false,
            DataSource               = "UNAVAILABLE",
            FallbackType             = "unknown",
            NutritionFlags           = [],
            Warnings                 = [],
            EstimatedNutritionProfile = new EstimatedNutritionProfileDto
            {
                DataSource = new Dictionary<string, string> { ["source"] = "UNAVAILABLE" }
            }
        };

    private static NutritionProcessingClassificationDto BuildProcessingClassification(string? processingLevel, IReadOnlyList<string> warnings)
    {
        var normalized = processingLevel?.Trim().ToLowerInvariant() ?? "desconhecido";
        var level = normalized switch
        {
            "in_natura" or "minimamente_processado" => "natural",
            "processado" => "processed",
            "ultraprocessado" => "ultra_processed",
            _ => "unknown"
        };

        var reasons = new List<string>();
        if (level == "ultra_processed") reasons.Add("Produto com alto nível de processamento.");
        if (level == "processed") reasons.Add("Produto processado; observe frequência de consumo.");
        if (level == "natural") reasons.Add("Baixo nível de processamento detectado.");
        reasons.AddRange(warnings.Where(warning => warning.Contains("açúcar", StringComparison.OrdinalIgnoreCase) || warning.Contains("sódio", StringComparison.OrdinalIgnoreCase)).Take(2));

        return new NutritionProcessingClassificationDto
        {
            Level = level,
            Confidence = level == "unknown" ? "low" : "medium",
            Reasons = reasons.DefaultIfEmpty("Nível de processamento não identificado com segurança.").Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private static FoodAnalysisTrustInput BuildTrustInput(
        UnifiedNutritionAnalysisResponse response,
        NutritionAnalysisResponseDto pipeline,
        NutritionEnrichedData enriched)
    {
        var profile = enriched.NormalizedProfile;
        var fieldCount = CountNutritionFields(profile);
        var isOpenFoodFacts = string.Equals(pipeline.DataSource, "OPENFOODFACTS", StringComparison.OrdinalIgnoreCase);
        var warnings = enriched.ValidationWarnings
            .Concat(pipeline.Warnings)
            .Concat(response.ImageQuality.Warnings)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        return new FoodAnalysisTrustInput
        {
            Source = isOpenFoodFacts ? "nutrition_openfoodfacts" : "nutrition_vision",
            OcrConfidence = isOpenFoodFacts ? "high" : response.ImageQuality.OverallConfidence,
            BlurDetected = response.ImageQuality.BlurDetected,
            ReflectionDetected = response.ImageQuality.ReflectionDetected,
            CroppedTable = response.ImageQuality.CroppedTable,
            PartialRead = !isOpenFoodFacts && (response.AnalysisQuality.Mode is "partial" or "unsafe" || response.ImageQuality.RetryRequested || !pipeline.HasReliableNutritionData),
            TablePartiallyObstructed = response.ImageQuality.TablePartiallyObstructed,
            NutritionRegionDetected = response.HasNutritionTable || response.ImageQuality.TableVisible || isOpenFoodFacts,
            TableCompletenessLow = !isOpenFoodFacts && (!response.HasMinimumNutritionData || fieldCount < 4),
            ParsingBroken = !isOpenFoodFacts && fieldCount == 0,
            InferredDataCount = profile.IsFromOpenAI ? profile.FieldValues.Count(item => item.Value.Confidence is < 0.6) : 0,
            NutritionFieldCount = fieldCount,
            Warnings = warnings,
            NutritionValues = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase)
            {
                ["calories"] = profile.CaloriesPer100g ?? profile.CaloriesPer100ml,
                ["carbs"] = profile.EstimatedCarbsPer100g,
                ["sugar"] = profile.EstimatedSugarPer100g,
                ["added_sugar"] = profile.EstimatedAddedSugarPer100g,
                ["protein"] = profile.EstimatedProteinPer100g,
                ["fat"] = profile.EstimatedFatPer100g,
                ["saturated_fat"] = profile.EstimatedSaturatedFatPer100g,
                ["sodium"] = profile.EstimatedSodiumPer100g
            }
        };
    }

    private static int CountNutritionFields(EstimatedNutritionProfileDto profile)
    {
        var values = new double?[]
        {
            profile.CaloriesPer100g ?? profile.CaloriesPer100ml,
            profile.EstimatedCarbsPer100g,
            profile.EstimatedSugarPer100g,
            profile.EstimatedProteinPer100g,
            profile.EstimatedFatPer100g,
            profile.EstimatedSaturatedFatPer100g,
            profile.EstimatedFiberPer100g,
            profile.EstimatedSodiumPer100g
        };

        return values.Count(value => value.HasValue);
    }

    private static void ApplyQuality(
        UnifiedNutritionAnalysisResponse response,
        NutritionAnalysisResponseDto pipeline,
        NutritionEnrichedData enriched,
        byte[]? imageBytes)
    {
        if (string.Equals(pipeline.DataSource, "OPENFOODFACTS", StringComparison.OrdinalIgnoreCase))
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
        }
        else
        {
            var warnings = enriched.ValidationWarnings
                .Concat(enriched.NormalizedProfile.DataSource.Values)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
            response.ImageQuality = NutritionQualityEvaluator.EvaluateImageQuality(imageBytes, enriched.NormalizedProfile, warnings);
            response.AnalysisQuality = NutritionQualityEvaluator.EvaluateAnalysisQuality(enriched.NormalizedProfile, response.ImageQuality, warnings);
            response.NutritionReliabilityScore = NutritionQualityEvaluator.CalculateReliabilityScore(
                enriched.NormalizedProfile,
                warnings,
                response.ImageQuality.SafeForPreciseNutritionAnalysis ? 0 : 10);
        }

        response.IngredientContext = NutritionQualityEvaluator.BuildIngredientContext(response.Analysis.Ingredients, enriched.ProcessingLevel);
    }

    private static List<NutritionQuickFlagDto> BuildQuickFlags(UnifiedNutritionScore score, NutritionProcessingClassificationDto processingClassification)
    {
        var flags = new List<NutritionQuickFlagDto>();
        flags.AddRange(score.Highlights.Take(2).Select(highlight => new NutritionQuickFlagDto { Type = "positive", Label = HumanizeFlag(highlight) }));

        if (processingClassification.Level == "ultra_processed")
            flags.Add(new NutritionQuickFlagDto { Type = "warning", Label = "Ultraprocessado" });

        flags.AddRange(score.Warnings.Take(3).Select(warning => new NutritionQuickFlagDto
        {
            Type = warning.Contains("EVITAR", StringComparison.OrdinalIgnoreCase) ? "danger" : "warning",
            Label = HumanizeFlag(warning)
        }));

        return flags
            .Where(flag => !string.IsNullOrWhiteSpace(flag.Label))
            .GroupBy(flag => flag.Label, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(6)
            .ToList();
    }

    private static string HumanizeFlag(string value)
    {
        var text = value.Replace("⚠️", string.Empty).Trim();
        if (text.Contains("açúcar", StringComparison.OrdinalIgnoreCase)) return "Contém açúcar adicionado.";
        if (text.Contains("sódio", StringComparison.OrdinalIgnoreCase)) return "Atenção ao sódio.";
        if (text.Contains("gordura saturada", StringComparison.OrdinalIgnoreCase)) return "Atenção à gordura saturada.";
        return text.TrimEnd('.') + ".";
    }
}
