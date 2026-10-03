using LabelWise.Application.DTOs;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
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
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<WhatsAppController> logger,
            IMongoDatabase database)
        {
            _nutritionService = nutritionService;
            _whatsAppSender = whatsAppSender;
            _metaMediaService = metaMediaService;
            _nutritionRepository = nutritionRepository;
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

                // 1. Valida se o paciente está cadastrado no Portal da Clínica B2B
                var pacienteCadastrado = await _nutritionRepository.ObterPacientePorIdAsync(senderPhone);

                if (pacienteCadastrado == null)
                {
                    // 2. Camada B2C: Gestão do Trial de 15 dias e Onboarding Guiado
                    var trialCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("B2C_Trial_Users");
                    var filterTrial = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", senderPhone);
                    var userDoc = await trialCollection.Find(filterTrial).FirstOrDefaultAsync();

                    var now = DateTime.UtcNow;

                    // --- PASSO 1: ESTREIA DO UTILIZADOR ---
                    if (userDoc == null)
                    {
                        userDoc = new MongoDB.Bson.BsonDocument
                        {
                            { "_id", senderPhone },
                            { "TrialStartDate", now },
                            { "LastInteractionDate", now.Date },
                            { "DailyMessageCount", 0 },
                            { "ProfileConfigured", false } // Ainda não configurado
                        };
                        await trialCollection.InsertOneAsync(userDoc);

                        // Cria registos base temporários nas tabelas de nutrição
                        var goalsCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("DailyGoals");
                        var defaultGoal = new MongoDB.Bson.BsonDocument
                        {
                            { "_id", Guid.NewGuid().ToString() },
                            { "UserId", senderPhone },
                            { "NutritionistId", "b2c_autonomous_user" },
                            { "TargetDate", now.Date },
                            { "TargetCalories", 2000 },
                            { "TargetProteinG", 150 },
                            { "TargetCarbsG", 200 },
                            { "TargetFatG", 60 },
                            { "DietaryRestrictions", "" },
                            { "FavoriteFoods", "" },
                            { "PrescribedMealPlan", "Plano autónomo pendente de configuração." }
                        };
                        await goalsCollection.InsertOneAsync(defaultGoal);

                        var patientsCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("Nutrition_Patients");
                        var defaultPatient = new MongoDB.Bson.BsonDocument
                        {
                            { "_id", senderPhone },
                            { "ProfessionalId", "b2c_autonomous_user" },
                            { "MainGoal", "Pendente" },
                            { "MedicalRestrictions", "" },
                            { "FoodAversions", "" }
                        };
                        await patientsCollection.ReplaceOneAsync(
                            Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", senderPhone),
                            defaultPatient,
                            new ReplaceOptions { IsUpsert = true }
                        );

                        // Envia mensagem de boas vindas com exemplo
                        string mensagemBoasVindas = "🎉 *Bem-vindo ao LabelWise (Versão de Teste - 15 dias)!* 🥗\n\n" +
                                                    "Para começarmos a personalizar a sua IA e garantir total segurança com **alergias e restrições**, por favor envie uma mensagem com o seu objetivo e preferências.\n\n" +
                                                    "📝 *Exemplo de texto para enviar agora:*\n" +
                                                    "_'Meu objetivo é emagrecimento, meta de 1800 calorias, sou alérgico a amendoim e não gosto de ovo.'_\n\n" +
                                                    "Assim que enviar este texto, o seu perfil estará pronto e poderá começar a registrar as suas refeições! ✨";

                        await _whatsAppSender.SendTextMessageAsync(senderPhone, mensagemBoasVindas);
                        return Ok(); // Encerra aqui na estreia
                    }

                    // --- PASSO 2: EXTRAÇÃO ESTRUTURADA DO PERFIL (VIA IA) ---
                    bool profileConfigured = userDoc.Contains("ProfileConfigured") && userDoc["ProfileConfigured"].AsBoolean;

                    if (!profileConfigured)
                    {
                        var textoConfig = messagingEvent?.Text?.Body ?? string.Empty;

                        // 🛡️ VALIDAÇÃO 1: Evitar textos curtos como "olá", "ok", "sim"
                        if (textoConfig.Trim().Length < 10)
                        {
                            string msgErroCurta = "👋 Olá! Notei que ainda precisa configurar o seu perfil.\n\n" +
                                                  "Para a IA funcionar corretamente, por favor envie uma frase com o seu objetivo e preferências.\n\n" +
                                                  "📝 *Exemplo:* _'Quero emagrecer, 1500 kcal, sou alérgico a amendoim e não gosto de ovo.'_";
                            await _whatsAppSender.SendTextMessageAsync(senderPhone, msgErroCurta);
                            return Ok(); // Interrompe para o utilizador tentar de novo
                        }

                        await _whatsAppSender.SendTextMessageAsync(senderPhone, "⚙️ Processando o seu perfil...");

                        // 🚀 EXTRAÇÃO INTELIGENTE DE DADOS VIA GEMINI API
                        var perfilExtraido = await ExtrairPerfilComGeminiAsync(textoConfig);

                        // 🛡️ VALIDAÇÃO 2: Se a IA não conseguiu extrair um objetivo válido
                        if (perfilExtraido.MainGoal == "Não informado" || perfilExtraido.MainGoal == "Não identificado automaticamente")
                        {
                            string msgErroIA = "🤔 Não consegui identificar os detalhes do seu objetivo nessa mensagem.\n\n" +
                                               "Por favor, tente ser um pouco mais específico sobre a sua meta e eventuais restrições.\n\n" +
                                               "📝 *Exemplo:* _'Meu objetivo é hipertrofia, 2500 calorias, não gosto de ovo.'_";
                            await _whatsAppSender.SendTextMessageAsync(senderPhone, msgErroIA);
                            return Ok(); // Interrompe para o utilizador tentar de novo
                        }

                        // Atualiza as tabelas com os dados limpos extraídos pela IA
                        var patientsCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("Nutrition_Patients");
                        var patientFilter = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", senderPhone);
                        var updatePatient = Builders<MongoDB.Bson.BsonDocument>.Update
                            .Set("MainGoal", perfilExtraido.MainGoal)
                            .Set("MedicalRestrictions", perfilExtraido.MedicalRestrictions)
                            .Set("FoodAversions", perfilExtraido.FoodAversions);

                        await patientsCollection.UpdateOneAsync(patientFilter, updatePatient);

                        var goalsCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("DailyGoals");
                        var goalFilter = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("UserId", senderPhone);
                        var updateGoal = Builders<MongoDB.Bson.BsonDocument>.Update
                            .Set("TargetCalories", perfilExtraido.TargetCalories)
                            .Set("DietaryRestrictions", perfilExtraido.MedicalRestrictions)
                            .Set("PrescribedMealPlan", $"Objetivo: {perfilExtraido.MainGoal}");

                        await goalsCollection.UpdateOneAsync(goalFilter, updateGoal);

                        // Marca o perfil como 100% configurado
                        var updateTrialConfig = Builders<MongoDB.Bson.BsonDocument>.Update.Set("ProfileConfigured", true);
                        await trialCollection.UpdateOneAsync(filterTrial, updateTrialConfig);

                        string respostaConfig = $"✅ *Perfil configurado com sucesso!* 🥗\n\n" +
                                                $"🎯 **Objetivo:** {perfilExtraido.MainGoal}\n" +
                                                $"🔥 **Calorias Diárias:** {perfilExtraido.TargetCalories} kcal\n" +
                                                $"🛡️ **Alergias/Restrições:** {(string.IsNullOrWhiteSpace(perfilExtraido.MedicalRestrictions) ? "Nenhuma" : perfilExtraido.MedicalRestrictions)}\n" +
                                                $"🚫 **Aversões:** {(string.IsNullOrWhiteSpace(perfilExtraido.FoodAversions) ? "Nenhuma" : perfilExtraido.FoodAversions)}\n\n" +
                                                "👉 *Tudo pronto! Já pode enviar as suas refeições* por texto, foto ou áudio (ex: _'Comi frango com batata doce'_).";

                        await _whatsAppSender.SendTextMessageAsync(senderPhone, respostaConfig);
                        return Ok(); // Encerra aqui a etapa de configuração
                    }

                    // --- PASSO 3: VALIDAÇÕES DE TRIAL (15 DIAS E 3 MENSAGENS) ---
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

                    if (dailyCount >= 3)
                    {
                        _logger.LogWarning("[WhatsApp B2C] ⚠️ Limite diário de mensagens atingido para: {Phone}", senderPhone);
                        await _whatsAppSender.SendTextMessageAsync(
                            senderPhone,
                            "⚠️ Atingiu o limite de 3 interações gratuitas para hoje. O seu saldo diário será renovado amanhã! ⏰"
                        );
                        return Ok();
                    }

                    // Incrementa o contador de interações apenas para refeições
                    dailyCount++;
                    var updateB2C = Builders<MongoDB.Bson.BsonDocument>.Update
                        .Set("LastInteractionDate", lastInteractionDate)
                        .Set("DailyMessageCount", dailyCount);

                    await trialCollection.UpdateOneAsync(filterTrial, updateB2C);
                }

                // =========================================================================
                // FLUXO NORMAL DE PROCESSAMENTO DE REFEIÇÕES (IA)
                // =========================================================================
                string? textoDigitado = null;
                string? imagemBase64 = null;

                if (messageType == "text")
                {
                    textoDigitado = messagingEvent?.Text?.Body;

                    if (!string.IsNullOrWhiteSpace(textoDigitado))
                    {
                        var textoLimpo = textoDigitado.Trim().ToLowerInvariant();
                        if (textoLimpo == "minha dieta" || textoLimpo == "cardápio" || textoLimpo == "cardapio" || textoLimpo == "menu" || textoLimpo == "dieta")
                        {
                            var baseUrl = $"{Request.Scheme}://{Request.Host}";
                            var magicLink = $"{baseUrl}/paciente.html?phone={senderPhone}";

                            var respostaLink = "🥗 *Seu Plano Alimentar Interativo está pronto!*\n\n" +
                                               "Toque no link abaixo para ver o seu progresso de calorias de hoje, os macronutrientes e o seu cardápio completo:\n\n" +
                                               $"👉 {magicLink}\n\n" +
                                               "_Dica: Salve essa página nos favoritos do seu celular para consultar sempre que precisar!_ ✨";

                            await _whatsAppSender.SendTextMessageAsync(senderPhone, respostaLink);
                            return Ok();
                        }
                    }
                }
                else if (messageType == "image" && messagingEvent?.Image?.Id != null)
                {
                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "📸 Analisando o seu prato, só um instante...");
                    imagemBase64 = await _metaMediaService.DownloadMediaAsBase64Async(messagingEvent.Image.Id);
                    textoDigitado = "Analise esta refeição da imagem.";
                }
                else if (messageType == "audio" && messagingEvent?.Audio?.Id != null)
                {
                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "🎙 Ouvindo o seu áudio e transcrevendo...");
                    var audioBytes = await _metaMediaService.DownloadMediaAsBytesAsync(messagingEvent.Audio.Id);
                    textoDigitado = await TranscreverAudioComGeminiAsync(audioBytes);
                }
                else
                {
                    return Ok();
                }

                if (string.IsNullOrWhiteSpace(textoDigitado) && string.IsNullOrWhiteSpace(imagemBase64))
                    return Ok();

                var contextoPendente = await _nutritionRepository.ObterClarificacaoPendenteAsync(senderPhone);
                string textoFinalParaIa = textoDigitado ?? string.Empty;
                string? imagemFinalParaIa = imagemBase64;

                if (contextoPendente != null)
                {
                    textoFinalParaIa = $"[Descrição anterior: {contextoPendente.OriginalTextInput}] " +
                                       $"[Pergunta de dúvida feita: {contextoPendente.ClarificationQuestion}] " +
                                       $"[Resposta complementar do usuário: {textoDigitado}]";
                    imagemFinalParaIa ??= contextoPendente.OriginalBase64Image;
                    await _nutritionRepository.RemoverClarificacaoPendenteAsync(senderPhone);
                }

                await _nutritionRepository.SalvarMensagemHistoricoAsync(senderPhone, "user", textoFinalParaIa);

                var request = new ParseMealRequestDto(
                    senderPhone,
                    TextInput: textoFinalParaIa,
                    Base64Image: imagemFinalParaIa,
                    AudioUrl: null,
                    LocalTime: DateTime.UtcNow
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
                        clarificationQuestion: result.ClarificationQuestion ?? "Pode detalhar melhor sua refeição?"
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

        [HttpPost("send-reminder")]
        public async Task<IActionResult> SendReminder([FromQuery] string phone, [FromQuery] string userName = "Anderson", [FromQuery] string mealTime = "café da manhã")
        {
            if (string.IsNullOrWhiteSpace(phone)) return BadRequest(new { success = false, message = "Telefone obrigatório." });
            bool enviado = await _whatsAppSender.SendTemplateReminderAsync(phone, userName, mealTime);
            if (enviado) return Ok(new { success = true, message = $"Lembrete enviado para {phone}!" });
            return StatusCode(500, new { success = false, message = "Falha ao enviar lembrete." });
        }

        // =========================================================================
        // MÉTODOS AUXILIARES: INTEGRAÇÕES COM O GEMINI
        // =========================================================================

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
            var apiKey = _configuration["GeminiApiKey"] ?? _configuration["Gemini:ApiKey"];
            var endpoint = _configuration["Gemini:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
            var model = _configuration["Model"] ?? "gemini-3.1-flash-lite";

            var client = _httpClientFactory.CreateClient();

            var systemPrompt = @"Você é um especialista em triagem nutricional. Leia o texto e extraia os dados estritamente em formato JSON válido:
            {
                ""TargetCalories"": <número inteiro da meta de calorias. Se não informado, use 2000>,
                ""MainGoal"": ""<string com o objetivo. Ex: 'Emagrecimento'. Se não informado, retorne 'Não informado'>"",
                ""MedicalRestrictions"": ""<string com as alergias separadas por vírgula. Se não houver, vazio>"",
                ""FoodAversions"": ""<string com aversões alimentares separadas por vírgula. Se não houver, vazio>""
            }
            Apenas devolva o JSON e nada mais.";

            var requestBody = new
            {
                model = model,
                temperature = 0.0,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userText }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            try
            {
                var response = await client.SendAsync(requestMessage);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonResponse);
                    var jsonContent = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

                    if (!string.IsNullOrWhiteSpace(jsonContent))
                    {
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        return JsonSerializer.Deserialize<SetupProfileDto>(jsonContent, options) ?? new SetupProfileDto();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao extrair perfil estruturado com Gemini. Usando fallback.");
            }

            return new SetupProfileDto
            {
                TargetCalories = 2000,
                MainGoal = "Não identificado automaticamente",
                MedicalRestrictions = userText,
                FoodAversions = ""
            };
        }

        private string FormatarRespostaParaWhatsApp(MealAnalysisResponseDto aiResult, DailyStatusResponseDto? statusDoDia)
        {
            if (aiResult.ClarificationQuestion != null && aiResult.ClarificationQuestion.Contains("instáveis", StringComparison.OrdinalIgnoreCase))
                return "⚠️ *Ops! Nossos serviços estão instáveis no momento.*\nPor favor, tente novamente em instantes.";

            if (aiResult.IsAdvice)
                return $"💡 *Conselho do Nutri:*\n\n{(!string.IsNullOrWhiteSpace(aiResult.AdviceText) ? aiResult.AdviceText : "Como posso ajudar?")}";

            if (aiResult.RequiresUserClarification)
                return $"🤔 *Fiquei na dúvida sobre o seu prato:*\n{aiResult.ClarificationQuestion}";

            var prato = !string.IsNullOrWhiteSpace(aiResult.DishName) ? aiResult.DishName : aiResult.MealType;
            var calorias = aiResult.TotalMeal?.Calories ?? 0;
            var proteina = aiResult.TotalMeal?.ProteinG ?? 0;
            var carbo = aiResult.TotalMeal?.CarbsG ?? 0;
            var gordura = aiResult.TotalMeal?.FatG ?? 0;

            var msg = $"✅ *Refeição registrada:* {prato}\n🔥 *Calorias:* {calorias} kcal\n🥩 *Proteínas:* {proteina}g\n🍞 *Carboidratos:* {carbo}g\n🥑 *Gorduras:* {gordura}g\n\n";

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