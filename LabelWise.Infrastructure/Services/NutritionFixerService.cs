using LabelWise.Application.Interfaces;

namespace LabelWise.Infrastructure.Services;

/// <summary>
/// Corrige gordura saturada e calorias usando coerência matemática.
/// Nunca sobrescreve um valor válido com estimativas — toda correção gera warning.
/// </summary>
public sealed class NutritionFixerService : INutritionFixerService
{
    public double? FixSaturatedFat(
        double? fat,
        double? saturatedFat,
        IReadOnlyList<double> candidates,
        List<string> warnings)
    {
        if (fat == null) return saturatedFat;

        if (saturatedFat == null || saturatedFat < fat * 0.3)
        {
            if (candidates != null && candidates.Count > 0)
            {
                var best = candidates.Max();
                if (best >= fat * 0.5 && best <= fat)
                {
                    warnings.Add("Saturated fat corrected from OCR");
                    return best;
                }
            }

            if (fat >= 40)
            {
                var estimated = Math.Round(fat.Value * 0.85, 1);
                warnings.Add("Saturated fat estimated based on high fat product");
                return estimated;
            }
        }

        return saturatedFat;
    }

    public double FixCalories(
        double? fat,
        double? carbs,
        double? protein,
        double? calories,
        List<string> warnings)
    {
        if (fat == null || carbs == null || protein == null)
            return calories ?? 0;

        var calc = fat.Value * 9 + carbs.Value * 4 + protein.Value * 4;

        if (calories == null || Math.Abs(calories.Value - calc) > 100)
        {
            warnings.Add("Calories corrected from macros");
            return Math.Round(calc);
        }

        return calories.Value;
    }
}
