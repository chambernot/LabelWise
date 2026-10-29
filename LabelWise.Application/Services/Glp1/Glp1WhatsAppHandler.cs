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
// Enquanto houver ActivationStep, TODA mensagem pertence à ativação: números isolados e textos com "kg"
// nunca chegam à remoção de refeição nem ao registro de peso do fluxo antigo.
// Regra fixa: o bot só registra a dose; nunca sugere, comenta ou avalia dose.
public class Glp1WhatsAppHandler
{
    public const string RespostaReset = "Dados GLP-1 apagados. Pode recomeçar.";
    public const string RespostaNaoConfigurado = "Modo GLP-1 não configurado.";
    public const string RespostaComandoDesconhecido =
        "🧪 Não reconheci esse comando do Modo GLP-1. Você quis dizer:\n\n" +
        "• *glp1*: ativa o modo ou mostra suas metas\n" +
        "• *glp1 status*: mostra seus dados GLP-1\n" +
        "• *sair glp1*: desativa o modo\n" +
        "• *glp1 reset*: apaga seus dados GLP-1";

    public const string PerguntaConsentimento =
        "Para acompanhar seu tratamento, vou guardar o medicamento, a dose que você informar e os sintomas que me contar. " +
        "São dados de saúde, usados só no seu acompanhamento. Você autoriza? Responda SIM ou NÃO.";
    public const string RespostaConsentimentoRecusado = "Tudo bem, não guardei nada.";

    public const string PerguntaAcompanhamento = "Seu tratamento tem acompanhamento médico? Responda SIM ou NÃO.";
    public const string RespostaSemAcompanhamento =
        "Entendi. O Modo GLP-1 é só para quem faz o tratamento com acompanhamento médico, " +
        "porque esses medicamentos precisam de avaliação e ajustes feitos por um profissional. " +
        "Recomendo procurar um médico para orientar o seu tratamento. 🩺\n\n" +
        "O Nutrição Fácil continua disponível normalmente para registrar suas refeições, água e treinos.";

    public const string PerguntaMedicamento =
        "Qual medicamento você usa?\n\n" +
        "1 Semaglutida (Ozempic, Wegovy ou genérico)\n" +
        "2 Tirzepatida (Mounjaro)\n" +
        "3 Liraglutida\n" +
        "4 Outro";

    public const string PerguntaDose = "Qual a sua dose atual? É só para registro, não vou comentar sobre dose.";

    public const string PerguntaDiaAplicacao =
        "Em que dia da semana você aplica?\n\n" +
        "1 Domingo\n2 Segunda\n3 Terça\n4 Quarta\n5 Quinta\n6 Sexta\n7 Sábado";

    public const string PerguntaDadosCorporais = "Me diga seu peso, altura e sexo numa mensagem só. Ex.: 72 kg, 1,65 m, feminino";

    public const string RespostaDesativado = "Modo GLP-1 desativado. Seus dados continuam guardados; mande GLP1 para reativar.";
    public const string RespostaNaoAtivo = "O Modo GLP-1 não está ativo. Mande GLP1 para ativar.";
    public const string RespostaAtivacaoCancelada = "Ativação do Modo GLP-1 cancelada. Não guardei nada.";

    private const string Comandos =
        "Comandos:\n" +
        "• *glp1 status*: mostra seus dados GLP-1\n" +
        "• *sair glp1*: desativa o modo";

    private static readonly string[] DiasDaSemana = { "domingo", "segunda", "terça", "quarta", "quinta", "sexta", "sábado" };
    private static readonly string[] DiasDaSemanaSemAcento = { "domingo", "segunda", "terca", "quarta", "quinta", "sexta", "sabado" };
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly IGlp1Repository _repository;
    private readonly IWhatsAppSenderService _whatsAppSender;
    private readonly Glp1GoalCalculator _calculator;
    private readonly Glp1Options _options;
    private readonly ILogger<Glp1WhatsAppHandler> _logger;
    private readonly HashSet<string> _telefonesLiberados;

