using LabelWise.Domain.Entities;

namespace LabelWise.Application.Interfaces
{
    public interface IAnalysisRepository
    {
        Task<ProductAnalysis?> GetByIdAsync(Guid analysisId);
        Task<IReadOnlyCollection<ProductAnalysis>> GetByDeviceIdAsync(string deviceId);
        Task<IReadOnlyCollection<ProductAnalysis>> GetByUserIdAsync(Guid userId);
    }
}
