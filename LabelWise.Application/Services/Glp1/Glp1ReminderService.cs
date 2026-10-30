using LabelWise.Application.Interfaces;
using LabelWise.Application.Interfaces.Persistence;
using LabelWise.Domain.Entities.Glp1;
using LabelWise.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabelWise.Application.Services.Glp1;

public static class Glp1ReminderTypes
{
    public const string LembreteAplicacao = "LembreteAplicacao";
    public const string AlertaIngestao = "AlertaIngestao";
}

public sealed record Glp1ReminderSelection(string Phone, string Tipo, string Canal, string Motivo);

public sealed record Glp1ReminderRunResult(
    bool DryRun,
    int HoraBrasilia,
    string Observacao,
    IReadOnlyList<Glp1ReminderSelection> Selecionados,
    int Enviados,
    int Falhas);

// Lembretes do Modo GLP-1, chamados de hora em hora pelo cron (POST /api/whatsapp/send-glp1-reminders):
// - 9h (Brasília): lembrete do dia da aplicação para quem tem InjectionWeekday = hoje;
// - 21h: cuidado com ingestão baixa (3 dias seguidos com registro e média abaixo de 50% da proteína ou do piso calórico),
//   com um Glp1Alert IngestaoBaixa, no máximo a cada 3 dias.
// Só telefones de Glp1:AllowedPhones com o modo ativo e sem RemindersOptOut. Um envio de cada tipo por pessoa por dia.
public class Glp1ReminderService
{
    public const int HoraLembreteAplicacao = 9;
    public const int HoraAlertaIngestao = 21;
    public const int DiasAnalisadosIngestao = 3;
    public const int DiasEntreAlertasIngestao = 3;
    public const decimal FracaoMinimaProteina = 0.5m;

    private readonly IGlp1Repository _repository;
    private readonly IWhatsAppSenderService _whatsAppSender;
    private readonly Glp1Options _options;
    private readonly ILogger<Glp1ReminderService> _logger;
    private readonly HashSet<string> _telefonesLiberados;

    public Glp1ReminderService(IGlp1Repository repository, IWhatsAppSenderService whatsAppSender, IOptions<Glp1Options> options, ILogger<Glp1ReminderService> logger)
    {
        _repository = repository;
        _whatsAppSender = whatsAppSender;
        _options = options.Value;
        _logger = logger;
        _telefonesLiberados = Glp1Phones.Liberados(_options);
    }

