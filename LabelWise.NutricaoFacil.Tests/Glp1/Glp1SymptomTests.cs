using LabelWise.Application.Services.Glp1;
using LabelWise.Domain.Entities.Glp1;
using LabelWise.Shared.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LabelWise.NutricaoFacil.Tests.Glp1;

public class Glp1SymptomTests
{
    private const string Telefone = "5511999990000";
    private const string OutroTelefone = "5521988887777";

    private readonly FakeGlp1Repository _repository = new();
    private readonly FakeWhatsAppSender _sender = new();

    private Glp1WhatsAppHandler Handler()
    {
        var options = new Glp1Options { AllowedPhones = new[] { Telefone } };
        return new Glp1WhatsAppHandler(_repository, _sender, new Glp1GoalCalculator(options), Options.Create(options), NullLogger<Glp1WhatsAppHandler>.Instance);
    }

    private void ModoAtivo(bool enabled = true)
    {
        _repository.Perfil = new Glp1Profile
        {
            Id = Telefone,
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
            WaterTargetMl = 2520
        };
    }

    private static IEnumerable<string> TextosDasDicas(string categoria) => Glp1TipsCatalog.Todas(categoria).Select(d => d.Texto);

    // ================================================================ detector (sem IA)

    [Theory]
    [InlineData("estou enjoada", Glp1Symptom.Nausea)]
    [InlineData("Enjoo desde cedo", Glp1Symptom.Nausea)]
    [InlineData("fiquei enjoado", Glp1Symptom.Nausea)]
    [InlineData("muita náusea hoje", Glp1Symptom.Nausea)]
    [InlineData("vomitei de manhã", Glp1Symptom.Vomito)]
    [InlineData("tive um vômito", Glp1Symptom.Vomito)]
    [InlineData("intestino preso há 3 dias", Glp1Symptom.Constipacao)]
    [InlineData("prisão de ventre", Glp1Symptom.Constipacao)]
    [InlineData("estou com diarreia", Glp1Symptom.Diarreia)]
    [InlineData("intestino solto", Glp1Symptom.Diarreia)]
    [InlineData("muita azia", Glp1Symptom.Refluxo)]
    [InlineData("refluxo à noite", Glp1Symptom.Refluxo)]
    [InlineData("uma queimação no peito", Glp1Symptom.Refluxo)]
    [InlineData("estou sem fome", Glp1Symptom.SemApetite)]
    [InlineData("sem apetite nenhum", Glp1Symptom.SemApetite)]
    public void Detector_ReconheceCadaSintoma(string texto, Glp1Symptom esperado)
    {
        var deteccao = Glp1SymptomDetector.Detectar(texto);

        Assert.Equal(new[] { esperado }, deteccao.Sintomas);
        Assert.False(deteccao.TemSinalDeAlerta);
    }

    [Fact]
    public void Detector_AnsiaDeVomito_EhEnjooNaoVomito()
    {
        Assert.Equal(new[] { Glp1Symptom.Nausea }, Glp1SymptomDetector.Detectar("estou com ânsia de vômito").Sintomas);
    }

    [Theory]
    [InlineData("não estou enjoada hoje")]
    [InlineData("não tive azia")]
    [InlineData("almocei arroz e feijão")]
    [InlineData("bom dia")]
    public void Detector_SemSintoma(string texto)
    {
        Assert.True(Glp1SymptomDetector.Detectar(texto).Vazio);
    }

    [Theory]
    [InlineData("vomitei o dia todo", "vômito que não para")]
    [InlineData("não paro de vomitar", "vômito que não para")]
    [InlineData("vomitando sem parar", "vômito que não para")]
    [InlineData("vi sangue no vômito", "sangue")]
    [InlineData("quase desmaiei", "desmaio")]
    [InlineData("dor muito forte na barriga", "dor abdominal forte")]
    [InlineData("estou com uma dor no estômago insuportável", "dor abdominal forte")]
    [InlineData("não consigo beber água", "não consegue beber água")]
    [InlineData("minha urina está muito escura", "urina muito escura")]
    [InlineData("estou com tremor e suor frio", "tremor com suor frio")]
    [InlineData("me sinto confusa", "confusão")]
    public void Detector_ReconheceSinaisDeAlerta(string texto, string sinal)
    {
        Assert.Contains(sinal, Glp1SymptomDetector.Detectar(texto).SinaisDeAlerta);
    }

