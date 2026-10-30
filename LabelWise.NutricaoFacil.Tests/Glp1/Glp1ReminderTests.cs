using LabelWise.Application.Services.Glp1;
using LabelWise.Domain.Entities.Glp1;
using LabelWise.Shared.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LabelWise.NutricaoFacil.Tests.Glp1;

public class Glp1ReminderTests
{
    private const string Telefone = "5511999990000";
    private const string Telefone2 = "5511888880000";
    private const string ForaDaLista = "5521977776666";

    // Segunda-feira, 02/11/2026 (Brasília = UTC-3)
    private static readonly DateTime Segunda = new(2026, 11, 2);
    private static DateTime AsHorasBr(DateTime diaBr, int hora) => DateTime.SpecifyKind(diaBr.AddHours(hora + 3), DateTimeKind.Utc);
    private static DateTime InicioDoDiaUtc(DateTime diaBr) => DateTime.SpecifyKind(diaBr.AddHours(3), DateTimeKind.Utc);

    private static readonly DateTime Segunda9h = AsHorasBr(Segunda, 9);
    private static readonly DateTime Segunda21h = AsHorasBr(Segunda, 21);

    private readonly FakeGlp1Repository _repository = new();
    private readonly FakeWhatsAppSender _sender = new();
    private readonly Glp1Options _options = new() { AllowedPhones = new[] { Telefone, Telefone2 } };

    private Glp1ReminderService Servico() =>
        new(_repository, _sender, Options.Create(_options), NullLogger<Glp1ReminderService>.Instance);

    private Glp1WhatsAppHandler Handler() =>
        new(_repository, _sender, new Glp1GoalCalculator(_options), Options.Create(_options), NullLogger<Glp1WhatsAppHandler>.Instance);

    private static Glp1Profile Perfil(string phone, int diaAplicacao = 1, bool optOut = false, bool enabled = true) => new()
    {
        Id = phone,
        Enabled = enabled,
        ConsentAt = DateTime.UtcNow,
        ConsentVersion = "glp1-v1",
        HasMedicalFollowUp = true,
        Medication = "Semaglutida",
        Dose = "0,5 mg",
        InjectionWeekday = diaAplicacao,
        ProteinTargetG = 86,
        CalorieFloorKcal = 1200,
        WaterTargetMl = 2520,
        RemindersOptOut = optOut
    };

    private void Consumo(string phone, DateTime diaBr, decimal proteina, int calorias, int refeicoes = 2) =>
        _repository.ConsumoPorDia[(phone, InicioDoDiaUtc(diaBr))] = new Glp1DayIntake(proteina, calorias, refeicoes);

    // Três dias (sábado, domingo e segunda) com os mesmos números
    private void TresDias(string phone, decimal proteina, int calorias)
    {
        for (int i = 0; i < 3; i++) Consumo(phone, Segunda.AddDays(-i), proteina, calorias);
    }

    // ================================================================ dia da aplicação (9h)

    [Fact]
    public async Task Aplicacao_9hNoDiaCerto_ConversouNas24h_EnviaTexto()
    {
        _repository.Perfil = Perfil(Telefone);
        _repository.UltimaMensagem[Telefone] = Segunda9h.AddHours(-2);

        var resultado = await Servico().ExecutarAsync(Segunda9h, dryRun: false);

        Assert.Equal(1, resultado.Enviados);
        var (telefone, texto) = Assert.Single(_sender.Mensagens);
        Assert.Equal(Telefone, telefone);
        Assert.Equal(Glp1ReminderTexts.LembreteAplicacao, texto);
        Assert.DoesNotContain("dose", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_sender.Templates);
    }

    [Fact]
    public async Task Aplicacao_SemConversaNas24h_UsaTemplateSemVariaveis()
    {
        _repository.Perfil = Perfil(Telefone);
        _repository.UltimaMensagem[Telefone] = Segunda9h.AddHours(-30);

        await Servico().ExecutarAsync(Segunda9h, dryRun: false);

        var (telefone, template, args) = Assert.Single(_sender.Templates);
        Assert.Equal(Telefone, telefone);
        Assert.Equal("glp1_lembrete_aplicacao", template);
        Assert.Empty(args);
        Assert.Empty(_sender.Mensagens);
    }

