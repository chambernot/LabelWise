namespace LabelWise.Application.DTOs.Access
{
    public class AppSessionResponse
    {
        public string DeviceId { get; set; } = string.Empty;
        public bool IsPremium { get; set; }
        public bool IsTrialActive { get; set; }
        public DateTimeOffset TrialEndsAt { get; set; }
        public int DaysRemaining { get; set; }
        public string SubscriptionStatus { get; set; } = "none";
    }
}
