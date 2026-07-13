using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LabelWise.Domain.Entities;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Repositório para gerenciamento de produtos conhecidos
    /// </summary>
    public interface IKnownProductRepository
    {
        /// <summary>
        /// Busca produto por código de barras
        /// </summary>
        Task<KnownProduct?> GetByBarcodeAsync(string barcode);

        /// <summary>
        /// Busca produto por ID
        /// </summary>
        Task<KnownProduct?> GetByIdAsync(Guid id);

        /// <summary>
        /// Busca produto por nome exato e marca
        /// </summary>
        Task<KnownProduct?> GetByNameAndBrandAsync(string name, string brand);

        /// <summary>
        /// Adiciona um novo produto conhecido
        /// </summary>
        Task<KnownProduct> AddAsync(KnownProduct product);

        /// <summary>
        /// Atualiza um produto conhecido
        /// </summary>
        Task UpdateAsync(KnownProduct product);

        /// <summary>
        /// Remove um produto conhecido
        /// </summary>
        Task DeleteAsync(Guid id);

        /// <summary>
        /// Lista produtos por categoria
        /// </summary>
        Task<List<KnownProduct>> GetByCategoryAsync(string category, int limit = 50);

        /// <summary>
        /// Lista os produtos mais populares (mais identificados)
        /// </summary>
        Task<List<KnownProduct>> GetMostPopularAsync(int limit = 20);

        /// <summary>
        /// Lista produtos validados
        /// </summary>
        Task<List<KnownProduct>> GetValidatedProductsAsync(int limit = 100);

        /// <summary>
        /// Conta total de produtos conhecidos
        /// </summary>
        Task<int> GetTotalCountAsync();

        /// <summary>
        /// Verifica se um produto já existe
        /// </summary>
        Task<bool> ExistsAsync(string name, string brand);
    }
}
