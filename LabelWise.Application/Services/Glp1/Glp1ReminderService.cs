using LabelWise.Application.Interfaces;
using LabelWise.Application.Interfaces.Persistence;
using LabelWise.Domain.Entities.Glp1;
using LabelWise.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace LabelWise.Application.Services.Glp1;

public static class Glp1ReminderTypes
{
    public const string LembreteAplicacao = "LembreteAplicacao";
    public const string AlertaIngestao = "AlertaIngestao";
}

public sealed record Glp1ReminderSelection(string Phone, string Tipo, string Canal, string Motivo);

public sealed record Glp1ReminderRunResult(
    string Tipo,
    bool DryRun,
    string Observacao,
    IReadOnlyList<Glp1ReminderSelection> Selecionados,
    int Enviados,
    int Falhas);

// Resultado da regra de ingestão baixa: motivo (para o alerta da nutricionista) e o mínimo que vai no {{1}} do template
public sealed record Glp1LowIntake(string Motivo, string MinimoTexto);

// Lembretes do Modo GLP-1 (POST /api/whatsapp/send-glp1-reminders?tipo=lembrete|alerta). O cron externo escolhe a hora:
// - "lembrete" (às 9h): dia da aplicação, para quem tem InjectionWeekday = hoje (Brasília);
// - "alerta" (às 21h): ingestão baixa (3 dias seguidos com registro e média abaixo de 50% da proteína ou do piso calórico),
//   com um Glp1Alert IngestaoBaixa, no máximo a cada 3 dias.
// Só telefones de Glp1:AllowedPhones com o modo ativo e sem RemindersOptOut. Um envio de cada tipo por pessoa por dia.
public class Glp1ReminderService
{
    public const int DiasAnalisadosIngestao = 3;
    public const int DiasEntreAlertasIngestao = 3;
    public const decimal FracaoMinimaProteina = 0.5m;

    public const string CanalTexto = "texto";
    public const string CanalTemplate = "template";

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

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

    // ---------------------------------------------------------------- parâmetros do endpoint

    // "lembrete" | "alerta" → tipo interno; qualquer outro valor → nulo
    public static string? ParseTipo(string? tipo) => tipo?.Trim().ToLowerInvariant() switch
    {
        "lembrete" => Glp1ReminderTypes.LembreteAplicacao,
        "alerta" => Glp1ReminderTypes.AlertaIngestao,
        _ => null
    };

    // Devolve a mensagem de erro (400) ou nulo quando os parâmetros estão certos
    public static string? ValidarParametros(string? tipo, string? phone, bool forceTemplate)
    {
        if (ParseTipo(tipo) == null) return "Parâmetro obrigatório: tipo=lembrete ou tipo=alerta.";
        if (forceTemplate && string.IsNullOrWhiteSpace(phone)) return "forceTemplate=true só é aceito junto com phone=.";
        return null;
    }

    // ---------------------------------------------------------------- execução

