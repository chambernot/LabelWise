namespace LabelWise.Application.DTOs.IngredientAnalysis;

public sealed class ClaimDetectionDto
{
    public string Text { get; set; } = string.Empty;
    public string Type { get; set; } = "other";
    public string Confidence { get; set; } = "low";
    public EvidenceType EvidenceType { get; set; } = EvidenceType.ClaimDetected;
    public List<EvidenceType> EvidenceTypes { get; set; } = [EvidenceType.ClaimDetected];
    public List<SemanticEvidenceDto> Evidence { get; set; } = new();
    public EvidenceTrustLevel TrustLevel { get; set; } = EvidenceTrustLevel.ExplicitRegulatoryClaim;
    public string OriginBlock { get; set; } = "RegulatoryClaimBlock";
}
