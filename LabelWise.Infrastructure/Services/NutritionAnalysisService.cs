using System.Diagnostics;
using System.Text.Json;
using LabelWise.Application.DTOs.AI;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Validation;
using LabelWise.Application.Presentation;
using LabelWise.Application.Scoring;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services
{
    public class NutritionAnalysisService : INutritionAnalysisService
    {
        private const string CategoryEstimateBasis = "Estimativa padronizada por 100g para a categoria (tabela nutricional não visível)";
        private const string DatabaseEstimateBasis = "Estimativa por perfil nutricional da categoria no banco de dados (tabela nutricional não visível)";
        private const string LowNutritionConfidenceSummaryPrefix = "Nota: Estas são estimativas baseadas na categoria do produto, pois as informações nutricionais não estão totalmente visíveis na foto.";

        private static readonly IReadOnlyDictionary<string, int> CategoryBaseScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["arroz integral"] = 68,
            ["achocolatado em pó"] = 38,
            ["achocolatado"] = 38,
            ["biscoito recheado"] = 34,
            ["biscoito amanteigado"] = 38,
            ["salgadinho"] = 28,
            ["refrigerante"] = 18,
            ["chocolate"] = 32,
            ["pão de forma"] = 56,
            ["arroz"] = 60,
            ["iogurte comum"] = 60,
            ["iogurte proteico"] = 78,
            ["barra proteica"] = 74,
            ["queijo light"] = 68,
            ["queijo minas"] = 62
        };

        private static readonly IReadOnlyDictionary<string, int> CategoryScoreCaps = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["achocolatado"] = 58,
            ["biscoito recheado"] = 46,
            ["refrigerante"] = 38,
            ["salgadinho"] = 42,
            ["chocolate"] = 52
        };

        private readonly IVisualInterpreter _visualInterpreter;
        private readonly INutritionSanitizer _nutritionSanitizer;
        private readonly ICategoryNormalizationService _categoryNormalization;
        private readonly IDatabaseNutritionFallbackService _databaseFallback;
        private readonly IScoreInterpretationService _scoreInterpretationService;
        private readonly IProductRepository _productRepository;
        private readonly IAnalysisWriteRepository _analysisWriteRepository;
        private readonly ILogger<NutritionAnalysisService> _logger;

        public NutritionAnalysisService(
            IVisualInterpreter visualInterpreter,
            INutritionSanitizer nutritionSanitizer,
            ICategoryNormalizationService categoryNormalization,
            IDatabaseNutritionFallbackService databaseFallback,
            IScoreInterpretationService scoreInterpretationService,
            IProductRepository productRepository,
            IAnalysisWriteRepository analysisWriteRepository,
            ILogger<NutritionAnalysisService> logger)
        {
            _visualInterpreter = visualInterpreter ?? throw new ArgumentNullException(nameof(visualInterpreter));
            _nutritionSanitizer = nutritionSanitizer ?? throw new ArgumentNullException(nameof(nutritionSanitizer));
            _categoryNormalization = categoryNormalization ?? throw new ArgumentNullException(nameof(categoryNormalization));
            _databaseFallback = databaseFallback ?? throw new ArgumentNullException(nameof(databaseFallback));
            _scoreInterpretationService = scoreInterpretationService ?? throw new ArgumentNullException(nameof(scoreInterpretationService));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _analysisWriteRepository = analysisWriteRepository ?? throw new ArgumentNullException(nameof(analysisWriteRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<NutritionAnalysisResponseDto> AnalyzeProductImageAsync(
            byte[] imageData,
            string fileName,
            string languageCode = "pt",
            List<string>? requestedProfiles = null,
            Guid? userId = null,
            string? deviceId = null)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var visionResult = await PerformVisualInterpretationAsync(imageData);

                if (visionResult == null)
                {
                    return CreateErrorResponse("Não foi possível interpretar a imagem", stopwatch.Elapsed.TotalSeconds);
                }

                var analysisMode = DetermineAnalysisMode(visionResult);
                var autoWarnings = BuildWarnings(analysisMode);

                var response = new NutritionAnalysisResponseDto
                {
                    Success = string.IsNullOrWhiteSpace(visionResult.ErrorMessage),
                    ProductName = visionResult.ProductName ?? visionResult.ProbableProductName,
                    Brand = visionResult.Brand ?? visionResult.ProbableBrand,
                    Category = visionResult.Category ?? visionResult.ProbableCategory,
                    PackageWeight = visionResult.PackageWeight ?? visionResult.ProbablePackageWeight,
                    AnalysisMode = analysisMode,
                    VisibleClaims = visionResult.VisibleClaims ?? new List<string>(),
                    EstimatedNutritionProfile = EnsureBasis(visionResult.EstimatedNutritionProfile),
                    Classification = visionResult.Classification,
                    Summary = BuildSummary(visionResult, analysisMode),
                    ConfidenceDetails = visionResult.ConfidenceDetails,
                    Warnings = MergeWarnings(visionResult.Warnings, autoWarnings),
                    ErrorMessage = visionResult.ErrorMessage,
                    ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds
                };

                response.VisibleClaims ??= new List<string>();

                // Pipeline correto:
                // 1. Aplica a camada de validação e normalização determinística
                NutritionAnalysisValidator.Apply(response);

                // 2. Aplica fallback de productName quando necessário
                ApplyProductNameFallback(response);

                // 2.1. Determina se há dados nutricionais confiáveis ANTES de qualquer fallback
                DetermineNutritionDataReliability(response, visionResult);

                // 3. Aplica o modo híbrido com prudência quando não há tabela visível
                //    Agora usa o pipeline de normalização + fallback persistido no MongoDB
                //    MAS só preenche valores numéricos se hasReliableNutritionData = true
                await ApplyHybridCategoryInferenceAsync(response, visionResult);

                // 3.1. Reaplica higienização após inferência para evitar overwrite de valores saneados
                ApplyNutritionSanitization(response);

                // 4. Aplica overrides determinísticos por categoria
                ApplyCategoryOverrides(response);

                // 4.1. NOVO: Aplica regras de conservadorismo qualitativo quando não há dados confiáveis
                ApplyConservativeQualitativeRules(response, visionResult);

                // 4.2. Evita totalizações imprecisas quando a base da tabela é ambígua
                SanitizeEstimatedPackageCalories(response);

                // 5. Determina se a resposta é utilizável (success real)
                bool hasUsableData = HasUsableAnalysis(response);
                response.Success = hasUsableData;
                response.ErrorMessage = hasUsableData ? null : "Não foi possível interpretar dados úteis da imagem";

                // 6. Determina principalOffender ANTES de calcular o score
                // para que ScoreInterpretationService receba o ofensor correto
                ApplyPrincipalOffenderIfMissing(response);

                // 6.1. Calcula score com o principalOffender já definido
                ApplyScore(response);

                // 7. Gera warnings automáticos e summary final mais útil para o app
                if (response.Success)
                {
                    ApplyAutomaticWarnings(response);
                    ApplyQualitativeAlerts(response);
                    RefineWarningsForMobile(response);
                    response.Summary = BuildFinalSummary(response);
                }

                // 8. Garante coerência final entre analysisMode, macros, basis, summary e score
                EnforceResponseCoherence(response);

                if (response.Success)
                {
                    NutritionTextPresentationBuilder.Apply(response);

                    // 8.1. NOVO: Aplica modo conservador OBRIGATÓRIO quando não há dados quantitativos
                    ApplyConservativeModeEnforcement(response, visionResult, _logger);
                }

                if (response.Success)
                {
                    try
                    {
                        response.AnalysisId = await PersistAnalysisAsync(response, fileName, userId, deviceId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Falha ao persistir histórico da análise nutricional simplificada. A resposta será retornada sem AnalysisId.");

                        response.AnalysisId = null;
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante análise nutricional");
                return CreateErrorResponse($"Erro durante análise: {ex.Message}", stopwatch.Elapsed.TotalSeconds);
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        private async Task<VisualInterpretationResult?> PerformVisualInterpretationAsync(byte[] imageData)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"nutrition_{Guid.NewGuid()}.jpg");

            try
            {
                await File.WriteAllBytesAsync(tempPath, imageData);

                _logger.LogInformation("Starting nutrition-specific visual interpretation for temp image: {TempPath}", tempPath);

                var result = await _visualInterpreter.InterpretImageAsync(new VisualInterpretationRequest { ImagePath = tempPath });

                _logger.LogInformation("Visual interpretation completed with confidence: {Confidence}", result.InterpretationConfidence);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during visual interpretation for nutrition analysis");
                return null;
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try 
                    { 
                        File.Delete(tempPath); 
                        _logger.LogDebug("Temporary file deleted: {TempPath}", tempPath);
                    } 
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temporary file: {TempPath}", tempPath);
                    }
                }
            }
        }

        private static AnalysisMode DetermineAnalysisMode(VisualInterpretationResult visionResult)
        {
            return visionResult.ProbableCaptureType == CaptureType.NutritionTable
                ? AnalysisMode.FullNutritionLabel
                : AnalysisMode.FrontOfPackageOnly;
        }

        private static List<string> BuildWarnings(AnalysisMode analysisMode)
        {
            if (analysisMode != AnalysisMode.FrontOfPackageOnly)
            {
                return new List<string>();
            }

            return new List<string>
            {
                "Análise estimada com base na frente da embalagem e na categoria."
            };
        }

        private static List<string> MergeWarnings(List<string>? visionWarnings, List<string> autoWarnings)
        {
            return (visionWarnings ?? new List<string>())
                .Concat(autoWarnings)
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static EstimatedNutritionProfileDto EnsureBasis(EstimatedNutritionProfileDto? dto)
        {
            if (dto == null)
            {
                return new EstimatedNutritionProfileDto
                {
                    Basis = "Estimativa por categoria visual, sem leitura da tabela nutricional oficial"
                };
            }

            if (string.IsNullOrWhiteSpace(dto.Basis))
            {
                dto.Basis = "Estimativa por categoria visual, sem leitura da tabela nutricional oficial";
            }

            return dto;
        }

        private static string? BuildSummary(VisualInterpretationResult visionResult, AnalysisMode analysisMode)
        {
            var already = visionResult.Summary;
            if (!string.IsNullOrWhiteSpace(already))
            {
                return already;
            }

            var category = visionResult.Category ?? visionResult.ProbableCategory;
            var claims = visionResult.VisibleClaims ?? new List<string>();

            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(category))
            {
                parts.Add(category.Trim());
            }

            if (claims.Any(c => c.Contains("vitamina", StringComparison.OrdinalIgnoreCase) ||
                                c.Contains("mineral", StringComparison.OrdinalIgnoreCase)))
            {
                parts.Add("com fortificação de vitaminas e minerais");
            }

            if (analysisMode == AnalysisMode.FrontOfPackageOnly)
            {
                parts.Add("com provável presença relevante de açúcar e baixa densidade proteica (estimativa por categoria)");
            }

            if (parts.Count == 0) return null;

            var text = string.Join(", ", parts).Trim();
            return char.ToUpperInvariant(text[0]) + text.Substring(1) + ".";
        }

        private static NutritionAnalysisResponseDto CreateErrorResponse(string errorMessage, double processingTimeSeconds)
        {
            return new NutritionAnalysisResponseDto
            {
                Success = false,
                VisibleClaims = new List<string>(),
                Warnings = new List<string>(),
                Score = new NutritionalScore
                {
                    Value = 0,
                    Status = "baixo",
                    Color = "gray",
                    Label = "Análise insuficiente"
                },
                ErrorMessage = errorMessage,
                ProcessingTimeSeconds = processingTimeSeconds
            };
        }

        private void ApplyScore(NutritionAnalysisResponseDto response)
        {
            if (response == null)
            {
                return;
            }

            if (!response.Success)
            {
                response.Score = new NutritionalScore
                {
                    Value = 0,
                    Status = "baixo",
                    Color = "gray",
                    Label = "Análise insuficiente",
                    SafeLabel = "Análise insuficiente",
                    AbsoluteRecommendation = "Não foi possível determinar uma recomendação confiável.",
                    SemanticRecommendation = "Não foi possível determinar uma recomendação confiável.",
                    ScoreInterpretation = "Análise insuficiente para interpretar o score com segurança.",
                    AbsoluteLabel = "analise_insuficiente",
                    RecommendationLevel = "analise_insuficiente"
                };

                return;
            }

            var calculatedScore = NutritionScoreCalculator.Calculate(response);

            if (string.IsNullOrWhiteSpace(response.PrincipalOffender)
                && !string.IsNullOrWhiteSpace(calculatedScore.PrincipalOffender))
            {
                response.PrincipalOffender = calculatedScore.PrincipalOffender;
            }

            var interpretationContext = new ScoreInterpretationContext
            {
                Score = calculatedScore.Value,
                ProductName = response.ProductName,
                Category = response.Category,
                VisibleClaims = response.VisibleClaims ?? new List<string>(),
                PrincipalOffender = response.PrincipalOffender,
                NutritionProfile = response.EstimatedNutritionProfile,
                Classification = response.Classification
            };

            var semanticScore = _scoreInterpretationService.BuildSafeScoreLabel(interpretationContext);

            response.Score = calculatedScore;
            response.Score.SafeLabel = semanticScore.SafeLabel;
            response.Score.ProcessingLevel = semanticScore.ProcessingLevel;
            response.Score.RequiresModeration = semanticScore.RequiresModeration || calculatedScore.RequiresModeration;
            response.Score.IsUltraProcessed = (calculatedScore.IsUltraProcessed == true) || (semanticScore.IsUltraProcessed == true);
            response.Score.ComparativeRecommendation = semanticScore.ComparativeRecommendation;
            response.Score.ComparativeLabel = semanticScore.ComparativeLabel;

            if (string.IsNullOrWhiteSpace(response.Score.AbsoluteRecommendation))
            {
                response.Score.AbsoluteRecommendation = semanticScore.AbsoluteRecommendation;
            }

            response.Score.SemanticRecommendation = response.Score.AbsoluteRecommendation;

            if (string.IsNullOrWhiteSpace(response.Score.Reason))
            {
                response.Score.Reason = BuildHealthScoreReason(response.Category, response.EstimatedNutritionProfile, response.HasReliableNutritionData);
            }
            else if (!response.HasReliableNutritionData)
            {
                // Sanitizar reason quando não há dados confiáveis
                response.Score.Reason = SanitizeScoreReason(response.Score.Reason);
            }

            _logger.LogInformation(
                "HealthScore calculated: Final={Final}, Label={Label}, ProcessingLevel={ProcessingLevel}, RequiresModeration={RequiresModeration}, FinalRecommendation={FinalRecommendation}, Category={Category}, HasReliableData={HasReliableData}, Confidence={Confidence}, PrincipalOffender={PrincipalOffender}",
                calculatedScore.Value,
                response.Score.Label,
                response.Score.ProcessingLevel,
                response.Score.RequiresModeration,
                response.Score.AbsoluteRecommendation,
                response.Category ?? "unknown",
                response.HasReliableNutritionData,
                response.Score.Confidence,
                response.Score.PrincipalOffender);
        }

        private async Task ApplyHybridCategoryInferenceAsync(
            NutritionAnalysisResponseDto response,
            VisualInterpretationResult visionResult)
        {
            // NOVA REGRA: Se não há dados confiáveis, NÃO preencher valores numéricos
            // Apenas manter basis descritiva
            if (!response.HasReliableNutritionData)
            {
                _logger.LogInformation(
                    "[NutritionV1] Skipping numeric nutrition fallback. HasReliableData=false, FallbackType={FallbackType}, Category={Category}",
                    response.FallbackType,
                    response.Category ?? "unknown");

                // Criar perfil apenas com basis descritiva, SEM valores numéricos
                if (response.EstimatedNutritionProfile == null)
                {
                    response.EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                    {
                        Basis = "Análise baseada apenas na categoria, sem dados nutricionais exatos da tabela nutricional"
                    };
                }
                else if (string.IsNullOrWhiteSpace(response.EstimatedNutritionProfile.Basis))
                {
                    response.EstimatedNutritionProfile.Basis = "Análise baseada apenas na categoria, sem dados nutricionais exatos da tabela nutricional";
                }

                // IMPORTANTE: Limpar quaisquer valores numéricos que possam ter vindo da IA
                // quando não são confiáveis
                response.EstimatedNutritionProfile.CaloriesPer100g = null;
                response.EstimatedNutritionProfile.EstimatedPackageCalories = null;
                response.EstimatedNutritionProfile.EstimatedSugarPer100g = null;
                response.EstimatedNutritionProfile.EstimatedProteinPer100g = null;
                response.EstimatedNutritionProfile.EstimatedSodiumPer100g = null;
                response.EstimatedNutritionProfile.EstimatedFiberPer100g = null;
                response.EstimatedNutritionProfile.EstimatedFatPer100g = null;

                return;
            }

            // APENAS quando hasReliableNutritionData = true:
            // Aplicar fallback do banco de dados para valores ausentes
            if (response.AnalysisMode != AnalysisMode.FrontOfPackageOnly)
            {
                return;
            }

            var categoryEstimate = await BuildCategoryEstimateFromDatabaseAsync(
                response.Category,
                visionResult.ProductName ?? visionResult.ProbableProductName,
                visionResult.VisibleClaims,
                visionResult.Brand ?? visionResult.ProbableBrand,
                response.PackageWeight);

            if (response.EstimatedNutritionProfile == null)
            {
                response.EstimatedNutritionProfile = categoryEstimate;
                return;
            }

            MergeMissingNutritionValues(response.EstimatedNutritionProfile, categoryEstimate);
        }

        /// <summary>
        /// Busca o perfil nutricional no MongoDB usando o pipeline de normalização + fallback.
        /// Elimina a dependência de dicionários hardcoded.
        /// </summary>
        private async Task<EstimatedNutritionProfileDto> BuildCategoryEstimateFromDatabaseAsync(
            string? category,
            string? productName,
            IEnumerable<string>? visibleClaims,
            string? brand,
            string? packageWeight)
        {
            try
            {
                // === STEP 1: Normalizar categoria via banco ===
                var normalization = await _categoryNormalization.NormalizeAsync(
                    category,
                    productName,
                    visibleClaims,
                    brand);

                _logger.LogInformation(
                    "[NutritionV1] Category normalization. Category={Category}, ProductName={ProductName}, " +
                    "NormalizedCode={NormalizedCode}, Confidence={Confidence}, IsNormalized={IsNormalized}",
                    category,
                    productName,
                    normalization.NormalizedCategoryCode,
                    normalization.Confidence,
                    normalization.IsNormalized);

                if (!normalization.IsNormalized || string.IsNullOrWhiteSpace(normalization.NormalizedCategoryCode))
                {
                    _logger.LogWarning(
                        "[NutritionV1] Category not normalized. Returning generic fallback. Category={Category}, ProductName={ProductName}",
                        category, productName);

                    return BuildGenericFallbackEstimate(packageWeight);
                }

                // === STEP 2: Buscar perfil no banco via fallback service (sem re-resolução) ===
                var fallbackResult = await _databaseFallback.ApplyFallbackAsync(
                    null,
                    normalization.NormalizedCategoryCode,
                    nameof(AnalysisMode.FrontOfPackageOnly));

                if (fallbackResult.ProfileRejected || fallbackResult.NormalizedCategoryCode == null)
                {
                    _logger.LogWarning(
                        "[NutritionV1] Database fallback returned no usable profile. " +
                        "RequestedCode={RequestedCode}, Rejected={Rejected}. Using generic fallback.",
                        normalization.NormalizedCategoryCode,
                        fallbackResult.ProfileRejected);

                    return BuildGenericFallbackEstimate(packageWeight);
                }

                _logger.LogInformation(
                    "[NutritionV1] Database fallback applied. " +
                    "RequestedCode={RequestedCode}, AppliedCode={AppliedCode}, AppliedName={AppliedName}, " +
                    "Confidence={Confidence}, UsedParent={UsedParent}",
                    normalization.NormalizedCategoryCode,
                    fallbackResult.NormalizedCategoryCode,
                    fallbackResult.NormalizedCategoryName,
                    fallbackResult.Confidence,
                    fallbackResult.UsedParentCategoryFallback);

                // === STEP 3: Converter para EstimatedNutritionProfileDto com basis auditável ===
                var profile = fallbackResult.Profile;
                profile.Basis = $"{DatabaseEstimateBasis} ({fallbackResult.NormalizedCategoryName ?? fallbackResult.NormalizedCategoryCode} [{fallbackResult.NormalizedCategoryCode}])";

                // Calcular calorias da embalagem se peso disponível
                var weightInGrams = ParsePackageWeightInGrams(packageWeight);
                if (profile.CaloriesPer100g.HasValue && weightInGrams.HasValue)
                {
                    profile.EstimatedPackageCalories = Math.Round(profile.CaloriesPer100g.Value * weightInGrams.Value / 100d, 0);
                }

                return profile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[NutritionV1] Error during database fallback for category={Category}, productName={ProductName}. Using generic fallback.",
                    category, productName);

                return BuildGenericFallbackEstimate(packageWeight);
            }
        }

        private static EstimatedNutritionProfileDto BuildGenericFallbackEstimate(string? packageWeight)
        {
            return new EstimatedNutritionProfileDto
            {
                Basis = "Não foi possível estimar perfil nutricional com confiança suficiente para a categoria"
            };
        }

        private void ApplyNutritionSanitization(NutritionAnalysisResponseDto response)
        {
            var sanitizationResult = _nutritionSanitizer.Sanitize(response);
            if (sanitizationResult.IsSuccess && sanitizationResult.Value != null)
            {
                return;
            }

            _logger.LogWarning(
                "Nutrition sanitization failed during NutritionAnalysisService post-processing for category {Category}: {Errors}",
                response.Category ?? "unknown",
                string.Join("; ", sanitizationResult.Errors));
        }

        private static void MergeMissingNutritionValues(EstimatedNutritionProfileDto target, EstimatedNutritionProfileDto fallback)
        {
            var complemented = HasAnyMissingNutritionMetric(target, fallback);

            target.CaloriesPer100g ??= fallback.CaloriesPer100g;
            target.EstimatedPackageCalories ??= fallback.EstimatedPackageCalories;
            target.EstimatedSugarPer100g ??= fallback.EstimatedSugarPer100g;
            target.EstimatedProteinPer100g ??= fallback.EstimatedProteinPer100g;
            target.EstimatedSodiumPer100g ??= fallback.EstimatedSodiumPer100g;
            target.EstimatedFiberPer100g ??= fallback.EstimatedFiberPer100g;
            target.EstimatedFatPer100g ??= fallback.EstimatedFatPer100g;

            if (string.IsNullOrWhiteSpace(target.Basis))
            {
                target.Basis = fallback.Basis;
                return;
            }

            if (!target.Basis.Contains(CategoryEstimateBasis, StringComparison.OrdinalIgnoreCase)
                && complemented)
            {
                target.Basis = $"{target.Basis}. Complementado com estimativa genérica por categoria para campos ausentes.";
            }
        }

        private static bool HasAnyMissingNutritionMetric(EstimatedNutritionProfileDto target, EstimatedNutritionProfileDto fallback)
        {
            return (target.CaloriesPer100g == null && fallback.CaloriesPer100g != null)
                || (target.EstimatedPackageCalories == null && fallback.EstimatedPackageCalories != null)
                || (target.EstimatedSugarPer100g == null && fallback.EstimatedSugarPer100g != null)
                || (target.EstimatedProteinPer100g == null && fallback.EstimatedProteinPer100g != null)
                || (target.EstimatedSodiumPer100g == null && fallback.EstimatedSodiumPer100g != null)
                || (target.EstimatedFiberPer100g == null && fallback.EstimatedFiberPer100g != null)
                || (target.EstimatedFatPer100g == null && fallback.EstimatedFatPer100g != null);
        }

        private static void ApplyCategoryOverrides(NutritionAnalysisResponseDto response)
        {
            response.Classification ??= new ProductClassificationDto();
            response.Classification.Diabetic ??= new HealthProfileResult();
            response.Classification.BloodPressure ??= new HealthProfileResult();
            response.Classification.WeightLoss ??= new HealthProfileResult();
            response.Classification.MuscleGain ??= new HealthProfileResult();

            var normalizedCategory = NormalizeCategoryKey(response.Category);

            if (normalizedCategory.Contains("achocolatado", StringComparison.OrdinalIgnoreCase))
            {
                ApplyCategoryOverrideIfWeakerOrMissing(response.Classification.Diabetic, "nao_recomendado",
                    "Achocolatados costumam ter alta carga de açúcar, mesmo quando a tabela nutricional não está visível.");

                ApplyCategoryOverrideIfWeakerOrMissing(response.Classification.WeightLoss, "nao_indicado",
                    "Categoria tipicamente densa em açúcar e calorias, desfavorável para emagrecimento.");

                ApplyCategoryOverrideIfWeakerOrMissing(response.Classification.MuscleGain, "fraco",
                    "Produto com baixo teor proteico e perfil mais energético do que proteico.");
            }

            if (IsBeverageCategory(normalizedCategory))
            {
                var sugar = response.EstimatedNutritionProfile?.EstimatedSugarPer100g ?? 0;
                // Para bebidas, 5g/100ml = 10g numa porção de 200ml — limiar relevante para usuário.
                // Usa DowngradeClassificationIfNutritionWarrants para rebaixar mesmo quando a IA
                // retornou "adequado" sem evidência suficiente para sustentar essa classificação.
                if (sugar >= 5)
                {
                    DowngradeClassificationIfNutritionWarrants(response.Classification.WeightLoss!, "consumo_moderado",
                        "Bebida com presença relevante de açúcar por porção. Consumo moderado recomendado para dietas de emagrecimento.");

                    DowngradeClassificationIfNutritionWarrants(response.Classification.Diabetic!, "consumo_moderado",
                        "Bebida com presença relevante de açúcar. Atenção ao consumo para quem controla a glicemia.");
                }
            }

            // Regras universais guiadas pelos dados nutricionais reais
            // Espelha a lógica já usada no EnhancedNutritionPipelineOrchestrator
            if (response.HasReliableNutritionData)
            {
                ApplyNutritionDataDrivenClassificationOverrides(response);
            }
        }

        /// <summary>
        /// Aplica overrides de classificação baseados nos valores nutricionais reais,
        /// independente de categoria. Garante que weightLoss/diabetic não sejam positivos
        /// quando os dados nutricionais indicam o contrário.
        /// </summary>
        private static void ApplyNutritionDataDrivenClassificationOverrides(NutritionAnalysisResponseDto response)
        {
            var nutrition = response.EstimatedNutritionProfile;
            if (nutrition == null) return;

            var sugar = nutrition.EstimatedSugarPer100g ?? 0;
            var calories = nutrition.CaloriesPer100g ?? 0;
            var fat = nutrition.EstimatedFatPer100g ?? 0;
            var sodium = nutrition.EstimatedSodiumPer100g ?? 0;

            var normalizedCategory = NormalizeCategoryKey(response.Category);
            var isBeverage = IsBeverageCategory(normalizedCategory);
            var classification = response.Classification!;

            // Thresholds diferenciados para bebidas vs sólidos.
            // Para bebidas: 5g/100ml = 10g/200ml (porção típica) já é impacto relevante.
            // Para sólidos: limiar mais alto pois a porção tende a ser menor.
            var sugarModerateThreshold = isBeverage ? 5.0 : 8.0;
            var sugarHighThreshold = isBeverage ? 10.0 : 15.0;
            var caloriesModerateThreshold = isBeverage ? 100.0 : 220.0;
            var caloriesHighThreshold = isBeverage ? 200.0 : 350.0;
            var fatModerateThreshold = isBeverage ? 3.0 : 10.0;
            var fatHighThreshold = isBeverage ? 8.0 : 17.5;

            // WeightLoss
            if (calories > caloriesHighThreshold || sugar > sugarHighThreshold || fat > fatHighThreshold)
            {
                DowngradeClassificationIfNutritionWarrants(classification.WeightLoss!, "nao_recomendado",
                    isBeverage
                        ? "Bebida com alto teor de açúcar ou calorias não favorece emagrecimento."
                        : "Alta densidade energética ou excesso de açúcar/gordura não favorece emagrecimento.");
            }
            else if (calories > caloriesModerateThreshold || sugar > sugarModerateThreshold || fat > fatModerateThreshold)
            {
                DowngradeClassificationIfNutritionWarrants(classification.WeightLoss!, "consumo_moderado",
                    isBeverage
                        ? "Bebida com açúcar relevante por porção. Consumo moderado recomendado para quem busca emagrecimento."
                        : "Combinação de açúcar, gordura ou calorias exige atenção no controle de porção.");
            }

            // Diabetic
            if (sugar > sugarHighThreshold)
            {
                DowngradeClassificationIfNutritionWarrants(classification.Diabetic!, "nao_recomendado",
                    isBeverage
                        ? "Alto teor de açúcar para uma bebida. Não recomendado para quem controla a glicemia."
                        : "Alto teor de açúcar não recomendado para quem controla a glicemia.");
            }
            else if (sugar > sugarModerateThreshold)
            {
                DowngradeClassificationIfNutritionWarrants(classification.Diabetic!, "consumo_moderado",
                    isBeverage
                        ? "Bebida com açúcar relevante por porção. Atenção ao consumo para quem controla a glicemia."
                        : "Teor moderado de açúcar exige atenção para quem controla a glicemia.");
            }

            // BloodPressure: sódio > 400 já merece atenção (thresholds iguais para bebidas/sólidos)
            if (sodium > 600)
            {
                DowngradeClassificationIfNutritionWarrants(classification.BloodPressure!, "nao_recomendado",
                    "Teor elevado de sódio não é indicado para quem controla a pressão arterial.");
            }
            else if (sodium > 400)
            {
                DowngradeClassificationIfNutritionWarrants(classification.BloodPressure!, "consumo_moderado",
                    "Teor moderado de sódio pede moderação para quem cuida da pressão.");
            }
        }

        /// <summary>
        /// Rebaixa a classificação se os dados nutricionais indicam status mais restritivo.
        /// Nunca promove — apenas rebaixa. Preserva status já mais restritivos que o proposto.
        /// </summary>
        private static void DowngradeClassificationIfNutritionWarrants(
            HealthProfileResult classification, string proposedStatus, string proposedReason)
        {
            // Ordem crescente de restrição
            var restrictionOrder = new[] { "favoravel", "adequado", "consumo_moderado", "fraco", "nao_recomendado", "nao_indicado" };

            var currentIdx = Array.FindIndex(restrictionOrder, s =>
                string.Equals(s, classification.Status, StringComparison.OrdinalIgnoreCase));
            var proposedIdx = Array.FindIndex(restrictionOrder, s =>
                string.Equals(s, proposedStatus, StringComparison.OrdinalIgnoreCase));

            // Só rebaixa se o proposto for mais restritivo que o atual
            // Se o status atual é indeterminado/vazio, também aplica
            if (string.IsNullOrWhiteSpace(classification.Status)
                || classification.Status.Equals("indeterminado", StringComparison.OrdinalIgnoreCase)
                || (currentIdx >= 0 && proposedIdx > currentIdx))
            {
                classification.Status = proposedStatus;
                classification.Reason = proposedReason;
            }
        }

        private static void ApplyCategoryOverrideIfWeakerOrMissing(
            HealthProfileResult classification, string proposedStatus, string proposedReason)
        {
            if (string.IsNullOrWhiteSpace(classification.Status)
                || classification.Status.Equals("indeterminado", StringComparison.OrdinalIgnoreCase))
            {
                classification.Status = proposedStatus;
                classification.Reason = proposedReason;
            }
        }

        private static bool IsBeverageCategory(string normalizedCategory)
        {
            return normalizedCategory.Contains("suco", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("néctar", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("nectar", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("refresco", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("limonada", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("bebida à base", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("bebida a base", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Define o principalOffender quando está nulo mas há um nutriente claramente dominante.
        /// Para bebidas, o limiar de açúcar é reduzido pois porções líquidas concentram impacto.
        /// </summary>
        private static void ApplyPrincipalOffenderIfMissing(NutritionAnalysisResponseDto response)
        {
            if (!string.IsNullOrWhiteSpace(response.PrincipalOffender)) return;
            if (!response.HasReliableNutritionData) return;

            var nutrition = response.EstimatedNutritionProfile;
            if (nutrition == null) return;

            var normalizedCategory = NormalizeCategoryKey(response.Category);
            var isBeverage = IsBeverageCategory(normalizedCategory);

            var sugar = nutrition.EstimatedSugarPer100g ?? 0;
            var sodium = nutrition.EstimatedSodiumPer100g ?? 0;
            var fat = nutrition.EstimatedFatPer100g ?? 0;

            // Para bebidas: 6g/100ml já é relevante (12g/200ml = ~48% da meta diária da OMS)
            // Para sólidos: a partir de 10g/100g
            var sugarThreshold = isBeverage ? 6.0 : 10.0;

            var sugarScore = sugar >= sugarThreshold ? (sugar - sugarThreshold) * (isBeverage ? 3 : 2) : 0;
            var sodiumScore = sodium > 400 ? (sodium - 400) / 100.0 : 0;
            var fatScore = fat > 10 ? (fat - 10) / 2.0 : 0;

            if (sugarScore > 0 && sugarScore >= sodiumScore && sugarScore >= fatScore)
            {
                response.PrincipalOffender = "sugar";
            }
            else if (sodiumScore > 0 && sodiumScore >= fatScore)
            {
                response.PrincipalOffender = "sodium";
            }
            else if (fatScore > 0)
            {
                response.PrincipalOffender = "fat";
            }
        }

        /// <summary>
        /// Aplica regras de conservadorismo qualitativo quando não há dados nutricionais confiáveis.
        /// Elimina elogios nutricionais sem evidência e torna classificações mais prudentes.
        /// </summary>
        private static void ApplyConservativeQualitativeRules(
            NutritionAnalysisResponseDto response,
            VisualInterpretationResult visionResult)
        {
            // REGRA 1: Se não há dados confiáveis, classificações devem ser conservadoras
            if (!response.HasReliableNutritionData)
            {
                ApplyConservativeClassifications(response, visionResult);
            }

            // REGRA 2: Inferir riscos de ingredientes visíveis
            InferRisksFromVisibleIngredients(response, visionResult);

            // REGRA 3: Sanitizar reasons de classificações para evitar afirmações otimistas sem base
            SanitizeClassificationReasons(response);
        }

        /// <summary>
        /// Aplica classificações conservadoras quando não há dados nutricionais confiáveis.
        /// Evita afirmações positivas sem evidência.
        /// </summary>
        private static void ApplyConservativeClassifications(
            NutritionAnalysisResponseDto response,
            VisualInterpretationResult visionResult)
        {
            response.Classification ??= new ProductClassificationDto();

            // Para cada perfil, se status está indeterminado ou com afirmação positiva sem base,
            // substituir por classificação conservadora

            // Diabético
            if (response.Classification.Diabetic != null)
            {
                if (HasUnsubstantiatedPositiveClaim(response.Classification.Diabetic.Status, response.Classification.Diabetic.Reason))
                {
                    response.Classification.Diabetic.Status = "indeterminado";
                    response.Classification.Diabetic.Reason = "Não foi possível confirmar o teor de açúcares sem tabela nutricional visível.";
                }
            }

            // Pressão arterial
            if (response.Classification.BloodPressure != null)
            {
                if (HasUnsubstantiatedPositiveClaim(response.Classification.BloodPressure.Status, response.Classification.BloodPressure.Reason))
                {
                    response.Classification.BloodPressure.Status = "indeterminado";
                    response.Classification.BloodPressure.Reason = "Não foi possível confirmar o teor de sódio sem tabela nutricional visível.";
                }
            }

            // Emagrecimento
            if (response.Classification.WeightLoss != null)
            {
                if (HasUnsubstantiatedPositiveClaim(response.Classification.WeightLoss.Status, response.Classification.WeightLoss.Reason))
                {
                    response.Classification.WeightLoss.Status = "indeterminado";
                    response.Classification.WeightLoss.Reason = "Não foi possível confirmar densidade calórica e perfil nutricional sem tabela nutricional visível.";
                }
            }

            // Ganho de massa
            if (response.Classification.MuscleGain != null)
            {
                if (HasUnsubstantiatedPositiveClaim(response.Classification.MuscleGain.Status, response.Classification.MuscleGain.Reason))
                {
                    response.Classification.MuscleGain.Status = "indeterminado";
                    response.Classification.MuscleGain.Reason = "Não foi possível confirmar o teor de proteína sem tabela nutricional visível.";
                }
            }
        }

        /// <summary>
        /// Verifica se uma classificação tem afirmação positiva não substanciada.
        /// Retorna true se o status for positivo MAS a reason mencionar termos como "baixo teor", "boa pontuação", etc.
        /// sem dados quantitativos para suportar.
        /// </summary>
        private static bool HasUnsubstantiatedPositiveClaim(string? status, string? reason)
        {
            if (string.IsNullOrWhiteSpace(status) || string.IsNullOrWhiteSpace(reason))
            {
                return false;
            }

            // Status positivos/neutros que precisam de evidência
            var positiveStatuses = new[] { "adequado", "bom", "consumo_moderado", "recomendado" };
            var isPositiveStatus = positiveStatuses.Any(s => status.Contains(s, StringComparison.OrdinalIgnoreCase));

            if (!isPositiveStatus)
            {
                return false; // Status já é negativo/indeterminado, não precisa ajustar
            }

            // Verificar se reason tem afirmações otimistas sem base
            var unsubstantiatedClaims = new[]
            {
                "baixo teor",
                "baixa concentração",
                "baixo açúcar",
                "baixo sódio",
                "baixa gordura",
                "baixas calorias",
                "boa pontuação",
                "perfil equilibrado",
                "pode ajudar",
                "favorável",
                "adequado para",
                "recomendado para"
            };

            return unsubstantiatedClaims.Any(claim => reason.Contains(claim, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Infere riscos de ingredientes visíveis na lista de ingredientes ou claims.
        /// Ex: Se "sal" ou "glutamato monossódico" estão visíveis, inferir atenção para sódio.
        /// </summary>
        private static void InferRisksFromVisibleIngredients(
            NutritionAnalysisResponseDto response,
            VisualInterpretationResult visionResult)
        {
            var visibleClaims = (visionResult.VisibleClaims ?? new List<string>())
                .Concat(response.VisibleClaims ?? new List<string>())
                .Select(c => c.ToLowerInvariant())
                .ToList();

            // Inferir risco de sódio alto
            if (visibleClaims.Any(c => c.Contains("sal") ||
                                      c.Contains("glutamato monossódico") ||
                                      c.Contains("glutamato monosódico") ||
                                      c.Contains("msg") ||
                                      c.Contains("realçador de sabor")))
            {
                if (!response.InferredRisks.Contains("alto_sodio"))
                {
                    response.InferredRisks.Add("alto_sodio");
                }

                // Atualizar classificação de pressão arterial se ainda indeterminada
                if (response.Classification?.BloodPressure != null &&
                    (string.IsNullOrWhiteSpace(response.Classification.BloodPressure.Status) ||
                     response.Classification.BloodPressure.Status.Equals("indeterminado", StringComparison.OrdinalIgnoreCase)))
                {
                    response.Classification.BloodPressure.Status = "consumo_moderado";
                    response.Classification.BloodPressure.Reason = "Ingredientes sugestivos de alto teor de sódio detectados (sal, glutamato). Consumo moderado recomendado.";
                }
            }

            // Inferir risco de açúcar alto
            if (visibleClaims.Any(c => c.Contains("açúcar") ||
                                      c.Contains("acucar") ||
                                      c.Contains("xarope") ||
                                      c.Contains("glucose") ||
                                      c.Contains("frutose") ||
                                      c.Contains("sacarose") ||
                                      c.Contains("maltose")))
            {
                if (!response.InferredRisks.Contains("alto_acucar"))
                {
                    response.InferredRisks.Add("alto_acucar");
                }

                // Atualizar classificação diabética se ainda indeterminada
                if (response.Classification?.Diabetic != null &&
                    (string.IsNullOrWhiteSpace(response.Classification.Diabetic.Status) ||
                     response.Classification.Diabetic.Status.Equals("indeterminado", StringComparison.OrdinalIgnoreCase)))
                {
                    response.Classification.Diabetic.Status = "consumo_moderado";
                    response.Classification.Diabetic.Reason = "Ingredientes sugestivos de alto teor de açúcar detectados. Consumo moderado recomendado.";
                }
            }

            // Inferir risco de gordura alta
            if (visibleClaims.Any(c => c.Contains("gordura vegetal") ||
                                      c.Contains("óleo de palma") ||
                                      c.Contains("oleo de palma") ||
                                      c.Contains("gordura hidrogenada") ||
                                      c.Contains("gordura trans")))
            {
                if (!response.InferredRisks.Contains("alta_gordura"))
                {
                    response.InferredRisks.Add("alta_gordura");
                }
            }
        }

        /// <summary>
        /// Sanitiza reasons de classificações para evitar afirmações otimistas sem base.
        /// Remove frases como "baixo teor de" quando não há dados quantitativos.
        /// </summary>
        private static void SanitizeClassificationReasons(NutritionAnalysisResponseDto response)
        {
            if (response.HasReliableNutritionData)
            {
                return; // Só sanitizar quando NÃO há dados confiáveis
            }

            var classifications = new[]
            {
                response.Classification?.Diabetic,
                response.Classification?.BloodPressure,
                response.Classification?.WeightLoss,
                response.Classification?.MuscleGain
            };

            foreach (var classification in classifications)
            {
                if (classification?.Reason == null)
                {
                    continue;
                }

                // Substituir afirmações otimistas por versões conservadoras
                var reason = classification.Reason;

                reason = reason.Replace("baixo teor de açúcar", "teor de açúcar não confirmado", StringComparison.OrdinalIgnoreCase);
                reason = reason.Replace("baixo teor de sódio", "teor de sódio não confirmado", StringComparison.OrdinalIgnoreCase);
                reason = reason.Replace("baixo teor de gordura", "teor de gordura não confirmado", StringComparison.OrdinalIgnoreCase);
                reason = reason.Replace("baixas calorias", "densidade calórica não confirmada", StringComparison.OrdinalIgnoreCase);
                reason = reason.Replace("boa pontuação", "pontuação estimada", StringComparison.OrdinalIgnoreCase);
                reason = reason.Replace("perfil equilibrado", "perfil não totalmente confirmado", StringComparison.OrdinalIgnoreCase);
                reason = reason.Replace("pode ajudar em", "dados insuficientes para confirmar benefício em", StringComparison.OrdinalIgnoreCase);
                reason = reason.Replace("favorável para", "dados insuficientes para confirmar benefício para", StringComparison.OrdinalIgnoreCase);
                reason = reason.Replace("adequado para", "adequação não confirmada para", StringComparison.OrdinalIgnoreCase);
                reason = reason.Replace("recomendado para", "recomendação não confirmada para", StringComparison.OrdinalIgnoreCase);

                classification.Reason = reason;
            }
        }

        /// <summary>
        /// Aplica modo conservador OBRIGATÓRIO quando não há dados quantitativos reais.
        /// Esta é a camada final de sanitização que elimina TODAS as afirmações otimistas.
        /// 
        /// MODO CONSERVADOR ativado quando:
        /// - analysisMode = FrontOfPackageOnly
        /// - TODOS os campos nutricionais nulos
        /// - confidenceDetails.estimatedNutritionProfile <= 0.5
        /// </summary>
        private static void ApplyConservativeModeEnforcement(
            NutritionAnalysisResponseDto response,
            VisualInterpretationResult visionResult,
            ILogger<NutritionAnalysisService> logger)
        {
            // REGRA 1: Determinar se deve entrar em modo conservador OBRIGATÓRIO
            bool isConservativeModeRequired = IsConservativeModeRequired(response, visionResult);

            if (!isConservativeModeRequired)
            {
                return; // Não precisa sanitizar
            }

            logger?.LogWarning(
                "[ConservativeMode] ENFORCEMENT ACTIVATED. Category={Category}, Mode={Mode}, Confidence={Confidence}, HasReliableData={HasReliableData}",
                response.Category ?? "unknown",
                response.AnalysisMode,
                response.ConfidenceDetails?.EstimatedNutritionProfile,
                response.HasReliableNutritionData);

            // REGRA 2: Sanitizar TODOS os campos de texto
            SanitizeAllTextFieldsAggressively(response);

            // REGRA 3: Forçar classificações para "indeterminado" se tiverem status positivo
            ForceIndeterminateClassifications(response);

            // REGRA 4: Adicionar disclaimer explícito no summary
            AddConservativeModeDisclaimer(response);
        }

        /// <summary>
        /// Determina se o modo conservador OBRIGATÓRIO deve ser ativado.
        /// </summary>
        private static bool IsConservativeModeRequired(
            NutritionAnalysisResponseDto response,
            VisualInterpretationResult visionResult)
        {
            // Critério 1: Modo FrontOfPackageOnly
            if (response.AnalysisMode != AnalysisMode.FrontOfPackageOnly)
            {
                return false;
            }

            // Critério 2: TODOS os campos nutricionais nulos
            var profile = response.EstimatedNutritionProfile;
            bool allNutritionFieldsNull = profile == null ||
                (!profile.CaloriesPer100g.HasValue &&
                 !profile.EstimatedSugarPer100g.HasValue &&
                 !profile.EstimatedProteinPer100g.HasValue &&
                 !profile.EstimatedSodiumPer100g.HasValue &&
                 !profile.EstimatedFatPer100g.HasValue &&
                 !profile.EstimatedFiberPer100g.HasValue);

            if (!allNutritionFieldsNull)
            {
                return false; // Tem pelo menos um valor nutricional
            }

            // Critério 3: Confiança baixa ou ausente
            var nutritionConfidence = response.ConfidenceDetails?.EstimatedNutritionProfile ?? 0;
            if (nutritionConfidence > 0.5)
            {
                return false; // Confiança alta
            }

            // TODOS os critérios atendidos: modo conservador OBRIGATÓRIO
            return true;
        }

        /// <summary>
        /// Sanitiza TODOS os campos de texto de forma agressiva.
        /// Remove qualquer afirmação otimista sem evidência.
        /// </summary>
        private static void SanitizeAllTextFieldsAggressively(NutritionAnalysisResponseDto response)
        {
            // Lista de frases proibidas em modo conservador
            var prohibitedPhrases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["baixo teor de açúcar"] = "teor de açúcar não confirmado",
                ["baixo teor de sódio"] = "teor de sódio não confirmado",
                ["baixo teor de gordura"] = "teor de gordura não confirmado",
                ["baixo açúcar"] = "açúcar não confirmado",
                ["baixo sódio"] = "sódio não confirmado",
                ["baixa gordura"] = "gordura não confirmada",
                ["baixas calorias"] = "calorias não confirmadas",
                ["boa pontuação"] = "pontuação estimada conservadoramente",
                ["perfil equilibrado"] = "perfil não confirmado",
                ["opção tranquila"] = "análise limitada",
                ["pode ajudar em"] = "dados insuficientes para confirmar",
                ["ajuda em"] = "dados insuficientes para confirmar",
                ["favorável para"] = "dados insuficientes para confirmar",
                ["adequado para"] = "adequação não confirmada para",
                ["recomendado para"] = "recomendação não confirmada para",
                ["opção mais tranquila"] = "análise limitada",
                ["tranquilo para"] = "dados insuficientes para confirmar segurança para",
                ["seguro para"] = "segurança não confirmada para",
                ["bom para"] = "dados insuficientes para confirmar benefício para"
            };

            // Sanitizar summary
            if (!string.IsNullOrWhiteSpace(response.Summary))
            {
                foreach (var kvp in prohibitedPhrases)
                {
                    response.Summary = response.Summary.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase);
                }
            }

            // Sanitizar score.reason
            if (response.Score?.Reason != null)
            {
                foreach (var kvp in prohibitedPhrases)
                {
                    response.Score.Reason = response.Score.Reason.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase);
                }
            }

            // Sanitizar score.scoreInterpretation
            if (response.Score?.ScoreInterpretation != null)
            {
                foreach (var kvp in prohibitedPhrases)
                {
                    response.Score.ScoreInterpretation = response.Score.ScoreInterpretation.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase);
                }
            }

            // Sanitizar explicacaoScore
            if (!string.IsNullOrWhiteSpace(response.ExplicacaoScore))
            {
                foreach (var kvp in prohibitedPhrases)
                {
                    response.ExplicacaoScore = response.ExplicacaoScore.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase);
                }
            }

            // Sanitizar pontoPrincipal
            if (!string.IsNullOrWhiteSpace(response.PontoPrincipal))
            {
                foreach (var kvp in prohibitedPhrases)
                {
                    response.PontoPrincipal = response.PontoPrincipal.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase);
                }
            }

            // Sanitizar resumoRapido
            if (response.ResumoRapido != null && response.ResumoRapido.Any())
            {
                for (int i = 0; i < response.ResumoRapido.Count; i++)
                {
                    var item = response.ResumoRapido[i];
                    foreach (var kvp in prohibitedPhrases)
                    {
                        item = item.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase);
                    }
                    response.ResumoRapido[i] = item;
                }
            }

            // Sanitizar classification reasons (novamente, para garantir)
            var classifications = new[]
            {
                response.Classification?.Diabetic,
                response.Classification?.BloodPressure,
                response.Classification?.WeightLoss,
                response.Classification?.MuscleGain
            };

            foreach (var classification in classifications)
            {
                if (classification?.Reason != null)
                {
                    foreach (var kvp in prohibitedPhrases)
                    {
                        classification.Reason = classification.Reason.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
        }

        /// <summary>
        /// Força classificações para "indeterminado" se tiverem status positivo sem evidência.
        /// </summary>
        private static void ForceIndeterminateClassifications(NutritionAnalysisResponseDto response)
        {
            if (response.Classification == null)
            {
                return;
            }

            var positiveStatuses = new[] { "adequado", "bom", "recomendado", "favoravel" };

            // Diabetic
            if (response.Classification.Diabetic != null &&
                positiveStatuses.Any(s => response.Classification.Diabetic.Status?.Contains(s, StringComparison.OrdinalIgnoreCase) == true))
            {
                response.Classification.Diabetic.Status = "indeterminado";
                response.Classification.Diabetic.Reason = "Sem tabela nutricional visível, não foi possível confirmar o teor de açúcares.";
            }

            // BloodPressure
            if (response.Classification.BloodPressure != null &&
                positiveStatuses.Any(s => response.Classification.BloodPressure.Status?.Contains(s, StringComparison.OrdinalIgnoreCase) == true))
            {
                response.Classification.BloodPressure.Status = "indeterminado";
                response.Classification.BloodPressure.Reason = "Sem tabela nutricional visível, não foi possível confirmar o teor de sódio.";
            }

            // WeightLoss
            if (response.Classification.WeightLoss != null &&
                positiveStatuses.Any(s => response.Classification.WeightLoss.Status?.Contains(s, StringComparison.OrdinalIgnoreCase) == true))
            {
                response.Classification.WeightLoss.Status = "indeterminado";
                response.Classification.WeightLoss.Reason = "Sem tabela nutricional visível, não foi possível confirmar densidade calórica e perfil nutricional.";
            }

            // MuscleGain
            if (response.Classification.MuscleGain != null &&
                positiveStatuses.Any(s => response.Classification.MuscleGain.Status?.Contains(s, StringComparison.OrdinalIgnoreCase) == true))
            {
                response.Classification.MuscleGain.Status = "indeterminado";
                response.Classification.MuscleGain.Reason = "Sem tabela nutricional visível, não foi possível confirmar o teor de proteína.";
            }
        }

        /// <summary>
        /// Adiciona disclaimer explícito no summary quando em modo conservador.
        /// </summary>
        private static void AddConservativeModeDisclaimer(NutritionAnalysisResponseDto response)
        {
            var disclaimer = "⚠️ Análise limitada: Sem tabela nutricional visível, não foi possível confirmar valores nutricionais específicos. ";

            // Adicionar no início do summary se ainda não existir
            if (!string.IsNullOrWhiteSpace(response.Summary))
            {
                if (!response.Summary.Contains("⚠️", StringComparison.Ordinal) &&
                    !response.Summary.Contains("Análise limitada", StringComparison.OrdinalIgnoreCase))
                {
                    response.Summary = disclaimer + response.Summary;
                }
            }
            else
            {
                response.Summary = disclaimer + "Para análise precisa, fotografe a tabela nutricional da embalagem.";
            }
        }

        private int ComputeHealthScore(NutritionAnalysisResponseDto response)
        {
            var nutrition = response.EstimatedNutritionProfile;

            // NOVA REGRA: Se não há dados confiáveis, limitar drasticamente o score
            if (!response.HasReliableNutritionData)
            {
                var categoryBaseScore = GetCategoryBaseScore(response.Category);
                var adjustment = GetClassificationAlignmentAdjustment(response.Classification);

                // Penalidade por inferredRisks
                var riskPenalty = response.InferredRisks.Count * 5; // -5 por risco inferido

                var unreliableScore = categoryBaseScore + adjustment - riskPenalty;

                // Limitar score máximo a 55 quando não há dados confiáveis
                unreliableScore = Math.Clamp(unreliableScore, 15, 55);

                _logger.LogInformation(
                    "[NutritionV1] Score calculated WITHOUT reliable data. BaseScore={Base}, Adjustment={Adj}, " +
                    "RiskPenalty={RiskPenalty}, FinalScore={Final}, InferredRisks={Risks}",
                    categoryBaseScore,
                    adjustment,
                    riskPenalty,
                    unreliableScore,
                    string.Join(", ", response.InferredRisks));

                return unreliableScore;
            }

            // Lógica original quando HÁ dados confiáveis
            var score = GetCategoryBaseScore(response.Category);

            if (!HasExactNutritionData(nutrition))
            {
                score += GetClassificationAlignmentAdjustment(response.Classification);
                score = ApplyIntermediateProfileScoreFloor(score, response.Classification, nutrition);
                return ApplyCategoryScoreCap(response.Category, Math.Clamp(score, 20, 85));
            }

            score -= GetSugarPenalty(nutrition?.EstimatedSugarPer100g);
            score -= GetSodiumPenalty(nutrition?.EstimatedSodiumPer100g);
            score -= GetFatPenalty(nutrition?.EstimatedFatPer100g);
            score -= GetCaloriesPenalty(nutrition?.CaloriesPer100g);
            score += GetProteinBonus(nutrition?.EstimatedProteinPer100g);
            score += GetFiberBonus(nutrition?.EstimatedFiberPer100g);
            score += GetProtectiveNutritionBonus(nutrition);
            score -= GetLowProteinPenalty(response.Category, nutrition, response.VisibleClaims, response.Classification);
            score += GetClassificationAlignmentAdjustment(response.Classification);

            score = Math.Clamp(score, 0, 100);
            score = ApplyIntermediateProfileScoreFloor(score, response.Classification, nutrition);

            if (score == 0 && !IsExtremeNutritionProfile(nutrition))
            {
                score = 8;
            }

            return ApplyCategoryScoreCap(response.Category, score);
        }

        private static int GetCategoryBaseScore(string? category)
        {
            var normalizedCategory = NormalizeCategoryKey(category);
            if (string.IsNullOrWhiteSpace(normalizedCategory))
            {
                return 55;
            }

            foreach (var key in CategoryBaseScores.Keys.OrderByDescending(k => k.Length))
            {
                if (normalizedCategory.Contains(key, StringComparison.OrdinalIgnoreCase))
                {
                    return CategoryBaseScores[key];
                }
            }

            return 55;
        }

        private static EstimatedNutritionProfileDto CloneNutritionEstimate(EstimatedNutritionProfileDto source, string? packageWeight)
        {
            var clone = new EstimatedNutritionProfileDto
            {
                CaloriesPer100g = source.CaloriesPer100g,
                EstimatedSugarPer100g = source.EstimatedSugarPer100g,
                EstimatedProteinPer100g = source.EstimatedProteinPer100g,
                EstimatedSodiumPer100g = source.EstimatedSodiumPer100g,
                EstimatedFiberPer100g = source.EstimatedFiberPer100g,
                EstimatedFatPer100g = source.EstimatedFatPer100g,
                Basis = CategoryEstimateBasis
            };

            var weightInGrams = ParsePackageWeightInGrams(packageWeight);
            if (clone.CaloriesPer100g.HasValue && weightInGrams.HasValue)
            {
                clone.EstimatedPackageCalories = Math.Round(clone.CaloriesPer100g.Value * weightInGrams.Value / 100d, 0);
            }

            return clone;
        }

        private static double? ParsePackageWeightInGrams(string? packageWeight)
        {
            if (string.IsNullOrWhiteSpace(packageWeight))
            {
                return null;
            }

            var match = System.Text.RegularExpressions.Regex.Match(packageWeight, @"(?<value>\d+(?:[\.,]\d+)?)\s*(?<unit>kg|g|ml|l)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            if (!double.TryParse(match.Groups["value"].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                return null;
            }

            var unit = match.Groups["unit"].Value.ToLowerInvariant();
            return unit switch
            {
                "kg" or "l" => value * 1000,
                _ => value
            };
        }

        private static void SanitizeEstimatedPackageCalories(NutritionAnalysisResponseDto response)
        {
            var nutrition = response.EstimatedNutritionProfile;
            if (nutrition?.EstimatedPackageCalories == null)
            {
                return;
            }

            if (!ShouldSuppressEstimatedPackageCalories(response.Category, nutrition))
            {
                return;
            }

            nutrition.EstimatedPackageCalories = null;
            nutrition.Basis = AppendBasisNote(
                nutrition.Basis,
                "Calorias totais da embalagem omitidas porque a base nutricional pode variar entre produto cru, cozido ou porção.");
        }

        private static bool ShouldSuppressEstimatedPackageCalories(string? category, EstimatedNutritionProfileDto nutrition)
        {
            var normalizedCategory = NormalizeCategoryKey(category);
            if (!IsStapleCategoryWithAmbiguousPreparation(normalizedCategory))
            {
                return false;
            }

            if (string.Equals(nutrition.Basis, CategoryEstimateBasis, StringComparison.OrdinalIgnoreCase)
                || (nutrition.Basis != null && nutrition.Basis.StartsWith(DatabaseEstimateBasis, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            return !IsPackageCaloriesBasisExplicit(nutrition.Basis);
        }

        private static bool IsStapleCategoryWithAmbiguousPreparation(string normalizedCategory)
        {
            return normalizedCategory.Contains("arroz", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("feijão", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("feijao", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("macarrão", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("macarrao", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("massa", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPackageCaloriesBasisExplicit(string? basis)
        {
            if (string.IsNullOrWhiteSpace(basis))
            {
                return false;
            }

            var normalizedBasis = basis.Trim().ToLowerInvariant();

            return normalizedBasis.Contains("100g", StringComparison.OrdinalIgnoreCase)
                || normalizedBasis.Contains("100 g", StringComparison.OrdinalIgnoreCase)
                || normalizedBasis.Contains("100ml", StringComparison.OrdinalIgnoreCase)
                || normalizedBasis.Contains("100 ml", StringComparison.OrdinalIgnoreCase)
                || normalizedBasis.Contains("produto cru", StringComparison.OrdinalIgnoreCase)
                || normalizedBasis.Contains("produto seco", StringComparison.OrdinalIgnoreCase)
                || normalizedBasis.Contains("cozido", StringComparison.OrdinalIgnoreCase)
                || normalizedBasis.Contains("embalagem inteira", StringComparison.OrdinalIgnoreCase)
                || normalizedBasis.Contains("por embalagem", StringComparison.OrdinalIgnoreCase);
        }

        private static string AppendBasisNote(string? basis, string note)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                return basis ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(basis))
            {
                return note;
            }

            if (basis.Contains(note, StringComparison.OrdinalIgnoreCase))
            {
                return basis;
            }

            return $"{basis}. {note}";
        }

        private static bool HasExactNutritionData(EstimatedNutritionProfileDto? nutrition)
        {
            if (nutrition == null)
            {
                return false;
            }

            if (string.Equals(nutrition.Basis, CategoryEstimateBasis, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (nutrition.Basis != null && nutrition.Basis.StartsWith(DatabaseEstimateBasis, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return nutrition.CaloriesPer100g.HasValue ||
                   nutrition.EstimatedSugarPer100g.HasValue ||
                   nutrition.EstimatedProteinPer100g.HasValue ||
                   nutrition.EstimatedSodiumPer100g.HasValue ||
                   nutrition.EstimatedFiberPer100g.HasValue ||
                   nutrition.EstimatedFatPer100g.HasValue;
        }

        private static string BuildHealthScoreReason(string? category, EstimatedNutritionProfileDto? nutrition, bool hasReliableNutritionData)
        {
            // NOVA REGRA: Quando não há dados confiáveis, ser MUITO conservador
            if (!hasReliableNutritionData || !HasExactNutritionData(nutrition))
            {
                var categoryName = NormalizeCategoryToProductName(category ?? "produto da categoria");
                var qualitativeOffender = DetectQualitativePrincipalOffender(category);

                var parts = new List<string>
                {
                    $"Pontuação calculada qualitativamente pelo perfil típico de {categoryName}",
                    "com baixa confiança (sem extração quantitativa da tabela nutricional)"
                };

                if (!string.IsNullOrWhiteSpace(qualitativeOffender))
                {
                    parts.Add($"principal ponto de atenção inferido pela categoria: {qualitativeOffender}");
                }

                return string.Join(", ", parts) + ".";
            }

            // Lógica original quando HÁ dados confiáveis
            var dominantConcern = BuildDominantScoreConcern(category, nutrition);
            var mainCompensation = BuildMainScoreCompensation(nutrition);

            if (string.IsNullOrWhiteSpace(dominantConcern) && string.IsNullOrWhiteSpace(mainCompensation))
            {
                return "Pontuação calculada a partir do equilíbrio geral do perfil nutricional disponível.";
            }

            if (!string.IsNullOrWhiteSpace(dominantConcern) && !string.IsNullOrWhiteSpace(mainCompensation))
            {
                return $"Pontuação limitada por {dominantConcern}, apesar de {mainCompensation}.";
            }

            if (!string.IsNullOrWhiteSpace(dominantConcern))
            {
                return $"Pontuação limitada por {dominantConcern}.";
            }

            return $"Pontuação favorecida por {mainCompensation}.";
        }

        /// <summary>
        /// Sanitiza o reason do score para evitar afirmações otimistas sem base.
        /// </summary>
        private static string SanitizeScoreReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return reason;
            }

            // Remover afirmações otimistas
            reason = reason.Replace("baixo teor de açúcar", "teor de açúcar não confirmado", StringComparison.OrdinalIgnoreCase);
            reason = reason.Replace("baixo teor de sódio", "teor de sódio não confirmado", StringComparison.OrdinalIgnoreCase);
            reason = reason.Replace("baixo teor de gordura", "teor de gordura não confirmado", StringComparison.OrdinalIgnoreCase);
            reason = reason.Replace("baixas calorias", "densidade calórica não confirmada", StringComparison.OrdinalIgnoreCase);
            reason = reason.Replace("boa pontuação", "pontuação estimada conservadoramente", StringComparison.OrdinalIgnoreCase);
            reason = reason.Replace("perfil equilibrado", "perfil não totalmente confirmado", StringComparison.OrdinalIgnoreCase);
            reason = reason.Replace("favorecida por", "estimada considerando", StringComparison.OrdinalIgnoreCase);

            return reason;
        }

        private static string GetHealthScoreLabel(int score)
        {
            if (score <= 39)
            {
                return "baixo";
            }

            if (score <= 69)
            {
                return "moderado";
            }

            return "alto";
        }

        private static NutritionScoreDto MapToLegacyScore(HealthScoreDto healthScore)
        {
            var score = healthScore.Value;

            var status = score switch
            {
                >= 82 => "excelente",
                >= 68 => "bom",
                >= 45 => "atencao",
                >= 28 => "ruim",
                _ => "evitar"
            };

            var color = score switch
            {
                >= 68 => "green",
                >= 45 => "yellow",
                >= 28 => "orange",
                _ => "red"
            };

            var label = score switch
            {
                >= 82 => "Muito saudável",
                >= 68 => "Boa escolha",
                >= 45 => "Escolha razoável",
                >= 28 => "Consumo ocasional",
                _ => "Evitar frequência alta"
            };

            return new NutritionScoreDto
            {
                Score = score,
                Status = status,
                Color = color,
                Label = label
            };
        }

        private static int GetSugarPenalty(double? sugarPer100g)
        {
            return sugarPer100g switch
            {
                >= 45 => 24,
                >= 30 => 18,
                >= 20 => 12,
                >= 10 => 6,
                _ => 0
            };
        }

        private static int GetProtectiveNutritionBonus(EstimatedNutritionProfileDto? nutrition)
        {
            if (nutrition == null)
            {
                return 0;
            }

            var bonus = 0;

            if (nutrition.CaloriesPer100g <= 120)
            {
                bonus += 3;
            }
            else if (nutrition.CaloriesPer100g <= 180)
            {
                bonus += 1;
            }

            if (nutrition.EstimatedSodiumPer100g <= 120)
            {
                bonus += 3;
            }
            else if (nutrition.EstimatedSodiumPer100g <= 200)
            {
                bonus += 1;
            }

            if (nutrition.EstimatedFatPer100g <= 3)
            {
                bonus += 2;
            }
            else if (nutrition.EstimatedFatPer100g <= 5)
            {
                bonus += 1;
            }

            return bonus;
        }

        private static int GetSodiumPenalty(double? sodiumPer100g)
        {
            return sodiumPer100g switch
            {
                >= 1200 => 18,
                >= 800 => 12,
                >= 400 => 7,
                >= 200 => 3,
                _ => 0
            };
        }

        private static int GetFatPenalty(double? fatPer100g)
        {
            return fatPer100g switch
            {
                >= 35 => 16,
                >= 20 => 11,
                >= 10 => 6,
                >= 5 => 2,
                _ => 0
            };
        }

        private static int GetCaloriesPenalty(double? caloriesPer100g)
        {
            return caloriesPer100g switch
            {
                >= 550 => 15,
                >= 450 => 10,
                >= 300 => 5,
                _ => 0
            };
        }

        private static int GetProteinBonus(double? proteinPer100g)
        {
            return proteinPer100g switch
            {
                >= 20 => 14,
                >= 12 => 9,
                >= 8 => 5,
                _ => 0
            };
        }

        private static int GetFiberBonus(double? fiberPer100g)
        {
            return fiberPer100g switch
            {
                >= 10 => 12,
                >= 6 => 8,
                >= 3 => 4,
                _ => 0
            };
        }

        private static int GetLowProteinPenalty(
            string? category,
            EstimatedNutritionProfileDto? nutrition,
            List<string>? visibleClaims,
            ProductClassificationDto? classification)
        {
            var proteinPer100g = nutrition?.EstimatedProteinPer100g;
            if (!proteinPer100g.HasValue)
            {
                return 0;
            }

            if (proteinPer100g.Value < 4 && (nutrition?.CaloriesPer100g >= 250 || nutrition?.EstimatedSugarPer100g >= 15))
            {
                return 6;
            }

            var muscleGainStatus = classification?.MuscleGain?.Status ?? string.Empty;
            var hasSevereMuscleGainClassification = muscleGainStatus.Contains("fraco", StringComparison.OrdinalIgnoreCase)
                || muscleGainStatus.Contains("nao", StringComparison.OrdinalIgnoreCase);

            if (proteinPer100g.Value < 6 &&
                (HasProteinForwardClaim(visibleClaims)
                 || IsExplicitlyProteinTargetedCategory(category)
                 || hasSevereMuscleGainClassification))
            {
                return 4;
            }

            return 0;
        }

        private static int GetClassificationAlignmentAdjustment(ProductClassificationDto? classification)
        {
            var statuses = GetNormalizedProfileStatuses(classification);
            if (statuses.Count == 0)
            {
                return 0;
            }

            var adjustment = 0;

            foreach (var status in statuses)
            {
                if (status.Contains("adequado", StringComparison.OrdinalIgnoreCase))
                {
                    adjustment += 3;
                    continue;
                }

                if (status.Contains("consumo_moderado", StringComparison.OrdinalIgnoreCase))
                {
                    adjustment += 1;
                    continue;
                }

                if (status.Contains("fraco", StringComparison.OrdinalIgnoreCase))
                {
                    adjustment -= 6;
                    continue;
                }

                if (status.Contains("nao", StringComparison.OrdinalIgnoreCase))
                {
                    adjustment -= 12;
                }
            }

            return adjustment;
        }

        private static int ApplyIntermediateProfileScoreFloor(
            int score,
            ProductClassificationDto? classification,
            EstimatedNutritionProfileDto? nutrition)
        {
            var statuses = GetNormalizedProfileStatuses(classification);
            if (statuses.Count == 0)
            {
                return score;
            }

            var adequateCount = statuses.Count(status => status.Contains("adequado", StringComparison.OrdinalIgnoreCase));
            var moderateCount = statuses.Count(status => status.Contains("consumo_moderado", StringComparison.OrdinalIgnoreCase));
            var weakCount = statuses.Count(status => status.Contains("fraco", StringComparison.OrdinalIgnoreCase));
            var hasSevereRestriction = statuses.Any(status => status.Contains("nao", StringComparison.OrdinalIgnoreCase));

            if (hasSevereRestriction || weakCount >= 2 || IsExtremeNutritionProfile(nutrition))
            {
                return score;
            }

            if (adequateCount + moderateCount >= 4)
            {
                return Math.Max(score, 58);
            }

            if (adequateCount + moderateCount >= 3 && adequateCount >= 1)
            {
                return Math.Max(score, 54);
            }

            return score;
        }

        private static List<string> GetNormalizedProfileStatuses(ProductClassificationDto? classification)
        {
            return new List<string?>
            {
                classification?.Diabetic?.Status,
                classification?.BloodPressure?.Status,
                classification?.WeightLoss?.Status,
                classification?.MuscleGain?.Status
            }
            .Where(status => !string.IsNullOrWhiteSpace(status))
            .Select(status => status!.Trim().ToLowerInvariant())
            .ToList();
        }

        private static bool IsExtremeNutritionProfile(EstimatedNutritionProfileDto? nutrition)
        {
            if (nutrition == null)
            {
                return false;
            }

            var severeMarkers = 0;

            if (nutrition.EstimatedSugarPer100g >= 45) severeMarkers++;
            if (nutrition.EstimatedFatPer100g >= 30) severeMarkers++;
            if (nutrition.EstimatedSodiumPer100g >= 1000) severeMarkers++;
            if (nutrition.CaloriesPer100g >= 550) severeMarkers++;

            return severeMarkers >= 3
                || (nutrition.EstimatedSugarPer100g >= 35
                    && nutrition.EstimatedProteinPer100g <= 2
                    && nutrition.EstimatedFiberPer100g <= 1
                    && nutrition.CaloriesPer100g >= 450);
        }

        private static void ApplyAutomaticWarnings(NutritionAnalysisResponseDto response)
        {
            // NOVA REGRA: Adicionar warning claro quando não há dados confiáveis
            if (!response.HasReliableNutritionData)
            {
                AddWarning(response.Warnings,
                    "Análise baseada apenas na categoria do produto. Para avaliação precisa dos valores nutricionais, fotografe a tabela nutricional da embalagem.");

                // Adicionar warnings específicos por risco inferido
                foreach (var risk in response.InferredRisks)
                {
                    var riskWarning = risk switch
                    {
                        "alto_acucar" => "Categoria tipicamente com alto teor de açúcar. Moderação recomendada.",
                        "alto_sodio" => "Categoria tipicamente com alto teor de sódio. Atenção ao consumo recorrente.",
                        "alta_gordura" => "Categoria tipicamente com alta gordura. Considere o tamanho da porção.",
                        "ultraprocessado" => "Produto ultraprocessado. Priorize alimentos in natura quando possível.",
                        "aditivos_quimicos" => "Possível presença de aditivos químicos detectada nos ingredientes.",
                        _ => null
                    };

                    if (!string.IsNullOrWhiteSpace(riskWarning))
                    {
                        AddWarning(response.Warnings, riskWarning);
                    }
                }

                return;
            }

            // Lógica original quando HÁ dados confiáveis
            var nutrition = response.EstimatedNutritionProfile;
            if (nutrition == null)
            {
                return;
            }

            var normalizedCategory = NormalizeCategoryKey(response.Category);
            var isSweetSnackCategory = IsSweetSnackCategory(normalizedCategory);

            ApplyCategorySpecificWarnings(response, nutrition, normalizedCategory);

            var isBeverage = IsBeverageCategory(normalizedCategory);
            var sugarWarningThreshold = isBeverage ? 7.0 : 22.0;

            if (nutrition.EstimatedSugarPer100g >= sugarWarningThreshold)
            {
                AddWarning(response.Warnings,
                    isBeverage
                        ? "Contém açúcar relevante por porção. Para uma bebida, esse teor concentra impacto glicêmico mesmo em 200ml."
                        : isSweetSnackCategory
                            ? "Açúcar estimado alto para a categoria, o que pesa no consumo frequente."
                            : "Teor estimado de açúcar elevado para consumo frequente.");
            }

            if (nutrition.CaloriesPer100g >= 450)
            {
                AddWarning(response.Warnings,
                    isSweetSnackCategory
                        ? "Alta densidade calórica: pequenas porções já concentram muitas calorias."
                        : "Alta densidade calórica estimada por 100g, favorecendo excesso energético em porções pequenas.");
            }

            if (nutrition.EstimatedFatPer100g >= 20 && isSweetSnackCategory)
            {
                AddWarning(response.Warnings, "Também há gordura elevada para a categoria, reforçando o caráter indulgente do produto.");
            }

            if (nutrition.EstimatedSodiumPer100g >= 600)
            {
                AddWarning(response.Warnings, "Alto teor estimado de sódio por 100g, exigindo moderação especialmente em consumo recorrente.");
            }

            if (ShouldWarnLowProteinForMuscleGain(response, nutrition))
            {
                AddWarning(response.Warnings, "Para foco em ganho de massa, o teor estimado de proteína é baixo para a categoria.");
            }
        }

        private static bool ShouldWarnLowProteinForMuscleGain(NutritionAnalysisResponseDto response, EstimatedNutritionProfileDto nutrition)
        {
            if (nutrition.EstimatedProteinPer100g is null or >= 6)
            {
                return false;
            }

            if (IsExplicitlyProteinTargetedCategory(response.Category) || HasProteinForwardClaim(response.VisibleClaims))
            {
                return true;
            }

            var muscleGainStatus = response.Classification?.MuscleGain?.Status ?? string.Empty;
            if (string.IsNullOrWhiteSpace(muscleGainStatus) ||
                muscleGainStatus.Equals("indeterminado", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return muscleGainStatus.Contains("fraco", StringComparison.OrdinalIgnoreCase)
                || muscleGainStatus.Contains("nao", StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyCategorySpecificWarnings(NutritionAnalysisResponseDto response, EstimatedNutritionProfileDto nutrition, string normalizedCategory)
        {
            if (IsRiceCategory(normalizedCategory))
            {
                if (nutrition.EstimatedFiberPer100g is null or < 3)
                {
                    AddWarning(response.Warnings, "Baixo teor estimado de fibras para a categoria, o que reduz saciedade na refeição.");
                }

                AddWarning(response.Warnings, "Para controle glicêmico, vale moderar a porção e combinar com feijão, legumes ou proteínas.");
                AddWarning(response.Warnings, "Em dietas de emagrecimento, a porção faz diferença no total calórico da refeição.");
            }

            if (ShouldHighlightSugarAsPrimaryCaution(nutrition, normalizedCategory))
            {
                AddWarning(response.Warnings, "O principal ponto de atenção está no açúcar, mais do que em sódio ou gordura, no perfil nutricional disponível.");
            }
        }

        private static bool ShouldHighlightSugarAsPrimaryCaution(EstimatedNutritionProfileDto nutrition, string normalizedCategory)
        {
            var isBeverage = IsBeverageCategory(normalizedCategory);
            var sugarMin = isBeverage ? 7.0 : 10.0;
            var sugarMax = isBeverage ? 18.0 : 20.0;

            return nutrition.EstimatedSugarPer100g >= sugarMin
                && nutrition.EstimatedSugarPer100g < sugarMax
                && nutrition.EstimatedSodiumPer100g is <= 180
                && nutrition.EstimatedFatPer100g is <= 5
                && nutrition.CaloriesPer100g is <= 180;
        }

        private static bool HasProteinForwardClaim(List<string>? visibleClaims)
        {
            if (visibleClaims?.Any() != true)
            {
                return false;
            }

            return visibleClaims.Any(claim =>
                claim.Contains("proteic", StringComparison.OrdinalIgnoreCase)
                || claim.Contains("protein", StringComparison.OrdinalIgnoreCase)
                || claim.Contains("alto teor de proteína", StringComparison.OrdinalIgnoreCase)
                || claim.Contains("fonte de proteína", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsProteinRelevantCategory(string? category)
        {
            var normalizedCategory = NormalizeCategoryKey(category);

            return normalizedCategory.Contains("prote")
                || normalizedCategory.Contains("whey")
                || normalizedCategory.Contains("iogurte")
                || normalizedCategory.Contains("barra")
                || normalizedCategory.Contains("queijo")
                || normalizedCategory.Contains("suplemento");
        }

        private static bool IsExplicitlyProteinTargetedCategory(string? category)
        {
            var normalizedCategory = NormalizeCategoryKey(category);

            return normalizedCategory.Contains("proteic", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("protein", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("whey", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("barra proteica", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("suplemento", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildFinalSummary(NutritionAnalysisResponseDto response)
        {
            // NOVA REGRA: Quando não há dados confiáveis, deixar MUITO explícito
            if (!response.HasReliableNutritionData)
            {
                var unreliableProductName = GetDisplayProductName(response);
                var categoryDescription = !string.IsNullOrWhiteSpace(response.Category)
                    ? $"da categoria {response.Category}"
                    : "alimentício";

                var risksDescription = response.InferredRisks.Any()
                    ? $" com possíveis pontos de atenção: {FormatInferredRisks(response.InferredRisks)}"
                    : "";

                return $"Análise baseada apenas na categoria, sem dados nutricionais exatos. " +
                       $"{unreliableProductName} é um produto {categoryDescription}{risksDescription}. " +
                       $"Para análise precisa, fotografe a tabela nutricional da embalagem.";
            }

            // Lógica original quando HÁ dados confiáveis
            if (ShouldPreserveExistingSummary(response.Summary))
            {
                return ApplyLowNutritionConfidenceSummaryPrefix(response, EnsureSentenceTermination(response.Summary!));
            }

            var productName = GetDisplayProductName(response);

            if (TryBuildRiceSummary(response, productName, out var riceSummary))
            {
                return riceSummary;
            }

            var qualityDescription = response.Score?.Label switch
            {
                "alto" => "tem um perfil nutricional mais equilibrado",
                "moderado" => "tem um perfil nutricional intermediário",
                _ => "pede mais moderação no consumo frequente"
            };

            var nutritionContext = BuildSummaryNutritionContext(response.EstimatedNutritionProfile, response.Category);
            var analysisContext = response.AnalysisMode == AnalysisMode.FullNutritionLabel
                ? "Resumo baseado na tabela nutricional da embalagem."
                : "Resumo estimado a partir da categoria e da frente da embalagem.";

            var summary = $"{productName} {qualityDescription}{nutritionContext}. {analysisContext}";
            return ApplyLowNutritionConfidenceSummaryPrefix(response, summary);
        }

        /// <summary>
        /// Formata os riscos inferidos para exibição amigável ao usuário
        /// </summary>
        private static string FormatInferredRisks(List<string> inferredRisks)
        {
            var riskDescriptions = inferredRisks.Select(risk => risk switch
            {
                "alto_acucar" => "alto teor de açúcar",
                "alto_sodio" => "alto teor de sódio",
                "alta_gordura" => "alta gordura",
                "ultraprocessado" => "produto ultraprocessado",
                "aditivos_quimicos" => "presença de aditivos químicos",
                _ => risk.Replace("_", " ")
            }).ToList();

            if (riskDescriptions.Count == 1)
            {
                return riskDescriptions[0];
            }

            if (riskDescriptions.Count == 2)
            {
                return $"{riskDescriptions[0]} e {riskDescriptions[1]}";
            }

            var allButLast = string.Join(", ", riskDescriptions.Take(riskDescriptions.Count - 1));
            return $"{allButLast} e {riskDescriptions.Last()}";
        }

        private static string ApplyLowNutritionConfidenceSummaryPrefix(NutritionAnalysisResponseDto response, string summary)
        {
            if (string.IsNullOrWhiteSpace(summary))
            {
                return LowNutritionConfidenceSummaryPrefix;
            }

            var estimatedNutritionConfidence = response.ConfidenceDetails?.EstimatedNutritionProfile;
            if (estimatedNutritionConfidence is null or >= 0.3)
            {
                return summary;
            }

            if (summary.StartsWith(LowNutritionConfidenceSummaryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return summary;
            }

            return $"{LowNutritionConfidenceSummaryPrefix} {summary}";
        }

        private static bool TryBuildRiceSummary(NutritionAnalysisResponseDto response, string productName, out string summary)
        {
            summary = string.Empty;

            if (!IsRiceCategory(NormalizeCategoryKey(response.Category)))
            {
                return false;
            }

            var nutrition = response.EstimatedNutritionProfile;
            var profileText = response.Score?.Label switch
            {
                "alto" => "tem um perfil mais equilibrado dentro da categoria",
                "moderado" => "tem um perfil nutricional intermediário e funciona como uma base neutra da refeição",
                _ => "é uma base alimentar simples, mas pede mais atenção à composição da refeição"
            };

            var detailParts = new List<string>();

            if (nutrition?.EstimatedSodiumPer100g is <= 120)
            {
                detailParts.Add("costuma ter baixo teor de sódio");
            }

            if (nutrition?.EstimatedFiberPer100g is null or < 3)
            {
                detailParts.Add("oferece pouco teor de fibras");
            }

            var guidance = ShouldHighlightPortionGuidance(response)
                ? "Para controle glicêmico ou emagrecimento, vale moderar a porção e combinar com feijão, legumes ou proteínas."
                : "Combinar com leguminosas, vegetais e proteínas ajuda a deixar a refeição mais completa.";

            var analysisContext = response.AnalysisMode == AnalysisMode.FullNutritionLabel
                ? "Resumo baseado na tabela nutricional da embalagem."
                : "Resumo estimado a partir da categoria e da frente da embalagem.";

            summary = detailParts.Count > 0
                ? $"{productName} {profileText}. Em geral, {JoinSignals(detailParts)}. {guidance} {analysisContext}"
                : $"{productName} {profileText}. {guidance} {analysisContext}";

            return true;
        }

        private static bool ShouldHighlightPortionGuidance(NutritionAnalysisResponseDto response)
        {
            var diabeticStatus = response.Classification?.Diabetic?.Status ?? string.Empty;
            var weightLossStatus = response.Classification?.WeightLoss?.Status ?? string.Empty;

            return diabeticStatus.Contains("moderado", StringComparison.OrdinalIgnoreCase)
                || diabeticStatus.Contains("nao", StringComparison.OrdinalIgnoreCase)
                || weightLossStatus.Contains("moderado", StringComparison.OrdinalIgnoreCase)
                || weightLossStatus.Contains("nao", StringComparison.OrdinalIgnoreCase)
                || IsRiceCategory(NormalizeCategoryKey(response.Category));
        }

        private static string BuildSummaryNutritionContext(EstimatedNutritionProfileDto? nutrition, string? category = null)
        {
            if (nutrition == null)
            {
                return string.Empty;
            }

            var normalizedCategory = NormalizeCategoryKey(category);
            var negatives = BuildNegativeNutritionSignals(nutrition, normalizedCategory);
            var positives = BuildPositiveNutritionSignals(nutrition, normalizedCategory);

            if (negatives.Count > 0 && positives.Count > 0)
            {
                return $", principalmente por {JoinSignals(negatives)} e com algum equilíbrio de {JoinSignals(positives)}";
            }

            if (negatives.Count > 0)
            {
                return $", principalmente por {JoinSignals(negatives)}";
            }

            if (positives.Count > 0)
            {
                return $", com destaque para {JoinSignals(positives)}";
            }

            return ", sem marcadores nutricionais dominantes na leitura disponível";
        }

        private static List<string> BuildNegativeNutritionSignals(EstimatedNutritionProfileDto? nutrition, string? normalizedCategory = null)
        {
            var signals = new List<string>();
            if (nutrition == null)
            {
                return signals;
            }

            var isBeverage = IsBeverageCategory(normalizedCategory ?? string.Empty);

            // Para bebidas: 7g/100ml é relevante; para sólidos: 10g/100g
            var sugarModerateThreshold = isBeverage ? 7.0 : 10.0;
            var sugarHighThreshold = isBeverage ? 12.0 : 20.0;

            if (nutrition.EstimatedSugarPer100g >= sugarHighThreshold) signals.Add("açúcar elevado");
            else if (nutrition.EstimatedSugarPer100g >= sugarModerateThreshold)
            {
                signals.Add(isBeverage ? "açúcar relevante por porção" : "açúcar moderado");
            }

            if (nutrition.EstimatedFatPer100g >= 20) signals.Add("gordura elevada");
            if (nutrition.EstimatedSodiumPer100g >= 600) signals.Add("sódio elevado");
            if (nutrition.CaloriesPer100g >= 450) signals.Add("alta densidade calórica");

            return signals;
        }

        private static void RefineWarningsForMobile(NutritionAnalysisResponseDto response)
        {
            if (response.Warnings == null || response.Warnings.Count == 0)
            {
                return;
            }

            response.Warnings = response.Warnings
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(w => GetMobileWarningPriority(response, w))
                .ThenBy(w => w.Length)
                .Take(response.AnalysisMode == AnalysisMode.FrontOfPackageOnly ? 4 : 5)
                .ToList();
        }

        private static int GetMobileWarningPriority(NutritionAnalysisResponseDto response, string warning)
        {
            if (string.IsNullOrWhiteSpace(warning))
            {
                return int.MaxValue;
            }

            if (warning.Contains("faixa esperada", StringComparison.OrdinalIgnoreCase)
                || warning.Contains("média segura", StringComparison.OrdinalIgnoreCase)
                || warning.Contains("evidência textual detectada", StringComparison.OrdinalIgnoreCase)
                || warning.Contains("unidade explicitamente detectada", StringComparison.OrdinalIgnoreCase))
            {
                return -1;
            }

            var normalizedCategory = NormalizeCategoryKey(response.Category);

            if (IsRiceCategory(normalizedCategory))
            {
                if (warning.Contains("fibr", StringComparison.OrdinalIgnoreCase)) return 0;
                if (warning.Contains("glic", StringComparison.OrdinalIgnoreCase)) return 1;
                if (warning.Contains("emagrec", StringComparison.OrdinalIgnoreCase)
                    || warning.Contains("porção", StringComparison.OrdinalIgnoreCase)
                    || warning.Contains("porcao", StringComparison.OrdinalIgnoreCase)) return 2;
                if (warning.Contains("frente da embalagem", StringComparison.OrdinalIgnoreCase)
                    || warning.Contains("categoria", StringComparison.OrdinalIgnoreCase)) return 3;
                if (warning.Contains("sódio", StringComparison.OrdinalIgnoreCase)) return 4;
                if (warning.Contains("proteína", StringComparison.OrdinalIgnoreCase)
                    || warning.Contains("ganho de massa", StringComparison.OrdinalIgnoreCase)
                    || warning.Contains("massa", StringComparison.OrdinalIgnoreCase)) return 9;

                return 5;
            }

            if (warning.Contains("frente da embalagem", StringComparison.OrdinalIgnoreCase)
                || warning.Contains("categoria", StringComparison.OrdinalIgnoreCase)) return 0;
            if (warning.Contains("açúcar", StringComparison.OrdinalIgnoreCase)) return 1;
            if (warning.Contains("calórica", StringComparison.OrdinalIgnoreCase)
                || warning.Contains("calorias", StringComparison.OrdinalIgnoreCase)) return 2;
            if (warning.Contains("gordura", StringComparison.OrdinalIgnoreCase)) return 3;
            if (warning.Contains("sódio", StringComparison.OrdinalIgnoreCase)) return 4;
            if (warning.Contains("proteína", StringComparison.OrdinalIgnoreCase)
                || warning.Contains("ganho de massa", StringComparison.OrdinalIgnoreCase)
                || warning.Contains("massa", StringComparison.OrdinalIgnoreCase)) return 6;

            return 5;
        }

        private static string? BuildDominantScoreConcern(string? category, EstimatedNutritionProfileDto? nutrition)
        {
            if (nutrition == null)
            {
                return null;
            }

            var normalizedCategory = NormalizeCategoryKey(category);

            if (IsSweetSnackCategory(normalizedCategory) &&
                nutrition.EstimatedSugarPer100g >= 20 &&
                nutrition.CaloriesPer100g >= 450)
            {
                return "concentrar muito açúcar e calorias em pequenas porções";
            }

            if (nutrition.EstimatedSodiumPer100g >= 600 && nutrition.EstimatedFatPer100g >= 20)
            {
                return "combinar sódio e gordura em níveis elevados";
            }

            if (nutrition.EstimatedSugarPer100g >= 20)
            {
                return "excesso de açúcar para consumo frequente";
            }

            if (nutrition.CaloriesPer100g >= 450)
            {
                return "alta concentração de calorias por 100g";
            }

            if (nutrition.EstimatedSodiumPer100g >= 600)
            {
                return "sódio elevado para consumo recorrente";
            }

            if (nutrition.EstimatedFatPer100g >= 20)
            {
                return "gordura elevada para a categoria";
            }

            return null;
        }

        private static string? BuildMainScoreCompensation(EstimatedNutritionProfileDto? nutrition)
        {
            if (nutrition == null)
            {
                return null;
            }

            if (nutrition.EstimatedProteinPer100g >= 12 && nutrition.EstimatedFiberPer100g >= 6)
            {
                return "boa presença de proteína e fibras";
            }

            if (nutrition.EstimatedProteinPer100g >= 12)
            {
                return "boa presença de proteína";
            }

            if (nutrition.EstimatedFiberPer100g >= 6)
            {
                return "boa presença de fibras";
            }

            if (nutrition.EstimatedSugarPer100g <= 5)
            {
                return "baixo teor de açúcar";
            }

            return null;
        }

        private static List<string> BuildPositiveNutritionSignals(EstimatedNutritionProfileDto? nutrition, string? normalizedCategory = null)
        {
            var signals = new List<string>();
            if (nutrition == null)
            {
                return signals;
            }

            var isBeverage = IsBeverageCategory(normalizedCategory ?? string.Empty);

            if (nutrition.EstimatedProteinPer100g >= 12) signals.Add("bom teor de proteína");
            if (nutrition.EstimatedFiberPer100g >= 6) signals.Add("bom teor de fibras");
            if (nutrition.EstimatedSugarPer100g <= 5) signals.Add("baixo teor de açúcar");

            // Para bebidas, baixo sódio e baixa gordura são o esperado — não constituem diferencial positivo
            if (!isBeverage && nutrition.EstimatedSodiumPer100g <= 120) signals.Add("baixo teor de sódio");
            if (!isBeverage && nutrition.EstimatedFatPer100g <= 3) signals.Add("baixo teor de gordura");
            if (!isBeverage && nutrition.CaloriesPer100g <= 120) signals.Add("baixa densidade calórica");

            return signals;
        }

        private static bool ShouldPreserveExistingSummary(string? summary)
        {
            if (string.IsNullOrWhiteSpace(summary))
            {
                return false;
            }

            var normalizedSummary = summary.Trim();

            if (normalizedSummary.Contains("analisado com", StringComparison.OrdinalIgnoreCase)
                || normalizedSummary.Contains("sem marcadores nutricionais dominantes", StringComparison.OrdinalIgnoreCase)
                || normalizedSummary.Contains("resumo baseado", StringComparison.OrdinalIgnoreCase)
                || normalizedSummary.Contains("resumo estimado", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Preservar summaries da Azure AI que descrevem limitações honestamente
            if (normalizedSummary.Contains("frente da embalagem", StringComparison.OrdinalIgnoreCase)
                || normalizedSummary.Contains("tabela nutricional não", StringComparison.OrdinalIgnoreCase)
                || normalizedSummary.Contains("não foi possível", StringComparison.OrdinalIgnoreCase)
                || normalizedSummary.Contains("não visível", StringComparison.OrdinalIgnoreCase)
                || normalizedSummary.Contains("não legível", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return CountNutritionSpecificSignals(normalizedSummary) >= 2
                || System.Text.RegularExpressions.Regex.IsMatch(normalizedSummary, @"\d");
        }

        private static int CountNutritionSpecificSignals(string summary)
        {
            var signalCount = 0;

            if (summary.Contains("açúcar", StringComparison.OrdinalIgnoreCase)
                || summary.Contains("acucar", StringComparison.OrdinalIgnoreCase)) signalCount++;
            if (summary.Contains("sódio", StringComparison.OrdinalIgnoreCase)
                || summary.Contains("sodio", StringComparison.OrdinalIgnoreCase)) signalCount++;
            if (summary.Contains("gordura", StringComparison.OrdinalIgnoreCase)) signalCount++;
            if (summary.Contains("proteína", StringComparison.OrdinalIgnoreCase)
                || summary.Contains("proteina", StringComparison.OrdinalIgnoreCase)) signalCount++;
            if (summary.Contains("fibra", StringComparison.OrdinalIgnoreCase)) signalCount++;
            if (summary.Contains("caloria", StringComparison.OrdinalIgnoreCase)) signalCount++;
            if (summary.Contains("adequado", StringComparison.OrdinalIgnoreCase)
                || summary.Contains("consumo moderado", StringComparison.OrdinalIgnoreCase)
                || summary.Contains("moderado", StringComparison.OrdinalIgnoreCase)) signalCount++;

            return signalCount;
        }

        private static string EnsureSentenceTermination(string summary)
        {
            var trimmed = summary.Trim();
            if (trimmed.EndsWith(".", StringComparison.Ordinal)
                || trimmed.EndsWith("!", StringComparison.Ordinal)
                || trimmed.EndsWith("?", StringComparison.Ordinal))
            {
                return trimmed;
            }

            return $"{trimmed}.";
        }

        private static int ApplyCategoryScoreCap(string? category, int score)
        {
            var normalizedCategory = NormalizeCategoryKey(category);

            foreach (var key in CategoryScoreCaps.Keys.OrderByDescending(k => k.Length))
            {
                if (normalizedCategory.Contains(key, StringComparison.OrdinalIgnoreCase))
                {
                    return Math.Min(score, CategoryScoreCaps[key]);
                }
            }

            return score;
        }

        private static string NormalizeCategoryKey(string? category)
        {
            return category?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        private static bool IsSweetSnackCategory(string normalizedCategory)
        {
            return normalizedCategory.Contains("biscoito", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("bolacha", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("chocolate", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("achocolatado", StringComparison.OrdinalIgnoreCase);
        }

        private static string JoinSignals(List<string> signals)
        {
            var relevantSignals = signals
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Take(2)
                .ToList();

            return relevantSignals.Count switch
            {
                0 => string.Empty,
                1 => relevantSignals[0],
                _ => string.Join(" e ", relevantSignals)
            };
        }

        private static void AddMatchingWarning(List<string> prioritizedWarnings, List<string> sourceWarnings, params string[] keywords)
        {
            var match = sourceWarnings.FirstOrDefault(w => keywords.Any(k => w.Contains(k, StringComparison.OrdinalIgnoreCase)));
            if (!string.IsNullOrWhiteSpace(match))
            {
                AddWarning(prioritizedWarnings, match);
            }
        }

        private static void AddWarning(List<string> warnings, string warning)
        {
            if (warnings.Any(w => string.Equals(w, warning, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            warnings.Add(warning);
        }

        /// <summary>
        /// Verifica se a resposta contém dados úteis para análise nutricional.
        /// A resposta é considerada utilizável se houver pelo menos um dos seguintes:
        /// - Category preenchida
        /// - EstimatedNutritionProfile com algum valor numérico não-null
        /// - Classification com pelo menos um status diferente de "indeterminado"
        /// </summary>
        private static bool HasUsableAnalysis(NutritionAnalysisResponseDto response)
        {
            if (response == null)
            {
                return false;
            }

            // Verifica se tem category útil
            bool hasUsableCategory = HasUsableCategory(response.Category);

            // Verifica se tem nutrition profile útil
            bool hasUsableNutrition = HasUsableNutrition(response.EstimatedNutritionProfile);

            // Verifica se tem classification útil
            bool hasUsableClassification = HasUsableClassification(response.Classification);

            return hasUsableCategory || hasUsableNutrition || hasUsableClassification;
        }

        /// <summary>
        /// Verifica se a category está preenchida e é útil.
        /// </summary>
        private static bool HasUsableCategory(string? category)
        {
            return !string.IsNullOrWhiteSpace(category);
        }

        /// <summary>
        /// Verifica se o perfil nutricional contém pelo menos um valor numérico não-null.
        /// </summary>
        private static bool HasUsableNutrition(EstimatedNutritionProfileDto? nutritionProfile)
        {
            if (nutritionProfile == null)
            {
                return false;
            }

            return nutritionProfile.CaloriesPer100g.HasValue ||
                   nutritionProfile.EstimatedPackageCalories.HasValue ||
                   nutritionProfile.EstimatedSugarPer100g.HasValue ||
                   nutritionProfile.EstimatedProteinPer100g.HasValue ||
                   nutritionProfile.EstimatedSodiumPer100g.HasValue ||
                   nutritionProfile.EstimatedFiberPer100g.HasValue ||
                   nutritionProfile.EstimatedFatPer100g.HasValue;
        }

        /// <summary>
        /// Verifica se a classificação contém pelo menos um perfil com status diferente de "indeterminado".
        /// </summary>
        private static bool HasUsableClassification(ProductClassificationDto? classification)
        {
            if (classification == null)
            {
                return false;
            }

            bool diabeticUseful = classification.Diabetic?.Status != null &&
                                 !classification.Diabetic.Status.Equals("indeterminado", StringComparison.OrdinalIgnoreCase);

            bool bloodPressureUseful = classification.BloodPressure?.Status != null &&
                                      !classification.BloodPressure.Status.Equals("indeterminado", StringComparison.OrdinalIgnoreCase);

            bool weightLossUseful = classification.WeightLoss?.Status != null &&
                                   !classification.WeightLoss.Status.Equals("indeterminado", StringComparison.OrdinalIgnoreCase);

            bool muscleGainUseful = classification.MuscleGain?.Status != null &&
                                   !classification.MuscleGain.Status.Equals("indeterminado", StringComparison.OrdinalIgnoreCase);

            return diabeticUseful || bloodPressureUseful || weightLossUseful || muscleGainUseful;
        }

        /// <summary>
        /// Aplica fallback para productName quando está vazio mas category está disponível.
        /// </summary>
        private static void ApplyProductNameFallback(NutritionAnalysisResponseDto response)
        {
            if (response == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(response.Category))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(response.ProductName) || ShouldUseCategoryAsProductName(response.ProductName, response.Category))
            {
                response.ProductName = BuildCategoryAwareProductName(response.Category, response.VisibleClaims);
            }
        }

        /// <summary>
        /// Determina se os dados nutricionais são confiáveis e define hasReliableNutritionData e fallbackType.
        /// Regras:
        /// 1. NÃO gerar valores numéricos quando:
        ///    - analysisMode = FrontOfPackageOnly
        ///    - OU estimatedNutritionProfile vier nulo da IA
        ///    - OU confidenceDetails.estimatedNutritionProfile &lt; 0.6
        /// 2. Inferir riscos com base na categoria e ingredientes quando não houver dados confiáveis
        /// </summary>
        private static void DetermineNutritionDataReliability(
            NutritionAnalysisResponseDto response,
            VisualInterpretationResult visionResult)
        {
            if (response == null)
            {
                response.HasReliableNutritionData = false;
                response.FallbackType = "unknown";
                return;
            }

            var nutritionConfidence = response.ConfidenceDetails?.EstimatedNutritionProfile ?? 0;
            var hasProbableNutritionTable = visionResult.ProbableCaptureType == CaptureType.NutritionTable;
            var hasNutritionProfileFromAI = visionResult.EstimatedNutritionProfile != null;

            // Regra 1: Dados confiáveis apenas se:
            // - Modo FullNutritionLabel (tabela detectada)
            // - E confiança >= 0.6
            // - E perfil nutricional veio da IA
            if (response.AnalysisMode == AnalysisMode.FullNutritionLabel &&
                nutritionConfidence >= 0.6 &&
                hasNutritionProfileFromAI &&
                hasProbableNutritionTable)
            {
                response.HasReliableNutritionData = true;
                response.FallbackType = "real";

                // Verifica se há valores parciais (alguns null)
                var profile = response.EstimatedNutritionProfile;
                if (profile != null)
                {
                    var hasAnyNull = !profile.CaloriesPer100g.HasValue ||
                                    !profile.EstimatedSugarPer100g.HasValue ||
                                    !profile.EstimatedProteinPer100g.HasValue ||
                                    !profile.EstimatedSodiumPer100g.HasValue ||
                                    !profile.EstimatedFatPer100g.HasValue;

                    if (hasAnyNull)
                    {
                        response.FallbackType = "partial";
                    }
                }
            }
            else
            {
                // Não há dados confiáveis - será baseado em categoria
                response.HasReliableNutritionData = false;
                response.FallbackType = response.AnalysisMode == AnalysisMode.FrontOfPackageOnly 
                    ? "category_based" 
                    : "unknown";
            }

            // Regra 2: Inferir riscos qualitativos quando não há dados confiáveis
            if (!response.HasReliableNutritionData)
            {
                response.InferredRisks = InferNutritionalRisks(
                    response.Category,
                    visionResult.VisibleClaims,
                    visionResult.Classification);
            }
        }

        /// <summary>
        /// Infere riscos nutricionais com base na categoria, ingredientes e claims visíveis.
        /// Retorna lista de riscos como: "alto_sodio", "alto_acucar", "ultraprocessado", etc.
        /// </summary>
        private static List<string> InferNutritionalRisks(
            string? category,
            List<string>? visibleClaims,
            ProductClassificationDto? classification)
        {
            var risks = new List<string>();
            var normalizedCategory = NormalizeCategoryKey(category);
            var claims = (visibleClaims ?? new List<string>())
                .Select(c => c.ToLowerInvariant())
                .ToList();

            // Riscos por categoria
            if (normalizedCategory.Contains("refrigerante") ||
                normalizedCategory.Contains("achocolatado") ||
                normalizedCategory.Contains("biscoito recheado") ||
                normalizedCategory.Contains("chocolate") ||
                normalizedCategory.Contains("sobremesa"))
            {
                risks.Add("alto_acucar");
            }

            if (normalizedCategory.Contains("salgadinho") ||
                normalizedCategory.Contains("embutido") ||
                normalizedCategory.Contains("queijo ralado") ||
                normalizedCategory.Contains("tempero pronto") ||
                normalizedCategory.Contains("macarrão instantâneo") ||
                normalizedCategory.Contains("miojo"))
            {
                risks.Add("alto_sodio");
            }

            if (normalizedCategory.Contains("biscoito") ||
                normalizedCategory.Contains("bolacha") ||
                normalizedCategory.Contains("chocolate") ||
                normalizedCategory.Contains("salgadinho") ||
                normalizedCategory.Contains("fritura"))
            {
                risks.Add("alta_gordura");
            }

            // Detecta ultraprocessamento por categoria
            if (normalizedCategory.Contains("refrigerante") ||
                normalizedCategory.Contains("salgadinho") ||
                normalizedCategory.Contains("biscoito recheado") ||
                normalizedCategory.Contains("embutido") ||
                normalizedCategory.Contains("macarrão instantâneo") ||
                normalizedCategory.Contains("achocolatado em pó"))
            {
                risks.Add("ultraprocessado");
            }

            // Detecta aditivos problemáticos em claims/ingredientes
            if (claims.Any(c => c.Contains("glutamato") ||
                               c.Contains("corante") ||
                               c.Contains("aromatizante") ||
                               c.Contains("realçador de sabor")))
            {
                risks.Add("aditivos_quimicos");
            }

            // Usa classificação quando disponível
            if (classification?.Diabetic?.Status != null &&
                (classification.Diabetic.Status.Contains("nao", StringComparison.OrdinalIgnoreCase) ||
                 classification.Diabetic.Status.Contains("não", StringComparison.OrdinalIgnoreCase)))
            {
                if (!risks.Contains("alto_acucar"))
                {
                    risks.Add("alto_acucar");
                }
            }

            if (classification?.BloodPressure?.Status != null &&
                (classification.BloodPressure.Status.Contains("nao", StringComparison.OrdinalIgnoreCase) ||
                 classification.BloodPressure.Status.Contains("não", StringComparison.OrdinalIgnoreCase)))
            {
                if (!risks.Contains("alto_sodio"))
                {
                    risks.Add("alto_sodio");
                }
            }

            return risks.Distinct().ToList();
        }

        private static string GetDisplayProductName(NutritionAnalysisResponseDto response)
        {
            if (string.IsNullOrWhiteSpace(response.ProductName) ||
                ShouldUseCategoryAsProductName(response.ProductName, response.Category))
            {
                var fallback = BuildCategoryAwareProductName(response.Category, response.VisibleClaims);
                response.ProductName = fallback;
                return fallback;
            }

            return response.ProductName.Trim();
        }

        private static bool ShouldUseCategoryAsProductName(string? productName, string? category)
        {
            if (string.IsNullOrWhiteSpace(productName))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(category))
            {
                return false;
            }

            var normalizedProductName = NormalizeCategoryKey(productName);
            var normalizedCategory = NormalizeCategoryKey(category);

            if (normalizedProductName == normalizedCategory)
            {
                return false;
            }

            return normalizedProductName is "biscoito" or "bolacha" or "arroz" or "pão" or "queijo" or "iogurte" or "achocolatado" or "cereal"
                || (!normalizedProductName.Contains(' ') && normalizedCategory.Contains(normalizedProductName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Normaliza uma category para ser usada como productName.
        /// Aplica capitalização adequada.
        /// </summary>
        private static string NormalizeCategoryToProductName(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return "Produto alimentício";
            }

            var trimmed = category.Trim();

            // Capitaliza a primeira letra e mantém o resto em lowercase
            var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var capitalizedWords = words.Select(word =>
            {
                if (word.Length == 0) return word;
                return char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant();
            });

            return string.Join(" ", capitalizedWords);
        }

        private static string BuildCategoryAwareProductName(string? category, List<string>? visibleClaims)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return "Produto alimentício";
            }

            var normalizedCategory = NormalizeCategoryKey(category);
            var claims = visibleClaims ?? new List<string>();

            if (normalizedCategory.Contains("arroz", StringComparison.OrdinalIgnoreCase))
            {
                var riceName = claims.Any(c => c.Contains("integral", StringComparison.OrdinalIgnoreCase)) ||
                               normalizedCategory.Contains("integral", StringComparison.OrdinalIgnoreCase)
                    ? "Arroz Integral"
                    : claims.Any(c => c.Contains("parboilizado", StringComparison.OrdinalIgnoreCase)) ||
                      normalizedCategory.Contains("parboilizado", StringComparison.OrdinalIgnoreCase)
                        ? "Arroz Parboilizado"
                        : "Arroz Branco";

                if (claims.Any(c => c.Contains("tipo 1", StringComparison.OrdinalIgnoreCase)) ||
                    normalizedCategory.Contains("tipo 1", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{riceName} Tipo 1";
                }

                if (claims.Any(c => c.Contains("tipo 2", StringComparison.OrdinalIgnoreCase)) ||
                    normalizedCategory.Contains("tipo 2", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{riceName} Tipo 2";
                }

                return riceName;
            }

            if (normalizedCategory.Contains("feijão", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("feijao", StringComparison.OrdinalIgnoreCase))
            {
                return "Feijão";
            }

            if (normalizedCategory.Contains("macarrão", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("macarrao", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("massa", StringComparison.OrdinalIgnoreCase))
            {
                if (claims.Any(c => c.Contains("espaguete", StringComparison.OrdinalIgnoreCase)))
                {
                    return "Macarrão Espaguete";
                }

                if (claims.Any(c => c.Contains("penne", StringComparison.OrdinalIgnoreCase)))
                {
                    return "Macarrão Penne";
                }

                return "Macarrão";
            }

            return NormalizeCategoryToProductName(category);
        }

        private static bool IsRiceCategory(string normalizedCategory)
        {
            return normalizedCategory.Contains("arroz", StringComparison.OrdinalIgnoreCase);
        }

        #region Qualitative Alerts and Coherence

        /// <summary>
        /// Detecta o principal ofensor nutricional por categoria, mesmo sem macros quantitativos.
        /// Baseado em conhecimento nutricional genérico por tipologia alimentar.
        /// </summary>
        private static string? DetectQualitativePrincipalOffender(string? category)
        {
            var normalizedCategory = NormalizeCategoryKey(category);

            if (normalizedCategory.Contains("achocolatado")) return "açúcar";
            if (normalizedCategory.Contains("sobremesa")) return "açúcar";
            if (normalizedCategory.Contains("biscoito recheado")) return "açúcar e gordura";
            if (normalizedCategory.Contains("biscoito") || normalizedCategory.Contains("bolacha")) return "açúcar e gordura";
            if (normalizedCategory.Contains("refrigerante")) return "açúcar";
            if (normalizedCategory.Contains("chocolate")) return "açúcar e gordura";
            if (normalizedCategory.Contains("queijo ralado")) return "sódio e gordura";
            if (normalizedCategory.Contains("queijo") || normalizedCategory.Contains("requeijão") || normalizedCategory.Contains("cream cheese")) return "gordura e sódio";
            if (normalizedCategory.Contains("embutido") || normalizedCategory.Contains("salsicha") || normalizedCategory.Contains("linguiça") || normalizedCategory.Contains("linguica")) return "sódio e gordura";
            if (normalizedCategory.Contains("macarrão instantâneo") || normalizedCategory.Contains("miojo")) return "sódio";
            if (normalizedCategory.Contains("salgadinho")) return "sódio e gordura";

            return null;
        }

        /// <summary>
        /// Adiciona alertas qualitativos: principal ofensor por categoria e indicação de confiança do score.
        /// Funciona mesmo sem macros quantitativos, enriquecendo o retorno da API.
        /// </summary>
        private static void ApplyQualitativeAlerts(NutritionAnalysisResponseDto response)
        {
            response.Alerts ??= new List<string>();

            var principalOffender = DetectQualitativePrincipalOffender(response.Category);
            if (!string.IsNullOrWhiteSpace(principalOffender))
            {
                response.PrincipalOffender = principalOffender;
                AddAlert(response.Alerts, $"Principal ponto de atenção na categoria: {principalOffender}.");
            }

            var hasQuantitativeMacros = HasExactNutritionData(response.EstimatedNutritionProfile);
            if (!hasQuantitativeMacros && response.Score != null && response.Score.Value > 0)
            {
                AddAlert(response.Alerts, "Pontuação calculada com baixa confiança, baseada na categoria e painel frontal.");
            }
        }

        private static void AddAlert(List<string> alerts, string alert)
        {
            if (string.IsNullOrWhiteSpace(alert))
            {
                return;
            }

            if (alerts.Any(a => string.Equals(a, alert, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            alerts.Add(alert);
        }

        /// <summary>
        /// Garante coerência final entre analysisMode, estimatedNutritionProfile, basis, summary, alerts e score.
        /// Corrige contradições que possam ter surgido ao longo do pipeline de pós-processamento.
        /// </summary>
        private static void EnforceResponseCoherence(NutritionAnalysisResponseDto response)
        {
            var profile = response.EstimatedNutritionProfile;
            var hasQuantitativeMacros = profile != null
                && (profile.CaloriesPer100g.HasValue
                    || profile.EstimatedSugarPer100g.HasValue
                    || profile.EstimatedProteinPer100g.HasValue
                    || profile.EstimatedSodiumPer100g.HasValue
                    || profile.EstimatedFatPer100g.HasValue
                    || profile.EstimatedFiberPer100g.HasValue);

            response.Warnings ??= new List<string>();
            response.Alerts ??= new List<string>();

            // Regra 1: FullNutritionLabel sem dados quantitativos → rebaixar para FrontOfPackageOnly
            if (response.AnalysisMode == AnalysisMode.FullNutritionLabel && !hasQuantitativeMacros)
            {
                response.AnalysisMode = AnalysisMode.FrontOfPackageOnly;
                AddWarning(response.Warnings, "Modo de análise ajustado: tabela nutricional indicada, mas sem valores quantitativos extraídos.");
            }

            // Regra 2: FrontOfPackageOnly → basis deve ser transparente sobre ausência de extração quantitativa
            if (response.AnalysisMode == AnalysisMode.FrontOfPackageOnly && profile != null)
            {
                var basisLower = (profile.Basis ?? "").ToLowerInvariant();

                if (!hasQuantitativeMacros)
                {
                    // Sem macros: basis deve deixar claro que não houve estimativa numérica confiável
                    if (!basisLower.Contains("não foi possível") && !basisLower.Contains("não disponíveis"))
                    {
                        profile.Basis = "Análise baseada na frente da embalagem. Não houve extração quantitativa da tabela nutricional nem estimativa numérica confiável aplicada por categoria.";
                    }
                }
                else
                {
                    // Com macros estimados: deixar claro que são estimativas por categoria, não extrações
                    if (basisLower.Contains("estimativa") && basisLower.Contains("categoria")
                        && !basisLower.Contains("não houve extração"))
                    {
                        var categoryName = NormalizeCategoryToProductName(response.Category ?? "categoria");
                        profile.Basis = $"Estimativa por perfil nutricional típico de {categoryName}. Não houve extração quantitativa da tabela nutricional — valores são aproximações baseadas na categoria do produto.";
                    }
                }
            }

            // Regra 3: Summary não pode afirmar "tabela nutricional da embalagem" se modo é FrontOfPackageOnly
            if (response.AnalysisMode == AnalysisMode.FrontOfPackageOnly && !string.IsNullOrWhiteSpace(response.Summary))
            {
                if (response.Summary.Contains("Resumo baseado na tabela nutricional da embalagem", StringComparison.OrdinalIgnoreCase))
                {
                    response.Summary = response.Summary.Replace(
                        "Resumo baseado na tabela nutricional da embalagem.",
                        "Resumo estimado a partir da categoria e da frente da embalagem.",
                        StringComparison.OrdinalIgnoreCase);
                }
            }

            // Regra 4: Score.Reason deve ser coerente com PrincipalOffender quando presente
            if (response.AnalysisMode == AnalysisMode.FrontOfPackageOnly
                && response.Score != null
                && !string.IsNullOrWhiteSpace(response.PrincipalOffender))
            {
                var reason = response.Score.Reason ?? string.Empty;
                if (!reason.Contains(response.PrincipalOffender, StringComparison.OrdinalIgnoreCase))
                {
                    response.Score.Reason = $"{reason} Principal ponto de atenção inferido pela categoria: {response.PrincipalOffender}.".Trim();
                }
            }
        }

        private async Task<Guid> PersistAnalysisAsync(
            NutritionAnalysisResponseDto response,
            string fileName,
            Guid? userId,
            string? deviceId)
        {
            var product = new Product(
                response.ProductName ?? "Produto alimentício",
                response.Brand,
                barcode: null);

            var extractedData = JsonSerializer.Serialize(new PersistedNutritionAnalysisSnapshot
            {
                Category = response.Category,
                AnalysisMode = response.AnalysisMode,
                Score = response.Score?.Value,
                ScoreLabel = response.Score?.Label,
                PrincipalOffender = response.PrincipalOffender,
                Classification = response.Classification,
                ConfidenceDetails = response.ConfidenceDetails,
                Summary = response.Summary,
                VisibleClaims = response.VisibleClaims ?? new List<string>()
            });

            var productLabel = new ProductLabel(
                product.Id,
                fileName ?? string.Empty,
                extractedData,
                DateTimeOffset.UtcNow);

            product.SetLabel(productLabel);

            var nutritionalInfo = BuildNutritionalInfo(product.Id, response.EstimatedNutritionProfile, response.PackageWeight);
            if (nutritionalInfo != null)
            {
                product.SetNutritionalInfo(nutritionalInfo);
            }

            var classification = MapAnalysisClassification(response.Score?.Value, response.Classification);
            var confidence = MapConfidenceLevel(response.ConfidenceDetails, response.AnalysisMode);

            var productAnalysis = new ProductAnalysis(
                product.Id,
                userId,
                classification,
                confidence,
                response.Summary ?? string.Empty,
                deviceId);

            productAnalysis.AttachProduct(product);

            foreach (var message in (response.Alerts ?? new List<string>())
                .Concat(response.Warnings ?? new List<string>())
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                productAnalysis.AddAlert(new AnalysisAlert(
                    productAnalysis.Id,
                    message,
                    classification,
                    confidence));
            }

            await _productRepository.AddAsync(product);
            await _analysisWriteRepository.AddAsync(productAnalysis);

            _logger.LogInformation(
                "Nutrition analysis persisted. AnalysisId={AnalysisId}, Product={ProductName}, UserId={UserId}, DeviceId={DeviceId}, Mode={Mode}",
                productAnalysis.Id,
                response.ProductName ?? "N/A",
                userId?.ToString() ?? "anonymous",
                string.IsNullOrWhiteSpace(deviceId) ? "none" : deviceId,
                response.AnalysisMode);

            return productAnalysis.Id;
        }

        private static NutritionalInfo? BuildNutritionalInfo(
            Guid productId,
            EstimatedNutritionProfileDto? profile,
            string? packageWeight)
        {
            if (profile == null)
            {
                return null;
            }

            var hasNutrition = profile.CaloriesPer100g.HasValue
                || profile.EstimatedSugarPer100g.HasValue
                || profile.EstimatedProteinPer100g.HasValue
                || profile.EstimatedSodiumPer100g.HasValue
                || profile.EstimatedFiberPer100g.HasValue
                || profile.EstimatedFatPer100g.HasValue;

            if (!hasNutrition)
            {
                return null;
            }

            var info = new NutritionalInfo(productId);
            info.UpdateMacros(
                calories: ToDecimal(profile.CaloriesPer100g),
                totalFat: ToDecimal(profile.EstimatedFatPer100g),
                sodium: ToDecimal(profile.EstimatedSodiumPer100g),
                fiber: ToDecimal(profile.EstimatedFiberPer100g),
                sugars: ToDecimal(profile.EstimatedSugarPer100g),
                protein: ToDecimal(profile.EstimatedProteinPer100g));
            var packageWeightInGrams = ParsePackageWeightInGrams(packageWeight);
            var servingsPerContainer = packageWeightInGrams.HasValue && packageWeightInGrams.Value > 0
                ? ToDecimal(packageWeightInGrams.Value / 100d)
                : null;

            info.UpdateServing("100 g", servingsPerContainer);
            return info;
        }

        private static decimal? ToDecimal(double? value)
        {
            return value.HasValue ? Convert.ToDecimal(value.Value) : null;
        }

        private static AnalysisClassification MapAnalysisClassification(
            int? score,
            ProductClassificationDto? classification)
        {
            if (score.HasValue)
            {
                return score.Value switch
                {
                    >= 80 => AnalysisClassification.Excellent,
                    >= 60 => AnalysisClassification.Safe,
                    >= 40 => AnalysisClassification.Moderate,
                    >= 25 => AnalysisClassification.Caution,
                    _ => AnalysisClassification.Avoid
                };
            }

            var statuses = new[]
            {
                classification?.Diabetic?.Status,
                classification?.BloodPressure?.Status,
                classification?.WeightLoss?.Status,
                classification?.MuscleGain?.Status
            };

            if (statuses.Any(status => status != null && status.Contains("nao", StringComparison.OrdinalIgnoreCase)))
            {
                return AnalysisClassification.Caution;
            }

            if (statuses.Any(status => status != null && status.Contains("adequado", StringComparison.OrdinalIgnoreCase)))
            {
                return AnalysisClassification.Safe;
            }

            return AnalysisClassification.Unknown;
        }

        private static ConfidenceLevel MapConfidenceLevel(
            ConfidenceDetailsDto? confidenceDetails,
            AnalysisMode analysisMode)
        {
            var values = new[]
            {
                confidenceDetails?.ProductIdentification,
                confidenceDetails?.VisibleClaimsExtraction,
                confidenceDetails?.EstimatedNutritionProfile,
                confidenceDetails?.Classification
            }
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

            if (values.Count == 0)
            {
                return analysisMode == AnalysisMode.FullNutritionLabel
                    ? ConfidenceLevel.Medium
                    : ConfidenceLevel.Low;
            }

            var average = values.Average();
            return average switch
            {
                >= 0.8 => ConfidenceLevel.High,
                >= 0.55 => ConfidenceLevel.Medium,
                _ => ConfidenceLevel.Low
            };
        }

        private sealed class PersistedNutritionAnalysisSnapshot
        {
            public string? Category { get; set; }
            public AnalysisMode AnalysisMode { get; set; }
            public int? Score { get; set; }
            public string? ScoreLabel { get; set; }
            public string? PrincipalOffender { get; set; }
            public ProductClassificationDto? Classification { get; set; }
            public ConfidenceDetailsDto? ConfidenceDetails { get; set; }
            public string? Summary { get; set; }
            public List<string> VisibleClaims { get; set; } = new();
        }

        #endregion
    }
}