    // somenteTelefone: roda só para esse telefone e ignora a hora (as demais regras continuam valendo)
    public async Task<Glp1ReminderRunResult> ExecutarAsync(DateTime agoraUtc, bool dryRun, string? somenteTelefone = null)
    {
        var agoraBr = agoraUtc.AddHours(-3);
        var hora = agoraBr.Hour;
        bool ignorarHora = !string.IsNullOrWhiteSpace(somenteTelefone);

        bool avaliarAplicacao = ignorarHora || hora == HoraLembreteAplicacao;
        bool avaliarIngestao = ignorarHora || hora == HoraAlertaIngestao;

        if (!avaliarAplicacao && !avaliarIngestao)
        {
            return new Glp1ReminderRunResult(dryRun, hora, $"Nenhum lembrete GLP-1 às {hora}h (só às {HoraLembreteAplicacao}h e às {HoraAlertaIngestao}h).",
                Array.Empty<Glp1ReminderSelection>(), 0, 0);
        }

        var perfis = (await _repository.ListarPerfisAtivosAsync())
            .Where(p => _telefonesLiberados.Contains(Glp1Phones.SomenteDigitos(p.Id)))
            .Where(p => !ignorarHora || Glp1Phones.SomenteDigitos(p.Id) == Glp1Phones.SomenteDigitos(somenteTelefone))
            .ToList();

        var diaBr = agoraBr.ToString("yyyy-MM-dd");
        var selecionados = new List<Glp1ReminderSelection>();
        int enviados = 0, falhas = 0;

        foreach (var perfil in perfis)
        {
            if (!PodeReceberLembretes(perfil)) continue;

            try
            {
                if (avaliarAplicacao && EhDiaDaAplicacao(perfil, agoraUtc) &&
                    !await _repository.LembreteJaEnviadoAsync(perfil.Id, Glp1ReminderTypes.LembreteAplicacao, diaBr))
                {
                    var canal = await CanalAsync(perfil.Id, agoraUtc);
                    selecionados.Add(new Glp1ReminderSelection(perfil.Id, Glp1ReminderTypes.LembreteAplicacao, canal, "Hoje é o dia da aplicação"));

                    if (!dryRun && await _repository.RegistrarEnvioLembreteAsync(perfil.Id, Glp1ReminderTypes.LembreteAplicacao, diaBr, agoraUtc))
                    {
                        if (await EnviarAsync(perfil.Id, canal, Glp1ReminderTexts.LembreteAplicacao, _options.TemplateLembreteAplicacao)) enviados++; else falhas++;
                    }
                }

                if (avaliarIngestao)
                {
                    var motivo = await AvaliarIngestaoBaixaAsync(perfil, agoraUtc, diaBr);
                    if (motivo != null)
                    {
                        var canal = await CanalAsync(perfil.Id, agoraUtc);
                        selecionados.Add(new Glp1ReminderSelection(perfil.Id, Glp1ReminderTypes.AlertaIngestao, canal, motivo));

                        if (!dryRun && await _repository.RegistrarEnvioLembreteAsync(perfil.Id, Glp1ReminderTypes.AlertaIngestao, diaBr, agoraUtc))
                        {
                            await CriarAlertaIngestaoAsync(perfil.Id, motivo, agoraUtc);
                            if (await EnviarAsync(perfil.Id, canal, Glp1ReminderTexts.AlertaIngestao, _options.TemplateAlertaIngestao)) enviados++; else falhas++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                falhas++;
                _logger.LogError(ex, "Falha no lembrete GLP-1 do telefone final {Final}.", Final(perfil.Id));
            }
        }

        var observacao = dryRun
            ? $"Simulação: {selecionados.Count} envio(s) seriam feitos. Nada foi enviado."
            : $"Lembretes GLP-1 concluídos. Enviados: {enviados}, Falhas: {falhas}.";

        return new Glp1ReminderRunResult(dryRun, hora, observacao, selecionados, enviados, falhas);
    }

    // ---------------------------------------------------------------- regras (públicas para teste)

    public static bool PodeReceberLembretes(Glp1Profile perfil) =>
        perfil.Enabled && string.IsNullOrEmpty(perfil.ActivationStep) && perfil.ProteinTargetG.HasValue && !perfil.RemindersOptOut;

    // InjectionWeekday: 0 = domingo ... 6 = sábado, no dia de Brasília
    public static bool EhDiaDaAplicacao(Glp1Profile perfil, DateTime agoraUtc) =>
        perfil.InjectionWeekday.HasValue && perfil.InjectionWeekday.Value == (int)agoraUtc.AddHours(-3).DayOfWeek;

    // Hoje, ontem e anteontem, todos com refeição registrada, e média abaixo de 50% da proteína ou abaixo do piso calórico.
    // Devolve o motivo, ou nulo quando a regra não se aplica.
    public static string? MotivoIngestaoBaixa(Glp1Profile perfil, IReadOnlyList<Glp1DayIntake> ultimosDias)
    {
        if (ultimosDias.Count < DiasAnalisadosIngestao || ultimosDias.Any(d => d.Meals == 0)) return null;

        var mediaProteina = ultimosDias.Average(d => d.ProteinG);
        var mediaCalorias = (int)Math.Round(ultimosDias.Average(d => (decimal)d.CaloriesKcal));
        var metaProteina = perfil.ProteinTargetG ?? 0;
        var piso = perfil.CalorieFloorKcal ?? 0;

        var motivos = new List<string>();
        if (metaProteina > 0 && mediaProteina < metaProteina * FracaoMinimaProteina)
            motivos.Add($"proteína média de {mediaProteina:0} g (meta {metaProteina} g)");
        if (piso > 0 && mediaCalorias < piso)
            motivos.Add($"média de {mediaCalorias} kcal (piso {piso} kcal)");

        return motivos.Count == 0 ? null : $"Últimos {DiasAnalisadosIngestao} dias: " + string.Join(" e ", motivos);
    }

    // ---------------------------------------------------------------- apoio

    private async Task<string?> AvaliarIngestaoBaixaAsync(Glp1Profile perfil, DateTime agoraUtc, string diaBr)
    {
        if (await _repository.LembreteJaEnviadoAsync(perfil.Id, Glp1ReminderTypes.AlertaIngestao, diaBr)) return null;

        var ultimoAlerta = await _repository.ObterUltimoAlertaAsync(perfil.Id, Glp1AlertType.IngestaoBaixa);
        if (ultimoAlerta != null && ultimoAlerta.CreatedAt > agoraUtc.AddDays(-DiasEntreAlertasIngestao)) return null;

        var inicioHojeUtc = DateTime.SpecifyKind(agoraUtc.AddHours(-3).Date.AddHours(3), DateTimeKind.Utc);
        var dias = new List<Glp1DayIntake>();
        for (int i = 0; i < DiasAnalisadosIngestao; i++)
        {
            dias.Add(await _repository.ObterConsumoDoDiaAsync(perfil.Id, inicioHojeUtc.AddDays(-i)));
        }

        return MotivoIngestaoBaixa(perfil, dias);
    }

    private Task CriarAlertaIngestaoAsync(string phone, string detalhes, DateTime agoraUtc) =>
        _repository.InserirAlertaAsync(new Glp1Alert { UserId = phone, Type = Glp1AlertType.IngestaoBaixa, Details = detalhes, CreatedAt = agoraUtc });

    public const string CanalTexto = "texto";
    public const string CanalTemplate = "template";

    // Dentro da janela de 24 h da Meta, texto livre; fora dela, só template aprovado é entregue
    private async Task<string> CanalAsync(string phone, DateTime agoraUtc)
    {
        var ultima = await _repository.ObterUltimaMensagemRecebidaAsync(phone);
        return ultima.HasValue && ultima.Value > agoraUtc.AddHours(-24) ? CanalTexto : CanalTemplate;
    }

    private async Task<bool> EnviarAsync(string phone, string canal, string texto, string template)
    {
        if (canal == CanalTexto)
        {
            await _whatsAppSender.SendTextMessageAsync(phone, texto);
            return true;
        }
        return await _whatsAppSender.SendTemplateMessageAsync(phone, template);
    }

    private static string Final(string phone)
    {
        var d = Glp1Phones.SomenteDigitos(phone);
        return d.Length <= 4 ? d : d[^4..];
    }
}
