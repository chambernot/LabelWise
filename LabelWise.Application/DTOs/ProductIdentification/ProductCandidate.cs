using LabelWise.Domain.Enums;

namespace LabelWise.Application.DTOs.ProductIdentification
{
    /// <summary>
    /// Representa um candidato de produto identificado durante o processo de busca.
    /// Usado quando há múltiplas possibilidades de match.
    /// </summary>
    public class ProductCandidate
    {
        /// <summary>
        /// ID do produto na base de dados (se existir).
        /// </summary>
        public int? ProductId { get; set; }

        /// <summary>
        /// Código de barras do produto candidato.
        /// </summary>
        public string? Barcode { get; set; }

        /// <summary>
        /// Nome do produto candidato.
        /// </summary>
        public required string ProductName { get; set; }

        /// <summary>
        /// Marca do produto candidato.
        /// </summary>
        public string? Brand { get; set; }

        /// <summary>
        /// Categoria do produto candidato.
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Score de confiança deste candidato (0.0 a 1.0).
        /// </summary>
        public double ConfidenceScore { get; set; }

        /// <summary>
        /// Fonte de onde este candidato foi obtido.
        /// </summary>
        public MatchSource MatchSource { get; set; }

        /// <summary>
        /// Razão pela qual este candidato foi selecionado ou sugerido.
        /// </summary>
        public string? MatchReason { get; set; }

        /// <summary>
        /// Fonte dos dados (ex: "Open Food Facts", "Base Interna").
        /// </summary>
        public string? DataSource { get; set; }

        /// <summary>
        /// URL externa (se aplicável).
        /// </summary>
        public string? ExternalUrl { get; set; }

        /// <summary>
        /// Metadados adicionais sobre o candidato.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
