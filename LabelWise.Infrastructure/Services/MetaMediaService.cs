using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace LabelWise.Infrastructure.Services
{
    public class MetaMediaService : IMetaMediaService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<MetaMediaService> _logger;
        private readonly string _accessToken;

        public MetaMediaService(HttpClient httpClient, IConfiguration config, ILogger<MetaMediaService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _accessToken = (config["MetaWhatsApp:AccessToken"] ?? string.Empty).Trim();
        }

        // Método para retornar em Base64 (usado nas imagens para o Gemini/OpenAI)
        public async Task<string> DownloadMediaAsBase64Async(string mediaId)
        {
            var imageBytes = await DownloadMediaAsBytesAsync(mediaId);
            if (imageBytes == null || imageBytes.Length == 0) return null;

            return Convert.ToBase64String(imageBytes);
        }

        // NOVO: Método para retornar os bytes brutos (usado nos áudios para o Whisper)
        public async Task<byte[]> DownloadMediaAsBytesAsync(string mediaId)
        {
            try
            {
                // Etapa 1: Obter a URL temporária de download da mídia a partir do ID na Graph API
                var urlRequest = new HttpRequestMessage(HttpMethod.Get, $"https://graph.facebook.com/v25.0/{mediaId}");
                urlRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var urlResponse = await _httpClient.SendAsync(urlRequest);
                if (!urlResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[MetaMedia] Falha ao obter URL da mídia {MediaId}. Status: {Status}", mediaId, urlResponse.StatusCode);
                    return null;
                }

                var jsonString = await urlResponse.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(jsonString);

                if (!jsonDoc.RootElement.TryGetProperty("url", out var urlElement)) return null;
                var mediaUrl = urlElement.GetString();

                if (string.IsNullOrEmpty(mediaUrl)) return null;

                // Etapa 2: Fazer o download do arquivo binário real usando a URL obtida
                var downloadRequest = new HttpRequestMessage(HttpMethod.Get, mediaUrl);
                downloadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var downloadResponse = await _httpClient.SendAsync(downloadRequest);
                if (!downloadResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[MetaMedia] Falha ao baixar os dados binários da mídia {MediaId}. Status: {Status}", mediaId, downloadResponse.StatusCode);
                    return null;
                }

                return await downloadResponse.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MetaMedia] Erro crítico ao baixar a mídia {MediaId}", mediaId);
                return null;
            }
        }
    }
}