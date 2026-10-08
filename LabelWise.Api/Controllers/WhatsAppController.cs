using LabelWise.Application.DTOs;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Interfaces.AI;
using LabelWise.Application.Interfaces.Persistence;
using LabelWise.Domain.Entities.Nutrition;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LabelWise.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class WhatsAppController : ControllerBase
    {
        private readonly INutritionService _nutritionService;
        private readonly IWhatsAppSenderService _whatsAppSender;
        private readonly IMetaMediaService _metaMediaService;
        private readonly INutritionRepository _nutritionRepository;
        private readonly INutritionAgentService _nutritionAgentService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WhatsAppController> _logger;
        private readonly IMongoDatabase _database;
        private readonly string _verifyToken;

        public WhatsAppController(
            INutritionService nutritionService,
            IWhatsAppSenderService whatsAppSender,
            IMetaMediaService metaMediaService,
            INutritionRepository nutritionRepository,
            INutritionAgentService nutritionAgentService,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<WhatsAppController> logger,
            IMongoDatabase database)
        {
            _nutritionService = nutritionService;
            _whatsAppSender = whatsAppSender;
            _metaMediaService = metaMediaService;
            _nutritionRepository = nutritionRepository;
            _nutritionAgentService = nutritionAgentService;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _database = database;
            _verifyToken = configuration["MetaWhatsApp:VerifyToken"] ?? "labelwise_verify_token_123";
        }

        [HttpGet("webhook")]
        public IActionResult VerifyWebhook(
            [FromQuery(Name = "hub.mode")] string mode,
            [FromQuery(Name = "hub.verify_token")] string token,
            [FromQuery(Name = "hub.challenge")] string challenge)
        {
            if (mode == "subscribe" && token == _verifyToken)
            {
                return Ok(challenge);
            }
            return Forbid();
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> ReceiveMessage([FromBody] MetaWebhookPayload payload)
        {
            string? senderPhone = null;

            try
            {
                var messagingEvent = payload?.Entry?.FirstOrDefault()
                    ?.Changes?.FirstOrDefault()
                    ?.Value?.Messages?.FirstOrDefault();

                senderPhone = messagingEvent?.From;
                var messageType = messagingEvent?.Type;

                if (string.IsNullOrWhiteSpace(senderPhone) || string.IsNullOrWhiteSpace(messageType))
                {
                    return Ok();
                }

                string? textoBruto = null;
                string? imagemBase64 = null;

                if (messageType == "text")
                {
                    textoBruto = messagingEvent?.Text?.Body?.Trim();
                }
                else if (messageType == "audio" && messagingEvent?.Audio?.Id != null)
                {
                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "🎙 A ouvir o seu áudio e a transcrever...");
                    var audioBytes = await _metaMediaService.DownloadMediaAsBytesAsync(messagingEvent.Audio.Id);
                    textoBruto = await TranscreverAudioComGeminiAsync(audioBytes);
                }
                else if (messageType == "image" && messagingEvent?.Image?.Id != null)
                {
                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "📸 A analisar o seu prato, só um instante...");
                    imagemBase64 = await _metaMediaService.DownloadMediaAsBase64Async(messagingEvent.Image.Id);
                    textoBruto = "Analise esta refeição da imagem.";
                }
                else
                {
                    return Ok();
                }

                if (string.IsNullOrWhiteSpace(textoBruto) && string.IsNullOrWhiteSpace(imagemBase64))
                {
                    return Ok();
                }

                string textoLimpoCmd = Regex.Replace(textoBruto, "[^a-zA-Z0-9áéíóúâêîôûãõçÁÉÍÓÚÂÊÎÔÛÃÕÇ ]", "").Trim().ToLowerInvariant();

                var patientsCollectionCheck = _database.GetCollection<MongoDB.Bson.BsonDocument>("Nutrition_Patients");
                var patientDocCheck = await patientsCollectionCheck.Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", senderPhone)).FirstOrDefaultAsync();

                bool isB2bPatient = patientDocCheck != null &&
                                    patientDocCheck.Contains("ProfessionalId") &&
                                    patientDocCheck["ProfessionalId"].AsString != "b2c_autonomous_user";

                string? protocoloClinico = null;
                if (patientDocCheck != null && patientDocCheck.Contains("ClinicalProtocol"))
                {
                    protocoloClinico = patientDocCheck["ClinicalProtocol"].AsString;
                }

                string? foodAversions = null;
                if (patientDocCheck != null && patientDocCheck.Contains("FoodAversions"))
                {
                    foodAversions = patientDocCheck["FoodAversions"].AsString;
                }

                var trialCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("B2C_Trial_Users");
                var filterTrial = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", senderPhone);
                var userDoc = await trialCollection.Find(filterTrial).FirstOrDefaultAsync();

                if (!isB2bPatient)
                {
                    var now = DateTime.UtcNow;

                    if (userDoc == null)
                    {
                        userDoc = new MongoDB.Bson.BsonDocument
                        {
                            { "_id", senderPhone },
                            { "TrialStartDate", now },
                            { "LastInteractionDate", now.Date },
                            { "DailyMessageCount", 0 },
                            { "ProfileConfigured", false },
                            { "OnboardingStep", "WaitingForGoal" }
                        };
                        await trialCollection.InsertOneAsync(userDoc);

                        string mensagemBoasVindas = "🎉 *Bem-vindo ao Nutrição Facil (Versão de Teste - 15 dias)!* 🥗\n\n" +
                                                    "Vamos configurar o seu perfil com um formulário rápido e guiado passo a passo.\n\n" +
                                                    "🎯 **Passo 1 de 4:** Qual é o seu principal objetivo?\n" +
                                                    "*1️⃣* Emagrecimento\n" +
                                                    "*2️⃣* Hipertrofia\n" +
                                                    "*3️⃣* Manutenção\n\n" +
                                                    "_(Responda apenas com o número correspondente)_";

                        await _whatsAppSender.SendTextMessageAsync(senderPhone, mensagemBoasVindas);
                        return Ok();
                    }

                    if (textoLimpoCmd == "meta" || textoLimpoCmd == "perfil" || textoLimpoCmd == "configurar" || textoLimpoCmd == "ajustar")
                    {
                        var resetConfig = Builders<MongoDB.Bson.BsonDocument>.Update
                            .Set("ProfileConfigured", false)
                            .Set("OnboardingStep", "WaitingForGoal");
                        await trialCollection.UpdateOneAsync(filterTrial, resetConfig);

                        string mensagemReset = "⚙️ *Reconfiguração de Perfil Iniciada!*\n\n" +
                                               "🎯 **Passo 1 de 4:** Qual é o seu principal objetivo?\n" +
                                               "*1️⃣* Emagrecimento\n" +
                                               "*2️⃣* Hipertrofia\n" +
                                               "*3️⃣* Manutenção\n\n" +
                                               "_(Responda apenas com o número correspondente)_";
                        await _whatsAppSender.SendTextMessageAsync(senderPhone, mensagemReset);
                        return Ok();
                    }

                    bool profileConfigured = userDoc.Contains("ProfileConfigured") && userDoc["ProfileConfigured"].AsBoolean;

                    if (!profileConfigured)
                    {
                        string step = userDoc.Contains("OnboardingStep") ? userDoc["OnboardingStep"].AsString : "WaitingForGoal";

                        switch (step)
                        {
                            case "WaitingForGoal":
                                var objetivosValidos = new Dictionary<int, string>
                                {
                                    { 1, "Emagrecimento" },
                                    { 2, "Hipertrofia" },
                                    { 3, "Manutenção" }
                                };

                                if (int.TryParse(textoLimpoCmd, out int optGoal) && objetivosValidos.ContainsKey(optGoal))
                                {
                                    string goalSelecionado = objetivosValidos[optGoal];
                                    var updateGoal = Builders<MongoDB.Bson.BsonDocument>.Update
                                        .Set("TempGoal", goalSelecionado)
                                        .Set("OnboardingStep", "WaitingForCalories");
                                    await trialCollection.UpdateOneAsync(filterTrial, updateGoal);

                                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "🔥 **Passo 2 de 4:** Qual é a sua meta de calorias diárias?\n_(Digite apenas um número inteiro válido, ex: 1800)_");
                                }
                                else
                                {
                                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "❌ Opção inválida.\n\n🎯 **Passo 1 de 4:** Escolha o número correspondente ao seu principal objetivo:\n*1️⃣* Emagrecimento\n*2️⃣* Hipertrofia\n*3️⃣* Manutenção");
                                }
                                return Ok();

                            case "WaitingForCalories":
                                var matchCal = Regex.Match(textoBruto, @"\d+");
                                if (matchCal.Success && int.TryParse(matchCal.Value, out int parsedCal) && parsedCal >= 800 && parsedCal <= 5000)
                                {
                                    var updateCal = Builders<MongoDB.Bson.BsonDocument>.Update
                                        .Set("TempCalories", parsedCal)
                                        .Set("OnboardingStep", "WaitingForRestrictions");
                                    await trialCollection.UpdateOneAsync(filterTrial, updateCal);

                                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "🛡️ **Passo 3 de 4:** Tem alguma alergia ou restrição médica?\n_(Ex: Alergia a amendoim, intolerância à lactose, ou digite 'Nenhuma')_");
                                }
                                else
                                {
                                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "❌ Valor de calorias inválido. Por favor, digite apenas um número numérico válido entre 800 e 5000 (ex: 1800):");
                                }
                                return Ok();

                            case "WaitingForRestrictions":
                                var restrictions = textoBruto.Equals("nenhuma", StringComparison.OrdinalIgnoreCase) ? "" : textoBruto;

                                var updateRest = Builders<MongoDB.Bson.BsonDocument>.Update
                                    .Set("TempRestrictions", restrictions)
                                    .Set("OnboardingStep", "WaitingForAversions");
                                await trialCollection.UpdateOneAsync(filterTrial, updateRest);

                                await _whatsAppSender.SendTextMessageAsync(senderPhone, "🚫 **Passo 4 de 4:** Tem alguma aversão ou comida que não gosta?\n_(Ex: Ovo, cebola, ou digite 'Nenhuma')_");
                                return Ok();

                            case "WaitingForAversions":
                                var aversions = textoBruto.Equals("nenhuma", StringComparison.OrdinalIgnoreCase) ? "" : textoBruto;

                                string goal = userDoc.Contains("TempGoal") ? userDoc["TempGoal"].AsString : "Emagrecimento";
                                int calories = userDoc.Contains("TempCalories") ? userDoc["TempCalories"].AsInt32 : 2000;
                                string medRest = userDoc.Contains("TempRestrictions") ? userDoc["TempRestrictions"].AsString : "";

                                var patientsCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("Nutrition_Patients");
                                var patientDoc = new MongoDB.Bson.BsonDocument
                                {
                                    { "_id", senderPhone },
                                    { "ProfessionalId", "b2c_autonomous_user" },
                                    { "MainGoal", goal },
                                    { "MedicalRestrictions", medRest },
                                    { "FoodAversions", aversions }
                                };
                                await patientsCollection.ReplaceOneAsync(
                                    Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", senderPhone),
                                    patientDoc,
                                    new ReplaceOptions { IsUpsert = true }
                                );

                                var goalsCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("DailyGoals");
                                var goalFilter = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("UserId", senderPhone);
                                var existingGoal = await goalsCollection.Find(goalFilter).FirstOrDefaultAsync();

                                string goalId = existingGoal != null && existingGoal.Contains("_id")
                                    ? existingGoal["_id"].AsString
                                    : Guid.NewGuid().ToString();

                                var goalDoc = new MongoDB.Bson.BsonDocument
                                {
                                    { "_id", goalId },
                                    { "UserId", senderPhone },
                                    { "NutritionistId", "b2c_autonomous_user" },
                                    { "TargetDate", now.Date },
                                    { "TargetCalories", calories },
                                    { "TargetProteinG", 150 },
                                    { "TargetCarbsG", 200 },
                                    { "TargetFatG", 60 },
                                    { "DietaryRestrictions", medRest },
                                    { "FavoriteFoods", aversions },
                                    { "PrescribedMealPlan", $"Objetivo: {goal}" }
                                };
                                await goalsCollection.ReplaceOneAsync(goalFilter, goalDoc, new ReplaceOptions { IsUpsert = true });

                                var updateDone = Builders<MongoDB.Bson.BsonDocument>.Update
                                    .Set("ProfileConfigured", true);
                                await trialCollection.UpdateOneAsync(filterTrial, updateDone);

                                string respostaConfig = $"✅ *Formulário concluído com sucesso!* 🥗\n\n" +
                                                        $"🎯 **Objetivo:** {goal}\n" +
                                                        $"🔥 **Calorias Diárias:** {calories} kcal\n" +
                                                        $"🛡 **Alergias/Restrições:** {(string.IsNullOrWhiteSpace(medRest) ? "Nenhuma" : medRest)}\n" +
                                                        $"🚫 **Aversões:** {(string.IsNullOrWhiteSpace(aversions) ? "Nenhuma" : aversions)}\n\n" +
                                                        $"👉 *Tudo pronto! Já pode enviar as suas refeições* por texto, foto ou áudio.\n\n" +
                                                        $"_💡 Dica: Se quiser alterar suas metas no futuro, basta digitar *meta* a qualquer momento!_";

                                await _whatsAppSender.SendTextMessageAsync(senderPhone, respostaConfig);
                                return Ok();
                        }
                    }

                    var trialStartDate = userDoc["TrialStartDate"].ToUniversalTime();
                    if ((now - trialStartDate).TotalDays > 15)
                    {
                        _logger.LogWarning("[WhatsApp B2C] ⏳ Trial expirado para o número: {Phone}", senderPhone);
                        await _whatsAppSender.SendTextMessageAsync(
                            senderPhone,
                            "⏳ O seu período experimental gratuito de 15 dias terminou. Para continuar a usar o assistente, por favor faça a subscrição do plano completo. 🥗"
                        );
                        return Ok();
                    }

                    var lastInteractionDate = userDoc.Contains("LastInteractionDate") ? userDoc["LastInteractionDate"].ToUniversalTime().Date : now.Date;
                    int dailyCount = userDoc.Contains("DailyMessageCount") ? userDoc["DailyMessageCount"].AsInt32 : 0;

                    if (lastInteractionDate < now.Date)
                    {
                        dailyCount = 0;
                        lastInteractionDate = now.Date;
                    }

                    if (dailyCount >= 4)
                    {
                        _logger.LogWarning("[WhatsApp B2C] ⚠️ Limite diário de mensagens atingido para: {Phone}", senderPhone);
                        await _whatsAppSender.SendTextMessageAsync(
                            senderPhone,
                            "⚠ Atingiu o limite de 4 interações gratuitas para hoje. O seu saldo diário será renovado amanhã! ⏰"
                        );
                        return Ok();
                    }

                    dailyCount++;
                    var updateB2C = Builders<MongoDB.Bson.BsonDocument>.Update
                        .Set("LastInteractionDate", lastInteractionDate)
                        .Set("DailyMessageCount", dailyCount);

                    await trialCollection.UpdateOneAsync(filterTrial, updateB2C);
                }

                bool isNumeroIsolado = int.TryParse(textoLimpoCmd, out int numeroIsoladoVal);

                // 🔄 FASE 5: ATALHO DE SUBSTITUIÇÃO INTELIGENTE NO WHATSAPP
                if (textoLimpoCmd.StartsWith("substituir ") || textoLimpoCmd.StartsWith("trocar "))
                {
                    var alimentoAlvo = textoBruto.Replace("substituir", "", StringComparison.OrdinalIgnoreCase)
                                                   .Replace("trocar", "", StringComparison.OrdinalIgnoreCase)
                                                   .Replace("por", "", StringComparison.OrdinalIgnoreCase).Trim();

                    if (!string.IsNullOrWhiteSpace(alimentoAlvo))
                    {
                        await _whatsAppSender.SendTextMessageAsync(senderPhone, $"🔄 A procurar as melhores alternativas para *{alimentoAlvo}* respeitando o seu plano...");

                        var dataHojeBr = DateTime.UtcNow.AddHours(-3);
                        var logs = await _nutritionRepository.ObterRefeicoesDoDiaAsync(senderPhone, dataHojeBr);
                        var goal = await _nutritionRepository.ObterMetaDiariaAsync(senderPhone, dataHojeBr.Date);

                        int targetCal = goal?.TargetCalories ?? 2000;
                        int consumedCal = logs.Sum(x => x.Calories);
                        decimal targetProt = goal?.TargetProteinG ?? 150m;
                        decimal consumedProt = logs.Sum(x => x.ProteinG);
                        decimal targetCarbs = goal?.TargetCarbsG ?? 200m;
                        decimal consumedCarbs = logs.Sum(x => x.CarbsG);
                        decimal targetFat = goal?.TargetFatG ?? 60m;
                        decimal consumedFat = logs.Sum(x => x.FatG);

                        var remaining = new MacroSummaryDto(
                            Math.Max(0, targetCal - consumedCal),
                            Math.Max(0, targetProt - consumedProt),
                            Math.Max(0, targetCarbs - consumedCarbs),
                            Math.Max(0, targetFat - consumedFat)
                        );

                        string respostaTroca = await _nutritionAgentService.GenerateSmartSubstitutionAsync(
                            alimentoAlvo, remaining, protocoloClinico ?? "", foodAversions ?? ""
                        );

                        await _whatsAppSender.SendTextMessageAsync(senderPhone, respostaTroca);
                        await _nutritionRepository.SalvarMensagemHistoricoAsync(senderPhone, "assistant", respostaTroca);
                        return Ok();
                    }
                }

                if (textoLimpoCmd == "remover" || textoLimpoCmd == "apagar" || textoLimpoCmd == "excluir" || textoLimpoCmd == "listar refeicoes" || textoLimpoCmd == "remover refeicao")
                {
                    var dataHojeBr = DateTime.UtcNow.AddHours(-3);
                    var refeicoesHoje = await _nutritionRepository.ObterRefeicoesDoDiaAsync(senderPhone, dataHojeBr);

                    if (refeicoesHoje == null || !refeicoesHoje.Any())
                    {
                        await _whatsAppSender.SendTextMessageAsync(senderPhone, "⚠️ Não encontrei nenhuma refeição registada para hoje.");
                        return Ok();
                    }

                    var sbLista = new StringBuilder("🗑️ *As suas refeições registadas hoje:*\n\n");
                    int index = 1;
                    foreach (var r in refeicoesHoje.OrderBy(x => x.LoggedAt))
                    {
                        var horaStr = r.LoggedAt.AddHours(-3).ToString("HH:mm");
                        sbLista.AppendLine($"*{index}️⃣* {r.DishName ?? r.MealType} ({horaStr})");
                        index++;
                    }
                    sbLista.AppendLine("\n💡 *Para remover*, diga ou digite o número correspondente (Ex: *1*, *2* ou *remover 1*).");

                    await _whatsAppSender.SendTextMessageAsync(senderPhone, sbLista.ToString());
                    return Ok();
                }

                if (textoLimpoCmd.StartsWith("remover ") || textoLimpoCmd.StartsWith("apagar ") || textoLimpoCmd.StartsWith("excluir ") || isNumeroIsolado)
                {
                    var dataHojeBr = DateTime.UtcNow.AddHours(-3);
                    var refeicoesHoje = await _nutritionRepository.ObterRefeicoesDoDiaAsync(senderPhone, dataHojeBr);

                    if (refeicoesHoje == null || !refeicoesHoje.Any())
                    {
                        await _whatsAppSender.SendTextMessageAsync(senderPhone, "⚠️ Não tem nenhuma refeição registada hoje para remover.");
                        return Ok();
                    }

                    var refeicoesOrdenadas = refeicoesHoje.OrderBy(x => x.LoggedAt).ToList();
                    MealLog? refeicaoParaExcluir = null;

                    if (isNumeroIsolado)
                    {
                        int idx = numeroIsoladoVal - 1;
                        if (idx >= 0 && idx < refeicoesOrdenadas.Count)
                        {
                            refeicaoParaExcluir = refeicoesOrdenadas[idx];
                        }
                    }
                    else
                    {
                        var partes = textoBruto.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                        if (partes.Length > 1)
                        {
                            var argumento = partes[1].Trim();
                            if (int.TryParse(argumento, out int numeroRefeicao))
                            {
                                int idx = numeroRefeicao - 1;
                                if (idx >= 0 && idx < refeicoesOrdenadas.Count)
                                {
                                    refeicaoParaExcluir = refeicoesOrdenadas[idx];
                                }
                            }
                            else
                            {
                                refeicaoParaExcluir = refeicoesOrdenadas.FirstOrDefault(r =>
                                    (r.DishName ?? "").Contains(argumento, StringComparison.OrdinalIgnoreCase) ||
                                    (r.MealType ?? "").Contains(argumento, StringComparison.OrdinalIgnoreCase));
                            }
                        }
                    }

                    if (refeicaoParaExcluir != null)
                    {
                        await _nutritionRepository.ExcluirMealLogAsync(refeicaoParaExcluir.Id);
                        var statusAtualizado = await _nutritionService.GetDailyStatusAndSuggestionAsync(senderPhone, dataHojeBr);

                        string respostaRemocao = $"✅ *Refeição removida com sucesso!* \n" +
                                                 $"🗑️ _{refeicaoParaExcluir.DishName ?? refeicaoParaExcluir.MealType}_\n\n" +
                                                 $"📊 *O SEU NOVO RESUMO DE HOJE*\n" +
                                                 $"• *Calorias:* {statusAtualizado.Consumed.Calories} / {statusAtualizado.Target.Calories} kcal\n" +
                                                 $"_Faltam {statusAtualizado.Remaining.Calories} kcal_";

                        await _whatsAppSender.SendTextMessageAsync(senderPhone, respostaRemocao);
                        return Ok();
                    }
                    else
                    {
                        await _whatsAppSender.SendTextMessageAsync(senderPhone, "❌ Número inválido. Diga *remover* para ver a lista atualizada das suas refeições.");
                        return Ok();
                    }
                }

                if (textoLimpoCmd == "minha dieta" || textoLimpoCmd == "cardapio" || textoLimpoCmd == "menu" || textoLimpoCmd == "dieta")
                {
                    var baseUrl = $"{Request.Scheme}://{Request.Host}";
                    var magicLink = $"{baseUrl}/paciente.html?phone={senderPhone}";

                    var respostaLink = "🥗 *O seu Plano Alimentar Interativo está pronto!*\n\n" +
                                       "Toque no link abaixo para ver o seu progresso de calorias de hoje, os macronutrientes e o seu cardápio completo:\n\n" +
                                       $"👉 {magicLink}\n\n" +
                                       "_Dica: Guarde esta página nos favoritos do seu telemóvel para consultar sempre que precisar!_ ✨";

                    await _whatsAppSender.SendTextMessageAsync(senderPhone, respostaLink);
                    return Ok();
                }

                var saudacoesOuConversa = new[] { "oi", "ola", "olá", "tudo bem", "bom dia", "boa tarde", "boa noite", "eae", "hey", "obrigado", "obrigada", "valeu" };
                if (saudacoesOuConversa.Contains(textoLimpoCmd))
                {
                    string respostaSaudacao = "👋 Olá! Sou o seu assistente nutricional do Nutrição Facil.\n\n" +
                                              "Pode enviar-me uma **foto**, um **áudio** ou o **texto** do que comeu para eu registar a refeição. 🥗\n\n" +
                                              "💡 *Dica:* Diga *remover* para ver a lista de refeições de hoje ou *minha dieta* para ver o seu plano completo!";

                    await _whatsAppSender.SendTextMessageAsync(senderPhone, respostaSaudacao);
                    await _nutritionRepository.SalvarMensagemHistoricoAsync(senderPhone, "assistant", respostaSaudacao);
                    return Ok();
                }

                // 🛡️ DETEÇÃO ROBUSTA DE ÁGUA (Suporta ml, litros, l, copos e garrafas)
                bool mencionaAgua = textoBruto.Contains("agua", StringComparison.OrdinalIgnoreCase) ||
                                    textoBruto.Contains("água", StringComparison.OrdinalIgnoreCase) ||
                                    textoBruto.Contains("h2o", StringComparison.OrdinalIgnoreCase);

                if (mencionaAgua)
                {
                    int quantidadeMl = 250; // Valor padrão se disser apenas "copo de água" ou "água"

                    var matchMl = Regex.Match(textoBruto, @"(\d+)\s*(ml|litros|l)\b", RegexOptions.IgnoreCase);
                    if (matchMl.Success)
                    {
                        quantidadeMl = int.Parse(matchMl.Groups[1].Value);
                        if (matchMl.Groups[2].Value.Equals("l", StringComparison.OrdinalIgnoreCase) || matchMl.Groups[2].Value.Equals("litros", StringComparison.OrdinalIgnoreCase))
                        {
                            quantidadeMl *= 1000;
                        }
                    }
                    else
                    {
                        var matchCopo = Regex.Match(textoBruto, @"(\d+)?\s*(copo|copos|garrafa|garrafas)\b", RegexOptions.IgnoreCase);
                        if (matchCopo.Success)
                        {
                            int qtd = 1;
                            if (matchCopo.Groups[1].Success && int.TryParse(matchCopo.Groups[1].Value, out int parsedQtd))
                            {
                                qtd = parsedQtd;
                            }
                            quantidadeMl = qtd * 250; // Considera 250 ml por copo/garrafa padrão
                        }
                    }

                    var dataHojeBr = DateTime.UtcNow.AddHours(-3).Date;
                    var waterCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("WaterLogs");

                    var waterDoc = new MongoDB.Bson.BsonDocument
                    {
                        { "_id", Guid.NewGuid().ToString() },
                        { "UserId", senderPhone },
                        { "AmountMl", quantidadeMl },
                        { "LoggedAt", DateTime.UtcNow }
                    };
                    await waterCol.InsertOneAsync(waterDoc);

                    var filterWater = Builders<MongoDB.Bson.BsonDocument>.Filter.And(
                        Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("UserId", senderPhone),
                        Builders<MongoDB.Bson.BsonDocument>.Filter.Gte("LoggedAt", dataHojeBr)
                    );
                    var registrosAgua = await waterCol.Find(filterWater).ToListAsync();
                    int totalAguaHoje = registrosAgua.Sum(w => w["AmountMl"].AsInt32);
                    int metaAguaDiaria = 2500;

                    string respostaAgua = $"💧 {totalAguaHoje} / {metaAguaDiaria} ml de água registados com sucesso! 🚰";
                    await _whatsAppSender.SendTextMessageAsync(senderPhone, respostaAgua);
                    await _nutritionRepository.SalvarMensagemHistoricoAsync(senderPhone, "assistant", respostaAgua);
                    return Ok();
                }

                var matchPeso = Regex.Match(textoBruto, @"(?:peso|peso hoje|estou com|estou pesando)\D*(\d+[\.,]?\d*)\s*kg", RegexOptions.IgnoreCase);
                if (matchPeso.Success || (textoBruto.Contains("kg", StringComparison.OrdinalIgnoreCase) && Regex.IsMatch(textoBruto, @"\d+[\.,]?\d*")))
                {
                    string valorPesoStr = matchPeso.Success ? matchPeso.Groups[1].Value : Regex.Match(textoBruto, @"\d+[\.,]?\d*").Value;
                    if (decimal.TryParse(valorPesoStr.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal pesoInformado))
                    {
                        var dataHojeBr = DateTime.UtcNow.AddHours(-3).Date;
                        var weightCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("WeightLogs");

                        var weightDoc = new MongoDB.Bson.BsonDocument
                        {
                            { "_id", Guid.NewGuid().ToString() },
                            { "UserId", senderPhone },
                            { "WeightKg", (double)pesoInformado },
                            { "LoggedAt", DateTime.UtcNow }
                        };
                        await weightCol.ReplaceOneAsync(
                            Builders<MongoDB.Bson.BsonDocument>.Filter.And(
                                Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("UserId", senderPhone),
                                Builders<MongoDB.Bson.BsonDocument>.Filter.Gte("LoggedAt", dataHojeBr)
                            ),
                            weightDoc,
                            new ReplaceOptions { IsUpsert = true }
                        );

                        string respostaPeso = $"⚖️ Peso de *{pesoInformado:F1} kg* registado com sucesso! Continue firme no foco da evolução! 🎯";
                        await _whatsAppSender.SendTextMessageAsync(senderPhone, respostaPeso);
                        await _nutritionRepository.SalvarMensagemHistoricoAsync(senderPhone, "assistant", respostaPeso);
                        return Ok();
                    }
                }

                var contextoPendente = await _nutritionRepository.ObterClarificacaoPendenteAsync(senderPhone);
                string textoFinalParaIa = textoBruto;
                string? imagemFinalParaIa = imagemBase64;

                if (contextoPendente != null)
                {
                    _logger.LogInformation("[WhatsAppController] 🔄 Resposta de clarificação detetada para o utilizador {Phone}", senderPhone);

                    textoFinalParaIa = $"[Contexto Anterior da Refeição: {contextoPendente.OriginalTextInput}] " +
                                       $"[Alerta de Restrição Enviado: {contextoPendente.ClarificationQuestion}] " +
                                       $"[Confirmação / Resposta do Utilizador: {textoBruto}] " +
                                       $"Instrução: Processe e registe definitivamente esta refeição considerando a resposta do utilizador.";

                    imagemFinalParaIa ??= contextoPendente.OriginalBase64Image;
                    await _nutritionRepository.RemoverClarificacaoPendenteAsync(senderPhone);
                }

                await _nutritionRepository.SalvarMensagemHistoricoAsync(senderPhone, "user", textoFinalParaIa);

                var request = new ParseMealRequestDto(
                    senderPhone,
                    TextInput: textoFinalParaIa,
                    Base64Image: imagemFinalParaIa,
                    AudioUrl: null,
                    LocalTime: DateTime.UtcNow,
                    ClinicalProtocol: protocoloClinico
                );

                var result = await _nutritionService.ProcessMealEntryAsync(request);
                DailyStatusResponseDto? statusDoDia = null;

                bool isSystemError = result.ClarificationQuestion != null &&
                                     result.ClarificationQuestion.Contains("serviços de IA estão instáveis", StringComparison.OrdinalIgnoreCase);

                if (result.RequiresUserClarification && !isSystemError)
                {
                    var novaClarificacao = new MealClarificationContext(
                        userId: senderPhone,
                        originalTextInput: textoFinalParaIa,
                        originalBase64Image: imagemFinalParaIa,
                        clarificationQuestion: result.ClarificationQuestion ?? "Pode detalhar melhor a sua refeição?"
                    );

                    await _nutritionRepository.SalvarClarificacaoPendenteAsync(novaClarificacao);
                }
                else if (result.TotalMeal != null && !isSystemError && !result.IsAdvice)
                {
                    var dataHojeBr = DateTime.UtcNow.AddHours(-3);
                    statusDoDia = await _nutritionService.GetDailyStatusAndSuggestionAsync(senderPhone, dataHojeBr);
                }

                var respostaTexto = FormatarRespostaParaWhatsApp(result, statusDoDia);
                await _whatsAppSender.SendTextMessageAsync(senderPhone, respostaTexto);

                await _nutritionRepository.SalvarMensagemHistoricoAsync(senderPhone, "assistant", respostaTexto);

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro crítico no fluxo do Webhook do WhatsApp para {Phone}", senderPhone);

                if (!string.IsNullOrWhiteSpace(senderPhone))
                {
                    try
                    {
                        string respostaErro = "*Ops! Ocorreu uma instabilidade temporária.* 😔\nTente novamente em instantes!";
                        await _whatsAppSender.SendTextMessageAsync(senderPhone, respostaErro);
                    }
                    catch { }
                }

                return Ok();
            }
        }

        [HttpPost("send-daily-reminders")]
        public async Task<IActionResult> SendDailyReminders([FromHeader(Name = "X-Cron-Secret")] string secret)
        {
            var expectedSecret = _configuration["CronSecret"];
            if (string.IsNullOrEmpty(secret) || secret != expectedSecret)
            {
                return Unauthorized(new { success = false, message = "Acesso negado." });
            }

            try
            {
                var horaBrasilia = DateTime.UtcNow.AddHours(-3).Hour;
                string mealTime = "café da manhã";
                if (horaBrasilia >= 11 && horaBrasilia < 16) mealTime = "almoço";
                else if (horaBrasilia >= 16 && horaBrasilia < 24) mealTime = "jantar";

                var telefones = await _nutritionRepository.ObterTelefonesAtivosAsync();
                int enviados = 0, falhas = 0;

                foreach (var telefone in telefones)
                {
                    try
                    {
                        bool sucesso = await _whatsAppSender.SendTemplateReminderAsync(telefone, "Paciente", mealTime);
                        if (sucesso) enviados++; else falhas++;
                    }
                    catch { falhas++; }
                }
                return Ok(new { success = true, message = $"Rotina executada. Refeição: {mealTime}. Enviados: {enviados}, Falhas: {falhas}" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, message = "Erro interno ao processar os lembretes." });
            }
        }

        [HttpPost("send-end-of-day-feedback")]
        public async Task<IActionResult> SendEndOfDayFeedback([FromHeader(Name = "X-Cron-Secret")] string secret)
        {
            var expectedSecret = _configuration["CronSecret"];
            if (string.IsNullOrEmpty(secret) || secret != expectedSecret)
            {
                return Unauthorized(new { success = false, message = "Acesso negado." });
            }

            try
            {
                var dataHojeBr = DateTime.UtcNow.AddHours(-3).Date;
                var telefones = await _nutritionRepository.ObterTelefonesAtivosAsync();
                int enviados = 0, falhas = 0;

                foreach (var phone in telefones)
                {
                    try
                    {
                        var goal = await _nutritionRepository.ObterMetaDiariaAsync(phone, dataHojeBr);
                        var logs = await _nutritionRepository.ObterRefeicoesDoDiaAsync(phone, dataHojeBr);

                        int targetCal = goal?.TargetCalories ?? 2000;
                        decimal targetProt = goal?.TargetProteinG ?? 150;

                        int consumedCal = logs.Sum(x => x.Calories);
                        decimal consumedProt = logs.Sum(x => x.ProteinG);

                        var pratos = logs.Select(x => x.DishName).Where(d => !string.IsNullOrWhiteSpace(d)).ToList();

                        string feedbackMsg = await GerarFeedbackDiarioComGeminiAsync(
                            goal?.PrescribedMealPlan ?? "Manter a saúde",
                            targetCal, consumedCal, targetProt, consumedProt, pratos
                        );

                        string mensagemFinal = $"🌙 *Resumo do seu dia*\n\n{feedbackMsg}";

                        await _whatsAppSender.SendTextMessageAsync(phone, mensagemFinal);
                        enviados++;
                    }
                    catch
                    {
                        falhas++;
                    }
                }

                return Ok(new { success = true, message = $"Feedback diário concluído. Enviados: {enviados}, Falhas: {falhas}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no envio de feedback diário.");
                return StatusCode(500, new { success = false, message = "Erro ao processar feedbacks diários." });
            }
        }

        [HttpPost("send-reminder")]
        public async Task<IActionResult> SendReminder([FromQuery] string phone, [FromQuery] string userName = "Anderson", [FromQuery] string mealTime = "café da manhã")
        {
            if (string.IsNullOrWhiteSpace(phone)) return BadRequest(new { success = false, message = "Telefone obrigatório." });
            bool enviado = await _whatsAppSender.SendTemplateReminderAsync(phone, userName, mealTime);
            if (enviado) return Ok(new { success = true, message = $"Lembrete enviado para {phone}!" });
            return StatusCode(500, new { success = false, message = "Falha ao enviar lembrete." });
        }

        private async Task<string> GerarFeedbackDiarioComGeminiAsync(
            string patientGoal,
            int targetCalories,
            int consumedCalories,
            decimal targetProtein,
            decimal consumedProtein,
            List<string> mealsLogged)
        {
            try
            {
                var apiKey = _configuration["GeminiApiKey"] ?? _configuration["Gemini:ApiKey"];
                var endpoint = _configuration["Gemini:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
                var model = _configuration["Model"] ?? "gemini-1.5-flash";

                var mealsSummary = (mealsLogged != null && mealsLogged.Any())
                    ? string.Join(", ", mealsLogged)
                    : "Nenhuma refeição registrada hoje.";

                int diff = targetCalories - consumedCalories;
                string statusCalorico = diff switch
                {
                    > 200 => $"Faltaram {diff} kcal para atingir a meta.",
                    < -200 => $"Ultrapassou a meta em {Math.Abs(diff)} kcal.",
                    _ => "Atingiu a meta calórica com excelente precisão!"
                };

                var prompt = $@"
Você é um assistente nutricional motivacional, empático e amigável enviando uma mensagem no WhatsApp ao final do dia.
DADOS DO DIA DO PACIENTE:
- Objetivo: {patientGoal}
- Meta de Calorias: {targetCalories} kcal | Consumido: {consumedCalories} kcal ({statusCalorico})
- Meta Proteína: {targetProtein}g | Consumido: {consumedProtein:F0}g
- Refeições Registradas: {mealsSummary}

INSTRUÇÕES:
1. Escreva uma mensagem curta (máximo 3 a 4 frases) para o WhatsApp com emojis.
2. Seja encorajador se ele esteve perto da meta, ou acolhedor se ele ultrapassou/esqueceu de registrar.
3. Não use termos técnicos complexos. Termine com uma palavra de incentivo para o dia seguinte.
Retorne APENAS o texto da mensagem pronto para envio.
";

                var client = _httpClientFactory.CreateClient("Gemini");
                var requestBody = new
                {
                    model = model,
                    temperature = 0.7,
                    messages = new object[]
                    {
                        new { role = "user", content = prompt }
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var response = await client.SendAsync(requestMessage);
                if (!response.IsSuccessStatusCode)
                {
                    return "🌙 Boa noite! Passando para lembrar de conferir suas refeições registradas de hoje. Amanhã seguimos juntos no foco! 🥗";
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                var rawText = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? string.Empty;

                if (rawText.StartsWith("```"))
                {
                    var lines = rawText.Split('\n').Skip(1).Where(l => !l.StartsWith("```"));
                    rawText = string.Join("\n", lines);
                }

                return rawText.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar feedback diário com Gemini.");
                return "🌙 Boa noite! Passando para lembrar de conferir suas refeições registradas de hoje. Amanhã seguimos juntos no foco! 🥗";
            }
        }

        private async Task<string> TranscreverAudioComGeminiAsync(byte[] audioBytes)
        {
            var apiKey = _configuration["GeminiApiKey"] ?? _configuration["Gemini:ApiKey"];
            var endpoint = _configuration["Gemini:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
            var model = _configuration["Model"] ?? "gemini-3.1-flash-lite";

            var client = _httpClientFactory.CreateClient();
            var base64Audio = Convert.ToBase64String(audioBytes);
            var dataUri = $"data:audio/ogg;base64,{base64Audio}";

            var requestBody = new
            {
                model = model,
                temperature = 0.0,
                messages = new object[]
                {
                    new { role = "system", content = "Você é um transcritor. Transcreva fielmente o áudio. Retorne APENAS o texto." },
                    new { role = "user", content = new object[] { new { type = "text", text = "Transcreva:" }, new { type = "image_url", image_url = new { url = dataUri } } } }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await client.SendAsync(requestMessage);
            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? string.Empty;
        }

        private async Task<SetupProfileDto> ExtrairPerfilComGeminiAsync(string userText)
        {
            return await Task.FromResult(new SetupProfileDto());
        }

        private string FormatarRespostaParaWhatsApp(MealAnalysisResponseDto aiResult, DailyStatusResponseDto? statusDoDia)
        {
            if (aiResult.ClarificationQuestion != null && aiResult.ClarificationQuestion.Contains("instáveis", StringComparison.OrdinalIgnoreCase))
                return "⚠️ *Ops! Os nossos serviços estão instáveis no momento.*\nPor favor, tente novamente em instantes.";

            if (aiResult.IsAdvice)
                return $"💡 *Conselho do Nutri:* \n\n{(!string.IsNullOrWhiteSpace(aiResult.AdviceText) ? aiResult.AdviceText : "Como posso ajudar?")}";

            if (aiResult.RequiresUserClarification)
                return $"🤔 *Atenção ao Protocolo / Clarificação:*\n{aiResult.ClarificationQuestion}";

            var prato = !string.IsNullOrWhiteSpace(aiResult.DishName) ? aiResult.DishName : aiResult.MealType;
            var calorias = aiResult.TotalMeal?.Calories ?? 0;
            var proteina = aiResult.TotalMeal?.ProteinG ?? 0;
            var carbo = aiResult.TotalMeal?.CarbsG ?? 0;
            var gordura = aiResult.TotalMeal?.FatG ?? 0;

            var msg = $"✅ *Refeição registada:* {prato}\n\n";

            if (aiResult.Items != null && aiResult.Items.Any())
            {
                msg += "📋 *Itens identificados:*\n";
                foreach (var item in aiResult.Items)
                {
                    msg += $"• {item.FoodName} ({item.PortionDescription}) — *{item.Calories} kcal*\n";
                }
                msg += "\n";
            }

            msg += $"🔥 *Calorias Totais:* {calorias} kcal\n" +
                   $"🥩 *Proteínas:* {proteina}g\n" +
                   $"🍞 *Carboidratos:* {carbo}g\n" +
                   $"🥑 *Gorduras:* {gordura}g\n\n";

            if (statusDoDia != null)
            {
                if (statusDoDia.StreakDays > 0) msg += $"🔥 *OFENSIVA:* {statusDoDia.StreakDays} dia(s) seguidos no foco! 🚀\n\n";
                msg += $"📊 *O SEU RESUMO DE HOJE*\n• *Calorias:* {statusDoDia.Consumed.Calories} / {statusDoDia.Target.Calories} kcal\n";
                var faltamCal = statusDoDia.Remaining.Calories;
                msg += faltamCal <= 0 ? $"_⚠️ Atingiu ou ultrapassou a sua meta!_\n" : $"_Faltam {faltamCal} kcal_\n";
                msg += $"• *Proteínas:* {statusDoDia.Consumed.ProteinG:F0}g / {statusDoDia.Target.ProteinG:F0}g\n" +
                       $"• *Carboidratos:* {statusDoDia.Consumed.CarbsG:F0}g / {statusDoDia.Target.CarbsG:F0}g\n" +
                       $"• *Gorduras:* {statusDoDia.Consumed.FatG:F0}g / {statusDoDia.Target.FatG:F0}g\n";

                if (faltamCal > 100 && statusDoDia.Suggestions != null && statusDoDia.Suggestions.Any())
                {
                    msg += "\n💡 *SUGESTÕES PARA A PRÓXIMA REFEIÇÃO:*\n";
                    foreach (var sugestao in statusDoDia.Suggestions) msg += $"• {sugestao}\n";
                }
            }
            return msg;
        }

        private class SetupProfileDto
        {
            public int TargetCalories { get; set; } = 2000;
            public string MainGoal { get; set; } = "";
            public string MedicalRestrictions { get; set; } = "";
            public string FoodAversions { get; set; } = "";
        }
    }

    public class MetaWebhookPayload { public List<MetaEntry>? Entry { get; set; } }
    public class MetaEntry { public List<MetaChange>? Changes { get; set; } }
    public class MetaChange { public MetaValue? Value { get; set; } }
    public class MetaValue { public List<MetaMessage>? Messages { get; set; } }
    public class MetaMessage { public string? From { get; set; } public string? Type { get; set; } public MetaText? Text { get; set; } public MetaMedia? Image { get; set; } public MetaMedia? Audio { get; set; } }
    public class MetaText { public string? Body { get; set; } }
    public class MetaMedia { public string? Id { get; set; } public string? Mime_Type { get; set; } }
}