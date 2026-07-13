using System;
using System.Collections.Generic;

namespace LabelWise.Application.DTOs.KnownProducts
{
    /// <summary>
    /// Requisição de busca de produtos conhecidos
    /// </summary>
    public class KnownProductSearchRequest
    {
        /// <summary>
        /// Texto de busca (nome, marca, palavras-chave)
        /// </summary>
        public string SearchQuery { get; set; } = string.Empty;

        /// <summary>
        /// Código de barras (busca exata, se fornecido)
        /// </summary>
        public string? Barcode { get; set; }

        /// <summary>
        /// Filtrar por categoria específica
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Filtrar apenas produtos validados
        /// </summary>
        public bool ValidatedOnly { get; set; } = false;

        /// <summary>
        /// Número máximo de resultados
        /// </summary>
        public int MaxResults { get; set; } = 10;

        /// <summary>
        /// Confiança mínima do match (0.0 a 1.0)
        /// </summary>
        public double MinConfidence { get; set; } = 0.0;

        /// <summary>
        /// Idioma para busca (português, inglês, etc.)
        /// </summary>
        public string Language { get; set; } = "pt";

        /// <summary>
        /// Permitir busca fuzzy (tolerância a erros de digitação)
        /// </summary>
        public bool EnableFuzzySearch { get; set; } = true;
    }

    /// <summary>
    /// Resultado de busca de produto conhecido
    /// </summary>
    public class KnownProductSearchResult
    {
        /// <summary>
        /// ID do produto conhecido
        /// </summary>
        public Guid ProductId { get; set; }

        /// <summary>
        /// Nome do produto
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Marca do produto
        /// </summary>
        public string Brand { get; set; } = string.Empty;

        /// <summary>
        /// Categoria do produto
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Código de barras (se disponível)
        /// </summary>
        public string? Barcode { get; set; }

        /// <summary>
        /// Palavras-chave
        /// </summary>
        public string Keywords { get; set; } = string.Empty;

        /// <summary>
        /// Produto validado
        /// </summary>
        public bool IsValidated { get; set; }

        /// <summary>
        /// Número de vezes identificado (popularidade)
        /// </summary>
        public int IdentificationCount { get; set; }

        /// <summary>
        /// Score de relevância da busca (0.0 a 1.0)
        /// </summary>
        public double RelevanceScore { get; set; }

        /// <summary>
        /// Razão do match (por que este produto foi retornado)
        /// </summary>
        public string MatchReason { get; set; } = string.Empty;

        /// <summary>
        /// Fonte do match
        /// </summary>
        public KnownProductMatchSource MatchSource { get; set; }
    }

    /// <summary>
    /// Resposta de busca de produtos conhecidos
    /// </summary>
    public class KnownProductSearchResponse
    {
        /// <summary>
        /// Sucesso da operação
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Mensagem de erro (se houver)
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Resultados encontrados
        /// </summary>
        public List<KnownProductSearchResult> Results { get; set; } = new();

        /// <summary>
        /// Total de resultados encontrados (antes de aplicar MaxResults)
        /// </summary>
        public int TotalMatches { get; set; }

        /// <summary>
        /// Tempo de busca em segundos
        /// </summary>
        public double SearchTimeSeconds { get; set; }

        /// <summary>
        /// Query original
        /// </summary>
        public string OriginalQuery { get; set; } = string.Empty;

        /// <summary>
        /// Sugestão de correção da query (se fuzzy search encontrou algo melhor)
        /// </summary>
        public string? SuggestedQuery { get; set; }
    }

    /// <summary>
    /// Fonte do match de produto conhecido
    /// </summary>
    public enum KnownProductMatchSource
    {
        Barcode,          // Match exato por código de barras
        ExactName,        // Match exato por nome e marca
        FullTextSearch,   // Match por busca textual no catálogo persistido
        FuzzySearch,      // Match por busca fuzzy (tolerância a erros)
        Keywords,         // Match por palavras-chave
        Category          // Match por categoria
    }
}