    public Glp1WhatsAppHandler(
        IGlp1Repository repository,
        IWhatsAppSenderService whatsAppSender,
        Glp1GoalCalculator calculator,
        IOptions<Glp1Options> options,
        ILogger<Glp1WhatsAppHandler> logger)
    {
        _repository = repository;
        _whatsAppSender = whatsAppSender;
        _calculator = calculator;
        _options = options.Value;
        _logger = logger;
        _telefonesLiberados = (_options.AllowedPhones ?? Array.Empty<string>())
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

        // Sem espaços nem acentos: "glp1 status", "glp1status" e "GLP-1 Status" viram "glp1status"
        var comando = NormalizarComando(text);
        bool pareceComandoGlp1 = comando.StartsWith("glp1", StringComparison.Ordinal);

        if (!TelefoneLiberado(phone))
        {
            if (pareceComandoGlp1 || comando == "modoglp1" || comando == "sairglp1")
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
                await EnviarAsync(phone, await MontarStatusAsync(phone));
                return true;

            case "glp1reset":
                await _repository.ApagarDadosAsync(phone);
                await EnviarAsync(phone, RespostaReset);
                return true;

            case "glp1":
            case "modoglp1":
                await IniciarOuMostrarAsync(phone);
                return true;

            case "sairglp1":
                await SairAsync(phone);
                return true;
        }

        var perfil = await _repository.ObterPerfilAsync(phone);
        if (!string.IsNullOrEmpty(perfil?.ActivationStep))
        {
            await ProcessarEtapaAsync(perfil, text);
            return true;
        }

        // Começou com "glp1" mas não é um comando conhecido: pergunta em vez de mandar para a IA
        if (pareceComandoGlp1)
        {
            await EnviarAsync(phone, RespostaComandoDesconhecido);
            return true;
        }

        return false;
    }

    // ---------------------------------------------------------------- gatilho e saída

    private async Task IniciarOuMostrarAsync(string phone)
    {
        var perfil = await _repository.ObterPerfilAsync(phone);
        var agora = DateTime.UtcNow;

        if (perfil == null)
        {
            perfil = new Glp1Profile { Id = phone, CreatedAt = agora };
            await IrParaEtapaAsync(perfil, Glp1ActivationSteps.Consentimento, PerguntaConsentimento);
            return;
        }

        if (!string.IsNullOrEmpty(perfil.ActivationStep))
        {
            await EnviarAsync(phone, "Vamos continuar de onde paramos.\n\n" + PerguntaDaEtapa(perfil));
            return;
        }

        if (perfil.Enabled && AtivacaoConcluida(perfil))
        {
            await EnviarAsync(phone, "O Modo GLP-1 já está ativo ✅\n\n" + TextoMetas(perfil) + "\n\n" + Comandos);
            return;
        }

        if (AtivacaoConcluida(perfil))
        {
            // Reativação direta: consentimento e dados já foram informados antes
            perfil.Enabled = true;
            perfil.UpdatedAt = agora;
            await _repository.SalvarPerfilAsync(perfil);
            await EnviarAsync(phone, "Modo GLP-1 reativado ✅\n\n" + TextoMetas(perfil) + "\n\n" + Comandos);
            return;
        }

        // Perfil sem etapa e sem metas (estado incompleto): recomeça do consentimento
        await IrParaEtapaAsync(perfil, Glp1ActivationSteps.Consentimento, PerguntaConsentimento);
    }

    private async Task SairAsync(string phone)
    {
        var perfil = await _repository.ObterPerfilAsync(phone);

        if (perfil != null && !string.IsNullOrEmpty(perfil.ActivationStep))
        {
            await _repository.ApagarPerfilAsync(phone);
            await EnviarAsync(phone, RespostaAtivacaoCancelada);
            return;
        }

        if (perfil == null || !perfil.Enabled)
        {
            await EnviarAsync(phone, RespostaNaoAtivo);
            return;
        }

        perfil.Enabled = false;
        perfil.UpdatedAt = DateTime.UtcNow;
        await _repository.SalvarPerfilAsync(perfil);
        await EnviarAsync(phone, RespostaDesativado);
    }

    // ---------------------------------------------------------------- etapas da ativação

    private async Task ProcessarEtapaAsync(Glp1Profile perfil, string text)
    {
        var resposta = NormalizarResposta(text);

        if (resposta == "cancelar")
        {
            await _repository.ApagarPerfilAsync(perfil.Id);
            await EnviarAsync(perfil.Id, RespostaAtivacaoCancelada);
            return;
        }

        switch (perfil.ActivationStep)
        {
            case Glp1ActivationSteps.Consentimento:
                await EtapaConsentimentoAsync(perfil, resposta);
                break;
            case Glp1ActivationSteps.AcompanhamentoMedico:
                await EtapaAcompanhamentoAsync(perfil, resposta);
                break;
            case Glp1ActivationSteps.Medicamento:
                await EtapaMedicamentoAsync(perfil, resposta);
                break;
            case Glp1ActivationSteps.Dose:
                await EtapaDoseAsync(perfil, text);
                break;
            case Glp1ActivationSteps.DiaAplicacao:
                await EtapaDiaAplicacaoAsync(perfil, resposta);
                break;
            case Glp1ActivationSteps.DadosCorporais:
                await EtapaDadosCorporaisAsync(perfil, text);
                break;
            default:
                // Etapa desconhecida (dado antigo ou corrompido): recomeça do consentimento
                await IrParaEtapaAsync(perfil, Glp1ActivationSteps.Consentimento, PerguntaConsentimento);
                break;
        }
    }

