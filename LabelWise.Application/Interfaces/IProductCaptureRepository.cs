using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Interface para persistência de capturas de produto.
    /// </summary>
    public interface IProductCaptureRepository
    {
        Task<ProductCapture?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ProductCapture>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ProductCapture>> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ProductCapture>> GetByProductIdAndTypeAsync(
            Guid productId, 
            CaptureType captureType, 
            CancellationToken cancellationToken = default);

        Task<ProductCapture?> GetLatestByProductIdAndTypeAsync(
            Guid productId, 
            CaptureType captureType, 
            CancellationToken cancellationToken = default);

        Task<int> GetCaptureCountByProductIdAsync(Guid productId, CancellationToken cancellationToken = default);

        Task AddAsync(ProductCapture capture, CancellationToken cancellationToken = default);

        Task UpdateAsync(ProductCapture capture, CancellationToken cancellationToken = default);

        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
