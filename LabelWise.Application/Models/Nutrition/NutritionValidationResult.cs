using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Models.Nutrition;

/// <summary>
/// Resultado da etapa de validação pura: perfil sanitizado + metadados de inconsistências.
/// Não contém fallback, nível de processamento nem confiança — produzidos pelo Enricher.
/// </summary>
public sealed class NutritionSanitizationResult
{
    /// <summary>Perfil normalizado: valores impossíveis removidos, unidades ajustadas.</summary>
    public EstimatedNutritionProfileDto Profile { get; init; } = new();

    /// <summary>Avisos emitidos durante a sanitização (valores corrigidos, inconsistências).</summary>
    public List<string> Warnings { get; init; } = [];

    /// <summary>
    /// True quando as calorias declaradas são inferiores a 50% do valor esperado pelos macros.
    /// Nunca corrigido automaticamente — apenas sinalizado.
    /// </summary>
    public bool HasCaloriesInconsistency { get; init; }
}
