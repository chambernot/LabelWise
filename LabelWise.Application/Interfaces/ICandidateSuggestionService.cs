using LabelWise.Application.DTOs.ProductIdentification;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Serviço de sugestão de candidatos para identificação de produtos.
    /// 
    /// PROPÓSITO:
    /// Quando a identificação primária falha ou tem baixa confiança,
    /// este serviço gera uma lista de produtos candidatos baseada em:
    /// - Texto extraído (fuzzy matching)
    /// - Ingredientes parciais
    /// - Alergênicos identificados
    /// - Categoria inferida
    /// - Similaridade visual (futuro)
    /// 
    /// REGRA PRINCIPAL:
    /// Se confiança da identificação for baixa (&lt; 0.60), retornar
    /// ProductUnknown + topCandidates em vez de inventar um nome.
    /// 
    /// ARQUITETURA:
    /// Preparado para integração futura com:
    /// - Similaridade visual (embeddings de imagem)
    /// - Base de dados externa de produtos
    /// - Machine Learning para ranking de candidatos
    /// </summary>
    public interface ICandidateSuggestionService
    {
        /// <summary>
        /// Sugere candidatos de produtos baseado nas informações disponíveis.
        /// </summary>
        /// <param name="request">Request contendo texto, ingredientes, alergênicos, categoria</param>
        /// <returns>Lista de candidatos ordenados por confiança</returns>
        Task<CandidateSuggestionResult> SuggestCandidatesAsync(CandidateSuggestionRequest request);

        /// <summary>
        /// Busca candidatos por texto similar (fuzzy search).
        /// </summary>
        /// <param name="text">Texto para busca</param>
        /// <param name="maxResults">Número máximo de resultados</param>
        /// <returns>Lista de candidatos</returns>
        Task<List<SuggestedCandidate>> SearchByTextAsync(string text, int maxResults = 5);

        /// <summary>
        /// Busca candidatos por ingredientes similares.
        /// </summary>
        /// <param name="ingredients">Lista de ingredientes</param>
        /// <param name="maxResults">Número máximo de resultados</param>
        /// <returns>Lista de candidatos</returns>
        Task<List<SuggestedCandidate>> SearchByIngredientsAsync(
            List<string> ingredients, 
            int maxResults = 5);

        /// <summary>
        /// Busca candidatos por categoria.
        /// </summary>
        /// <param name="category">Categoria inferida</param>
        /// <param name="maxResults">Número máximo de resultados</param>
        /// <returns>Lista de candidatos</returns>
        Task<List<SuggestedCandidate>> SearchByCategoryAsync(
            string category, 
            int maxResults = 5);

        /// <summary>
        /// Busca candidatos por similaridade visual (preparação arquitetural).
        /// </summary>
        /// <param name="visualFeatures">Features visuais extraídas</param>
        /// <param name="maxResults">Número máximo de resultados</param>
        /// <returns>Lista de candidatos</returns>
        Task<List<SuggestedCandidate>> SearchByVisualSimilarityAsync(
            double[] visualFeatures, 
            int maxResults = 5);

        /// <summary>
        /// Combina e rankeia candidatos de múltiplas fontes.
        /// </summary>
        /// <param name="candidates">Lista de candidatos de diferentes estratégias</param>
        /// <param name="maxResults">Número máximo de resultados finais</param>
        /// <returns>Lista combinada e ordenada por confiança</returns>
        List<SuggestedCandidate> CombineAndRankCandidates(
            IEnumerable<SuggestedCandidate> candidates, 
            int maxResults = 5);
    }
}
