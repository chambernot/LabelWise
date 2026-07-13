using LabelWise.Application.DTOs;
using LabelWise.Application.DTOs.FoodAnalysisTrust;
using LabelWise.Application.DTOs.IngredientAnalysis;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.IngredientAnalysis;
using LabelWise.Domain.Enums;
using LabelWise.Domain.Models;
using LabelWise.Infrastructure.Helpers;
using LabelWise.Infrastructure.Services.IngredientAnalysis;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services;

public sealed class IngredientAnalysisService : IIngredientAnalysisService
{
    private readonly IOpenAIIngredientAnalysisService _openAiIngredientAnalysisService;
    private readonly IOcrProvider _ocrProvider;
    private readonly IDocumentIntelligenceService _documentIntelligenceService;
    private readonly IngredientClassifier _ingredientClassifier;
    private readonly IngredientNormalizer _ingredientNormalizer;
    private readonly AllergenDetector _allergenDetector;
    private readonly ClaimDetector _claimDetector;
    private readonly FoodCompatibilityEngine _foodCompatibilityEngine;
    private readonly IngredientConfidenceEngine _confidenceEngine;
    private readonly ProcessingLevelClassifier _processingLevelClassifier;
    private readonly PositiveIngredientDetector _positiveIngredientDetector;
    private readonly AnalysisCompletenessEvaluator _analysisCompletenessEvaluator;
    private readonly IngredientSemanticEngine _semanticEngine;
    private readonly AssistantSummaryBuilder _assistantSummaryBuilder;
    private readonly OcrCorrectionEngine _ocrCorrectionEngine;
    private readonly OcrCleaningService _ocrCleaningService;
    private readonly SemanticReconstructionService _semanticReconstructionService;
    private readonly StructuredTextLayer _structuredTextLayer;
    private readonly RegulatoryClaimExtractor _regulatoryClaimExtractor;
    private readonly IngredientParserService _ingredientParserService;
    private readonly SemanticClaimResolver _semanticClaimResolver;
    private readonly SemanticSafetyValidator _semanticSafetyValidator;
    private readonly SemanticFoodConsolidator _semanticFoodConsolidator;
    private readonly FoodSemanticResponseBuilder _foodSemanticResponseBuilder;
    private readonly ProductionSafeFoodAnalysisEngine _productionSafeFoodAnalysisEngine;
    private readonly IngredientRecoveryEngine _ingredientRecoveryEngine;
    private readonly IngredientPresentationBuilder _presentationBuilder;
    private readonly IFoodAnalysisTrustEngine _trustEngine;
    private readonly FoodAnalysisQualityGate _qualityGate;
    private readonly IRegulatoryEngine _regulatoryEngine;
    private readonly IConflictResolutionEngine _conflictResolutionEngine;
    private readonly IDecisionEngine _decisionEngine;
    private readonly FoodOcrCleaningEngine _foodOcrCleaningEngine;
    private readonly FoodSemanticFinalizationEngine _foodSemanticFinalizationEngine;
    private readonly ILogger<IngredientAnalysisService> _logger;

    public IngredientAnalysisService(
        IOpenAIIngredientAnalysisService openAiIngredientAnalysisService,
        IOcrProvider ocrProvider,
        IDocumentIntelligenceService documentIntelligenceService,
        IngredientClassifier ingredientClassifier,
        IngredientNormalizer ingredientNormalizer,
        AllergenDetector allergenDetector,
        ClaimDetector claimDetector,
        FoodCompatibilityEngine foodCompatibilityEngine,
        IngredientConfidenceEngine confidenceEngine,
        ProcessingLevelClassifier processingLevelClassifier,
        PositiveIngredientDetector positiveIngredientDetector,
        AnalysisCompletenessEvaluator analysisCompletenessEvaluator,
        IngredientSemanticEngine semanticEngine,
        AssistantSummaryBuilder assistantSummaryBuilder,
        OcrCorrectionEngine ocrCorrectionEngine,
        OcrCleaningService ocrCleaningService,
        SemanticReconstructionService semanticReconstructionService,
        StructuredTextLayer structuredTextLayer,
        RegulatoryClaimExtractor regulatoryClaimExtractor,
        IngredientParserService ingredientParserService,
        SemanticClaimResolver semanticClaimResolver,
        SemanticSafetyValidator semanticSafetyValidator,
        SemanticFoodConsolidator semanticFoodConsolidator,
        FoodSemanticResponseBuilder foodSemanticResponseBuilder,
        ProductionSafeFoodAnalysisEngine productionSafeFoodAnalysisEngine,
        IngredientRecoveryEngine ingredientRecoveryEngine,
        IngredientPresentationBuilder presentationBuilder,
        IFoodAnalysisTrustEngine trustEngine,
        FoodAnalysisQualityGate qualityGate,
        IRegulatoryEngine regulatoryEngine,
        IConflictResolutionEngine conflictResolutionEngine,
        IDecisionEngine decisionEngine,
        FoodOcrCleaningEngine foodOcrCleaningEngine,
        FoodSemanticFinalizationEngine foodSemanticFinalizationEngine,
        ILogger<IngredientAnalysisService> logger)
    {
        _openAiIngredientAnalysisService = openAiIngredientAnalysisService;
        _ocrProvider = ocrProvider;
        _documentIntelligenceService = documentIntelligenceService;
        _ingredientClassifier = ingredientClassifier;
        _ingredientNormalizer = ingredientNormalizer;
        _allergenDetector = allergenDetector;
        _claimDetector = claimDetector;
        _foodCompatibilityEngine = foodCompatibilityEngine;
        _confidenceEngine = confidenceEngine;
        _processingLevelClassifier = processingLevelClassifier;
        _positiveIngredientDetector = positiveIngredientDetector;
        _analysisCompletenessEvaluator = analysisCompletenessEvaluator;
        _semanticEngine = semanticEngine;
        _assistantSummaryBuilder = assistantSummaryBuilder;
        _ocrCorrectionEngine = ocrCorrectionEngine;
        _ocrCleaningService = ocrCleaningService;
        _semanticReconstructionService = semanticReconstructionService;
        _structuredTextLayer = structuredTextLayer;
        _regulatoryClaimExtractor = regulatoryClaimExtractor;
        _ingredientParserService = ingredientParserService;
        _semanticClaimResolver = semanticClaimResolver;
        _semanticSafetyValidator = semanticSafetyValidator;
        _semanticFoodConsolidator = semanticFoodConsolidator;
        _foodSemanticResponseBuilder = foodSemanticResponseBuilder;
        _productionSafeFoodAnalysisEngine = productionSafeFoodAnalysisEngine;
        _ingredientRecoveryEngine = ingredientRecoveryEngine;
        _presentationBuilder = presentationBuilder;
        _trustEngine = trustEngine;
        _qualityGate = qualityGate;
        _regulatoryEngine = regulatoryEngine;
        _conflictResolutionEngine = conflictResolutionEngine;
        _decisionEngine = decisionEngine;
        _foodOcrCleaningEngine = foodOcrCleaningEngine;
        _foodSemanticFinalizationEngine = foodSemanticFinalizationEngine;
        _logger = logger;
    }

