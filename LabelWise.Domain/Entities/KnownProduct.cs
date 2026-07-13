using System;
using LabelWise.Domain.Common;

namespace LabelWise.Domain.Entities
{
    /// <summary>
    /// Catálogo de produtos conhecidos para busca e identificação.
    /// Usado como fallback quando OCR/IA não identificam o produto com confiança suficiente.
    /// 
    /// PROPÓSITO:
    /// - Permitir busca textual aproximada em produtos conhecidos
    /// - Servir como base para ranking de relevância
    /// - Preparar migração futura para Azure AI Search ou pgvector
    /// </summary>
    public class KnownProduct : AuditableEntity
    {
        /// <summary>
        /// Nome comercial do produto
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Marca do produto
        /// </summary>
        public string Brand { get; set; } = string.Empty;

        /// <summary>
        /// Categoria do produto (ex: "Biscoito", "Suco", "Iogurte")
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Código de barras (EAN-13, UPC, etc.)
        /// </summary>
        public string? Barcode { get; set; }

        /// <summary>
        /// Texto conhecido da embalagem frontal (para matching)
        /// </summary>
        public string? KnownFrontText { get; set; }

        /// <summary>
        /// Lista de ingredientes conhecida (separada por vírgula)
        /// </summary>
        public string? KnownIngredients { get; set; }

        /// <summary>
        /// Lista de alérgenos conhecidos (separada por vírgula)
        /// </summary>
        public string? KnownAllergens { get; set; }

        /// <summary>
        /// Palavras-chave para busca (separadas por espaço)
        /// Inclui variações de nome, marca, apelidos populares
        /// </summary>
        public string Keywords { get; set; } = string.Empty;

        /// <summary>
        /// Indica se este produto foi validado manualmente ou provém de fonte confiável
        /// </summary>
        public bool IsValidated { get; set; }

        /// <summary>
        /// Contagem de vezes que este produto foi identificado
        /// Usado para ranking de popularidade
        /// </summary>
        public int IdentificationCount { get; set; }

        /// <summary>
        /// Última vez que este produto foi identificado
        /// </summary>
        public DateTimeOffset? LastIdentifiedAt { get; set; }

        /// <summary>
        /// Texto normalizado para busca full-text (calculado automaticamente)
        /// Combina Name, Brand, Keywords para indexação e busca no catálogo.
        /// </summary>
        public string SearchText { get; private set; } = string.Empty;

        public KnownProduct()
        {
            UpdateSearchText();
        }

        /// <summary>
        /// Atualiza o texto de busca combinando campos relevantes
        /// Deve ser chamado sempre que Name, Brand ou Keywords mudarem
        /// </summary>
        public void UpdateSearchText()
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(Name))
                parts.Add(Name);

            if (!string.IsNullOrWhiteSpace(Brand))
                parts.Add(Brand);

            if (!string.IsNullOrWhiteSpace(Keywords))
                parts.Add(Keywords);

            if (!string.IsNullOrWhiteSpace(Category))
                parts.Add(Category);

            SearchText = string.Join(" ", parts).ToLowerInvariant();
        }

        /// <summary>
        /// Registra uma nova identificação deste produto
        /// </summary>
        public void RecordIdentification()
        {
            IdentificationCount++;
            LastIdentifiedAt = DateTimeOffset.UtcNow;
            SetUpdated();
        }

        /// <summary>
        /// Marca o produto como validado
        /// </summary>
        public void MarkAsValidated()
        {
            IsValidated = true;
            SetUpdated();
        }
    }
}
