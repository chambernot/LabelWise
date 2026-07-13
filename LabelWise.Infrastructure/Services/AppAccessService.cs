using LabelWise.Application.DTOs.Access;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services
{
    public class AppAccessService : IAppAccessService
    {
        private const int TrialDurationDays = 15;

        private readonly IAppUserRepository _appUserRepository;
        private readonly ILogger<AppAccessService> _logger;

        public AppAccessService(
            IAppUserRepository appUserRepository,
            ILogger<AppAccessService> logger)
        {
            _appUserRepository = appUserRepository ?? throw new ArgumentNullException(nameof(appUserRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AppSessionResponse> InitializeSessionAsync(string deviceId, string platform)
        {
            var normalizedDeviceId = NormalizeDeviceId(deviceId);
            var normalizedPlatform = NormalizePlatform(platform, allowUnknown: false);
            var nowUtc = DateTimeOffset.UtcNow;
            var appUser = await _appUserRepository.GetByDeviceIdAsync(normalizedDeviceId);

            if (appUser == null)
            {
                appUser = new AppUser(normalizedDeviceId, normalizedPlatform, nowUtc, TrialDurationDays);
                await _appUserRepository.CreateAsync(appUser);
                _logger.LogInformation("Trial criado para deviceId {DeviceId}. TrialEndsAt={TrialEndsAt}", normalizedDeviceId, appUser.TrialEndsAt);
            }
            else
            {
                appUser.MarkSeen(nowUtc, normalizedPlatform);
                await _appUserRepository.UpdateAsync(appUser);
            }

            await _appUserRepository.SaveChangesAsync();

            _logger.LogInformation("Sessão inicializada para deviceId {DeviceId}. Premium={IsPremium}", normalizedDeviceId, appUser.IsPremium);

            return new AppSessionResponse
            {
                DeviceId = appUser.DeviceId,
                IsPremium = appUser.IsPremium,
                IsTrialActive = IsTrialActive(appUser),
                TrialEndsAt = appUser.TrialEndsAt,
                DaysRemaining = GetRemainingTrialDays(appUser),
                SubscriptionStatus = appUser.SubscriptionStatus
            };
        }

        public async Task<AppAccessStateResponse> GetAccessStateAsync(string deviceId)
        {
            var appUser = await GetOrCreateAppUserAsync(deviceId);
            var accessState = BuildAccessState(appUser);

            if (!accessState.IsPremium && !accessState.IsTrialActive)
            {
                _logger.LogInformation("Trial expirado para deviceId {DeviceId}.", accessState.DeviceId);
            }

            return accessState;
        }

        public async Task<bool> CanUseAnalysisAsync(string deviceId)
        {
            return (await GetAccessStateAsync(deviceId)).CanUseAnalysis;
        }

        public async Task<bool> CanUseComparisonAsync(string deviceId)
        {
            return (await GetAccessStateAsync(deviceId)).CanUseComparison;
        }

        public async Task<bool> CanUseHistoryAsync(string deviceId)
        {
            return (await GetAccessStateAsync(deviceId)).CanUseHistory;
        }

        public bool IsTrialActive(AppUser user)
        {
            return user != null && user.TrialEndsAt > DateTimeOffset.UtcNow;
        }

        public int GetRemainingTrialDays(AppUser user)
        {
            if (user == null || !IsTrialActive(user))
            {
                return 0;
            }

            return Math.Max(0, (int)Math.Ceiling((user.TrialEndsAt - DateTimeOffset.UtcNow).TotalDays));
        }

        private async Task<AppUser> GetOrCreateAppUserAsync(string deviceId, string? platform = null)
        {
            var normalizedDeviceId = NormalizeDeviceId(deviceId);
            var normalizedPlatform = NormalizePlatform(platform, allowUnknown: true);
            var nowUtc = DateTimeOffset.UtcNow;
            var appUser = await _appUserRepository.GetByDeviceIdAsync(normalizedDeviceId);

            if (appUser == null)
            {
                appUser = new AppUser(normalizedDeviceId, normalizedPlatform, nowUtc, TrialDurationDays);
                await _appUserRepository.CreateAsync(appUser);
                await _appUserRepository.SaveChangesAsync();

                _logger.LogInformation("Usuário anônimo criado automaticamente para deviceId {DeviceId}.", normalizedDeviceId);
                return appUser;
            }

            appUser.MarkSeen(nowUtc, normalizedPlatform);
            await _appUserRepository.UpdateAsync(appUser);
            await _appUserRepository.SaveChangesAsync();
            return appUser;
        }

        private AppAccessStateResponse BuildAccessState(AppUser appUser)
        {
            var isTrialActive = IsTrialActive(appUser);
            var hasAccess = appUser.IsPremium || isTrialActive;

            return new AppAccessStateResponse
            {
                DeviceId = appUser.DeviceId,
                IsPremium = appUser.IsPremium,
                IsTrialActive = isTrialActive,
                TrialEndsAt = appUser.TrialEndsAt,
                DaysRemaining = GetRemainingTrialDays(appUser),
                SubscriptionStatus = appUser.SubscriptionStatus,
                CanUseAnalysis = hasAccess,
                CanUseComparison = hasAccess,
                CanUseHistory = hasAccess,
                Message = BuildMessage(appUser, isTrialActive)
            };
        }

        private static string BuildMessage(AppUser appUser, bool isTrialActive)
        {
            if (appUser.IsPremium)
            {
                return "Acesso premium ativo.";
            }

            if (isTrialActive)
            {
                var remainingDays = Math.Max(0, (int)Math.Ceiling((appUser.TrialEndsAt - DateTimeOffset.UtcNow).TotalDays));
                return remainingDays <= 1
                    ? "Seu período de trial está ativo e termina em até 1 dia."
                    : $"Seu período de trial está ativo. Restam {remainingDays} dias.";
            }

            return "Seu período gratuito terminou. Assine para continuar.";
        }

        private static string NormalizeDeviceId(string deviceId)
        {
            var normalized = deviceId?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ArgumentException("deviceId é obrigatório.", nameof(deviceId));
            }

            if (normalized.Length > 200)
            {
                throw new ArgumentException("deviceId excede o tamanho máximo permitido.", nameof(deviceId));
            }

            return normalized;
        }

        private static string NormalizePlatform(string? platform, bool allowUnknown)
        {
            if (string.IsNullOrWhiteSpace(platform))
            {
                if (allowUnknown)
                {
                    return "unknown";
                }

                throw new ArgumentException("platform é obrigatório.", nameof(platform));
            }

            var normalized = platform.Trim().ToLowerInvariant();
            return normalized switch
            {
                "android" or "ios" or "unknown" => normalized,
                _ when allowUnknown => normalized,
                _ => throw new ArgumentException("platform deve ser 'android' ou 'ios'.", nameof(platform))
            };
        }
    }
}
