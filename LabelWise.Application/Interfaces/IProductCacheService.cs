using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Resultado da busca de produto em cache.
    /// </summary>
    public record CachedProductResult(
        Product Product,
        ValidatedProduct ValidatedData,
        bool RequiresRevalidation);

    /// <summary>
    /// Serviço de cache de produtos validados.
    /// </summary>
    public interface IProductCacheService
    {
        /// <summary>
        /// Busca um produto validado pelo código de barras.
        /// </summary>
        Task<CachedProductResult?> GetByBarcodeAsync(
            string barcode, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifica se um produto está em cache e é válido.
        /// </summary>
        Task<bool> IsCachedAndValidAsync(
            string barcode, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Adiciona ou atualiza um produto no cache.
        /// </summary>
        Task CacheProductAsync(
            Product product, 
            ValidatedProduct validatedData, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Incrementa contador de reutilização do produto.
        /// </summary>
        Task IncrementReuseCountAsync(
            Guid productId, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalida o cache de um produto.
        /// </summary>
        Task InvalidateCacheAsync(
            Guid productId, 
            CancellationToken cancellationToken = default);
    }
}