    public async Task<IngredientAnalysisResponse> AnalyzeAsync(
        byte[] imageBytes,
        string? mimeType,
        CancellationToken cancellationToken = default)
    {
        var response = new IngredientAnalysisResponse();
        var prepared = VisionImagePreparer.Prepare(imageBytes);
        var analysisBytes = prepared.Success ? prepared.Bytes : imageBytes;
        var analysisMimeType = prepared.Success ? prepared.MimeType : mimeType;

        if (!prepared.Success && !string.IsNullOrWhiteSpace(prepared.ErrorMessage))
            response.Warnings.Add(prepared.ErrorMessage);

        var ocrTextTask = ExtractOcrTextAsync(analysisBytes, analysisMimeType, cancellationToken);
        var documentTextTask = _documentIntelligenceService.ExtractTextAsync(analysisBytes, cancellationToken);

        await Task.WhenAll(ocrTextTask, documentTextTask);

        var ocrText = await ocrTextTask;
        var documentText = await documentTextTask;
        var ocrCorrection = _ocrCorrectionEngine.Correct(ocrText, documentText);
        ocrText = ocrCorrection.CorrectedOcrText;
        documentText = ocrCorrection.CorrectedDocumentIntelligenceText;
        var cleanedText = _ocrCleaningService.Clean(ocrText, documentText);
        var semanticText = _semanticReconstructionService.Reconstruct(cleanedText.Text);
        var structuredText = _structuredTextLayer.Build(semanticText.Text);
        var foodOcrCleaning = _foodOcrCleaningEngine.Clean(semanticText.Text);
        structuredText = foodOcrCleaning.StructuredText;
        var semanticCorrections = cleanedText.Corrections.Concat(semanticText.Corrections).Concat(foodOcrCleaning.Corrections).ToList();
        if (!string.IsNullOrWhiteSpace(foodOcrCleaning.Text))
        {
            ocrText = foodOcrCleaning.Text;
            documentText = null;
        }

        var combinedOcrContext = BuildOcrContext(ocrText, documentText);

        var openAiExtraction = await _openAiIngredientAnalysisService.AnalyzeAsync(
            analysisBytes,
            analysisMimeType,
            combinedOcrContext,
            cancellationToken);
        var openAiVisionUsed = openAiExtraction is not null;
        var visionExtraction = openAiExtraction ?? new IngredientExtractionResult
            {
                Warnings = ["Não foi possível usar a IA visual; resultado baseado apenas em OCR e regras."]
            };

        var context = new IngredientAnalysisContext
        {
            VisionExtraction = visionExtraction,
            OcrText = ocrText,
            DocumentIntelligenceText = documentText
        };

        var regulatoryClaims = _regulatoryClaimExtractor.Extract(structuredText);
        var ingredientParse = _ingredientParserService.ParseWithRecovery(context, structuredText, _ingredientRecoveryEngine);
        var ingredients = ingredientParse.Ingredients;
        var normalizedIngredients = ingredientParse.NormalizedIngredients;
        var productionValidation = new IngredientProductionValidator().Validate(ingredients, structuredText);
        if (productionValidation.SanitizedIngredients.Count > 0 &&
            !productionValidation.SanitizedIngredients.SequenceEqual(ingredients, StringComparer.OrdinalIgnoreCase))
        {
            ingredients = productionValidation.SanitizedIngredients;
            normalizedIngredients = _ingredientNormalizer.Normalize(ingredients);
        }

        var claims = regulatoryClaims.AllClaims
            .Where(claim => !string.IsNullOrWhiteSpace(claim))
            .GroupBy(IngredientTextNormalizer.Normalize)
            .Select(group => group.First())
            .ToList();
        var allergenRisks = _allergenDetector.DetectRisks(context, ingredients, claims);
        var resolvedClaims = _semanticClaimResolver.Resolve(claims, allergenRisks);
        claims = resolvedClaims.Claims;
        allergenRisks = resolvedClaims.AllergenRisks;
        var safety = _semanticSafetyValidator.Validate(claims, allergenRisks, structuredText);
        claims = safety.Claims;
        allergenRisks = safety.AllergenRisks;
        var claimsDetected = _claimDetector.Detect(claims);
        var allergens = allergenRisks
            .Where(risk => risk.RiskType == "contains")
            .Select(risk => risk.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var rawTextLength = (ocrText?.Length ?? 0) + (documentText?.Length ?? 0);
        var imageQualityConfidence = _confidenceEngine.EvaluateImageQuality(prepared.Success, rawTextLength);
        var sourceConflictCount = _confidenceEngine.CountConflicts(claims, allergenRisks);
        var classificationConfidence = _confidenceEngine.EvaluateOverall(
            openAiVisionUsed,
            !string.IsNullOrWhiteSpace(ocrText),
            !string.IsNullOrWhiteSpace(documentText),
            ingredients.Count,
            claims.Count,
            sourceConflictCount,
            imageQualityConfidence);
        var allWarnings = response.Warnings
            .Concat(visionExtraction.Warnings.Where(w => !string.IsNullOrWhiteSpace(w)))
            .Concat(productionValidation.Warnings)
            .ToList();
        var dietProfiles = _foodCompatibilityEngine.Evaluate(new FoodCompatibilityContext
        {
            Ingredients = ingredients,
            NormalizedIngredients = normalizedIngredients,
            Claims = claims,
            AllergenRisks = allergenRisks,
            RawOcrText = ocrText,
            NutritionContext = documentText,
            Warnings = allWarnings
        });
        var analysisCompleteness = _analysisCompletenessEvaluator.Evaluate(
            imageQualityConfidence,
            rawTextLength,
            ingredients.Count,
            claims.Count,
            allWarnings);
        ApplyRealisticConfidence(allergenRisks, analysisCompleteness);
        ApplyAnalysisCompleteness(dietProfiles, analysisCompleteness);
        var reliability = new ConfidenceCalibrationEngine().Evaluate(
            imageQualityConfidence,
            analysisCompleteness,
            ingredients,
            ocrCorrection.Corrections.Concat(semanticCorrections).ToList(),
            allWarnings,
            productionValidation.BoundaryLeakDetected,
            semanticText.Corrections.Count > 0);
        var overallConfidence = MinConfidence(CombineConfidence(imageQualityConfidence, classificationConfidence, analysisCompleteness.Status), reliability.Confidence);
        var processingLevel = _processingLevelClassifier.Evaluate(ingredients, normalizedIngredients, claims);
        var processingClassification = _processingLevelClassifier.Classify(ingredients, normalizedIngredients, claims);
        var ingredientContext = NutritionQualityEvaluator.BuildIngredientContext(ingredients, processingClassification.Level);
        var ingredientConfidence = BuildIngredientConfidence(
            ingredients,
            normalizedIngredients,
            analysisCompleteness,
            openAiVisionUsed,
            ocrText,
            documentText,
            ocrCorrection.Corrections.Concat(semanticCorrections).ToList(),
            productionValidation.BoundaryLeakDetected);
        var positiveIngredients = _positiveIngredientDetector.Detect(normalizedIngredients);
        var reasonSources = BuildReasonSources(openAiVisionUsed, ocrText, documentText, ingredients, claims, dietProfiles);
        var crossContaminationRisks = BuildCrossContaminationRisks(allergenRisks);
        var semanticProfile = _semanticEngine.Build(normalizedIngredients);
        var overallSemanticConfidence = MinConfidence(_confidenceEngine.EvaluateSemanticConfidence(
            imageQualityConfidence,
            classificationConfidence,
            analysisCompleteness.Status,
            ingredients.Count,
            claimsDetected.Count,
            allergenRisks.Count,
            sourceConflictCount), reliability.Confidence);
        semanticProfile.OverallSemanticConfidence = overallSemanticConfidence;

        response.Success = true;
        response.Message = ingredients.Count > 0 || claims.Count > 0 || allergens.Count > 0
            ? "Análise de ingredientes concluída."
            : "Nenhum ingrediente ou claim alimentar legível foi detectado.";
        response.ProductName = visionExtraction.ProductName;
        response.Brand = visionExtraction.Brand;
        response.CleanedSemanticText = semanticText.Text;
        response.IngredientsDetected = ingredients;
        response.Claims = claims;
        response.IngredientConfidence = ingredientConfidence;
        response.Allergens = allergens;
        response.NormalizedIngredients = normalizedIngredients;
        response.ClaimsDetected = claimsDetected;
        response.AllergenRisks = allergenRisks;
        response.DietProfiles = dietProfiles;
        response.ProcessingLevel = processingLevel;
        response.ProcessingClassification = processingClassification;
        response.IngredientContext = ingredientContext;
        response.PositiveIngredients = positiveIngredients;
        response.ReasonSources = reasonSources;
        response.AnalysisCompleteness = analysisCompleteness;
        response.CrossContaminationRisk = crossContaminationRisks;
        response.SemanticProfile = semanticProfile;
        response.AssistantSummary = _assistantSummaryBuilder.Build(dietProfiles, allergenRisks, claimsDetected, processingLevel, ingredientContext, ingredients, positiveIngredients, analysisCompleteness, overallSemanticConfidence);
        response.OcrCorrections = ocrCorrection.Corrections.Concat(semanticCorrections).ToList();
        response.StructuredTextBlocks = structuredText.Blocks;
        response.BlockConfidence = BuildBlockConfidenceSummary(structuredText, ingredients.Count, claimsDetected.Count, allergenRisks.Count);
        response.SemanticInferences = safety.SemanticInferences;
        response.Warnings.AddRange(visionExtraction.Warnings.Where(w => !string.IsNullOrWhiteSpace(w)));
        response.Warnings.AddRange(safety.Warnings);
        response.Warnings.AddRange(productionValidation.Warnings);
        response.Warnings.AddRange(reliability.Reasons);
        response.AnalysisQuality = ToAnalysisQuality(overallConfidence, analysisCompleteness.Status);
        response.SafeForPreciseNutritionAnalysis = analysisCompleteness.Status == "complete" && response.AnalysisQuality is "high" or "medium";
        response.RetryRecommended = !response.SafeForPreciseNutritionAnalysis;
        if (response.RetryRecommended)
            response.Recommendations.Add("A leitura parece parcial. Recomendamos nova foto mais próxima e alinhada.");
        response.TechnicalWarnings = response.Warnings
            .Where(warning => IngredientTextNormalizer.ContainsAny(warning, ["ocr", "parcial", "reflexo", "desfoc", "ilegível", "ilegiv", "fronteira", "confiança", "confianca"]))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        _semanticFoodConsolidator.Consolidate(response, dietProfiles, allergenRisks, claimsDetected);
        response.Diagnostics.OpenAiVisionUsed = openAiVisionUsed;
        response.Diagnostics.OcrProviderUsed = !string.IsNullOrWhiteSpace(ocrText);
        response.Diagnostics.DocumentIntelligenceUsed = !string.IsNullOrWhiteSpace(documentText);
        response.Diagnostics.RawTextLength = rawTextLength;
        response.Diagnostics.OverallConfidence = overallConfidence;
        response.Diagnostics.OcrConfidence = imageQualityConfidence;
        response.Diagnostics.ClassificationConfidence = classificationConfidence;
        response.Diagnostics.ImageQualityConfidence = imageQualityConfidence;
        response.Diagnostics.OverallSemanticConfidence = overallSemanticConfidence;
        response.Diagnostics.SourceConflictCount = sourceConflictCount;
        _foodSemanticResponseBuilder.Apply(response);
        _productionSafeFoodAnalysisEngine.Apply(response);
        var trust = _trustEngine.Evaluate(BuildTrustInput(
            response,
            structuredText,
            imageQualityConfidence,
            sourceConflictCount,
            ocrCorrection.Corrections.Count + semanticCorrections.Count,
            productionValidation.BoundaryLeakDetected));
        _qualityGate.Apply(response, trust);
        var presentation = _presentationBuilder.Build(response);
        response.SummaryCards = presentation.SummaryCards;
        response.QuickInsights = presentation.QuickInsights;
        response.QuickFlags = presentation.QuickFlags;
        response.OverallFoodRating = presentation.OverallFoodRating;
        response.PresentationSummary = presentation.PresentationSummary;
        response.UnifiedFoodAnalysis = _presentationBuilder.BuildUnified(response);
        var centralDecision = await BuildCentralFoodDecisionAsync(response, structuredText, cancellationToken);
        ApplyCentralDecision(response, centralDecision);
        _foodSemanticFinalizationEngine.Apply(response, structuredText, BuildOcrContext(ocrText, documentText) ?? string.Empty, foodOcrCleaning.Traces);
        ApplyMobileFirstContract(response);

        _logger.LogInformation(
            "[IngredientAnalysis.Diagnostics] ProcessingRules={ProcessingRules}; AllergenRules={AllergenRules}; Claims={Claims}",
            string.Join(" | ", response.ProcessingClassification.Reasons),
            string.Join(" | ", response.AllergenRisks.Select(risk => $"{risk.RiskType}:{risk.Name}:{risk.Source}")),
            string.Join(" | ", response.ClaimsDetected.Select(claim => $"{claim.Type}:{claim.Text}")));

        return response;
    }

    private static void ApplyMobileFirstContract(IngredientAnalysisResponse response)
    {
        var sanitizedRaw = response.IngredientsDetected
            .Select(MobileIngredientSanitizer.Sanitize)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sanitizedNormalized = response.NormalizedIngredients
            .Where(item => !string.IsNullOrWhiteSpace(item.Raw) || !string.IsNullOrWhiteSpace(item.Normalized))
            .Select(item =>
            {
                var sanitized = new IngredientNormalizedDto
                {
                    Raw = MobileIngredientSanitizer.Sanitize(item.Raw),
                    Normalized = MobileIngredientSanitizer.Sanitize(item.Normalized),
                    Category = item.Category,
                    Confidence = item.Confidence,
                    Source = item.Source,
                    DetectionType = item.DetectionType,
                    SemanticEvidence = item.SemanticEvidence,
                    AnimalOrigin = item.AnimalOrigin,
                    DietaryRisk = item.DietaryRisk
                };
                return sanitized;
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Raw) || !string.IsNullOrWhiteSpace(item.Normalized))
            .GroupBy(item => IngredientTextNormalizer.Normalize(string.IsNullOrWhiteSpace(item.Normalized) ? item.Raw : item.Normalized))
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .Select(group => group.First())
            .ToList();

        response.Product = new MobileProductDto
        {
            Name = NullIfWhiteSpace(response.ProductName),
            Brand = NullIfWhiteSpace(response.Brand),
            Category = ResolveMobileProductCategory(sanitizedNormalized),
            ProcessingLevel = ResolveMobileProcessingLevel(response.ProcessingClassification.Level, sanitizedNormalized)
        };

        response.Ingredients = new MobileIngredientsDto
        {
            Raw = sanitizedRaw,
            Normalized = sanitizedNormalized
                .Select(ToMobileIngredient)
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        // Propagate sanitization back to the underlying lists so debug/diagnostic surfaces
        // never leak scientific/transgenic terms either.
        response.IngredientsDetected = sanitizedRaw;
        response.NormalizedIngredients = sanitizedNormalized;

        var allergyMap = BuildMobileAllergies(response.AllergenRisks);

        response.Compatibility = new MobileCompatibilityDto
        {
            Vegan = ReconcileProfile(ToMobileCompatibility(response.DietProfiles.Vegan), "vegan", sanitizedRaw, sanitizedNormalized, allergyMap),
            Vegetarian = ReconcileProfile(ToMobileCompatibility(response.DietProfiles.Vegetarian), "vegetarian", sanitizedRaw, sanitizedNormalized, allergyMap),
            GlutenFree = ReconcileProfile(ToMobileCompatibility(response.DietProfiles.GlutenFree), "gluten_free", sanitizedRaw, sanitizedNormalized, allergyMap),
            LactoseFree = ReconcileProfile(ToMobileCompatibility(response.DietProfiles.LactoseFree), "lactose_free", sanitizedRaw, sanitizedNormalized, allergyMap),
            Allergies = allergyMap
        };

        response.Analysis = new MobileAnalysisDto
        {
            OcrConfidence = ToPercent(response.Diagnostics.OcrConfidence),
            IngredientBlockConfidence = ToPercent(response.BlockConfidence.IngredientConfidence),
            PartialReading = DeterminePartialReading(response),
            SafeMode = DetermineSafeMode(response),
            ImageQuality = ToImageQuality(response.Diagnostics.ImageQualityConfidence)
        };

        response.Alerts = BuildMobileAlerts(response.AllergenRisks);
        response.UserExperience = BuildUserExperience(response.Compatibility, response.Alerts, response.Product);
    }

    private static MobileProfileCompatibilityDto ReconcileProfile(
        MobileProfileCompatibilityDto profile,
        string profileKey,
        IReadOnlyList<string> rawIngredients,
        IReadOnlyList<IngredientNormalizedDto> normalizedIngredients,
        IReadOnlyDictionary<string, MobileAllergyRiskDto> allergies)
    {
        var realIncompatible = HasRealIncompatibleIngredient(profileKey, rawIngredients, normalizedIngredients, allergies, out var reason);
        var hasCrossContaminationOrAttention = HasCrossContaminationOrUncertainty(profileKey, allergies);

        if (realIncompatible)
        {
            profile.Compatible = false;
            profile.Status = "unsafe";
            if (!string.IsNullOrWhiteSpace(reason))
                profile.Summary = reason;
            else if (string.IsNullOrWhiteSpace(profile.Summary))
                profile.Summary = "Ingrediente incompatível detectado.";
            return profile;
        }

        // No real incompatible ingredient — block any false-positive "unsafe"
        if (profile.Status == "unsafe" || profile.Compatible == false)
        {
            profile.Compatible = true;
            profile.Status = hasCrossContaminationOrAttention ? "attention" : "safe";
            profile.Summary = hasCrossContaminationOrAttention
                ? BuildAttentionSummary(profileKey)
                : BuildSafeSummary(profileKey);
            // Purge reasons that described a now-invalid incompatibility to avoid
            // showing contradictory messages like "Contém mel" alongside compatible=true.
            profile.Reasons = profile.Reasons
                .Where(r => !IsIncompatibilityReason(r))
                .ToList();
        }
        else if (hasCrossContaminationOrAttention && profile.Status == "safe")
        {
            profile.Status = "attention";
            if (string.IsNullOrWhiteSpace(profile.Summary))
                profile.Summary = BuildAttentionSummary(profileKey);
        }

        // Confidence-driven downgrade: "safe" requires >= 80 confidence.
        if (profile.Status == "safe" && profile.Confidence < 80)
        {
            profile.Status = "attention";
            // Override summary to reflect confidence limitation, not a false incompatibility signal.
            profile.Summary = BuildConfidenceAttentionSummary(profileKey);
        }

        if (string.IsNullOrWhiteSpace(profile.Summary))
            profile.Summary = profile.Status switch
            {
                "safe" => BuildSafeSummary(profileKey),
                "attention" => BuildAttentionSummary(profileKey),
                _ => string.Empty
            };

        return profile;
    }

    private static bool HasRealIncompatibleIngredient(
        string profileKey,
        IReadOnlyList<string> rawIngredients,
        IReadOnlyList<IngredientNormalizedDto> normalizedIngredients,
        IReadOnlyDictionary<string, MobileAllergyRiskDto> allergies,
        out string reason)
    {
        reason = string.Empty;

        bool ContainsAllergen(string key) =>
            allergies.TryGetValue(key, out var entry) && entry.Risk == "contains";

        bool AnyNameMatch(IEnumerable<string> tokens)
        {
            // Use word-boundary protection for short tokens (≤4 chars) to prevent false-positive
            // substring hits: "mel" inside "vermelho", "ovo" inside "couve", "boi" inside "boinhas".
            return tokens.Any(token =>
            {
                var normalizedToken = IngredientTextNormalizer.Normalize(token);
                if (normalizedToken.Length <= 4)
                {
                    var pattern = $@"(?<![\p{{L}}]){System.Text.RegularExpressions.Regex.Escape(normalizedToken)}(?![\p{{L}}])";
                    return rawIngredients.Any(name =>
                            System.Text.RegularExpressions.Regex.IsMatch(IngredientTextNormalizer.Normalize(name), pattern, System.Text.RegularExpressions.RegexOptions.CultureInvariant)) ||
                        normalizedIngredients.Any(item =>
                            System.Text.RegularExpressions.Regex.IsMatch(IngredientTextNormalizer.Normalize(item.Normalized), pattern, System.Text.RegularExpressions.RegexOptions.CultureInvariant) ||
                            System.Text.RegularExpressions.Regex.IsMatch(IngredientTextNormalizer.Normalize(item.Raw), pattern, System.Text.RegularExpressions.RegexOptions.CultureInvariant));
                }

                return rawIngredients.Any(name => IngredientTextNormalizer.ContainsAny(name, [token])) ||
                    normalizedIngredients.Any(item =>
                        IngredientTextNormalizer.ContainsAny(item.Normalized, [token]) ||
                        IngredientTextNormalizer.ContainsAny(item.Raw, [token]));
            });
        }

        switch (profileKey)
        {
            case "vegan":
                if (ContainsAllergen("milk")) { reason = "Contém leite ou derivados."; return true; }
                if (ContainsAllergen("egg")) { reason = "Contém ovo."; return true; }
                if (ContainsAllergen("fish")) { reason = "Contém peixe."; return true; }
                if (ContainsAllergen("crustaceans")) { reason = "Contém crustáceo."; return true; }
                if (AnyNameMatch(["carne", "frango", "boi", "porco", "gelatina", "whey", "caseina", "caseína", "mel", "leite", "ovo", "peixe"]))
                {
                    reason = "Ingrediente de origem animal detectado.";
                    return true;
                }
                if (normalizedIngredients.Any(item => item.AnimalOrigin))
                {
                    reason = "Ingrediente de origem animal detectado.";
                    return true;
                }
                return false;

            case "vegetarian":
                if (ContainsAllergen("fish")) { reason = "Contém peixe."; return true; }
                if (ContainsAllergen("crustaceans")) { reason = "Contém crustáceo."; return true; }
                if (AnyNameMatch(["carne", "frango", "boi", "porco", "peixe", "gelatina", "atum", "sardinha"]))
                {
                    reason = "Contém carne, peixe ou gelatina.";
                    return true;
                }
                return false;

            case "lactose_free":
                if (ContainsAllergen("milk")) { reason = "Contém leite ou derivados."; return true; }
                if (AnyNameMatch(["leite", "lactose", "caseina", "caseína", "whey", "soro de leite", "manteiga", "queijo", "iogurte"]))
                {
                    reason = "Contém ingrediente lácteo.";
                    return true;
                }
                return false;

            case "gluten_free":
                if (ContainsAllergen("gluten")) { reason = "Contém glúten."; return true; }
                if (AnyNameMatch(["trigo", "cevada", "centeio", "malte", "gluten", "glúten", "farinha de trigo"]))
                {
                    reason = "Contém farinha de trigo ou derivado com glúten.";
                    return true;
                }
                return false;
        }

        return false;
    }

    private static bool HasCrossContaminationOrUncertainty(string profileKey, IReadOnlyDictionary<string, MobileAllergyRiskDto> allergies)
    {
        bool MayContain(string key) =>
            allergies.TryGetValue(key, out var entry) && entry.Risk == "cross_contamination";

        return profileKey switch
        {
            "vegan" => MayContain("milk") || MayContain("egg") || MayContain("fish") || MayContain("crustaceans"),
            "vegetarian" => MayContain("fish") || MayContain("crustaceans"),
            "lactose_free" => MayContain("milk"),
            "gluten_free" => MayContain("gluten"),
            _ => false
        };
    }

    private static bool IsIncompatibilityReason(string reason)
    {
        var n = IngredientTextNormalizer.Normalize(reason);
        return n.Contains("ingrediente de origem animal", StringComparison.Ordinal) ||
               n.Contains("sinal de ingrediente animal", StringComparison.Ordinal) ||
               n.Contains("compatibilidade vegana nao e segura", StringComparison.Ordinal) ||
               n.Contains("contem ingrediente de origem animal", StringComparison.Ordinal) ||
               n.Contains("contem mel", StringComparison.Ordinal) ||
               n.Contains("contem ovo", StringComparison.Ordinal) ||
               n.Contains("contem leite", StringComparison.Ordinal) ||
               n.Contains("contem peixe", StringComparison.Ordinal) ||
               n.Contains("contem carne", StringComparison.Ordinal) ||
               n.Contains("contem gelatina", StringComparison.Ordinal);
    }

    private static string BuildConfidenceAttentionSummary(string profileKey) => profileKey switch
    {
        "vegan" => "Sem ingredientes animais identificados, mas a leitura parcial impede confirmação total.",
        "vegetarian" => "Sem carne ou peixe identificados, mas a leitura parcial impede confirmação total.",
        "lactose_free" => "Sem ingredientes lácteos identificados, mas a leitura parcial impede confirmação total.",
        "gluten_free" => "Sem fontes de glúten identificadas, mas a leitura parcial impede confirmação total.",
        _ => "Compatibilidade não pôde ser confirmada com a leitura atual."
    };

    private static string BuildSafeSummary(string profileKey) => profileKey switch
    {
        "vegan" => "Nenhum ingrediente de origem animal identificado.",
        "vegetarian" => "Sem carne, peixe ou gelatina identificados.",
        "lactose_free" => "Sem ingredientes lácteos identificados.",
        "gluten_free" => "Sem fontes comuns de glúten identificadas.",
        _ => "Compatível com base na lista visível."
    };

    private static string BuildAttentionSummary(string profileKey) => profileKey switch
    {
        "vegan" => "Nenhum ingrediente animal identificado, mas há alerta de possível contaminação cruzada.",
        "vegetarian" => "Sem carne ou peixe identificados, mas há alerta de possível contaminação cruzada.",
        "lactose_free" => "Sem lactose identificada, mas há alerta de possível contaminação cruzada com leite.",
        "gluten_free" => "Sem fontes diretas de glúten, mas há alerta de possível contaminação cruzada.",
        _ => "Compatibilidade requer atenção."
    };

    private static MobileAlertsDto BuildMobileAlerts(IEnumerable<AllergenRiskDto> allergenRisks)
    {
        var contains = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var mayContain = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var risk in allergenRisks.Where(risk => !string.IsNullOrWhiteSpace(risk.Name)))
        {
            var key = NormalizeAllergyKey(risk.Name);
            var label = AllergenDisplayLabel(key, risk.Name);

            if (risk.RiskType == "contains")
                contains[key] = label;
            else if (risk.RiskType is "may_contain" or "cross_contamination" && !contains.ContainsKey(key))
                mayContain[key] = label;
        }

        return new MobileAlertsDto
        {
            Contains = contains.Values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
            MayContain = mayContain.Values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private static string AllergenDisplayLabel(string normalizedKey, string fallback) => normalizedKey switch
    {
        "milk" => "leite",
        "gluten" => "glúten",
        "peanut" => "amendoim",
        "tree_nuts" => "castanhas",
        "soy" => "soja",
        "egg" => "ovo",
        "fish" => "peixe",
        "crustaceans" => "crustáceos",
        _ => string.IsNullOrWhiteSpace(fallback) ? normalizedKey.Replace('_', ' ') : fallback.Trim().ToLowerInvariant()
    };

    private static MobileUserExperienceDto BuildUserExperience(
        MobileCompatibilityDto compatibility,
        MobileAlertsDto alerts,
        MobileProductDto product)
    {
        var badges = new List<string>();

        // Profile badges respect priority: unsafe > attention > safe; only ONE badge per profile.
        AddProfileBadge(badges, "Vegano", compatibility.Vegan);
        AddProfileBadge(badges, "Vegetariano", compatibility.Vegetarian);
        AddProfileBadge(badges, "Sem glúten", compatibility.GlutenFree);
        AddProfileBadge(badges, "Sem lactose", compatibility.LactoseFree);

        foreach (var allergen in alerts.Contains)
            badges.Add($"❌ Contém {allergen}");

        foreach (var allergen in alerts.MayContain)
            badges.Add($"⚠ Pode conter {allergen}");

        if (product.ProcessingLevel == "ultra_processed")
            badges.Add("⚠ Ultraprocessado");

        return new MobileUserExperienceDto
        {
            Badges = badges.Distinct(StringComparer.Ordinal).ToList(),
            Summary = BuildUserExperienceSummary(alerts, product, compatibility)
        };
    }

    private static void AddProfileBadge(List<string> badges, string label, MobileProfileCompatibilityDto profile)
    {
        var lower = label.ToLowerInvariant();

        switch (profile.Status)
        {
            case "unsafe":
                // Priority: when unsafe we never emit "safe" or "attention" for this profile.
                badges.Add($"❌ Incompatível com {lower}");
                break;
            case "attention":
                badges.Add($"⚠ {label} com atenção");
                break;
            case "safe":
                badges.Add($"✅ {label}");
                break;
            // "unknown" → no badge to avoid UX noise.
        }
    }

    private static string BuildUserExperienceSummary(
        MobileAlertsDto alerts,
        MobileProductDto product,
        MobileCompatibilityDto compatibility)
    {
        var sentences = new List<string>();

        var prefix = product.ProcessingLevel == "ultra_processed" ? "Produto ultraprocessado" : "Produto";

        if (alerts.Contains.Count > 0)
            sentences.Add($"{prefix} contendo {JoinHumanized(alerts.Contains.Take(4).ToList())}.");
        else if (product.ProcessingLevel == "ultra_processed")
            sentences.Add($"{prefix}.");

        if (alerts.MayContain.Count > 0)
            sentences.Add($"Pode conter {JoinHumanized(alerts.MayContain.Take(4).ToList())}.");

        // Only emit "Vegano com atenção" when there is a real cross-contamination signal for
        // animal allergens — not when attention is triggered purely by low OCR confidence.
        var veganCrossContamination = compatibility.Allergies.Any(kv =>
            kv.Key is "milk" or "egg" or "fish" or "crustaceans" &&
            kv.Value.Risk == "cross_contamination");
        if (compatibility.Vegan.Status == "attention" &&
            alerts.Contains.All(a => a != "leite" && a != "ovo") &&
            veganCrossContamination)
            sentences.Add("Vegano com atenção.");

        if (sentences.Count == 0)
            return "Sem alertas relevantes na lista de ingredientes visível.";

        return string.Join(" ", sentences);
    }

    private static string JoinHumanized(IReadOnlyList<string> values)
    {
        if (values.Count == 0) return string.Empty;
        if (values.Count == 1) return values[0];
        if (values.Count == 2) return $"{values[0]} e {values[1]}";
        return string.Join(", ", values.Take(values.Count - 1)) + " e " + values[^1];
    }

    private static bool DeterminePartialReading(IngredientAnalysisResponse response)
    {
        var ocrPercent = ToPercent(response.Diagnostics.OcrConfidence);

        if (response.AnalysisCompleteness.Status == "insufficient")
            return true;

        if (response.AnalysisCompleteness.Status == "partial")
            return true;

        if (ocrPercent < 90 && (response.IngredientsDetected.Count == 0 || response.BlockConfidence.IngredientConfidence == "low"))
            return true;

        return false;
    }

    private static bool DetermineSafeMode(IngredientAnalysisResponse response)
    {
        var ocrPercent = ToPercent(response.Diagnostics.OcrConfidence);

        // Safe mode triggers when OCR is below 70%
        if (ocrPercent < 70)
            return true;

        // Or when completeness is explicitly insufficient (not just partial)
        if (response.AnalysisCompleteness.Status == "insufficient")
            return true;

        // Do NOT trigger safe mode just because ProductionSafeModeApplied is true
        // That flag is set by low confidence in different context
        // With high OCR (>90%) and good blocks, we should NOT be in safe mode

        return false;
    }

    private static MobileIngredientDto ToMobileIngredient(IngredientNormalizedDto item)
    {
        var category = string.IsNullOrWhiteSpace(item.Category) ? "unknown" : item.Category;
        return new MobileIngredientDto
        {
            Name = string.IsNullOrWhiteSpace(item.Normalized) ? item.Raw : item.Normalized,
            Type = category,
            RiskLevel = ResolveIngredientRiskLevel(item),
            Category = category
        };
    }

    private static string ResolveIngredientRiskLevel(IngredientNormalizedDto item)
    {
        if (item.AnimalOrigin || item.DietaryRisk is "gluten" or "lactose" or "nuts")
            return "high";

        if (IngredientDictionary.UltraProcessingCategories.Contains(item.Category, StringComparer.OrdinalIgnoreCase) ||
            item.Category is "artificial_sweetener" or "preservative" or "emulsifier")
            return "moderate";

        return item.Confidence == "low" ? "unknown" : "low";
    }

    private static MobileProfileCompatibilityDto ToMobileCompatibility(DietProfileCompatibilityDto profile)
    {
        var distinctReasons = profile.Reasons
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();

        // Warnings must not duplicate reasons or each other
        var distinctWarnings = profile.Warnings
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Where(w => !distinctReasons.Any(r =>
                r.Equals(w, StringComparison.OrdinalIgnoreCase) ||
                r.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                w.Contains(r, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        return new()
        {
            Compatible = profile.Compatible && (profile.CompatibilityStatus is DietCompatibilityStatuses.ConfirmedCompatible or DietCompatibilityStatuses.LikelyCompatible),
            Status = ToMobileStatus(profile),
            Confidence = ToPercent(profile.Confidence),
            Summary = BuildProfileSummary(profile),
            Reasons = distinctReasons,
            Warnings = distinctWarnings
        };
    }

    private static string BuildProfileSummary(DietProfileCompatibilityDto profile)
    {
        var reason = profile.Reasons.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (!string.IsNullOrWhiteSpace(reason))
            return reason.Trim();

        var warning = profile.Warnings.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (!string.IsNullOrWhiteSpace(warning))
            return warning.Trim();

        return profile.CompatibilityStatus switch
        {
            DietCompatibilityStatuses.ConfirmedCompatible => "Compatibilidade confirmada por claim visível.",
            DietCompatibilityStatuses.LikelyCompatible => "Sem ingredientes incompatíveis detectados na lista visível.",
            DietCompatibilityStatuses.LikelyNotCompatible => "Evidência sugere incompatibilidade.",
            DietCompatibilityStatuses.NotCompatible => "Ingrediente incompatível confirmado.",
            DietCompatibilityStatuses.Attention => "Atenção: pode conter ingrediente sensível.",
            _ => string.Empty
        };
    }

    private static string ToMobileStatus(DietProfileCompatibilityDto profile)
    {
        if (profile.CompatibilityStatus == DietCompatibilityStatuses.NotCompatible)
            return "unsafe";

        if (profile.CompatibilityStatus is DietCompatibilityStatuses.LikelyNotCompatible or DietCompatibilityStatuses.Attention)
            return "attention";

        if (profile.CompatibilityStatus == DietCompatibilityStatuses.Uncertain)
            return profile.Warnings.Count > 0 ? "attention" : "unknown";

        return profile.Compatible ? "safe" : "unknown";
    }

    private static Dictionary<string, MobileAllergyRiskDto> BuildMobileAllergies(IEnumerable<AllergenRiskDto> allergenRisks)
    {
        var result = new Dictionary<string, MobileAllergyRiskDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var risk in allergenRisks
            .Where(risk => !string.IsNullOrWhiteSpace(risk.Name))
            .GroupBy(risk => NormalizeAllergyKey(risk.Name), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(item => AllergyRiskRank(item.RiskType)).First()))
        {
            result[NormalizeAllergyKey(risk.Name)] = new MobileAllergyRiskDto
            {
                Safe = false,
                Risk = risk.RiskType is "may_contain" or "cross_contamination" ? "cross_contamination" : "contains",
                Severity = risk.RiskType is "may_contain" or "cross_contamination"
                    ? "medium"
                    : risk.AllergenSeverity.RegulatoryLevel,
                Source = risk.Source
            };
        }

        return result;
    }

    private static string NormalizeAllergyKey(string value)
    {
        var normalized = IngredientTextNormalizer.Normalize(value);
        if (IngredientTextNormalizer.ContainsAny(normalized, ["leite", "lactose", "caseina", "whey"])) return "milk";
        if (IngredientTextNormalizer.ContainsAny(normalized, ["gluten", "trigo", "cevada", "centeio", "malte"])) return "gluten";
        if (IngredientTextNormalizer.ContainsAny(normalized, ["amendoim"])) return "peanut";
        if (IngredientTextNormalizer.ContainsAny(normalized, ["castanha", "nozes", "amendoa", "avela", "macadamia"])) return "tree_nuts";
        if (IngredientTextNormalizer.ContainsAny(normalized, ["soja"])) return "soy";
        if (IngredientTextNormalizer.ContainsAny(normalized, ["ovo"])) return "egg";
        if (IngredientTextNormalizer.ContainsAny(normalized, ["peixe"])) return "fish";
        if (IngredientTextNormalizer.ContainsAny(normalized, ["crustaceo", "camarao"])) return "crustaceans";
        return normalized.Replace(' ', '_');
    }

    private static int AllergyRiskRank(string riskType) => riskType switch
    {
        "contains" => 0,
        "may_contain" => 1,
        "cross_contamination" => 2,
        _ => 3
    };

    private static string ResolveMobileProcessingLevel(string classificationLevel, IEnumerable<IngredientNormalizedDto> normalizedIngredients)
    {
        if (classificationLevel == "ultra_processed")
            return "ultra_processed";

        var ingredientList = normalizedIngredients.ToList();
        var categories = ingredientList.Select(item => item.Category).Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        var names = ingredientList.Select(item => string.IsNullOrWhiteSpace(item.Normalized) ? item.Raw : item.Normalized).ToList();

        // Ultra-processed markers: artificial additives, sweeteners, preservatives
        if (categories.Any(cat => cat.Contains("sweetener", StringComparison.OrdinalIgnoreCase) ||
                                   cat.Contains("preservative", StringComparison.OrdinalIgnoreCase) ||
                                   cat.Contains("emulsifier", StringComparison.OrdinalIgnoreCase)) ||
            names.Any(name => IngredientTextNormalizer.ContainsAny(name,
                ["sucralose", "ciclamato", "sacarina", "sorbitol", "aspartame", "acesulfame",
                 "benzoico", "metilparabeno", "sorbato", "nitrito", "nitrato"])))
        {
            return "ultra_processed";
        }

        // If classification engine already resolved a valid level, use it
        if (!string.IsNullOrWhiteSpace(classificationLevel) && classificationLevel != "unknown")
            return classificationLevel;

        // Infer from ingredient categories when classification returned unknown
        var naturalCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "vegetable", "herb", "seasoning", "condiment", "fat" };
        var processedMarkers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "flavor_enhancer", "colorant", "acidulant", "stabilizer" };

        var total = categories.Count;
        if (total == 0)
            return "unknown";

        var naturalCount = categories.Count(c => naturalCategories.Contains(c));
        var processedCount = categories.Count(c => processedMarkers.Contains(c));

        // All or nearly all natural → minimally_processed
        if (naturalCount >= Math.Ceiling(total * 0.8) && processedCount == 0)
            return "minimally_processed";

        // Mix of natural + flavor enhancers (like glutamato) → processed
        if (naturalCount > 0 && processedCount > 0)
            return "processed";

        return "unknown";
    }

    private static string ResolveMobileProductCategory(IEnumerable<IngredientNormalizedDto> normalizedIngredients)
    {
        var categories = normalizedIngredients.Select(item => item.Category).Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        var names = normalizedIngredients.Select(item => string.IsNullOrWhiteSpace(item.Normalized) ? item.Raw : item.Normalized).ToList();

        if (categories.Any(category => category.Contains("sweetener", StringComparison.OrdinalIgnoreCase)) ||
            names.Any(name => IngredientTextNormalizer.ContainsAny(name, ["sucralose", "ciclamato", "sacarina", "sorbitol", "aspartame", "estévia", "estevia", "poliol"])))
            return "sweetener";

        if (categories.Any(category => category.Contains("dairy", StringComparison.OrdinalIgnoreCase) || category.Contains("milk", StringComparison.OrdinalIgnoreCase)))
            return "dairy";

        if (categories.Any(category => category.Contains("cereal", StringComparison.OrdinalIgnoreCase) || category.Contains("gluten", StringComparison.OrdinalIgnoreCase)))
            return "cereal";

        // Seasoning/condiment: majority of ingredients are herbs, vegetables, seasonings or condiments
        var seasoningCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "seasoning", "herb", "vegetable", "condiment", "flavor_enhancer", "fat" };
        var total = categories.Count;
        var seasoningCount = categories.Count(c => seasoningCategories.Contains(c));
        if (total > 0 && seasoningCount >= Math.Ceiling(total * 0.5))
            return "seasoning";

        return "unknown";
    }

    private static int ToPercent(string? confidence) => confidence switch
    {
        "very_high" => 98,
        "high" => 95,
        "medium" => 75,
        "low" => 45,
        "very_low" => 20,
        _ => 0
    };

    private static string ToImageQuality(string? confidence) => confidence switch
    {
        "high" or "very_high" => "good",
        "medium" => "fair",
        "low" or "very_low" => "poor",
        _ => "unknown"
    };

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<FoodDecision> BuildCentralFoodDecisionAsync(
        IngredientAnalysisResponse response,
        StructuredTextDocument structuredText,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var blocks = structuredText.Blocks.Select(block => block.Text).Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        var regulatoryText = string.Join("\n", response.Claims.Concat(response.ClaimsDetected.Select(claim => claim.Text)).Concat(blocks));
        var regulatoryClaims = await _regulatoryEngine.DetectClaimsAsync(regulatoryText, blocks);
        var explicitIngredients = response.IngredientsDetected
            .Select(ingredient => new Evidence
            {
                Type = "ingredient_confirmed",
                Text = ingredient,
                Source = "ingredient_list",
                Priority = EvidencePriority.IngredientExplicit,
                Confidence = ToConfidenceScore(response.IngredientConfidence.FirstOrDefault(item => string.Equals(item.Ingredient, ingredient, StringComparison.OrdinalIgnoreCase))?.Confidence),
                OriginBlock = "IngredientList"
            })
            .ToList();
        var semanticInferences = response.SemanticInferences
            .Select(inference => new Evidence
            {
                Type = inference.Type,
                Text = inference.Text,
                Source = inference.Source,
                Priority = EvidencePriority.SemanticInference,
                Confidence = ToConfidenceScore(inference.Confidence),
                OriginBlock = inference.OriginBlock
            })
            .ToList();
        var conflicts = _conflictResolutionEngine.DetectConflicts(regulatoryClaims, explicitIngredients, semanticInferences);
        var conflictQuality = _conflictResolutionEngine.EvaluateAnalysisQuality(conflicts);
        var inputQuality = conflictQuality == LabelWise.Domain.Enums.AnalysisQuality.Reliable
            ? ToDecisionAnalysisQuality(response.AnalysisQuality, response.AnalysisCompleteness.Status)
            : conflictQuality;

        return await _decisionEngine.MakeDecisionAsync(new DecisionInput
        {
            RegulatoryInformation = regulatoryClaims,
            ExplicitIngredients = explicitIngredients,
            SemanticInferences = semanticInferences,
            Conflicts = conflicts,
            AnalysisQuality = inputQuality,
            RequestedProfiles = ["gluten_free", "lactose_free", "vegan", "vegetarian", "diabetic", "hypertension", "weight_loss", "muscle_gain", "child", "low_carb", "keto"]
        });
    }

    private static void ApplyCentralDecision(IngredientAnalysisResponse response, FoodDecision decision)
    {
        response.CriticalAlerts = decision.Alerts.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        response.Warnings.AddRange(decision.Warnings.Where(warning => !string.IsNullOrWhiteSpace(warning)));
        response.Warnings = response.Warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        response.Recommendations = response.Recommendations
            .Concat(decision.Recommendations.Select(recommendation => recommendation.Text))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        response.SummaryCards = decision.SummaryCards.Select(ToSummaryCardDto).ToList();
        response.QuickInsights = decision.QuickInsights.Select(ToQuickInsightDto).ToList();
        response.PresentationHints = decision.PresentationHints.Select(ToPresentationHintDto).ToList();
        response.ProcessingClassification = new ProcessingClassificationDto
        {
            Level = ToProcessingLevelValue(decision.ProcessingLevel),
            Confidence = decision.OverallConfidence >= 0.70 ? "high" : decision.OverallConfidence >= 0.45 ? "medium" : "low",
            ProcessingScore = decision.ProcessingScore,
            Reasons = decision.Warnings.Take(4).ToList()
        };
        response.ProcessingLevel = new ProcessingLevelDto
        {
            Value = response.ProcessingClassification.Level,
            Confidence = response.ProcessingClassification.Confidence,
            ProcessingScore = response.ProcessingClassification.ProcessingScore,
            Reasons = response.ProcessingClassification.Reasons
        };
        response.AssistantSummary = new AssistantSummaryDto
        {
            Text = decision.AssistantSummary,
            Confidence = response.ProcessingClassification.Confidence,
            Highlights = decision.QuickInsights.Select(insight => insight.Text).Take(3).ToList(),
            Warnings = decision.Warnings.Take(3).ToList(),
            EvidenceTypes = [EvidenceType.IngredientDetected]
        };
        response.OverallFoodRating = new OverallFoodRatingDto
        {
            Level = decision.Quality == LabelWise.Domain.Enums.AnalysisQuality.Reliable ? "consolidated" : "preliminary",
            Label = decision.Quality == LabelWise.Domain.Enums.AnalysisQuality.Reliable ? "Análise consolidada" : "Resultado preliminar",
            Reasons = decision.Warnings.Take(3).DefaultIfEmpty(decision.FoodClassification).ToList()
        };
        response.PresentationSummary = new PresentationSummaryDto
        {
            Title = decision.Alerts.Count > 0 ? "Atenção ao rótulo" : "Análise consolidada",
            Subtitle = decision.AssistantSummary,
            Highlight = decision.Alerts.FirstOrDefault() ?? "Sem alerta crítico detectado."
        };
        response.UnifiedFoodAnalysis = new UnifiedFoodAnalysisResponse
        {
            ProductName = response.ProductName,
            Brand = response.Brand,
            SummaryCards = response.SummaryCards,
            QuickInsights = response.QuickInsights,
            QuickFlags = response.QuickFlags,
            ProcessingClassification = response.ProcessingClassification,
            IngredientContext = response.IngredientContext,
            OverallFoodRating = response.OverallFoodRating,
            PresentationSummary = response.PresentationSummary,
            OcrCorrections = response.OcrCorrections,
            IngredientConfidence = response.IngredientConfidence,
            IngredientAnalysis = response.UnifiedFoodAnalysis.IngredientAnalysis
        };
    }

    private static SummaryCardDto ToSummaryCardDto(SummaryCard card) =>
        new()
        {
            Type = card.Title,
            Status = card.Severity,
            Label = card.Subtitle,
            Icon = card.Icon,
            PresentationHint = ToPresentationHintDto(card.PresentationHint)
        };

    private static QuickInsightDto ToQuickInsightDto(QuickInsight insight) =>
        new()
        {
            Type = insight.Type,
            Text = insight.Text,
            Severity = insight.Severity,
            Icon = insight.Severity == "critical" ? "alert" : "info"
        };

    private static PresentationHintDto ToPresentationHintDto(PresentationHint hint) =>
        new()
        {
            Severity = hint.Severity,
            DisplayMode = hint.DisplayMode,
            Highlight = hint.Highlight,
            Priority = hint.Priority,
            UiStyle = hint.UiStyle
        };

    private static string ToProcessingLevelValue(LabelWise.Domain.Enums.ProcessingLevel level) => level switch
    {
        LabelWise.Domain.Enums.ProcessingLevel.MinimallyProcessed => "minimally_processed",
        LabelWise.Domain.Enums.ProcessingLevel.ProcessedCulinaryIngredients => "processed_culinary_ingredient",
        LabelWise.Domain.Enums.ProcessingLevel.Processed => "processed",
        LabelWise.Domain.Enums.ProcessingLevel.UltraProcessed => "ultra_processed",
        _ => "unknown"
    };

    private static LabelWise.Domain.Enums.AnalysisQuality ToDecisionAnalysisQuality(string analysisQuality, string completenessStatus)
    {
        if (completenessStatus == "insufficient" || analysisQuality == "unreliable")
            return LabelWise.Domain.Enums.AnalysisQuality.Insufficient;

        if (completenessStatus == "partial" || analysisQuality == "low")
            return LabelWise.Domain.Enums.AnalysisQuality.Partial;

        return LabelWise.Domain.Enums.AnalysisQuality.Reliable;
    }

    private static double ToConfidenceScore(string? confidence) => confidence switch
    {
        "high" => 0.90,
        "medium" => 0.70,
        "low" => 0.45,
        "very_low" => 0.20,
        _ => 0.60
    };

    private static FoodAnalysisTrustInput BuildTrustInput(
        IngredientAnalysisResponse response,
        StructuredTextDocument structuredText,
        string ocrConfidence,
        int sourceConflictCount,
        int ocrCorrectionCount,
        bool boundaryLeakDetected)
    {
        var blocks = structuredText.Blocks;
        var lowIngredientCount = response.IngredientConfidence.Count(item => item.Confidence is "low" or "very_low");
        var inferredCount = response.SemanticInferences.Count + response.InferredFacts.Count + response.AllergenRisks.Count(risk => risk.TrustLevel is EvidenceTrustLevel.SemanticInference or EvidenceTrustLevel.WeakInference);
        var textConflictCount = response.Warnings.Count(warning => IngredientTextNormalizer.ContainsAny(warning, ["conflito", "inconsist", "mistura", "fronteira"]));

        return new FoodAnalysisTrustInput
        {
            Source = "ingredient_analysis",
            OcrConfidence = ocrConfidence,
            PartialRead = response.AnalysisCompleteness.Status == "insufficient" ||
                (response.AnalysisCompleteness.Status == "partial" && response.IngredientsDetected.Count == 0 && response.ClaimsDetected.Count == 0),
            IngredientRegionDetected = blocks.Any(block => block.RegionType == TextRegionType.IngredientList),
            NutritionRegionDetected = blocks.Any(block => block.RegionType == TextRegionType.NutritionTable),
            RegulatoryClaimRegionDetected = blocks.Any(block => block.RegionType is TextRegionType.RegulatoryClaim or TextRegionType.AllergenBlock),
            IngredientCompletenessLow = response.IngredientsDetected.Count == 0 ||
                (response.AnalysisCompleteness.Status == "insufficient" && lowIngredientCount > Math.Max(1, response.IngredientConfidence.Count / 2)),
            ParsingBroken = response.IngredientsDetected.Count == 0 && response.Claims.Count == 0 && response.Allergens.Count == 0,
            BoundaryLeakDetected = boundaryLeakDetected,
            DuplicatedOcr = HasDuplicatedOcr(response.CleanedSemanticText),
            TextConsistencyConflicts = textConflictCount,
            IngredientConsistencyConflicts = response.Diagnostics.SourceConflictCount,
            SemanticConflictCount = sourceConflictCount,
            InferredDataCount = inferredCount,
            ExplicitClaimCount = response.ClaimsDetected.Count(claim => claim.EvidenceTypes.Contains(EvidenceType.ClaimDetected)),
            OcrCorrectionCount = ocrCorrectionCount,
            IngredientCount = response.IngredientsDetected.Count,
            LowConfidenceIngredientCount = lowIngredientCount,
            Warnings = response.Warnings.ToList()
        };
    }

    private static bool HasDuplicatedOcr(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var lines = value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(IngredientTextNormalizer.Normalize)
            .Where(line => line.Length > 12)
            .ToList();

        return lines.Count > 3 && lines.GroupBy(line => line, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() >= 3);
    }

    private static BlockConfidenceSummaryDto BuildBlockConfidenceSummary(
        StructuredTextDocument structuredText,
        int ingredientCount,
        int claimCount,
        int allergenRiskCount)
    {
        var blocks = structuredText.Blocks;
        var ingredientConfidence = ResolveBlockConfidence(blocks, TextRegionType.IngredientList, ingredientCount > 0);
        var claimsConfidence = ResolveBlockConfidence(blocks, TextRegionType.RegulatoryClaim, claimCount > 0);
        var allergenConfidence = ResolveBlockConfidence(blocks, TextRegionType.AllergenBlock, allergenRiskCount > 0 || claimCount > 0);
        var nutritionConfidence = ResolveBlockConfidence(blocks, TextRegionType.NutritionTable, blocks.Any(block => block.RegionType == TextRegionType.NutritionTable));

        return new BlockConfidenceSummaryDto
        {
            IngredientConfidence = ingredientConfidence,
            NutritionConfidence = nutritionConfidence,
            ClaimsConfidence = claimsConfidence,
            AllergenConfidence = allergenConfidence,
            Reasons = BuildBlockConfidenceReasons(blocks, ingredientCount, claimCount).ToList()
        };
    }

    private static string ResolveBlockConfidence(IEnumerable<StructuredTextBlockDto> blocks, TextRegionType regionType, bool hasEvidence)
    {
        var regionBlocks = blocks.Where(block => block.RegionType == regionType).ToList();
        if (regionBlocks.Count == 0)
            return hasEvidence ? "medium" : "low";

        if (!hasEvidence && regionType is TextRegionType.IngredientList or TextRegionType.RegulatoryClaim or TextRegionType.AllergenBlock)
            return "medium";

        return regionBlocks.Any(block => block.Confidence == "high") || hasEvidence ? "high" : "medium";
    }

    private static IEnumerable<string> BuildBlockConfidenceReasons(IReadOnlyList<StructuredTextBlockDto> blocks, int ingredientCount, int claimCount)
    {
        if (blocks.Any(block => block.RegionType == TextRegionType.IngredientList))
            yield return $"Bloco de ingredientes isolado com {ingredientCount} itens detectados.";
        if (blocks.Any(block => block.RegionType is TextRegionType.RegulatoryClaim or TextRegionType.AllergenBlock))
            yield return $"Claims/alergênicos avaliados separadamente com {claimCount} claims regulatórios.";
        if (blocks.Any(block => block.RegionType == TextRegionType.NutritionTable))
            yield return "Tabela nutricional mantida em bloco próprio para evitar contaminação dos ingredientes.";
    }

    private async Task<string?> ExtractOcrTextAsync(byte[] imageBytes, string? mimeType, CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"labelwise-ingredients-{Guid.NewGuid():N}.jpg");

        try
        {
            await File.WriteAllBytesAsync(tempPath, imageBytes, cancellationToken);
            var result = await _ocrProvider.ExtractTextAsync(new OcrRequestDto
            {
                ImagePath = tempPath,
                FileName = Path.GetFileName(tempPath),
                ContentType = string.IsNullOrWhiteSpace(mimeType) ? "image/jpeg" : mimeType
            });

            return result.Success && !string.IsNullOrWhiteSpace(result.RawText)
                ? result.RawText.Trim()
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[IngredientAnalysis] Falha ao executar OCR reutilizável.");
            return null;
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[IngredientAnalysis] Falha ao remover arquivo temporário de OCR.");
            }
        }
    }

