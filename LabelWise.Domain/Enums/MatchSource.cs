namespace LabelWise.Domain.Enums
{
    /// <summary>
    /// Fonte ou origem da correspondência/match de identificação do produto.
    /// Usado para rastrear como o produto foi identificado com precisão.
    /// </summary>
    public enum MatchSource
    {
        /// <summary>
        /// Produto identificado através de código de barras (EAN, UPC, QR Code).
        /// Confiança alta: código único e preciso.
        /// </summary>
        Barcode = 1,

        /// <summary>
        /// Produto identificado através de OCR da embalagem frontal (nome + marca).
        /// Confiança média: depende da qualidade do OCR e da base de dados.
        /// </summary>
        FrontOcr = 2,

        /// <summary>
        /// Produto identificado através de similaridade visual (AI/ML).
        /// Confiança variável: depende da qualidade do modelo e dataset.
        /// </summary>
        Similarity = 3,

        /// <summary>
        /// Produto identificado através de múltiplas fontes combinadas.
        /// Confiança alta: múltiplos métodos confirmam o mesmo produto.
        /// </summary>
        Combined = 4,

        /// <summary>
        /// Produto identificado através de Azure OpenAI Vision (GPT-4 Vision).
        /// Confiança alta: modelo multimodal avançado com interpretação contextual.
        /// </summary>
        OpenAiVision = 5,

        /// <summary>
        /// Produto identificado através de OCR + Azure OpenAI Vision.
        /// Confiança muito alta: OCR estruturado combinado com interpretação semântica.
        /// </summary>
        OcrPlusOpenAiVision = 6,

        /// <summary>
        /// Produto identificado através de catálogo local de produtos conhecidos.
        /// Confiança alta: busca textual em base validada de produtos.
        /// </summary>
        LocalCatalog = 7,

        /// <summary>
        /// Produto não identificado ou método desconhecido.
        /// Confiança baixa: produto não reconhecido.
        /// </summary>
        Unknown = 0
    }
}
