using System.Text.Json.Serialization;
using LabelWise.Application.DTOs.FoodAnalysisTrust;

namespace LabelWise.Application.DTOs.IngredientAnalysis;

public sealed class IngredientAnalysisResponse
{
    [JsonPropertyName("product")]
    public MobileProductDto Product { get; set; } = new();
    [JsonPropertyName("ingredients")]
    public MobileIngredientsDto Ingredients { get; set; } = new();
    [JsonPropertyName("compatibility")]
    public MobileCompatibilityDto Compatibility { get; set; } = new();
    [JsonPropertyName("analysis")]
    public MobileAnalysisDto Analysis { get; set; } = new();
    [JsonPropertyName("alerts")]
    public MobileAlertsDto Alerts { get; set; } = new();
    [JsonPropertyName("userExperience")]
    public MobileUserExperienceDto UserExperience { get; set; } = new();

    [JsonIgnore]
    public bool Success { get; set; }
    [JsonIgnore]
    public string? Message { get; set; }
    [JsonIgnore]
    public string? ProductName { get; set; }
    [JsonIgnore]
    public string? Brand { get; set; }
    [JsonIgnore]
    public string CleanedSemanticText { get; set; } = string.Empty;
    [JsonIgnore]
    public List<string> IngredientsDetected { get; set; } = new();
    [JsonIgnore]
    public List<string> Allergens { get; set; } = new();
    [JsonIgnore]
    public List<string> Claims { get; set; } = new();
    [JsonIgnore]
    public List<IngredientConfidenceDto> IngredientConfidence { get; set; } = new();
    [JsonIgnore]
    public List<IngredientNormalizedDto> NormalizedIngredients { get; set; } = new();
    [JsonIgnore]
    public List<AllergenRiskDto> AllergenRisks { get; set; } = new();
    [JsonIgnore]
    public List<ClaimDetectionDto> ClaimsDetected { get; set; } = new();
    [JsonIgnore]
    public DietProfilesDto DietProfiles { get; set; } = new();
    [JsonIgnore]
    public ProcessingLevelDto ProcessingLevel { get; set; } = new();
    [JsonIgnore]
    public ProcessingClassificationDto ProcessingClassification { get; set; } = new();
    [JsonIgnore]
    public LabelWise.Application.DTOs.Nutrition.IngredientContextDto IngredientContext { get; set; } = new();
    [JsonIgnore]
    public List<PositiveIngredientDto> PositiveIngredients { get; set; } = new();
    [JsonIgnore]
    public List<string> ReasonSources { get; set; } = new();
    [JsonIgnore]
    public AnalysisCompletenessDto AnalysisCompleteness { get; set; } = new();
    [JsonIgnore]
    public List<CrossContaminationRiskDto> CrossContaminationRisk { get; set; } = new();
    [JsonIgnore]
    public IngredientSemanticProfileDto SemanticProfile { get; set; } = new();
    [JsonIgnore]
    public FutureUserProfileSupportDto FutureUserProfileSupport { get; set; } = new();
    [JsonIgnore]
    public AssistantSummaryDto AssistantSummary { get; set; } = new();
    [JsonIgnore]
    public List<SummaryCardDto> SummaryCards { get; set; } = new();
    [JsonIgnore]
    public List<QuickInsightDto> QuickInsights { get; set; } = new();
    [JsonIgnore]
    public List<QuickFlagDto> QuickFlags { get; set; } = new();
    [JsonIgnore]
    public List<PresentationHintDto> PresentationHints { get; set; } = new();
    [JsonIgnore]
    public OverallFoodRatingDto OverallFoodRating { get; set; } = new();
    [JsonIgnore]
    public PresentationSummaryDto PresentationSummary { get; set; } = new();
    [JsonIgnore]
    public List<OcrCorrectionDto> OcrCorrections { get; set; } = new();
    [JsonIgnore]
    public List<StructuredTextBlockDto> StructuredTextBlocks { get; set; } = new();
    [JsonIgnore]
    public BlockConfidenceSummaryDto BlockConfidence { get; set; } = new();
    [JsonIgnore]
    public List<SemanticInferenceDto> SemanticInferences { get; set; } = new();
    [JsonIgnore]
    public List<FoodSemanticFactDto> ConfirmedFacts { get; set; } = new();
    [JsonIgnore]
    public List<FoodSemanticFactDto> InferredFacts { get; set; } = new();
    [JsonIgnore]
    public List<string> CriticalAlerts { get; set; } = new();
    [JsonIgnore]
    public ConfidenceSummaryDto ConfidenceSummary { get; set; } = new();
    [JsonIgnore]
    public ProcessingAnalysisDto ProcessingAnalysis { get; set; } = new();
    [JsonIgnore]
    public NutritionAnalysisSafetyDto NutritionAnalysis { get; set; } = new();
    [JsonIgnore]
    public CategorizedIngredientsDto CategorizedIngredients { get; set; } = new();
    [JsonIgnore]
    public bool ProductionSafeModeApplied { get; set; }
    [JsonIgnore]
    public FoodAnalysisTrustReport Trust { get; set; } = new();
    [JsonIgnore]
    public UnifiedFoodAnalysisResponse UnifiedFoodAnalysis { get; set; } = new();
    [JsonIgnore]
    public List<StructuredFoodEntityDto> StructuredFoodEntities { get; set; } = new();
    [JsonIgnore]
    public UnifiedSemanticStateDto UnifiedSemanticState { get; set; } = new();
    [JsonIgnore]
    public FoodPublicResponseDto PublicResponse { get; set; } = new();
    [JsonIgnore]
    public FoodDebugResponseDto DebugResponse { get; set; } = new();
    [JsonIgnore]
    public List<string> Warnings { get; set; } = new();
    [JsonIgnore]
    public string AnalysisQuality { get; set; } = "low";
    [JsonIgnore]
    public bool SafeForPreciseNutritionAnalysis { get; set; }
    [JsonIgnore]
    public bool RetryRecommended { get; set; }
    [JsonIgnore]
    public List<string> Recommendations { get; set; } = new();
    [JsonIgnore]
    public List<string> TechnicalWarnings { get; set; } = new();
    [JsonIgnore]
    public IngredientAnalysisDiagnosticsDto Diagnostics { get; set; } = new();
}

