namespace LabelWise.Application.DTOs.IngredientAnalysis;

public sealed class AllergenRiskDto
{
    public string Allergen { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Severity { get; set; } = "contains";
    public string Source { get; set; } = string.Empty;
    public string RiskType { get; set; } = "contains";
    public string Confidence { get; set; } = "low";
    public AllergenSeverityDto AllergenSeverity { get; set; } = new();
    public List<string> Evidence { get; set; } = new();
    public EvidenceType EvidenceType { get; set; } = EvidenceType.OcrInference;
    public List<EvidenceType> EvidenceTypes { get; set; } = new();
    public List<SemanticEvidenceDto> SemanticEvidence { get; set; } = new();
    public EvidenceTrustLevel TrustLevel { get; set; } = EvidenceTrustLevel.WeakInference;
    public string OriginBlock { get; set; } = string.Empty;
}

public sealed class AllergenSeverityDto
{
    public string RegulatoryLevel { get; set; } = "low";
    public int RiskWeight { get; set; } = 20;
}
