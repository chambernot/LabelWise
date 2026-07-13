namespace LabelWise.Application.DTOs.Access
{
    public class AppAccessStateResponse
    {
        public string DeviceId { get; set; } = string.Empty;
        public bool IsPremium { get; set; }
        public bool IsTrialActive { get; set; }
        public DateTimeOffset TrialEndsAt { get; set; }
        public int DaysRemaining { get; set; }
        public string SubscriptionStatus { get; set; } = "none";
        public bool CanUseAnalysis { get; set; }
        public bool CanUseComparison { get; set; }
        public bool CanUseHistory { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
