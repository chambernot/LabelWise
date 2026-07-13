using System;
using System.Threading.Tasks;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// Serviço para aplicação de fallback nutricional baseado em categoria.
/// </summary>
public interface IDatabaseNutritionFallbackService
{
    /// <summary>
    /// Aplica fallback inteligente usando perfil da categoria já normalizada do banco de dados.
    /// O código da categoria informado deve ser exatamente o código normalizado (ex: achocolatado_po),
    /// e será usado diretamente para buscar o perfil sem re-resolução.
    /// </summary>
    /// <param name="partialNutrition">Dados nutricionais parciais (se houver).</param>
    /// <param name="normalizedCategoryCode">Código da categoria já normalizada pelo CategoryNormalizationService.</param>
    /// <param name="analysisMode">Modo de análise (tabela completa, parcial, etc).</param>
    /// <returns>Perfil nutricional completo com fallback aplicado.</returns>
    Task<DatabaseFallbackResult> ApplyFallbackAsync(
        EstimatedNutritionProfileDto? partialNutrition,
        string? normalizedCategoryCode,
        string analysisMode);
}

/// <summary>
/// Resultado do fallback baseado em banco de dados.
/// </summary>
public class DatabaseFallbackResult
{
    public EstimatedNutritionProfileDto Profile { get; set; } = new();

    /// <summary>
    /// Código da categoria que foi solicitada para o fallback.
    /// </summary>
    public string? RequestedCategoryCode { get; set; }

    /// <summary>
    /// Código da categoria do perfil efetivamente aplicado (pode diferir se houve fallback de hierarquia).
    /// </summary>
    public string? NormalizedCategoryCode { get; set; }
    public string? NormalizedCategoryName { get; set; }
    public double Confidence { get; set; }
    public bool IsFullyEstimated { get; set; }
    public bool IsPartiallyEstimated { get; set; }

    /// <summary>
    /// TRUE se o perfil aplicado veio de uma categoria pai (fallback hierárquico).
    /// </summary>
    public bool UsedParentCategoryFallback { get; set; }

    public System.Collections.Generic.Dictionary<string, string> FallbackSources { get; set; } = new();
    public System.Collections.Generic.List<string> Inconsistencies { get; set; } = new();
    public bool ProfileRejected { get; set; }
}
