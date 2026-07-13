using System;
using LabelWise.Domain.Common;

namespace LabelWise.Domain.Entities;

/// <summary>
/// Categoria nutricional normalizada para classificação de alimentos.
/// Versão V2 com hierarquia e display_order.
/// </summary>
public class NutritionCategory : AuditableEntity
{
    public int Id { get; set; }

    /// <summary>
    /// Código único da categoria (ex: laticinio_cremoso).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Nome descritivo da categoria.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Descrição detalhada da categoria.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Código da categoria pai para hierarquia (ex: laticinio -> laticinio_cremoso).
    /// </summary>
    public string? ParentCode { get; set; }

    /// <summary>
    /// Indica se a categoria está ativa.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Ordem de exibição em listagens.
    /// </summary>
    public int DisplayOrder { get; set; } = 0;

    // Navegação
    public NutritionCategory? ParentCategory { get; set; }
}
