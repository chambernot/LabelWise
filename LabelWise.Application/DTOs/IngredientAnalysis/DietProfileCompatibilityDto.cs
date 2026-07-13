using LabelWise.Domain.Enums;
using System.Text.Json.Serialization;

namespace LabelWise.Application.DTOs.IngredientAnalysis;

/// <summary>
/// ⚠️ LEGACY: Este enum será substituído por FoodCompatibilityStatus da nova arquitetura
/// </summary>
public enum EvidenceType
{
    IngredientDetected,
    ClaimDetected,
    CrossContamination,
    OcrInference,
    OpenAiInference,
    NutritionInference
}

/// <summary>
/// ⚠️ LEGACY: Substituído por EvidencePriority na nova arquitetura
/// </summary>
public enum EvidenceTrustLevel
{
    Unknown = 0,
    WeakInference = 20,
    SemanticInference = 40,
    StructuredText = 60,
    ExplicitIngredient = 80,
    ExplicitRegulatoryClaim = 100
}

/// <summary>
/// ⚠️ LEGACY: Substituído por EvidencePriority na nova arquitetura
/// </summary>
public enum TrustLevel
{
    ExplicitRegulatory = 100,
    ExplicitIngredient = 80,
    StructuredOCR = 60,
    SemanticInference = 40,
    WeakInference = 20,
    Unknown = 0
}

/// <summary>
/// ⚠️ LEGACY: Substituído por FoodCompatibilityStatus na nova arquitetura
/// </summary>
public enum CompatibilityStatus
{
    ConfirmedCompatible,
    LikelyCompatible,
    Uncertain,
    LikelyNotCompatible,
    NotCompatible
}

/// <summary>
/// ⚠️ LEGACY: Substituído por FoodCompatibilityStatus na nova arquitetura
/// </summary>
public enum DietCompatibilityStatus
{
    confirmed_compatible,
    likely_compatible,
    uncertain,
    likely_not_compatible,
    attention,
    not_compatible
}

public static class DietCompatibilityStatuses
{
    public const string ConfirmedCompatible = "confirmed_compatible";
    public const string LikelyCompatible = "likely_compatible";
    public const string Uncertain = "uncertain";
    public const string LikelyNotCompatible = "likely_not_compatible";
    public const string Attention = "attention";
    public const string NotCompatible = "not_compatible";
}

public sealed class SemanticEvidenceDto
{
    public EvidenceType EvidenceType { get; set; }
    public string Type { get; set; } = EvidenceType.OcrInference.ToString();
    public string Text { get; set; } = string.Empty;
    public string Confidence { get; set; } = "low";
    public string Source { get; set; } = string.Empty;
    public EvidenceTrustLevel TrustLevel { get; set; } = EvidenceTrustLevel.WeakInference;
    public string OriginBlock { get; set; } = string.Empty;
}

public class DietCompatibilityDto
{
    public bool Compatible { get; set; }
    public string CompatibilityLevel { get; set; } = "unknown";
    public string CompatibilityStatus { get; set; } = DietCompatibilityStatuses.Uncertain;
    public string Confidence { get; set; } = "low";
    public List<string> Reasons { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> ReasonSources { get; set; } = new();
    public CompatibilityStatus Status { get; set; } = LabelWise.Application.DTOs.IngredientAnalysis.CompatibilityStatus.Uncertain;
    public List<SemanticEvidenceDto> Evidence { get; set; } = new();
    public List<EvidenceType> EvidenceTypes { get; set; } = new();
}

public sealed class DietProfileCompatibilityDto
{
    public bool Compatible { get; set; }
    public string CompatibilityLevel { get; set; } = "unknown";
    public string CompatibilityStatus { get; set; } = DietCompatibilityStatuses.Uncertain;
    public string Confidence { get; set; } = "low";
    public List<string> Reasons { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> ReasonSources { get; set; } = new();
    public CompatibilityStatus Status { get; set; } = LabelWise.Application.DTOs.IngredientAnalysis.CompatibilityStatus.Uncertain;
    public List<SemanticEvidenceDto> Evidence { get; set; } = new();
    public List<EvidenceType> EvidenceTypes { get; set; } = new();
}
