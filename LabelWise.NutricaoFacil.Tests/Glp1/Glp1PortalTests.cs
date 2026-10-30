using LabelWise.Api.Controllers;
using LabelWise.Application.Services.Glp1;
using LabelWise.Domain.Entities.Glp1;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;

namespace LabelWise.NutricaoFacil.Tests.Glp1;

public class Glp1PortalTests
{
    private const string Telefone = "5511999990000";
    private const string ChaveMestre = "chave-mestre-teste";

    // Quinta, 29/10/2026 às 15h de Brasília
    private static readonly DateTime Agora = new(2026, 10, 29, 18, 0, 0, DateTimeKind.Utc);
    private static DateTime DiaBr(int diasAtras, int hora = 12) => DateTime.SpecifyKind(Agora.AddHours(-3).Date.AddDays(-diasAtras).AddHours(hora + 3), DateTimeKind.Utc);

    private readonly FakeGlp1Repository _repository = new();

    private static Glp1Profile Perfil() => new()
    {
        Id = Telefone,
        Enabled = true,
        Medication = "Tirzepatida",
        Dose = "5 mg",
        InjectionWeekday = 1,
        ProteinTargetG = 100,
        CalorieFloorKcal = 1200,
        WaterTargetMl = 2500
    };

    // ================================================================ resumo (classe pura)

    [Fact]
    public void Resumo_CalculaMediasEPercentualDeProteina()
    {
        var refeicoes = new List<Glp1MealEntry>
        {
            new(DiaBr(0, 8), 400, 50, false), new(DiaBr(0, 13), 500, 40, false), // hoje: 90 g (≥ 80)
            new(DiaBr(1), 600, 60, false),                                       // ontem: 60 g
            new(DiaBr(3), 700, 85, false),                                       // 85 g (≥ 80)
            new(DiaBr(3, 18), -300, 0, true)                                     // treino não conta
        };
        var agua = new List<Glp1WaterEntry> { new(DiaBr(0), 1500), new(DiaBr(0, 15), 500), new(DiaBr(2), 1000) };
        var pesos = new List<Glp1WeightEntry> { new(DiaBr(5), 82.5m), new(DiaBr(0), 81.9m) };
        var sintomas = new List<Glp1SymptomLog>
        {
            new() { UserId = Telefone, Symptom = Glp1Symptom.Nausea, LoggedAt = DiaBr(1) },
            new() { UserId = Telefone, Symptom = Glp1Symptom.Nausea, LoggedAt = DiaBr(1, 15) },
            new() { UserId = Telefone, Symptom = Glp1Symptom.Vomito, IsRedFlag = true, LoggedAt = DiaBr(1, 16) }
        };
        var alertas = new List<Glp1Alert> { new() { Id = "a1", UserId = Telefone, Type = Glp1AlertType.SinalAlerta, Details = "sangue", CreatedAt = DiaBr(1) } };

        var resumo = Glp1PortalSummaryBuilder.Montar(Perfil(), 7, Agora, refeicoes, agua, pesos, sintomas, alertas);

        Assert.Equal(7, resumo.Daily.Count);
        Assert.Equal("2026-10-29", resumo.Daily[^1].Date);
        Assert.Equal("2026-10-23", resumo.Daily[0].Date);

        Assert.Equal(3, resumo.DaysWithMeals);
        Assert.Equal(78.3m, resumo.ProteinAvgG);           // (90 + 60 + 85) / 3
        Assert.Equal(2, resumo.DaysProteinAtLeast80Pct);
        Assert.Equal(67, resumo.ProteinAtLeast80PctPercent);

        Assert.Equal(2, resumo.DaysWithWater);
        Assert.Equal(1500, resumo.WaterAvgMl);              // (2000 + 1000) / 2

        var hoje = resumo.Daily[^1];
        Assert.Equal(90, hoje.ProteinG);
        Assert.Equal(900, hoje.Calories);
        Assert.Equal(2, hoje.Meals);

        var ontem = resumo.Daily[^2];
        Assert.Equal(new[] { "enjoo", "vômito" }, ontem.Symptoms);
        Assert.True(ontem.HasRedFlag);

        Assert.Equal(700, resumo.Daily[^4].Calories); // o treino (-300 kcal) do mesmo dia não entra
        Assert.Equal(1, resumo.Daily[^4].Meals);

        Assert.Equal(new[] { 82.5m, 81.9m }, resumo.Weights.Select(p => p.WeightKg));
        Assert.Equal("a1", Assert.Single(resumo.Alerts).Id);

        Assert.Equal("Tirzepatida", resumo.Medication);
        Assert.Equal("5 mg", resumo.Dose);       // a nutricionista vê a dose informada
        Assert.Equal("segunda", resumo.InjectionDay);
    }

