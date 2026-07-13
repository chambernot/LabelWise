using LabelWise.Application.DTOs.Access;

namespace LabelWise.Application.Interfaces
{
    public interface ISubscriptionService
    {
        Task<AppAccessStateResponse> ActivateAsync(SubscriptionActivationRequest request);
        Task<AppAccessStateResponse> RestoreAsync(SubscriptionRestoreRequest request);
    }
}
