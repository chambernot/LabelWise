using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
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

            // Pega as chaves com segurança e remove espaços em branco
            _accessToken = (config["MetaWhatsApp:AccessToken"] ?? string.Empty).Trim();
            _phoneNumberId = (config["MetaWhatsApp:PhoneNumberId"] ?? string.Empty).Trim();
        }

        public async Task<bool> SendTemplateReminderAsync(string toPhone, string userName, string mealTime)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_phoneNumberId) || string.IsNullOrWhiteSpace(_accessToken))
                {
                    _logger.LogError("[WhatsAppSenderService] ❌ PhoneNumberId ou AccessToken não configurado nas variáveis de ambiente.");
                    return false;
                }

                var endpoint = $"https://graph.facebook.com/v20.0/{_phoneNumberId}/messages";

                // Payload no formato esperado pela Meta para envio de templates
                var payload = new
                {
                    messaging_product = "whatsapp",
                    to = toPhone,
                    type = "template",
                    template = new
                    {
                        name = "lembrete_refeicao_diaria",
                        language = new { code = "pt_BR" },
                        components = new[]
                        {
                            new
                            {
                                type = "body",
                                parameters = new object[]
                                {
                                    new { type = "text", text = userName }, // Preenche o {{1}}
                                    new { type = "text", text = mealTime }  // Preenche o {{2}}
                                }
                            }
                        }
                    }
                };

                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                // Dispara para a Graph API da Meta usando o HttpClient injetado
                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("[WhatsAppSenderService] 🚀 Template enviado com sucesso para {Phone}. Resposta Meta: {Body}", toPhone, responseBody);
                    return true;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("[WhatsAppSenderService] ❌ Falha ao enviar template Meta ({StatusCode}): {Error}", response.StatusCode, errorContent);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WhatsAppSenderService] ❌ Exceção ao enviar template de lembrete para {Phone}", toPhone);
                return false;
            }
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
                _logger.LogError("[MetaWhatsApp] ❌ Falha. Status: {Status}. Detalhes: {Error}", response.StatusCode, errorBody);
            }
            else
            {
                _logger.LogInformation("[MetaWhatsApp] ✅ Mensagem enviada com sucesso para {Phone}", phone);
            }
        }
    }
}