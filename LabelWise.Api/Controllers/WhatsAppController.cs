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
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LabelWise.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class WhatsAppController : ControllerBase
    {
        private const int TrialDurationDays = 15;
        private const int DailyTrialInteractionLimit = 4;
        private static readonly int[] TrialReminderDays = { 3, 7, 13 };

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

        // 🔗 ENDPOINT DE RASTREIO DE CLIQUE NO LINK DE PAGAMENTO
        [HttpGet("ir-para-pagamento")]
        public async Task<IActionResult> IrParaPagamento([FromQuery] string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return BadRequest("Telefone não especificado.");
            }

            try
            {
                var trackingCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("PaymentTracking");
                var filter = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", phone);

                // 1. Regista o clique na collection
                var update = Builders<MongoDB.Bson.BsonDocument>.Update
                    .Set("ClickedAt", DateTime.UtcNow)
                    .Set("Status", "Clicked");

                await trackingCol.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });

                // 2. Busca o link real da InfinitePay guardado para este utilizador
                var doc = await trackingCol.Find(filter).FirstOrDefaultAsync();
                string? infinitePayUrl = doc != null && doc.Contains("InfinitePayUrl") ? doc["InfinitePayUrl"].AsString : null;

                if (!string.IsNullOrEmpty(infinitePayUrl))
                {
                    // 3. Redireciona o utilizador para o checkout oficial
                    return Redirect(infinitePayUrl);
                }

                return Content("Link de pagamento não encontrado ou expirado. Por favor, solicite um novo link no WhatsApp.", "text/html; charset=utf-8");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar clique de pagamento para {Phone}", phone);
                return StatusCode(500, "Erro interno ao redirecionar.");
            }
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
                else if (messageType == "button")
                {
                    textoBruto = messagingEvent?.Button?.Text?.Trim();
                }
                else if (messageType == "interactive")
                {
                    var interactiveType = messagingEvent?.Interactive?.Type;
                    if (interactiveType == "button_reply")
                    {
                        textoBruto = messagingEvent?.Interactive?.ButtonReply?.Title?.Trim();
                    }
                    else if (interactiveType == "list_reply")
                    {
                        textoBruto = messagingEvent?.Interactive?.ListReply?.Title?.Trim();
                    }
                }
                else if (messageType == "audio" && messagingEvent?.Audio?.Id != null)
                {
                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "🎙 A ouvir o seu áudio e a transcrever...");
                    var audioBytes = await _metaMediaService.DownloadMediaAsBytesAsync(messagingEvent.Audio.Id);
                    textoBruto = await TranscreverAudioComGeminiAsync(audioBytes);

                    if (string.IsNullOrWhiteSpace(textoBruto))
                    {
                        await _whatsAppSender.SendTextMessageAsync(senderPhone, "⚠️ Não consegui compreender o áudio. Por favor, tente enviar novamente ou digite por texto.");
                        return Ok();
                    }
                }
                else if (messageType == "image" && messagingEvent?.Image?.Id != null)
                {
                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "📸 A analisar a imagem, só um instante...");
                    imagemBase64 = await _metaMediaService.DownloadMediaAsBase64Async(messagingEvent.Image.Id);
                    textoBruto = "Analise esta imagem nutricional.";
                }
                else
                {
                    return Ok();
                }

                if (string.IsNullOrWhiteSpace(textoBruto) && string.IsNullOrWhiteSpace(imagemBase64))
                {
                    return Ok();
                }

                string textoLimpoCmd = Regex.Replace(textoBruto ?? "", "[^a-zA-Z0-9áéíóúâêîôûãõçÁÉÍÓÚÂÊÎÔÛÃÕÇ ]", "").Trim().ToLowerInvariant();

                // 📊 INTERCETOR DE RESUMO / PAINEL VISUAL (DIÁRIO E MENSAL)
                if (textoLimpoCmd == "resumo" || textoLimpoCmd == "relatorio" || textoLimpoCmd == "relatório" ||
                    textoLimpoCmd == "resumo do mes" || textoLimpoCmd == "resumo do mês" || textoLimpoCmd == "resumo diario" ||
                    textoLimpoCmd == "resumo diário" || textoLimpoCmd == "progresso" || textoLimpoCmd == "painel")
                {
                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "⏳ A calcular o seu resumo diário e mensal...");
                    string painelTexto = await ObterResumoFormatadoWhatsappAsync(senderPhone);
                    await _whatsAppSender.SendTextMessageAsync(senderPhone, painelTexto);
                    await _nutritionRepository.SalvarMensagemHistoricoAsync(senderPhone, "assistant", painelTexto);
                    return Ok();
                }

                // 🚀 INTERCETOR DE NOVIDADES ATUALIZADO
                if (textoLimpoCmd == "quero ver a novidade")
                {
                    string novidadeTexto = "🔥 *Novidades Incríveis no Nutrição Fácil!* 🚀\n\n" +
                                           "Implementámos várias funcionalidades de ponta para facilitar a sua rotina:\n\n" +
                                           "1️⃣ **Substituição e Troca Inteligente:** Quer trocar algum alimento? Basta enviar ex: *'Substituir frango por patinho'* e eu encontro a melhor alternativa respeitando o seu plano!\n\n" +
                                           "2️⃣ **Modo Correção de Refeições:** Esqueceu-se de algo no prato? Diga ex: *'Esqueci de registar o azeite'* e eu ajusto a sua última refeição na hora!\n\n" +
                                           "3️⃣ **Painel Visual com Barras (🟩⬜):** Digite *'resumo'* a qualquer momento para ver o seu painel diário e o acumulado mensal com gráficos visuais de macros e água!\n\n" +
                                           "4️⃣ **Feedbacks de Fim de Dia:** Agora conta com uma rotina inteligente que lhe envia um resumo motivacional personalizado ao final do dia para celebrar o seu progresso! 🥗💪\n\n" +
                                           "Vá em frente, teste enviar um destes comandos agora mesmo!";

                    await _whatsAppSender.SendTextMessageAsync(senderPhone, novidadeTexto);
                    await _nutritionRepository.SalvarMensagemHistoricoAsync(senderPhone, "assistant", novidadeTexto);
                    return Ok();
                }

                if (textoLimpoCmd == "agora não" || textoLimpoCmd == "agora nao")
                {
                    string msgOk = "Sem problemas! Estarei por aqui quando precisar registar as suas refeições ou treinos. 🥗💪";
                    await _whatsAppSender.SendTextMessageAsync(senderPhone, msgOk);
                    return Ok();
                }

                if (textoLimpoCmd == "assinar" || textoLimpoCmd == "pagar" || textoLimpoCmd == "plano" || textoLimpoCmd == "assinatura")
                {
                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "⏳ A gerar o seu link seguro de pagamento na InfinitePay...");
                    string? paymentUrlOriginal = await GerarLinkPagamentoInfinitePayAsync(senderPhone, "Cliente Nutrição Fácil", 29.90m);

                    if (!string.IsNullOrEmpty(paymentUrlOriginal))
                    {
                        var trackingCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("PaymentTracking");
                        var trackingDoc = new MongoDB.Bson.BsonDocument
                        {
                            { "_id", senderPhone },
                            { "InfinitePayUrl", paymentUrlOriginal },
                            { "SentAt", DateTime.UtcNow },
                            { "ClickedAt", MongoDB.Bson.BsonNull.Value },
                            { "Status", "Sent" }
                        };

                        await trackingCol.ReplaceOneAsync(
                            Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", senderPhone),
                            trackingDoc,
                            new ReplaceOptions { IsUpsert = true }
                        );

                        var baseUrl = $"{Request.Scheme}://{Request.Host}";
                        var trackingLink = $"{baseUrl}/api/whatsapp/ir-para-pagamento?phone={senderPhone}";

                        string msgAssinatura = "💳 *Assinatura Mensal - Nutrição Fácil*\n\n" +
                                               "Para continuar a usar o assistente de nutrição sem limites, clique no link seguro abaixo (R$ 29,90):\n\n" +
                                               $"👉 {trackingLink}\n\n" +
                                               "A liberação do acesso depende da confirmação e ativação da assinatura no sistema.";
                        await _whatsAppSender.SendTextMessageAsync(senderPhone, msgAssinatura);
                    }
                    else
                    {
                        await _whatsAppSender.SendTextMessageAsync(senderPhone, "⚠️ Ocorreu um erro ao gerar o link de pagamento. Tente novamente em instantes.");
                    }
                    return Ok();
                }

                // 🛡️ INTERCETOR DE MENSAGEM "JÁ PAGUEI"
                if (textoLimpoCmd == "ja paguei" || textoLimpoCmd == "já paguei" ||
                    textoLimpoCmd == "paguei" || textoLimpoCmd == "fiz o pix" ||
                    textoLimpoCmd == "ja fiz o pix" || textoLimpoCmd == "fiz o pagamento" ||
                    textoLimpoCmd == "pago")
                {
                    string msgAguarde = "⏳ *A aguardar confirmação do sistema...*\n\n" +
                                        "Se já efetuou o pagamento, não se preocupe! O processamento do sistema da InfinitePay pode demorar até 5 minutos (ou um pouco mais se for por cartão de crédito).\n\n" +
                                        "Assim que o banco nos enviar a confirmação automática, o seu acesso Premium será libertado instantaneamente e eu aviso-lhe por aqui! 🎉\n\n" +
                                        "_(Se já passou muito tempo, por favor aguarde que um humano da nossa equipa de suporte irá analisar esta conversa)_";

                    await _whatsAppSender.SendTextMessageAsync(senderPhone, msgAguarde);
                    await _nutritionRepository.SalvarMensagemHistoricoAsync(senderPhone, "assistant", msgAguarde);
                    return Ok();
                }

                var patientsCollectionCheck = _database.GetCollection<MongoDB.Bson.BsonDocument>("Nutrition_Patients");
                var patientDocCheck = await patientsCollectionCheck.Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", senderPhone)).FirstOrDefaultAsync();

                bool isB2bPatient = patientDocCheck != null &&
                                    patientDocCheck.Contains("ProfessionalId") &&
                                    patientDocCheck["ProfessionalId"].AsString != "b2c_autonomous_user";

                var trialCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("B2C_Trial_Users");
                var filterTrial = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", senderPhone);
                var userDoc = await trialCollection.Find(filterTrial).FirstOrDefaultAsync();
                bool isPremium = userDoc != null &&
                               userDoc.TryGetValue("Premium", out var premiumValue) &&
                               premiumValue.IsBoolean && premiumValue.AsBoolean;
                bool isTrialLimitedUser = !isB2bPatient && !isPremium;

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

                if (!isB2bPatient && !isPremium)
                {
                    var now = DateTime.UtcNow;

                    if (userDoc == null)
                    {
                        userDoc = new MongoDB.Bson.BsonDocument
                        {
                            { "_id", senderPhone },
                            { "TrialStartDate", now },
                            { "LastInteractionDate", DateTime.SpecifyKind(now.AddHours(-3).Date, DateTimeKind.Utc) },
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

                    if (userDoc.TryGetValue("TrialStartDate", out var trialStartValue) && trialStartValue.IsBsonDateTime)
                    {
                        var trialStartDate = trialStartValue.ToUniversalTime();
                        if (now >= trialStartDate.AddDays(TrialDurationDays))
                        {
                            _logger.LogInformation("[WhatsApp B2C] Trial expirado após {TrialDays} dias.", TrialDurationDays);

                            string? paymentUrlOriginal = await GerarLinkPagamentoInfinitePayAsync(senderPhone, "Cliente Trial", 29.90m);
                            string? trackingLink = null;

                            if (!string.IsNullOrEmpty(paymentUrlOriginal))
                            {
                                var trackingCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("PaymentTracking");
                                var trackingDoc = new MongoDB.Bson.BsonDocument
                                {
                                    { "_id", senderPhone },
                                    { "InfinitePayUrl", paymentUrlOriginal },
                                    { "SentAt", DateTime.UtcNow },
                                    { "ClickedAt", MongoDB.Bson.BsonNull.Value },
                                    { "Status", "Sent" }
                                };
                                await trackingCol.ReplaceOneAsync(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", senderPhone), trackingDoc, new ReplaceOptions { IsUpsert = true });

                                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                                trackingLink = $"{baseUrl}/api/whatsapp/ir-para-pagamento?phone={senderPhone}";
                            }

                            string msgExpirado = "⏳ *Seu período de teste de 15 dias terminou.*\n\n" +
                                                 "Se quiser continuar, veja os detalhes da assinatura mensal abaixo:\n\n" +
                                                 (!string.IsNullOrEmpty(trackingLink) ? $"👉 {trackingLink}\n\n" : "") +
                                                 "A liberação do acesso depende da confirmação e ativação da assinatura no sistema.";
                            await _whatsAppSender.SendTextMessageAsync(senderPhone, msgExpirado);
                            return Ok();
                        }
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
                                               "_(Responda com o número, ou digite *cancelar* para sair)_";
                        await _whatsAppSender.SendTextMessageAsync(senderPhone, mensagemReset);
                        return Ok();
                    }

                    bool profileConfigured = userDoc.Contains("ProfileConfigured") && userDoc["ProfileConfigured"].AsBoolean;

                    if (!profileConfigured)
                    {
                        string step = userDoc.Contains("OnboardingStep") ? userDoc["OnboardingStep"].AsString : "WaitingForGoal";

                        if (textoLimpoCmd == "cancelar" || textoLimpoCmd == "sair" || textoLimpoCmd == "voltar")
                        {
                            var goalsCollectionCheck = _database.GetCollection<MongoDB.Bson.BsonDocument>("DailyGoals");
                            var existingGoalCheck = await goalsCollectionCheck.Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("UserId", senderPhone)).FirstOrDefaultAsync();

                            if (existingGoalCheck != null)
                            {
                                var updateCancel = Builders<MongoDB.Bson.BsonDocument>.Update.Set("ProfileConfigured", true);
                                await trialCollection.UpdateOneAsync(filterTrial, updateCancel);

                                await _whatsAppSender.SendTextMessageAsync(senderPhone, "✅ *Configuração cancelada!* \n\nO seu perfil e metas anteriores foram mantidos. Pode continuar a registar as suas refeições normalmente! 🥗");
                                return Ok();
                            }
                            else
                            {
                                await _whatsAppSender.SendTextMessageAsync(senderPhone, "⚠️ Como é o seu primeiro acesso, preciso que termine esta configuração rápida para poder calcular as suas metas diárias.\n\nPor favor, escolha uma das opções (Ex: digite 1, 2 ou 3).");
                                return Ok();
                            }
                        }

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
                                        .Set("OnboardingStep", "WaitingForProfile");
                                    await trialCollection.UpdateOneAsync(filterTrial, updateGoal);

                                    await _whatsAppSender.SendTextMessageAsync(senderPhone,
                                        "🔥 **Passo 2 de 4:** Para eu calcular a sua dieta exata (como um nutricionista faria), preciso de te conhecer melhor!\n\n" +
                                        "👉 *Diga-me numa só mensagem: O seu Peso, Altura, Idade, Sexo e rotina de atividade física.*");
                                }
                                else
                                {
                                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "❌ Opção inválida.\n\n🎯 **Passo 1 de 4:** Escolha o número correspondente ao seu principal objetivo:\n*1️⃣* Emagrecimento\n*2️⃣* Hipertrofia\n*3️⃣* Manutenção\n\n_(Ou digite *cancelar* para sair)_");
                                }
                                return Ok();

                            case "WaitingForProfile":
                                await _whatsAppSender.SendTextMessageAsync(senderPhone, "🧠 *A processar os seus dados biológicos e a calcular as metas ideais com IA...* ⏱️");

                                string userGoal = userDoc.Contains("TempGoal") ? userDoc["TempGoal"].AsString : "Emagrecimento";
                                var calculatedGoals = await _nutritionAgentService.CalculateNutritionalGoalsAsync(textoBruto!, userGoal);

                                var updateProfile = Builders<MongoDB.Bson.BsonDocument>.Update
                                    .Set("TempCalories", calculatedGoals.TargetCalories)
                                    .Set("TempProtein", (double)calculatedGoals.TargetProteinG)
                                    .Set("TempCarbs", (double)calculatedGoals.TargetCarbsG)
                                    .Set("TempFat", (double)calculatedGoals.TargetFatG)
                                    .Set("TempWater", calculatedGoals.TargetWaterMl)
                                    .Set("OnboardingStep", "WaitingForRestrictions");

                                await trialCollection.UpdateOneAsync(filterTrial, updateProfile);

                                string msgCalculo = $"✅ **Metas Calculadas com Sucesso!**\n\n" +
                                                    $"📊 {calculatedGoals.ShortExplanation}\n\n" +
                                                    $"🎯 *As suas Metas Diárias:*\n" +
                                                    $"🔥 Calorias: {calculatedGoals.TargetCalories} kcal\n" +
                                                    $"🥩 Proteína: {calculatedGoals.TargetProteinG:F0}g | 🍞 Carbo: {calculatedGoals.TargetCarbsG:F0}g | 🥑 Gordura: {calculatedGoals.TargetFatG:F0}g\n" +
                                                    $"💧 Água: {calculatedGoals.TargetWaterMl} ml\n\n" +
                                                    $"🛡️ **Passo 3 de 4:** Tem alguma alergia, intolerância ou restrição médica? _(Digite 'Nenhuma' se não tiver)_";

                                await _whatsAppSender.SendTextMessageAsync(senderPhone, msgCalculo);
                                return Ok();

                            case "WaitingForRestrictions":
                                var restrictions = string.Equals(textoBruto, "nenhuma", StringComparison.OrdinalIgnoreCase) ? "" : textoBruto;

                                var updateRest = Builders<MongoDB.Bson.BsonDocument>.Update
                                    .Set("TempRestrictions", restrictions)
                                    .Set("OnboardingStep", "WaitingForAversions");
                                await trialCollection.UpdateOneAsync(filterTrial, updateRest);

                                await _whatsAppSender.SendTextMessageAsync(senderPhone, "🚫 **Passo 4 de 4:** Tem alguma aversão ou comida que não gosta?\n_(Ex: Ovo, cebola, ou digite 'Nenhuma')_");
                                return Ok();

                            case "WaitingForAversions":
                                var aversions = string.Equals(textoBruto, "nenhuma", StringComparison.OrdinalIgnoreCase) ? "" : textoBruto;

                                string goal = userDoc.Contains("TempGoal") ? userDoc["TempGoal"].AsString : "Emagrecimento";
                                int calories = userDoc.Contains("TempCalories") ? userDoc["TempCalories"].AsInt32 : 2000;
                                decimal protein = userDoc.Contains("TempProtein") ? (decimal)userDoc["TempProtein"].AsDouble : 150m;
                                decimal carbs = userDoc.Contains("TempCarbs") ? (decimal)userDoc["TempCarbs"].AsDouble : 200m;
                                decimal fat = userDoc.Contains("TempFat") ? (decimal)userDoc["TempFat"].AsDouble : 60m;
                                int waterMl = userDoc.Contains("TempWater") ? userDoc["TempWater"].AsInt32 : 2500;
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
                                    { "TargetProteinG", (double)protein },
                                    { "TargetCarbsG", (double)carbs },
                                    { "TargetFatG", (double)fat },
                                    { "TargetWaterMl", waterMl },
                                    { "DietaryRestrictions", medRest },
                                    { "FavoriteFoods", aversions },
                                    { "PrescribedMealPlan", $"Objetivo: {goal}. Calculado autonomamente." }
                                };
                                await goalsCollection.ReplaceOneAsync(goalFilter, goalDoc, new ReplaceOptions { IsUpsert = true });

                                var updateDone = Builders<MongoDB.Bson.BsonDocument>.Update
                                    .Set("ProfileConfigured", true);
                                await trialCollection.UpdateOneAsync(filterTrial, updateDone);

                                string respostaConfig = $"✅ *Perfil configurado com sucesso!* 🥗\n\n" +
                                                        $"🎯 **Objetivo:** {goal}\n" +
                                                        $"🔥 **Calorias Diárias:** {calories} kcal\n" +
                                                        $"🛡 **Alergias/Restrições:** {(string.IsNullOrWhiteSpace(medRest) ? "Nenhuma" : medRest)}\n" +
                                                        $"🚫 **Aversões:** {(string.IsNullOrWhiteSpace(aversions) ? "Nenhuma" : aversions)}\n\n" +
                                                        $"👉 *Tudo pronto! Já pode enviar as suas refeições* por texto, foto ou áudio.\n\n" +
                                                        $"_💡 Dica: Se quiser reajustar o cálculo das suas metas no futuro, basta digitar *meta* a qualquer momento!_";

                                await _whatsAppSender.SendTextMessageAsync(senderPhone, respostaConfig);
                                return Ok();
                        }
                    }
                }

                bool isNumeroIsolado = int.TryParse(textoLimpoCmd, out int numeroIsoladoVal);

                if (textoLimpoCmd.StartsWith("substituir ") || textoLimpoCmd.StartsWith("trocar "))
                {
                    var alimentoAlvo = (textoBruto ?? "").Replace("substituir", "", StringComparison.OrdinalIgnoreCase)
                                                         .Replace("trocar", "", StringComparison.OrdinalIgnoreCase)
                                                         .Replace("por", "", StringComparison.OrdinalIgnoreCase).Trim();

                    if (!string.IsNullOrWhiteSpace(alimentoAlvo))
                    {
                        if (isTrialLimitedUser && !await ReserveTrialInteractionOrNotifyAsync(senderPhone, trialCollection))
                        {
                            return Ok();
                        }

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

                // 🗑️ REMOÇÃO SIMPLES
                bool eIntencaoRemoverSimples =
                    textoLimpoCmd == "remover" || textoLimpoCmd == "apagar" || textoLimpoCmd == "excluir" ||
                    textoLimpoCmd == "deletar" || textoLimpoCmd == "listar refeicoes" || textoLimpoCmd == "remover refeicao" ||
                    Regex.IsMatch(textoLimpoCmd, @"^r+[em]*o+v+e*r*$") ||
                    Regex.IsMatch(textoLimpoCmd, @"^a+p+a+g+a*r*$") ||
                    Regex.IsMatch(textoLimpoCmd, @"^e+x+c+l+u+i*r*$");

                if (eIntencaoRemoverSimples)
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

                // 🗑️ REMOÇÃO COM ARGUMENTO OU NÚMERO ISOLADO
                bool eIntencaoRemoverComArg =
                    textoLimpoCmd.StartsWith("remover ") || textoLimpoCmd.StartsWith("rrmover ") ||
                    textoLimpoCmd.StartsWith("rmover ") || textoLimpoCmd.StartsWith("apagar ") ||
                    textoLimpoCmd.StartsWith("excluir ") || textoLimpoCmd.StartsWith("deletar ") ||
                    isNumeroIsolado;

                if (eIntencaoRemoverComArg)
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
                        var partes = (textoBruto ?? "").Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
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
                        await _whatsAppSender.SendTextMessageAsync(senderPhone, "❌ Número ou refeição inválida. Diga *remover* para ver a lista atualizada das suas refeições.");
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
                                              "💡 *Dica:* Diga *resumo* para ver o seu painel de hoje/mês, *remover* para ver as refeições ou *minha dieta* para o plano completo!";

                    await _whatsAppSender.SendTextMessageAsync(senderPhone, respostaSaudacao);
                    await _nutritionRepository.SalvarMensagemHistoricoAsync(senderPhone, "assistant", respostaSaudacao);
                    return Ok();
                }

                bool mencionaAgua = (textoBruto ?? "").Contains("agua", StringComparison.OrdinalIgnoreCase) ||
                                    (textoBruto ?? "").Contains("água", StringComparison.OrdinalIgnoreCase) ||
                                    (textoBruto ?? "").Contains("h2o", StringComparison.OrdinalIgnoreCase);

                if (mencionaAgua)
                {
                    int quantidadeMl = 250;

                    var matchMl = Regex.Match(textoBruto ?? "", @"(\d+)\s*(ml|litros|l)\b", RegexOptions.IgnoreCase);
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
                        var matchCopo = Regex.Match(textoBruto ?? "", @"(\d+)?\s*(copo|copos|garrafa|garrafas)\b", RegexOptions.IgnoreCase);
                        if (matchCopo.Success)
                        {
                            int qtd = 1;
                            if (matchCopo.Groups[1].Success && int.TryParse(matchCopo.Groups[1].Value, out int parsedQtd))
                            {
                                qtd = parsedQtd;
                            }
                            quantidadeMl = qtd * 250;
                        }
                    }

                    var dataHojeBrAgua = DateTime.UtcNow.AddHours(-3).Date;
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
                        Builders<MongoDB.Bson.BsonDocument>.Filter.Gte("LoggedAt", dataHojeBrAgua)
                    );
                    var registrosAgua = await waterCol.Find(filterWater).ToListAsync();
                    int totalAguaHoje = registrosAgua.Sum(w => w["AmountMl"].AsInt32);

                    int metaAguaDiaria = 2500;
                    var goalsCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("DailyGoals");
                    var goalFilterWater = Builders<MongoDB.Bson.BsonDocument>.Filter.And(
                        Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("UserId", senderPhone),
                        Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("TargetDate", dataHojeBrAgua)
                    );
                    var goalDocWater = await goalsCollection.Find(goalFilterWater).FirstOrDefaultAsync();
                    if (goalDocWater != null && goalDocWater.Contains("TargetWaterMl"))
                    {
                        metaAguaDiaria = goalDocWater["TargetWaterMl"].AsInt32;
                    }

                    string respostaAgua = $"💧 {totalAguaHoje} / {metaAguaDiaria} ml de água registados com sucesso! 🚰";
                    await _whatsAppSender.SendTextMessageAsync(senderPhone, respostaAgua);
                    await _nutritionRepository.SalvarMensagemHistoricoAsync(senderPhone, "assistant", respostaAgua);
                    return Ok();
                }

                var matchPeso = Regex.Match(textoBruto ?? "", @"(?:peso|peso hoje|estou com|estou pesando)\D*(\d+[\.,]?\d*)\s*kg", RegexOptions.IgnoreCase);
                if (matchPeso.Success || ((textoBruto ?? "").Contains("kg", StringComparison.OrdinalIgnoreCase) && Regex.IsMatch(textoBruto ?? "", @"\d+[\.,]?\d*")))
                {
                    string valorPesoStr = matchPeso.Success ? matchPeso.Groups[1].Value : Regex.Match(textoBruto ?? "", @"\d+[\.,]?\d*").Value;
                    if (decimal.TryParse(valorPesoStr.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal pesoInformado))
                    {
                        var dataHojeBrPeso = DateTime.UtcNow.AddHours(-3).Date;
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
                                Builders<MongoDB.Bson.BsonDocument>.Filter.Gte("LoggedAt", dataHojeBrPeso)
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

                // 🏃‍♂️ REGISTO DE EXERCÍCIO / TREINO
                var palavrasTreino = new[] { "treino", "treinei", "correr", "corri", "caminhada", "caminhei", "pedalar", "pedalei", "bike", "bicicleta", "natação", "natacao", "crossfit", "musculação", "musculacao", "pilates", "academia", "exercício", "exercicio", "esteira", "malhar", "malhei", "yoga", "futebol" };
                var palavrasComida = new[] { "comi", "bebi", "almoço", "almoco", "jantar", "café", "cafe", "lanche", "prato", "fome", "comida", "refeição", "refeicao" };

                bool pareceTreino = palavrasTreino.Any(p => textoLimpoCmd.Contains(p)) && !palavrasComida.Any(p => textoLimpoCmd.Contains(p));

                if (pareceTreino && string.IsNullOrWhiteSpace(imagemBase64))
                {
                    if (isTrialLimitedUser && !await ReserveTrialInteractionOrNotifyAsync(senderPhone, trialCollection))
                    {
                        return Ok();
                    }

                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "💪 A estimar as calorias gastas no seu treino...");
                    int caloriasGastas = await EstimarGastoCaloricoComGeminiAsync(textoBruto ?? "");

                    if (caloriasGastas > 0)
                    {
                        var dataHojeBrTreino = DateTime.UtcNow.AddHours(-3);
                        var mealCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("MealLogs");

                        var mealDoc = new MongoDB.Bson.BsonDocument
                        {
                            { "_id", Guid.NewGuid().ToString() },
                            { "UserId", senderPhone },
                            { "MealType", "Treino" },
                            { "DishName", $"🔥 {textoBruto}" },
                            { "Calories", -caloriasGastas },
                            { "ProteinG", 0.0 },
                            { "CarbsG", 0.0 },
                            { "FatG", 0.0 },
                            { "LoggedAt", DateTime.UtcNow },
                            { "Items", new MongoDB.Bson.BsonArray() }
                        };

                        await mealCol.InsertOneAsync(mealDoc);

                        var statusAtualizado = await _nutritionService.GetDailyStatusAndSuggestionAsync(senderPhone, dataHojeBrTreino);

                        string respostaTreino = $"🔥 *Treino registado com sucesso!*\n\n" +
                                                $"Gastou aproximadamente *{caloriasGastas} kcal* com essa atividade.\n" +
                                                $"Isso gerou um bónus calórico no seu saldo de hoje! 🏃‍♂️💨\n\n" +
                                                $"📊 *O SEU NOVO SALDO*\n" +
                                                $"• *Calorias Consumidas (Líquidas):* {statusAtualizado.Consumed.Calories} / {statusAtualizado.Target.Calories} kcal\n" +
                                                $"_Pode consumir mais {statusAtualizado.Remaining.Calories} kcal hoje._";

                        await _whatsAppSender.SendTextMessageAsync(senderPhone, respostaTreino);
                        await _nutritionRepository.SalvarMensagemHistoricoAsync(senderPhone, "assistant", respostaTreino);
                        return Ok();
                    }
                    else
                    {
                        string msgTreinoNaoIdentificado = "🏃‍♂️ Não consegui estimar o gasto calórico para essa atividade.\n\nTente detalhar o tempo ou distância (ex: *'30 min de caminhada'* ou *'Corri 5km'*).";
                        await _whatsAppSender.SendTextMessageAsync(senderPhone, msgTreinoNaoIdentificado);
                        return Ok();
                    }
                }

                // 🧠 CONTEXTO PENDENTE & MODO CORREÇÃO
                var contextoPendente = await _nutritionRepository.ObterClarificacaoPendenteAsync(senderPhone);
                string textoFinalParaIa = textoBruto ?? "";
                string? imagemFinalParaIa = imagemBase64;

                if (contextoPendente != null)
                {
                    textoFinalParaIa = $"[Contexto Anterior da Refeição: {contextoPendente.OriginalTextInput}] " +
                                       $"[Alerta de Restrição Enviado: {contextoPendente.ClarificationQuestion}] " +
                                       $"[Confirmação / Resposta do Utilizador: {textoBruto}] " +
                                       $"Instrução: Processe e registe definitivamente esta refeição considerando a resposta do utilizador.";

                    imagemFinalParaIa ??= contextoPendente.OriginalBase64Image;
                    await _nutritionRepository.RemoverClarificacaoPendenteAsync(senderPhone);
                }
                else
                {
                    var palavrasCorrecao = new[] { "esqueci", "corrigir", "não tinha", "adiciona", "tira", "remova", "errei", "faltei" };
                    bool pareceCorrecao = palavrasCorrecao.Any(p => textoLimpoCmd.Contains(p));

                    if (pareceCorrecao && string.IsNullOrWhiteSpace(imagemBase64))
                    {
                        var dataHojeBrCorrecao = DateTime.UtcNow.AddHours(-3);
                        var logsHoje = await _nutritionRepository.ObterRefeicoesDoDiaAsync(senderPhone, dataHojeBrCorrecao);

                        var ultimaRefeicao = logsHoje?.OrderByDescending(x => x.LoggedAt).FirstOrDefault();

                        if (ultimaRefeicao != null)
                        {
                            await _whatsAppSender.SendTextMessageAsync(senderPhone, "🛠️ A corrigir a sua última refeição...");

                            textoFinalParaIa = $"[MODO CORREÇÃO] O utilizador quer ajustar a sua última refeição. " +
                                               $"Refeição original guardada: '{ultimaRefeicao.DishName ?? ultimaRefeicao.MealType}' com {ultimaRefeicao.Calories} kcal. " +
                                               $"Novo comando do utilizador: '{textoBruto}'. " +
                                               $"Instrução: Refaça os cálculos adicionando ou removendo o que o utilizador pediu e registe como uma refeição completa corrigida.";

                            await _nutritionRepository.ExcluirMealLogAsync(ultimaRefeicao.Id);
                        }
                    }
                }

                // 🛡️ GUARDRAIL: FILTRO ANTI-GASTOS DE IA E MENSAGENS GENÉRICAS
                bool eIntencaoRefeicao = EhIntencaoRefeicaoOuComida(textoLimpoCmd, textoBruto ?? "", !string.IsNullOrWhiteSpace(imagemBase64), contextoPendente != null);

                if (!eIntencaoRefeicao)
                {
                    string msgOrientacao = "Não consegui identificar uma refeição no seu texto. 🥗\n\n" +
                                           "Para registrar o que comeu, descreva os alimentos e quantidades (ex: *'150g de frango e 2 colheres de arroz'*), ou envie um *áudio* ou *foto* do seu prato!\n\n" +
                                           "💡 *Outros comandos:* Digite *resumo* para o seu relatório ou *minha dieta* para o plano completo.";
                    await _whatsAppSender.SendTextMessageAsync(senderPhone, msgOrientacao);
                    return Ok();
                }

                if (isTrialLimitedUser && !await ReserveTrialInteractionOrNotifyAsync(senderPhone, trialCollection))
                {
                    return Ok();
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
                    var dataHojeBrFinal = DateTime.UtcNow.AddHours(-3);
                    statusDoDia = await _nutritionService.GetDailyStatusAndSuggestionAsync(senderPhone, dataHojeBrFinal);
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

        #region Helper Methods - Painel / Resumo Visual

        private static string GerarBarraProgresso(decimal atual, decimal meta, int tamanhoBarra = 10)
        {
            if (meta <= 0) meta = 1;
            decimal porcentagem = Math.Min(1.0m, Math.Max(0.0m, atual / meta));
            int preenchidos = (int)Math.Round(porcentagem * tamanhoBarra);
            int vazios = tamanhoBarra - preenchidos;

            var sb = new StringBuilder();
            for (int i = 0; i < preenchidos; i++) sb.Append("🟩");
            for (int i = 0; i < vazios; i++) sb.Append("⬜");

            return $"{sb} {porcentagem * 100:F0}%";
        }

        private async Task<string> ObterResumoFormatadoWhatsappAsync(string phone)
        {
            var hojeBr = DateTime.UtcNow.AddHours(-3);
            var inicioMes = new DateTime(hojeBr.Year, hojeBr.Month, 1);

            var statusHoje = await _nutritionService.GetDailyStatusAndSuggestionAsync(phone, hojeBr);

            int totalAguaHoje = 0;
            try
            {
                var waterCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("WaterLogs");
                var filterWater = Builders<MongoDB.Bson.BsonDocument>.Filter.And(
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("UserId", phone),
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Gte("LoggedAt", hojeBr.Date)
                );
                var registrosAgua = await waterCol.Find(filterWater).ToListAsync();
                totalAguaHoje = registrosAgua.Sum(w => w.Contains("AmountMl") && w["AmountMl"].IsNumeric ? w["AmountMl"].ToInt32() : 0);
            }
            catch { }

            int diasComRegistro = 0;
            int totalCaloriasIngeridasMes = 0;
            int totalCaloriasGastasTreinoMes = 0;
            int mediaCaloriasDiaria = 0;

            try
            {
                var mealCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("MealLogs");
                var filterMes = Builders<MongoDB.Bson.BsonDocument>.Filter.And(
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("UserId", phone),
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Gte("LoggedAt", inicioMes)
                );

                var logsDoMes = await mealCol.Find(filterMes).ToListAsync();

                if (logsDoMes != null && logsDoMes.Any())
                {
                    diasComRegistro = logsDoMes
                        .Where(x => x.Contains("LoggedAt") && x["LoggedAt"].IsBsonDateTime)
                        .Select(x => x["LoggedAt"].ToUniversalTime().AddHours(-3).Date)
                        .Distinct()
                        .Count();

                    foreach (var log in logsDoMes)
                    {
                        // Correção: Usar IsNumeric e ToInt32() para abranger Int32, Int64 ou Double do MongoDB
                        if (log.Contains("Calories") && log["Calories"].IsNumeric)
                        {
                            int cal = log["Calories"].ToInt32();

                            if (cal > 0)
                                totalCaloriasIngeridasMes += cal;
                            else if (cal < 0)
                                totalCaloriasGastasTreinoMes += Math.Abs(cal);
                        }
                    }

                    mediaCaloriasDiaria = diasComRegistro > 0 ? totalCaloriasIngeridasMes / diasComRegistro : 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao consultar dados mensais de nutrição para o telefone {Phone}", phone);
            }

            var sb = new StringBuilder();

            sb.AppendLine("📊 *PAINEL DE DESEMPENHO NUTRICIONAL* 📊\n");
            sb.AppendLine($"🗓️ *RESUMO DE HOJE ({hojeBr:dd/MM})*");
            sb.AppendLine($"🔥 *Calorias:* {statusHoje.Consumed.Calories} / {statusHoje.Target.Calories} kcal");
            sb.AppendLine(GerarBarraProgresso(statusHoje.Consumed.Calories, statusHoje.Target.Calories));

            sb.AppendLine($"🥩 *Proteínas:* {statusHoje.Consumed.ProteinG:F0}g / {statusHoje.Target.ProteinG:F0}g");
            sb.AppendLine(GerarBarraProgresso(statusHoje.Consumed.ProteinG, statusHoje.Target.ProteinG));

            sb.AppendLine($"🍞 *Carboidratos:* {statusHoje.Consumed.CarbsG:F0}g / {statusHoje.Target.CarbsG:F0}g");
            sb.AppendLine(GerarBarraProgresso(statusHoje.Consumed.CarbsG, statusHoje.Target.CarbsG));

            sb.AppendLine($"🥑 *Gorduras:* {statusHoje.Consumed.FatG:F0}g / {statusHoje.Target.FatG:F0}g");
            sb.AppendLine(GerarBarraProgresso(statusHoje.Consumed.FatG, statusHoje.Target.FatG));

            int metaAgua = 2500;
            sb.AppendLine($"💧 *Água:* {totalAguaHoje} / {metaAgua} ml");
            sb.AppendLine(GerarBarraProgresso(totalAguaHoje, metaAgua));

            if (statusHoje.StreakDays > 0)
            {
                sb.AppendLine($"\n🔥 *Sequência:* {statusHoje.StreakDays} dia(s) consecutivos no foco!");
            }

            sb.AppendLine("\n----------------------------------");

            // Força o mês em português
            var culturaBr = new System.Globalization.CultureInfo("pt-BR");
            string nomeMes = hojeBr.ToString("MMMM/yyyy", culturaBr);
            nomeMes = char.ToUpper(nomeMes[0]) + nomeMes.Substring(1);

            sb.AppendLine($"📅 *ACUMULADO DO MÊS ({nomeMes})*");
            sb.AppendLine($"✅ *Dias Ativos:* {diasComRegistro} dia(s) registados");
            sb.AppendLine($"🔥 *Média Diária Ingerida:* {mediaCaloriasDiaria:N0} kcal/dia");
            sb.AppendLine($"🍽️ *Total Ingerido:* {totalCaloriasIngeridasMes:N0} kcal");

            // Lógica para mostrar quanto consumiu a mais ou a menos da meta acumulada
            if (diasComRegistro > 0)
            {
                int metaAcumulada = diasComRegistro * statusHoje.Target.Calories;
                int diferenca = totalCaloriasIngeridasMes - metaAcumulada;

                if (diferenca > 0)
                    sb.AppendLine($"📈 *Balanço:* Passou {diferenca:N0} kcal da meta");
                else if (diferenca < 0)
                    sb.AppendLine($"📉 *Balanço:* Poupou {Math.Abs(diferenca):N0} kcal (Déficit)");
                else
                    sb.AppendLine($"🎯 *Balanço:* Exatamente na meta!");
            }

            if (totalCaloriasGastasTreinoMes > 0)
            {
                sb.AppendLine($"🏃‍♂️ *Total Gasto em Treinos:* {totalCaloriasGastasTreinoMes:N0} kcal");
            }

            sb.AppendLine("\n💡 *Dica:* Digite *minha dieta* para ver o plano em página externa ou *remover* para ajustar uma refeição de hoje.");

            return sb.ToString();
        }

        #endregion

        private static bool EhIntencaoRefeicaoOuComida(string textoLimpo, string textoOriginal, bool temImagem, bool temContextoPendente)
        {
            if (temImagem || temContextoPendente) return true;
            if (string.IsNullOrWhiteSpace(textoLimpo)) return false;

            var palavrasChaveRefeicao = new[]
            {
                "comi", "jantei", "almocei", "tomei", "bebi", "lanchei", "saboreei", "consumi", "prato",
                "arroz", "feijao", "feijão", "frango", "carne", "ovo", "ovos", "pao", "pão", "salada",
                "massa", "macarrao", "macarrão", "peixe", "batata", "queijo", "leite", "iogurte", "fruta",
                "maca", "maçã", "banana", "whey", "suco", "cafe", "café", "tapioca", "aveia",
                "colher", "colheres", "fatia", "fatias", "porcao", "porção", "xicara", "xícara", "copo",
                "gramas", "gr", "g", "ml", "kcal", "calorias", "marmita", "buffet", "restaurante"
            };

            if (palavrasChaveRefeicao.Any(p => textoLimpo.Contains(p))) return true;

            if (Regex.IsMatch(textoOriginal, @"\d+\s*[a-zA-ZáéíóúâêîôûãõçÁÉÍÓÚÂÊÎÔÛÃÕÇ]+")) return true;

            return false;
        }

        [HttpPost("send-daily-reminders")]
        public async Task<IActionResult> SendDailyReminders([FromHeader(Name = "X-Cron-Secret")] string secret)
        {
            var expectedSecret = _configuration["CronSecret"];
            if (string.IsNullOrEmpty(secret) || secret != expectedSecret) return Unauthorized(new { success = false, message = "Acesso negado." });

            try
            {
                var dataHojeBr = DateTime.UtcNow.AddHours(-3).ToString("yyyy-MM-dd");
                var horaBrasilia = DateTime.UtcNow.AddHours(-3).Hour;
                string mealTime = "café da manhã";
                if (horaBrasilia >= 11 && horaBrasilia < 16) mealTime = "almoço";
                else if (horaBrasilia >= 16 && horaBrasilia < 24) mealTime = "jantar";

                var logCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("CronExecutionLogs");
                string logId = $"reminder_{mealTime}_{dataHojeBr}";

                var existingLog = await logCollection.Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", logId)).FirstOrDefaultAsync();
                if (existingLog != null) return Ok(new { success = true, message = $"Os lembretes para {mealTime} já foram enviados hoje. Ignorando." });

                await logCollection.InsertOneAsync(new MongoDB.Bson.BsonDocument { { "_id", logId }, { "ExecutedAt", DateTime.UtcNow } });

                var telefones = await _nutritionRepository.ObterTelefonesAtivosAsync();
                var trialCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("B2C_Trial_Users");
                int enviados = 0, falhas = 0;

                foreach (var telefone in telefones)
                {
                    try
                    {
                        var trialDoc = await trialCollection.Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", telefone)).FirstOrDefaultAsync();
                        if (trialDoc != null)
                        {
                            bool isPremium = trialDoc.TryGetValue("Premium", out var pVal) && pVal.IsBoolean && pVal.AsBoolean;
                            if (!isPremium && trialDoc.TryGetValue("TrialStartDate", out var startVal) && startVal.IsBsonDateTime)
                            {
                                var trialStart = startVal.ToUniversalTime();
                                if (DateTime.UtcNow >= trialStart.AddDays(TrialDurationDays)) continue;
                            }
                        }

                        bool sucesso = await _whatsAppSender.SendTemplateMessageAsync(telefone, "lembrete_diario_nutri", "Paciente", mealTime);
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
                var dataHojeBrDt = DateTime.UtcNow.AddHours(-3).Date;
                var dataHojeBrStr = dataHojeBrDt.ToString("yyyy-MM-dd");

                var logCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("CronExecutionLogs");
                string logId = $"feedback_{dataHojeBrStr}";

                var existingLog = await logCollection.Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", logId)).FirstOrDefaultAsync();
                if (existingLog != null)
                {
                    return Ok(new { success = true, message = "Os feedbacks de fim de dia já foram enviados hoje. Ignorando." });
                }

                await logCollection.InsertOneAsync(new MongoDB.Bson.BsonDocument { { "_id", logId }, { "ExecutedAt", DateTime.UtcNow } });

                var telefones = await _nutritionRepository.ObterTelefonesAtivosAsync();
                var trialCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("B2C_Trial_Users");
                int enviados = 0, falhas = 0;

                foreach (var phone in telefones)
                {
                    try
                    {
                        var trialDoc = await trialCollection.Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", phone)).FirstOrDefaultAsync();
                        if (trialDoc != null)
                        {
                            bool isPremium = trialDoc.TryGetValue("Premium", out var pVal) && pVal.IsBoolean && pVal.AsBoolean;
                            if (!isPremium && trialDoc.TryGetValue("TrialStartDate", out var startVal) && startVal.IsBsonDateTime)
                            {
                                var trialStart = startVal.ToUniversalTime();
                                if (DateTime.UtcNow >= trialStart.AddDays(TrialDurationDays))
                                {
                                    continue;
                                }
                            }
                        }

                        var goal = await _nutritionRepository.ObterMetaDiariaAsync(phone, dataHojeBrDt);
                        var logs = await _nutritionRepository.ObterRefeicoesDoDiaAsync(phone, dataHojeBrDt);

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

        [HttpPost("send-expired-offers")]
        public async Task<IActionResult> SendExpiredOffers([FromHeader(Name = "X-Cron-Secret")] string secret)
        {
            var expectedSecret = _configuration["CronSecret"];
            if (string.IsNullOrEmpty(secret) || secret != expectedSecret) return Unauthorized(new { success = false, message = "Acesso negado." });

            try
            {
                // --- 1. INÍCIO DO CONTROLE DE LOG DE EXECUÇÃO ---
                var dataHojeBrDt = DateTime.UtcNow.AddHours(-3).Date;
                var dataHojeBrStr = dataHojeBrDt.ToString("yyyy-MM-dd");

                var logCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("CronExecutionLogs");
                string logId = $"expired_offers_{dataHojeBrStr}";

                var existingLog = await logCollection.Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", logId)).FirstOrDefaultAsync();
                if (existingLog != null)
                {
                    return Ok(new { success = true, message = "As ofertas de expiração já foram enviadas hoje. Ignorando." });
                }

                // Insere o log confirmando que o job rodou hoje
                await logCollection.InsertOneAsync(new MongoDB.Bson.BsonDocument { { "_id", logId }, { "ExecutedAt", DateTime.UtcNow } });
                // --- FIM DO CONTROLE DE LOG DE EXECUÇÃO ---

                var trialCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("B2C_Trial_Users");
                var utcNow = DateTime.UtcNow;
                var limitDate = utcNow.AddDays(-TrialDurationDays);

                var filter = Builders<MongoDB.Bson.BsonDocument>.Filter.And(
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Lte("TrialStartDate", limitDate),
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Ne("Premium", true),
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Ne("ExpiredOfferSent", true)
                );

                var expiredUsers = await trialCollection.Find(filter).ToListAsync();

                // Log para ver se o filtro encontrou alguém na base
                _logger.LogInformation("[SendExpiredOffers] Encontrados {Count} utilizadores elegíveis para oferta de expiração hoje.", expiredUsers.Count);

                int enviados = 0, falhas = 0;

                foreach (var user in expiredUsers)
                {
                    if (user.TryGetValue("_id", out var idValue) && idValue.IsString)
                    {
                        string phone = idValue.AsString;
                        try
                        {
                            bool sucesso = await _whatsAppSender.SendTemplateMessageAsync(phone, "oferta_fim_trial");

                            if (sucesso)
                            {
                                enviados++;
                                var update = Builders<MongoDB.Bson.BsonDocument>.Update.Set("ExpiredOfferSent", true);
                                await trialCollection.UpdateOneAsync(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", phone), update);
                            }
                            else
                            {
                                falhas++;
                                _logger.LogWarning("[SendExpiredOffers] A API do WhatsApp retornou falha ao enviar para o número {Phone}. Verifique as variáveis do template.", phone);
                            }
                        }
                        catch (Exception exInner)
                        {
                            falhas++;
                            _logger.LogError(exInner, "[SendExpiredOffers] Exceção ao tentar enviar template para {Phone}", phone);
                        }
                    }
                }
                return Ok(new { success = true, message = $"Ofertas de recuperação enviadas via template: {enviados}, Falhas: {falhas}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro crítico no envio de ofertas de expiração.");
                return StatusCode(500, new { success = false, message = "Erro ao processar envios." });
            }
        }

        [HttpPost("send-reminder")]
        public async Task<IActionResult> SendReminder([FromQuery] string phone, [FromQuery] string userName = "Anderson", [FromQuery] string mealTime = "café da manhã")
        {
            if (string.IsNullOrWhiteSpace(phone)) return BadRequest(new { success = false, message = "Telefone obrigatório." });
            bool enviado = await _whatsAppSender.SendTemplateMessageAsync(phone, "lembrete_diario_nutri", userName, mealTime);
            if (enviado) return Ok(new { success = true, message = $"Lembrete enviado para {phone}!" });
            return StatusCode(500, new { success = false, message = "Falha ao enviar lembrete." });
        }

        [HttpPost("send-trial-reminders")]
        public async Task<IActionResult> SendTrialReminders([FromHeader(Name = "X-Cron-Secret")] string secret)
        {
            var expectedSecret = _configuration["CronSecret"];
            if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(expectedSecret) || secret != expectedSecret)
                return Unauthorized(new { success = false, message = "Acesso negado." });

            try
            {
                var trialCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("B2C_Trial_Users");
                var utcNow = DateTime.UtcNow;
                var todayBrazil = GetBrazilDate(utcNow);
                var earliestStart = utcNow.AddDays(-TrialDurationDays);
                var filter = Builders<MongoDB.Bson.BsonDocument>.Filter.And(
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Gte("TrialStartDate", earliestStart),
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Lte("TrialStartDate", utcNow));

                var trialUsers = await trialCollection.Find(filter).ToListAsync();
                int sentCount = 0, skippedNotCampaignDay = 0, skippedAlreadyInteracted = 0, skippedAlreadySent = 0, errors = 0;

                foreach (var user in trialUsers)
                {
                    if (!user.TryGetValue("_id", out var idValue) || !idValue.IsString ||
                        !user.TryGetValue("TrialStartDate", out var startValue) || !startValue.IsBsonDateTime) continue;

                    string phone = idValue.AsString;
                    bool premium = user.TryGetValue("Premium", out var premiumValue) && premiumValue.IsBoolean && premiumValue.AsBoolean;
                    if (premium) continue;

                    var trialStart = startValue.ToUniversalTime();
                    var elapsedDays = (utcNow - trialStart).TotalDays;
                    if (elapsedDays < 0 || elapsedDays >= TrialDurationDays) continue;

                    int trialDay = (int)Math.Floor(elapsedDays) + 1;
                    if (!TrialReminderDays.Contains(trialDay)) { skippedNotCampaignDay++; continue; }

                    DateTime lastInteractionDate = DateTime.MinValue;
                    if (user.TryGetValue("LastInteractionDate", out var interactionValue) && interactionValue.IsBsonDateTime)
                        lastInteractionDate = interactionValue.ToUniversalTime().Date;

                    if (lastInteractionDate == todayBrazil.Date) { skippedAlreadyInteracted++; continue; }

                    var claimFilter = Builders<MongoDB.Bson.BsonDocument>.Filter.And(
                        Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", phone),
                        Builders<MongoDB.Bson.BsonDocument>.Filter.Ne("TrialReminderDaysSent", trialDay));
                    var claimUpdate = Builders<MongoDB.Bson.BsonDocument>.Update.AddToSet("TrialReminderDaysSent", trialDay);
                    var claim = await trialCollection.UpdateOneAsync(claimFilter, claimUpdate);
                    if (claim.ModifiedCount == 0) { skippedAlreadySent++; continue; }

                    try
                    {
                        bool success = await _whatsAppSender.SendTemplateMessageAsync(phone, "trial_engajamento", "Cliente");

                        if (success) { sentCount++; _logger.LogInformation("Lembrete do trial enviado no dia {TrialDay}.", trialDay); }
                        else
                        {
                            await trialCollection.UpdateOneAsync(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", phone), Builders<MongoDB.Bson.BsonDocument>.Update.Pull("TrialReminderDaysSent", trialDay));
                            errors++;
                        }
                    }
                    catch (Exception ex)
                    {
                        await trialCollection.UpdateOneAsync(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", phone), Builders<MongoDB.Bson.BsonDocument>.Update.Pull("TrialReminderDaysSent", trialDay));
                        _logger.LogError(ex, "Erro ao enviar lembrete de trial no dia {TrialDay}.", trialDay);
                        errors++;
                    }
                }

                return Ok(new { success = true, message = $"Campanha concluída. Enviados: {sentCount}; já interagiram hoje: {skippedAlreadyInteracted}; já enviados: {skippedAlreadySent}; fora dos dias da campanha: {skippedNotCampaignDay}; erros: {errors}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro crítico no endpoint de lembretes de trial.");
                return StatusCode(500, new { success = false, message = "Erro interno ao processar lembretes de trial." });
            }
        }

        private async Task<bool> ReserveTrialInteractionOrNotifyAsync(
            string phone,
            IMongoCollection<MongoDB.Bson.BsonDocument> trialCollection)
        {
            bool reserved = await TryReserveDailyTrialInteractionAsync(
                trialCollection, phone, DateTime.UtcNow);
            if (reserved) return true;

            _logger.LogInformation("[WhatsApp B2C] Limite diário do trial atingido para interação de IA.");
            string? paymentUrlOriginal = await GerarLinkPagamentoInfinitePayAsync(phone, "Cliente Limite", 29.90m);
            string? trackingLink = null;

            if (!string.IsNullOrEmpty(paymentUrlOriginal))
            {
                var trackingCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("PaymentTracking");
                var trackingDoc = new MongoDB.Bson.BsonDocument
                {
                    { "_id", phone },
                    { "InfinitePayUrl", paymentUrlOriginal },
                    { "SentAt", DateTime.UtcNow },
                    { "ClickedAt", MongoDB.Bson.BsonNull.Value },
                    { "Status", "Sent" }
                };
                await trackingCol.ReplaceOneAsync(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", phone), trackingDoc, new ReplaceOptions { IsUpsert = true });

                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                trackingLink = $"{baseUrl}/api/whatsapp/ir-para-pagamento?phone={phone}";
            }

            string message = "⚠️ *Você chegou ao limite de 4 análises de IA do teste de hoje.*\n\n" +
                             "Mensagens simples e registros básicos não contam para esse limite. Amanhã você poderá continuar.\n\n" +
                             (!string.IsNullOrEmpty(trackingLink)
                                 ? $"Se quiser conhecer a assinatura mensal: 👉 {trackingLink}"
                                 : "Se quiser saber sobre a assinatura mensal, responda *assinar*.");
            await _whatsAppSender.SendTextMessageAsync(phone, message);
            return false;
        }

        private static DateTime GetBrazilDate(DateTime utcNow)
        {
            return DateTime.SpecifyKind(utcNow.AddHours(-3).Date, DateTimeKind.Utc);
        }

        private static async Task<bool> TryReserveDailyTrialInteractionAsync(
            IMongoCollection<MongoDB.Bson.BsonDocument> collection,
            string userId,
            DateTime utcNow)
        {
            var today = GetBrazilDate(utcNow);
            var todayBson = new MongoDB.Bson.BsonDateTime(today);

            for (var attempt = 0; attempt < 5; attempt++)
            {
                var document = await collection.Find(
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", userId)).FirstOrDefaultAsync();
                if (document == null) return false;

                var hasDate = document.TryGetValue("LastInteractionDate", out var oldDateValue) && !oldDateValue.IsBsonNull;
                var oldDate = hasDate && oldDateValue.IsBsonDateTime
                    ? oldDateValue.ToUniversalTime().Date
                    : DateTime.MinValue;
                var isSameDay = oldDate == today.Date;

                int oldCount = 0;
                var hasCount = document.TryGetValue("DailyMessageCount", out var oldCountValue) && !oldCountValue.IsBsonNull;
                if (hasCount && oldCountValue.IsNumeric)
                {
                    oldCount = oldCountValue.ToInt32();
                }

                var currentCount = isSameDay ? oldCount : 0;
                if (currentCount >= DailyTrialInteractionLimit) return false;

                var compareFilter = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", userId);
                compareFilter &= hasDate
                    ? Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("LastInteractionDate", oldDateValue)
                    : Builders<MongoDB.Bson.BsonDocument>.Filter.Exists("LastInteractionDate", false);
                compareFilter &= hasCount
                    ? Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("DailyMessageCount", oldCountValue)
                    : Builders<MongoDB.Bson.BsonDocument>.Filter.Exists("DailyMessageCount", false);

                var update = Builders<MongoDB.Bson.BsonDocument>.Update
                    .Set("LastInteractionDate", todayBson)
                    .Set("DailyMessageCount", currentCount + 1);
                var result = await collection.UpdateOneAsync(compareFilter, update);
                if (result.ModifiedCount == 1) return true;
            }

            return false;
        }

        private async Task<string?> GerarLinkPagamentoInfinitePayAsync(string phone, string name, decimal valorOriginal)
        {
            try
            {
                var apiKey = _configuration["InfinitePay:ApiKey"]?.Trim();
                var handle = _configuration["InfinitePay:Handle"]?.Trim();

                if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(handle))
                {
                    _logger.LogError("[INFINITEPAY] Chave da API ou Handle não estão configurados no appsettings.json.");
                    return null;
                }

                var requestUrl = "https://api.checkout.infinitepay.io/links";
                int amountInCents = (int)(valorOriginal * 100);

                var linkRequest = new
                {
                    handle = handle,
                    order_nsu = Guid.NewGuid().ToString(),
                    items = new[]
                    {
                        new
                        {
                            quantity = 1,
                            price = amountInCents,
                            description = "Nutrição Fácil - Assinatura Mensal"
                        }
                    },
                    customer = new
                    {
                        name = name,
                        phone_number = phone
                    },
                    webhook = new
                    {
                        url = "https://labelwise-1.onrender.com/api/whatsapp/infinitepay-webhook",
                        events = new[] { "paid" }
                    }
                };

                var client = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Content = JsonContent.Create(linkRequest);

                var response = await client.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("[INFINITEPAY LINK ERROR] Status: {StatusCode} | Body: {Error}", response.StatusCode, responseContent);
                    return null;
                }

                using var doc = JsonDocument.Parse(responseContent);

                if (doc.RootElement.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == JsonValueKind.String)
                {
                    return urlProp.GetString();
                }

                if (doc.RootElement.TryGetProperty("data", out var dataProp) && dataProp.TryGetProperty("url", out var dataUrlProp))
                {
                    return dataUrlProp.GetString();
                }

                _logger.LogError("[INFINITEPAY PARSE ERROR] Link gerado, mas 'url' não encontrada no Body: {Body}", responseContent);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exceção ao gerar link de pagamento na InfinitePay.");
                return null;
            }
        }

        [HttpPost("infinitepay-webhook")]
        public async Task<IActionResult> InfinitePayWebhook([FromBody] JsonElement payload)
        {
            try
            {
                string jsonString = payload.GetRawText();
                _logger.LogInformation("[INFINITEPAY WEBHOOK] Recebido: {Payload}", jsonString);

                string? status = null;
                string? phone = null;

                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;

                if (root.TryGetProperty("status", out var statusProp))
                {
                    status = statusProp.GetString()?.ToLower();
                }

                if (root.TryGetProperty("customer", out var customerProp) && customerProp.TryGetProperty("phone_number", out var phoneProp))
                {
                    phone = phoneProp.GetString();
                }

                if ((status == "approved" || status == "paid") && !string.IsNullOrEmpty(phone))
                {
                    string cleanPhone = Regex.Replace(phone, "[^0-9]", "");

                    // 1. Atualizar a base de dados de Trial para Premium
                    var trialCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("B2C_Trial_Users");
                    var filter = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", cleanPhone);
                    var update = Builders<MongoDB.Bson.BsonDocument>.Update.Set("Premium", true);

                    await trialCollection.UpdateOneAsync(filter, update);

                    // 2. Atualizar também a collection de rastreio de pagamentos para "Paid"
                    var trackingCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("PaymentTracking");
                    var trackingFilter = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", cleanPhone);
                    var trackingUpdate = Builders<MongoDB.Bson.BsonDocument>.Update
                        .Set("Status", "Paid")
                        .Set("PaidAt", DateTime.UtcNow);

                    await trackingCol.UpdateOneAsync(trackingFilter, trackingUpdate, new UpdateOptions { IsUpsert = true });

                    _logger.LogInformation("✅ Utilizador {Phone} ativado como Premium e registado como pago com sucesso!", cleanPhone);

                    // 3. Enviar mensagem de boas-vindas ao Premium
                    string msgSucesso = "🎉 *Pagamento Confirmado!* \n\nA sua assinatura Premium do Nutrição Fácil está ativa. Já pode continuar a registar as suas refeições sem quaisquer limites! 🥗🚀";
                    await _whatsAppSender.SendTextMessageAsync(cleanPhone, msgSucesso);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro crítico ao processar o webhook da InfinitePay.");
                return Ok();
            }
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
​2. Seja encorajador se ele esteve perto da meta, ou acolhedor se ele ultrapassou/esqueceu de registrar.
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
            try
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
                if (!response.IsSuccessStatusCode) return string.Empty;

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao transcrever áudio com Gemini.");
                return string.Empty;
            }
        }

        private async Task<int> EstimarGastoCaloricoComGeminiAsync(string textoAtividade)
        {
            try
            {
                var apiKey = _configuration["GeminiApiKey"] ?? _configuration["Gemini:ApiKey"];
                var endpoint = _configuration["Gemini:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
                var model = _configuration["Model"] ?? "gemini-1.5-flash";

                var prompt = $"O utilizador relatou a seguinte atividade física: '{textoAtividade}'. " +
                             $"Estime o gasto calórico médio dessa atividade em kcal para um adulto médio. " +
                             $"Responda APENAS com o número inteiro correspondente às calorias gastas. " +
                             $"Se não for uma atividade física reconhecível, responda 0.";

                var client = _httpClientFactory.CreateClient();
                var requestBody = new
                {
                    model = model,
                    temperature = 0.1,
                    messages = new[] { new { role = "user", content = prompt } }
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var response = await client.SendAsync(requestMessage);
                if (!response.IsSuccessStatusCode) return 0;

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                var rawText = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "0";

                var match = Regex.Match(rawText, @"\d+");
                if (match.Success && int.TryParse(match.Value, out int kcal))
                {
                    return kcal;
                }
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao estimar gasto calórico.");
                return 0;
            }
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

        [HttpPost("broadcast-novidade")]
        public async Task<IActionResult> DispararNovidade([FromHeader(Name = "X-Admin-Secret")] string secret)
        {
            var expectedSecret = _configuration["CronSecret"] ?? "senha_secreta_123";
            if (string.IsNullOrEmpty(secret) || secret != expectedSecret) return Unauthorized(new { success = false, message = "Acesso negado." });

            try
            {
                var trialCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("B2C_Trial_Users");
                var users = await trialCollection.Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Empty).ToListAsync();

                int enviados = 0;
                int falhas = 0;

                foreach (var user in users)
                {
                    if (user.TryGetValue("_id", out var idValue) && idValue.IsString)
                    {
                        string phone = idValue.AsString;
                        try
                        {
                            bool sucesso = await _whatsAppSender.SendTemplateMessageAsync(phone, "alerta_novidade_app");
                            if (sucesso) enviados++; else falhas++;
                        }
                        catch { falhas++; }
                    }
                }

                return Ok(new { success = true, message = $"Disparo concluído. Enviados: {enviados}, Falhas: {falhas}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no disparo da novidade.");
                return StatusCode(500, new { success = false, message = "Erro ao processar o disparo." });
            }
        }

        [HttpPost("send-water-reminders")]
        public async Task<IActionResult> SendWaterReminders([FromHeader(Name = "X-Cron-Secret")] string secret)
        {
            var expectedSecret = _configuration["CronSecret"];
            if (string.IsNullOrEmpty(secret) || secret != expectedSecret) return Unauthorized(new { success = false, message = "Acesso negado." });

            try
            {
                var nowBr = DateTime.UtcNow.AddHours(-3);
                var horaBrasilia = nowBr.Hour;

                // 🛑 Trava de segurança: Não enviar de madrugada/noite (só envia entre as 09:00 e as 20:59)
                if (horaBrasilia < 9 || horaBrasilia >= 21)
                {
                    return Ok(new { success = true, message = "Fora do horário permitido para envio de lembretes de água." });
                }

                // ⏱️ Regista a execução pela HORA para permitir múltiplos envios diários, mas sem duplicar na mesma hora
                var logCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("CronExecutionLogs");
                string logId = $"water_reminder_{nowBr:yyyy-MM-dd}_{horaBrasilia}";

                var existingLog = await logCollection.Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", logId)).FirstOrDefaultAsync();
                if (existingLog != null) return Ok(new { success = true, message = $"O lembrete de água das {horaBrasilia}h já foi enviado hoje. Ignorando." });

                await logCollection.InsertOneAsync(new MongoDB.Bson.BsonDocument { { "_id", logId }, { "ExecutedAt", DateTime.UtcNow } });

                var telefones = await _nutritionRepository.ObterTelefonesAtivosAsync();
                var trialCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("B2C_Trial_Users");
                var waterCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("WaterLogs");
                var goalsCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("DailyGoals");

                int enviados = 0, falhas = 0, ignoradosMetaBatida = 0;

                foreach (var telefone in telefones)
                {
                    try
                    {
                        // Validação do ciclo de vida do utilizador (Trial / Premium)
                        var trialDoc = await trialCollection.Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", telefone)).FirstOrDefaultAsync();
                        if (trialDoc != null)
                        {
                            bool isPremium = trialDoc.TryGetValue("Premium", out var pVal) && pVal.IsBoolean && pVal.AsBoolean;
                            if (!isPremium && trialDoc.TryGetValue("TrialStartDate", out var startVal) && startVal.IsBsonDateTime)
                            {
                                var trialStart = startVal.ToUniversalTime();
                                if (DateTime.UtcNow >= trialStart.AddDays(TrialDurationDays)) continue;
                            }
                        }

                        // 1. Busca a Meta de Água (Padrão 2500ml se não existir)
                        int metaAgua = 2500;
                        var goalFilter = Builders<MongoDB.Bson.BsonDocument>.Filter.And(
                            Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("UserId", telefone),
                            Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("TargetDate", nowBr.Date)
                        );
                        var goalDoc = await goalsCollection.Find(goalFilter).FirstOrDefaultAsync();
                        if (goalDoc != null && goalDoc.Contains("TargetWaterMl"))
                        {
                            metaAgua = goalDoc["TargetWaterMl"].AsInt32;
                        }

                        // 2. Calcula Água Consumida Hoje
                        var waterFilter = Builders<MongoDB.Bson.BsonDocument>.Filter.And(
                            Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("UserId", telefone),
                            Builders<MongoDB.Bson.BsonDocument>.Filter.Gte("LoggedAt", nowBr.Date)
                        );
                        var registrosAgua = await waterCol.Find(waterFilter).ToListAsync();
                        int totalAguaHoje = registrosAgua.Sum(w => w.Contains("AmountMl") && w["AmountMl"].IsInt32 ? w["AmountMl"].AsInt32 : 0);

                        // 3. Inteligência do alerta: Só envia se ainda NÃO bateu a meta
                        if (totalAguaHoje >= metaAgua)
                        {
                            ignoradosMetaBatida++;
                            continue;
                        }

                        int faltaMl = metaAgua - totalAguaHoje;

                        // 4. Envia a notificação (Template do WhatsApp)
                        bool sucesso = await _whatsAppSender.SendTemplateMessageAsync(telefone, "lembrete_agua", faltaMl.ToString());

                        if (sucesso) enviados++; else falhas++;
                    }
                    catch
                    {
                        falhas++;
                    }
                }

                return Ok(new { success = true, message = $"Lembrete das {horaBrasilia}h concluído. Enviados: {enviados}, Meta já batida: {ignoradosMetaBatida}, Falhas: {falhas}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro interno ao processar os lembretes de água.");
                return StatusCode(500, new { success = false, message = "Erro interno ao processar." });
            }
        }

        public class MetaWebhookPayload { public List<MetaEntry>? Entry { get; set; } }
        public class MetaEntry { public List<MetaChange>? Changes { get; set; } }
        public class MetaChange { public MetaValue? Value { get; set; } }
        public class MetaValue { public List<MetaMessage>? Messages { get; set; } }

        public class MetaMessage
        {
            public string? From { get; set; }
            public string? Type { get; set; }
            public MetaText? Text { get; set; }
            public MetaMedia? Image { get; set; }
            public MetaMedia? Audio { get; set; }

            public MetaButton? Button { get; set; }
            public MetaInteractive? Interactive { get; set; }
        }
        public class MetaText { public string? Body { get; set; } }
        public class MetaMedia { public string? Id { get; set; } public string? MimeType { get; set; } }

        public class MetaButton { public string? Text { get; set; } }
        public class MetaInteractive
        {
            public string? Type { get; set; }
            [JsonPropertyName("button_reply")]
            public MetaInteractiveReply? ButtonReply { get; set; }
            [JsonPropertyName("list_reply")]
            public MetaInteractiveReply? ListReply { get; set; }
        }
        public class MetaInteractiveReply { public string? Id { get; set; } public string? Title { get; set; } }

        public class InfinitePayLinkResponse
        {
            public string id { get; set; }
            public string url { get; set; }
        }
    }
}