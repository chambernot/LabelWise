namespace LabelWise.Domain.Entities.Nutrition;

public class DailyNutritionGoal
{
    public string? DietaryRestrictions { get; set; } // Ex: "Sem lactose, Vegetariano, Diabetes"
    public string? FavoriteFoods { get; set; }       // Ex: "Amo ovos, whey de morango, frutas, castanhas"
    public string Id { get; private set; }
    public string UserId { get; private set; }
    public DateTime TargetDate { get; private set; }
    public int TargetCalories { get; private set; }
    public decimal TargetProteinG { get; private set; }
    public decimal TargetCarbsG { get; private set; }
    public decimal TargetFatG { get; private set; }

    protected DailyNutritionGoal() { }

    public DailyNutritionGoal(string userId, DateTime targetDate, int targetCalories, decimal targetProteinG, decimal targetCarbsG, decimal targetFatG)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("UserId é obrigatório.");

        Id = Guid.NewGuid().ToString();
        UserId = userId;
        TargetDate = targetDate.Date; // Garante que a hora seja 00:00:00
        TargetCalories = targetCalories;
        TargetProteinG = targetProteinG;
        TargetCarbsG = targetCarbsG;
        TargetFatG = targetFatG;
    }

    // Comportamento de domínio para atualizar as metas caso o usuário mude de plano
    public void UpdateGoals(int newCalories, decimal newProtein, decimal newCarbs, decimal newFat)
    {
        TargetCalories = newCalories;
        TargetProteinG = newProtein;
        TargetCarbsG = newCarbs;
        TargetFatG = newFat;
    }
}