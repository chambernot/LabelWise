using LabelWise.Application.Interfaces;
using LabelWise.Application.Interfaces.Persistence;
using LabelWise.Domain.Entities.Glp1;
using LabelWise.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LabelWise.Application.Services.Glp1;

// Ponto de entrada do Modo GLP-1 no bot. O WhatsAppController chama TryHandleAsync antes dos comandos fixos:
// true = a mensagem foi respondida aqui; false = o fluxo normal segue como antes.
// Telefones fora de Glp1:AllowedPhones sempre recebem false, sem nenhuma consulta ao banco.
public class Glp1WhatsAppHandler
{
    public const string RespostaReset = "Dados GLP-1 apagados. Pode recomeçar.";
    public const string RespostaNaoConfigurado = "Modo GLP-1 não configurado.";
    public const string RespostaComandoDesconhecido =
        "🧪 Não reconheci esse comando do Modo GLP-1. Você quis dizer:\n\n" +
        "• *glp1 status*: mostra seus dados GLP-1\n" +
        "• *glp1 reset*: apaga seus dados GLP-1";

    private static readonly string[] DiasDaSemana = { "domingo", "segunda", "terça", "quarta", "quinta", "sexta", "sábado" };

    private readonly IGlp1Repository _repository;
    private readonly IWhatsAppSenderService _whatsAppSender;
    private readonly ILogger<Glp1WhatsAppHandler> _logger;
    private readonly HashSet<string> _telefonesLiberados;

    public Glp1WhatsAppHandler(
        IGlp1Repository repository,
        IWhatsAppSenderService whatsAppSender,
        IOptions<Glp1Options> options,
        ILogger<Glp1WhatsAppHandler> logger)
    {
        _repository = repository;
        _whatsAppSender = whatsAppSender;
        _logger = logger;
        _telefonesLiberados = (options.Value.AllowedPhones ?? Array.Empty<string>())
            .Select(SomenteDigitos)
            .Where(x => x.Length > 0)
            .ToHashSet();
    }

    public bool TelefoneLiberado(string? phone)
    {
        return !string.IsNullOrWhiteSpace(phone) && _telefonesLiberados.Contains(SomenteDigitos(phone));
    }

    public async Task<bool> TryHandleAsync(string phone, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        // Sem espaços: "glp1 status", "glp1status" e "GLP-1 Status" viram "glp1status"
        var comando = NormalizarComando(text);
        bool pareceComandoGlp1 = comando.StartsWith("glp1", StringComparison.Ordinal);

        if (!TelefoneLiberado(phone))
        {
            if (pareceComandoGlp1)
            {
                _logger.LogInformation(
                    "Comando GLP-1 recebido de telefone fora de Glp1:AllowedPhones (final {Final}); {Quantidade} telefone(s) configurado(s).",
                    FinalDoTelefone(phone), _telefonesLiberados.Count);
            }
            return false;
        }

        switch (comando)
        {
            case "glp1status":
                await _whatsAppSender.SendTextMessageAsync(phone, await MontarStatusAsync(phone));
                return true;

            case "glp1reset":
                await _repository.ApagarDadosAsync(phone);
                await _whatsAppSender.SendTextMessageAsync(phone, RespostaReset);
                return true;

            default:
                // Começou com "glp1" mas não é um comando conhecido: pergunta em vez de mandar para a IA
                if (pareceComandoGlp1)
                {
                    await _whatsAppSender.SendTextMessageAsync(phone, RespostaComandoDesconhecido);
                    return true;
                }
                return false;
        }
    }