public sealed class MobileProductDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("brand")]
    public string? Brand { get; set; }
    [JsonPropertyName("category")]
    public string Category { get; set; } = "unknown";
    [JsonPropertyName("processingLevel")]
    public string ProcessingLevel { get; set; } = "unknown";
}

public sealed class MobileIngredientsDto
{
    [JsonPropertyName("raw")]
    public List<string> Raw { get; set; } = new();
    [JsonPropertyName("normalized")]
    public List<MobileIngredientDto> Normalized { get; set; } = new();
}

public sealed class MobileIngredientDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("type")]
    public string Type { get; set; } = "unknown";
    [JsonPropertyName("riskLevel")]
    public string RiskLevel { get; set; } = "unknown";
    [JsonPropertyName("category")]
    public string Category { get; set; } = "unknown";
}

public sealed class MobileCompatibilityDto
{
    [JsonPropertyName("vegan")]
    public MobileProfileCompatibilityDto Vegan { get; set; } = new();
    [JsonPropertyName("vegetarian")]
    public MobileProfileCompatibilityDto Vegetarian { get; set; } = new();
    [JsonPropertyName("glutenFree")]
    public MobileProfileCompatibilityDto GlutenFree { get; set; } = new();
    [JsonPropertyName("lactoseFree")]
    public MobileProfileCompatibilityDto LactoseFree { get; set; } = new();
    [JsonPropertyName("allergies")]
    public Dictionary<string, MobileAllergyRiskDto> Allergies { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MobileProfileCompatibilityDto
{
    [JsonPropertyName("compatible")]
    public bool Compatible { get; set; }
    [JsonPropertyName("status")]
    public string Status { get; set; } = "unknown";
    [JsonPropertyName("confidence")]
    public int Confidence { get; set; }
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
    [JsonPropertyName("reasons")]
    public List<string> Reasons { get; set; } = new();
    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();
}

public sealed class MobileAlertsDto
{
    [JsonPropertyName("contains")]
    public List<string> Contains { get; set; } = new();
    [JsonPropertyName("mayContain")]
    public List<string> MayContain { get; set; } = new();
}

public sealed class MobileUserExperienceDto
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
    [JsonPropertyName("badges")]
    public List<string> Badges { get; set; } = new();
}

public sealed class MobileAllergyRiskDto
{
    [JsonPropertyName("safe")]
    public bool Safe { get; set; }
    [JsonPropertyName("risk")]
    public string Risk { get; set; } = "unknown";
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "low";
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;
}

public sealed class MobileAnalysisDto
{
    [JsonPropertyName("ocrConfidence")]
    public int OcrConfidence { get; set; }
    [JsonPropertyName("ingredientBlockConfidence")]
    public int IngredientBlockConfidence { get; set; }
    [JsonPropertyName("partialReading")]
    public bool PartialReading { get; set; }
    [JsonPropertyName("safeMode")]
    public bool SafeMode { get; set; }
    [JsonPropertyName("imageQuality")]
    public string ImageQuality { get; set; } = "unknown";
}

public sealed class StructuredTextBlockDto
{
    public string Type { get; set; } = "UnknownBlock";
    public string SemanticRegion
    {
        get => RegionType switch
        {
            TextRegionType.IngredientList => "INGREDIENTS_BLOCK",
            TextRegionType.AllergenBlock => "ALLERGEN_BLOCK",
            TextRegionType.RegulatoryClaim => "CLAIMS_BLOCK",
            TextRegionType.NutritionTable => "NUTRITION_BLOCK",
            TextRegionType.ManufacturerInfo or TextRegionType.StorageInfo => "FOOTER_BLOCK",
            TextRegionType.MarketingText => "MARKETING_BLOCK",
            _ => _semanticRegion
        };
        set => _semanticRegion = value;
    }

