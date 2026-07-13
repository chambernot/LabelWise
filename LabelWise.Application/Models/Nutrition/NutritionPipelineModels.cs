using LabelWise.Application.DTOs.AI;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Models.Nutrition;

/// <summary>
/// Carries a single nutrient value together with its origin and extraction confidence.
/// Used for field-level traceability through the nutrition pipeline.
/// </summary>
public sealed class FieldValue
{
    public double? Value      { get; init; }
    /// <summary>Origin tag: "OCR", "GPT", "Fallback", "Unknown".</summary>
    public string  Source     { get; init; } = "Unknown";
    /// <summary>Extraction confidence in [0, 1].</summary>
    public double  Confidence { get; init; }

    public bool IsValid => Value.HasValue;

    public static FieldValue FromOcr(double value, double confidence = 0.90)
        => new() { Value = value, Source = "OCR",      Confidence = confidence };

    public static FieldValue FromGpt(double value, double confidence = 0.75)
        => new() { Value = value, Source = "GPT",      Confidence = confidence };

    public static FieldValue FromFallback(double value, double confidence = 0.40)
        => new() { Value = value, Source = "Fallback", Confidence = confidence };

    public FieldValue WithConfidence(double confidence)
        => new() { Value = Value, Source = Source, Confidence = confidence };
}

public enum NutritionDecisionMode
{
    FullNutritionLabel,
    PartialNutritionLabel,
    FrontOfPackageOnly,
    CategoryFallback
}

public enum DataSource
{
    Real,
    Partial,
    Fallback,
    Inferred
}

public enum ProductForm
{
    Solid,
    Powder,
    LiquidPrepared,
    Unknown
}

public enum NutritionServingModel
{
    Per100g,
    Per100ml,
    PerPortion,
    Mixed,
    Unknown
}

/// <summary>
/// Unidade base da tabela nutricional do produto.
/// Usada para impedir conversão automática entre ml e g.
/// </summary>
public enum NutritionUnit
{
    Unknown,
    Gram,
    Milliliter
}

public sealed class AnalysisEvidence
{
    public bool HasVisibleNutritionTable { get; set; }
    public bool HasReliableNumericExtraction { get; set; }
    public bool HasIngredientsList { get; set; }
    public bool HasVisibleClaims { get; set; }
    public double NumericExtractionConfidence { get; set; }
    public double IngredientsConfidence { get; set; }
    public double ProductIdentificationConfidence { get; set; }
}

public sealed class CategoryDecisionResult
{
    public string? CategoryCode { get; set; }
    public string? CategoryName { get; set; }
    public string ProcessingLevel { get; set; } = "processado";
    public string PreferredOffender { get; set; } = "dados insuficientes";
    public int FallbackScoreMin { get; set; } = 35;
    public int FallbackScoreMax { get; set; } = 60;
    public bool CanInferProteinPositive { get; set; }
    public bool CanInferFiberPositive { get; set; }
    public bool CanInferLowSodiumPositive { get; set; }
    public bool IsUltraProcessed { get; set; }
    public List<string> QualitativeSignals { get; set; } = [];
}

public sealed class ScoreImpact
{
    public string Reason { get; set; } = string.Empty;
    public int Points { get; set; }
}

public sealed class ScoreCalculationResult
{
    public int ScoreRaw { get; set; }
    public int ScoreAdjusted { get; set; }
    public List<ScoreImpact> Penalties { get; set; } = [];
    public List<ScoreImpact> Bonuses { get; set; } = [];
    public string ProbableOffender { get; set; } = string.Empty;
    public List<string> AppliedRules { get; set; } = [];
}

public sealed class ScoreInterpretationResult
{
    public string Label { get; set; } = string.Empty;
    public string SafeLabel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string RecommendationLevel { get; set; } = string.Empty;
    public string SemanticRecommendation { get; set; } = string.Empty;
    public string AbsoluteRecommendation { get; set; } = string.Empty;
    public string ComparativeRecommendation { get; set; } = string.Empty;
    public string ScoreInterpretation { get; set; } = string.Empty;
    public string AbsoluteLabel { get; set; } = string.Empty;
}

public enum FieldConfidence
{
    None,
    Low,
    Medium,
    High
}

public class NutritionConfidenceResult
{
    public Dictionary<string, FieldConfidence> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public double GlobalScore { get; set; }

    public FieldConfidence GetFieldConfidence(string fieldName)
        => Fields.TryGetValue(fieldName, out var confidence) ? confidence : FieldConfidence.None;
}

public class OpenAiNutritionExtractionResult
{
    public string? ProductName { get; set; }
    public string? Brand { get; set; }
    public OpenAiNutritionServingInfo? Serving { get; set; }
    public OpenAiNutritionInfo? NutritionPerServing { get; set; }
    public OpenAiNutritionInfo? NutritionPer100g { get; set; }
}

