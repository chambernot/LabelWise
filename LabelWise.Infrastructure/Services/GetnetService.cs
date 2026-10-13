using System;
using System.Collections.Generic;
using System.Text;

namespace LabelWise.Infrastructure.Services
{
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public interface IGetnetService
    {
        Task<string?> GerarLinkPagamentoAsync(string customerPhone, string customerName, decimal valor);
    }

    public class GetnetService : IGetnetService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GetnetService> _logger;
        private readonly bool _isProduction;

        public GetnetService(HttpClient httpClient, IConfiguration configuration, ILogger<GetnetService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _isProduction = _configuration.GetValue<bool>("Getnet:IsProduction");
        }

        // URLs oficiais da Getnet Global API / Payment Links
        private string GetBaseUrl() => _isProduction
            ? "https://api.getnet.com.br"
            : "https://api-sbx.pre.globalgetnet.com";

        private async Task<string?> GetAccessTokenAsync()
        {
            try
            {
                var clientId = _configuration["Getnet:ClientId"];
                var clientSecret = _configuration["Getnet:ClientSecret"];
                var requestUrl = $"{GetBaseUrl()}/auth/oauth/v2/token";

                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));

                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                request.Content = new FormUrlEncodedContent(new[]
                {
                new KeyValuePair<string, string>("scope", "oob"),
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            });

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Erro ao obter Token da Getnet: {Error}", errorContent);
                    return null;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                return doc.RootElement.GetProperty("access_token").GetString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exceção ao autenticar na Getnet.");
                return null;
            }
        }

        public async Task<string?> GerarLinkPagamentoAsync(string customerPhone, string customerName, decimal valor)
        {
            try
            {
                var token = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token)) return null;

                // Endpoint exato da Payment Link API (conforme documentação oficial)
                var requestUrl = $"{GetBaseUrl()}/dpy/payment-link/v1/payment-links";

                var payload = new
                {
                    seller_id = _configuration["Getnet:SellerId"],
                    expiration = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-ddTHH:mm:ss"),
                    max_orders = 1,
                    currency = "BRL",
                    order_id = $"sub_{customerPhone}_{DateTime.UtcNow.Ticks}",
                    customer = new
                    {
                        name = customerName,
                        phone_number = customerPhone
                    },
                    products = new[]
                    {
                    new
                    {
                        product_type = "digital_goods",
                        title = "Nutrição Fácil - Assinatura Mensal",
                        amount = (int)(valor * 100), // Getnet exige em cêntimos (ex: 2990 = R$ 29,90)
                        quantity = 1
                    }
                }
                };

                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Erro ao gerar link de pagamento na Getnet: {Error}", errorContent);
                    return null;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);

                // Retorna o link web onde o cliente finaliza o pagamento
                if (doc.RootElement.TryGetProperty("payment_url", out var urlProp))
                {
                    return urlProp.GetString();
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exceção ao gerar link de pagamento.");
                return null;
            }
        }
    }
}
