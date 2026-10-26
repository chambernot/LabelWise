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
using System.Security.Cryptography;
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

                // 1. Registra o clique na collection
                var update = Builders<MongoDB.Bson.BsonDocument>.Update
                    .Set("ClickedAt", DateTime.UtcNow)
                    .Set("Status", "Clicked");

                await trackingCol.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });

                // 2. Busca o link real da InfinitePay salvo para este usuário
                var doc = await trackingCol.Find(filter).FirstOrDefaultAsync();
                string? infinitePayUrl = doc != null && doc.Contains("InfinitePayUrl") ? doc["InfinitePayUrl"].AsString : null;

                if (!string.IsNullOrEmpty(infinitePayUrl))
                {
                    // 3. Redireciona o usuário para o checkout oficial
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
                // Avisos de entrega das mensagens que enviamos (sent/delivered/read/failed)
                await RegistrarStatusDeEntregaAsync(payload);

                var messagingEvent = payload?.Entry?.FirstOrDefault()
                    ?.Changes?.FirstOrDefault()
                    ?.Value?.Messages?.FirstOrDefault();

                senderPhone = messagingEvent?.From;
                var messageType = messagingEvent?.Type;

                if (string.IsNullOrWhiteSpace(senderPhone) || string.IsNullOrWhiteSpace(messageType))
                {
                    return Ok();
                }

                // Painel gerencial: registra a interação sem nunca interromper o atendimento
                try
                {
                    await UserActivityTracker.RegistrarMensagemRecebidaAsync(_database, senderPhone, messageType);
                }
                catch (Exception exAtividade)
                {
                    _logger.LogWarning(exAtividade, "Não foi possível registrar a atividade de {Phone}.", senderPhone);
                }

                // Classificação do usuário antes de processar mídia, para não gastar IA com quem já está bloqueado
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
                // Parceiro: liberado pelo dono no painel gerencial (Premium sem pagamento)
                bool isPartner = userDoc != null &&
                                 userDoc.TryGetValue("Partner", out var partnerValue) &&
                                 partnerValue.IsBoolean && partnerValue.AsBoolean;
                bool isTrialLimitedUser = !isB2bPatient && !isPremium;

                string? textoBruto = null;
                string? imagemBase64 = null;

                bool eMidia = (messageType == "audio" && messagingEvent?.Audio?.Id != null) ||
                              (messageType == "image" && messagingEvent?.Image?.Id != null);

                if (eMidia && isTrialLimitedUser && userDoc != null)
                {
                    if (TrialExpirado(userDoc, DateTime.UtcNow))
                    {
                        await EnviarMensagemTrialExpiradoAsync(senderPhone);
                        return Ok();
                    }

                    if (LimiteDiarioTrialAtingido(userDoc, DateTime.UtcNow))
                    {
                        await EnviarMensagemLimiteDiarioAsync(senderPhone);
                        return Ok();
                    }
                }

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
                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "🎙 Ouvindo seu áudio e transcrevendo...");
                    var audioBytes = await _metaMediaService.DownloadMediaAsBytesAsync(messagingEvent.Audio.Id);
                    textoBruto = await TranscreverAudioComGeminiAsync(audioBytes);

                    if (string.IsNullOrWhiteSpace(textoBruto))
                    {
                        await _whatsAppSender.SendTextMessageAsync(senderPhone, "⚠️ Não consegui entender o áudio. Por favor, tente enviar novamente ou digite sua mensagem.");
                        return Ok();
                    }
                }
                else if (messageType == "image" && messagingEvent?.Image?.Id != null)
                {
                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "📸 Analisando a imagem, só um instante...");
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
                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "⏳ Calculando seu resumo diário e mensal...");
                    string painelTexto = await ObterResumoFormatadoWhatsappAsync(senderPhone);
                    await _whatsAppSender.SendTextMessageAsync(senderPhone, painelTexto);
                    await _nutritionRepository.SalvarMensagemHistoricoAsync(senderPhone, "assistant", painelTexto);
                    return Ok();
                }

                // 🚀 INTERCETOR DE NOVIDADES ATUALIZADO
                if (textoLimpoCmd == "quero ver a novidade")
                {
                    string novidadeTexto = "🔥 *Novidades Incríveis no Nutrição Fácil!* 🚀\n\n" +
                                           "Implementamos várias funcionalidades de ponta para facilitar sua rotina:\n\n" +
                                           "1️⃣ **Substituição e Troca Inteligente:** Quer trocar algum alimento? Basta enviar, por exemplo: *'Substituir frango por patinho'* e eu encontro a melhor alternativa respeitando seu plano!\n\n" +
                                           "2️⃣ **Modo Correção de Refeições:** Esqueceu algo no prato? Diga, por exemplo: *'Esqueci de registrar o azeite'* e eu ajusto sua última refeição na hora!\n\n" +
                                           "3️⃣ **Painel Visual com Barras (🟩⬜):** Digite *'resumo'* a qualquer momento para ver seu painel diário e o acumulado mensal com gráficos visuais de macros e água!\n\n" +
                                           "4️⃣ **Feedbacks de Fim de Dia:** Agora você conta com uma rotina inteligente que te envia um resumo motivacional personalizado no fim do dia para celebrar seu progresso! 🥗💪\n\n" +
                                           "Vá em frente, teste enviar um destes comandos agora mesmo!";

                    await _whatsAppSender.SendTextMessageAsync(senderPhone, novidadeTexto);
                    await _nutritionRepository.SalvarMensagemHistoricoAsync(senderPhone, "assistant", novidadeTexto);
                    return Ok();
                }

                if (textoLimpoCmd == "agora não" || textoLimpoCmd == "agora nao")
                {
                    string msgOk = "Sem problemas! Estarei por aqui quando precisar registrar suas refeições ou treinos. 🥗💪";
                    await _whatsAppSender.SendTextMessageAsync(senderPhone, msgOk);
                    return Ok();
                }

                if (textoLimpoCmd == "assinar" || textoLimpoCmd == "pagar" || textoLimpoCmd == "plano" || textoLimpoCmd == "assinatura")
                {
                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "⏳ Gerando seu link seguro de pagamento na InfinitePay...");
                    string? trackingLink = await CriarLinkPagamentoRastreadoAsync(senderPhone, "Cliente Nutrição Fácil");

                    if (!string.IsNullOrEmpty(trackingLink))
                    {
                        string msgAssinatura = "💳 *Assinatura Mensal - Nutrição Fácil*\n\n" +
                                               "Para continuar usando o assistente de nutrição sem limites, clique no link seguro abaixo (R$ 29,90):\n\n" +
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
                    string msgAguarde = "⏳ *Aguardando confirmação do sistema...*\n\n" +
                                        "Se você já fez o pagamento, não se preocupe! O processamento da InfinitePay pode levar até 5 minutos (ou um pouco mais se for no cartão de crédito).\n\n" +
                                        "Assim que o banco enviar a confirmação automática, seu acesso Premium será liberado na hora e eu te aviso por aqui! 🎉\n\n" +
                                        "_(Se já passou muito tempo, aguarde: alguém da nossa equipe de suporte vai analisar esta conversa)_";

                    await _whatsAppSender.SendTextMessageAsync(senderPhone, msgAguarde);
                    await _nutritionRepository.SalvarMensagemHistoricoAsync(senderPhone, "assistant", msgAguarde);
                    return Ok();
                }

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

                // Parceiros também passam por aqui para configurar o perfil (onboarding e comando "meta"),
                // mas sem expiração de teste. O Premium pago continua fora deste bloco, como antes.
                if (!isB2bPatient && (!isPremium || isPartner))
                {
                    var now = DateTime.UtcNow;

                    // Parceiro cadastrado pelo painel antes de falar com o bot: boas-vindas próprias, já no passo 1
                    if (isPartner && userDoc!.TryGetValue("PartnerPendingWelcome", out var pendingWelcome) &&
                        pendingWelcome.IsBoolean && pendingWelcome.AsBoolean)
                    {
                        await trialCollection.UpdateOneAsync(filterTrial, Builders<MongoDB.Bson.BsonDocument>.Update.Unset("PartnerPendingWelcome"));

                        string mensagemParceiro = "🤝 *Bem-vindo ao Nutrição Fácil!* 🥗\n\n" +
                                                  "Seu acesso de *parceiro* já está liberado, completo e sem limites.\n\n" +
                                                  "Vamos configurar seu perfil com um formulário rápido e guiado passo a passo.\n\n" +
                                                  "🎯 **Passo 1 de 4:** Qual é o seu principal objetivo?\n" +
                                                  "*1️⃣* Emagrecimento\n" +
                                                  "*2️⃣* Hipertrofia\n" +
                                                  "*3️⃣* Manutenção\n\n" +
                                                  "_(Responda apenas com o número correspondente)_";

                        await _whatsAppSender.SendTextMessageAsync(senderPhone, mensagemParceiro);
                        return Ok();
                    }

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

                        string mensagemBoasVindas = "🎉 *Bem-vindo ao Nutrição Fácil (Versão de Teste - 15 dias)!* 🥗\n\n" +
                                                    "Vamos configurar seu perfil com um formulário rápido e guiado passo a passo.\n\n" +
                                                    "🎯 **Passo 1 de 4:** Qual é o seu principal objetivo?\n" +
                                                    "*1️⃣* Emagrecimento\n" +
                                                    "*2️⃣* Hipertrofia\n" +
                                                    "*3️⃣* Manutenção\n\n" +
                                                    "_(Responda apenas com o número correspondente)_";

                        await _whatsAppSender.SendTextMessageAsync(senderPhone, mensagemBoasVindas);
                        return Ok();
                    }

                    if (!isPremium && TrialExpirado(userDoc, now))
                    {
                        await EnviarMensagemTrialExpiradoAsync(senderPhone);
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

                                await _whatsAppSender.SendTextMessageAsync(senderPhone, "✅ *Configuração cancelada!* \n\nSeu perfil e suas metas anteriores foram mantidos. Você pode continuar registrando suas refeições normalmente! 🥗");
                                return Ok();
                            }
                            else
                            {
                                await _whatsAppSender.SendTextMessageAsync(senderPhone, "⚠️ Como é o seu primeiro acesso, preciso que você termine esta configuração rápida para eu poder calcular suas metas diárias.\n\nPor favor, escolha uma das opções (Ex: digite 1, 2 ou 3).");
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
                                        "🔥 **Passo 2 de 4:** Para eu calcular sua dieta exata (como um nutricionista faria), preciso te conhecer melhor!\n\n" +
                                        "👉 *Me diga em uma única mensagem: seu peso, altura, idade, sexo e rotina de atividade física.*");
                                }
                                else
                                {
                                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "❌ Opção inválida.\n\n🎯 **Passo 1 de 4:** Escolha o número correspondente ao seu principal objetivo:\n*1️⃣* Emagrecimento\n*2️⃣* Hipertrofia\n*3️⃣* Manutenção\n\n_(Ou digite *cancelar* para sair)_");
                                }
                                return Ok();

                            case "WaitingForProfile":
                                await _whatsAppSender.SendTextMessageAsync(senderPhone, "🧠 *Processando seus dados e calculando as metas ideais com IA...* ⏱️");

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
                                                    $"🎯 *Suas Metas Diárias:*\n" +
                                                    $"🔥 Calorias: {calculatedGoals.TargetCalories} kcal\n" +
                                                    $"🥩 Proteína: {calculatedGoals.TargetProteinG:F0}g | 🍞 Carbo: {calculatedGoals.TargetCarbsG:F0}g | 🥑 Gordura: {calculatedGoals.TargetFatG:F0}g\n" +
                                                    $"💧 Água: {calculatedGoals.TargetWaterMl} ml\n\n" +
                                                    $"🛡️ **Passo 3 de 4:** Você tem alguma alergia, intolerância ou restrição médica? _(Digite 'Nenhuma' se não tiver)_";

                                await _whatsAppSender.SendTextMessageAsync(senderPhone, msgCalculo);
                                return Ok();

                            case "WaitingForRestrictions":
                                var restrictions = string.Equals(textoBruto, "nenhuma", StringComparison.OrdinalIgnoreCase) ? "" : textoBruto;

                                var updateRest = Builders<MongoDB.Bson.BsonDocument>.Update
                                    .Set("TempRestrictions", restrictions)
                                    .Set("OnboardingStep", "WaitingForAversions");
                                await trialCollection.UpdateOneAsync(filterTrial, updateRest);

                                await _whatsAppSender.SendTextMessageAsync(senderPhone, "🚫 **Passo 4 de 4:** Você tem alguma aversão ou comida de que não gosta?\n_(Ex: Ovo, cebola, ou digite 'Nenhuma')_");
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
                                                        $"👉 *Tudo pronto! Já pode enviar suas refeições* por texto, foto ou áudio.\n\n" +
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

                        await _whatsAppSender.SendTextMessageAsync(senderPhone, $"🔄 Procurando as melhores alternativas para *{alimentoAlvo}* respeitando seu plano...");

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
                        await _whatsAppSender.SendTextMessageAsync(senderPhone, "⚠️ Não encontrei nenhuma refeição registrada hoje.");
                        return Ok();
                    }

                    var sbLista = new StringBuilder("🗑️ *Suas refeições registradas hoje:*\n\n");
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
                        await _whatsAppSender.SendTextMessageAsync(senderPhone, "⚠️ Você não tem nenhuma refeição registrada hoje para remover.");
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
                        var statusAtualizado = await _nutritionService.GetDailyStatusAsync(senderPhone, dataHojeBr);

                        string respostaRemocao = $"✅ *Refeição removida com sucesso!* \n" +
                                                 $"🗑️ _{refeicaoParaExcluir.DishName ?? refeicaoParaExcluir.MealType}_\n\n" +
                                                 $"📊 *SEU NOVO RESUMO DE HOJE*\n" +
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
                    var tokenAcesso = await PatientAccessTokens.CriarAsync(_database, senderPhone);
                    var magicLink = $"{baseUrl}/paciente.html?token={tokenAcesso}";

                    var respostaLink = "🥗 *Seu Plano Alimentar Interativo está pronto!*\n\n" +
                                       "Toque no link abaixo para ver seu progresso de calorias de hoje, os macronutrientes e seu cardápio completo:\n\n" +
                                       $"👉 {magicLink}\n\n" +
                                       $"_🔒 Este link é pessoal e vale por {PatientAccessTokens.ValidadeDias} dias. Não compartilhe. Quando expirar, é só digitar *minha dieta* de novo._ ✨";

                    await _whatsAppSender.SendTextMessageAsync(senderPhone, respostaLink);
                    return Ok();
                }

                var saudacoesOuConversa = new[] { "oi", "ola", "olá", "tudo bem", "bom dia", "boa tarde", "boa noite", "eae", "hey", "obrigado", "obrigada", "valeu" };
                if (saudacoesOuConversa.Contains(textoLimpoCmd))
                {
                    string respostaSaudacao = "👋 Olá! Sou seu assistente nutricional do Nutrição Fácil.\n\n" +
                                              "Você pode me enviar uma **foto**, um **áudio** ou o **texto** do que comeu para eu registrar a refeição. 🥗\n\n" +
                                              "💡 *Dica:* Diga *resumo* para ver seu painel de hoje/mês, *remover* para ver as refeições ou *minha dieta* para o plano completo!";

                    await _whatsAppSender.SendTextMessageAsync(senderPhone, respostaSaudacao);
                    await _nutritionRepository.SalvarMensagemHistoricoAsync(senderPhone, "assistant", respostaSaudacao);
                    return Ok();
                }

                bool mencionaAgua = (textoBruto ?? "").Contains("agua", StringComparison.OrdinalIgnoreCase) ||
                                    (textoBruto ?? "").Contains("água", StringComparison.OrdinalIgnoreCase) ||
                                    (textoBruto ?? "").Contains("h2o", StringComparison.OrdinalIgnoreCase);

                // Só o volume, sem dizer o que bebeu (ex.: "tomei mais 600 ml"): pergunta em vez de presumir.
                // A resposta é tratada logo abaixo ("água") ou segue para a IA com o contexto pendente (outra bebida).
                if (!mencionaAgua && string.IsNullOrWhiteSpace(imagemBase64) && EhRegistroSoDeVolume(textoBruto ?? ""))
                {
                    int volumeInformado = LerVolumeMl(textoBruto ?? "") ?? 0;
                    string perguntaBebida = $"🥤 Anotei *{volumeInformado} ml*. O que você bebeu?\n\n" +
                                            "Responda *água* para eu somar na sua hidratação, ou diga a bebida " +
                                            "(ex.: *suco de laranja*, *café com leite*, *refrigerante*) para eu calcular as calorias.";

                    await _nutritionRepository.SalvarClarificacaoPendenteAsync(new MealClarificationContext(
                        userId: senderPhone,
                        originalTextInput: textoBruto ?? "",
                        originalBase64Image: null,
                        clarificationQuestion: perguntaBebida));

                    await _whatsAppSender.SendTextMessageAsync(senderPhone, perguntaBebida);
                    await _nutritionRepository.SalvarMensagemHistoricoAsync(senderPhone, "assistant", perguntaBebida);
                    return Ok();
                }

                if (mencionaAgua)
                {
                    int quantidadeMl = 250;
                    int? volumeNaMensagem = LerVolumeMl(textoBruto ?? "");

                    // Resposta "água" à pergunta "O que você bebeu?": usa o volume que o usuário informou antes
                    if (volumeNaMensagem == null)
                    {
                        var pendenteBebida = await _nutritionRepository.ObterClarificacaoPendenteAsync(senderPhone);
                        int? volumePendente = pendenteBebida != null ? LerVolumeMl(pendenteBebida.OriginalTextInput ?? "") : null;
                        if (volumePendente != null)
                        {
                            volumeNaMensagem = volumePendente;
                            await _nutritionRepository.RemoverClarificacaoPendenteAsync(senderPhone);
                        }
                    }

                    if (volumeNaMensagem != null)
                    {
                        quantidadeMl = volumeNaMensagem.Value;
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

                    var waterCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("WaterLogs");

                    var waterDoc = new MongoDB.Bson.BsonDocument
                    {
                        { "_id", Guid.NewGuid().ToString() },
                        { "UserId", senderPhone },
                        { "AmountMl", quantidadeMl },
                        { "LoggedAt", DateTime.UtcNow }
                    };
                    await waterCol.InsertOneAsync(waterDoc);

                    int totalAguaHoje = await ObterAguaDoDiaAsync(senderPhone, DateTime.UtcNow);
                    int metaAguaDiaria = await ObterMetaAguaAsync(senderPhone, DateTime.UtcNow);

                    string respostaAgua = $"💧 {totalAguaHoje} / {metaAguaDiaria} ml de água registrados com sucesso! 🚰";
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

                        string respostaPeso = $"⚖️ Peso de *{pesoInformado:F1} kg* registrado com sucesso! Continue firme no foco da evolução! 🎯";
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

                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "💪 Estimando as calorias gastas no seu treino...");

                    // Passamos o telefone para buscar o peso
                    int caloriasGastas = await EstimarGastoCaloricoComGeminiAsync(textoBruto ?? "", senderPhone);

                    if (caloriasGastas > 0)
                    {
                        var dataHojeBrTreino = DateTime.UtcNow.AddHours(-3);

                        // Gravado na mesma coleção das refeições (Nutrition_MealLogs), com calorias negativas,
                        // para entrar no saldo do dia, no "remover", no painel e no portal
                        var treinoLog = new MealLog(
                            userId: senderPhone,
                            mealType: MealLog.TipoTreino,
                            dishName: $"🔥 {textoBruto}",
                            calories: -caloriasGastas,
                            proteinG: 0m,
                            carbsG: 0m,
                            fatG: 0m,
                            loggedAt: DateTime.UtcNow);

                        await _nutritionRepository.InserirMealLogAsync(treinoLog);

                        // --- CÁLCULO MANUAL DO SALDO PARA EVITAR ZERAR ---
                        var logs = await _nutritionRepository.ObterRefeicoesDoDiaAsync(senderPhone, dataHojeBrTreino.Date);
                        var goal = await _nutritionRepository.ObterMetaDiariaAsync(senderPhone, dataHojeBrTreino.Date);

                        int metaCalorias = goal?.TargetCalories ?? 2000;
                        int ingerido = logs.Where(l => l.Calories > 0).Sum(l => l.Calories);
                        int gasto = logs.Where(l => l.Calories < 0).Sum(l => Math.Abs(l.Calories));
                        int saldoLiquido = ingerido - gasto;
                        int restante = metaCalorias - saldoLiquido;

                        string respostaTreino = $"🔥 *Treino registrado com sucesso!*\n\n" +
                                                $"Você gastou aproximadamente *{caloriasGastas} kcal* com essa atividade.\n" +
                                                $"Isso gerou um bônus calórico no seu saldo de hoje! 🏃‍♂️💨\n\n" +
                                                $"📊 *SEU NOVO SALDO*\n" +
                                                $"• *Meta Diária:* {metaCalorias} kcal\n" +
                                                $"• *Calorias Ingeridas:* {ingerido} kcal\n" +
                                                $"• *Gasto em Treinos:* {gasto} kcal\n" +
                                                $"• *Saldo Líquido Atual:* {saldoLiquido} kcal\n\n" +
                                                $"_Você pode consumir mais {restante} kcal hoje._";

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
                MealLog? refeicaoParaCorrigir = null;

                if (contextoPendente != null)
                {
                    textoFinalParaIa = $"[Contexto Anterior da Refeição: {contextoPendente.OriginalTextInput}] " +
                                       $"[Pergunta enviada ao usuário: {contextoPendente.ClarificationQuestion}] " +
                                       $"[Confirmação / Resposta do Usuário: {textoBruto}] " +
                                       $"Instrução: Processe e registre definitivamente esta refeição considerando a resposta do usuário.";

                    imagemFinalParaIa ??= contextoPendente.OriginalBase64Image;
                }
                else
                {
                    var palavrasCorrecao = new[] { "esqueci", "corrigir", "não tinha", "adiciona", "tira", "remova", "errei", "faltei" };
                    bool pareceCorrecao = palavrasCorrecao.Any(p => textoLimpoCmd.Contains(p));

                    if (pareceCorrecao && string.IsNullOrWhiteSpace(imagemBase64))
                    {
                        var dataHojeBrCorrecao = DateTime.UtcNow.AddHours(-3);
                        var logsHoje = await _nutritionRepository.ObterRefeicoesDoDiaAsync(senderPhone, dataHojeBrCorrecao);

                        var ultimaRefeicao = logsHoje?.Where(x => !x.IsTreino).OrderByDescending(x => x.LoggedAt).FirstOrDefault();

                        if (ultimaRefeicao != null)
                        {
                            refeicaoParaCorrigir = ultimaRefeicao;

                            textoFinalParaIa = $"[MODO CORREÇÃO] O usuário quer ajustar sua última refeição. " +
                                               $"Refeição original salva: '{ultimaRefeicao.DishName ?? ultimaRefeicao.MealType}' com {ultimaRefeicao.Calories} kcal. " +
                                               $"Novo comando do usuário: '{textoBruto}'. " +
                                               $"Instrução: Refaça os cálculos adicionando ou removendo o que o usuário pediu e registre como uma refeição completa corrigida.";
                        }
                    }
                }

                // 🛡️ GUARDRAIL: FILTRO ANTI-GASTOS DE IA E MENSAGENS GENÉRICAS
                bool eIntencaoRefeicao = EhIntencaoRefeicaoOuComida(textoLimpoCmd, textoBruto ?? "", !string.IsNullOrWhiteSpace(imagemBase64), contextoPendente != null || refeicaoParaCorrigir != null);

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

                // Só altera dados depois de garantir que a IA vai rodar (antes, o limite do trial podia apagar a refeição sem refazê-la)
                if (contextoPendente != null)
                {
                    await _nutritionRepository.RemoverClarificacaoPendenteAsync(senderPhone);
                }

                if (refeicaoParaCorrigir != null)
                {
                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "🛠️ Corrigindo sua última refeição...");
                    await _nutritionRepository.ExcluirMealLogAsync(refeicaoParaCorrigir.Id);
                }

                var request = new ParseMealRequestDto(
                    senderPhone,
                    TextInput: textoFinalParaIa,
                    Base64Image: imagemFinalParaIa,
                    AudioUrl: null,
                    LocalTime: DateTime.UtcNow,
                    ClinicalProtocol: protocoloClinico
                );

                var result = await _nutritionService.ProcessMealEntryAsync(request);

                // Grava no histórico só depois da análise: se for gravada antes, a IA recebe a mesma mensagem
                // duas vezes (no histórico e como pedido atual) e soma as quantidades em dobro
                await _nutritionRepository.SalvarMensagemHistoricoAsync(senderPhone, "user", textoFinalParaIa);
                DailyStatusResponseDto? statusDoDia = null;

                bool isSystemError = result.ClarificationQuestion != null &&
                                     result.ClarificationQuestion.Contains("serviços de IA estão instáveis", StringComparison.OrdinalIgnoreCase);

                if (result.RequiresUserClarification && !isSystemError)
                {
                    var novaClarificacao = new MealClarificationContext(
                        userId: senderPhone,
                        originalTextInput: textoFinalParaIa,
                        originalBase64Image: imagemFinalParaIa,
                        clarificationQuestion: result.ClarificationQuestion ?? "Você pode detalhar melhor sua refeição?"
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

        private async Task RegistrarStatusDeEntregaAsync(MetaWebhookPayload? payload)
        {
            if (payload?.Entry == null) return;

            foreach (var status in payload.Entry
                         .SelectMany(e => e.Changes ?? new List<MetaChange>())
                         .SelectMany(c => c.Value?.Statuses ?? new List<MetaStatus>()))
            {
                if (string.IsNullOrWhiteSpace(status.Id) || string.IsNullOrWhiteSpace(status.Status)) continue;

                try
                {
                    var quando = long.TryParse(status.Timestamp, out var unix)
                        ? DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime
                        : DateTime.UtcNow;

                    var erro = status.Errors?.FirstOrDefault();
                    string? errorMessage = erro == null ? null
                        : !string.IsNullOrWhiteSpace(erro.ErrorData?.Details) ? erro.ErrorData!.Details
                        : erro.Message ?? erro.Title;

                    await TemplateDeliveryTracker.RegistrarStatusAsync(
                        _database, status.Id, status.Status, quando, erro?.Code?.ToString(), errorMessage);

                    if (status.Status == TemplateDeliveryTracker.StatusFalhou)
                    {
                        _logger.LogWarning("[WhatsApp Status] Mensagem {MessageId} falhou: {Code} {Error}", status.Id, erro?.Code, errorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Não foi possível registrar o status de entrega {MessageId}.", status.Id);
                }
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
            var agoraUtc = DateTime.UtcNow;
            var hojeBr = agoraUtc.AddHours(-3);
            // 1º dia do mês às 00:00 de Brasília, expresso em UTC
            var inicioMesUtc = DateTime.SpecifyKind(new DateTime(hojeBr.Year, hojeBr.Month, 1).AddHours(3), DateTimeKind.Utc);

            // Painel não usa as sugestões da IA: status calculado sem chamada de IA
            var statusHoje = await _nutritionService.GetDailyStatusAsync(phone, hojeBr);

            int totalAguaHoje = 0;
            try
            {
                totalAguaHoje = await ObterAguaDoDiaAsync(phone, agoraUtc);
            }
            catch { }

            int metaAgua = await ObterMetaAguaAsync(phone, agoraUtc);

            int diasComRegistro = 0;
            int totalCaloriasIngeridasMes = 0;
            int totalCaloriasGastasTreinoMes = 0;
            int mediaCaloriasDiaria = 0;

            try
            {
                var filterMes = Builders<MongoDB.Bson.BsonDocument>.Filter.And(
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("UserId", phone),
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Gte("LoggedAt", inicioMesUtc)
                );

                // Refeições e treinos ficam em Nutrition_MealLogs. A coleção antiga "MealLogs" só tem treinos
                // gravados antes da correção; continua sendo lida para não perder o histórico do mês.
                var logsDoMes = await _database.GetCollection<MongoDB.Bson.BsonDocument>("Nutrition_MealLogs").Find(filterMes).ToListAsync();
                var treinosLegados = await _database.GetCollection<MongoDB.Bson.BsonDocument>("MealLogs").Find(filterMes).ToListAsync();
                logsDoMes.AddRange(treinosLegados);

                var diasComRefeicao = new HashSet<DateTime>();

                foreach (var log in logsDoMes)
                {
                    // IsNumeric + ToInt32() abrange Int32, Int64 ou Double do MongoDB
                    if (!log.Contains("Calories") || !log["Calories"].IsNumeric) continue;

                    int cal = log["Calories"].ToInt32();
                    bool eTreino = cal < 0 || (log.Contains("MealType") && log["MealType"].IsString && log["MealType"].AsString == MealLog.TipoTreino);

                    if (eTreino)
                    {
                        totalCaloriasGastasTreinoMes += Math.Abs(cal);
                    }
                    else if (cal > 0)
                    {
                        totalCaloriasIngeridasMes += cal;
                        if (log.Contains("LoggedAt") && log["LoggedAt"].IsBsonDateTime)
                        {
                            diasComRefeicao.Add(log["LoggedAt"].ToUniversalTime().AddHours(-3).Date);
                        }
                    }
                }

                // Dia ativo = dia com pelo menos uma refeição registrada (treino sozinho não conta)
                diasComRegistro = diasComRefeicao.Count;
                mediaCaloriasDiaria = diasComRegistro > 0 ? totalCaloriasIngeridasMes / diasComRegistro : 0;
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
            sb.AppendLine($"✅ *Dias Ativos:* {diasComRegistro} dia(s) registrados");
            sb.AppendLine($"🔥 *Média Diária Ingerida:* {mediaCaloriasDiaria:N0} kcal/dia");
            sb.AppendLine($"🍽️ *Total Ingerido:* {totalCaloriasIngeridasMes:N0} kcal");

            // Balanço do mês: saldo líquido (ingerido - gasto em treinos) contra a meta acumulada dos dias ativos
            if (diasComRegistro > 0)
            {
                int metaAcumulada = diasComRegistro * statusHoje.Target.Calories;
                int diferenca = (totalCaloriasIngeridasMes - totalCaloriasGastasTreinoMes) - metaAcumulada;

                if (diferenca > 0)
                    sb.AppendLine($"📈 *Balanço:* Ultrapassou a meta em {diferenca:N0} kcal");
                else if (diferenca < 0)
                    sb.AppendLine($"📉 *Balanço:* Ficou {Math.Abs(diferenca):N0} kcal abaixo da meta (déficit)");
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

        // Volume em ml ou litros, aceitando decimal ("1,5 litro", "600ml", "2 l")
        private static readonly Regex VolumeRegex = new(@"(\d+(?:[.,]\d+)?)\s*(ml|mls|lt|litros?|l)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly HashSet<string> PalavrasDeRegistroDeAgua = new(StringComparer.OrdinalIgnoreCase)
        {
            "tomei", "bebi", "mais", "de", "do", "da", "um", "uma", "uns", "umas", "e", "eu", "ja", "já", "agora", "hoje",
            "acabei", "foram", "foi", "so", "só", "cerca", "quase", "aproximadamente", "copo", "copos", "garrafa", "garrafas"
        };

        // Volume da mensagem em ml ("600 ml" → 600, "1,5 litro" → 1500); null se não houver volume
        private static int? LerVolumeMl(string texto)
        {
            var match = VolumeRegex.Match(texto);
            if (!match.Success ||
                !decimal.TryParse(match.Groups[1].Value.Replace(",", "."), System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var volume))
            {
                return null;
            }

            // "ml" é mililitro; qualquer outra unidade aceita (l, lt, litro, litros) é litro
            bool emLitros = !match.Groups[2].Value.StartsWith("ml", StringComparison.OrdinalIgnoreCase);
            return (int)Math.Round(emLitros ? volume * 1000 : volume);
        }

        // Mensagem que só informa um volume, sem dizer o que foi bebido.
        // "Tomei mais 600 ml" → sim (o bot pergunta o que foi); "bebi 300ml de suco" → não (tem "suco"), segue para a IA.
        private static bool EhRegistroSoDeVolume(string texto)
        {
            if (!VolumeRegex.IsMatch(texto)) return false;

            var resto = VolumeRegex.Replace(texto, " ");
            resto = Regex.Replace(resto, "[^a-zA-ZáéíóúâêîôûãõçÁÉÍÓÚÂÊÎÔÛÃÕÇ ]", " ");
            return resto.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(p => PalavrasDeRegistroDeAgua.Contains(p));
        }

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

                        var pratos = logs.Where(x => !x.IsTreino).Select(x => x.DishName).Where(d => !string.IsNullOrWhiteSpace(d)).ToList();

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
                _logger.LogInformation("[SendExpiredOffers] Encontrados {Count} usuários elegíveis para oferta de expiração hoje.", expiredUsers.Count);

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
                        bool success = await EnviarLembreteDeRegistroAsync(phone);

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

        // Lembrete de registro: usa o template de Utilidade (não sofre o bloqueio 131049 de marketing).
        // Enquanto ele não existir ou não estiver aprovado na Meta, a recusa é imediata e o envio cai no
        // template antigo de Marketing. Nenhum dos dois tem variáveis (enviar parâmetro gera o erro 132000).
        private const string TemplateLembreteRegistro = "lembrete_registro_refeicoes";
        private const string TemplateLembreteRegistroAntigo = "trial_engajamento";

        private async Task<bool> EnviarLembreteDeRegistroAsync(string phone)
        {
            var templatePrincipal = _configuration["MetaWhatsApp:TemplateLembreteRegistro"]?.Trim();
            if (string.IsNullOrEmpty(templatePrincipal)) templatePrincipal = TemplateLembreteRegistro;

            var envio = await _whatsAppSender.SendTemplateMessageWithResultAsync(phone, templatePrincipal);
            if (envio.Success || templatePrincipal == TemplateLembreteRegistroAntigo) return envio.Success;

            _logger.LogWarning("[Lembrete Trial] Template {Template} recusado pela Meta ({Code}: {Erro}); usando {Antigo}.",
                templatePrincipal, envio.ErrorCode, envio.ErrorMessage, TemplateLembreteRegistroAntigo);
            return await _whatsAppSender.SendTemplateMessageAsync(phone, TemplateLembreteRegistroAntigo);
        }

        private async Task<bool> ReserveTrialInteractionOrNotifyAsync(
            string phone,
            IMongoCollection<MongoDB.Bson.BsonDocument> trialCollection)
        {
            bool reserved = await TryReserveDailyTrialInteractionAsync(
                trialCollection, phone, DateTime.UtcNow);
            if (reserved) return true;

            await EnviarMensagemLimiteDiarioAsync(phone);
            return false;
        }

        private async Task EnviarMensagemLimiteDiarioAsync(string phone)
        {
            _logger.LogInformation("[WhatsApp B2C] Limite diário do trial atingido para interação de IA.");
            string? trackingLink = await CriarLinkPagamentoRastreadoAsync(phone, "Cliente Limite");

            string message = "⚠️ *Você chegou ao limite de 4 análises de IA do teste de hoje.*\n\n" +
                             "Mensagens simples e registros básicos não contam para esse limite. Amanhã você poderá continuar.\n\n" +
                             (!string.IsNullOrEmpty(trackingLink)
                                 ? $"Se quiser conhecer a assinatura mensal: 👉 {trackingLink}"
                                 : "Se quiser saber sobre a assinatura mensal, responda *assinar*.");
            await _whatsAppSender.SendTextMessageAsync(phone, message);
        }

        private async Task EnviarMensagemTrialExpiradoAsync(string phone)
        {
            _logger.LogInformation("[WhatsApp B2C] Trial expirado após {TrialDays} dias.", TrialDurationDays);
            string? trackingLink = await CriarLinkPagamentoRastreadoAsync(phone, "Cliente Trial");

            string msgExpirado = "⏳ *Seu período de teste de 15 dias terminou.*\n\n" +
                                 "Se quiser continuar, veja os detalhes da assinatura mensal abaixo:\n\n" +
                                 (!string.IsNullOrEmpty(trackingLink) ? $"👉 {trackingLink}\n\n" : "") +
                                 "A liberação do acesso depende da confirmação e ativação da assinatura no sistema.";
            await _whatsAppSender.SendTextMessageAsync(phone, msgExpirado);
        }

        private static bool TrialExpirado(MongoDB.Bson.BsonDocument userDoc, DateTime utcNow)
        {
            return userDoc.TryGetValue("TrialStartDate", out var trialStartValue) &&
                   trialStartValue.IsBsonDateTime &&
                   utcNow >= trialStartValue.ToUniversalTime().AddDays(TrialDurationDays);
        }

        // Leitura sem reservar: mesma regra do TryReserveDailyTrialInteractionAsync
        private static bool LimiteDiarioTrialAtingido(MongoDB.Bson.BsonDocument userDoc, DateTime utcNow)
        {
            var today = GetBrazilDate(utcNow);

            bool isSameDay = userDoc.TryGetValue("LastInteractionDate", out var dateValue) &&
                             dateValue.IsBsonDateTime &&
                             dateValue.ToUniversalTime().Date == today.Date;
            if (!isSameDay) return false;

            int count = userDoc.TryGetValue("DailyMessageCount", out var countValue) && countValue.IsNumeric
                ? countValue.ToInt32()
                : 0;
            return count >= DailyTrialInteractionLimit;
        }

        private static DateTime GetBrazilDate(DateTime utcNow)
        {
            return DateTime.SpecifyKind(utcNow.AddHours(-3).Date, DateTimeKind.Utc);
        }

        // 00:00 de Brasília do dia corrente, expresso em UTC (03:00Z)
        private static DateTime InicioDoDiaBrasilEmUtc(DateTime utcNow)
        {
            return DateTime.SpecifyKind(utcNow.AddHours(-3).Date.AddHours(3), DateTimeKind.Utc);
        }

        private async Task<int> ObterAguaDoDiaAsync(string phone, DateTime utcNow)
        {
            var inicioDiaUtc = InicioDoDiaBrasilEmUtc(utcNow);
            var waterCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("WaterLogs");
            var filterWater = Builders<MongoDB.Bson.BsonDocument>.Filter.And(
                Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("UserId", phone),
                Builders<MongoDB.Bson.BsonDocument>.Filter.Gte("LoggedAt", inicioDiaUtc),
                Builders<MongoDB.Bson.BsonDocument>.Filter.Lt("LoggedAt", inicioDiaUtc.AddDays(1))
            );
            var registrosAgua = await waterCol.Find(filterWater).ToListAsync();
            return registrosAgua.Sum(w => w.Contains("AmountMl") && w["AmountMl"].IsNumeric ? w["AmountMl"].ToInt32() : 0);
        }

        // Meta de água do usuário (exata do dia, senão a mais recente); 2500 ml quando não houver meta definida
        private async Task<int> ObterMetaAguaAsync(string phone, DateTime utcNow)
        {
            const int metaAguaPadrao = 2500;
            try
            {
                var goal = await _nutritionRepository.ObterMetaDiariaAsync(phone, utcNow.AddHours(-3).Date);
                return goal != null && goal.TargetWaterMl > 0 ? goal.TargetWaterMl : metaAguaPadrao;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Não foi possível ler a meta de água de {Phone}; usando o padrão.", phone);
                return metaAguaPadrao;
            }
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

        // Gera o link na InfinitePay, grava o rastreio (PaymentTracking) e devolve o link de rastreio de clique
        private async Task<string?> CriarLinkPagamentoRastreadoAsync(string phone, string nomeCliente)
        {
            var orderNsu = Guid.NewGuid().ToString();
            string? paymentUrlOriginal = await GerarLinkPagamentoInfinitePayAsync(phone, nomeCliente, 29.90m, orderNsu);
            if (string.IsNullOrEmpty(paymentUrlOriginal)) return null;

            var trackingCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("PaymentTracking");
            var trackingDoc = new MongoDB.Bson.BsonDocument
            {
                { "_id", phone },
                { "InfinitePayUrl", paymentUrlOriginal },
                { "OrderNsu", orderNsu },
                { "WebhookProtegido", !string.IsNullOrEmpty(ObterSegredoWebhookInfinitePay()) },
                { "SentAt", DateTime.UtcNow },
                { "ClickedAt", MongoDB.Bson.BsonNull.Value },
                { "Status", "Sent" }
            };
            await trackingCol.ReplaceOneAsync(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", phone), trackingDoc, new ReplaceOptions { IsUpsert = true });

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            return $"{baseUrl}/api/whatsapp/ir-para-pagamento?phone={phone}";
        }

        private string? ObterSegredoWebhookInfinitePay()
        {
            var secret = _configuration["InfinitePay:WebhookSecret"]?.Trim();
            return string.IsNullOrEmpty(secret) ? null : secret;
        }

        private async Task<string?> GerarLinkPagamentoInfinitePayAsync(string phone, string name, decimal valorOriginal, string orderNsu)
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

                // Com segredo configurado, ele vai no caminho da URL do webhook; só a InfinitePay conhece essa URL
                var webhookUrl = "https://labelwise-1.onrender.com/api/whatsapp/infinitepay-webhook";
                var webhookSecret = ObterSegredoWebhookInfinitePay();
                if (webhookSecret != null)
                {
                    webhookUrl += "/" + Uri.EscapeDataString(webhookSecret);
                }

                var linkRequest = new
                {
                    handle = handle,
                    order_nsu = orderNsu,
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
                        url = webhookUrl,
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
        [HttpPost("infinitepay-webhook/{token}")]
        public async Task<IActionResult> InfinitePayWebhook([FromBody] JsonElement payload, string? token = null)
        {
            try
            {
                string jsonString = payload.GetRawText();
                _logger.LogInformation("[INFINITEPAY WEBHOOK] Recebido: {Payload}", jsonString);

                string? status = null;
                string? phone = null;
                string? orderNsu = null;

                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;

                if (root.TryGetProperty("status", out var statusProp) && statusProp.ValueKind == JsonValueKind.String)
                {
                    status = statusProp.GetString()?.ToLower();
                }

                if (root.TryGetProperty("customer", out var customerProp) && customerProp.ValueKind == JsonValueKind.Object &&
                    customerProp.TryGetProperty("phone_number", out var phoneProp) && phoneProp.ValueKind == JsonValueKind.String)
                {
                    phone = phoneProp.GetString();
                }

                if (root.TryGetProperty("order_nsu", out var orderNsuProp) && orderNsuProp.ValueKind == JsonValueKind.String)
                {
                    orderNsu = orderNsuProp.GetString();
                }

                if ((status == "approved" || status == "paid") && !string.IsNullOrEmpty(phone))
                {
                    string cleanPhone = Regex.Replace(phone, "[^0-9]", "");

                    var trackingCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("PaymentTracking");
                    var pagamentoRastreado = await LocalizarPagamentoRastreadoAsync(trackingCol, orderNsu, cleanPhone);

                    if (!WebhookInfinitePayAutorizado(token, orderNsu, pagamentoRastreado))
                    {
                        _logger.LogWarning("[INFINITEPAY WEBHOOK] Chamada recusada: segredo inválido e nenhum link de pagamento pendente correspondente.");
                        return Unauthorized();
                    }

                    // O telefone gravado no rastreio é o mesmo formato do WhatsApp (_id do trial); o do payload pode vir sem o 55
                    if (pagamentoRastreado != null && pagamentoRastreado["_id"].IsString)
                    {
                        cleanPhone = pagamentoRastreado["_id"].AsString;
                    }

                    // 1. Atualizar a base de dados de Trial para Premium
                    var trialCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("B2C_Trial_Users");
                    var filter = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", cleanPhone);
                    var update = Builders<MongoDB.Bson.BsonDocument>.Update.Set("Premium", true);

                    await trialCollection.UpdateOneAsync(filter, update);

                    // 2. Atualizar também a collection de rastreio de pagamentos para "Paid"
                    var trackingFilter = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", cleanPhone);
                    var trackingUpdate = Builders<MongoDB.Bson.BsonDocument>.Update
                        .Set("Status", "Paid")
                        .Set("PaidAt", DateTime.UtcNow);

                    await trackingCol.UpdateOneAsync(trackingFilter, trackingUpdate, new UpdateOptions { IsUpsert = true });

                    _logger.LogInformation("✅ Usuário {Phone} ativado como Premium e registrado como pago com sucesso!", cleanPhone);

                    // 3. Enviar mensagem de boas-vindas ao Premium
                    string msgSucesso = "🎉 *Pagamento Confirmado!* \n\nSua assinatura Premium do Nutrição Fácil está ativa. Você já pode continuar registrando suas refeições sem nenhum limite! 🥗🚀";
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

        // Aceita a confirmação de pagamento quando:
        // - o segredo do webhook não está configurado (comportamento anterior, com aviso no log); ou
        // - o segredo veio correto na URL; ou
        // - o order_nsu do payload é o mesmo de um link que nós geramos; ou
        // - existe um link pendente gerado antes da proteção (WebhookProtegido ausente/false), para não perder pagamentos em andamento.
        private bool WebhookInfinitePayAutorizado(string? token, string? orderNsu, MongoDB.Bson.BsonDocument? pagamentoRastreado)
        {
            var secret = ObterSegredoWebhookInfinitePay();
            if (secret == null)
            {
                _logger.LogWarning("[INFINITEPAY WEBHOOK] InfinitePay:WebhookSecret não configurado; webhook aceito sem validação.");
                return true;
            }

            if (!string.IsNullOrEmpty(token) &&
                CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(secret)))
            {
                return true;
            }

            if (pagamentoRastreado == null) return false;

            if (!string.IsNullOrEmpty(orderNsu) &&
                pagamentoRastreado.TryGetValue("OrderNsu", out var orderNsuSalvo) && orderNsuSalvo.IsString &&
                orderNsuSalvo.AsString == orderNsu)
            {
                return true;
            }

            bool linkProtegido = pagamentoRastreado.TryGetValue("WebhookProtegido", out var protegido) && protegido.IsBoolean && protegido.AsBoolean;
            bool jaPago = pagamentoRastreado.TryGetValue("Status", out var statusSalvo) && statusSalvo.IsString && statusSalvo.AsString == "Paid";
            return !linkProtegido && !jaPago;
        }

        private static async Task<MongoDB.Bson.BsonDocument?> LocalizarPagamentoRastreadoAsync(
            IMongoCollection<MongoDB.Bson.BsonDocument> trackingCol, string? orderNsu, string cleanPhone)
        {
            if (!string.IsNullOrEmpty(orderNsu))
            {
                var porOrderNsu = await trackingCol.Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("OrderNsu", orderNsu)).FirstOrDefaultAsync();
                if (porOrderNsu != null) return porOrderNsu;
            }

            if (string.IsNullOrEmpty(cleanPhone)) return null;

            var porTelefone = await trackingCol.Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", cleanPhone)).FirstOrDefaultAsync();
            if (porTelefone != null || cleanPhone.StartsWith("55")) return porTelefone;

            return await trackingCol.Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", "55" + cleanPhone)).FirstOrDefaultAsync();
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

        private async Task<int> EstimarGastoCaloricoComGeminiAsync(string textoAtividade, string phone)
        {
            try
            {
                // Busca o último peso registrado do usuário para usar no cálculo
                var weightCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("WeightLogs");
                var lastWeightDoc = await weightCol.Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("UserId", phone))
                                                   .SortByDescending(w => w["LoggedAt"])
                                                   .FirstOrDefaultAsync();

                double pesoUsuario = 70.0; // Peso padrão caso não tenha registrado
                if (lastWeightDoc != null && lastWeightDoc.Contains("WeightKg") && lastWeightDoc["WeightKg"].IsNumeric)
                {
                    pesoUsuario = lastWeightDoc["WeightKg"].ToDouble();
                }

                var apiKey = _configuration["GeminiApiKey"] ?? _configuration["Gemini:ApiKey"];
                var endpoint = _configuration["Gemini:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
                var model = _configuration["Model"] ?? "gemini-1.5-flash";

                // Prompt detalhado exigindo cálculo preciso baseado no peso
                var prompt = $"O usuário relatou a seguinte atividade física: '{textoAtividade}'.\n" +
                             $"O peso atual deste usuário é de {pesoUsuario:F1} kg.\n\n" +
                             $"Instruções:\n" +
                             $"1. Estime o gasto calórico com precisão matemática baseando-se no tipo de exercício, duração (se mencionada) e no peso de {pesoUsuario:F1} kg do usuário.\n" +
                             $"2. Se o usuário disser 'musculação pesada', utilize o valor MET adequado e não superestime. \n" +
                             $"3. Se o texto incluir várias atividades (ex: musculação + esteira), calcule a soma das duas separadamente e devolva apenas o valor total.\n" +
                             $"4. Responda APENAS com o número inteiro (sem texto, sem 'kcal', apenas o número). Se não for atividade física reconhecível, responda 0.";

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
                return "⚠️ *Ops! Nossos serviços estão instáveis no momento.*\nPor favor, tente novamente em instantes.";

            if (aiResult.IsAdvice)
                return $"💡 *Conselho do Nutri:* \n\n{(!string.IsNullOrWhiteSpace(aiResult.AdviceText) ? aiResult.AdviceText : "Como posso ajudar?")}";

            if (aiResult.RequiresUserClarification)
                return $"🤔 *Só mais um detalhe:*\n{aiResult.ClarificationQuestion}";

            var prato = !string.IsNullOrWhiteSpace(aiResult.DishName) ? aiResult.DishName : aiResult.MealType;
            var calorias = aiResult.TotalMeal?.Calories ?? 0;
            var proteina = aiResult.TotalMeal?.ProteinG ?? 0;
            var carbo = aiResult.TotalMeal?.CarbsG ?? 0;
            var gordura = aiResult.TotalMeal?.FatG ?? 0;

            var msg = $"✅ *Refeição registrada:* {prato}\n\n";

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
                msg += $"📊 *SEU RESUMO DE HOJE*\n• *Calorias:* {statusDoDia.Consumed.Calories} / {statusDoDia.Target.Calories} kcal\n";
                var faltamCal = statusDoDia.Remaining.Calories;
                msg += faltamCal <= 0 ? $"_⚠️ Você atingiu ou ultrapassou sua meta!_\n" : $"_Faltam {faltamCal} kcal_\n";
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

                int enviados = 0, falhas = 0, ignoradosMetaBatida = 0;

                foreach (var telefone in telefones)
                {
                    try
                    {
                        // Validação do ciclo de vida do usuário (Trial / Premium)
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
                        int metaAgua = await ObterMetaAguaAsync(telefone, DateTime.UtcNow);

                        // 2. Calcula Água Consumida Hoje
                        int totalAguaHoje = await ObterAguaDoDiaAsync(telefone, DateTime.UtcNow);

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
        public class MetaValue
        {
            public List<MetaMessage>? Messages { get; set; }
            public List<MetaStatus>? Statuses { get; set; }
        }

        // Aviso de entrega de uma mensagem enviada por nós
        public class MetaStatus
        {
            public string? Id { get; set; }
            public string? Status { get; set; }
            public string? Timestamp { get; set; }
            [JsonPropertyName("recipient_id")]
            public string? RecipientId { get; set; }
            public List<MetaStatusError>? Errors { get; set; }
        }

        public class MetaStatusError
        {
            public int? Code { get; set; }
            public string? Title { get; set; }
            public string? Message { get; set; }
            [JsonPropertyName("error_data")]
            public MetaStatusErrorData? ErrorData { get; set; }
        }

        public class MetaStatusErrorData { public string? Details { get; set; } }

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