    private string _semanticRegion = "UNKNOWN_BLOCK";
    public TextRegionType RegionType { get; set; } = TextRegionType.Unknown;
    public string Text { get; set; } = string.Empty;
    public string Source { get; set; } = "ocr";
    public string Confidence { get; set; } = "low";
}

public sealed class BlockConfidenceSummaryDto
{
    public string IngredientConfidence { get; set; } = "low";
    public string NutritionConfidence { get; set; } = "low";
    public string ClaimsConfidence { get; set; } = "low";
    public string AllergenConfidence { get; set; } = "low";
    public List<string> Reasons { get; set; } = new();
}

public enum TextRegionType
{
    NutritionTable,
    IngredientList,
    AllergenBlock,
    RegulatoryClaim,
    ManufacturerInfo,
    StorageInfo,
    MarketingText,
    Unknown
}

public sealed class FoodSemanticFactDto
{
    public string Text { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Confidence { get; set; } = "low";
    public string DetectionType { get; set; } = "suspected";
    public List<SemanticEvidenceDto> SemanticEvidence { get; set; } = new();
}

public sealed class ConfidenceSummaryDto
{
    public string GlobalConfidence { get; set; } = "low";
    public string OcrConfidence { get; set; } = "low";
    public string Completeness { get; set; } = "insufficient";
    public bool IsPartialReading { get; set; } = true;
    public bool BlocksAbsoluteConclusions { get; set; } = true;
    public bool ScoreIsEstimated { get; set; } = true;
    public List<string> Reasons { get; set; } = new();
}

public sealed class ProcessingAnalysisDto
{
    public string Level { get; set; } = "unknown";
    public string Confidence { get; set; } = "low";
    public int ProcessingScore { get; set; }
    public List<string> IndustrialSignals { get; set; } = new();
    public List<string> Reasons { get; set; } = new();
}

public sealed class NutritionAnalysisSafetyDto
{
    public bool DefinitiveScoreAllowed { get; set; }
    public bool ScoreEstimated { get; set; } = true;
    public string Status { get; set; } = "resultado_preliminar";
    public List<string> Warnings { get; set; } = new();
}

public sealed class CategorizedIngredientsDto
{
    public List<string> Dairy { get; set; } = new();
    public List<string> Gluten { get; set; } = new();
    public List<string> Nuts { get; set; } = new();
    public List<string> Additives { get; set; } = new();
    public List<string> Sweeteners { get; set; } = new();
    public List<string> Preservatives { get; set; } = new();
    public List<string> Emulsifiers { get; set; } = new();
    public List<string> Other { get; set; } = new();
}

public sealed class SemanticInferenceDto
{
    public string Type { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Confidence { get; set; } = "low";
    public string Source { get; set; } = "semantic_inference";
    public string OriginBlock { get; set; } = string.Empty;
    public EvidenceTrustLevel TrustLevel { get; set; } = EvidenceTrustLevel.SemanticInference;
}

public sealed class IngredientConfidenceDto
{
    public string Ingredient { get; set; } = string.Empty;
    public string Confidence { get; set; } = "low";
    public List<string> Reasons { get; set; } = new();
    public EvidenceType EvidenceType { get; set; } = EvidenceType.IngredientDetected;
    public List<EvidenceType> EvidenceTypes { get; set; } = new();
    public EvidenceTrustLevel TrustLevel { get; set; } = EvidenceTrustLevel.ExplicitIngredient;
}

public sealed class ProcessingLevelDto
{
    public string Value { get; set; } = "unknown";
    public string Confidence { get; set; } = "low";
    public int ProcessingScore { get; set; }
    public List<string> Reasons { get; set; } = new();
}

public sealed class PositiveIngredientDto
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "unknown";
}

public sealed class AnalysisCompletenessDto
{
    public string Status { get; set; } = "insufficient";
    public List<string> Reasons { get; set; } = new();
}

public sealed class CrossContaminationRiskDto
{
    public string Allergen { get; set; } = string.Empty;
    public string Risk { get; set; } = "cross_contamination";
    public string Severity { get; set; } = "medium";
    public List<string> Evidence { get; set; } = new();
    public EvidenceType EvidenceType { get; set; } = EvidenceType.CrossContamination;
    public List<EvidenceType> EvidenceTypes { get; set; } = [EvidenceType.CrossContamination];
}

public sealed class IngredientSemanticProfileDto
{
    public List<IngredientSemanticItemDto> ProhibitedIngredients { get; set; } = new();
    public List<IngredientSemanticItemDto> ToleratedIngredients { get; set; } = new();
    public List<IngredientSemanticItemDto> PositiveIngredients { get; set; } = new();
    public List<IngredientSemanticItemDto> ControversialIngredients { get; set; } = new();
    public List<SemanticEvidenceDto> Evidence { get; set; } = new();
    public List<EvidenceType> EvidenceTypes { get; set; } = new();
    public string OverallSemanticConfidence { get; set; } = "low";
}

public sealed class IngredientSemanticItemDto
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "unknown";
    public List<string> Reasons { get; set; } = new();
    public EvidenceType EvidenceType { get; set; } = EvidenceType.IngredientDetected;
    public List<EvidenceType> EvidenceTypes { get; set; } = [EvidenceType.IngredientDetected];
}

public sealed class FutureUserProfileSupportDto
{
    public bool Ready { get; set; } = true;
    public List<string> SupportedProfiles { get; set; } = ["vegan", "diabetic", "lactose_intolerance", "child", "athlete"];
}

public sealed class IngredientAnalysisDiagnosticsDto
{
    public bool OpenAiVisionUsed { get; set; }
    public bool OcrProviderUsed { get; set; }
    public bool DocumentIntelligenceUsed { get; set; }
    public int RawTextLength { get; set; }
    public string OverallConfidence { get; set; } = "low";
    public string OcrConfidence { get; set; } = "low";
    public string ClassificationConfidence { get; set; } = "low";
    public string ImageQualityConfidence { get; set; } = "low";
    public string OverallSemanticConfidence { get; set; } = "low";
    public int SourceConflictCount { get; set; }
}
