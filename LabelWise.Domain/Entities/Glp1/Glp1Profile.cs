using MongoDB.Bson.Serialization.Attributes;
using System;

namespace LabelWise.Domain.Entities.Glp1;

// Perfil do Modo GLP-1. Fica em coleção própria (Glp1Profiles) porque o onboarding B2C
// faz ReplaceOne em Nutrition_Patients e apagaria qualquer campo extra gravado lá.
public class Glp1Profile
{
    [BsonId]
    public string Id { get; set; } = string.Empty; // telefone

    public bool Enabled { get; set; }
    public string? ActivationStep { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ConsentAt { get; set; }
    public string? ConsentVersion { get; set; }

    public bool? HasMedicalFollowUp { get; set; }
    public string? Medication { get; set; }
    public string? Dose { get; set; } // só registro: o bot nunca sugere nem ajusta dose
    public int? InjectionWeekday { get; set; } // 0 = domingo ... 6 = sábado

    public decimal? HeightCm { get; set; }
    public string? Sex { get; set; }

    public int? ProteinTargetG { get; set; }
    public int? CalorieFloorKcal { get; set; }
    public int? WaterTargetMl { get; set; }

    public bool RemindersOptOut { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; }
}
