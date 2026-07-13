namespace LabelWise.Application.DTOs.IngredientAnalysis;

public sealed class AssistantSummaryDto
{
    public string Text { get; set; } = string.Empty;
    public string Confidence { get; set; } = "low";
    public List<string> Highlights { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<EvidenceType> EvidenceTypes { get; set; } = new();
    public List<SemanticEvidenceDto> Evidence { get; set; } = new();
}