    [Fact]
    public async Task Aplicacao_OutroDiaDaSemana_NaoEnvia()
    {
        _repository.Perfil = Perfil(Telefone, diaAplicacao: 3); // quarta

        var resultado = await Servico().ExecutarAsync(Segunda9h, dryRun: false);

        Assert.Empty(resultado.Selecionados);
        Assert.Empty(_sender.Mensagens);
        Assert.Empty(_sender.Templates);
    }

    [Fact]
    public async Task Aplicacao_ForaDas9h_NaoEnvia()
    {
        _repository.Perfil = Perfil(Telefone);

        var resultado = await Servico().ExecutarAsync(AsHorasBr(Segunda, 10), dryRun: false);

        Assert.Empty(resultado.Selecionados);
        Assert.Contains("10h", resultado.Observacao);
    }

    [Fact]
    public void DiaDaAplicacao_UsaODiaDeBrasilia()
    {
        // 01h UTC de terça ainda é segunda (22h) em Brasília
        Assert.True(Glp1ReminderService.EhDiaDaAplicacao(Perfil(Telefone, diaAplicacao: 1), new DateTime(2026, 11, 3, 1, 0, 0, DateTimeKind.Utc)));
    }

    // ================================================================ repetição e opt-out

    [Fact]
    public async Task MesmoTipoNoMesmoDia_SoUmaVez()
    {
        _repository.Perfil = Perfil(Telefone);

        await Servico().ExecutarAsync(Segunda9h, dryRun: false);
        var segunda = await Servico().ExecutarAsync(Segunda9h.AddMinutes(30), dryRun: false);

        Assert.Single(_sender.Templates);
        Assert.Empty(segunda.Selecionados);
    }

    [Fact]
    public async Task OptOut_NaoRecebeNada()
    {
        _repository.Perfil = Perfil(Telefone, optOut: true);
        TresDias(Telefone, proteina: 20, calorias: 600);

        Assert.Empty((await Servico().ExecutarAsync(Segunda9h, false)).Selecionados);
        Assert.Empty((await Servico().ExecutarAsync(Segunda21h, false)).Selecionados);
        Assert.Empty(_repository.AlertasGravados);
    }

    [Fact]
    public async Task ModoDesativado_NaoRecebeNada()
    {
        _repository.Perfil = Perfil(Telefone, enabled: false);

        Assert.Empty((await Servico().ExecutarAsync(Segunda9h, false)).Selecionados);
    }

    [Fact]
    public async Task TelefoneForaDaLista_NaoRecebeMesmoComPerfilAtivo()
    {
        _repository.OutrosPerfis.Add(Perfil(ForaDaLista));

        Assert.Empty((await Servico().ExecutarAsync(Segunda9h, false)).Selecionados);
        Assert.Empty(_sender.Templates);
    }

    // ================================================================ ingestão baixa (21h)

    [Fact]
    public async Task IngestaoBaixa_ProteinaAbaixoDaMetade_EnviaECriaAlerta()
    {
        _repository.Perfil = Perfil(Telefone);
        _repository.UltimaMensagem[Telefone] = Segunda21h.AddHours(-1);
        TresDias(Telefone, proteina: 30, calorias: 1400); // 30 < 43 (50% de 86)

        var resultado = await Servico().ExecutarAsync(Segunda21h, dryRun: false);

        var selecao = Assert.Single(resultado.Selecionados);
        Assert.Equal(Glp1ReminderTypes.AlertaIngestao, selecao.Tipo);
        Assert.Contains("proteína média de 30 g", selecao.Motivo);
        Assert.Equal(Glp1ReminderTexts.AlertaIngestao, Assert.Single(_sender.Mensagens).Texto);

        var alerta = Assert.Single(_repository.AlertasGravados);
        Assert.Equal(Glp1AlertType.IngestaoBaixa, alerta.Type);
        Assert.Equal(Telefone, alerta.UserId);
    }

