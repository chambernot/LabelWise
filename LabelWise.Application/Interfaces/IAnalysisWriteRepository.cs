using LabelWise.Domain.Entities;

namespace LabelWise.Application.Interfaces
{
    public interface IAnalysisWriteRepository
    {
        Task AddAsync(ProductAnalysis analysis, CancellationToken cancellationToken = default);
        Task UpdateAsync(ProductAnalysis analysis, CancellationToken cancellationToken = default);
    }
}
