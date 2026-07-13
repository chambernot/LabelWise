using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Models.Nutrition;

/// <summary>
/// Fonte única de verdade para cálculo de completeness de dados nutricionais.
///
/// Substituí todas as implementações duplicadas em:
///   - NutritionController.CountFilledDiFields
///   - NutritionController.CalculateEffectiveCompleteness
///   - NutritionResponseBuilder.CountFilledFields
///   - NutritionScoringService.CalculateDataCompleteness
/// </summary>
public static class NutritionCompletenessCalculator
{
    private const int TotalFields = 8;

    /// <summary>
    /// Retorna um valor de 0 a 100 representando a percentagem de campos
    /// nutricionais preenchidos no perfil (base: 8 campos principais).
    /// </summary>
    public static int Calculate(EstimatedNutritionProfileDto? profile)
    {
        if (profile is null) return 0;

        int filled = 0;

        if (profile.CaloriesPer100g.HasValue)              filled++;
        if (profile.EstimatedCarbsPer100g.HasValue)        filled++;
        if (profile.EstimatedSugarPer100g.HasValue)        filled++;
        if (profile.EstimatedProteinPer100g.HasValue)      filled++;
        if (profile.EstimatedFatPer100g.HasValue)          filled++;
        if (profile.EstimatedSaturatedFatPer100g.HasValue) filled++;
        if (profile.EstimatedSodiumPer100g.HasValue)       filled++;
        if (profile.EstimatedFiberPer100g.HasValue)        filled++;

        return (int)(filled / (double)TotalFields * 100);
    }

    /// <summary>
    /// Retorna true se o perfil atingiu completeness mínima (≥ 50%, ou seja, ≥ 4 campos).
    /// </summary>
    public static bool HasMinimumData(EstimatedNutritionProfileDto? profile)
        => Calculate(profile) >= 50;

    /// <summary>
    /// Retorna true se o perfil contém ao menos 1 campo preenchido.
    /// </summary>
    public static bool HasAnyData(EstimatedNutritionProfileDto? profile)
        => Calculate(profile) > 0;
}
