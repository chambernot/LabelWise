using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Infrastructure.AI.Parsers;

/// <summary>
/// Lógica de score compartilhada entre todas as estratégias de parsing.
/// </summary>
internal static class NutritionParserScoring
{
    /// <summary>
    /// Calcula score 0–100 baseado em:
    /// - quantidade de campos preenchidos (+10 por campo, máx 70)
    /// - penalidade por inconsistência calórica (-20)
    /// - bônus por ter campos críticos (calories + protein + carbs) (+30)
    /// </summary>
    public static int Calculate(DocumentIntelligenceNutritionResult result)
    {
        if (result is null) return 0;

        int score = 0;

        // +10 por campo preenchido (máx 70)
        score += CountFilledFields(result) * 10;

        // bônus por ter os 3 campos mais críticos
        bool hasCalories = result.Calories?.Value.HasValue == true;
        bool hasProtein  = result.Protein?.Value.HasValue  == true;
        bool hasCarbs    = result.Carbs?.Value.HasValue    == true;

        if (hasCalories && hasProtein && hasCarbs)
            score += 30;

        // penalidade por inconsistência calórica
        if (result.HasCaloriesInconsistency)
            score -= 20;

        return Math.Clamp(score, 0, 100);
    }

    public static int CountFilledFields(DocumentIntelligenceNutritionResult result)
    {
        int count = 0;
        if (result.Calories?.Value.HasValue     == true) count++;
        if (result.Carbs?.Value.HasValue        == true) count++;
        if (result.Sugar?.Value.HasValue        == true) count++;
        if (result.AddedSugar?.Value.HasValue   == true) count++;
        if (result.Protein?.Value.HasValue      == true) count++;
        if (result.Fat?.Value.HasValue          == true) count++;
        if (result.SaturatedFat?.Value.HasValue == true) count++;
        if (result.Sodium?.Value.HasValue       == true) count++;
        if (result.Fiber?.Value.HasValue        == true) count++;
        return count;
    }
}
