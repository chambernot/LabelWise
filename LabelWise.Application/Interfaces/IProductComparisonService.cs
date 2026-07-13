using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Interfaces
{
    public interface IProductComparisonService
    {
        Task<ProductComparisonResponse> CompareAsync(string analysisIdA, string analysisIdB, Guid? userId = null, string? deviceId = null);
        Task<ProductComparisonResponse> CompareAsync(ProductComparisonAnalysisInputDto productA, ProductComparisonAnalysisInputDto productB);
    }
}
