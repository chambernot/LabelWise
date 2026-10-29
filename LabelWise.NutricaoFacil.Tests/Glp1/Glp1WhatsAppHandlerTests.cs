using LabelWise.Application.Interfaces;
using LabelWise.Application.Interfaces.Persistence;
using LabelWise.Application.Services.Glp1;
using LabelWise.Domain.Entities.Glp1;
using LabelWise.Shared.Options;
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
        var options = Options.Create(new Glp1Options { AllowedPhones = allowedPhones });
        return new Glp1WhatsAppHandler(_repository, _sender, options);
    }

    [Theory]
    [InlineData("glp1 status")]
    [InlineData("glp1 reset")]
    [InlineData("almocei arroz e feijão")]
    public async Task TelefoneForaDaLista_NaoTrataNada(string texto)
    {
        var handler = CriarHandler(TelefoneLiberado);

        var tratou = await handler.TryHandleAsync(OutroTelefone, texto);

        Assert.False(tratou);
        Assert.Empty(_sender.Mensagens);
        Assert.Equal(0, _repository.Chamadas);
    }

    [Fact]
    public async Task ListaVazia_NaoTrataNada()
    {
        var handler = CriarHandler();

        Assert.False(await handler.TryHandleAsync(TelefoneLiberado, "glp1 status"));
        Assert.Empty(_sender.Mensagens);
        Assert.Equal(0, _repository.Chamadas);
    }

    [Fact]
    public async Task TelefoneLiberado_MensagemComum_SegueOFluxoNormal()
    {
        var handler = CriarHandler(TelefoneLiberado);

        Assert.False(await handler.TryHandleAsync(TelefoneLiberado, "almocei arroz e feijão"));
        Assert.Empty(_sender.Mensagens);
    }

    [Fact]
    public async Task Status_SemPerfil_InformaNaoConfiguradoComDadosDeHoje()
    {
        _repository.ProteinaHoje = 45.5m;
        _repository.AguaHoje = 1500;
        _repository.Sintomas = 2;
        _repository.Alertas = 1;
        var handler = CriarHandler(TelefoneLiberado);

        var tratou = await handler.TryHandleAsync(TelefoneLiberado, "glp1 status");

        Assert.True(tratou);
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
        _repository.Perfil = new Glp1Profile
        {
            Id = TelefoneLiberado,
            Enabled = true,
            ActivationStep = "Concluido",
            ConsentAt = new DateTime(2026, 10, 29, 15, 0, 0, DateTimeKind.Utc),
            ConsentVersion = "glp1-v1",
            HasMedicalFollowUp = true,
            Medication = "Semaglutida",
            Dose = "0,5 mg",
            InjectionWeekday = 1,
            HeightCm = 170,
            Sex = "F",
            ProteinTargetG = 86,
            CalorieFloorKcal = 1200,
            WaterTargetMl = 2520,
            RemindersOptOut = false
        };
        var handler = CriarHandler(TelefoneLiberado);

        Assert.True(await handler.TryHandleAsync(TelefoneLiberado, "GLP-1 Status"));

        var texto = Assert.Single(_sender.Mensagens).Texto;
        Assert.DoesNotContain(Glp1WhatsAppHandler.RespostaNaoConfigurado, texto);
        Assert.Contains("Ativo: sim", texto);
        Assert.Contains("Consentimento: 29/10/2026 12:00 (versão glp1-v1)", texto);
        Assert.Contains("Acompanhamento médico: sim", texto);
        Assert.Contains("Medicamento: Semaglutida", texto);
        Assert.Contains("Dose: 0,5 mg", texto);
        Assert.Contains("Dia da aplicação: segunda", texto);
        Assert.Contains("Altura: 170 cm", texto);
        Assert.Contains("Meta de proteína: 86 g", texto);
        Assert.Contains("Piso calórico: 1200 kcal", texto);
        Assert.Contains("Meta de água: 2520 ml", texto);
        Assert.Contains("Lembretes desativados: não", texto);
    }

    [Fact]
    public async Task Reset_ApagaOsDadosDoTelefoneEConfirma()
    {
        var handler = CriarHandler("+55 (11) 99999-0000"); // formato com máscara também vale

        var tratou = await handler.TryHandleAsync(TelefoneLiberado, "glp1 reset");

        Assert.True(tratou);
        Assert.Equal(new[] { TelefoneLiberado }, _repository.TelefonesApagados);
        var (telefone, texto) = Assert.Single(_sender.Mensagens);
        Assert.Equal(TelefoneLiberado, telefone);
        Assert.Equal("Dados GLP-1 apagados. Pode recomeçar.", texto);
    }

    private sealed class FakeGlp1Repository : IGlp1Repository
    {
        public Glp1Profile? Perfil { get; set; }
        public decimal ProteinaHoje { get; set; }
        public int AguaHoje { get; set; }
        public int Sintomas { get; set; }
        public int Alertas { get; set; }
        public List<string> TelefonesApagados { get; } = new();
        public int Chamadas { get; private set; }

        public Task<Glp1Profile?> ObterPerfilAsync(string userId) { Chamadas++; return Task.FromResult(Perfil); }
        public Task<int> ContarSintomasAsync(string userId) { Chamadas++; return Task.FromResult(Sintomas); }
        public Task<int> ContarAlertasAsync(string userId) { Chamadas++; return Task.FromResult(Alertas); }
        public Task<decimal> ObterProteinaDoDiaAsync(string userId, DateTime inicioDiaUtc) { Chamadas++; return Task.FromResult(ProteinaHoje); }
        public Task<int> ObterAguaDoDiaAsync(string userId, DateTime inicioDiaUtc) { Chamadas++; return Task.FromResult(AguaHoje); }

        public Task ApagarDadosAsync(string userId)
        {
            Chamadas++;
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
