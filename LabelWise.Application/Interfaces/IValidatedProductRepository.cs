using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LabelWise.Domain.Entities;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Interface para persistência de produtos validados.
    /// </summary>
    public interface IValidatedProductRepository
    {
        Task<ValidatedProduct?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<ValidatedProduct?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default);

        Task<ValidatedProduct?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ValidatedProduct>> GetByValidationLevelAsync(
            ValidationLevel level, 
            int skip = 0, 
            int take = 20, 
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ValidatedProduct>> GetMostReusedAsync(
            int top = 10, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Obtém produtos validados por um usuário específico.
        /// Útil para sugestão de candidatos baseada em histórico.
        /// </summary>
        Task<IReadOnlyList<ValidatedProduct>> GetByUserIdAsync(
            int userId,
            int skip = 0,
            int take = 20,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);

        Task<bool> ExistsByProductIdAsync(Guid productId, CancellationToken cancellationToken = default);

        Task AddAsync(ValidatedProduct validatedProduct, CancellationToken cancellationToken = default);

        Task UpdateAsync(ValidatedProduct validatedProduct, CancellationToken cancellationToken = default);

        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