    // tipo: Glp1ReminderTypes.*; somenteTelefone: roda só para esse telefone (as regras continuam valendo);
    // forceTemplate: usa o template mesmo dentro da janela de 24 h (para testar o template aprovado)
    public async Task<Glp1ReminderRunResult> ExecutarAsync(DateTime agoraUtc, string tipo, bool dryRun, string? somenteTelefone = null, bool forceTemplate = false)
    {
        if (tipo != Glp1ReminderTypes.LembreteAplicacao && tipo != Glp1ReminderTypes.AlertaIngestao)
        {
            throw new ArgumentException($"Tipo de lembrete GLP-1 inválido: {tipo}", nameof(tipo));
        }

        var filtrarTelefone = !string.IsNullOrWhiteSpace(somenteTelefone);
        var perfis = (await _repository.ListarPerfisAtivosAsync())
            .Where(p => _telefonesLiberados.Contains(Glp1Phones.SomenteDigitos(p.Id)))
            .Where(p => !filtrarTelefone || Glp1Phones.SomenteDigitos(p.Id) == Glp1Phones.SomenteDigitos(somenteTelefone))
            .ToList();

        var diaBr = agoraUtc.AddHours(-3).ToString("yyyy-MM-dd");
        var selecionados = new List<Glp1ReminderSelection>();
        int enviados = 0, falhas = 0;

        foreach (var perfil in perfis)
        {
            if (!PodeReceberLembretes(perfil)) continue;

            try
            {
                if (await _repository.LembreteJaEnviadoAsync(perfil.Id, tipo, diaBr)) continue;

                string motivo, texto, parametro;
                Glp1LowIntake? ingestao = null;

                if (tipo == Glp1ReminderTypes.LembreteAplicacao)
                {
                    if (!EhDiaDaAplicacao(perfil, agoraUtc)) continue;
                    motivo = "Hoje é o dia da aplicação";
                    parametro = perfil.ProteinTargetG!.Value.ToString(PtBr);
                    texto = Glp1ReminderTexts.LembreteAplicacao(perfil.ProteinTargetG.Value);
                }
                else
                {
                    ingestao = await AvaliarIngestaoBaixaAsync(perfil, agoraUtc);
                    if (ingestao == null) continue;
                    motivo = ingestao.Motivo;
                    parametro = ingestao.MinimoTexto;
                    texto = Glp1ReminderTexts.AlertaIngestao(ingestao.MinimoTexto);
                }

                var canal = forceTemplate ? CanalTemplate : await CanalAsync(perfil.Id, agoraUtc);
                selecionados.Add(new Glp1ReminderSelection(perfil.Id, tipo, canal, motivo));

                if (dryRun || !await _repository.RegistrarEnvioLembreteAsync(perfil.Id, tipo, diaBr, agoraUtc)) continue;

                if (ingestao != null)
                {
                    await CriarAlertaIngestaoAsync(perfil.Id, ingestao.Motivo, agoraUtc);
                }

                if (await EnviarAsync(perfil.Id, tipo, canal, texto, parametro)) enviados++; else falhas++;
            }
            catch (Exception ex)
            {
                falhas++;
                _logger.LogError(ex, "Falha no lembrete GLP-1 {Tipo} do telefone final {Final}.", tipo, Final(perfil.Id));
            }
        }

        var observacao = dryRun
            ? $"Simulação: {selecionados.Count} envio(s) seriam feitos. Nada foi enviado."
            : $"Lembretes GLP-1 ({tipo}) concluídos. Enviados: {enviados}, Falhas: {falhas}.";

        return new Glp1ReminderRunResult(tipo, dryRun, observacao, selecionados, enviados, falhas);
    }

    // ---------------------------------------------------------------- regras (públicas para teste)

    public static bool PodeReceberLembretes(Glp1Profile perfil) =>
        perfil.Enabled && string.IsNullOrEmpty(perfil.ActivationStep) && perfil.ProteinTargetG.HasValue && !perfil.RemindersOptOut;

    // InjectionWeekday: 0 = domingo ... 6 = sábado, no dia de Brasília
    public static bool EhDiaDaAplicacao(Glp1Profile perfil, DateTime agoraUtc) =>
        perfil.InjectionWeekday.HasValue && perfil.InjectionWeekday.Value == (int)agoraUtc.AddHours(-3).DayOfWeek;

