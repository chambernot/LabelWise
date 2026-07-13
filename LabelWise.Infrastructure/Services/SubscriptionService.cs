using LabelWise.Application.DTOs.Access;
using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly IAppUserRepository _appUserRepository;
        private readonly IAppAccessService _appAccessService;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(
            IAppUserRepository appUserRepository,
            IAppAccessService appAccessService,
            ILogger<SubscriptionService> logger)
        {
            _appUserRepository = appUserRepository ?? throw new ArgumentNullException(nameof(appUserRepository));
            _appAccessService = appAccessService ?? throw new ArgumentNullException(nameof(appAccessService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AppAccessStateResponse> ActivateAsync(SubscriptionActivationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.PlanId))
            {
                throw new ArgumentException("planId é obrigatório.", nameof(request.PlanId));
            }

            var deviceId = NormalizeDeviceId(request.DeviceId);
            var platform = NormalizePlatform(request.Platform);
            var nowUtc = DateTimeOffset.UtcNow;
            var appUser = await _appUserRepository.GetByDeviceIdAsync(deviceId)
                ?? throw new KeyNotFoundException("deviceId não encontrado. Inicialize a sessão antes de ativar a assinatura.");

            var subscriptionPlatform = platform == "ios" ? "app_store" : "google_play";
            appUser.ActivatePremium(nowUtc, subscriptionPlatform, request.PlanId, "active");
            appUser.MarkSeen(nowUtc, platform);
            await _appUserRepository.UpdateAsync(appUser);
            await _appUserRepository.SaveChangesAsync();

            _logger.LogInformation(
                "Premium ativado para deviceId {DeviceId}. Platform={Platform}, PlanId={PlanId}, TokenProvided={TokenProvided}",
                deviceId,
                subscriptionPlatform,
                request.PlanId,
                !string.IsNullOrWhiteSpace(request.PurchaseToken));

            var state = await _appAccessService.GetAccessStateAsync(deviceId);
            state.Message = "Assinatura premium ativada com sucesso.";
            return state;
        }

        public async Task<AppAccessStateResponse> RestoreAsync(SubscriptionRestoreRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var deviceId = NormalizeDeviceId(request.DeviceId);
            var appUser = await _appUserRepository.GetByDeviceIdAsync(deviceId);
            if (appUser == null)
            {
                throw new KeyNotFoundException("deviceId não encontrado. Inicialize a sessão antes de restaurar a assinatura.");
            }

            appUser.MarkSeen(DateTimeOffset.UtcNow, NormalizePlatform(request.Platform));
            await _appUserRepository.UpdateAsync(appUser);
            await _appUserRepository.SaveChangesAsync();

            var state = await _appAccessService.GetAccessStateAsync(deviceId);
            state.Message = appUser.IsPremium
                ? "Assinatura premium restaurada com sucesso."
                : "Nenhuma assinatura ativa encontrada para restaurar no momento.";

            _logger.LogInformation("Restore de assinatura solicitado para deviceId {DeviceId}. Premium={IsPremium}", deviceId, appUser.IsPremium);

            return state;
        }

        private static string NormalizeDeviceId(string deviceId)
        {
            var normalized = deviceId?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ArgumentException("deviceId é obrigatório.", nameof(deviceId));
            }

            return normalized;
        }

        private static string NormalizePlatform(string platform)
        {
            if (string.IsNullOrWhiteSpace(platform))
            {
                throw new ArgumentException("platform é obrigatório.", nameof(platform));
            }

            return platform.Trim().ToLowerInvariant();
        }
    }
}
