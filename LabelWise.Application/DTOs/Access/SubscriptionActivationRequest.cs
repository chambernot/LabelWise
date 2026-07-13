namespace LabelWise.Application.DTOs.Access
{
    public class SubscriptionActivationRequest
    {
        public string DeviceId { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string? PurchaseToken { get; set; }
        public string PlanId { get; set; } = string.Empty;
    }
}
