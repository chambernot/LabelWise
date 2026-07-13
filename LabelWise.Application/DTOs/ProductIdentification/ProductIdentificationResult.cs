using LabelWise.Domain.Enums;

namespace LabelWise.Application.DTOs.ProductIdentification
{
    /// <summary>
    /// Resultado da identificação de produto.
    /// Contém o código do produto, informações básicas e confiança da identificação.
    /// </summary>
    public class ProductIdentificationResult
    {
        /// <summary>
        /// Indica se a identificação foi bem-sucedida.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Método utilizado para identificação do produto.
        /// </summary>
        public IdentificationMethod Method { get; set; }

        /// <summary>
        /// Nível de confiança da identificação (0.0 a 1.0).
        /// - 1.0: Código de barras lido com sucesso + produto encontrado em base externa
        /// - 0.8-0.9: Código lido mas produto não encontrado em base externa
        /// - 0.5-0.7: Identificação por OCR ou reconhecimento visual
        /// - 0.0-0.4: Baixa confiança ou falha
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Código de barras identificado (EAN-13, UPC, etc.).
        /// </summary>
        public string? Barcode { get; set; }

        /// <summary>
        /// Nome do produto identificado.
        /// </summary>
        public string? ProductName { get; set; }

        /// <summary>
        /// Marca do produto identificado.
        /// </summary>
        public string? Brand { get; set; }

        /// <summary>
        /// Categoria do produto (bebida, alimento processado, etc.).
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Fonte dos dados (ex: "Open Food Facts", "OCR Local", "Manual Entry").
        /// </summary>
        public string? DataSource { get; set; }

        /// <summary>
        /// URL da fonte externa (se aplicável).
        /// </summary>
        public string? ExternalSourceUrl { get; set; }

        /// <summary>
        /// Indica se os dados vieram de uma base externa confiável.
        /// </summary>
        public bool IsFromExternalDatabase { get; set; }

        /// <summary>
        /// Mensagem de erro (se Success = false).
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Detalhes adicionais sobre a identificação (warnings, sugestões, etc.).
        /// </summary>
        public List<string> Details { get; set; } = new();

        /// <summary>
        /// Metadados adicionais do processo de identificação.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>
        /// Tempo de processamento em segundos.
        /// </summary>
        public double ProcessingTimeSeconds { get; set; }

        // ═══════════════════════════════════════════════════════════════════════
        // CAMPOS ADICIONAIS PARA SUPORTE A MATCHING AVANÇADO
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// ID do produto correspondente na base de dados interna (se existir).
        /// </summary>
        public int? MatchedProductId { get; set; }

        /// <summary>
        /// Nome do produto correspondente (pode vir de base externa ou OCR).
        /// </summary>
        public string? MatchedProductName { get; set; }

        /// <summary>
        /// Marca do produto correspondente.
        /// </summary>
        public string? MatchedBrand { get; set; }

        /// <summary>
        /// Confiança do match (0.0 a 1.0).
        /// Diferente de Confidence (que é do método de identificação),
        /// este campo representa a confiança de que o produto identificado
        /// é realmente o produto correto.
        /// </summary>
        public double MatchConfidence { get; set; }

        /// <summary>
        /// Fonte ou origem do match (Barcode, FrontOcr, Similarity, etc.).
        /// </summary>
        public MatchSource MatchSource { get; set; }

        /// <summary>
        /// Lista de candidatos alternativos encontrados durante a busca.
        /// Útil para UI mostrar opções ao usuário caso a confiança seja baixa.
        /// </summary>
        public List<ProductCandidate> TopCandidates { get; set; } = new();

        /// <summary>
        /// Indica se o match é confiável o suficiente para prosseguir
        /// automaticamente sem confirmação do usuário.
        /// Regra: MatchConfidence >= 0.85 e MatchSource = Barcode ou Combined.
        /// </summary>
        public bool IsReliableMatch { get; set; }
    }
}
