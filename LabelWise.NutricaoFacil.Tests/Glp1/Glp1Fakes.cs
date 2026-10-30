using LabelWise.Application.Interfaces;
using LabelWise.Application.Interfaces.Persistence;
using LabelWise.Application.Services.Glp1;
using LabelWise.Domain.Entities.Glp1;

namespace LabelWise.NutricaoFacil.Tests.Glp1;

// Dublês compartilhados pelos testes do Modo GLP-1
internal sealed class FakeGlp1Repository : IGlp1Repository
{
    public Glp1Profile? Perfil { get; set; }
    public decimal ProteinaHoje { get; set; }
    public int CaloriasHoje { get; set; }
    public int RefeicoesHoje { get; set; }
    public int AguaHoje { get; set; }
    public int Sintomas { get; set; }
    public int Alertas { get; set; }
    public string? Restricoes { get; set; }
    public string? Aversoes { get; set; }
    public List<string> TelefonesApagados { get; } = new();
    public List<decimal> Pesos { get; } = new();
    public List<Glp1SymptomLog> SintomasGravados { get; } = new();
    public List<Glp1Alert> AlertasGravados { get; } = new();
    public int Chamadas { get; private set; }

    public Task<Glp1Profile?> ObterPerfilAsync(string userId) { Chamadas++; return Task.FromResult(Perfil?.Id == userId ? Perfil : null); }
    public Task SalvarPerfilAsync(Glp1Profile perfil) { Chamadas++; Perfil = perfil; return Task.CompletedTask; }
    public Task ApagarPerfilAsync(string userId) { Chamadas++; Perfil = null; return Task.CompletedTask; }
    public Task RegistrarPesoAsync(string userId, decimal pesoKg, DateTime agoraUtc) { Chamadas++; Pesos.Add(pesoKg); return Task.CompletedTask; }
    public Task<int> ContarSintomasAsync(string userId) { Chamadas++; return Task.FromResult(Sintomas); }
    public Task<int> ContarAlertasAsync(string userId) { Chamadas++; return Task.FromResult(Alertas); }
    public Task<decimal> ObterProteinaDoDiaAsync(string userId, DateTime inicioDiaUtc) { Chamadas++; return Task.FromResult(ProteinaHoje); }
    public Task<Glp1DayIntake> ObterConsumoDoDiaAsync(string userId, DateTime inicioDiaUtc) { Chamadas++; return Task.FromResult(new Glp1DayIntake(ProteinaHoje, CaloriasHoje, RefeicoesHoje)); }
    public Task<int> ObterAguaDoDiaAsync(string userId, DateTime inicioDiaUtc) { Chamadas++; return Task.FromResult(AguaHoje); }

    public Task InserirSintomasAsync(IEnumerable<Glp1SymptomLog> sintomas) { Chamadas++; SintomasGravados.AddRange(sintomas); return Task.CompletedTask; }
    public Task<List<Glp1SymptomLog>> ObterSintomasDesdeAsync(string userId, DateTime desdeUtc) { Chamadas++; return Task.FromResult(SintomasGravados.Where(s => s.UserId == userId && s.LoggedAt >= desdeUtc).ToList()); }
    public Task InserirAlertaAsync(Glp1Alert alerta) { Chamadas++; AlertasGravados.Add(alerta); return Task.CompletedTask; }
    public Task<(string? Restricoes, string? Aversoes)> ObterRestricoesEAversoesAsync(string userId) { Chamadas++; return Task.FromResult((Restricoes, Aversoes)); }

    public Task ApagarDadosAsync(string userId)
    {
        Chamadas++;
        Perfil = null;
        TelefonesApagados.Add(userId);
        return Task.CompletedTask;
    }
}

internal sealed class FakeWhatsAppSender : IWhatsAppSenderService
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