    private async Task EtapaConsentimentoAsync(Glp1Profile perfil, string resposta)
    {
        switch (LerSimNao(resposta))
        {
            case true:
                perfil.ConsentAt = DateTime.UtcNow;
                perfil.ConsentVersion = _options.ConsentVersion;
                await IrParaEtapaAsync(perfil, Glp1ActivationSteps.AcompanhamentoMedico, PerguntaAcompanhamento);
                break;
            case false:
                await _repository.ApagarPerfilAsync(perfil.Id);
                await EnviarAsync(perfil.Id, RespostaConsentimentoRecusado);
                break;
            default:
                await RepetirAsync(perfil, "Responda SIM para autorizar ou NÃO para não autorizar. Ex.: SIM");
                break;
        }
    }

    private async Task EtapaAcompanhamentoAsync(Glp1Profile perfil, string resposta)
    {
        switch (LerSimNao(resposta))
        {
            case true:
                perfil.HasMedicalFollowUp = true;
                await IrParaEtapaAsync(perfil, Glp1ActivationSteps.Medicamento, PerguntaMedicamento);
                break;
            case false:
                // Encerra sem ativar e sem guardar dados de saúde
                await _repository.ApagarPerfilAsync(perfil.Id);
                await EnviarAsync(perfil.Id, RespostaSemAcompanhamento);
                break;
            default:
                await RepetirAsync(perfil, "Responda SIM ou NÃO. Ex.: SIM");
                break;
        }
    }

    private async Task EtapaMedicamentoAsync(Glp1Profile perfil, string resposta)
    {
        var medicamento = LerMedicamento(resposta);
        if (medicamento == null)
        {
            await RepetirAsync(perfil, "Responda com o número da opção. Ex.: 1");
            return;
        }

        perfil.Medication = medicamento;
        await IrParaEtapaAsync(perfil, Glp1ActivationSteps.Dose, PerguntaDose);
    }

    private async Task EtapaDoseAsync(Glp1Profile perfil, string text)
    {
        // Texto livre, só para registro. Não interpretamos nem comentamos o valor.
        var dose = Regex.Replace(text, @"\s+", " ").Trim();
        if (!Regex.IsMatch(dose, @"[\p{L}\p{N}]"))
        {
            await RepetirAsync(perfil, "Escreva a dose como está na sua receita. Ex.: 0,5 mg");
            return;
        }

        perfil.Dose = dose.Length > 60 ? dose[..60] : dose;
        await IrParaEtapaAsync(perfil, Glp1ActivationSteps.DiaAplicacao, PerguntaDiaAplicacao);
    }

    private async Task EtapaDiaAplicacaoAsync(Glp1Profile perfil, string resposta)
    {
        var dia = LerDiaDaSemana(resposta);
        if (dia == null)
        {
            await RepetirAsync(perfil, "Responda com o número do dia. Ex.: 2 (segunda)");
            return;
        }

        perfil.InjectionWeekday = dia;
        await IrParaEtapaAsync(perfil, Glp1ActivationSteps.DadosCorporais, PerguntaDadosCorporais);
    }

    private async Task EtapaDadosCorporaisAsync(Glp1Profile perfil, string text)
    {
        var lido = Glp1BodyDataParser.Ler(
            text,
            faltaPeso: perfil.PendingWeightKg == null,
            faltaAltura: perfil.HeightCm == null,
            faltaSexo: string.IsNullOrEmpty(perfil.Sex));

        perfil.PendingWeightKg ??= lido.PesoKg;
        perfil.HeightCm ??= lido.AlturaCm;
        if (string.IsNullOrEmpty(perfil.Sex)) perfil.Sex = lido.Sexo;

        var faltando = DadosFaltando(perfil);
        if (faltando.Count > 0)
        {
            perfil.UpdatedAt = DateTime.UtcNow;
            await _repository.SalvarPerfilAsync(perfil);

            var prefixo = lido.Vazio ? "Não entendi." : "Anotei.";
            await EnviarAsync(perfil.Id, $"{prefixo} Agora me diga {JuntarFaltando(faltando)}.");
            return;
        }

        await ConcluirAtivacaoAsync(perfil);
    }

