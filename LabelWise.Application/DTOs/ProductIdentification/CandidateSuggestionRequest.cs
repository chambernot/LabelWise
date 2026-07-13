namespace LabelWise.Application.DTOs.ProductIdentification
{
    /// <summary>
    /// Request para o serviço de sugestão de candidatos.
    /// Usado quando a identificação primária falha ou tem baixa confiança.
    /// </summary>
    public class CandidateSuggestionRequest
    {
        /// <summary>
        /// Texto extraído do OCR (embalagem frontal, nome, etc.).
        /// </summary>
        public string? ExtractedText { get; set; }

        /// <summary>
        /// Lista de ingredientes parciais identificados.
        /// </summary>
        public List<string> PartialIngredients { get; set; } = [];

        /// <summary>
        /// Lista de alergênicos identificados.
        /// </summary>
        public List<string> Allergens { get; set; } = [];

        /// <summary>
        /// Categoria inferida do produto (ex: "Bebida", "Laticínio", "Snack").
        /// </summary>
        public string? InferredCategory { get; set; }

        /// <summary>
        /// Código de barras parcial ou incompleto (se disponível).
        /// </summary>
        public string? PartialBarcode { get; set; }

        /// <summary>
        /// Número máximo de candidatos a retornar.
        /// </summary>
        public int MaxCandidates { get; set; } = 5;

        /// <summary>
        /// Confiança mínima para incluir um candidato.
        /// </summary>
        public double MinConfidence { get; set; } = 0.30;

        /// <summary>
        /// Dados de imagem para futura similaridade visual (preparação arquitetural).
        /// </summary>
        public byte[]? ImageData { get; set; }

        /// <summary>
        /// Features visuais extraídas para similaridade (preparação arquitetural).
        /// </summary>
        public double[]? VisualFeatures { get; set; }

        /// <summary>
        /// ID do usuário para personalização de sugestões.
        /// </summary>
        public int? UserId { get; set; }

        /// <summary>
        /// Idioma preferido para busca.
        /// </summary>
        public string LanguageCode { get; set; } = "pt";
    }
}
