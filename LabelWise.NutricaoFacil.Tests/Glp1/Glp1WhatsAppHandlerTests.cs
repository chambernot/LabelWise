using LabelWise.Application.Interfaces;
using LabelWise.Application.Interfaces.Persistence;
using LabelWise.Application.Services.Glp1;
using LabelWise.Domain.Entities.Glp1;
using LabelWise.Shared.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LabelWise.NutricaoFacil.Tests.Glp1;

public class Glp1WhatsAppHandlerTests
{
    private const string TelefoneLiberado = "5511999990000";
    private const string OutroTelefone = "5521988887777";

    private readonly FakeGlp1Repository _repository = new();
    private readonly FakeWhatsAppSender _sender = new();

    private Glp1WhatsAppHandler CriarHandler(params string[] allowedPhones)
    {
        var glp1Options = new Glp1Options { AllowedPhones = allowedPhones };
        return new Glp1WhatsAppHandler(
            _repository,
            _sender,
            new Glp1GoalCalculator(glp1Options),
            Options.Create(glp1Options),
            NullLogger<Glp1WhatsAppHandler>.Instance);
    }

    private Glp1WhatsAppHandler Handler() => CriarHandler(TelefoneLiberado);

    private string UltimaMensagem => _sender.Mensagens[^1].Texto;

    // Perfil parado numa etapa, com as etapas anteriores já respondidas
    private Glp1Profile PerfilNaEtapa(string etapa)
    {
        var perfil = new Glp1Profile
        {
            Id = TelefoneLiberado,
            ActivationStep = etapa,
            ConsentAt = DateTime.UtcNow,
            ConsentVersion = "glp1-v1",
            CreatedAt = DateTime.UtcNow
        };

        if (etapa is Glp1ActivationSteps.Medicamento or Glp1ActivationSteps.Dose or Glp1ActivationSteps.DiaAplicacao or Glp1ActivationSteps.DadosCorporais)
            perfil.HasMedicalFollowUp = true;
        if (etapa is Glp1ActivationSteps.Dose or Glp1ActivationSteps.DiaAplicacao or Glp1ActivationSteps.DadosCorporais)
            perfil.Medication = "Semaglutida";
        if (etapa is Glp1ActivationSteps.DiaAplicacao or Glp1ActivationSteps.DadosCorporais)
            perfil.Dose = "0,5 mg";
        if (etapa is Glp1ActivationSteps.DadosCorporais)
            perfil.InjectionWeekday = 1;

        _repository.Perfil = perfil;
        return perfil;
    }

    private Glp1Profile PerfilAtivo(bool enabled = true)
    {
        _repository.Perfil = new Glp1Profile
        {
            Id = TelefoneLiberado,
            Enabled = enabled,
            ConsentAt = DateTime.UtcNow,
            ConsentVersion = "glp1-v1",
            HasMedicalFollowUp = true,
            Medication = "Tirzepatida",
            Dose = "2,5 mg",
            InjectionWeekday = 1,
            HeightCm = 165,
            Sex = "Feminino",
            ProteinTargetG = 86,
            CalorieFloorKcal = 1200,
            WaterTargetMl = 2520,
            CreatedAt = DateTime.UtcNow
        };
        return _repository.Perfil;
    }

    // ================================================================ telefone fora da lista

    [Theory]
    [InlineData("glp1")]
    [InlineData("glp1 status")]
    [InlineData("glp1 reset")]
    [InlineData("glp1status")]
    [InlineData("sair glp1")]
    [InlineData("glp1 ajuda")]
    [InlineData("2")]
    [InlineData("almocei arroz e feijão")]
    public async Task TelefoneForaDaLista_NaoTrataNada(string texto)
    {
        var tratou = await Handler().TryHandleAsync(OutroTelefone, texto);

        Assert.False(tratou);
        Assert.Empty(_sender.Mensagens);
        Assert.Equal(0, _repository.Chamadas);
    }

