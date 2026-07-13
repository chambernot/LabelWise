using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Infrastructure.Services;

/// <summary>
/// Calcula confiança da extração nutricional pelo número de campos presentes,
/// penalizando quando há warnings de correção.
/// </summary>
public sealed class ConfidenceService : IConfidenceService
{
    public string Calculate(NutritionProfile profile, IReadOnlyList<string> warnings)
    {
        int score = 0;

        if (profile.Calories != null)     score++;
        if (profile.Carbs != null)        score++;
        if (profile.Fat != null)          score++;
        if (profile.Protein != null)      score++;
        if (profile.SaturatedFat != null) score++;
        if (profile.Sugar != null)        score++;
        if (profile.Sodium != null)       score++;

        if (warnings.Count > 0) score--;

        return score switch
        {
            >= 6 => "alta",
            >= 4 => "media",
            _    => "baixa"
        };
    }
}
