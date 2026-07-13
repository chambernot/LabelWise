using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Models.Nutrition;

public static class NutritionProfileEvaluator
{
    public static UserProfileInsightsDto Evaluate(EstimatedNutritionProfileDto? nutrition)
    {
        var sugar  = nutrition?.EstimatedSugarPer100g ?? 0;
        var sodium = nutrition?.EstimatedSodiumPer100g;  // null = não medido
        var calories = nutrition?.CaloriesPer100g ?? nutrition?.CaloriesPer100ml ?? 0;
        var protein  = nutrition?.EstimatedProteinPer100g ?? 0;

        return new UserProfileInsightsDto
        {
            Diabetic     = EvaluateDiabetic(sugar),
            Hypertension = EvaluateHypertension(sodium),
            WeightLoss   = EvaluateWeightLoss(calories),
            MuscleGain   = EvaluateMuscleGain(protein)
        };
    }

    private static string EvaluateDiabetic(double sugar)
    {
        if (sugar > 15)
            return "Evitar: alto teor de açúcar";

        if (sugar > 5)
            return "Consumir com moderação: contém açúcar";

        return "Baixo teor de açúcar";
    }

    private static string EvaluateHypertension(double? sodium)
    {
        if (!sodium.HasValue)
            return "Sódio não informado";

        if (sodium > 600)
            return "Evitar: alto teor de sódio";

        if (sodium > 300)
            return "Consumir com moderação: contém sódio";

        return "Baixo teor de sódio";
    }

    private static string EvaluateWeightLoss(double calories)
    {
        if (calories > 400)
            return "Alta densidade calórica";

        if (calories > 250)
            return "Consumir com moderação";

        return "Baixa densidade calórica";
    }

    private static string EvaluateMuscleGain(double protein)
    {
        if (protein > 15)
            return "Boa fonte de proteína";

        if (protein > 8)
            return "Proteína moderada";

        return "Baixo teor proteico";
    }
}
