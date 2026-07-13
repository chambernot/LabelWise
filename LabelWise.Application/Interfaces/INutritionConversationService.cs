using LabelWise.Application.DTOs.NutritionConversation;

namespace LabelWise.Application.Interfaces;

public interface INutritionConversationService
{
    Task<ConversationStartResponse> StartAsync(
        byte[] imageBytes,
        string? contentType,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<ConversationMessageResponse?> SendMessageAsync(
        ConversationMessageRequest request,
        CancellationToken cancellationToken = default);
}