    [Fact]
    public async Task IngestaoBaixa_SemConversa_UsaTemplateDeAlerta()
    {
        _repository.Perfil = Perfil(Telefone);
        TresDias(Telefone, proteina: 30, calorias: 1400);

        await Servico().ExecutarAsync(Segunda21h, dryRun: false);

        Assert.Equal("glp1_alerta_ingestao", Assert.Single(_sender.Templates).Template);
    }

    [Fact]
    public async Task IngestaoBaixa_AbaixoDoPisoCalorico_TambemConta()
    {
        _repository.Perfil = Perfil(Telefone);
        TresDias(Telefone, proteina: 80, calorias: 900);

        var selecao = Assert.Single((await Servico().ExecutarAsync(Segunda21h, false)).Selecionados);
        Assert.Contains("média de 900 kcal", selecao.Motivo);
    }

    [Fact]
    public async Task IngestaoAdequada_NaoEnvia()
    {
        _repository.Perfil = Perfil(Telefone);
        TresDias(Telefone, proteina: 60, calorias: 1400);

        Assert.Empty((await Servico().ExecutarAsync(Segunda21h, false)).Selecionados);
        Assert.Empty(_repository.AlertasGravados);
    }

    [Fact]
    public async Task IngestaoBaixa_UmDiaSemRegistro_NaoEnvia()
    {
        _repository.Perfil = Perfil(Telefone);
        TresDias(Telefone, proteina: 20, calorias: 500);
        Consumo(Telefone, Segunda.AddDays(-1), 0, 0, refeicoes: 0); // domingo sem refeição

        Assert.Empty((await Servico().ExecutarAsync(Segunda21h, false)).Selecionados);
    }

    [Theory]
    [InlineData(2, false)] // alerta há 2 dias: não repete
    [InlineData(4, true)]  // alerta há 4 dias: pode repetir
    public async Task IngestaoBaixa_NaoRepeteAntesDe3Dias(int diasDesdeUltimoAlerta, bool deveEnviar)
    {
        _repository.Perfil = Perfil(Telefone);
        TresDias(Telefone, proteina: 20, calorias: 500);
        _repository.AlertasGravados.Add(new Glp1Alert { UserId = Telefone, Type = Glp1AlertType.IngestaoBaixa, CreatedAt = Segunda21h.AddDays(-diasDesdeUltimoAlerta) });

        var resultado = await Servico().ExecutarAsync(Segunda21h, false);

        Assert.Equal(deveEnviar, resultado.Selecionados.Any());
    }

    [Fact]
    public async Task IngestaoBaixa_AlertaDeSinalDeAlertaNaoBloqueia()
    {
        _repository.Perfil = Perfil(Telefone);
        TresDias(Telefone, proteina: 20, calorias: 500);
        _repository.AlertasGravados.Add(new Glp1Alert { UserId = Telefone, Type = Glp1AlertType.SinalAlerta, CreatedAt = Segunda21h.AddDays(-1) });

        Assert.Single((await Servico().ExecutarAsync(Segunda21h, false)).Selecionados);
    }

    [Fact]
    public void MotivoIngestaoBaixa_RegrasPuras()
    {
        var perfil = Perfil(Telefone);
        var baixo = new Glp1DayIntake(20, 1300, 2);
        var ok = new Glp1DayIntake(60, 1300, 2);

        Assert.NotNull(Glp1ReminderService.MotivoIngestaoBaixa(perfil, new[] { baixo, baixo, baixo }));
        Assert.Null(Glp1ReminderService.MotivoIngestaoBaixa(perfil, new[] { ok, ok, ok }));
        Assert.Null(Glp1ReminderService.MotivoIngestaoBaixa(perfil, new[] { baixo, baixo }));                          // menos de 3 dias
        Assert.Null(Glp1ReminderService.MotivoIngestaoBaixa(perfil, new[] { baixo, new Glp1DayIntake(0, 0, 0), baixo })); // dia sem registro
        Assert.NotNull(Glp1ReminderService.MotivoIngestaoBaixa(perfil, new[] { ok, ok, new Glp1DayIntake(0, 0, 1) }));    // média 40 g < 43 g
    }

    // ================================================================ dryRun e phone

