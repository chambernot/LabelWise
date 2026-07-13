namespace LabelWise.Application.DTOs.ProductIdentification
{
    /// <summary>
    /// Resultado do serviço de sugestão de candidatos.
    /// Contém lista de produtos candidatos quando a identificação primária falha.
    /// </summary>
    public class CandidateSuggestionResult
    {
        /// <summary>
        /// Indica se o serviço encontrou candidatos válidos.
        /// </summary>
        public bool HasCandidates => TopCandidates.Count > 0;

        /// <summary>
        /// Lista ordenada dos melhores candidatos (maior confiança primeiro).
        /// </summary>
        public List<SuggestedCandidate> TopCandidates { get; set; } = [];

        /// <summary>
        /// Indica se o produto é desconhecido (nenhuma identificação confiável).
        /// Quando true, o frontend deve exibir opções de seleção manual.
        /// </summary>
        public bool IsProductUnknown { get; set; }

        /// <summary>
        /// Razão pela qual a identificação primária falhou.
        /// </summary>
        public string? FallbackReason { get; set; }

        /// <summary>
        /// Estratégias utilizadas para gerar candidatos.
        /// </summary>
        public List<string> StrategiesUsed { get; set; } = [];

        /// <summary>
        /// Tempo de processamento em segundos.
        /// </summary>
        public double ProcessingTimeSeconds { get; set; }

        /// <summary>
        /// Mensagem amigável para o usuário.
        /// </summary>
        public string? UserMessage { get; set; }

        /// <summary>
        /// Indica se similaridade visual foi utilizada (preparação arquitetural).
        /// </summary>
        public bool UsedVisualSimilarity { get; set; }

        /// <summary>
        /// Metadados adicionais do processo de sugestão.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = [];

        /// <summary>
        /// Cria resultado vazio indicando produto desconhecido.
        /// </summary>
        public static CandidateSuggestionResult CreateUnknown(string reason)
        {
            return new CandidateSuggestionResult
            {
                IsProductUnknown = true,
                FallbackReason = reason,
                UserMessage = "Não foi possível identificar o produto. Por favor, selecione manualmente ou forneça mais informações."
            };
        }

        /// <summary>
        /// Cria resultado com candidatos sugeridos.
        /// </summary>
        public static CandidateSuggestionResult CreateWithCandidates(
            List<SuggestedCandidate> candidates,
            string fallbackReason,
            List<string> strategiesUsed)
        {
            return new CandidateSuggestionResult
            {
                TopCandidates = candidates.OrderByDescending(c => c.CandidateConfidence).ToList(),
                IsProductUnknown = candidates.Count == 0,
                FallbackReason = fallbackReason,
                StrategiesUsed = strategiesUsed,
                UserMessage = candidates.Count > 0
                    ? $"Encontramos {candidates.Count} produto(s) similares. Por favor, confirme ou selecione o correto."
                    : "Não foi possível identificar o produto. Por favor, selecione manualmente."
            };
        }
    }

    /// <summary>
    /// Representa um candidato sugerido com informações detalhadas.
    /// </summary>
    public class SuggestedCandidate
    {
        /// <summary>
        /// ID do produto na base de dados (se existir).
        /// Pode ser Guid (ValidatedProduct) ou int (Product legado).
        /// </summary>
        public Guid? ProductId { get; set; }

        /// <summary>
        /// Nome sugerido do produto.
        /// </summary>
        public required string CandidateName { get; set; }

        /// <summary>
        /// Marca sugerida do produto.
        /// </summary>
        public string? CandidateBrand { get; set; }

        /// <summary>
        /// Categoria do produto candidato.
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Confiança desta sugestão (0.0 a 1.0).
        /// </summary>
        public double CandidateConfidence { get; set; }

        /// <summary>
        /// Estratégia que gerou esta sugestão.
        /// </summary>
        public CandidateMatchStrategy MatchStrategy { get; set; }

        /// <summary>
        /// Razão pela qual este candidato foi sugerido.
        /// </summary>
        public string? MatchReason { get; set; }

        /// <summary>
        /// Score de similaridade textual (se aplicável).
        /// </summary>
        public double? TextSimilarityScore { get; set; }

        /// <summary>
        /// Score de similaridade de ingredientes (se aplicável).
        /// </summary>
        public double? IngredientSimilarityScore { get; set; }

        /// <summary>
        /// Score de similaridade visual (se aplicável - preparação arquitetural).
        /// </summary>
        public double? VisualSimilarityScore { get; set; }

        /// <summary>
        /// Código de barras do candidato (se conhecido).
        /// </summary>
        public string? Barcode { get; set; }

        /// <summary>
        /// URL de imagem do candidato (se disponível).
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Detalhes adicionais sobre o match.
        /// </summary>
        public Dictionary<string, string> MatchDetails { get; set; } = [];
    }

    /// <summary>
    /// Estratégias utilizadas para encontrar candidatos.
    /// </summary>
    public enum CandidateMatchStrategy
    {
        /// <summary>
        /// Correspondência por texto similar (fuzzy match).
        /// </summary>
        TextSimilarity = 1,

        /// <summary>
        /// Correspondência por ingredientes similares.
        /// </summary>
        IngredientMatch = 2,

        /// <summary>
        /// Correspondência por categoria inferida.
        /// </summary>
        CategoryMatch = 3,

        /// <summary>
        /// Correspondência por alergênicos.
        /// </summary>
        AllergenMatch = 4,

        /// <summary>
        /// Correspondência por código de barras parcial.
        /// </summary>
        PartialBarcode = 5,

        /// <summary>
        /// Correspondência por similaridade visual (futuro).
        /// </summary>
        VisualSimilarity = 6,

        /// <summary>
        /// Correspondência combinada de múltiplas estratégias.
        /// </summary>
        Combined = 7,

        /// <summary>
        /// Sugestão baseada em histórico do usuário.
        /// </summary>
        UserHistory = 8
    }
}
