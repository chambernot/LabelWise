using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LabelWise.Application.DTOs;

namespace LabelWise.Application.Interfaces
{
    public interface IAnalysisHistoryService
    {
        Task<IEnumerable<AnalysisHistorySummaryDto>> GetUserAnalysisHistoryAsync(Guid userId);
        Task<IEnumerable<AnalysisHistorySummaryDto>> GetDeviceAnalysisHistoryAsync(string deviceId);
        Task<AnalysisHistoryDetailDto?> GetAnalysisDetailAsync(Guid analysisId, Guid userId);
        Task<AnalysisHistoryDetailDto?> GetAnalysisDetailByDeviceAsync(Guid analysisId, string deviceId);
    }
}