    [Fact]
    public void Resumo_SemRegistros_ZerosSemErro()
    {
        var resumo = Glp1PortalSummaryBuilder.Montar(Perfil(), 30, Agora,
            new List<Glp1MealEntry>(), new List<Glp1WaterEntry>(), new List<Glp1WeightEntry>(), new List<Glp1SymptomLog>(), new List<Glp1Alert>());

        Assert.Equal(30, resumo.Daily.Count);
        Assert.Equal(0, resumo.ProteinAvgG);
        Assert.Equal(0, resumo.ProteinAtLeast80PctPercent);
        Assert.Equal(0, resumo.WaterAvgMl);
    }

    [Fact]
    public void InicioDoPeriodo_ContaHojeComoUmDosDias()
    {
        Assert.Equal(new DateTime(2026, 10, 23, 3, 0, 0, DateTimeKind.Utc), Glp1PortalSummaryBuilder.InicioDoPeriodoUtc(Agora, 7));
    }

    // ================================================================ carteira

    [Fact]
    public void Carteira_ChaveMestreVeTodos()
    {
        Assert.True(Glp1PortalSummaryBuilder.PodeVerPaciente(true, "qualquer", Array.Empty<string>()));
    }

    [Fact]
    public void Carteira_NutricionistaSoVeOsProprios()
    {
        Assert.True(Glp1PortalSummaryBuilder.PodeVerPaciente(false, "nutri-1", new[] { "outra", "nutri-1" }));
        Assert.False(Glp1PortalSummaryBuilder.PodeVerPaciente(false, "nutri-1", new[] { "outra" }));
        Assert.False(Glp1PortalSummaryBuilder.PodeVerPaciente(false, "nutri-1", Array.Empty<string>()));
        Assert.False(Glp1PortalSummaryBuilder.PodeVerPaciente(false, "nutri-1", new string?[] { null }));
    }

    // ================================================================ acesso ao endpoint

    private NutritionistPortalController Controller()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["NutritionistDefaultKey"] = ChaveMestre })
            .Build();

        // Banco inexistente com timeout curto: qualquer consulta falha rápido (a chave mestre não consulta o banco)
        var database = new MongoClient("mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=300&connectTimeoutMS=300").GetDatabase("teste");

        return new NutritionistPortalController(null!, database, config, NullLogger<NutritionistPortalController>.Instance, null!, _repository);
    }

    private static int Status(IActionResult resultado) => resultado switch
    {
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        _ => throw new InvalidOperationException(resultado.GetType().Name)
    };

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Endpoint_SemChave_401(string? chave)
    {
        Assert.Equal(401, Status(await Controller().GetGlp1Summary(chave!, Telefone)));
        Assert.Equal(401, Status(await Controller().MarkGlp1AlertSeen(chave!, Telefone, "a1")));
        Assert.Equal(0, _repository.Chamadas);
    }

    [Fact]
    public async Task Endpoint_ChaveInvalida_401()
    {
        Assert.Equal(401, Status(await Controller().GetGlp1Summary("chave-errada", Telefone)));
        Assert.Equal(0, _repository.Chamadas);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(365)]
    public async Task Endpoint_PeriodoInvalido_400(int dias)
    {
        Assert.Equal(400, Status(await Controller().GetGlp1Summary(ChaveMestre, Telefone, dias)));
    }

    [Fact]
    public async Task Endpoint_PacienteSemModoGlp1_DataNula()
    {
        var resultado = Assert.IsType<OkObjectResult>(await Controller().GetGlp1Summary(ChaveMestre, Telefone, 30));
        Assert.Null(resultado.Value!.GetType().GetProperty("data")!.GetValue(resultado.Value));
    }

    [Fact]
    public async Task Endpoint_ChaveMestre_DevolveResumo()
    {
        _repository.Perfil = Perfil();

        var resultado = Assert.IsType<OkObjectResult>(await Controller().GetGlp1Summary(ChaveMestre, Telefone, 90));

        var resumo = Assert.IsType<Glp1PortalSummaryDto>(resultado.Value!.GetType().GetProperty("data")!.GetValue(resultado.Value));
        Assert.Equal(90, resumo.Days);
        Assert.Equal(Telefone, resumo.Phone);
    }

    [Fact]
    public async Task MarcarComoVisto_ExistenteOk_InexistenteNotFound()
    {
        _repository.AlertasGravados.Add(new Glp1Alert { Id = "a1", UserId = Telefone, Type = Glp1AlertType.IngestaoBaixa, CreatedAt = Agora });

        Assert.Equal(200, Status(await Controller().MarkGlp1AlertSeen(ChaveMestre, Telefone, "a1")));
        Assert.NotNull(_repository.AlertasGravados[0].SeenByNutritionistAt);

        Assert.Equal(404, Status(await Controller().MarkGlp1AlertSeen(ChaveMestre, Telefone, "nao-existe")));
        Assert.Equal(404, Status(await Controller().MarkGlp1AlertSeen(ChaveMestre, "5521000000000", "a1"))); // alerta de outro paciente
    }
}
