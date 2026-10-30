using LabelWise.Domain.Entities.Glp1;

namespace LabelWise.Application.Services.Glp1;

// Monta o resumo GLP-1 de um paciente para o portal da nutricionista (sem banco: recebe os registros já lidos).
// Médias de proteína e água consideram só os dias com registro, para não punir quem esqueceu de registrar.
public static class Glp1PortalSummaryBuilder
{
    public static readonly int[] PeriodosPermitidos = { 7, 30, 90 };
    public const decimal FracaoProteinaDiaBom = 0.8m;

    private static readonly string[] DiasDaSemana = { "domingo", "segunda", "terça", "quarta", "quinta", "sexta", "sábado" };

    // Primeiro instante (UTC) do período: N dias de Brasília contando hoje
    public static DateTime InicioDoPeriodoUtc(DateTime agoraUtc, int dias) =>
        DateTime.SpecifyKind(agoraUtc.AddHours(-3).Date.AddDays(-(dias - 1)).AddHours(3), DateTimeKind.Utc);

    public static Glp1PortalSummaryDto Montar(
        Glp1Profile perfil,
        int dias,
        DateTime agoraUtc,
        IReadOnlyList<Glp1MealEntry> refeicoes,
        IReadOnlyList<Glp1WaterEntry> agua,
        IReadOnlyList<Glp1WeightEntry> pesos,
        IReadOnlyList<Glp1SymptomLog> sintomas,
        IReadOnlyList<Glp1Alert> alertas)
    {
        static string Dia(DateTime utc) => utc.AddHours(-3).ToString("yyyy-MM-dd");

        var hojeBr = agoraUtc.AddHours(-3).Date;
        var datas = Enumerable.Range(0, dias).Select(i => hojeBr.AddDays(-(dias - 1 - i)).ToString("yyyy-MM-dd")).ToList();

        var refeicoesPorDia = refeicoes.Where(r => !r.IsTreino).GroupBy(r => Dia(r.LoggedAt)).ToDictionary(g => g.Key, g => g.ToList());
        var aguaPorDia = agua.GroupBy(a => Dia(a.LoggedAt)).ToDictionary(g => g.Key, g => g.Sum(x => x.AmountMl));
        var sintomasPorDia = sintomas.GroupBy(s => Dia(s.LoggedAt)).ToDictionary(g => g.Key, g => g.ToList());

        var diario = datas.Select(data =>
        {
            refeicoesPorDia.TryGetValue(data, out var doDia);
            sintomasPorDia.TryGetValue(data, out var sintomasDoDia);
            return new Glp1PortalDayDto(
                data,
                doDia?.Sum(r => r.ProteinG) ?? 0,
                doDia?.Sum(r => Math.Max(0, r.Calories)) ?? 0,
                doDia?.Count ?? 0,
                aguaPorDia.TryGetValue(data, out var ml) ? ml : 0,
                (sintomasDoDia ?? new List<Glp1SymptomLog>()).Select(s => Glp1SymptomDetector.Nome(s.Symptom)).Distinct().ToList(),
                sintomasDoDia?.Any(s => s.IsRedFlag) ?? false);
        }).ToList();

        var diasComRefeicao = diario.Where(d => d.Meals > 0).ToList();
        var meta = perfil.ProteinTargetG ?? 0;
        int diasProteinaBoa = meta > 0 ? diasComRefeicao.Count(d => d.ProteinG >= meta * FracaoProteinaDiaBom) : 0;

        var diasComAgua = diario.Where(d => d.WaterMl > 0).ToList();

        return new Glp1PortalSummaryDto(
            perfil.Id,
            perfil.Enabled,
            perfil.Medication,
            perfil.Dose,
            perfil.InjectionWeekday is >= 0 and <= 6 ? DiasDaSemana[perfil.InjectionWeekday.Value] : null,
            perfil.ProteinTargetG,
            perfil.CalorieFloorKcal,
            perfil.WaterTargetMl,
            perfil.RemindersOptOut,
            dias,
            diasComRefeicao.Count,
            diasComRefeicao.Count > 0 ? Math.Round(diasComRefeicao.Average(d => d.ProteinG), 1) : 0,
            diasProteinaBoa,
            diasComRefeicao.Count > 0 ? (int)Math.Round(100m * diasProteinaBoa / diasComRefeicao.Count) : 0,
            diasComAgua.Count,
            diasComAgua.Count > 0 ? (int)Math.Round(diasComAgua.Average(d => (decimal)d.WaterMl)) : 0,
            diario,
            pesos.OrderBy(p => p.LoggedAt).Select(p => new Glp1PortalWeightDto(Dia(p.LoggedAt), p.WeightKg)).ToList(),
            alertas.OrderByDescending(a => a.CreatedAt)
                .Select(a => new Glp1PortalAlertDto(a.Id, a.Type.ToString(), a.Details, a.CreatedAt, a.SeenByNutritionistAt))
                .ToList());
    }

    // Carteira: a chave mestre vê todos; as demais nutricionistas só os pacientes com meta (DailyGoals) cadastrada por elas
    public static bool PodeVerPaciente(bool chaveMestre, string nutricionistaId, IEnumerable<string?> nutricionistasDoPaciente) =>
        chaveMestre || nutricionistasDoPaciente.Any(id => !string.IsNullOrEmpty(id) && id == nutricionistaId);
}
