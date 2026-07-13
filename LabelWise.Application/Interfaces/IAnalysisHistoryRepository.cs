using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Interfaces
{
    public interface IAnalysisHistoryRepository
    {
        Task<ProductComparisonAnalysisInputDto?> GetByIdAsync(Guid analysisId, Guid? userId = null);
    }
}
