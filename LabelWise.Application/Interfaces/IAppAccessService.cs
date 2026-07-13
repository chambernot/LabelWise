using LabelWise.Application.DTOs.Access;
using LabelWise.Domain.Entities;

namespace LabelWise.Application.Interfaces
{
    public interface IAppAccessService
    {
        Task<AppSessionResponse> InitializeSessionAsync(string deviceId, string platform);
        Task<AppAccessStateResponse> GetAccessStateAsync(string deviceId);
        Task<bool> CanUseAnalysisAsync(string deviceId);
        Task<bool> CanUseComparisonAsync(string deviceId);
        Task<bool> CanUseHistoryAsync(string deviceId);
        bool IsTrialActive(AppUser user);
        int GetRemainingTrialDays(AppUser user);
    }
}