    [Fact]
    public async Task DryRun_SoLista()
    {
        _repository.Perfil = Perfil(Telefone);
        TresDias(Telefone, proteina: 20, calorias: 500);

        var resultado = await Servico().ExecutarAsync(Segunda21h, dryRun: true);

        Assert.True(resultado.DryRun);
        Assert.Single(resultado.Selecionados);
        Assert.Empty(_sender.Mensagens);
        Assert.Empty(_sender.Templates);
        Assert.Empty(_repository.AlertasGravados);
        Assert.Empty(_repository.LembretesEnviados);
    }

    [Fact]
    public async Task Phone_IgnoraAHoraERodaSoParaEsseTelefone()
    {
        _repository.Perfil = Perfil(Telefone);
        _repository.OutrosPerfis.Add(Perfil(Telefone2));

        var resultado = await Servico().ExecutarAsync(AsHorasBr(Segunda, 15), dryRun: false, somenteTelefone: "+55 (11) 99999-0000");

        var selecao = Assert.Single(resultado.Selecionados);
        Assert.Equal(Telefone, selecao.Phone);
        Assert.Equal(Glp1ReminderTypes.LembreteAplicacao, selecao.Tipo);
        Assert.Equal(Telefone, Assert.Single(_sender.Templates).Telefone);
    }

    [Fact]
    public async Task Phone_ContinuaRespeitandoOptOut()
    {
        _repository.Perfil = Perfil(Telefone, optOut: true);

        Assert.Empty((await Servico().ExecutarAsync(AsHorasBr(Segunda, 15), false, Telefone)).Selecionados);
    }

    // ================================================================ comandos no WhatsApp

    [Fact]
    public async Task PararEVoltarLembretes()
    {
        _repository.Perfil = Perfil(Telefone);

        Assert.True(await Handler().TryHandleAsync(Telefone, "parar lembretes glp1"));
        Assert.True(_repository.Perfil.RemindersOptOut);
        Assert.Equal(Glp1WhatsAppHandler.RespostaLembretesParados, _sender.Mensagens[^1].Texto);

        Assert.True(await Handler().TryHandleAsync(Telefone, "Voltar lembretes GLP-1"));
        Assert.False(_repository.Perfil.RemindersOptOut);
        Assert.Equal(Glp1WhatsAppHandler.RespostaLembretesVoltaram, _sender.Mensagens[^1].Texto);
    }

    [Fact]
    public async Task PararLembretes_SemModo_AvisaQueNaoEstaAtivo()
    {
        Assert.True(await Handler().TryHandleAsync(Telefone, "parar lembretes glp1"));
        Assert.Equal(Glp1WhatsAppHandler.RespostaNaoAtivo, Assert.Single(_sender.Mensagens).Texto);
    }

    [Fact]
    public async Task TestarLembrete_EnviaNaHoraSemContarComoEnvioDoDia()
    {
        _repository.Perfil = Perfil(Telefone, diaAplicacao: 3, optOut: true); // ignora regra e opt-out

        Assert.True(await Handler().TryHandleAsync(Telefone, "glp1 testar lembrete"));

        Assert.Equal(Glp1ReminderTexts.LembreteAplicacao, Assert.Single(_sender.Mensagens).Texto);
        Assert.Empty(_repository.LembretesEnviados);
    }

    [Fact]
    public async Task TestarAlerta_EnviaECriaOAlerta()
    {
        _repository.Perfil = Perfil(Telefone);

        Assert.True(await Handler().TryHandleAsync(Telefone, "glp1 testar alerta"));

        Assert.Equal(Glp1ReminderTexts.AlertaIngestao, Assert.Single(_sender.Mensagens).Texto);
        Assert.Equal(Glp1AlertType.IngestaoBaixa, Assert.Single(_repository.AlertasGravados).Type);
    }

    [Theory]
    [InlineData("glp1 testar lembrete")]
    [InlineData("parar lembretes glp1")]
    public async Task ComandosForaDaLista_NaoFazemNada(string texto)
    {
        Assert.False(await Handler().TryHandleAsync(ForaDaLista, texto));
        Assert.Empty(_sender.Mensagens);
        Assert.Empty(_repository.AlertasGravados);
    }
}