    [Theory]
    [InlineData("comi pizza e fiquei enjoada", true)]
    [InlineData("almocei 120g de frango e senti azia", true)]
    [InlineData("estou enjoada", false)]
    [InlineData("não comi nada, estou sem fome", false)]
    [InlineData("tomei o remédio e fiquei enjoada", false)]
    public void Detector_DizSeTemComida(string texto, bool temComida)
    {
        Assert.Equal(temComida, Glp1SymptomDetector.Detectar(texto).TemComida);
    }

    // ================================================================ só sintoma

    [Theory]
    [InlineData("estou enjoada", Glp1Symptom.Nausea, "enjoo", Glp1TipsCatalog.Nausea)]
    [InlineData("vomitei", Glp1Symptom.Vomito, "vômito", Glp1TipsCatalog.Hidratacao)]
    [InlineData("intestino preso", Glp1Symptom.Constipacao, "intestino preso", Glp1TipsCatalog.Constipacao)]
    [InlineData("estou com diarreia", Glp1Symptom.Diarreia, "diarreia", Glp1TipsCatalog.Diarreia)]
    [InlineData("muita azia", Glp1Symptom.Refluxo, "azia/refluxo", Glp1TipsCatalog.Refluxo)]
    [InlineData("estou sem fome", Glp1Symptom.SemApetite, "falta de apetite", Glp1TipsCatalog.SemApetite)]
    public async Task SoSintoma_GravaERespondeComDica(string texto, Glp1Symptom sintoma, string nome, string categoria)
    {
        ModoAtivo();

        var resultado = await Handler().ProcessarMensagemAsync(Telefone, texto);

        Assert.True(resultado.Handled);
        var gravado = Assert.Single(_repository.SintomasGravados);
        Assert.Equal(sintoma, gravado.Symptom);
        Assert.False(gravado.IsRedFlag);
        Assert.Equal(Telefone, gravado.UserId);
        Assert.Empty(_repository.AlertasGravados);

        var resposta = Assert.Single(_sender.Mensagens).Texto;
        Assert.StartsWith($"Anotei: {nome}. 💡 ", resposta);
        Assert.Contains(resposta[$"Anotei: {nome}. 💡 ".Length..], TextosDasDicas(categoria));
    }

    [Fact]
    public async Task DoisSintomas_GravaOsDois()
    {
        ModoAtivo();

        await Handler().ProcessarMensagemAsync(Telefone, "estou enjoada e com azia");

        Assert.Equal(new[] { Glp1Symptom.Nausea, Glp1Symptom.Refluxo }, _repository.SintomasGravados.Select(s => s.Symptom));
        Assert.StartsWith("Anotei: enjoo e azia/refluxo. 💡 ", Assert.Single(_sender.Mensagens).Texto);
    }

    [Fact]
    public async Task SintomaPorAudioTranscrito_TambemFunciona()
    {
        ModoAtivo();

        // O controller passa o texto já transcrito do áudio
        var resultado = await Handler().ProcessarMensagemAsync(Telefone, "Oi, hoje eu acordei bem enjoada, sabe?");

        Assert.True(resultado.Handled);
        Assert.Equal(Glp1Symptom.Nausea, Assert.Single(_repository.SintomasGravados).Symptom);
    }

    // ================================================================ dicas filtradas por restrições e aversões

    [Fact]
    public async Task Dica_RespeitaRestricoesEAversoes()
    {
        ModoAtivo();
        _repository.Restricoes = "Intolerância à lactose";
        _repository.Aversoes = "não gosto de ovo nem de frango";

        for (int i = 0; i < 30; i++)
        {
            await Handler().ProcessarMensagemAsync(Telefone, "estou sem fome");
        }

        Assert.All(_sender.Mensagens, m =>
        {
            Assert.DoesNotContain("iogurte", m.Texto);
            Assert.DoesNotContain("leite", m.Texto);
            Assert.DoesNotContain("ovo", m.Texto);
        });
    }

