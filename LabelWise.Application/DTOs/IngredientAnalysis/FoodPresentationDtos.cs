namespace LabelWise.Application.DTOs.IngredientAnalysis;

public sealed class UnifiedFoodAnalysisResponse
{
    public string Version { get; set; } = "1.0";
    public string AnalysisType { get; set; } = "ingredient_analysis";
    public List<string> AnalysisScopes { get; set; } = ["ingredients", "diet_profiles", "processing_level"];
    public string? ProductName { get; set; }
    public string? Brand { get; set; }
    public List<SummaryCardDto> SummaryCards { get; set; } = new();
    public List<QuickInsightDto> QuickInsights { get; set; } = new();
    public List<QuickFlagDto> QuickFlags { get; set; } = new();
    public ProcessingClassificationDto ProcessingClassification { get; set; } = new();
    public LabelWise.Application.DTOs.Nutrition.IngredientContextDto IngredientContext { get; set; } = new();
    public OverallFoodRatingDto OverallFoodRating { get; set; } = new();
    public PresentationSummaryDto PresentationSummary { get; set; } = new();
    public List<OcrCorrectionDto> OcrCorrections { get; set; } = new();
    public List<IngredientConfidenceDto> IngredientConfidence { get; set; } = new();
    public IngredientAnalysisTechnicalSnapshotDto IngredientAnalysis { get; set; } = new();
}

public sealed class StructuredFoodEntityDto
{
    public string CanonicalName { get; set; } = string.Empty;
    public string OriginalText { get; set; } = string.Empty;
    public string Category { get; set; } = "unknown";
    public string Confidence { get; set; } = "low";
    public string SourceBlock { get; set; } = "UnknownBlock";
    public string DetectionType { get; set; } = "suspected";
    public string IndustrializationImpact { get; set; } = "neutral";
    public FoodEntityAllergenProfileDto AllergenProfile { get; set; } = new();
    public string SemanticGroup { get; set; } = "unknown";
}

public sealed class FoodEntityAllergenProfileDto
{
    public bool IsAllergen { get; set; }
    public string RiskType { get; set; } = "none";
    public List<string> RelatedAllergens { get; set; } = new();
}

public sealed class UnifiedSemanticStateDto
{
    public List<StructuredFoodEntityDto> Entities { get; set; } = new();
    public List<string> Ingredients { get; set; } = new();
    public List<string> Claims { get; set; } = new();
    public List<string> ConfirmedAllergens { get; set; } = new();
    public List<string> CrossContaminationRisks { get; set; } = new();
    public ProcessingClassificationDto ProcessingClassification { get; set; } = new();
    public DietProfilesDto DietProfiles { get; set; } = new();
    public string TrustLevel { get; set; } = "low";
    public int TrustScore { get; set; }
    public List<string> ConsistencyWarnings { get; set; } = new();
}

public sealed class FoodPublicResponseDto
{
    public object? Analysis { get; set; }
    public DietProfilesDto Compatibility { get; set; } = new();
    public List<string> Alerts { get; set; } = new();
    public List<StructuredFoodEntityDto> Ingredients { get; set; } = new();
    public LabelWise.Application.DTOs.Nutrition.IngredientContextDto Nutrition { get; set; } = new();
    public LabelWise.Application.DTOs.FoodAnalysisTrust.FoodAnalysisTrustReport Trust { get; set; } = new();
    public PresentationSummaryDto Summary { get; set; } = new();
}

public sealed class FoodDebugResponseDto
{
    public string RawOcr { get; set; } = string.Empty;
    public List<OcrCorrectionDto> OcrCorrections { get; set; } = new();
    public List<StructuredTextBlockDto> Blocks { get; set; } = new();
    public List<string> SemanticLogs { get; set; } = new();
    public List<string> ConflictLogs { get; set; } = new();
    public List<string> ParsingTraces { get; set; } = new();
    public List<string> IngredientLineage { get; set; } = new();
    public List<string> DecisionLineage { get; set; } = new();
}

public sealed class SummaryCardDto
{
    public string Type { get; set; } = "unknown";
    public string Status { get; set; } = "neutral";
    public string Label { get; set; } = string.Empty;
    public string Icon { get; set; } = "info";
    public PresentationHintDto PresentationHint { get; set; } = new();
}

public sealed class PresentationHintDto
{
    public string Severity { get; set; } = "info";
    public string DisplayMode { get; set; } = "default";
    public bool Highlight { get; set; }
    public int Priority { get; set; } = 5;
    public string UiStyle { get; set; } = "neutral";
}

public sealed class QuickFlagDto
{
    public string Type { get; set; } = "positive";
    public string Label { get; set; } = string.Empty;
}

public sealed class ProcessingClassificationDto
{
    public string Level { get; set; } = "unknown";
    public string Confidence { get; set; } = "low";
    public int ProcessingScore { get; set; }
    public List<string> Reasons { get; set; } = new();
}

public sealed class QuickInsightDto
{
    public string Type { get; set; } = "general";
    public string Text { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
    public string Icon { get; set; } = "info";
}

public sealed class OverallFoodRatingDto
{
    public string Level { get; set; } = "unknown";
    public string Label { get; set; } = "Análise inconclusiva";
    public List<string> Reasons { get; set; } = new();
}

public sealed class PresentationSummaryDto
{
    public string Title { get; set; } = "Análise de ingredientes";
    public string Subtitle { get; set; } = "Confira os principais pontos detectados.";
    public string Highlight { get; set; } = "Ver detalhes";
}

public sealed class OcrCorrectionDto
{
    public string Original { get; set; } = string.Empty;
    public string Corrected { get; set; } = string.Empty;
    public string Confidence { get; set; } = "low";
    public string Reason { get; set; } = "Correção contextual alimentar";
}

public sealed class IngredientAnalysisTechnicalSnapshotDto
{
    public List<IngredientNormalizedDto> NormalizedIngredients { get; set; } = new();
    public DietProfilesDto DietProfiles { get; set; } = new();
    public ProcessingLevelDto ProcessingLevel { get; set; } = new();
    public ProcessingClassificationDto ProcessingClassification { get; set; } = new();
    public LabelWise.Application.DTOs.Nutrition.IngredientContextDto IngredientContext { get; set; } = new();
    public IngredientSemanticProfileDto SemanticProfile { get; set; } = new();
    public AssistantSummaryDto AssistantSummary { get; set; } = new();
    public IngredientAnalysisDiagnosticsDto Diagnostics { get; set; } = new();
    public List<ClaimDetectionDto> ClaimsDetected { get; set; } = new();
    public List<IngredientConfidenceDto> IngredientConfidence { get; set; } = new();
    public List<PositiveIngredientDto> PositiveIngredients { get; set; } = new();
    public List<AllergenRiskDto> AllergenRisks { get; set; } = new();
    public List<string> ReasonSources { get; set; } = new();
}
