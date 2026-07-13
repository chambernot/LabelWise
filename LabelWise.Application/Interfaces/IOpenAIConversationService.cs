using LabelWise.Application.Models.NutritionConversation;

namespace LabelWise.Application.Interfaces;

public interface IOpenAIConversationService
{
    Task<string> GenerateInitialMessageAsync(
        NutritionConversationContext context,
        CancellationToken cancellationToken = default);

    Task<string> GenerateReplyAsync(
        NutritionConversationContext context,
        IReadOnlyList<ConversationMessage> conversationHistory,
        CancellationToken cancellationToken = default);
}