    [Fact]
    public void Catalogo_TemTodasAsCategoriasComAQuantidadeCerta()
    {
        foreach (var categoria in new[] { Glp1TipsCatalog.Nausea, Glp1TipsCatalog.Constipacao, Glp1TipsCatalog.Diarreia, Glp1TipsCatalog.Refluxo, Glp1TipsCatalog.SemApetite, Glp1TipsCatalog.Hidratacao })
        {
            Assert.Equal(4, Glp1TipsCatalog.Todas(categoria).Count);
        }
        Assert.Equal(10, Glp1TipsCatalog.Todas(Glp1TipsCatalog.ProteinaPorcaoPequena).Count);
    }

    [Fact]
    public void Catalogo_FiltraPorTermoDaRestricao()
    {
        var permitidas = Glp1TipsCatalog.Permitidas(Glp1TipsCatalog.ProteinaPorcaoPequena, "lactose", "peixe");

        Assert.DoesNotContain(permitidas, d => d.Texto.Contains("queijo") || d.Texto.Contains("leite") || d.Texto.Contains("atum"));
        Assert.Contains(permitidas, d => d.Texto == "1 ovo cozido");
    }

    [Fact]
    public void Catalogo_TudoFiltrado_UsaHidratacaoOuPadrao()
    {
        // Categoria sem nenhuma dica disponível cai nas dicas de hidratação
        Assert.Contains(Glp1TipsCatalog.Escolher("categoria_inexistente", null, null), TextosDasDicas(Glp1TipsCatalog.Hidratacao));
    }

    // ================================================================ sinais de alerta

    [Theory]
    [InlineData("vomitei o dia todo")]
    [InlineData("tem sangue nas fezes")]
    [InlineData("desmaiei agora há pouco")]
    [InlineData("dor muito forte na barriga")]
    [InlineData("não consigo beber água, tudo volta")]
    [InlineData("urina muito escura")]
    [InlineData("tremendo com suor frio")]
    [InlineData("estou confuso e tonto")]
    public async Task SinalDeAlerta_GravaRedFlagEAlertaSemDicaAlimentar(string texto)
    {
        ModoAtivo();

        var resultado = await Handler().ProcessarMensagemAsync(Telefone, texto);

        Assert.True(resultado.Handled);
        Assert.Null(resultado.TextoParaAnexar);
        Assert.NotEmpty(_repository.SintomasGravados);
        Assert.All(_repository.SintomasGravados, s => Assert.True(s.IsRedFlag));

        var alerta = Assert.Single(_repository.AlertasGravados);
        Assert.Equal(Glp1AlertType.SinalAlerta, alerta.Type);
        Assert.Equal(Telefone, alerta.UserId);
        Assert.False(string.IsNullOrWhiteSpace(alerta.Details));
        Assert.Null(alerta.SeenByNutritionistAt);

        Assert.Equal("Esses sinais precisam de avaliação médica o quanto antes. Se piorar, procure um pronto atendimento.",
            Assert.Single(_sender.Mensagens).Texto);
    }

    [Fact]
    public async Task SinalDeAlertaComComida_NaoSegueParaARefeicao()
    {
        ModoAtivo();

        var resultado = await Handler().ProcessarMensagemAsync(Telefone, "comi pizza e vomitei o dia todo");

        Assert.True(resultado.Handled);
        Assert.Equal(Glp1WhatsAppHandler.RespostaSinalDeAlerta, Assert.Single(_sender.Mensagens).Texto);
    }

    [Fact]
    public async Task SinalDeAlerta_NaoGuardaOTextoOriginal()
    {
        ModoAtivo();

        await Handler().ProcessarMensagemAsync(Telefone, "desmaiei no trabalho hoje");

        Assert.DoesNotContain("trabalho", Assert.Single(_repository.AlertasGravados).Details);
    }

    // ================================================================ mensagem mista

    [Fact]
    public async Task SintomaComComida_GravaESegueParaARefeicaoComDicaNoFim()
    {
        ModoAtivo();

        var resultado = await Handler().ProcessarMensagemAsync(Telefone, "comi pizza e fiquei enjoada");

        Assert.False(resultado.Handled); // o controller segue para o registro da refeição
        Assert.Equal(Glp1Symptom.Nausea, Assert.Single(_repository.SintomasGravados).Symptom);
        Assert.Empty(_sender.Mensagens);  // nada enviado ainda: a anotação vai no fim da resposta da refeição
        Assert.StartsWith("📝 Anotei: enjoo. 💡 ", resultado.TextoParaAnexar);
        Assert.StartsWith("Anotei: enjoo. 💡 ", resultado.RespostaSeNaoHouverRefeicao);
    }