    private async Task ConcluirAtivacaoAsync(Glp1Profile perfil)
    {
        var agora = DateTime.UtcNow;
        var peso = perfil.PendingWeightKg!.Value;
        var metas = _calculator.Calcular(peso);

        await _repository.RegistrarPesoAsync(perfil.Id, peso, agora);

        perfil.ProteinTargetG = metas.ProteinTargetG;
        perfil.CalorieFloorKcal = metas.CalorieFloorKcal;
        perfil.WaterTargetMl = metas.WaterTargetMl;
        perfil.Enabled = true;
        perfil.ConsentAt ??= agora;
        perfil.ConsentVersion ??= _options.ConsentVersion;
        perfil.ActivationStep = null;
        perfil.PendingWeightKg = null;
        perfil.UpdatedAt = agora;
        await _repository.SalvarPerfilAsync(perfil);

        await EnviarAsync(perfil.Id, "Modo GLP-1 ativado ✅\n\n" + TextoMetas(perfil));
    }

    private async Task IrParaEtapaAsync(Glp1Profile perfil, string etapa, string pergunta)
    {
        perfil.ActivationStep = etapa;
        perfil.UpdatedAt = DateTime.UtcNow;
        await _repository.SalvarPerfilAsync(perfil);
        await EnviarAsync(perfil.Id, pergunta);
    }

    // Resposta inválida: repete a pergunta da etapa atual com um exemplo
    private Task RepetirAsync(Glp1Profile perfil, string exemplo)
    {
        return EnviarAsync(perfil.Id, $"Não entendi. {exemplo}\n\n{PerguntaDaEtapa(perfil)}\n\n_(Mande CANCELAR para sair da ativação.)_");
    }

    private static string PerguntaDaEtapa(Glp1Profile perfil) => perfil.ActivationStep switch
    {
        Glp1ActivationSteps.Consentimento => PerguntaConsentimento,
        Glp1ActivationSteps.AcompanhamentoMedico => PerguntaAcompanhamento,
        Glp1ActivationSteps.Medicamento => PerguntaMedicamento,
        Glp1ActivationSteps.Dose => PerguntaDose,
        Glp1ActivationSteps.DiaAplicacao => PerguntaDiaAplicacao,
        Glp1ActivationSteps.DadosCorporais => DadosFaltando(perfil).Count is > 0 and < 3
            ? $"Me diga {JuntarFaltando(DadosFaltando(perfil))}."
            : PerguntaDadosCorporais,
        _ => PerguntaConsentimento
    };

    private static List<string> DadosFaltando(Glp1Profile perfil)
    {
        var faltando = new List<string>();
        if (perfil.PendingWeightKg == null) faltando.Add("seu peso (ex.: 72 kg)");
        if (perfil.HeightCm == null) faltando.Add("sua altura (ex.: 1,65 m)");
        if (string.IsNullOrEmpty(perfil.Sex)) faltando.Add("seu sexo (feminino ou masculino)");
        return faltando;
    }

    private static string JuntarFaltando(List<string> itens) =>
        itens.Count == 1 ? itens[0] : string.Join(", ", itens.Take(itens.Count - 1)) + " e " + itens[^1];

    private static bool AtivacaoConcluida(Glp1Profile perfil) =>
        string.IsNullOrEmpty(perfil.ActivationStep) && perfil.ProteinTargetG.HasValue;

    private static string TextoMetas(Glp1Profile perfil)
    {
        var agua = perfil.WaterTargetMl.HasValue
            ? Math.Round(perfil.WaterTargetMl.Value / 1000m, 1, MidpointRounding.AwayFromZero).ToString("0.0", PtBr)
            : "—";
        var piso = perfil.CalorieFloorKcal?.ToString("N0", PtBr) ?? "—";

        return "Suas metas:\n" +
               $"🥩 proteína {perfil.ProteinTargetG?.ToString() ?? "—"} g/dia\n" +
               $"🔥 mínimo de {piso} kcal/dia\n" +
               $"💧 água {agua} L/dia";
    }

    // ---------------------------------------------------------------- leitura das respostas

