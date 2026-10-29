using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace LabelWise.Domain.Entities.Glp1;

public enum Glp1AlertType
{
    IngestaoBaixa,
    SinalAlerta
}

// Alerta para a nutricionista (coleção Glp1Alerts)
public class Glp1Alert
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string UserId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public Glp1AlertType Type { get; set; }

    public string? Details { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? SeenByNutritionistAt { get; set; }
}
