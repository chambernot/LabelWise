using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;

namespace LabelWise.Infrastructure.Services;

public sealed class NutritionConsistencyValidator : INutritionConsistencyValidator
{
    public NutritionValidationResult Validate(EstimatedNutritionProfileDto profile)
    {
        return Validate(profile, null);
    }

    public NutritionValidationResult Validate(EstimatedNutritionProfileDto profile, string? categoryHint)
    {
        var result = new NutritionValidationResult();

        if (profile == null)
        {
            result.Errors.Add("Perfil nutricional ausente para validação");
            result.IsValid = false;
            return result;
        }

        ValidateImpossibleValues(profile, result);
        ValidateCaloricConsistency(profile, result);
        ValidateMacroPlausibility(profile, result);
        ValidateColumnMixing(profile, result);
        ValidateSodium(profile, result);
        ValidateSugarConsistency(profile, categoryHint, result);

        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    private static void ValidateCaloricConsistency(EstimatedNutritionProfileDto profile, NutritionValidationResult result)
    {
        var calories = profile.CaloriesPer100g ?? profile.CaloriesPer100ml;
        if (!calories.HasValue)
            return;

        if (!profile.EstimatedCarbsPer100g.HasValue ||
            !profile.EstimatedProteinPer100g.HasValue ||
            !profile.EstimatedFatPer100g.HasValue)
        {
            return;
        }

        var expected = (profile.EstimatedCarbsPer100g.Value * 4)
                     + (profile.EstimatedProteinPer100g.Value * 4)
                     + (profile.EstimatedFatPer100g.Value * 9);

        if (expected <= 0)
            return;

        var delta = Math.Abs(calories.Value - expected) / expected;
        if (delta > 0.15)
        {
            result.Warnings.Add("Inconsistência calórica: macros não batem com calorias");
        }
    }

    private static void ValidateImpossibleValues(EstimatedNutritionProfileDto profile, NutritionValidationResult result)
    {
        ValidateNonNegative("Calorias", profile.CaloriesPer100g ?? profile.CaloriesPer100ml, result);
        ValidateNonNegative("Carboidratos", profile.EstimatedCarbsPer100g, result);
        ValidateNonNegative("Gordura", profile.EstimatedFatPer100g, result);
        ValidateNonNegative("Proteína", profile.EstimatedProteinPer100g, result);
        ValidateNonNegative("Açúcar", profile.EstimatedSugarPer100g, result);
        ValidateNonNegative("Sódio", profile.EstimatedSodiumPer100g, result);

        if (profile.EstimatedCarbsPer100g > 100)
            result.Errors.Add("Carboidrato acima do limite plausível (>100g)");

        if (profile.EstimatedFatPer100g > 100)
            result.Errors.Add("Gordura acima do limite plausível (>100g)");

        if (profile.EstimatedProteinPer100g > 100)
            result.Errors.Add("Proteína acima do limite plausível (>100g)");

        if (profile.EstimatedSugarPer100g.HasValue &&
            profile.EstimatedCarbsPer100g.HasValue &&
            profile.EstimatedSugarPer100g > profile.EstimatedCarbsPer100g)
        {
            result.Errors.Add("Açúcar total maior que carboidratos");
        }

        if (profile.EstimatedAddedSugarPer100g.HasValue &&
            profile.EstimatedSugarPer100g.HasValue &&
            profile.EstimatedAddedSugarPer100g > profile.EstimatedSugarPer100g)
        {
            result.Errors.Add("Açúcar adicionado maior que açúcar total");
        }

        if (profile.EstimatedSaturatedFatPer100g.HasValue &&
            profile.EstimatedFatPer100g.HasValue &&
            profile.EstimatedSaturatedFatPer100g > profile.EstimatedFatPer100g)
        {
            result.Errors.Add("Gordura saturada maior que gordura total");
        }
    }

    private static void ValidateMacroPlausibility(EstimatedNutritionProfileDto profile, NutritionValidationResult result)
    {
        var macroSum = (profile.EstimatedCarbsPer100g ?? 0)
            + (profile.EstimatedProteinPer100g ?? 0)
            + (profile.EstimatedFatPer100g ?? 0)
            + (profile.EstimatedFiberPer100g ?? 0)
            + (profile.EstimatedPolyolsPer100g ?? 0);

        if (macroSum > 115)
            result.Errors.Add("Soma nutricional por 100g/ml acima do limite plausível");
        else if (macroSum > 100)
            result.Warnings.Add("Soma de nutrientes próxima/acima de 100g; possível erro de OCR ou mistura de colunas");

        if (profile.ServingAmount is <= 0)
            result.Errors.Add("Porção com valor inválido");

        if (profile.ServingAmount > 1000)
            result.Warnings.Add("Porção declarada fora da faixa usual; confirmar leitura OCR");
    }

    private static void ValidateColumnMixing(EstimatedNutritionProfileDto profile, NutritionValidationResult result)
    {
        var calories = profile.CaloriesPer100g ?? profile.CaloriesPer100ml;
        var carbs = profile.EstimatedCarbsPer100g;
        var protein = profile.EstimatedProteinPer100g;
        var fat = profile.EstimatedFatPer100g;

        if (!calories.HasValue)
            return;

        if (calories < 100 && ((carbs ?? 0) > 50 || (protein ?? 0) > 20 || (fat ?? 0) > 20))
        {
            result.Warnings.Add("Possível mistura de colunas (100g vs porção)");
            return;
        }

        var macroSum = (carbs ?? 0) + (protein ?? 0) + (fat ?? 0);
        if (calories > 450 && macroSum < 20)
        {
            result.Warnings.Add("Possível mistura de colunas (100g vs porção)");
        }
    }

    private static void ValidateSodium(EstimatedNutritionProfileDto profile, NutritionValidationResult result)
    {
        if (!profile.EstimatedSodiumPer100g.HasValue)
            return;

        if (profile.EstimatedSodiumPer100g > 5000)
        {
            result.Errors.Add("Sódio acima do limite plausível (>5000mg)");
            return;
        }

        if (profile.EstimatedSodiumPer100g < 1)
        {
            result.Warnings.Add("Sódio muito baixo para leitura real (suspeito)");
        }
    }

    private static void ValidateSugarConsistency(EstimatedNutritionProfileDto profile, string? categoryHint, NutritionValidationResult result)
    {
        if (profile.EstimatedSugarPer100g != 0)
            return;

        var context = $"{categoryHint} {profile.Basis}".ToLowerInvariant();
        var expectedSugaryCategory = context.Contains("biscoit")
            || context.Contains("achocolat")
            || context.Contains("chocolate")
            || context.Contains("sobremesa")
            || context.Contains("refrigerante");

        if (expectedSugaryCategory)
        {
            result.Warnings.Add("Açúcar zerado para categoria tipicamente açucarada (suspeito)");
        }
    }

    private static void ValidateNonNegative(string nutrientName, double? value, NutritionValidationResult result)
    {
        if (value.HasValue && value.Value < 0)
        {
            result.Errors.Add($"{nutrientName} com valor negativo");
        }
    }
}