    private static bool? LerSimNao(string resposta) => resposta switch
    {
        "sim" or "s" or "si" or "autorizo" or "sim autorizo" or "aceito" or "sim aceito" or "tenho" or "sim tenho" or "claro" => true,
        "nao" or "n" or "nao autorizo" or "nao aceito" or "nao tenho" => false,
        _ => null
    };

    private static string? LerMedicamento(string resposta)
    {
        if (resposta == "1" || Regex.IsMatch(resposta, @"\b(semaglutida|ozempic|wegovy|rybelsus)\b")) return "Semaglutida";
        if (resposta == "2" || Regex.IsMatch(resposta, @"\b(tirzepatida|mounjaro)\b")) return "Tirzepatida";
        if (resposta == "3" || Regex.IsMatch(resposta, @"\b(liraglutida|saxenda|victoza)\b")) return "Liraglutida";
        if (resposta == "4" || resposta == "outro" || resposta == "outra") return "Outro";
        return null;
    }

    // 1 = domingo ... 7 = sábado → 0 ... 6
    private static int? LerDiaDaSemana(string resposta)
    {
        if (int.TryParse(resposta, out var numero))
        {
            return numero is >= 1 and <= 7 ? numero - 1 : null;
        }

        for (int i = 0; i < DiasDaSemanaSemAcento.Length; i++)
        {
            if (resposta.StartsWith(DiasDaSemanaSemAcento[i], StringComparison.Ordinal)) return i;
        }
        return null;
    }

    // ---------------------------------------------------------------- status

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
            if (perfil.PendingWeightKg.HasValue)
            {
                sb.AppendLine($"Peso informado na ativação: {Numero(perfil.PendingWeightKg, "kg")}");
            }
            sb.AppendLine($"Meta de proteína: {Numero(perfil.ProteinTargetG, "g")}");
            sb.AppendLine($"Piso calórico: {Numero(perfil.CalorieFloorKcal, "kcal")}");
            sb.AppendLine($"Meta de água: {Numero(perfil.WaterTargetMl, "ml")}");
            sb.AppendLine($"Lembretes desativados: {SimNao(perfil.RemindersOptOut)}");
            sb.AppendLine($"Criado em: {DataHora(perfil.CreatedAt)}");
            sb.AppendLine($"Atualizado em: {DataHora(perfil.UpdatedAt)}");
        }

        sb.AppendLine();
        sb.AppendLine("*Hoje*");
        sb.AppendLine($"Proteína: {proteinaHoje.ToString("0.#", PtBr)} g");
        sb.AppendLine($"Água: {aguaHoje} ml");
        sb.AppendLine($"Sintomas registrados: {sintomas}");
        sb.Append($"Alertas: {alertas}");

        return sb.ToString();
    }

    // ---------------------------------------------------------------- utilitários

    private Task EnviarAsync(string phone, string texto) => _whatsAppSender.SendTextMessageAsync(phone, texto);

    // "GLP-1 Status!" → "glp1status"
    private static string NormalizarComando(string text)
    {
        return Regex.Replace(Glp1BodyDataParser.SemAcentos(text).ToLowerInvariant(), "[^a-z0-9]", "");
    }

    // "Não." → "nao"; "  Sábado " → "sabado"
    private static string NormalizarResposta(string text)
    {
        var t = Regex.Replace(Glp1BodyDataParser.SemAcentos(text).ToLowerInvariant(), "[^a-z0-9 ]", " ");
        return Regex.Replace(t, @"\s+", " ").Trim();
    }

    private static string SomenteDigitos(string? phone)
    {
        return new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
    }

    private static string FinalDoTelefone(string? phone)
    {
        var digitos = SomenteDigitos(phone);
        return digitos.Length <= 4 ? digitos : digitos[^4..];
    }

    private static string Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? "—" : valor;

    private static string SimNao(bool? valor) => valor switch { true => "sim", false => "não", null => "—" };

    private static string Numero(decimal? valor, string unidade) =>
        valor.HasValue ? $"{valor.Value.ToString("0.#", PtBr)} {unidade}" : "—";

    private static string Numero(int? valor, string unidade) => valor.HasValue ? $"{valor.Value} {unidade}" : "—";

    private static string DiaDaSemana(int? dia) =>
        dia is >= 0 and <= 6 ? DiasDaSemana[dia.Value] : "—";

    // Horário de Brasília (UTC-3 fixo); data não preenchida (DateTime.MinValue) aparece como "—"
    private static string DataHora(DateTime? utc) =>
        utc.HasValue && utc.Value > DateTime.MinValue.AddDays(1)
            ? utc.Value.AddHours(-3).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
            : "—";
}
