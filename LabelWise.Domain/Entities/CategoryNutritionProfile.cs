using System;
using LabelWise.Domain.Common;

namespace LabelWise.Domain.Entities;

/// <summary>
/// Perfil nutricional típico por categoria para fallback inteligente.
/// Versão V2 com gordura saturada, trans e metadados expandidos.
/// </summary>
public class CategoryNutritionProfile : AuditableEntity
{
    public int Id { get; set; }

    /// <summary>
    /// Código da categoria nutricional.
    /// </summary>
    public string CategoryCode { get; set; } = string.Empty;

    // === CALORIAS ===
    public decimal? CaloriesPer100g { get; set; }
    public decimal? CaloriesMin { get; set; }
    public decimal? CaloriesMax { get; set; }

    // === PROTEÍNA ===
    public decimal? ProteinPer100g { get; set; }
    public decimal? ProteinMin { get; set; }
    public decimal? ProteinMax { get; set; }

    // === GORDURA TOTAL ===
    public decimal? FatPer100g { get; set; }
    public decimal? FatMin { get; set; }
    public decimal? FatMax { get; set; }

    // === GORDURA SATURADA (V2) ===
    public decimal? SaturatedFatPer100g { get; set; }
    public decimal? SaturatedFatMin { get; set; }
    public decimal? SaturatedFatMax { get; set; }

    // === GORDURA TRANS (V2) ===
    public decimal? TransFatPer100g { get; set; }
    public decimal? TransFatMin { get; set; }
    public decimal? TransFatMax { get; set; }

    // === CARBOIDRATOS ===
    public decimal? CarbohydratesPer100g { get; set; }
    public decimal? CarbohydratesMin { get; set; }
    public decimal? CarbohydratesMax { get; set; }

    // === AÇÚCAR ===
    public decimal? SugarPer100g { get; set; }
    public decimal? SugarMin { get; set; }
    public decimal? SugarMax { get; set; }

    // === FIBRA ===
    public decimal? FiberPer100g { get; set; }
    public decimal? FiberMin { get; set; }
    public decimal? FiberMax { get; set; }

    // === SÓDIO ===
    public decimal? SodiumPer100g { get; set; }
    public decimal? SodiumMin { get; set; }
    public decimal? SodiumMax { get; set; }

    // === METADADOS ===

    /// <summary>
    /// Nível de confiança do perfil (0.0 a 1.0).
    /// </summary>
    public decimal ConfidenceLevel { get; set; } = 0.70m;

    /// <summary>
    /// Fonte dos dados (TACO, IBGE, Anvisa, USDA, etc).
    /// </summary>
    public string? DataSource { get; set; }

    /// <summary>
    /// Ano de referência dos dados (V2).
    /// </summary>
    public int? ReferenceYear { get; set; }

    /// <summary>
    /// Tamanho da amostra usada para calibrar o perfil (V2).
    /// </summary>
    public int? SampleSize { get; set; }

    /// <summary>
    /// Observações sobre o perfil.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// TRUE para bebidas (valores por 100ml).
    /// </summary>
    public bool IsLiquid { get; set; } = false;

    /// <summary>
    /// Indica se o perfil está ativo.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Versão do perfil para controle de mudanças (V2).
    /// </summary>
    public int Version { get; set; } = 1;

    // === NAVEGAÇÃO ===
    public NutritionCategory? Category { get; set; }
}
