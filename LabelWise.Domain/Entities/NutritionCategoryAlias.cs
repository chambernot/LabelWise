using System;
using LabelWise.Domain.Common;

namespace LabelWise.Domain.Entities;

/// <summary>
/// Mapeamento de nomes detectados pela IA para categorias normalizadas.
/// Versão V2 com normalização automática e machine learning features.
/// </summary>
public class NutritionCategoryAlias : AuditableEntity
{
    public int Id { get; set; }
    
    /// <summary>
    /// Código da categoria normalizada.
    /// </summary>
    public string CategoryCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Nome original detectado (ex: "Creme de Queijo Light").
    /// </summary>
    public string AliasName { get; set; } = string.Empty;
    
    /// <summary>
    /// Nome normalizado para busca (lowercase, sem acentos).
        /// Preenchido automaticamente pela camada de persistência.
    /// </summary>
    public string AliasNameNormalized { get; set; } = string.Empty;
    
    /// <summary>
    /// Confiança do mapeamento (0.0 a 1.0).
    /// </summary>
    public decimal Confidence { get; set; } = 1.00m;
    
    /// <summary>
    /// Tipo de match: exact, partial, fuzzy.
    /// </summary>
    public string MatchType { get; set; } = "exact";
    
    /// <summary>
    /// Indica se o mapeamento está ativo.
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Contador de uso para machine learning.
    /// </summary>
    public int UsageCount { get; set; } = 0;
    
    // Navegação
    public NutritionCategory? Category { get; set; }
}
