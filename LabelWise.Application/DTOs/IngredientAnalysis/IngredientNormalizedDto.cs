namespace LabelWise.Application.DTOs.IngredientAnalysis;

public sealed class IngredientNormalizedDto
{
    public string Raw { get; set; } = string.Empty;
    public string Normalized { get; set; } = string.Empty;
    public string Category { get; set; } = "unknown";
    public string Confidence { get; set; } = "low";
    public string Source { get; set; } = "IngredientBlock";
    public string DetectionType { get; set; } = "confirmed";
    public List<SemanticEvidenceDto> SemanticEvidence { get; set; } = new();
    public bool AnimalOrigin { get; set; }
    public string DietaryRisk { get; set; } = "unknown";
}
