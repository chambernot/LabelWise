using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace LabelWise.Domain.Entities.Glp1;

public enum Glp1Symptom
{
    Nausea,
    Vomito,
    Constipacao,
    Diarreia,
    Refluxo,
    SemApetite,
    Outro
}

// Registro de sintoma (coleção Glp1SymptomLogs). Não guarda o texto original da mensagem.
public class Glp1SymptomLog
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string UserId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public Glp1Symptom Symptom { get; set; }

    public bool IsRedFlag { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime LoggedAt { get; set; }
}
