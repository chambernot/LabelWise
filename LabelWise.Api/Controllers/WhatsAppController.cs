using LabelWise.Application.DTOs;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Interfaces.Persistence;
using LabelWise.Domain.Entities.Nutrition;
using LabelWise.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
        private readonly string _verifyToken;

        public WhatsAppController(
            INutritionService nutritionService,
            IWhatsAppSenderService whatsAppSender,
            IMetaMediaService metaMediaService,
            INutritionRepository nutritionRepository,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<WhatsAppController> logger)
        {
            _nutritionService = nutritionService;
            _whatsAppSender = whatsAppSender;
            _metaMediaService = metaMediaService;
            _nutritionRepository = nutritionRepository;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _verifyToken = configuration["MetaWhatsApp:VerifyToken"] ?? "labelwise_verify_token_123";
        }

        // Validação da URL exigida pela Meta (GET)
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

                // =========================================================================
                // 🛡️ BARREIRA DE SEGURANÇA (ANTI-CUSTO)
                // Verifica se o número do remetente possui uma dieta/meta cadastrada no banco.
                // Se não tiver, responde educadamente e encerra aqui sem chamar o Gemini.
                // =========================================================================
                var metaCadastrada = await _nutritionRepository.ObterMetaDiariaAsync(senderPhone, DateTime.UtcNow);
                if (metaCadastrada == null)
                {
                    _logger.LogWarning("[WhatsApp Security] ⛔ Mensagem bloqueada de número não cadastrado: {Phone}", senderPhone);

                    await _whatsAppSender.SendTextMessageAsync(
                        senderPhone,
                        "Olá! Este canal do LabelWise é de uso exclusivo para pacientes com acompanhamento nutricional ativo na clínica. Por favor, entre em contato com sua nutricionista para liberar o seu acesso. 🥗"
                    );

                    return Ok();
                }
                // =========================================================================

                string? textoDigitado = null;
                string? imagemBase64 = null;

                // 1. Tratamento por tipo de mensagem
                if (messageType == "text")
                {
                    textoDigitado = messagingEvent?.Text?.Body;
                }
                else if (messageType == "image" && messagingEvent?.Image?.Id != null)
                {
                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "📸 Analisando o seu prato, só um instante...");
                    imagemBase64 = await _metaMediaService.DownloadMediaAsBase64Async(messagingEvent.Image.Id);
                    textoDigitado = "Analise esta refeição da imagem.";
                }
                else if (messageType == "audio" && messagingEvent?.Audio?.Id != null)
                {
                    await _whatsAppSender.SendTextMessageAsync(senderPhone, "🎙️ Ouvindo o seu áudio e transcrevendo...");
                    var audioBytes = await _metaMediaService.DownloadMediaAsBytesAsync(messagingEvent.Audio.Id);

                    // Transcrevendo via Gemini
                    textoDigitado = await TranscreverAudioComGeminiAsync(audioBytes);

                    _logger.LogInformation("[WhatsAppController] 🎧 Áudio transcrito via Gemini para {Phone}: {Text}", senderPhone, textoDigitado);
                }
                else
                {
                    return Ok();
                }

                if (string.IsNullOrWhiteSpace(textoDigitado) && string.IsNullOrWhiteSpace(imagemBase64))
                    return Ok();

                // 2. Verifica se existe uma dúvida pendente para este usuário
                var contextoPendente = await _nutritionRepository.ObterClarificacaoPendenteAsync(senderPhone);

                string textoFinalParaIa = textoDigitado ?? string.Empty;
                string? imagemFinalParaIa = imagemBase64;

                if (contextoPendente != null)
                {
                    _logger.LogInformation("[WhatsAppController] 🔄 Resposta de clarificação detectada para o usuário {Phone}", senderPhone);

                    textoFinalParaIa = $"[Descrição anterior: {contextoPendente.OriginalTextInput}] " +
                                       $"[Pergunta de dúvida feita: {contextoPendente.ClarificationQuestion}] " +
                                       $"[Resposta complementar do usuário: {textoDigitado}]";

                    imagemFinalParaIa ??= contextoPendente.OriginalBase64Image;
                    await _nutritionRepository.RemoverClarificacaoPendenteAsync(senderPhone);
                }

                // 3. Envia para o serviço de nutrição
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

                // 4. Tratamento de pendência ou obtenção de status consolidado com sugestões
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
                else if (result.TotalMeal != null && !isSystemError)
                {
                    var dataHojeBr = DateTime.UtcNow.AddHours(-3);
                    statusDoDia = await _nutritionService.GetDailyStatusAndSuggestionAsync(senderPhone, dataHojeBr);
                }

                // 5. Envia a resposta final formatada
                var respostaTexto = FormatarRespostaParaWhatsApp(result, statusDoDia);
                await _whatsAppSender.SendTextMessageAsync(senderPhone, respostaTexto);

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
                    catch (Exception sendEx)
                    {
                        _logger.LogError(sendEx, "Falha ao enviar mensagem de erro amigável para o WhatsApp.");
                    }
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
                _logger.LogWarning("⚠️ Tentativa de acesso não autorizada ao Cron Job de lembretes.");
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
                        bool sucesso = await _whatsAppSender.SendTemplateReminderAsync(
                            telefone,
                            "Paciente",
                            mealTime
                        );

                        if (sucesso) enviados++;
                        else falhas++;
                    }
                    catch (Exception ex)
                    {
                        falhas++;
                        _logger.LogError(ex, "Erro ao enviar lembrete automático para o telefone {Phone}", telefone);
                    }
                }

                _logger.LogInformation("✅ Disparo em lote concluído. {Enviados} enviados, {Falhas} falhas.", enviados, falhas);
                return Ok(new { success = true, message = $"Rotina executada. Refeição cobrada: {mealTime}. Enviados: {enviados}, Falhas: {falhas}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro crítico ao processar o disparo em lote de lembretes.");
                return StatusCode(500, new { success = false, message = "Erro interno ao processar os lembretes." });
            }
        }

        [HttpPost("send-reminder")]
        public async Task<IActionResult> SendReminder(
            [FromQuery] string phone,
            [FromQuery] string userName = "Anderson",
            [FromQuery] string mealTime = "café da manhã")
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return BadRequest(new { success = false, message = "O número de telefone é obrigatório." });
            }

            bool enviado = await _whatsAppSender.SendTemplateReminderAsync(phone, userName, mealTime);

            if (enviado)
            {
                return Ok(new { success = true, message = $"Lembrete enviado com sucesso via Template Meta para o número {phone}!" });
            }

            return StatusCode(500, new
            {
                success = false,
                message = "Falha ao enviar o lembrete. Verifique os logs do sistema e confirme se o template 'lembrete_refeicao_dia' já foi APROVADO no painel da Meta."
            });
        }

        private async Task<string> TranscreverAudioComGeminiAsync(byte[] audioBytes)
        {
            var apiKey = _configuration["GeminiApiKey"] ?? _configuration["Gemini:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("Chave da API Gemini não configurada para a transcrição de áudio.");
            }

            var endpoint = _configuration["Gemini:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
            var model = _configuration["Model"] ?? "gemini-3.1-flash-lite";

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            var base64Audio = Convert.ToBase64String(audioBytes);
            var dataUri = $"data:audio/ogg;base64,{base64Audio}";

            var requestBody = new
            {
                model = model,
                temperature = 0.0,
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "Você é um transcritor de áudio profissional. Sua única tarefa é transcrever fielmente o áudio enviado em português do Brasil para texto. Retorne APENAS o texto transcrito, sem introduções, sem aspas, sem formatações extras e sem comentários."
                    },
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = "Por favor, transcreva o áudio a seguir:" },
                            new { type = "image_url", image_url = new { url = dataUri } }
                        }
                    }
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = content
            };
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await client.SendAsync(requestMessage);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Erro na transcrição via Gemini ({response.StatusCode}): {errorBody}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);

            var transcription = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            return transcription.Trim();
        }

        private string FormatarRespostaParaWhatsApp(
            MealAnalysisResponseDto aiResult,
            DailyStatusResponseDto? statusDoDia)
        {
            bool isSystemError = aiResult.ClarificationQuestion != null &&
                               aiResult.ClarificationQuestion.Contains("serviços de IA estão instáveis", StringComparison.OrdinalIgnoreCase);

            if (isSystemError)
            {
                return "⚠️ *Ops! Nossos serviços estão instáveis no momento.*\n\n" +
                       "Por favor, tente enviar sua foto ou descrição novamente em instantes.";
            }

            if (aiResult.RequiresUserClarification)
            {
                return $"🤔 *Fiquei na dúvida sobre o seu prato:*\n{aiResult.ClarificationQuestion}";
            }

            var prato = !string.IsNullOrWhiteSpace(aiResult.DishName) ? aiResult.DishName : aiResult.MealType;
            var calorias = aiResult.TotalMeal?.Calories ?? 0;
            var proteina = aiResult.TotalMeal?.ProteinG ?? 0;
            var carbo = aiResult.TotalMeal?.CarbsG ?? 0;
            var gordura = aiResult.TotalMeal?.FatG ?? 0;

            var msg = $"✅ *Refeição registrada:* {prato}\n" +
                      $"🔥 *Calorias:* {calorias} kcal\n" +
                      $"🥩 *Proteínas:* {proteina}g\n" +
                      $"🍞 *Carboidratos:* {carbo}g\n" +
                      $"🥑 *Gorduras:* {gordura}g\n\n";

            if (statusDoDia != null)
            {
                msg += $"📊 *SEU RESUMO DE HOJE*\n" +
                       $"• *Calorias:* {statusDoDia.Consumed.Calories} / {statusDoDia.Target.Calories} kcal\n";

                var faltamCal = statusDoDia.Remaining.Calories;
                msg += faltamCal <= 0
                    ? $"_⚠️ Você atingiu ou ultrapassou sua meta diária de calorias!_\n"
                    : $"_Faltam {faltamCal} kcal_\n";

                msg += $"• *Proteínas:* {statusDoDia.Consumed.ProteinG:F0}g / {statusDoDia.Target.ProteinG:F0}g\n" +
                       $"• *Carboidratos:* {statusDoDia.Consumed.CarbsG:F0}g / {statusDoDia.Target.CarbsG:F0}g\n" +
                       $"• *Gorduras:* {statusDoDia.Consumed.FatG:F0}g / {statusDoDia.Target.FatG:F0}g\n";

                if (faltamCal > 100 && statusDoDia.Suggestions != null && statusDoDia.Suggestions.Any())
                {
                    msg += "\n💡 *SUGESTÕES PARA A PRÓXIMA REFEIÇÃO:*\n";
                    foreach (var sugestao in statusDoDia.Suggestions)
                    {
                        msg += $"• {sugestao}\n";
                    }
                }
            }

            return msg;
        }
    }

    // Classes de Mapeamento do JSON da Meta Cloud API
    public class MetaWebhookPayload
    {
        public List<MetaEntry>? Entry { get; set; }
    }
    public class MetaEntry
    {
        public List<MetaChange>? Changes { get; set; }
    }
    public class MetaChange
    {
        public MetaValue? Value { get; set; }
    }
    public class MetaValue
    {
        public List<MetaMessage>? Messages { get; set; }
    }
    public class MetaMessage
    {
        public string? From { get; set; }
        public string? Type { get; set; }
        public MetaText? Text { get; set; }
        public MetaMedia? Image { get; set; }
        public MetaMedia? Audio { get; set; }
    }
    public class MetaText
    {
        public string? Body { get; set; }
    }
    public class MetaMedia
    {
        public string? Id { get; set; }
        public string? Mime_Type { get; set; }
    }
}