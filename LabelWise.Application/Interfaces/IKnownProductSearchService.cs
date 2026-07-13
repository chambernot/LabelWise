using System.Threading.Tasks;
using LabelWise.Application.DTOs.KnownProducts;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Serviço de busca de produtos conhecidos.
    /// 
    /// OBJETIVO:
    /// Fornecer busca textual aproximada usando o catálogo persistido da aplicação
    /// como alternativa econômica a mecanismos externos de search.
    /// 
    /// ESTRATÉGIA:
    /// Interface abstrata preparada para futura migração para:
    /// - Azure AI Search
    /// - MongoDB Atlas Search
    /// - Elasticsearch
    /// 
    /// IMPORTANTE:
    /// Esta interface não deve expor detalhes de implementação do provider.
    /// Qualquer provider deve ser capaz de implementá-la.
    /// </summary>
    public interface IKnownProductSearchService
    {
        /// <summary>
        /// Busca produtos conhecidos por texto, barcode ou outras características
        /// </summary>
        /// <param name="request">Requisição de busca</param>
        /// <returns>Resultados ranqueados por relevância</returns>
        Task<KnownProductSearchResponse> SearchAsync(KnownProductSearchRequest request);

        /// <summary>
        /// Busca rápida apenas por código de barras (otimizada)
        /// </summary>
        /// <param name="barcode">Código de barras</param>
        /// <returns>Produto encontrado ou null</returns>
        Task<KnownProductSearchResult?> SearchByBarcodeAsync(string barcode);

        /// <summary>
        /// Sugere produtos similares baseado em texto parcial
        /// Útil para auto-complete
        /// </summary>
        /// <param name="partialText">Texto parcial (ex: "bisc cho", "coca")</param>
        /// <param name="maxResults">Máximo de sugestões</param>
        /// <returns>Lista de sugestões</returns>
        Task<KnownProductSearchResponse> SuggestAsync(string partialText, int maxResults = 5);

        /// <summary>
        /// Reindexar todos os produtos conhecidos
        /// (Usado após importação em lote ou manutenção)
        /// </summary>
        Task ReindexAllAsync();
    }
}
