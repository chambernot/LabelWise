using System;

namespace LabelWise.Domain.Entities.Nutrition;

public class DailyNutritionGoal
{
    public string Id { get; private set; }
    public string UserId { get; private set; }
    public string? NutritionistId { get; private set; } // Identifica o profissional responsável pela prescrição
    public DateTime TargetDate { get; private set; }
    public int TargetCalories { get; private set; }
    public decimal TargetProteinG { get; private set; }
    public decimal TargetCarbsG { get; private set; }
    public decimal TargetFatG { get; private set; }
    public string? DietaryRestrictions { get; private set; } // Ex: "Sem lactose, Vegetariano, Diabetes"
    public string? FavoriteFoods { get; private set; }       // Ex: "Amo ovos, whey de morango, frutas, castanhas"
    // NOVO: O cardápio detalhado prescrito pela nutricionista
    public string? PrescribedMealPlan { get; private set; } // Ex: "Café: Ovos e Pão\nAlmoço: Frango e Arroz..."

    // Método para atualizar o cardápio junto com as metas
    public void UpdatePrescribedPlan(string? prescribedPlan)
    {
        PrescribedMealPlan = prescribedPlan;
    }
    protected DailyNutritionGoal() { }

    public DailyNutritionGoal(
        string userId,
        DateTime targetDate,
        int targetCalories,
        decimal targetProteinG,
        decimal targetCarbsG,
        decimal targetFatG,
        string? nutritionistId = null,
        string? dietaryRestrictions = null,
        string? favoriteFoods = null,
        string? prescribedMealPlan = null)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("UserId é obrigatório.");

        Id = Guid.NewGuid().ToString();
        UserId = userId;
        TargetDate = targetDate.Date; // Garante que a hora seja 00:00:00
        TargetCalories = targetCalories;
        TargetProteinG = targetProteinG;
        TargetCarbsG = targetCarbsG;
        TargetFatG = targetFatG;
        NutritionistId = nutritionistId;
        DietaryRestrictions = dietaryRestrictions;
        FavoriteFoods = favoriteFoods;
        PrescribedMealPlan = prescribedMealPlan;
    }

    // Comportamento de domínio para atualizar as metas caso o nutricionista mude o plano
    public void UpdateGoals(int newCalories, decimal newProtein, decimal newCarbs, decimal newFat, string? nutritionistId = null)
    {
        TargetCalories = newCalories;
        TargetProteinG = newProtein;
        TargetCarbsG = newCarbs;
        TargetFatG = newFat;

        if (!string.IsNullOrWhiteSpace(nutritionistId))
        {
            NutritionistId = nutritionistId;
        }
    }

    // Comportamento de domínio para atualizar restrições e preferências
    public void UpdatePreferences(string? dietaryRestrictions, string? favoriteFoods)
    {
        DietaryRestrictions = dietaryRestrictions;
        FavoriteFoods = favoriteFoods;
    }
}