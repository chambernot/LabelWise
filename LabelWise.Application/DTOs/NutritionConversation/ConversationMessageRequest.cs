namespace LabelWise.Application.DTOs.NutritionConversation;

public sealed class ConversationMessageRequest
{
    public string ConversationId { get; set; } = string.Empty;
    public string AnalysisId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
