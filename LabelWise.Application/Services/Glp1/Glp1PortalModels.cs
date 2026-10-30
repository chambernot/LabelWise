namespace LabelWise.Application.Services.Glp1;

// Registros brutos lidos do banco para o portal da nutricionista
public sealed record Glp1MealEntry(DateTime LoggedAt, int Calories, decimal ProteinG, bool IsTreino);
public sealed record Glp1WaterEntry(DateTime LoggedAt, int AmountMl);
public sealed record Glp1WeightEntry(DateTime LoggedAt, decimal WeightKg);

// Resposta de GET /api/nutritionist/glp1/{phone}
public sealed record Glp1PortalSummaryDto(
    string Phone,
    bool Enabled,
    string? Medication,
    string? Dose,
    string? InjectionDay,
    int? ProteinTargetG,
    int? CalorieFloorKcal,
    int? WaterTargetMl,
    bool RemindersOptOut,
    int Days,
    int DaysWithMeals,
    decimal ProteinAvgG,
    int DaysProteinAtLeast80Pct,
    int ProteinAtLeast80PctPercent,
    int DaysWithWater,
    int WaterAvgMl,
    IReadOnlyList<Glp1PortalDayDto> Daily,
    IReadOnlyList<Glp1PortalWeightDto> Weights,
    IReadOnlyList<Glp1PortalAlertDto> Alerts);

public sealed record Glp1PortalDayDto(string Date, decimal ProteinG, int Calories, int Meals, int WaterMl, IReadOnlyList<string> Symptoms, bool HasRedFlag);
public sealed record Glp1PortalWeightDto(string Date, decimal WeightKg);
public sealed record Glp1PortalAlertDto(string Id, string Type, string? Details, DateTime CreatedAt, DateTime? SeenAt);
