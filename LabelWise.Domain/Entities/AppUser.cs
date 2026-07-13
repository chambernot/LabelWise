using LabelWise.Domain.Common;

namespace LabelWise.Domain.Entities
{
    public class AppUser : AuditableEntity
    {
        public string DeviceId { get; private set; } = string.Empty;
        public string Platform { get; private set; } = "unknown";
        public DateTimeOffset FirstAccessAt { get; private set; }
        public DateTimeOffset TrialEndsAt { get; private set; }
        public bool IsPremium { get; private set; }
        public string SubscriptionStatus { get; private set; } = "none";
        public string? SubscriptionPlatform { get; private set; }
        public string? SubscriptionPlanId { get; private set; }
        public DateTimeOffset LastSeenAt { get; private set; }

        protected AppUser()
        {
        }

        public AppUser(string deviceId, string platform, DateTimeOffset nowUtc, int trialDays = 15)
        {
            DeviceId = NormalizeRequiredDeviceId(deviceId);
            Platform = NormalizePlatform(platform);
            FirstAccessAt = nowUtc;
            TrialEndsAt = nowUtc.AddDays(trialDays);
            LastSeenAt = nowUtc;
            IsPremium = false;
            SubscriptionStatus = "none";
        }

        public void MarkSeen(DateTimeOffset nowUtc, string? platform = null)
        {
            LastSeenAt = nowUtc;

            if (!string.IsNullOrWhiteSpace(platform))
            {
                Platform = NormalizePlatform(platform);
            }

            SetUpdated();
        }

        public void ActivatePremium(DateTimeOffset nowUtc, string subscriptionPlatform, string planId, string subscriptionStatus = "active")
        {
            IsPremium = true;
            SubscriptionStatus = NormalizeStatus(subscriptionStatus);
            SubscriptionPlatform = NormalizeNullable(subscriptionPlatform);
            SubscriptionPlanId = NormalizeNullable(planId);
            LastSeenAt = nowUtc;
            SetUpdated();
        }

        public void UpdateSubscription(DateTimeOffset nowUtc, bool isPremium, string subscriptionStatus, string? subscriptionPlatform, string? subscriptionPlanId)
        {
            IsPremium = isPremium;
            SubscriptionStatus = NormalizeStatus(subscriptionStatus);
            SubscriptionPlatform = NormalizeNullable(subscriptionPlatform);
            SubscriptionPlanId = NormalizeNullable(subscriptionPlanId);
            LastSeenAt = nowUtc;
            SetUpdated();
        }

        private static string NormalizeRequiredDeviceId(string deviceId)
        {
            var normalized = NormalizeNullable(deviceId);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ArgumentException("deviceId é obrigatório.", nameof(deviceId));
            }

            return normalized;
        }

        private static string NormalizePlatform(string? platform)
        {
            return NormalizeNullable(platform)?.ToLowerInvariant() ?? "unknown";
        }

        private static string NormalizeStatus(string? status)
        {
            return NormalizeNullable(status)?.ToLowerInvariant() ?? "none";
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
