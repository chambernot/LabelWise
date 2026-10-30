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

    // ---------- lembretes
    public List<Glp1Profile> OutrosPerfis { get; } = new();
    public Dictionary<(string Phone, DateTime InicioDiaUtc), Glp1DayIntake> ConsumoPorDia { get; } = new();
    public HashSet<string> LembretesEnviados { get; } = new();
    public Dictionary<string, DateTime> UltimaMensagem { get; } = new();

    private IEnumerable<Glp1Profile> TodosPerfis => (Perfil != null ? new[] { Perfil } : Array.Empty<Glp1Profile>()).Concat(OutrosPerfis);

    public Task<List<Glp1Profile>> ListarPerfisAtivosAsync()
    {
        Chamadas++;
        return Task.FromResult(TodosPerfis.Where(p => p.Enabled && p.ActivationStep == null).ToList());
    }

    public Task<Glp1Alert?> ObterUltimoAlertaAsync(string userId, Glp1AlertType tipo)
    {
        Chamadas++;
        return Task.FromResult(AlertasGravados.Where(a => a.UserId == userId && a.Type == tipo).OrderByDescending(a => a.CreatedAt).FirstOrDefault());
    }

    public Task<bool> LembreteJaEnviadoAsync(string userId, string tipo, string diaBr) { Chamadas++; return Task.FromResult(LembretesEnviados.Contains($"{userId}_{tipo}_{diaBr}")); }
    public Task<bool> RegistrarEnvioLembreteAsync(string userId, string tipo, string diaBr, DateTime agoraUtc) { Chamadas++; return Task.FromResult(LembretesEnviados.Add($"{userId}_{tipo}_{diaBr}")); }
    public Task<DateTime?> ObterUltimaMensagemRecebidaAsync(string userId) { Chamadas++; return Task.FromResult(UltimaMensagem.TryGetValue(userId, out var d) ? d : (DateTime?)null); }

    // Consumo por telefone e dia; sem entrada específica, usa ProteinaHoje/CaloriasHoje/RefeicoesHoje
    Task<Glp1DayIntake> IGlp1Repository.ObterConsumoDoDiaAsync(string userId, DateTime inicioDiaUtc)
    {
        Chamadas++;
        return Task.FromResult(ConsumoPorDia.TryGetValue((userId, inicioDiaUtc), out var c) ? c : new Glp1DayIntake(ProteinaHoje, CaloriasHoje, RefeicoesHoje));
    }

    // ---------- portal
    public List<Glp1MealEntry> Refeicoes { get; } = new();
    public List<Glp1WaterEntry> Agua { get; } = new();
    public List<Glp1WeightEntry> PesosRegistrados { get; } = new();

    public Task<List<Glp1Profile>> ObterPerfisAsync(IEnumerable<string> userIds) { Chamadas++; return Task.FromResult(TodosPerfis.Where(p => userIds.Contains(p.Id)).ToList()); }
    public Task<HashSet<string>> ObterTelefonesComAlertaNaoVistoAsync(IEnumerable<string> userIds) { Chamadas++; return Task.FromResult(AlertasGravados.Where(a => a.SeenByNutritionistAt == null && userIds.Contains(a.UserId)).Select(a => a.UserId).ToHashSet()); }
    public Task<List<Glp1Alert>> ObterAlertasParaPortalAsync(string userId, DateTime desdeUtc) { Chamadas++; return Task.FromResult(AlertasGravados.Where(a => a.UserId == userId && (a.CreatedAt >= desdeUtc || a.SeenByNutritionistAt == null)).ToList()); }

    public Task<bool> MarcarAlertaVistoAsync(string userId, string alertId, DateTime agoraUtc)
    {
        Chamadas++;
        var alerta = AlertasGravados.FirstOrDefault(a => a.Id == alertId && a.UserId == userId);
        if (alerta == null) return Task.FromResult(false);
        alerta.SeenByNutritionistAt = agoraUtc;
        return Task.FromResult(true);
    }

    public Task<List<Glp1MealEntry>> ObterRefeicoesDesdeAsync(string userId, DateTime desdeUtc) { Chamadas++; return Task.FromResult(Refeicoes.Where(r => r.LoggedAt >= desdeUtc).ToList()); }
    public Task<List<Glp1WaterEntry>> ObterAguaDesdeAsync(string userId, DateTime desdeUtc) { Chamadas++; return Task.FromResult(Agua.Where(a => a.LoggedAt >= desdeUtc).ToList()); }
    public Task<List<Glp1WeightEntry>> ObterPesosDesdeAsync(string userId, DateTime desdeUtc) { Chamadas++; return Task.FromResult(PesosRegistrados.Where(p => p.LoggedAt >= desdeUtc).ToList()); }
}

internal sealed class FakeWhatsAppSender : IWhatsAppSenderService
{
    public List<(string Telefone, string Texto)> Mensagens { get; } = new();
    public List<(string Telefone, string Template, string[] Args)> Templates { get; } = new();

    public Task SendTextMessageAsync(string phone, string message)
    {
        Mensagens.Add((phone, message));
        return Task.CompletedTask;
    }

    public Task<bool> SendTemplateMessageAsync(string toPhone, string templateName, params string[] templateArgs)
    {
        Templates.Add((toPhone, templateName, templateArgs));
        return Task.FromResult(true);
    }

    public Task<WhatsAppSendResult> SendTemplateMessageWithResultAsync(string toPhone, string templateName, params string[] templateArgs) =>
        throw new InvalidOperationException("O Modo GLP-1 não deveria enviar template nestes testes.");
}