    private async Task<string> MontarStatusAsync(string phone)
    {
        var agoraUtc = DateTime.UtcNow;
        // Mesmo dia de Brasília (UTC-3 fixo) usado no restante do bot
        var inicioDiaUtc = DateTime.SpecifyKind(agoraUtc.AddHours(-3).Date.AddHours(3), DateTimeKind.Utc);

        var perfil = await _repository.ObterPerfilAsync(phone);
        var proteinaHoje = await _repository.ObterProteinaDoDiaAsync(phone, inicioDiaUtc);
        var aguaHoje = await _repository.ObterAguaDoDiaAsync(phone, inicioDiaUtc);
        var sintomas = await _repository.ContarSintomasAsync(phone);
        var alertas = await _repository.ContarAlertasAsync(phone);

        var sb = new StringBuilder();
        sb.AppendLine("🧪 *Modo GLP-1: status*");
        sb.AppendLine();

        if (perfil == null)
        {
            sb.AppendLine(RespostaNaoConfigurado);
        }
        else
        {
            sb.AppendLine($"Ativo: {SimNao(perfil.Enabled)}");
            sb.AppendLine($"Etapa de ativação: {Texto(perfil.ActivationStep)}");
            sb.AppendLine($"Consentimento: {DataHora(perfil.ConsentAt)} (versão {Texto(perfil.ConsentVersion)})");
            sb.AppendLine($"Acompanhamento médico: {SimNao(perfil.HasMedicalFollowUp)}");
            sb.AppendLine($"Medicamento: {Texto(perfil.Medication)}");
            sb.AppendLine($"Dose: {Texto(perfil.Dose)}");
            sb.AppendLine($"Dia da aplicação: {DiaDaSemana(perfil.InjectionWeekday)}");
            sb.AppendLine($"Altura: {Numero(perfil.HeightCm, "cm")}");
            sb.AppendLine($"Sexo: {Texto(perfil.Sex)}");
            sb.AppendLine($"Meta de proteína: {Numero(perfil.ProteinTargetG, "g")}");
            sb.AppendLine($"Piso calórico: {Numero(perfil.CalorieFloorKcal, "kcal")}");
            sb.AppendLine($"Meta de água: {Numero(perfil.WaterTargetMl, "ml")}");
            sb.AppendLine($"Lembretes desativados: {SimNao(perfil.RemindersOptOut)}");
            sb.AppendLine($"Criado em: {DataHora(perfil.CreatedAt)}");
            sb.AppendLine($"Atualizado em: {DataHora(perfil.UpdatedAt)}");
        }

        sb.AppendLine();
        sb.AppendLine("*Hoje*");
        sb.AppendLine($"Proteína: {proteinaHoje.ToString("0.#", CultureInfo.GetCultureInfo("pt-BR"))} g");
        sb.AppendLine($"Água: {aguaHoje} ml");
        sb.AppendLine($"Sintomas registrados: {sintomas}");
        sb.Append($"Alertas: {alertas}");

        return sb.ToString();
    }

    // "GLP-1 Status!" → "glp1status"
    private static string NormalizarComando(string text)
    {
        return Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9]", "");
    }

    private static string FinalDoTelefone(string? phone)
    {
        var digitos = SomenteDigitos(phone);
        return digitos.Length <= 4 ? digitos : digitos[^4..];
    }

    private static string SomenteDigitos(string? phone)
    {
        return new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
    }

    private static string Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? "—" : valor;

    private static string SimNao(bool? valor) => valor switch { true => "sim", false => "não", null => "—" };

    private static string Numero(decimal? valor, string unidade) =>
        valor.HasValue ? $"{valor.Value.ToString("0.#", CultureInfo.GetCultureInfo("pt-BR"))} {unidade}" : "—";

    private static string Numero(int? valor, string unidade) => valor.HasValue ? $"{valor.Value} {unidade}" : "—";

    private static string DiaDaSemana(int? dia) =>
        dia is >= 0 and <= 6 ? DiasDaSemana[dia.Value] : "—";

    // Horário de Brasília (UTC-3 fixo); data não preenchida (DateTime.MinValue) aparece como "—"
    private static string DataHora(DateTime? utc) =>
        utc.HasValue && utc.Value > DateTime.MinValue.AddDays(1)
            ? utc.Value.AddHours(-3).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
            : "—";
}