public class OpenAiNutritionServingInfo
{
    public double? Amount { get; set; }
    public string? Unit { get; set; }
    public string? Description { get; set; }
}

public class OpenAiNutritionInfo
{
    public double? CaloriesKcal { get; set; }
    public double? Carbohydrates { get; set; }
    public double? Proteins { get; set; }
    public double? TotalFats { get; set; }
    public double? SaturatedFats { get; set; }
    public double? TransFats { get; set; }
    public double? Fiber { get; set; }
    public double? Sugar { get; set; }
    public double? AddedSugar { get; set; }
    public double? SodiumMg { get; set; }
}

public sealed class NutritionAnalysisContext
{
    public VisualInterpretationResult? VisionResult { get; set; }
    public string? FileName { get; set; }
    public string LanguageCode { get; set; } = "pt";
    public List<string>? RequestedProfiles { get; set; }
    public Guid? UserId { get; set; }
    public string? DeviceId { get; set; }

    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public double ProcessingTimeSeconds { get; set; }
    public Guid? AnalysisId { get; set; }

    public string? ProductName { get; set; }
    public string? Brand { get; set; }
    public string? CategoryRaw { get; set; }
    public string? CategoryNormalized { get; set; }
    public string? CategoryNormalizedCode { get; set; }
    public string? PackageWeight { get; set; }
    public string ProcessingLevel { get; set; } = "processado";

    public NutritionDecisionMode AnalysisMode { get; set; } = NutritionDecisionMode.CategoryFallback;
    public AnalysisMode PublicAnalysisMode { get; set; } = Domain.Enums.AnalysisMode.FrontOfPackageOnly;
    public AnalysisEvidence Evidence { get; set; } = new();

    public List<string> VisibleClaims { get; set; } = [];
    public EstimatedNutritionProfileDto? ExtractedNutrition { get; set; }
    public EstimatedNutritionProfileDto? ReferenceNutrition { get; set; }
    public EstimatedNutritionProfileDto? FinalNutritionProfile { get; set; }
    public List<string> QualitativeSignals { get; set; } = [];

    public int ScoreRaw { get; set; }
    public int ScoreAdjusted { get; set; }
    public string ScoreLabel { get; set; } = string.Empty;
    public string SafeLabel { get; set; } = string.Empty;
    public string RecommendationLevel { get; set; } = string.Empty;
    public string PrincipalOffender { get; set; } = string.Empty;
    public bool RequiresModeration { get; set; }
    public bool IsUltraProcessed { get; set; }

    public string? Summary { get; set; }
    public List<string> Alerts { get; set; } = [];
    public ProductClassificationDto HealthProfiles { get; set; } = new();
    public List<string> ConsistencyIssues { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<string> InferredRisks { get; set; } = [];

    public string? ExplicacaoScore { get; set; }
    public string? PontoPrincipal { get; set; }
    public List<string> ResumoRapido { get; set; } = [];
    public string Tom { get; set; } = "simples e direto";
    public string FallbackType { get; set; } = "unknown";
    public bool HasReliableNutritionData { get; set; }
    public DataSource NutritionDataSource { get; set; } = DataSource.Inferred;
    public ProductForm ProductForm { get; set; } = ProductForm.Unknown;
    public NutritionServingModel ServingModel { get; set; } = NutritionServingModel.Unknown;
    public string? NormalizationApplied { get; set; }
    public string? NutritionColumnUsed { get; set; }
    public bool IsNutritionLocked { get; set; }
    public bool IsInconsistent { get; set; }
    public List<string> NutritionFlags { get; set; } = [];
    public List<string> PrincipalOffenders { get; set; } = [];

    // Validação Híbrida OCR (Azure OpenAI Vision + Computer Vision)
    public bool HybridOcrValidationApplied { get; set; }
    public bool HybridOcrCorrectionApplied { get; set; }
    public string? HybridOcrValidationMethod { get; set; }

    /// <summary>
    /// Quando true, indica que nenhum dado nutricional válido foi extraído.
    /// Score, perfis e classificações devem ser nulos na resposta final —
    /// nunca gerados a partir de estimativas por categoria.
    /// </summary>
    public bool BlockScoreAndProfiles { get; set; }

    public ConfidenceDetailsDto ConfidenceDetails { get; set; } = new();
    public CategoryNormalizationResult? CategoryNormalization { get; set; }
    public CategoryDecisionResult CategoryDecision { get; set; } = new();
    public ScoreCalculationResult? ScoreCalculation { get; set; }
    public ScoreInterpretationResult? ScoreInterpretation { get; set; }
}
