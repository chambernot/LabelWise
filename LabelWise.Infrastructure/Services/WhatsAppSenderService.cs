using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LabelWise.Infrastructure.Services
{
    public class WhatsAppSenderService : IWhatsAppSenderService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WhatsAppSenderService> _logger;
        private readonly string _accessToken;
        private readonly string _phoneNumberId;

        public WhatsAppSenderService(HttpClient httpClient, IConfiguration config, ILogger<WhatsAppSenderService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _accessToken = (config["MetaWhatsApp:AccessToken"] ?? string.Empty).Trim();
            _phoneNumberId = (config["MetaWhatsApp:PhoneNumberId"] ?? string.Empty).Trim();
        }

        // Método genérico que aceita o nome do template e qualquer quantidade de variáveis
        public async Task<bool> SendTemplateMessageAsync(string toPhone, string templateName, params string[] templateArgs)
        {
            var result = await SendTemplateMessageWithResultAsync(toPhone, templateName, templateArgs);
            return result.Success;
        }

        public async Task<WhatsAppSendResult> SendTemplateMessageWithResultAsync(string toPhone, string templateName, params string[] templateArgs)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_phoneNumberId) || string.IsNullOrWhiteSpace(_accessToken))
                {
                    _logger.LogError("[WhatsAppSenderService] ❌ PhoneNumberId ou AccessToken não configurado.");
                    return new WhatsAppSendResult(false, null, "config", "PhoneNumberId ou AccessToken do WhatsApp não configurado.");
                }

                var endpoint = $"https://graph.facebook.com/v20.0/{_phoneNumberId}/messages";

                // Prepara os componentes dinamicamente (se houver argumentos)
                object[] componentsArray = Array.Empty<object>();
                if (templateArgs != null && templateArgs.Length > 0)
                {
                    var parameters = templateArgs.Select(arg => new { type = "text", text = arg }).ToArray();
                    componentsArray = new object[]
                    {
                        new { type = "body", parameters = parameters }
                    };
                }

                var payload = new
                {
                    messaging_product = "whatsapp",
                    to = toPhone,
                    type = "template",
                    template = new
                    {
                        name = templateName,
                        language = new { code = "pt_BR" },
                        components = componentsArray
                    }
                };

                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("[WhatsAppSenderService] 🚀 Template '{Template}' enviado para {Phone}.", templateName, toPhone);
                    return new WhatsAppSendResult(true, LerMessageId(responseBody), null, null);
                }

                _logger.LogError("[WhatsAppSenderService] ❌ Falha ao enviar template Meta ({StatusCode}): {Error}", response.StatusCode, responseBody);
                var (codigo, mensagem) = LerErro(responseBody);
                return new WhatsAppSendResult(false, null, codigo ?? ((int)response.StatusCode).ToString(), mensagem ?? $"HTTP {(int)response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WhatsAppSenderService] ❌ Exceção ao enviar template '{Template}' para {Phone}", templateName, toPhone);
                return new WhatsAppSendResult(false, null, "exception", ex.Message);
            }
        }

        // Resposta de sucesso da Meta: { "messages": [ { "id": "wamid...." } ] }
        private static string? LerMessageId(string responseBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("messages", out var messages) &&
                    messages.ValueKind == JsonValueKind.Array && messages.GetArrayLength() > 0 &&
                    messages[0].TryGetProperty("id", out var id))
                {
                    return id.GetString();
                }
            }
            catch (JsonException) { }
            return null;
        }

        // Resposta de erro da Meta: { "error": { "code": 132001, "message": "...", "error_data": { "details": "..." } } }
        private static (string? Codigo, string? Mensagem) LerErro(string responseBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("error", out var error))
                {
                    string? codigo = error.TryGetProperty("code", out var c) ? c.ToString() : null;
                    string? mensagem = error.TryGetProperty("error_data", out var data) && data.TryGetProperty("details", out var details)
                        ? details.GetString()
                        : error.TryGetProperty("message", out var m) ? m.GetString() : null;
                    return (codigo, mensagem);
                }
            }
            catch (JsonException) { }
            return (null, null);
        }

        public async Task SendTextMessageAsync(string phone, string message)
        {
            var endpoint = $"https://graph.facebook.com/v25.0/{_phoneNumberId}/messages";

            var payload = new
            {
                messaging_product = "whatsapp",
                to = phone,
                type = "text",
                text = new { body = message }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("[MetaWhatsApp] ❌ Falha texto. Status: {Status}. Detalhes: {Error}", response.StatusCode, errorBody);
            }
        }
    }
}