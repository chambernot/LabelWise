using LabelWise.Application.Configuration;
using LabelWise.Application.Interfaces;
using LabelWise.Infrastructure.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using LabelWise.Infrastructure.Persistence.Mongo;

namespace LabelWise.Infrastructure.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            MongoSerializationConfigurator.Configure();
            var mongoSettings = configuration.GetSection(MongoDbSettings.SectionName).Get<MongoDbSettings>() ?? new MongoDbSettings();

            services.AddSingleton(Options.Create(mongoSettings));
            services.AddSingleton(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;

                if (string.IsNullOrWhiteSpace(settings.ConnectionString))
                {
                    throw new InvalidOperationException("MongoDbSettings:ConnectionString não foi configurada.");
                }

                return new MongoClient(settings.ConnectionString);
            });

            services.AddSingleton(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;

                if (string.IsNullOrWhiteSpace(settings.DatabaseName))
                {
                    throw new InvalidOperationException("MongoDbSettings:DatabaseName não foi configurado.");
                }

                return sp.GetRequiredService<MongoClient>().GetDatabase(settings.DatabaseName);
            });

            services.AddSingleton<MongoDbContext>();
            services.AddSingleton<MongoLegacyBootstrapSeeder>();
            services.AddHostedService<MongoIndexInitializerHostedService>();

            // register repositories, infra services, OCR/AI adapters (stubs)
            // services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<LabelWise.Application.Interfaces.IUserRepository, LabelWise.Infrastructure.Repositories.UserRepository>();
            services.AddScoped<LabelWise.Application.Interfaces.IUserProfileRepository, LabelWise.Infrastructure.Repositories.UserProfileRepository>();
            services.AddScoped<LabelWise.Application.Interfaces.IAppUserRepository, LabelWise.Infrastructure.Repositories.AppUserRepository>();
            services.AddScoped<LabelWise.Application.Interfaces.IProductRepository, LabelWise.Infrastructure.Repositories.ProductRepository>();
            services.AddScoped<LabelWise.Application.Interfaces.IAnalysisRepository, LabelWise.Infrastructure.Repositories.AnalysisRepository>();
            services.AddScoped<LabelWise.Application.Interfaces.IAnalysisWriteRepository, LabelWise.Infrastructure.Repositories.AnalysisWriteRepository>();
            services.AddScoped<LabelWise.Application.Interfaces.IConversationRepository, LabelWise.Infrastructure.Repositories.ConversationRepository>();
            // services.AddScoped<IOcrService, OcrHttpClientService>();
            // services.AddScoped<IIaService, OpenAiAdapterService>();
            // register local storage implementation for temporary files
            services.AddSingleton<LabelWise.Infrastructure.Storage.IFileStorage, LabelWise.Infrastructure.Storage.LocalFileStorage>();

            // ═══════════════════════════════════════════════════════════════════════════
            // OCR PROVIDER CONFIGURATION - CONFIGURÁVEL VIA appsettings.json
            // ═══════════════════════════════════════════════════════════════════════════

            ConfigureOcrProvider(services, configuration);

            // Image Upload Service
            services.AddScoped<LabelWise.Application.Interfaces.IImageUploadService, LabelWise.Infrastructure.Services.ImageUploadService>();

            // Label Reading Service
            services.AddScoped<LabelWise.Application.Interfaces.ILabelReadingService, LabelWise.Infrastructure.Services.LabelReadingService>();

            // Pipeline Orchestrator
            services.AddScoped<LabelWise.Application.Interfaces.IProductAnalysisPipelineOrchestrator, LabelWise.Infrastructure.Services.ProductAnalysisPipelineOrchestrator>();

            // register product analysis service implementation (infrastructure implementation)
            services.AddScoped<LabelWise.Application.Interfaces.IProductAnalysisService, LabelWise.Infrastructure.Services.ProductAnalysisServiceImpl>();

            // register analysis history service
            services.AddScoped<LabelWise.Application.Interfaces.IAppAccessService, LabelWise.Infrastructure.Services.AppAccessService>();
            services.AddScoped<LabelWise.Application.Interfaces.ISubscriptionService, LabelWise.Infrastructure.Services.SubscriptionService>();
            services.AddScoped<LabelWise.Application.Interfaces.IAnalysisHistoryService, LabelWise.Infrastructure.Services.AnalysisHistoryService>();
            services.AddScoped<LabelWise.Application.Interfaces.IAnalysisHistoryRepository, LabelWise.Infrastructure.Repositories.AnalysisHistoryRepository>();
            services.AddScoped<LabelWise.Application.Interfaces.IScoreInterpretationService, LabelWise.Infrastructure.Services.ScoreInterpretationService>();
            services.AddScoped<LabelWise.Application.Interfaces.IProductComparisonService, LabelWise.Infrastructure.Services.ProductComparisonService>();
            services.AddScoped<LabelWise.Application.Interfaces.IImageAnalysisCacheService, LabelWise.Infrastructure.Services.ImageAnalysisCacheService>();
            services.AddScoped<LabelWise.Application.Interfaces.INutritionFingerprintService, LabelWise.Infrastructure.Services.NutritionFingerprintService>();

            // ═══════════════════════════════════════════════════════════════════════════
            // NUTRITION PIPELINE SERVICES (Nova pipeline de 9 stages)
            // ═══════════════════════════════════════════════════════════════════════════
            services.AddScoped<LabelWise.Application.Interfaces.ICategoryDecisionEngine, LabelWise.Infrastructure.Services.NutritionPipeline.CategoryDecisionEngine>();
            services.AddScoped<LabelWise.Application.Interfaces.IScoreCalculator, LabelWise.Infrastructure.Services.NutritionPipeline.PipelineScoreCalculator>();
            services.AddScoped<LabelWise.Application.Interfaces.IAnalysisConsistencyValidator, LabelWise.Infrastructure.Services.NutritionPipeline.AnalysisConsistencyValidator>();
            services.AddScoped<LabelWise.Application.Interfaces.INutritionParseSanityValidator, LabelWise.Infrastructure.Services.NutritionPipeline.NutritionParseSanityValidator>();
            services.AddScoped<LabelWise.Application.Interfaces.INutritionResponseMapper, LabelWise.Infrastructure.Services.NutritionPipeline.NutritionResponseMapper>();

            // ═══════════════════════════════════════════════════════════════════════════
            // MULTI-CAPTURE PERSISTENCE SERVICES
            // ═══════════════════════════════════════════════════════════════════════════
            services.AddScoped<LabelWise.Application.Interfaces.IProductCaptureRepository, LabelWise.Infrastructure.Repositories.ProductCaptureRepository>();
            services.AddScoped<LabelWise.Application.Interfaces.IProductAnalysisSessionRepository, LabelWise.Infrastructure.Repositories.ProductAnalysisSessionRepository>();
            services.AddScoped<LabelWise.Application.Interfaces.IValidatedProductRepository, LabelWise.Infrastructure.Repositories.ValidatedProductRepository>();
            services.AddScoped<LabelWise.Application.Interfaces.ICapturePersistenceService, LabelWise.Infrastructure.Services.CapturePersistenceService>();
            services.AddScoped<LabelWise.Application.Interfaces.IProductCacheService, LabelWise.Infrastructure.Services.ProductCacheService>();

            // ═══════════════════════════════════════════════════════════════════════════
            // CANDIDATE SUGGESTION SERVICE (Fallback para identificação)
            // ═══════════════════════════════════════════════════════════════════════════
            services.AddScoped<LabelWise.Application.Interfaces.ICandidateSuggestionService, LabelWise.Infrastructure.Services.CandidateSuggestionService>();

            // ═══════════════════════════════════════════════════════════════════════════
            // KNOWN PRODUCTS CATALOG - MongoDB Search
            // ═══════════════════════════════════════════════════════════════════════════
            services.AddScoped<LabelWise.Application.Interfaces.IKnownProductRepository, LabelWise.Infrastructure.Repositories.KnownProductRepository>();
            services.AddScoped<LabelWise.Application.Interfaces.IKnownProductSearchService, LabelWise.Infrastructure.Services.MongoKnownProductSearchService>();

            // ═══════════════════════════════════════════════════════════════════════════
            // NUTRITION FALLBACK SYSTEM - Database-driven Category Profiles
            // ═══════════════════════════════════════════════════════════════════════════
            services.AddScoped<LabelWise.Application.Interfaces.ICategoryNutritionProfileRepository, LabelWise.Infrastructure.Repositories.CategoryNutritionProfileRepository>();
            services.AddScoped<LabelWise.Application.Interfaces.ICategoryMappingRepository, LabelWise.Infrastructure.Repositories.CategoryMappingRepository>();
            services.AddScoped<LabelWise.Application.Interfaces.IDatabaseNutritionFallbackService, LabelWise.Infrastructure.Services.DatabaseNutritionFallbackService>();

            // Nutrition Fallback Pipeline Services
            services.AddScoped<LabelWise.Application.Interfaces.ICategoryNormalizationService, LabelWise.Infrastructure.Services.CategoryNormalizationService>();
            services.AddScoped<LabelWise.Application.Interfaces.INutritionDataMergeService, LabelWise.Infrastructure.Services.NutritionDataMergeService>();
            services.AddScoped<LabelWise.Application.Interfaces.IPrincipalOffenderDetector, LabelWise.Infrastructure.Services.PrincipalOffenderDetector>();

            // Enhanced Nutrition Pipeline Orchestrator
            services.AddScoped<LabelWise.Application.Interfaces.IEnhancedNutritionPipelineOrchestrator, LabelWise.Infrastructure.Services.EnhancedNutritionPipelineOrchestrator>();

            // Product Identification Service (depende do CandidateSuggestionService + KnownProductSearchService)
            services.AddScoped<LabelWise.Application.Interfaces.IProductIdentificationService, LabelWise.Infrastructure.Services.ProductIdentificationService>();

            // ═══════════════════════════════════════════════════════════════════════════
            // GUIDED CAPTURE SERVICE (Fluxo guiado para apps mobile)
            // ═══════════════════════════════════════════════════════════════════════════
            services.AddScoped<LabelWise.Application.Interfaces.IGuidedCaptureService, LabelWise.Infrastructure.Services.GuidedCaptureService>();

            // ═══════════════════════════════════════════════════════════════════════════
            // DEV FULL GUIDED ANALYSIS ORCHESTRATOR (Development only)
            // ═══════════════════════════════════════════════════════════════════════════
            services.AddScoped<LabelWise.Application.Interfaces.IDevFullGuidedAnalysisOrchestrator, LabelWise.Infrastructure.Services.DevFullGuidedAnalysisOrchestrator>();

            // ═══════════════════════════════════════════════════════════════════════════
            // NUTRITION ANALYSIS SERVICE (Pipeline de 9 stages — substitui o legado)
            // ═══════════════════════════════════════════════════════════════════════════
            services.AddScoped<LabelWise.Application.Interfaces.INutritionAnalysisService, LabelWise.Infrastructure.Services.NutritionPipeline.NutritionAnalysisPipeline>();
            services.AddScoped<LabelWise.Application.Interfaces.INutritionSanitizer, LabelWise.Infrastructure.Services.NutritionSanitizer>();
            services.AddScoped<LabelWise.Application.Interfaces.IAdvancedNutritionScoringService, LabelWise.Infrastructure.Services.AdvancedNutritionScoringService>();
            services.AddScoped<LabelWise.Application.Interfaces.INutritionDataValidatorService, LabelWise.Infrastructure.Services.NutritionDataValidatorService>();
            services.AddScoped<LabelWise.Application.Interfaces.INutritionConsistencyValidator, LabelWise.Infrastructure.Services.NutritionConsistencyValidator>();
            services.AddScoped<LabelWise.Application.Interfaces.INutritionAutoCorrector, LabelWise.Infrastructure.Services.NutritionAutoCorrector>();
            services.AddScoped<LabelWise.Application.Interfaces.INutritionOcrCrossValidator, LabelWise.Infrastructure.Services.NutritionOcrCrossValidator>();
            services.AddScoped<LabelWise.Application.Interfaces.INutritionScoringService, LabelWise.Infrastructure.Services.NutritionScoringServiceV2>();
            services.AddScoped<LabelWise.Application.Interfaces.IIntelligentAnalysisScoreService, LabelWise.Infrastructure.Services.IntelligentAnalysisScoreService>();
            services.AddScoped<LabelWise.Application.Interfaces.INutritionResponseBuilder, LabelWise.Infrastructure.Services.NutritionResponseBuilder>();
            services.AddScoped<LabelWise.Application.Interfaces.INutritionConfidenceEngine, LabelWise.Infrastructure.Services.NutritionConfidenceEngine>();

            // ── State Machine determinística (única fonte de verdade do pipeline) ──
            services.AddScoped<LabelWise.Application.Interfaces.INutritionStateMachine, LabelWise.Infrastructure.Services.NutritionStateMachine>();

            // ── Split Validator / Enricher (NutritionDataValidatorService implementa ambos) ──
            services.AddScoped<LabelWise.Application.Interfaces.INutritionValidator>(sp =>
                sp.GetRequiredService<LabelWise.Infrastructure.Services.NutritionDataValidatorService>());
            services.AddScoped<LabelWise.Application.Interfaces.INutritionEnricher>(sp =>
                sp.GetRequiredService<LabelWise.Infrastructure.Services.NutritionDataValidatorService>());
            services.AddScoped<LabelWise.Infrastructure.Services.NutritionDataValidatorService>();

            // ── Nutrition Analysis Orchestrator ───────────────────────────────────────
            services.AddScoped<LabelWise.Application.Interfaces.INutritionAnalysisOrchestrator,
                               LabelWise.Infrastructure.Services.NutritionAnalysisOrchestrator>();

            // ═══════════════════════════════════════════════════════════════════════════
            // OPEN FOOD FACTS SERVICE (Fonte primária quando há código de barras)
            // ═══════════════════════════════════════════════════════════════════════════
            services.AddSingleton<LabelWise.Application.Interfaces.IBarcodeDetectorService, LabelWise.Infrastructure.Services.BarcodeDetectorService>();

            services.AddHttpClient<LabelWise.Application.Interfaces.IOpenFoodFactsService, LabelWise.Infrastructure.Services.OpenFoodFactsService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(5);
                client.DefaultRequestHeaders.Add("User-Agent", "LabelWise/1.0 (contact@labelwise.app)");
            });

            // Hybrid OCR Validator (Azure Computer Vision + OpenAI Vision)
            services.AddScoped<LabelWise.Application.Interfaces.IHybridOcrValidator>(sp =>
            {
                var config = configuration.GetSection("OCR:AzureVision");
                var endpoint = config["Endpoint"];
                var apiKey = config["ApiKey"];
                var logger = sp.GetRequiredService<ILogger<LabelWise.Infrastructure.Services.HybridOcrValidator>>();

                if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
                {
                    logger.LogWarning("Azure Computer Vision not configured for HybridOcrValidator, validation will be skipped");
                    return new LabelWise.Infrastructure.Services.HybridOcrValidator(
                        new LabelWise.Infrastructure.Ocr.MockOcrProvider(),
                        logger);
                }

                var azureVisionOcr = new LabelWise.Infrastructure.Ocr.AzureComputerVisionOcrProvider(
                    endpoint,
                    apiKey,
                    sp.GetService<ILogger<LabelWise.Infrastructure.Ocr.AzureComputerVisionOcrProvider>>());

                logger.LogInformation("✅ HybridOcrValidator configured with Azure Computer Vision");
                return new LabelWise.Infrastructure.Services.HybridOcrValidator(azureVisionOcr, logger);
            });

            // ═══════════════════════════════════════════════════════════════════════════
            // STRUCTURED TABLE OCR PARSER (Usa coordenadas espaciais do OCR)
            // ═══════════════════════════════════════════════════════════════════════════
            services.AddScoped<LabelWise.Infrastructure.Services.StructuredTableOcrParser>();

            // ═══════════════════════════════════════════════════════════════════════════
            // REFACTORED NUTRITION ANALYSIS SERVICE (Com separação de dados extraídos/estimados)
            // ═══════════════════════════════════════════════════════════════════════════
            services.AddScoped<LabelWise.Infrastructure.Services.RefactoredNutritionAnalysisService>();

            // Register OpenAI Vision Service (non-Azure)
            services.AddSingleton(_ =>
            {
                var section = configuration.GetSection(AzureOpenAiVisionOptions.SectionName);
                return Options.Create(new AzureOpenAiVisionOptions
                {
                    Endpoint = section["Endpoint"] ?? string.Empty,
                    ApiKey = section["ApiKey"] ?? string.Empty,
                    Model = section["Model"] ?? "gpt-4o",
                    DebugImagePath = section["DebugImagePath"]
                });
            });

            services.AddScoped<IVisualInterpreter, LabelWise.Infrastructure.AI.NutritionVisionInterpreter>();

            // ═══════════════════════════════════════════════════════════════════════════
            // NUTRITION IMAGE ANALYZER (OpenAI Vision para extração nutricional)
            // ═══════════════════════════════════════════════════════════════════════════
            services.AddHttpClient("OpenAI");
            services.AddScoped<LabelWise.Application.Interfaces.INutritionImageAnalyzer, 
                               LabelWise.Infrastructure.AI.OpenAiNutritionImageAnalyzer>();
            services.AddScoped<LabelWise.Application.Interfaces.IOpenAIConversationService,
                               LabelWise.Infrastructure.AI.OpenAIConversationService>();
            services.AddScoped<LabelWise.Application.Interfaces.INutritionConversationService,
                               LabelWise.Infrastructure.Services.NutritionConversationService>();

            services.AddScoped<LabelWise.Application.Interfaces.IOpenAIIngredientAnalysisService,
                               LabelWise.Infrastructure.AI.OpenAIIngredientAnalysisService>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.IngredientClassifier>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.IngredientNormalizer>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.AllergenDetector>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.ClaimDetector>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.DietProfileEngine>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.FoodCompatibilityEngine>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.IngredientConfidenceEngine>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.ProcessingLevelClassifier>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.PositiveIngredientDetector>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.AnalysisCompletenessEvaluator>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.IngredientSemanticEngine>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.AssistantSummaryBuilder>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.OcrCorrectionEngine>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.OcrSemanticCleaner>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.OcrCleaningService>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.SemanticReconstructionService>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.StructuredTextLayer>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.RegulatoryClaimExtractor>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.IngredientParserService>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.SemanticClaimResolver>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.SemanticSafetyValidator>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.SemanticFoodConsolidator>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.FoodSemanticResponseBuilder>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.ProductionSafeFoodAnalysisEngine>();
            services.AddScoped<LabelWise.Application.Interfaces.IFoodAnalysisTrustEngine, LabelWise.Infrastructure.Services.FoodAnalysisTrustEngine>();
            services.AddScoped<LabelWise.Infrastructure.Services.FoodAnalysisQualityGate>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.IngredientAnchorDetector>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.IngredientGrammarValidator>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.IngredientRegionPromoter>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.IngredientRecoveryEngine>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.ProcessingSignalRecovery>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.IngredientNoiseFilter>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.NutritionLeakBlocker>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.IngredientSemanticValidator>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.IngredientPurificationLayer>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.FoodDictionaryNormalizer>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.FoodTokenRepairEngine>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.OCRSemanticSanitizer>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.CompoundIngredientSplitter>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.CompoundIngredientParser>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.SemanticRegionTransitionEngine>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.BlockBoundaryResolver>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.ConfidencePenaltyRules>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.ConfidenceCalibrationEngine>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.IngredientTextSanitizer>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.IngredientProductionValidator>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.IngredientPresentationBuilder>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.FoodOcrCleaningEngine>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.FoodIngredientTokenizer>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.FoodEntityValidator>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.FoodCanonicalizationEngine>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.SemanticDeduplicationEngine>();
            services.AddScoped<LabelWise.Infrastructure.Services.IngredientAnalysis.FoodSemanticFinalizationEngine>();
            services.AddScoped<LabelWise.Application.Interfaces.IRegulatoryEngine, LabelWise.Infrastructure.Services.FoodAnalysis.RegulatoryEngine>();
            services.AddScoped<LabelWise.Application.Interfaces.IConflictResolutionEngine, LabelWise.Infrastructure.Services.FoodAnalysis.ConflictResolutionEngine>();
            services.AddScoped<LabelWise.Application.Interfaces.IDecisionEngine, LabelWise.Infrastructure.Services.FoodAnalysis.UnifiedFoodDecisionEngine>();
            services.AddScoped<LabelWise.Infrastructure.Services.FoodAnalysis.IngredientKnowledgeBase>();
            services.AddScoped<LabelWise.Infrastructure.Services.FoodAnalysis.CompatibilityDecisionEngine>();
            services.AddScoped<LabelWise.Infrastructure.Services.FoodAnalysis.RegulatoryCompatibilityResolver>();
            services.AddScoped<LabelWise.Infrastructure.Services.FoodAnalysis.CompatibilityDeterministicResolver>();
            services.AddScoped<LabelWise.Infrastructure.Services.FoodAnalysis.FoodProcessingEngine>();
            services.AddScoped<LabelWise.Infrastructure.Services.FoodAnalysis.ProcessingConfidenceGate>();
            services.AddScoped<LabelWise.Infrastructure.Services.FoodAnalysis.SemanticConsolidationEngine>();
            services.AddScoped<LabelWise.Infrastructure.Services.FoodAnalysis.QuickInsightSafetyFilter>();
            services.AddScoped<LabelWise.Infrastructure.Services.FoodAnalysis.ProductionSafetyValidator>();
            services.AddScoped<LabelWise.Infrastructure.Services.FoodAnalysis.UserProfileFoodRestrictions>();
            services.AddScoped<LabelWise.Application.Interfaces.IIngredientAnalysisService,
                               LabelWise.Infrastructure.Services.IngredientAnalysisService>();

            // ═══════════════════════════════════════════════════════════════════════════
            // DOCUMENT INTELLIGENCE SERVICE (Azure Form Recognizer — prebuilt-layout)
            // ═══════════════════════════════════════════════════════════════════════════
            services.Configure<LabelWise.Application.Configuration.AzureDocumentIntelligenceOptions>(
                configuration.GetSection(LabelWise.Application.Configuration.AzureDocumentIntelligenceOptions.SectionName));
            services.AddScoped<LabelWise.Application.Interfaces.IDocumentIntelligenceService,
                               LabelWise.Infrastructure.AI.DocumentIntelligenceService>();

            // ═══════════════════════════════════════════════════════════════════════════
            // NUTRITION SCORING SERVICE V2 (Sistema científico e consistente)
            // ═══════════════════════════════════════════════════════════════════════════
            services.AddScoped<LabelWise.Application.Interfaces.INutritionScoringService, 
                               LabelWise.Infrastructure.Services.NutritionScoringServiceV2>();

            // ═══════════════════════════════════════════════════════════════════════════
            // DETERMINISTIC OCR PIPELINE SERVICES
            // ═══════════════════════════════════════════════════════════════════════════
            services.AddScoped<LabelWise.Application.Interfaces.IOcrParserService,    LabelWise.Infrastructure.Services.OcrParserService>();
            services.AddScoped<LabelWise.Application.Interfaces.INutritionFixerService, LabelWise.Infrastructure.Services.NutritionFixerService>();
            services.AddScoped<LabelWise.Application.Interfaces.IConfidenceService,   LabelWise.Infrastructure.Services.ConfidenceService>();
            services.AddScoped<LabelWise.Application.Interfaces.ICategoryService,     LabelWise.Infrastructure.Services.CategoryService>();
            services.AddScoped<LabelWise.Application.Interfaces.ISummaryService,      LabelWise.Infrastructure.Services.SummaryService>();

            // ═══════════════════════════════════════════════════════════════════════════
            // IMAGE PRE-PROCESSING + OCR QUALITY EVALUATION
            // ═══════════════════════════════════════════════════════════════════════════
            services.AddSingleton<LabelWise.Application.Interfaces.IImagePreprocessingService,
                                  LabelWise.Infrastructure.Services.ImagePreprocessingService>();
            services.AddSingleton<LabelWise.Application.Interfaces.IOcrQualityEvaluator,
                                  LabelWise.Infrastructure.Services.OcrQualityEvaluator>();

            return services;
        }

        private static void ConfigureOcrProvider(IServiceCollection services, IConfiguration configuration)
        {
            var ocrOptions = configuration.GetSection("OCR").Get<OcrOptions>() ?? new OcrOptions();

            System.Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            System.Console.WriteLine("📋 OCR PROVIDER CONFIGURATION");
            System.Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            System.Console.WriteLine($"🔧 Provider: {ocrOptions.Provider}");
            System.Console.WriteLine($"🎭 Use Mock Provider: {ocrOptions.UseMockProvider}");

            // REGRA CRÍTICA: UseMockProvider tem precedência sobre Provider
            if (ocrOptions.UseMockProvider)
            {
                ConfigureMockProvider(services);
            }
            else if (ocrOptions.Provider.Equals("Tesseract", StringComparison.OrdinalIgnoreCase))
            {
                ConfigureTesseractProvider(services, ocrOptions);
            }
            else if (ocrOptions.Provider.Equals("AzureComputerVision", StringComparison.OrdinalIgnoreCase))
            {
                ConfigureAzureProvider(services, ocrOptions);
            }
            else if (ocrOptions.Provider.Equals("AzureVision", StringComparison.OrdinalIgnoreCase))
            {
                ConfigureAzureVisionProvider(services, ocrOptions);
            }
            else if (ocrOptions.Provider.Equals("Selector", StringComparison.OrdinalIgnoreCase))
            {
                ConfigureSelectorProvider(services, ocrOptions);
            }
            else if (ocrOptions.Provider.Equals("Composite", StringComparison.OrdinalIgnoreCase))
            {
                ConfigureCompositeProvider(services, ocrOptions);
            }
            else if (ocrOptions.Provider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
            {
                ConfigureMockProvider(services);
            }
            else
            {
                System.Console.WriteLine($"   ❌ UNKNOWN PROVIDER: {ocrOptions.Provider}");
                System.Console.WriteLine("   ⚠️  Provider not recognized!");
                System.Console.WriteLine("   💡 Valid options: 'Tesseract', 'AzureComputerVision', 'AzureVision', 'Selector', 'Composite', 'Mock'");
                System.Console.WriteLine("   ❌ APPLICATION STARTUP WILL FAIL");

                throw new InvalidOperationException(
                    $"Invalid OCR provider configured: '{ocrOptions.Provider}'. " +
                    $"Valid options are: 'Tesseract', 'AzureComputerVision', 'AzureVision', 'Selector', 'Composite', 'Mock'. " +
                    $"Check your appsettings.json configuration.");
            }

            System.Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
        }

        private static void ConfigureMockProvider(IServiceCollection services)
        {
            System.Console.WriteLine("⚠️  MOCK PROVIDER SELECTED");
            System.Console.WriteLine("   ℹ️  Using MockOcrProvider (SIMULATED data only)");
            System.Console.WriteLine("   ⚠️  This provider returns FAKE data for development");
            System.Console.WriteLine("   ❌ NOT SUITABLE FOR PRODUCTION");
            System.Console.WriteLine("   💡 Set 'OCR:UseMockProvider' to false to use real OCR");
            services.AddSingleton<LabelWise.Application.Interfaces.IOcrProvider, LabelWise.Infrastructure.Ocr.MockOcrProvider>();
        }

        private static void ConfigureTesseractProvider(IServiceCollection services, OcrOptions ocrOptions)
        {
            System.Console.WriteLine("✅ TESSERACT PROVIDER SELECTED");
            System.Console.WriteLine("   🚀 Using TesseractOcrProvider (REAL OCR)");
            System.Console.WriteLine($"   📂 Tessdata Path: {ocrOptions.TessdataPath ?? "[auto-detect]"}");
            System.Console.WriteLine($"   🌐 Language: {ocrOptions.Language}");

            services.AddSingleton<LabelWise.Application.Interfaces.IOcrProvider>(sp =>
            {
                var logger = sp.GetService<ILogger<LabelWise.Infrastructure.Ocr.TesseractOcrProvider>>();
                var provider = new LabelWise.Infrastructure.Ocr.TesseractOcrProvider(
                    logger,
                    ocrOptions.TessdataPath,
                    ocrOptions.Language);

                logger?.LogInformation("🚀 IOcrProvider registered: {ProviderName} (Type: {ProviderType})",
                    provider.ProviderName,
                    provider.GetType().FullName);

                System.Console.WriteLine($"   ✅ Provider Instantiated: {provider.ProviderName}");
                System.Console.WriteLine($"   📦 Provider Type: {provider.GetType().FullName}");

                if (ocrOptions.ValidateOnStartup)
                {
                    var metadata = provider.GetMetadata();
                    var isAvailable = provider.IsAvailableAsync().Result;

                    System.Console.WriteLine($"   🔍 Validation: Available = {isAvailable}");

                    if (!isAvailable)
                    {
                        System.Console.WriteLine("   ❌ WARNING: Tesseract is NOT properly configured!");
                        System.Console.WriteLine("   📋 Metadata:");
                        foreach (var kvp in metadata)
                        {
                            System.Console.WriteLine($"      - {kvp.Key}: {kvp.Value}");
                        }

                        logger?.LogError(
                            "Tesseract OCR is not available. Check tessdata configuration. " +
                            "The application will start but OCR operations will FAIL.");
                    }
                    else
                    {
                        System.Console.WriteLine("   ✅ Tesseract validated successfully!");
                    }
                }

                return provider;
            });
        }

        private static void ConfigureAzureProvider(IServiceCollection services, OcrOptions ocrOptions)
        {
            System.Console.WriteLine("☁️  AZURE COMPUTER VISION PROVIDER SELECTED");
            System.Console.WriteLine("   🚀 Using AzureComputerVisionOcrProvider");
            System.Console.WriteLine($"   🌐 Endpoint: {ocrOptions.Azure.Endpoint ?? "[NOT CONFIGURED]"}");
            System.Console.WriteLine($"   🔑 API Key: {(string.IsNullOrWhiteSpace(ocrOptions.Azure.ApiKey) ? "[NOT CONFIGURED]" : "[CONFIGURED]")}");

            if (string.IsNullOrWhiteSpace(ocrOptions.Azure.Endpoint) || 
                string.IsNullOrWhiteSpace(ocrOptions.Azure.ApiKey))
            {
                System.Console.WriteLine("   ❌ ERROR: Azure endpoint or API key not configured!");
                System.Console.WriteLine("   💡 Configure in appsettings.json:");
                System.Console.WriteLine("      OCR:Azure:Endpoint = https://your-resource.cognitiveservices.azure.com/");
                System.Console.WriteLine("      OCR:Azure:ApiKey = your-api-key");
                throw new InvalidOperationException(
                    "Azure Computer Vision OCR selected but endpoint or API key not configured. " +
                    "Please configure OCR:Azure:Endpoint and OCR:Azure:ApiKey in appsettings.json");
            }

            services.AddSingleton<LabelWise.Application.Interfaces.IOcrProvider>(sp =>
            {
                var logger = sp.GetService<ILogger<LabelWise.Infrastructure.Ocr.AzureComputerVisionOcrProvider>>();
                var provider = new LabelWise.Infrastructure.Ocr.AzureComputerVisionOcrProvider(
                    ocrOptions.Azure.Endpoint!,
                    ocrOptions.Azure.ApiKey!,
                    logger);

                logger?.LogInformation("🚀 IOcrProvider registered: {ProviderName}", provider.ProviderName);
                System.Console.WriteLine($"   ✅ Provider Instantiated: {provider.ProviderName}");

                if (ocrOptions.Azure.ValidateOnStartup)
                {
                    var isAvailable = provider.IsAvailableAsync().Result;
                    System.Console.WriteLine($"   🔍 Validation: Available = {isAvailable}");

                    if (!isAvailable)
                    {
                        logger?.LogWarning("Azure Computer Vision may not be properly configured");
                    }
                }

                return provider;
            });
        }

        private static void ConfigureAzureVisionProvider(IServiceCollection services, OcrOptions ocrOptions)
        {
            System.Console.WriteLine("☁️  AZURE AI VISION READ OCR PROVIDER SELECTED");
            System.Console.WriteLine("   🚀 Using AzureVisionReadOcrProvider (Read API)");
            System.Console.WriteLine($"   🌐 Endpoint: {ocrOptions.AzureVision.Endpoint ?? "[NOT CONFIGURED]"}");
            System.Console.WriteLine($"   🔑 API Key: {(string.IsNullOrWhiteSpace(ocrOptions.AzureVision.ApiKey) ? "[NOT CONFIGURED]" : "[CONFIGURED]")}");
            System.Console.WriteLine($"   🌍 Language: {ocrOptions.AzureVision.Language}");

            if (string.IsNullOrWhiteSpace(ocrOptions.AzureVision.Endpoint) ||
                string.IsNullOrWhiteSpace(ocrOptions.AzureVision.ApiKey))
            {
                System.Console.WriteLine("   ❌ ERROR: Azure Vision endpoint or API key not configured!");
                System.Console.WriteLine("   💡 Configure in appsettings.json:");
                System.Console.WriteLine("      OCR:AzureVision:Endpoint = https://your-resource.cognitiveservices.azure.com/");
                System.Console.WriteLine("      OCR:AzureVision:ApiKey = your-api-key");
                throw new InvalidOperationException(
                    "Azure AI Vision OCR selected but endpoint or API key not configured. " +
                    "Please configure OCR:AzureVision:Endpoint and OCR:AzureVision:ApiKey in appsettings.json");
            }

            services.AddSingleton<LabelWise.Application.Interfaces.IOcrProvider>(sp =>
            {
                var logger = sp.GetService<ILogger<LabelWise.Infrastructure.Ocr.AzureVisionReadOcrProvider>>();
                var provider = new LabelWise.Infrastructure.Ocr.AzureVisionReadOcrProvider(
                    ocrOptions.AzureVision.Endpoint,
                    ocrOptions.AzureVision.ApiKey,
                    ocrOptions.AzureVision.Language,
                    ocrOptions.AzureVision.TimeoutSeconds,
                    ocrOptions.AzureVision.EnableDetailedLogging,
                    logger);

                logger?.LogInformation("🚀 IOcrProvider registered: {ProviderName}", provider.ProviderName);
                System.Console.WriteLine($"   ✅ Provider Instantiated: {provider.ProviderName}");

                if (ocrOptions.AzureVision.ValidateOnStartup)
                {
                    var isAvailable = provider.IsAvailableAsync().Result;
                    System.Console.WriteLine($"   🔍 Validation: Available = {isAvailable}");

                    if (!isAvailable)
                    {
                        logger?.LogWarning("Azure Vision may not be properly configured");
                    }
                    else
                    {
                        System.Console.WriteLine("   ✅ Azure Vision validated successfully!");
                    }
                }

                return provider;
            });
        }

        private static void ConfigureSelectorProvider(IServiceCollection services, OcrOptions ocrOptions)
        {
            System.Console.WriteLine("🎯 SMART OCR SELECTOR PROVIDER (Tesseract → Azure fallback)");
            System.Console.WriteLine("   📊 Strategy: Execute Tesseract first (free, local)");
            System.Console.WriteLine("   📊 If confidence < threshold → Use Azure Vision (paid, high quality)");
            System.Console.WriteLine($"   🎚️  Threshold: {ocrOptions.Selector.UseAzureWhenTesseractConfidenceBelow:P0}");
            System.Console.WriteLine($"   🔄 Always execute both: {ocrOptions.Selector.AlwaysExecuteBoth}");

            // Validar Azure Vision configurado
            if (string.IsNullOrWhiteSpace(ocrOptions.AzureVision.Endpoint) ||
                string.IsNullOrWhiteSpace(ocrOptions.AzureVision.ApiKey))
            {
                System.Console.WriteLine("   ❌ ERROR: Azure Vision not configured!");
                System.Console.WriteLine("   💡 Selector requires Azure Vision as fallback.");
                System.Console.WriteLine("   💡 Configure OCR:AzureVision:Endpoint and OCR:AzureVision:ApiKey");
                throw new InvalidOperationException(
                    "Selector provider requires Azure Vision configuration. " +
                    "Please configure OCR:AzureVision:Endpoint and OCR:AzureVision:ApiKey in appsettings.json");
            }

            services.AddSingleton<LabelWise.Application.Interfaces.IOcrProvider>(sp =>
            {
                // Criar Tesseract provider
                var tesseractLogger = sp.GetService<ILogger<LabelWise.Infrastructure.Ocr.TesseractOcrProvider>>();
                var tesseractProvider = new LabelWise.Infrastructure.Ocr.TesseractOcrProvider(
                    tesseractLogger,
                    ocrOptions.TessdataPath,
                    ocrOptions.Language);

                System.Console.WriteLine($"   ✅ Tesseract Provider Created: {tesseractProvider.ProviderName}");

                // Criar Azure Vision provider
                var azureLogger = sp.GetService<ILogger<LabelWise.Infrastructure.Ocr.AzureVisionReadOcrProvider>>();
                var azureProvider = new LabelWise.Infrastructure.Ocr.AzureVisionReadOcrProvider(
                    ocrOptions.AzureVision.Endpoint,
                    ocrOptions.AzureVision.ApiKey,
                    ocrOptions.AzureVision.Language,
                    ocrOptions.AzureVision.TimeoutSeconds,
                    ocrOptions.AzureVision.EnableDetailedLogging,
                    azureLogger);

                System.Console.WriteLine($"   ✅ Azure Vision Provider Created: {azureProvider.ProviderName}");

                // Criar Selector
                var selectorLogger = sp.GetService<ILogger<LabelWise.Infrastructure.Ocr.OcrProviderSelector>>();
                var selector = new LabelWise.Infrastructure.Ocr.OcrProviderSelector(
                    tesseractProvider,
                    azureProvider,
                    ocrOptions.Selector.UseAzureWhenTesseractConfidenceBelow,
                    selectorLogger);

                selectorLogger?.LogInformation("🚀 IOcrProvider registered: {ProviderName}", selector.ProviderName);
                System.Console.WriteLine($"   ✅ Selector Instantiated: {selector.ProviderName}");

                // Validar se pelo menos um provider está disponível
                if (ocrOptions.ValidateOnStartup)
                {
                    var isAvailable = selector.IsAvailableAsync().Result;
                    System.Console.WriteLine($"   🔍 Validation: Available = {isAvailable}");

                    if (!isAvailable)
                    {
                        selectorLogger?.LogWarning("⚠️ Selector: Nenhum provider está disponível!");
                    }
                    else
                    {
                        System.Console.WriteLine("   ✅ Selector validated successfully!");
                      }
                }

                return selector;
            });
        }

        private static void ConfigureCompositeProvider(IServiceCollection services, OcrOptions ocrOptions)
        {
            System.Console.WriteLine("🔀 COMPOSITE PROVIDER SELECTED (Multi-provider with fallback)");
            System.Console.WriteLine($"   Primary: {ocrOptions.Composite.PrimaryProvider}");
            System.Console.WriteLine($"   Fallback: {ocrOptions.Composite.FallbackProvider}");
            System.Console.WriteLine($"   Confidence Threshold: {ocrOptions.Composite.ConfidenceThreshold:F2}");

            services.AddSingleton<LabelWise.Application.Interfaces.IOcrProvider>(sp =>
            {
                // Criar provider primário
                var primaryProvider = CreateProvider(
                    sp, 
                    ocrOptions.Composite.PrimaryProvider, 
                    ocrOptions);

                // Criar provider de fallback
                var fallbackProvider = CreateProvider(
                    sp, 
                    ocrOptions.Composite.FallbackProvider, 
                    ocrOptions);

                // Criar provider composto
                var logger = sp.GetService<ILogger<LabelWise.Infrastructure.Ocr.CompositeOcrProvider>>();
                var compositeProvider = new LabelWise.Infrastructure.Ocr.CompositeOcrProvider(
                    primaryProvider,
                    fallbackProvider,
                    ocrOptions.Composite.ConfidenceThreshold,
                    logger);

                logger?.LogInformation("🚀 CompositeOcrProvider registered");
                System.Console.WriteLine($"   ✅ Provider Instantiated: {compositeProvider.ProviderName}");

                return compositeProvider;
            });
        }

        private static LabelWise.Application.Interfaces.IOcrProvider CreateProvider(
            IServiceProvider serviceProvider,
            string providerType,
            OcrOptions ocrOptions)
        {
            if (providerType.Equals("Tesseract", StringComparison.OrdinalIgnoreCase))
            {
                var logger = serviceProvider.GetService<ILogger<LabelWise.Infrastructure.Ocr.TesseractOcrProvider>>();
                return new LabelWise.Infrastructure.Ocr.TesseractOcrProvider(
                    logger,
                    ocrOptions.TessdataPath,
                    ocrOptions.Language);
            }
            else if (providerType.Equals("AzureComputerVision", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(ocrOptions.Azure.Endpoint) ||
                    string.IsNullOrWhiteSpace(ocrOptions.Azure.ApiKey))
                {
                    throw new InvalidOperationException(
                        $"Cannot create {providerType} provider: Azure endpoint or API key not configured");
                }

                var logger = serviceProvider.GetService<ILogger<LabelWise.Infrastructure.Ocr.AzureComputerVisionOcrProvider>>();
                return new LabelWise.Infrastructure.Ocr.AzureComputerVisionOcrProvider(
                    ocrOptions.Azure.Endpoint!,
                    ocrOptions.Azure.ApiKey!,
                    logger);
            }
            else if (providerType.Equals("AzureVision", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(ocrOptions.AzureVision.Endpoint) ||
                    string.IsNullOrWhiteSpace(ocrOptions.AzureVision.ApiKey))
                {
                    throw new InvalidOperationException(
                        $"Cannot create {providerType} provider: Azure Vision endpoint or API key not configured");
                }

                var logger = serviceProvider.GetService<ILogger<LabelWise.Infrastructure.Ocr.AzureVisionReadOcrProvider>>();
                return new LabelWise.Infrastructure.Ocr.AzureVisionReadOcrProvider(
                    ocrOptions.AzureVision.Endpoint,
                    ocrOptions.AzureVision.ApiKey,
                    ocrOptions.AzureVision.Language,
                    ocrOptions.AzureVision.TimeoutSeconds,
                    ocrOptions.AzureVision.EnableDetailedLogging,
                    logger);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unknown provider type for composite: {providerType}. " +
                    $"Valid options: 'Tesseract', 'AzureComputerVision', 'AzureVision'");
            }
        }
    }
}
