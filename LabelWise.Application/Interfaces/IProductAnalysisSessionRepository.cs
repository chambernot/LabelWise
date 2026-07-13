using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LabelWise.Domain.Entities;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Interface para persistência de sessões de análise.
    /// </summary>
    public interface IProductAnalysisSessionRepository
    {
        Task<ProductAnalysisSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<ProductAnalysisSession?> GetByIdWithCapturesAsync(Guid id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ProductAnalysisSession>> GetByUserIdAsync(
            Guid userId, 
            int skip = 0, 
            int take = 20, 
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ProductAnalysisSession>> GetByProductIdAsync(
            Guid productId, 
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ProductAnalysisSession>> GetActiveSessionsAsync(
            Guid? userId = null, 
            CancellationToken cancellationToken = default);

        Task<ProductAnalysisSession?> GetLatestByUserIdAsync(
            Guid userId, 
            CancellationToken cancellationToken = default);

        Task AddAsync(ProductAnalysisSession session, CancellationToken cancellationToken = default);

        Task UpdateAsync(ProductAnalysisSession session, CancellationToken cancellationToken = default);

        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
