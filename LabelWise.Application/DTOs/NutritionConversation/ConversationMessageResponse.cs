namespace LabelWise.Application.DTOs.NutritionConversation;

public sealed class ConversationMessageResponse
{
    public string ConversationId { get; set; } = string.Empty;
    public string AnalysisId { get; set; } = string.Empty;
    public string AssistantMessage { get; set; } = string.Empty;
}
