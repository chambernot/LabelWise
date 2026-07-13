namespace LabelWise.Application.Models.NutritionConversation;

public sealed class NutritionConversationSession
{
    public string Id { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string AnalysisId { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public string Status { get; set; } = "active";
    public NutritionConversationContext Context { get; set; } = new();
    public List<ConversationMessage> Messages { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