    // ================================================================ usuário sem o modo: nada muda

    [Theory]
    [InlineData("estou enjoada")]
    [InlineData("vomitei o dia todo")]
    [InlineData("comi pizza e fiquei enjoada")]
    public async Task TelefoneForaDaLista_NadaMuda(string texto)
    {
        ModoAtivo();

        var resultado = await Handler().ProcessarMensagemAsync(OutroTelefone, texto);

        Assert.Equal(Glp1MessageResult.NaoTratado, resultado);
        Assert.Empty(_sender.Mensagens);
        Assert.Equal(0, _repository.Chamadas);
    }

    [Theory]
    [InlineData("estou enjoada")]
    [InlineData("vomitei o dia todo")]
    public async Task ModoDesativadoOuSemPerfil_NadaMuda(string texto)
    {
        Assert.Equal(Glp1MessageResult.NaoTratado, await Handler().ProcessarMensagemAsync(Telefone, texto));

        ModoAtivo(enabled: false);
        Assert.Equal(Glp1MessageResult.NaoTratado, await Handler().ProcessarMensagemAsync(Telefone, texto));

        Assert.Empty(_repository.SintomasGravados);
        Assert.Empty(_repository.AlertasGravados);
        Assert.Empty(_sender.Mensagens);
    }

    // ================================================================ contexto para a IA (Modo Nutri)

    [Fact]
    public async Task ContextoParaIa_TrazTratamentoMetasConsumoESintomasMasNuncaADose()
    {
        ModoAtivo();
        _repository.ProteinaHoje = 40;
        _repository.AguaHoje = 1500;
        _repository.SintomasGravados.Add(new Glp1SymptomLog { UserId = Telefone, Symptom = Glp1Symptom.Nausea, LoggedAt = DateTime.UtcNow.AddDays(-1) });
        _repository.SintomasGravados.Add(new Glp1SymptomLog { UserId = Telefone, Symptom = Glp1Symptom.Nausea, LoggedAt = DateTime.UtcNow.AddDays(-2) });
        _repository.SintomasGravados.Add(new Glp1SymptomLog { UserId = Telefone, Symptom = Glp1Symptom.Refluxo, LoggedAt = DateTime.UtcNow.AddDays(-3) });
        _repository.SintomasGravados.Add(new Glp1SymptomLog { UserId = Telefone, Symptom = Glp1Symptom.Diarreia, LoggedAt = DateTime.UtcNow.AddDays(-10) }); // fora dos 7 dias

        var contexto = await Handler().MontarContextoParaIaAsync(Telefone, DateTime.UtcNow);

        Assert.NotNull(contexto);
        Assert.Contains("MODO GLP-1 ATIVO", contexto);
        Assert.Contains("Medicamento: Tirzepatida", contexto);
        Assert.Contains("Dia da aplicação: segunda", contexto);
        Assert.Contains("proteína 86 g/dia", contexto);
        Assert.Contains("mínimo de 1.200 kcal/dia", contexto);
        Assert.Contains("água 2,5 L/dia", contexto);
        Assert.Contains("Hoje até agora: proteína 40 g, água 1,5 L", contexto);
        Assert.Contains("enjoo (2x)", contexto);
        Assert.Contains("azia/refluxo (1x)", contexto);
        Assert.DoesNotContain("diarreia", contexto);
        Assert.DoesNotContain("2,5 mg", contexto);
        Assert.DoesNotContain("dose", contexto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ContextoParaIa_UsuarioSemOModo_Nulo()
    {
        Assert.Null(await Handler().MontarContextoParaIaAsync(Telefone, DateTime.UtcNow));
        Assert.Null(await Handler().MontarContextoParaIaAsync(OutroTelefone, DateTime.UtcNow));

        ModoAtivo(enabled: false);
        Assert.Null(await Handler().MontarContextoParaIaAsync(Telefone, DateTime.UtcNow));
        Assert.False(await Handler().EstaAtivoAsync(Telefone));
    }
}