    [Fact]
    public async Task ListaVazia_NaoTrataNada()
    {
        var handler = CriarHandler();

        Assert.False(await handler.TryHandleAsync(TelefoneLiberado, "glp1"));
        Assert.Empty(_sender.Mensagens);
        Assert.Equal(0, _repository.Chamadas);
    }

    [Fact]
    public async Task TelefoneLiberado_SemAtivacao_MensagemComumSegueOFluxoNormal()
    {
        Assert.False(await Handler().TryHandleAsync(TelefoneLiberado, "almocei arroz e feijão"));
        Assert.False(await Handler().TryHandleAsync(TelefoneLiberado, "2"));
        Assert.False(await Handler().TryHandleAsync(TelefoneLiberado, "peso 72kg"));
        Assert.Empty(_sender.Mensagens);
    }

    [Fact]
    public async Task TelefoneLiberado_ModoAtivoSemEtapa_MensagemComumSegueOFluxoNormal()
    {
        PerfilAtivo();

        Assert.False(await Handler().TryHandleAsync(TelefoneLiberado, "2"));
        Assert.Empty(_sender.Mensagens);
    }

    // ================================================================ gatilho

    [Theory]
    [InlineData("glp1")]
    [InlineData("GLP1")]
    [InlineData("glp 1")]
    [InlineData("glp-1")]
    [InlineData("Modo GLP1")]
    [InlineData("modo glp-1")]
    public async Task Gatilho_SemPerfil_IniciaPeloConsentimento(string texto)
    {
        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, texto));

        Assert.Equal(Glp1ActivationSteps.Consentimento, _repository.Perfil!.ActivationStep);
        Assert.False(_repository.Perfil.Enabled);
        Assert.Null(_repository.Perfil.ConsentAt);
        Assert.Equal(Glp1WhatsAppHandler.PerguntaConsentimento, UltimaMensagem);
    }

    [Fact]
    public async Task Gatilho_DuranteAtivacao_RepeteAPerguntaAtual()
    {
        PerfilNaEtapa(Glp1ActivationSteps.DiaAplicacao);

        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, "glp1"));

        Assert.Equal(Glp1ActivationSteps.DiaAplicacao, _repository.Perfil!.ActivationStep);
        Assert.Contains(Glp1WhatsAppHandler.PerguntaDiaAplicacao, UltimaMensagem);
    }

    // ================================================================ passo 1: consentimento

    [Theory]
    [InlineData("SIM")]
    [InlineData("sim")]
    [InlineData("Sim!")]
    [InlineData("autorizo")]
    public async Task Consentimento_Sim_VaiParaAcompanhamentoMedico(string texto)
    {
        PerfilNaEtapa(Glp1ActivationSteps.Consentimento).ConsentAt = null;

        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, texto));

        Assert.Equal(Glp1ActivationSteps.AcompanhamentoMedico, _repository.Perfil!.ActivationStep);
        Assert.NotNull(_repository.Perfil.ConsentAt);
        Assert.Equal("glp1-v1", _repository.Perfil.ConsentVersion);
        Assert.Equal(Glp1WhatsAppHandler.PerguntaAcompanhamento, UltimaMensagem);
    }

    [Theory]
    [InlineData("NÃO")]
    [InlineData("nao")]
    [InlineData("Não.")]
    public async Task Consentimento_Nao_NaoGuardaNada(string texto)
    {
        PerfilNaEtapa(Glp1ActivationSteps.Consentimento).ConsentAt = null;

        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, texto));

        Assert.Null(_repository.Perfil);
        Assert.Equal("Tudo bem, não guardei nada.", UltimaMensagem);
    }

    [Theory]
    [InlineData("talvez")]
    [InlineData("2")]
    [InlineData("72 kg")]
    public async Task Consentimento_RespostaInvalida_RepeteComExemplo(string texto)
    {
        PerfilNaEtapa(Glp1ActivationSteps.Consentimento).ConsentAt = null;

        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, texto));

        Assert.Equal(Glp1ActivationSteps.Consentimento, _repository.Perfil!.ActivationStep);
        Assert.Null(_repository.Perfil.ConsentAt);
        Assert.Contains("Ex.: SIM", UltimaMensagem);
        Assert.Contains(Glp1WhatsAppHandler.PerguntaConsentimento, UltimaMensagem);
        Assert.Empty(_repository.Pesos);
    }

    // ================================================================ passo 2: acompanhamento médico

    [Fact]
    public async Task Acompanhamento_Sim_VaiParaMedicamento()
    {
        PerfilNaEtapa(Glp1ActivationSteps.AcompanhamentoMedico);

        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, "sim"));

        Assert.True(_repository.Perfil!.HasMedicalFollowUp);
        Assert.Equal(Glp1ActivationSteps.Medicamento, _repository.Perfil.ActivationStep);
        Assert.Equal(Glp1WhatsAppHandler.PerguntaMedicamento, UltimaMensagem);
    }

    [Fact]
    public async Task Acompanhamento_Nao_EncerraSemAtivarERecomendaMedico()
    {
        PerfilNaEtapa(Glp1ActivationSteps.AcompanhamentoMedico);

        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, "não"));

        Assert.Null(_repository.Perfil);
        Assert.Equal(Glp1WhatsAppHandler.RespostaSemAcompanhamento, UltimaMensagem);
        Assert.Contains("acompanhamento médico", UltimaMensagem);
        Assert.Contains("procurar um médico", UltimaMensagem);
        Assert.Contains("continua disponível", UltimaMensagem);
    }

    [Fact]
    public async Task Acompanhamento_RespostaInvalida_RepeteComExemplo()
    {
        PerfilNaEtapa(Glp1ActivationSteps.AcompanhamentoMedico);

        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, "às vezes"));

        Assert.Equal(Glp1ActivationSteps.AcompanhamentoMedico, _repository.Perfil!.ActivationStep);
        Assert.Contains("Ex.: SIM", UltimaMensagem);
        Assert.Contains(Glp1WhatsAppHandler.PerguntaAcompanhamento, UltimaMensagem);
    }

    // ================================================================ passo 3: medicamento

    [Theory]
    [InlineData("1", "Semaglutida")]
    [InlineData("2", "Tirzepatida")]
    [InlineData("3", "Liraglutida")]
    [InlineData("4", "Outro")]
    [InlineData("Ozempic", "Semaglutida")]
    [InlineData("uso mounjaro", "Tirzepatida")]
    public async Task Medicamento_OpcaoValida_VaiParaDose(string texto, string esperado)
    {
        PerfilNaEtapa(Glp1ActivationSteps.Medicamento);

        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, texto));

        Assert.Equal(esperado, _repository.Perfil!.Medication);
        Assert.Equal(Glp1ActivationSteps.Dose, _repository.Perfil.ActivationStep);
        Assert.Equal(Glp1WhatsAppHandler.PerguntaDose, UltimaMensagem);
    }

    [Theory]
    [InlineData("9")]
    [InlineData("não sei")]
    public async Task Medicamento_RespostaInvalida_RepeteComExemplo(string texto)
    {
        PerfilNaEtapa(Glp1ActivationSteps.Medicamento);

        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, texto));

        Assert.Null(_repository.Perfil!.Medication);
        Assert.Equal(Glp1ActivationSteps.Medicamento, _repository.Perfil.ActivationStep);
        Assert.Contains("Ex.: 1", UltimaMensagem);
        Assert.Contains(Glp1WhatsAppHandler.PerguntaMedicamento, UltimaMensagem);
    }

    // ================================================================ passo 4: dose

    [Fact]
    public async Task Dose_TextoLivre_GuardaSemComentar()
    {
        PerfilNaEtapa(Glp1ActivationSteps.Dose);

        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, "  0,5 mg  "));

        Assert.Equal("0,5 mg", _repository.Perfil!.Dose);
        Assert.Equal(Glp1ActivationSteps.DiaAplicacao, _repository.Perfil.ActivationStep);
        // A resposta é só a próxima pergunta: nada sobre a dose informada
        Assert.Equal(Glp1WhatsAppHandler.PerguntaDiaAplicacao, UltimaMensagem);
        Assert.DoesNotContain("0,5", UltimaMensagem);
    }

    [Fact]
    public async Task Dose_SoPontuacao_RepeteComExemplo()
    {
        PerfilNaEtapa(Glp1ActivationSteps.Dose).Dose = null;

        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, "..."));

        Assert.Null(_repository.Perfil!.Dose);
        Assert.Equal(Glp1ActivationSteps.Dose, _repository.Perfil.ActivationStep);
        Assert.Contains("Ex.: 0,5 mg", UltimaMensagem);
    }

    // ================================================================ passo 5: dia da aplicação

    [Theory]
    [InlineData("1", 0)]
    [InlineData("2", 1)]
    [InlineData("7", 6)]
    [InlineData("sábado", 6)]
    [InlineData("Segunda-feira", 1)]
    public async Task DiaAplicacao_Valido_VaiParaDadosCorporais(string texto, int esperado)
    {
        PerfilNaEtapa(Glp1ActivationSteps.DiaAplicacao);

        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, texto));

        Assert.Equal(esperado, _repository.Perfil!.InjectionWeekday);
        Assert.Equal(Glp1ActivationSteps.DadosCorporais, _repository.Perfil.ActivationStep);
        Assert.Equal(Glp1WhatsAppHandler.PerguntaDadosCorporais, UltimaMensagem);
    }

    [Theory]
    [InlineData("8")]
    [InlineData("0")]
    [InlineData("72 kg")]
    public async Task DiaAplicacao_RespostaInvalida_RepeteSemRegistrarPeso(string texto)
    {
        PerfilNaEtapa(Glp1ActivationSteps.DiaAplicacao);

        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, texto));

        Assert.Null(_repository.Perfil!.InjectionWeekday);
        Assert.Equal(Glp1ActivationSteps.DiaAplicacao, _repository.Perfil.ActivationStep);
        Assert.Contains("Ex.: 2 (segunda)", UltimaMensagem);
        Assert.Empty(_repository.Pesos);
    }

    // ================================================================ passo 6 e 7: dados corporais e conclusão

    [Fact]
    public async Task DadosCorporais_Completos_AtivaComMetas()
    {
        PerfilNaEtapa(Glp1ActivationSteps.DadosCorporais);

        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, "72 kg, 1,65 m, feminino"));

        var perfil = _repository.Perfil!;
        Assert.True(perfil.Enabled);
        Assert.Null(perfil.ActivationStep);
        Assert.Null(perfil.PendingWeightKg);
        Assert.Equal(165m, perfil.HeightCm);
        Assert.Equal("Feminino", perfil.Sex);
        Assert.Equal(86, perfil.ProteinTargetG);
        Assert.Equal(1200, perfil.CalorieFloorKcal);
        Assert.Equal(2520, perfil.WaterTargetMl);
        Assert.NotNull(perfil.ConsentAt);
        Assert.Equal("glp1-v1", perfil.ConsentVersion);

        Assert.Equal(72m, Assert.Single(_repository.Pesos));

        Assert.Contains("Modo GLP-1 ativado ✅", UltimaMensagem);
        Assert.Contains("🥩 proteína 86 g/dia", UltimaMensagem);
        Assert.Contains("🔥 mínimo de 1.200 kcal/dia", UltimaMensagem);
        Assert.Contains("💧 água 2,5 L/dia", UltimaMensagem);
    }

    [Fact]
    public async Task DadosCorporais_Parciais_PerguntaSoOQueFaltou()
    {
        PerfilNaEtapa(Glp1ActivationSteps.DadosCorporais);
        var handler = Handler();

        Assert.True(await handler.TryHandleAsync(TelefoneLiberado, "72kg"));
        Assert.Equal(Glp1ActivationSteps.DadosCorporais, _repository.Perfil!.ActivationStep);
        Assert.Equal(72m, _repository.Perfil.PendingWeightKg);
        Assert.Contains("sua altura", UltimaMensagem);
        Assert.Contains("seu sexo", UltimaMensagem);
        Assert.DoesNotContain("seu peso", UltimaMensagem);
        Assert.Empty(_repository.Pesos); // o peso só vai para WeightLogs ao concluir

        Assert.True(await handler.TryHandleAsync(TelefoneLiberado, "165 cm"));
        Assert.Equal(165m, _repository.Perfil.HeightCm);
        Assert.Contains("seu sexo", UltimaMensagem);
        Assert.DoesNotContain("sua altura", UltimaMensagem);

        Assert.True(await handler.TryHandleAsync(TelefoneLiberado, "feminino"));
        Assert.True(_repository.Perfil.Enabled);
        Assert.Contains("Modo GLP-1 ativado ✅", UltimaMensagem);
    }

    [Fact]
    public async Task DadosCorporais_FaltandoSoOPeso_AceitaNumeroSemUnidade()
    {
        PerfilNaEtapa(Glp1ActivationSteps.DadosCorporais);
        var handler = Handler();

        Assert.True(await handler.TryHandleAsync(TelefoneLiberado, "1m70, masculino"));
        Assert.Equal(170m, _repository.Perfil!.HeightCm);
        Assert.Equal("Masculino", _repository.Perfil.Sex);
        Assert.Contains("seu peso", UltimaMensagem);

        Assert.True(await handler.TryHandleAsync(TelefoneLiberado, "80"));
        Assert.True(_repository.Perfil.Enabled);
        Assert.Equal(96, _repository.Perfil.ProteinTargetG); // 80 × 1,2
        Assert.Equal(80m, Assert.Single(_repository.Pesos));
    }

    [Theory]
    [InlineData("não sei")]
    [InlineData("72")] // sem unidade e faltando peso e altura: ambíguo, não adivinha
    public async Task DadosCorporais_RespostaInvalida_RepeteComExemplo(string texto)
    {
        PerfilNaEtapa(Glp1ActivationSteps.DadosCorporais);

        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, texto));

        Assert.Equal(Glp1ActivationSteps.DadosCorporais, _repository.Perfil!.ActivationStep);
        Assert.Null(_repository.Perfil.PendingWeightKg);
        Assert.Contains("Não entendi.", UltimaMensagem);
        Assert.Contains("ex.: 72 kg", UltimaMensagem);
        Assert.Empty(_repository.Pesos);
    }

    // ================================================================ fluxo completo

    [Fact]
    public async Task FluxoCompleto_DoGatilhoAteAtivar()
    {
        var handler = Handler();

        foreach (var texto in new[] { "GLP-1", "sim", "sim", "2", "2,5 mg", "2", "72 kg, 1,65 m, feminino" })
        {
            Assert.True(await handler.TryHandleAsync(TelefoneLiberado, texto));
        }

        var perfil = _repository.Perfil!;
        Assert.True(perfil.Enabled);
        Assert.Null(perfil.ActivationStep);
        Assert.Equal("Tirzepatida", perfil.Medication); // o "2" foi a opção do medicamento, não remoção de refeição
        Assert.Equal("2,5 mg", perfil.Dose);
        Assert.Equal(1, perfil.InjectionWeekday);
        Assert.Contains("Modo GLP-1 ativado ✅", UltimaMensagem);
    }

    // ================================================================ "2" e "kg" durante a ativação

    [Theory]
    [InlineData(Glp1ActivationSteps.Consentimento)]
    [InlineData(Glp1ActivationSteps.AcompanhamentoMedico)]
    [InlineData(Glp1ActivationSteps.Dose)]
    [InlineData(Glp1ActivationSteps.DiaAplicacao)]
    [InlineData(Glp1ActivationSteps.DadosCorporais)]
    public async Task NumeroIsolado_DuranteAtivacao_PertenceAAtivacao(string etapa)
    {
        PerfilNaEtapa(etapa);

        // true = o WhatsAppController para aqui e não chega à remoção de refeição
        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, "2"));
        Assert.Single(_sender.Mensagens);
    }

    [Theory]
    [InlineData(Glp1ActivationSteps.Consentimento)]
    [InlineData(Glp1ActivationSteps.Medicamento)]
    [InlineData(Glp1ActivationSteps.DiaAplicacao)]
    public async Task TextoComKg_DuranteAtivacao_NaoRegistraPeso(string etapa)
    {
        PerfilNaEtapa(etapa);

        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, "peso 72kg"));
        Assert.Empty(_repository.Pesos);
        Assert.Equal(etapa, _repository.Perfil!.ActivationStep);
    }

    [Theory]
    [InlineData("cancelar")]
    [InlineData("sair glp1")]
    public async Task Cancelar_DuranteAtivacao_ApagaOPerfil(string texto)
    {
        PerfilNaEtapa(Glp1ActivationSteps.Dose);

        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, texto));

        Assert.Null(_repository.Perfil);
        Assert.Equal(Glp1WhatsAppHandler.RespostaAtivacaoCancelada, UltimaMensagem);
    }

    // ================================================================ modo ativo, saída e reativação

    [Fact]
    public async Task Gatilho_ModoJaAtivo_MostraMetasEComandos()
    {
        PerfilAtivo();

        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, "glp1"));

        Assert.Contains("já está ativo", UltimaMensagem);
        Assert.Contains("🥩 proteína 86 g/dia", UltimaMensagem);
        Assert.Contains("sair glp1", UltimaMensagem);
        Assert.Contains("glp1 status", UltimaMensagem);
        Assert.Null(_repository.Perfil!.ActivationStep);
    }

    [Fact]
    public async Task SairGlp1_DesativaEMantemOsDados()
    {
        PerfilAtivo();

        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, "sair glp1"));

        Assert.False(_repository.Perfil!.Enabled);
        Assert.Equal("Tirzepatida", _repository.Perfil.Medication);
        Assert.Equal(86, _repository.Perfil.ProteinTargetG);
        Assert.Equal("Modo GLP-1 desativado. Seus dados continuam guardados; mande GLP1 para reativar.", UltimaMensagem);
    }

    [Fact]
    public async Task SairGlp1_SemModoAtivo_AvisaQueNaoEstaAtivo()
    {
        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, "sair glp1"));
        Assert.Equal(Glp1WhatsAppHandler.RespostaNaoAtivo, UltimaMensagem);
    }

    [Fact]
    public async Task Reativacao_VoltaDiretoSemRefazerPerguntas()
    {
        PerfilAtivo(enabled: false);

        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, "GLP1"));

        Assert.True(_repository.Perfil!.Enabled);
        Assert.Null(_repository.Perfil.ActivationStep);
        Assert.Contains("reativado ✅", UltimaMensagem);
        Assert.Contains("🥩 proteína 86 g/dia", UltimaMensagem);
        Assert.DoesNotContain("Você autoriza", UltimaMensagem);
        Assert.Single(_sender.Mensagens);
    }

    [Fact]
    public async Task SairEReativar_CicloCompleto()
    {
        PerfilAtivo();
        var handler = Handler();

        await handler.TryHandleAsync(TelefoneLiberado, "sair glp1");
        Assert.False(_repository.Perfil!.Enabled);

        await handler.TryHandleAsync(TelefoneLiberado, "glp1");
        Assert.True(_repository.Perfil.Enabled);
        Assert.Contains("reativado", UltimaMensagem);
    }

    // ================================================================ status, reset e comando desconhecido

    [Theory]
    [InlineData("glp1status")]
    [InlineData("GLP1STATUS")]
    [InlineData("glp1  status")]
    [InlineData("glp-1 status")]
    public async Task Status_AceitaVariacoesDeEscrita(string texto)
    {
        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, texto));
        Assert.Contains("Modo GLP-1: status", Assert.Single(_sender.Mensagens).Texto);
    }

    [Fact]
    public async Task Status_SemPerfil_InformaNaoConfiguradoComDadosDeHoje()
    {
        _repository.ProteinaHoje = 45.5m;
        _repository.AguaHoje = 1500;
        _repository.Sintomas = 2;
        _repository.Alertas = 1;

        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, "glp1 status"));

        var (telefone, texto) = Assert.Single(_sender.Mensagens);
        Assert.Equal(TelefoneLiberado, telefone);
        Assert.Contains(Glp1WhatsAppHandler.RespostaNaoConfigurado, texto);
        Assert.Contains("Proteína: 45,5 g", texto);
        Assert.Contains("Água: 1500 ml", texto);
        Assert.Contains("Sintomas registrados: 2", texto);
        Assert.Contains("Alertas: 1", texto);
    }

    [Fact]
    public async Task Status_ComPerfil_MostraOsCamposGravados()
    {
        PerfilAtivo().ConsentAt = new DateTime(2026, 10, 29, 15, 0, 0, DateTimeKind.Utc);

        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, "GLP-1 Status"));

        var texto = Assert.Single(_sender.Mensagens).Texto;
        Assert.DoesNotContain(Glp1WhatsAppHandler.RespostaNaoConfigurado, texto);
        Assert.Contains("Ativo: sim", texto);
        Assert.Contains("Consentimento: 29/10/2026 12:00 (versão glp1-v1)", texto);
        Assert.Contains("Acompanhamento médico: sim", texto);
        Assert.Contains("Medicamento: Tirzepatida", texto);
        Assert.Contains("Dose: 2,5 mg", texto);
        Assert.Contains("Dia da aplicação: segunda", texto);
        Assert.Contains("Altura: 165 cm", texto);
        Assert.Contains("Meta de proteína: 86 g", texto);
        Assert.Contains("Piso calórico: 1200 kcal", texto);
        Assert.Contains("Meta de água: 2520 ml", texto);
        Assert.Contains("Lembretes desativados: não", texto);
    }

    [Theory]
    [InlineData("glp1 reset")]
    [InlineData("glp1reset")]
    public async Task Reset_ApagaOsDadosDoTelefoneEConfirma(string texto)
    {
        var handler = CriarHandler("+55 (11) 99999-0000"); // formato com máscara também vale

        Assert.True(await handler.TryHandleAsync(TelefoneLiberado, texto));

        Assert.Equal(new[] { TelefoneLiberado }, _repository.TelefonesApagados);
        var (telefone, resposta) = Assert.Single(_sender.Mensagens);
        Assert.Equal(TelefoneLiberado, telefone);
        Assert.Equal("Dados GLP-1 apagados. Pode recomeçar.", resposta);
    }

    [Fact]
    public async Task ComandoGlp1Desconhecido_PerguntaEmVezDeIrParaAIA()
    {
        Assert.True(await Handler().TryHandleAsync(TelefoneLiberado, "glp1 ajuda"));
        Assert.Equal(Glp1WhatsAppHandler.RespostaComandoDesconhecido, Assert.Single(_sender.Mensagens).Texto);
    }

    // ================================================================ contexto do dia (resposta da refeição e "resumo")

    [Fact]
    public async Task ContextoDoDia_TelefoneForaDaLista_NuloSemConsultarOBanco()
    {
        PerfilAtivo();

        Assert.Null(await Handler().ObterContextoDoDiaAsync(OutroTelefone, DateTime.UtcNow));
        Assert.Equal(0, _repository.Chamadas);
    }

    [Fact]
    public async Task ContextoDoDia_SemPerfilOuDesativadoOuEmAtivacao_Nulo()
    {
        Assert.Null(await Handler().ObterContextoDoDiaAsync(TelefoneLiberado, DateTime.UtcNow));

        PerfilAtivo(enabled: false);
        Assert.Null(await Handler().ObterContextoDoDiaAsync(TelefoneLiberado, DateTime.UtcNow));

        PerfilNaEtapa(Glp1ActivationSteps.Dose).Enabled = true;
        Assert.Null(await Handler().ObterContextoDoDiaAsync(TelefoneLiberado, DateTime.UtcNow));
    }

    [Fact]
    public async Task ContextoDoDia_ModoAtivo_TrazMetasEConsumo()
    {
        PerfilAtivo();
        _repository.ProteinaHoje = 40;
        _repository.CaloriasHoje = 900;
        _repository.RefeicoesHoje = 2;
        _repository.AguaHoje = 1500;
        var agora = DateTime.UtcNow;

        var ctx = await Handler().ObterContextoDoDiaAsync(TelefoneLiberado, agora);

        Assert.NotNull(ctx);
        Assert.Equal(new Glp1DayContext(86, 1200, 2520, 40, 900, 2, 1500, agora), ctx);
    }

    // ================================================================ dublês

    private sealed class FakeGlp1Repository : IGlp1Repository
    {
        public Glp1Profile? Perfil { get; set; }
        public decimal ProteinaHoje { get; set; }
        public int AguaHoje { get; set; }
        public int Sintomas { get; set; }
        public int Alertas { get; set; }
        public List<string> TelefonesApagados { get; } = new();
        public List<decimal> Pesos { get; } = new();
        public int Chamadas { get; private set; }

        public Task<Glp1Profile?> ObterPerfilAsync(string userId) { Chamadas++; return Task.FromResult(Perfil?.Id == userId ? Perfil : null); }
        public Task SalvarPerfilAsync(Glp1Profile perfil) { Chamadas++; Perfil = perfil; return Task.CompletedTask; }
        public Task ApagarPerfilAsync(string userId) { Chamadas++; Perfil = null; return Task.CompletedTask; }
        public Task RegistrarPesoAsync(string userId, decimal pesoKg, DateTime agoraUtc) { Chamadas++; Pesos.Add(pesoKg); return Task.CompletedTask; }
        public Task<int> ContarSintomasAsync(string userId) { Chamadas++; return Task.FromResult(Sintomas); }
        public Task<int> ContarAlertasAsync(string userId) { Chamadas++; return Task.FromResult(Alertas); }
        public Task<decimal> ObterProteinaDoDiaAsync(string userId, DateTime inicioDiaUtc) { Chamadas++; return Task.FromResult(ProteinaHoje); }
        public int CaloriasHoje { get; set; }
        public int RefeicoesHoje { get; set; }
        public Task<Glp1DayIntake> ObterConsumoDoDiaAsync(string userId, DateTime inicioDiaUtc) { Chamadas++; return Task.FromResult(new Glp1DayIntake(ProteinaHoje, CaloriasHoje, RefeicoesHoje)); }
        public Task<int> ObterAguaDoDiaAsync(string userId, DateTime inicioDiaUtc) { Chamadas++; return Task.FromResult(AguaHoje); }

        public Task ApagarDadosAsync(string userId)
        {
            Chamadas++;
            Perfil = null;
            TelefonesApagados.Add(userId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWhatsAppSender : IWhatsAppSenderService
    {
        public List<(string Telefone, string Texto)> Mensagens { get; } = new();

        public Task SendTextMessageAsync(string phone, string message)
        {
            Mensagens.Add((phone, message));
            return Task.CompletedTask;
        }

        public Task<bool> SendTemplateMessageAsync(string toPhone, string templateName, params string[] templateArgs) =>
            throw new InvalidOperationException("O Modo GLP-1 não deveria enviar template nestes testes.");

        public Task<WhatsAppSendResult> SendTemplateMessageWithResultAsync(string toPhone, string templateName, params string[] templateArgs) =>
            throw new InvalidOperationException("O Modo GLP-1 não deveria enviar template nestes testes.");
    }
}
