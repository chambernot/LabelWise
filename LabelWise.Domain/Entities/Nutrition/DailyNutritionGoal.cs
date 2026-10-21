using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LabelWise.Domain.Entities.Nutrition;

[BsonIgnoreExtraElements] // Proteção crucial contra crashes de novos campos no MongoDB
public class DailyNutritionGoal
{
    [BsonId]
    public string Id { get; private set; }

    public string UserId { get; private set; }
    public string? NutritionistId { get; private set; } // Identifica o profissional responsável pela prescrição
    public DateTime TargetDate { get; private set; }
    public int TargetCalories { get; private set; }

    [BsonRepresentation(BsonType.Double)]
    public decimal TargetProteinG { get; private set; }

    [BsonRepresentation(BsonType.Double)]
    public decimal TargetCarbsG { get; private set; }

    [BsonRepresentation(BsonType.Double)]
    public decimal TargetFatG { get; private set; }

    // NOVO: Meta de ingestão de água em ml
    public int TargetWaterMl { get; private set; }

    public string? DietaryRestrictions { get; private set; } // Ex: "Sem lactose, Vegetariano, Diabetes"
    public string? FavoriteFoods { get; private set; }       // Ex: "Amo ovos, whey de morango, frutas, castanhas"

    // O cardápio detalhado prescrito pela nutricionista ou gerado autonomamente
    public string? PrescribedMealPlan { get; private set; } // Ex: "Café: Ovos e Pão\nAlmoço: Frango e Arroz..."

    // 🔒 Construtor sem parâmetros exigido pelo MongoDB C# Driver para deserialização
    [BsonConstructor]
    public DailyNutritionGoal() { }

    public DailyNutritionGoal(
        string userId,
        DateTime targetDate,
        int targetCalories,
        decimal targetProteinG,
        decimal targetCarbsG,
        decimal targetFatG,
        int targetWaterMl = 2500, // Valor padrão seguro caso não seja informado
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
        TargetWaterMl = targetWaterMl; // Inicializa a meta de água
        NutritionistId = nutritionistId;
        DietaryRestrictions = dietaryRestrictions;
        FavoriteFoods = favoriteFoods;
        PrescribedMealPlan = prescribedMealPlan;
    }

    // Método para atualizar o cardápio junto com as metas
    public void UpdatePrescribedPlan(string? prescribedPlan)
    {
        PrescribedMealPlan = prescribedPlan;
    }

    // Comportamento de domínio para atualizar as metas caso o nutricionista mude o plano
    public void UpdateGoals(int newCalories, decimal newProtein, decimal newCarbs, decimal newFat, int? newWaterMl = null, string? nutritionistId = null)
    {
        TargetCalories = newCalories;
        TargetProteinG = newProtein;
        TargetCarbsG = newCarbs;
        TargetFatG = newFat;

        if (newWaterMl.HasValue)
        {
            TargetWaterMl = newWaterMl.Value;
        }

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