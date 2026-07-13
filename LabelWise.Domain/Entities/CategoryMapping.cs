using System;
using LabelWise.Domain.Common;

namespace LabelWise.Domain.Entities;

/// <summary>
/// Mapeamento de nomes detectados pela IA para categorias normalizadas.
/// </summary>
public class CategoryMapping : AuditableEntity
{
    public int Id { get; set; }
    
    /// <summary>
    /// Nome detectado pela IA (ex: "creme de queijo light").
    /// </summary>
    public string RawCategoryName { get; set; } = string.Empty;
    
    /// <summary>
    /// Código da categoria normalizada correspondente.
    /// </summary>
    public string NormalizedCategoryCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Confiança do mapeamento (0.0 a 1.0).
    /// </summary>
    public decimal Confidence { get; set; } = 1.00m;
    
    /// <summary>
    /// Indica se o mapeamento está ativo.
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    // === NAVEGAÇÃO ===
    public NutritionCategory? NormalizedCategory { get; set; }
}
