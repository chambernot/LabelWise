using LabelWise.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LabelWise.Api.Controllers
{
    // Painel gerencial do dono da aplicação (wwwroot/admin.html).
    // Acesso exclusivo pelo header X-Admin-Secret, que precisa bater com a configuração AdminSecret.
    [ApiController]
    [Route("api/admin/users")]
    [AllowAnonymous]
    public class AdminUsersController : ControllerBase
    {
        private const int TrialDurationDays = 15; // mesmo valor do WhatsAppController
        private const int DiasEmRisco = 3;
        private const int DiasInativoPadrao = 7;
        private const int MinDiasUsoIntensoPadrao = 5;
        private const int JanelaFrequenciaDias = 30;
        private const int MaxDestinatariosPorEnvio = 100;
        private const string ColecaoEnvios = TemplateDeliveryTracker.ColecaoEnvios;

        private static readonly Regex NomeTemplateValido = new("^[a-z0-9_]{1,512}$", RegexOptions.Compiled);

        private readonly IMongoDatabase _database;
        private readonly IWhatsAppSenderService _whatsAppSender;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminUsersController> _logger;

        public AdminUsersController(
            IMongoDatabase database,
            IWhatsAppSenderService whatsAppSender,
            IConfiguration configuration,
            ILogger<AdminUsersController> logger)
        {
            _database = database;
            _whatsAppSender = whatsAppSender;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("verify")]
        public IActionResult Verify([FromHeader(Name = "X-Admin-Secret")] string? secret)
        {
            return ValidarAcesso(secret) ?? Ok(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers(
            [FromHeader(Name = "X-Admin-Secret")] string? secret,
            [FromQuery] int diasInativo = DiasInativoPadrao,
            [FromQuery] int minDiasUsoIntenso = MinDiasUsoIntensoPadrao)
        {
            var negado = ValidarAcesso(secret);
            if (negado != null) return negado;

            diasInativo = Math.Clamp(diasInativo, DiasEmRisco + 1, 365);
            minDiasUsoIntenso = Math.Clamp(minDiasUsoIntenso, 1, TrialDurationDays);

            try
            {
                var usuarios = await MontarUsuariosAsync(diasInativo, minDiasUsoIntenso);

                var resumo = new
                {
                    total = usuarios.Count,
                    ativos = usuarios.Count(x => x.Status == "Ativo"),
                    emRisco = usuarios.Count(x => x.Status == "Em risco"),
                    inativos = usuarios.Count(x => x.Status == "Inativo"),
                    semInteracao = usuarios.Count(x => x.Status == "Sem interação"),
                    nuncaUsou = usuarios.Count(x => x.Segmento == SegmentoNuncaUsou),
                    usouEParou = usuarios.Count(x => x.Segmento == SegmentoUsouEParou),
                    trialAcabouUsoIntenso = usuarios.Count(x => x.Segmento == SegmentoTrialAcabou),
                    trialAtivo = usuarios.Count(x => x.Plano == "Trial"),
                    trialExpirado = usuarios.Count(x => x.Plano == "Trial expirado"),
                    premium = usuarios.Count(x => x.Plano == "Premium"),
                    b2b = usuarios.Count(x => x.Plano == "B2B")
                };

                return Ok(new
                {
                    success = true,
                    geradoEm = DateTime.UtcNow,
                    parametros = new { diasInativo, minDiasUsoIntenso, diasEmRisco = DiasEmRisco, janelaFrequenciaDias = JanelaFrequenciaDias },
                    resumo,
                    data = usuarios
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao montar o painel gerencial de usuários.");
                return StatusCode(500, new { success = false, message = "Erro interno ao montar o painel de usuários." });
            }
        }

        [HttpPost("send-template")]
        public async Task<IActionResult> EnviarTemplate(
            [FromHeader(Name = "X-Admin-Secret")] string? secret,
            [FromBody] AdminSendTemplateRequest request)
        {
            var negado = ValidarAcesso(secret);
            if (negado != null) return negado;

            var templateName = request.TemplateName?.Trim() ?? "";
            if (!NomeTemplateValido.IsMatch(templateName))
            {
                return BadRequest(new { success = false, message = "Nome de template inválido. Use o nome exato aprovado na Meta (letras minúsculas, números e _)." });
            }

            var phones = (request.Phones ?? new List<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct()
                .ToList();

            if (phones.Count == 0 || phones.Count > MaxDestinatariosPorEnvio)
            {
                return BadRequest(new { success = false, message = $"Informe entre 1 e {MaxDestinatariosPorEnvio} destinatários por envio." });
            }

            try
            {
                var usuarios = (await MontarUsuariosAsync(DiasInativoPadrao, MinDiasUsoIntensoPadrao)).ToDictionary(x => x.Phone);
                var envios = _database.GetCollection<BsonDocument>(ColecaoEnvios);

                // Telefone → data do envio anterior deste template (aceito pela Meta e sem falha de entrega)
                var receberamRecentemente = new Dictionary<string, DateTime>();
                if (request.SkipIfSentWithinDays > 0)
                {
                    var filtroRecentes = Builders<BsonDocument>.Filter.And(
                        Builders<BsonDocument>.Filter.Eq("Template", templateName),
                        Builders<BsonDocument>.Filter.Eq("Success", true),
                        Builders<BsonDocument>.Filter.Ne("DeliveryStatus", TemplateDeliveryTracker.StatusFalhou),
                        Builders<BsonDocument>.Filter.Gte("SentAt", DateTime.UtcNow.AddDays(-request.SkipIfSentWithinDays)));
                    var recentes = await envios.Find(filtroRecentes)
                        .Project(Builders<BsonDocument>.Projection.Include("UserId").Include("SentAt"))
                        .ToListAsync();
                    foreach (var r in recentes)
                    {
                        if (!r.TryGetValue("UserId", out var uid) || !uid.IsString ||
                            !r.TryGetValue("SentAt", out var sentAt) || !sentAt.IsBsonDateTime) continue;
                        var quando = sentAt.ToUniversalTime();
                        if (!receberamRecentemente.TryGetValue(uid.AsString, out var atual) || quando > atual)
                            receberamRecentemente[uid.AsString] = quando;
                    }
                }

                var resultados = new List<object>();
                int enviados = 0, falhas = 0, ignorados = 0;

                foreach (var phone in phones)
                {
                    if (!usuarios.TryGetValue(phone, out var usuario))
                    {
                        ignorados++;
                        resultados.Add(new { phone, status = "ignorado", motivo = "Usuário não encontrado na base." });
                        continue;
                    }

                    if (receberamRecentemente.TryGetValue(phone, out var envioAnterior))
                    {
                        ignorados++;
                        resultados.Add(new
                        {
                            phone,
                            status = "ignorado",
                            motivo = $"Já recebeu este template em {envioAnterior.AddHours(-3):dd/MM 'às' HH:mm}. Para reenviar, desmarque \"Não reenviar\"."
                        });
                        continue;
                    }

                    var parametros = (request.Parameters ?? new List<string>())
                        .Select(p => PersonalizarParametro(p, usuario))
                        .ToArray();

                    var envio = await _whatsAppSender.SendTemplateMessageWithResultAsync(phone, templateName, parametros);
                    var statusInicial = envio.Success ? TemplateDeliveryTracker.StatusAceito : TemplateDeliveryTracker.StatusFalhou;
                    var agoraEnvio = DateTime.UtcNow;

                    await envios.InsertOneAsync(new BsonDocument
                    {
                        { "_id", Guid.NewGuid().ToString() },
                        { "UserId", phone },
                        { "Template", templateName },
                        { "Parameters", new BsonArray(parametros) },
                        { "Segment", request.Segment != null ? new BsonString(request.Segment) : BsonNull.Value },
                        { "Success", envio.Success },
                        { "MessageId", envio.MessageId != null ? new BsonString(envio.MessageId) : BsonNull.Value },
                        { "DeliveryStatus", statusInicial },
                        { "DeliveryStatusRank", TemplateDeliveryTracker.Rank(statusInicial) },
                        { "DeliveryUpdatedAt", agoraEnvio },
                        { "ErrorCode", envio.ErrorCode != null ? new BsonString(envio.ErrorCode) : BsonNull.Value },
                        { "ErrorMessage", envio.ErrorMessage != null ? new BsonString(envio.ErrorMessage) : BsonNull.Value },
                        { "SentAt", agoraEnvio }
                    });

                    if (envio.Success)
                    {
                        enviados++;
                        resultados.Add(new { phone, status = "enviado", motivo = (string?)null });
                    }
                    else
                    {
                        falhas++;
                        resultados.Add(new
                        {
                            phone,
                            status = "falha",
                            motivo = $"A Meta recusou o envio: {envio.ErrorMessage ?? "erro desconhecido"}" + (envio.ErrorCode != null ? $" (código {envio.ErrorCode})" : "")
                        });
                    }
                }

                _logger.LogInformation("[Painel Gerencial] Template {Template}: {Enviados} enviados, {Falhas} falhas, {Ignorados} ignorados.",
                    templateName, enviados, falhas, ignorados);

                return Ok(new { success = true, enviados, falhas, ignorados, resultados });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no envio de template pelo painel gerencial.");
                return StatusCode(500, new { success = false, message = "Erro interno ao enviar as mensagens." });
            }
        }

        // Últimos envios do painel, com o andamento da entrega informado pela Meta
        [HttpGet("sends")]
        public async Task<IActionResult> GetEnviosRecentes(
            [FromHeader(Name = "X-Admin-Secret")] string? secret,
            [FromQuery] int limit = 50)
        {
            var negado = ValidarAcesso(secret);
            if (negado != null) return negado;

            limit = Math.Clamp(limit, 1, 200);

            var docs = await _database.GetCollection<BsonDocument>(ColecaoEnvios)
                .Find(FilterDefinition<BsonDocument>.Empty)
                .Sort(Builders<BsonDocument>.Sort.Descending("SentAt"))
                .Limit(limit)
                .ToListAsync();

            var data = docs.Select(d => new
            {
                phone = TextoOuNull(d, "UserId"),
                template = TextoOuNull(d, "Template"),
                segment = TextoOuNull(d, "Segment"),
                parameters = d.TryGetValue("Parameters", out var ps) && ps.IsBsonArray
                    ? ps.AsBsonArray.Where(x => x.IsString).Select(x => x.AsString).ToList()
                    : new List<string>(),
                sentAt = DataOuNull(d, "SentAt"),
                // Envios anteriores a este ajuste não têm DeliveryStatus: só se sabe se a Meta aceitou
                deliveryStatus = TextoOuNull(d, "DeliveryStatus")
                    ?? (d.TryGetValue("Success", out var ok) && ok.IsBoolean && ok.AsBoolean ? "accepted_legacy" : TemplateDeliveryTracker.StatusFalhou),
                deliveryUpdatedAt = DataOuNull(d, "DeliveryUpdatedAt"),
                messageId = TextoOuNull(d, "MessageId"),
                errorCode = TextoOuNull(d, "ErrorCode"),
                errorMessage = TextoOuNull(d, "ErrorMessage")
            });

            return Ok(new { success = true, data });
        }

        private static string? TextoOuNull(BsonDocument d, string campo) =>
            d.TryGetValue(campo, out var v) && v.IsString ? v.AsString : null;

        private static DateTime? DataOuNull(BsonDocument d, string campo) =>
            d.TryGetValue(campo, out var v) && v.IsBsonDateTime ? v.ToUniversalTime() : null;

        private IActionResult? ValidarAcesso(string? secret)
        {
            var esperado = _configuration["AdminSecret"]?.Trim();
            if (string.IsNullOrEmpty(esperado))
            {
                return StatusCode(503, new { success = false, message = "AdminSecret não está configurado no servidor." });
            }

            if (string.IsNullOrEmpty(secret) ||
                !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(secret.Trim()), Encoding.UTF8.GetBytes(esperado)))
            {
                return Unauthorized(new { success = false, message = "Chave de administrador inválida." });
            }

            return null;
        }

        #region Montagem dos usuários

        private const string SegmentoNuncaUsou = "nunca_usou";
        private const string SegmentoUsouEParou = "usou_e_parou";
        private const string SegmentoTrialAcabou = "trial_acabou_uso_intenso";

        private sealed class Atividade
        {
            public DateTime? Ultima;
            public string? OrigemUltima;
            public readonly HashSet<string> Dias = new();
            public readonly Dictionary<string, int> RefeicoesPorDia = new();

            public void Registrar(string dia, DateTime quando, string origem)
            {
                Dias.Add(dia);
                if (Ultima == null || quando > Ultima)
                {
                    Ultima = quando;
                    OrigemUltima = origem;
                }
            }
        }

        private async Task<List<AdminUserRow>> MontarUsuariosAsync(int diasInativo, int minDiasUsoIntenso)
        {
            var agora = DateTime.UtcNow;
            var hojeBr = agora.AddHours(-3).Date;
            var corteFrequencia = hojeBr.AddDays(-(JanelaFrequenciaDias - 1)).ToString("yyyy-MM-dd");

            var atividades = new Dictionary<string, Atividade>();
            Atividade Obter(string phone)
            {
                if (!atividades.TryGetValue(phone, out var a))
                {
                    a = new Atividade();
                    atividades[phone] = a;
                }
                return a;
            }

            // 1. Atividade a partir dos registros (cobre também o período anterior ao UserActivity)
            await AgregarLogsAsync("Nutrition_MealLogs", "LoggedAt", "Refeição", null, contaRefeicoes: true, Obter);
            await AgregarLogsAsync("MealLogs", "LoggedAt", "Treino", null, contaRefeicoes: false, Obter); // treinos antigos
            await AgregarLogsAsync("WaterLogs", "LoggedAt", "Água", null, contaRefeicoes: false, Obter);
            await AgregarLogsAsync("WeightLogs", "LoggedAt", "Peso", null, contaRefeicoes: false, Obter);
            await AgregarLogsAsync("WhatsAppChatHistory", "Timestamp", "Mensagem", new BsonDocument("Role", "user"), contaRefeicoes: false, Obter);

            // 2. Toda mensagem recebida (registrada pelo UserActivityTracker)
            var diasAtividade = await _database.GetCollection<BsonDocument>(UserActivityTracker.ColecaoAtividadeDias)
                .Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
            foreach (var d in diasAtividade)
            {
                if (!d.TryGetValue("UserId", out var uid) || !uid.IsString ||
                    !d.TryGetValue("Day", out var dia) || !dia.IsString ||
                    !d.TryGetValue("LastAt", out var lastAt) || !lastAt.IsBsonDateTime) continue;
                Obter(uid.AsString).Registrar(dia.AsString, lastAt.ToUniversalTime(), "Mensagem");
            }

            var perfisAtividade = (await _database.GetCollection<BsonDocument>(UserActivityTracker.ColecaoAtividade)
                    .Find(FilterDefinition<BsonDocument>.Empty).ToListAsync())
                .Where(x => x["_id"].IsString)
                .ToDictionary(x => x["_id"].AsString);

            // 3. Cadastros
            var trials = (await _database.GetCollection<BsonDocument>("B2C_Trial_Users")
                    .Find(FilterDefinition<BsonDocument>.Empty).ToListAsync())
                .Where(x => x["_id"].IsString)
                .ToDictionary(x => x["_id"].AsString);

            var pacientes = (await _database.GetCollection<BsonDocument>("Nutrition_Patients")
                    .Find(FilterDefinition<BsonDocument>.Empty).ToListAsync())
                .Where(x => x["_id"].IsString)
                .ToDictionary(x => x["_id"].AsString);

            using var cursorMetas = await _database.GetCollection<BsonDocument>("DailyGoals")
                .DistinctAsync<string>("UserId", FilterDefinition<BsonDocument>.Empty);
            var telefonesComMeta = await cursorMetas.ToListAsync();

            var ultimosEnvios = await ObterUltimosEnviosAsync();

            var telefones = new HashSet<string>(trials.Keys);
            telefones.UnionWith(pacientes.Keys);
            telefones.UnionWith(telefonesComMeta.Where(t => !string.IsNullOrWhiteSpace(t)));
            telefones.UnionWith(perfisAtividade.Keys);

            var linhas = new List<AdminUserRow>();

            foreach (var phone in telefones)
            {
                atividades.TryGetValue(phone, out var a);
                a ??= new Atividade();
                trials.TryGetValue(phone, out var trial);
                pacientes.TryGetValue(phone, out var paciente);
                perfisAtividade.TryGetValue(phone, out var perfil);
                ultimosEnvios.TryGetValue(phone, out var ultimoEnvio);

                // Plano
                string? professionalId = paciente != null && paciente.TryGetValue("ProfessionalId", out var prof) && prof.IsString ? prof.AsString : null;
                bool isB2b = professionalId != null && professionalId != "b2c_autonomous_user";
                bool premium = trial != null && trial.TryGetValue("Premium", out var p) && p.IsBoolean && p.AsBoolean;

                DateTime? inicioTrial = trial != null && trial.TryGetValue("TrialStartDate", out var ts) && ts.IsBsonDateTime ? ts.ToUniversalTime() : null;
                bool trialExpirado = inicioTrial.HasValue && agora >= inicioTrial.Value.AddDays(TrialDurationDays);

                string plano = isB2b ? "B2B"
                    : premium ? "Premium"
                    : trial != null ? (trialExpirado ? "Trial expirado" : "Trial")
                    : "Sem plano";

                int? diaDoTrial = plano == "Trial" && inicioTrial.HasValue
                    ? Math.Min(TrialDurationDays, (int)Math.Floor((agora - inicioTrial.Value).TotalDays) + 1)
                    : null;

                // Onboarding (só faz sentido para B2C)
                bool? onboardingConcluido = null;
                string? etapaOnboarding = null;
                if (trial != null)
                {
                    onboardingConcluido = trial.TryGetValue("ProfileConfigured", out var pc) && pc.IsBoolean && pc.AsBoolean;
                    if (onboardingConcluido == false)
                    {
                        var etapa = trial.TryGetValue("OnboardingStep", out var st) && st.IsString ? st.AsString : "WaitingForGoal";
                        etapaOnboarding = etapa switch
                        {
                            "WaitingForGoal" => "Objetivo (1/4)",
                            "WaitingForProfile" => "Dados físicos (2/4)",
                            "WaitingForRestrictions" => "Restrições (3/4)",
                            "WaitingForAversions" => "Aversões (4/4)",
                            _ => etapa
                        };
                    }
                }

                // Uso e frequência
                int totalRefeicoes = a.RefeicoesPorDia.Values.Sum();
                int diasComRefeicao = a.RefeicoesPorDia.Count(x => x.Value > 0);
                int refeicoes30 = a.RefeicoesPorDia.Where(x => string.CompareOrdinal(x.Key, corteFrequencia) >= 0).Sum(x => x.Value);
                int diasAtivos30 = a.Dias.Count(d => string.CompareOrdinal(d, corteFrequencia) >= 0);

                int? diasSemInteracao = a.Ultima.HasValue
                    ? Math.Max(0, (int)(hojeBr - a.Ultima.Value.AddHours(-3).Date).TotalDays)
                    : null;

                string status = diasSemInteracao == null ? "Sem interação"
                    : diasSemInteracao < DiasEmRisco ? "Ativo"
                    : diasSemInteracao < diasInativo ? "Em risco"
                    : "Inativo";

                string frequencia = diasAtivos30 switch
                {
                    0 => "Sem uso",
                    < 5 => "Baixa",
                    < 15 => "Média",
                    _ => "Alta"
                };

                // Segmentos para as campanhas (mutuamente exclusivos, nesta ordem de prioridade)
                bool isB2c = plano == "Trial" || plano == "Trial expirado";
                string? segmento = null;
                if (isB2c && totalRefeicoes == 0)
                    segmento = SegmentoNuncaUsou;
                else if (plano == "Trial expirado" && diasComRefeicao >= minDiasUsoIntenso)
                    segmento = SegmentoTrialAcabou;
                else if (totalRefeicoes > 0 && (diasSemInteracao ?? int.MaxValue) >= DiasEmRisco)
                    segmento = SegmentoUsouEParou;

                string? nome = paciente != null && paciente.TryGetValue("Name", out var n) && n.IsString && !string.IsNullOrWhiteSpace(n.AsString) ? n.AsString : null;

                linhas.Add(new AdminUserRow(
                    Phone: phone,
                    Name: nome,
                    Plano: plano,
                    NutritionistId: isB2b ? professionalId : null,
                    Status: status,
                    Segmento: segmento,
                    UltimaInteracao: a.Ultima,
                    UltimaInteracaoOrigem: a.OrigemUltima,
                    DiasSemInteracao: diasSemInteracao,
                    DiasAtivos30: diasAtivos30,
                    Refeicoes30: refeicoes30,
                    TotalRefeicoes: totalRefeicoes,
                    DiasComRefeicao: diasComRefeicao,
                    Frequencia: frequencia,
                    OnboardingConcluido: onboardingConcluido,
                    EtapaOnboarding: etapaOnboarding,
                    InicioTrial: inicioTrial,
                    DiaDoTrial: diaDoTrial,
                    FimTrial: inicioTrial?.AddDays(TrialDurationDays),
                    PrimeiroContato: perfil != null && perfil.TryGetValue("FirstSeenAt", out var fs) && fs.IsBsonDateTime ? fs.ToUniversalTime() : inicioTrial,
                    MensagensRecebidas: perfil != null && perfil.TryGetValue("InboundCount", out var ic) && ic.IsNumeric ? ic.ToInt32() : 0,
                    UltimoTemplate: ultimoEnvio?.Template,
                    UltimoEnvioEm: ultimoEnvio?.SentAt,
                    UltimoEnvioSucesso: ultimoEnvio?.Success,
                    UltimoEnvioStatus: ultimoEnvio?.DeliveryStatus,
                    UltimoEnvioErro: ultimoEnvio?.ErrorMessage));
            }

            return linhas
                .OrderByDescending(x => x.UltimaInteracao.HasValue)
                .ThenByDescending(x => x.UltimaInteracao)
                .ToList();
        }

        // Agrupa a coleção por usuário e por dia (horário de Brasília) no próprio MongoDB
        private async Task AgregarLogsAsync(
            string colecao, string campoData, string origem, BsonDocument? filtroExtra, bool contaRefeicoes, Func<string, Atividade> obter)
        {
            var match = new BsonDocument
            {
                { "UserId", new BsonDocument("$type", "string") },
                { campoData, new BsonDocument("$type", "date") }
            };
            if (filtroExtra != null) match.Merge(filtroExtra);

            BsonValue somaRefeicoes = contaRefeicoes
                ? new BsonDocument("$cond", new BsonArray
                {
                    new BsonDocument("$and", new BsonArray
                    {
                        new BsonDocument("$ne", new BsonArray { "$MealType", "Treino" }),
                        new BsonDocument("$gt", new BsonArray { "$Calories", 0 })
                    }),
                    1,
                    0
                })
                : 0;

            var group = new BsonDocument
            {
                { "_id", new BsonDocument
                    {
                        { "u", "$UserId" },
                        { "d", new BsonDocument("$dateToString", new BsonDocument
                            {
                                { "format", "%Y-%m-%d" },
                                { "date", "$" + campoData },
                                { "timezone", "America/Sao_Paulo" }
                            })
                        }
                    }
                },
                { "last", new BsonDocument("$max", "$" + campoData) },
                { "refeicoes", new BsonDocument("$sum", somaRefeicoes) }
            };

            var pipeline = PipelineDefinition<BsonDocument, BsonDocument>.Create(new[]
            {
                new BsonDocument("$match", match),
                new BsonDocument("$group", group)
            });

            var grupos = await _database.GetCollection<BsonDocument>(colecao).Aggregate(pipeline).ToListAsync();

            foreach (var g in grupos)
            {
                var id = g["_id"].AsBsonDocument;
                var phone = id["u"].AsString;
                var dia = id["d"].AsString;
                var atividade = obter(phone);

                atividade.Registrar(dia, g["last"].ToUniversalTime(), origem);

                if (contaRefeicoes)
                {
                    int qtd = g["refeicoes"].ToInt32();
                    atividade.RefeicoesPorDia[dia] = atividade.RefeicoesPorDia.TryGetValue(dia, out var atual) ? atual + qtd : qtd;
                }
            }
        }

        private sealed record UltimoEnvio(string? Template, DateTime? SentAt, bool? Success, string? DeliveryStatus, string? ErrorMessage);

        private async Task<Dictionary<string, UltimoEnvio>> ObterUltimosEnviosAsync()
        {
            var pipeline = PipelineDefinition<BsonDocument, BsonDocument>.Create(new[]
            {
                new BsonDocument("$sort", new BsonDocument("SentAt", -1)),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", "$UserId" },
                    { "template", new BsonDocument("$first", "$Template") },
                    { "sentAt", new BsonDocument("$first", "$SentAt") },
                    { "success", new BsonDocument("$first", "$Success") },
                    { "deliveryStatus", new BsonDocument("$first", "$DeliveryStatus") },
                    { "errorMessage", new BsonDocument("$first", "$ErrorMessage") }
                })
            });

            var docs = await _database.GetCollection<BsonDocument>(ColecaoEnvios).Aggregate(pipeline).ToListAsync();

            return docs
                .Where(d => d["_id"].IsString)
                .ToDictionary(
                    d => d["_id"].AsString,
                    d =>
                    {
                        bool? success = d["success"].IsBoolean ? d["success"].AsBoolean : null;
                        string? delivery = d.TryGetValue("deliveryStatus", out var ds) && ds.IsString ? ds.AsString
                            : success == true ? "accepted_legacy" : success == false ? TemplateDeliveryTracker.StatusFalhou : null;
                        return new UltimoEnvio(
                            d["template"].IsString ? d["template"].AsString : null,
                            d["sentAt"].IsBsonDateTime ? d["sentAt"].ToUniversalTime() : null,
                            success,
                            delivery,
                            d.TryGetValue("errorMessage", out var em) && em.IsString ? em.AsString : null);
                    });
        }

        #endregion

        // Variáveis aceitas nos parâmetros do template: {nome} {telefone} {dias_sem_uso} {dias_ativos} {refeicoes} {dia_trial} {plano}
        private static string PersonalizarParametro(string modelo, AdminUserRow u)
        {
            var texto = (modelo ?? "")
                .Replace("{nome}", u.Name ?? "Cliente")
                .Replace("{telefone}", u.Phone)
                .Replace("{dias_sem_uso}", (u.DiasSemInteracao ?? 0).ToString())
                .Replace("{dias_ativos}", u.DiasComRefeicao.ToString())
                .Replace("{refeicoes}", u.TotalRefeicoes.ToString())
                .Replace("{dia_trial}", (u.DiaDoTrial ?? TrialDurationDays).ToString())
                .Replace("{plano}", u.Plano);

            // A Meta recusa variáveis vazias, com quebra de linha/tab ou com mais de 4 espaços seguidos
            texto = Regex.Replace(texto, @"[\r\n\t]+", " ");
            texto = Regex.Replace(texto, @" {4,}", "   ").Trim();
            return string.IsNullOrEmpty(texto) ? "-" : texto;
        }
    }

    public record AdminUserRow(
        string Phone,
        string? Name,
        string Plano,
        string? NutritionistId,
        string Status,
        string? Segmento,
        DateTime? UltimaInteracao,
        string? UltimaInteracaoOrigem,
        int? DiasSemInteracao,
        int DiasAtivos30,
        int Refeicoes30,
        int TotalRefeicoes,
        int DiasComRefeicao,
        string Frequencia,
        bool? OnboardingConcluido,
        string? EtapaOnboarding,
        DateTime? InicioTrial,
        int? DiaDoTrial,
        DateTime? FimTrial,
        DateTime? PrimeiroContato,
        int MensagensRecebidas,
        string? UltimoTemplate,
        DateTime? UltimoEnvioEm,
        bool? UltimoEnvioSucesso,
        string? UltimoEnvioStatus,
        string? UltimoEnvioErro);

    public record AdminSendTemplateRequest(
        List<string>? Phones,
        string? TemplateName,
        List<string>? Parameters,
        string? Segment,
        int SkipIfSentWithinDays = 0);
}
