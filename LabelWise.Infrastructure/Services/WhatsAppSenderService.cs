using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
        private readonly IConfiguration _configuration;

        private readonly IHttpClientFactory _httpClientFactory;

        public WhatsAppSenderService(HttpClient httpClient, IConfiguration config, ILogger<WhatsAppSenderService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            // Pega as chaves, mas garante que não há espaços vazios copiados sem querer
            _accessToken = (config["MetaWhatsApp:AccessToken"] ?? string.Empty).Trim();
            _phoneNumberId = (config["MetaWhatsApp:PhoneNumberId"] ?? string.Empty).Trim();
        }


        public async Task<bool> SendTemplateReminderAsync(string toPhone, string userName, string mealTime)
        {
            try
            {
                var phoneNumberId = _configuration["MetaWhatsApp:PhoneNumberId"];
                var accessToken = _configuration["MetaWhatsApp:AccessToken"];

                if (string.IsNullOrWhiteSpace(phoneNumberId) || string.IsNullOrWhiteSpace(accessToken))
                {
                    _logger.LogError("[WhatsAppSenderService] ❌ PhoneNumberId ou AccessToken não configurado.");
                    return false;
                }

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

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

                var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                // Dispara para a Graph API da Meta
                var response = await client.PostAsync($"https://graph.facebook.com/v20.0/{phoneNumberId}/messages", jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("[WhatsAppSenderService] 🚀 Template de lembrete enviado com sucesso para {Phone}", toPhone);
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
                to = phone, // Certifique-se de que o telefone não tenha o '+' aqui
                type = "text",
                text = new { body = message }
            };

            // Monta a requisição isolada (evita cache de token antigo do .NET)
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