    private static string? BuildOcrContext(string? ocrText, string? documentText)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(ocrText))
            parts.Add($"OCR existente:\n{ocrText}");
        if (!string.IsNullOrWhiteSpace(documentText))
            parts.Add($"Azure Document Intelligence:\n{documentText}");

        return parts.Count == 0 ? null : string.Join("\n\n", parts);
    }

    private static void ApplyAnalysisCompleteness(DietProfilesDto dietProfiles, AnalysisCompletenessDto completeness)
    {
        if (completeness.Status == "complete")
            return;

        foreach (var profile in GetDietProfiles(dietProfiles))
        {
            if (profile.CompatibilityStatus is DietCompatibilityStatuses.NotCompatible or DietCompatibilityStatuses.LikelyNotCompatible or DietCompatibilityStatuses.Attention)
                continue;

            // Profiles already resolved by explicit regulatory evidence (cross-contamination claims,
            // confirmed allergen blocks) should not be further degraded by analysis completeness —
            // the evidence is authoritative regardless of ingredient list size.
            var hasRegulatoryEvidence = profile.EvidenceTypes.Any(e => e is EvidenceType.ClaimDetected or EvidenceType.CrossContamination);
            if (hasRegulatoryEvidence)
                continue;

            profile.CompatibilityStatus = DietCompatibilityStatuses.Uncertain;
            profile.Confidence = DowngradeConfidence(profile.Confidence);
            profile.Warnings.Add("Análise parcial: ausência de detecção não garante compatibilidade absoluta.");
            if (!profile.ReasonSources.Contains("ocr_text", StringComparer.OrdinalIgnoreCase))
                profile.ReasonSources.Add("ocr_text");
        }
    }

    private static IEnumerable<DietProfileCompatibilityDto> GetDietProfiles(DietProfilesDto dietProfiles)
    {
        yield return dietProfiles.Vegan;
        yield return dietProfiles.Vegetarian;
        yield return dietProfiles.LactoseFree;
        yield return dietProfiles.GlutenFree;
        yield return dietProfiles.DiabeticFriendly;
    }

    private static string DowngradeConfidence(string confidence) =>
        confidence == "high" ? "medium" : "low";

    private static string CombineConfidence(string ocrConfidence, string classificationConfidence, string completenessStatus)
    {
        if (completenessStatus == "insufficient")
            return "low";

        if (completenessStatus == "partial" || ocrConfidence == "low")
            return classificationConfidence == "high" ? "medium" : "low";

        return MinConfidence(ocrConfidence, classificationConfidence);
    }

    private static string MinConfidence(string first, string second) =>
        Score(first) <= Score(second) ? first : second;

    private static string ToAnalysisQuality(string confidence, string completenessStatus)
    {
        if (completenessStatus == "insufficient") return "unreliable";
        if (completenessStatus == "partial") return confidence == "low" ? "low" : "medium";
        return confidence == "high" ? "high" : confidence == "medium" ? "medium" : "low";
    }

    private static int Score(string confidence) =>
        confidence switch
        {
            "high" => 3,
            "medium" => 2,
            "low" => 1,
            "very_low" => 0,
            _ => 1
        };

    private static void ApplyRealisticConfidence(IReadOnlyList<AllergenRiskDto> allergenRisks, AnalysisCompletenessDto analysisCompleteness)
    {
        if (analysisCompleteness.Status == "complete")
            return;

        foreach (var risk in allergenRisks)
        {
            if (risk.Confidence == "high")
                risk.Confidence = "medium";

            if (risk.AllergenSeverity.RegulatoryLevel == "low")
                risk.Confidence = risk.Confidence == "high" ? "medium" : risk.Confidence;
        }
    }

    private static List<IngredientConfidenceDto> BuildIngredientConfidence(
        IReadOnlyList<string> ingredients,
        IReadOnlyList<IngredientNormalizedDto> normalizedIngredients,
        AnalysisCompletenessDto analysisCompleteness,
        bool openAiVisionUsed,
        string? ocrText,
        string? documentText,
        IReadOnlyList<OcrCorrectionDto> corrections,
        bool boundaryLeakDetected)
    {
        var calibration = new ConfidenceCalibrationEngine();
        var normalizedByRaw = normalizedIngredients
            .GroupBy(item => IngredientTextNormalizer.Normalize(item.Raw))
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return ingredients
            .Select(ingredient =>
            {
                var reasons = new List<string>();
                var score = 0;
                var normalized = IngredientTextNormalizer.Normalize(ingredient);

                if (openAiVisionUsed)
                {
                    score += 35;
                    reasons.Add("Detectado pela IA visual.");
                }

                if (!string.IsNullOrWhiteSpace(ocrText) && IngredientTextNormalizer.ContainsAny(ocrText, [ingredient]))
                {
                    score += 25;
                    reasons.Add("Confirmado no OCR.");
                }

                if (!string.IsNullOrWhiteSpace(documentText) && IngredientTextNormalizer.ContainsAny(documentText, [ingredient]))
                {
                    score += 25;
                    reasons.Add("Confirmado pelo Document Intelligence.");
                }

                if (normalizedByRaw.TryGetValue(normalized, out var normalizedIngredient) && normalizedIngredient.Confidence == "high")
                    score += 15;

                var repaired = WasIngredientRepaired(ingredient, corrections, ocrText, documentText);
                if (repaired)
                {
                    score -= 15;
                    reasons.Add("Ingrediente depende de correção OCR alimentar.");
                }

                if (analysisCompleteness.Status == "partial")
                {
                    score -= 20;
                    reasons.Add("Leitura parcial reduz a confiança.");
                }
                else if (analysisCompleteness.Status == "insufficient")
                {
                    score -= 40;
                    reasons.Add("Leitura insuficiente reduz a confiança.");
                }

                var evidenceTypes = BuildIngredientEvidenceTypes(openAiVisionUsed, ocrText, documentText, ingredient);
                var rawConfidence = score >= 70 ? "high" : score >= 45 ? "medium" : "low";
                var inferred = !evidenceTypes.Contains(EvidenceType.IngredientDetected) && evidenceTypes.Contains(EvidenceType.OpenAiInference);
                var partial = analysisCompleteness.Status != "complete";

                return new IngredientConfidenceDto
                {
                    Ingredient = ingredient,
                    Confidence = calibration.ApplyCeiling(ApplyIngredientConfidenceCeiling(rawConfidence, evidenceTypes, analysisCompleteness), repaired, inferred, partial, boundaryLeakDetected),
                    Reasons = reasons.DefaultIfEmpty("Ingrediente detectado por heurística semântica.").Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    EvidenceType = EvidenceType.IngredientDetected,
                    EvidenceTypes = evidenceTypes,
                    TrustLevel = evidenceTypes.Contains(EvidenceType.IngredientDetected) ? EvidenceTrustLevel.ExplicitIngredient : EvidenceTrustLevel.SemanticInference
                };
            })
            .ToList();
    }

    private static bool WasIngredientRepaired(string ingredient, IReadOnlyList<OcrCorrectionDto> corrections, params string?[] sources)
    {
        if (corrections.Count == 0)
            return false;

        var normalizedIngredient = IngredientTextNormalizer.Normalize(ingredient);
        if (corrections.Any(correction =>
            !string.IsNullOrWhiteSpace(correction.Corrected) &&
            normalizedIngredient.Contains(IngredientTextNormalizer.Normalize(correction.Corrected), StringComparison.Ordinal)))
        {
            return true;
        }

        var sourceText = IngredientTextNormalizer.Normalize(string.Join(" ", sources.Where(source => !string.IsNullOrWhiteSpace(source))));
        return corrections.Any(correction =>
            !string.IsNullOrWhiteSpace(correction.Original) &&
            sourceText.Contains(IngredientTextNormalizer.Normalize(correction.Original), StringComparison.Ordinal) &&
            !sourceText.Contains(normalizedIngredient, StringComparison.Ordinal));
    }

    private static string ApplyIngredientConfidenceCeiling(string confidence, IReadOnlyList<EvidenceType> evidenceTypes, AnalysisCompletenessDto analysisCompleteness)
    {
        var ceiling = evidenceTypes.Contains(EvidenceType.IngredientDetected) ? "medium" : "low";
        if (analysisCompleteness.Status != "complete")
            ceiling = "low";

        return Score(confidence) <= Score(ceiling) ? confidence : ceiling;
    }

    private static List<EvidenceType> BuildIngredientEvidenceTypes(bool openAiVisionUsed, string? ocrText, string? documentText, string ingredient)
    {
        var evidenceTypes = new List<EvidenceType>();
        if (!string.IsNullOrWhiteSpace(ocrText) && IngredientTextNormalizer.ContainsAny(ocrText, [ingredient]))
            evidenceTypes.Add(EvidenceType.IngredientDetected);
        if (!string.IsNullOrWhiteSpace(documentText) && IngredientTextNormalizer.ContainsAny(documentText, [ingredient]))
            evidenceTypes.Add(EvidenceType.IngredientDetected);
        if (openAiVisionUsed)
            evidenceTypes.Add(EvidenceType.OpenAiInference);

        return evidenceTypes.Distinct().DefaultIfEmpty(EvidenceType.IngredientDetected).ToList();
    }

    private static List<string> BuildReasonSources(
        bool openAiVisionUsed,
        string? ocrText,
        string? documentText,
        IReadOnlyList<string> ingredients,
        IReadOnlyList<string> claims,
        DietProfilesDto dietProfiles)
    {
        var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (openAiVisionUsed) sources.Add("openai_inference");
        if (!string.IsNullOrWhiteSpace(ocrText) || !string.IsNullOrWhiteSpace(documentText)) sources.Add("ocr_text");
        if (ingredients.Count > 0) sources.Add("ingredient_detected");
        if (claims.Count > 0) sources.Add("claim_detected");

        foreach (var source in GetDietProfiles(dietProfiles).SelectMany(profile => profile.ReasonSources))
            sources.Add(source);

        return sources.OrderBy(source => source, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<CrossContaminationRiskDto> BuildCrossContaminationRisks(IReadOnlyList<AllergenRiskDto> allergenRisks) =>
        allergenRisks
            .Where(risk => risk.RiskType is "may_contain" or "cross_contamination")
            .Select(risk => new CrossContaminationRiskDto
            {
                Allergen = risk.Name,
                Risk = "cross_contamination",
                Severity = risk.AllergenSeverity.RegulatoryLevel == "low" ? "low" : "medium",
                Evidence = risk.Evidence,
                EvidenceType = EvidenceType.CrossContamination,
                EvidenceTypes = risk.EvidenceTypes.Count > 0 ? risk.EvidenceTypes : [EvidenceType.CrossContamination]
            })
            .ToList();
}
