using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Models.NutritionConversation;

namespace LabelWise.Application.DTOs.NutritionConversation;

public sealed class ConversationStartResponse
{
    public string ConversationId { get; set; } = string.Empty;
    public string AnalysisId { get; set; } = string.Empty;
    public NutritionConversationSummary NutritionSummary { get; set; } = new();
    public UnifiedNutritionScore? Scores { get; set; }
    public UserProfileInsightsDto? Profiles { get; set; }
    public string InitialAssistantMessage { get; set; } = string.Empty;
}
