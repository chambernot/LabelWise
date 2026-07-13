using LabelWise.Application.DTOs.Nutrition;

public static class NutritionNormalizer
{
    public static void NormalizeTo100g(EstimatedNutritionProfileDto profile)
    {
        if (profile == null)
            return;

        if (profile.NutritionUnit == "g" && profile.CaloriesPer100g.HasValue)
            return;

        if (profile.NutritionUnit == "ml" && profile.CaloriesPer100ml.HasValue)
        {
            const double density = 1.0;

            profile.CaloriesPer100g = profile.CaloriesPer100ml / density;

            profile.EstimatedCarbsPer100g = Convert(profile.EstimatedCarbsPer100g, density);
            profile.EstimatedSugarPer100g = Convert(profile.EstimatedSugarPer100g, density);
            profile.EstimatedProteinPer100g = Convert(profile.EstimatedProteinPer100g, density);
            profile.EstimatedFatPer100g = Convert(profile.EstimatedFatPer100g, density);
            profile.EstimatedSaturatedFatPer100g = Convert(profile.EstimatedSaturatedFatPer100g, density);
            profile.EstimatedFiberPer100g = Convert(profile.EstimatedFiberPer100g, density);
            profile.EstimatedSodiumPer100g = Convert(profile.EstimatedSodiumPer100g, density);

            profile.Basis = "Normalizado para 100g (convertido de ml)";
            profile.NutritionUnit = "g";
        }
    }

    private static double? Convert(double? value, double density)
    {
        if (!value.HasValue) return null;
        return value.Value / density;
    }
}