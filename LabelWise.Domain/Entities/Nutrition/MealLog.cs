using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace LabelWise.Domain.Entities.Nutrition;

public class MealLog
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString(); // ◄ Removido o BsonRepresentation(BsonType.ObjectId)

    public string UserId { get; set; }
    public string MealType { get; set; }
    public string DishName { get; set; }
    public int Calories { get; set; }
    public decimal ProteinG { get; set; }
    public decimal CarbsG { get; set; }
    public decimal FatG { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime LoggedAt { get; set; }

    // Construtor vazio obrigatório para o MongoDB desserializar
    public MealLog() { }

    // Construtor utilitário
    public MealLog(string userId, string mealType, string dishName, int calories, decimal proteinG, decimal carbsG, decimal fatG, DateTime loggedAt)
    {
        Id = Guid.NewGuid().ToString();
        UserId = userId;
        MealType = mealType;
        DishName = dishName;
        Calories = calories;
        ProteinG = proteinG;
        CarbsG = carbsG;
        FatG = fatG;
        LoggedAt = loggedAt;
    }
}