namespace LabelWise.Application.Services.Glp1;

// Consumo do dia (Brasília) de quem está com o Modo GLP-1 ativo, mais as metas do Glp1Profiles.
// CaloriesTodayKcal e MealsToday contam só refeições (treinos ficam de fora).
public sealed record Glp1DayContext(
    int ProteinTargetG,
    int CalorieFloorKcal,
    int WaterTargetMl,
    decimal ProteinTodayG,
    int CaloriesTodayKcal,
    int MealsToday,
    int WaterTodayMl,
    DateTime NowUtc)
{
    // Depois das 18h de Brasília, comer abaixo do piso calórico gera o aviso de "comeu pouco"
    public const int HoraAvisoIngestaoBaixa = 18;

    public decimal ProteinRemainingG => Math.Max(0, ProteinTargetG - ProteinTodayG);

    public bool IngestaoBaixaNoFimDoDia =>
        NowUtc.AddHours(-3).Hour >= HoraAvisoIngestaoBaixa && CaloriesTodayKcal < CalorieFloorKcal;
}

public sealed record Glp1DayIntake(decimal ProteinG, int CaloriesKcal, int Meals);