    // Hoje, ontem e anteontem, todos com refeição registrada, e média abaixo de 50% da proteína ou abaixo do piso calórico.
    // O mínimo do template segue o critério que disparou; se forem os dois, vale a proteína (foco do Modo GLP-1).
    public static Glp1LowIntake? AvaliarIngestaoBaixa(Glp1Profile perfil, IReadOnlyList<Glp1DayIntake> ultimosDias)
    {
        if (ultimosDias.Count < DiasAnalisadosIngestao || ultimosDias.Any(d => d.Meals == 0)) return null;

        var mediaProteina = ultimosDias.Average(d => d.ProteinG);
        var mediaCalorias = (int)Math.Round(ultimosDias.Average(d => (decimal)d.CaloriesKcal));
        var metaProteina = perfil.ProteinTargetG ?? 0;
        var piso = perfil.CalorieFloorKcal ?? 0;

        bool proteinaBaixa = metaProteina > 0 && mediaProteina < metaProteina * FracaoMinimaProteina;
        bool caloriasBaixas = piso > 0 && mediaCalorias < piso;
        if (!proteinaBaixa && !caloriasBaixas) return null;

        var motivos = new List<string>();
        if (proteinaBaixa) motivos.Add($"proteína média de {mediaProteina:0} g (meta {metaProteina} g)");
        if (caloriasBaixas) motivos.Add($"média de {mediaCalorias} kcal (piso {piso} kcal)");

        var minimo = proteinaBaixa
            ? $"{metaProteina} g de proteína"
            : $"{piso.ToString("N0", PtBr)} kcal";

        return new Glp1LowIntake($"Últimos {DiasAnalisadosIngestao} dias: " + string.Join(" e ", motivos), minimo);
    }

    // ---------------------------------------------------------------- apoio

    private async Task<Glp1LowIntake?> AvaliarIngestaoBaixaAsync(Glp1Profile perfil, DateTime agoraUtc)
    {
        var ultimoAlerta = await _repository.ObterUltimoAlertaAsync(perfil.Id, Glp1AlertType.IngestaoBaixa);
        if (ultimoAlerta != null && ultimoAlerta.CreatedAt > agoraUtc.AddDays(-DiasEntreAlertasIngestao)) return null;

        var inicioHojeUtc = DateTime.SpecifyKind(agoraUtc.AddHours(-3).Date.AddHours(3), DateTimeKind.Utc);
        var dias = new List<Glp1DayIntake>();
        for (int i = 0; i < DiasAnalisadosIngestao; i++)
        {
            dias.Add(await _repository.ObterConsumoDoDiaAsync(perfil.Id, inicioHojeUtc.AddDays(-i)));
        }

        return AvaliarIngestaoBaixa(perfil, dias);
    }

    private Task CriarAlertaIngestaoAsync(string phone, string detalhes, DateTime agoraUtc) =>
        _repository.InserirAlertaAsync(new Glp1Alert { UserId = phone, Type = Glp1AlertType.IngestaoBaixa, Details = detalhes, CreatedAt = agoraUtc });

    // Dentro da janela de 24 h da Meta, texto livre; fora dela, só template aprovado é entregue
    private async Task<string> CanalAsync(string phone, DateTime agoraUtc)
    {
        var ultima = await _repository.ObterUltimaMensagemRecebidaAsync(phone);
        return ultima.HasValue && ultima.Value > agoraUtc.AddHours(-24) ? CanalTexto : CanalTemplate;
    }

    private async Task<bool> EnviarAsync(string phone, string tipo, string canal, string texto, string parametro)
    {
        if (canal == CanalTexto)
        {
            await _whatsAppSender.SendTextMessageAsync(phone, texto);
            return true;
        }

        var template = tipo == Glp1ReminderTypes.LembreteAplicacao ? _options.TemplateLembreteAplicacao : _options.TemplateAlertaIngestao;
        var resultado = await _whatsAppSender.SendTemplateMessageWithResultAsync(phone, template, parametro);

        // Sem o valor da variável nem o telefone completo: só o que ajuda a diagnosticar o envio
        if (resultado.Success)
        {
            _logger.LogInformation("[GLP-1] Template {Template} ({Tipo}) aceito pela Meta para o final {Final}. MessageId: {MessageId}",
                template, tipo, Final(phone), resultado.MessageId);
        }
        else
        {
            _logger.LogWarning("[GLP-1] Template {Template} ({Tipo}) recusado pela Meta para o final {Final}. Erro {ErrorCode}: {ErrorMessage}",
                template, tipo, Final(phone), resultado.ErrorCode, resultado.ErrorMessage);
        }

        return resultado.Success;
    }

    private static string Final(string phone)
    {
        var d = Glp1Phones.SomenteDigitos(phone);
        return d.Length <= 4 ? d : d[^4..];
    }
}
