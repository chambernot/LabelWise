using LabelWise.Application.Models.NutritionConversation;

namespace LabelWise.Application.Interfaces;

public interface IConversationRepository
{
    Task SaveAsync(NutritionConversationSession session, CancellationToken cancellationToken = default);

    Task<NutritionConversationSession?> GetAsync(
        string conversationId,
        string analysisId,
        CancellationToken cancellationToken = default);

    Task AppendMessagesAsync(
        string conversationId,
        string analysisId,
        IReadOnlyCollection<ConversationMessage> messages,
        CancellationToken cancellationToken = default);
}
