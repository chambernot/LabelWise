using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LabelWise.Application.Configuration;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.IngredientAnalysis;
using LabelWise.Infrastructure.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabelWise.Infrastructure.AI;

public sealed class OpenAIIngredientAnalysisService : IOpenAIIngredientAnalysisService
{
    private const string SystemPrompt = "Você é um extrator OCR/visão estrito para ingredientes e restrições alimentares. Trabalhe apenas com texto e elementos VISÍVEIS na imagem ou no contexto OCR fornecido. Nunca invente ingredientes, alergênicos ou claims. Responda apenas JSON válido.";

    private const string UserPrompt = @"TAREFA: analisar a imagem de uma embalagem de alimento para extrair ingredientes, alergênicos e claims alimentares visíveis.

REGRAS OBRIGATÓRIAS:
- Não exige tabela nutricional.
- Use apenas elementos visíveis na imagem ou no contexto OCR fornecido.
- Se não houver lista de ingredientes legível, retorne ingredientsDetected como array vazio.
- Não use conhecimento prévio do produto.
- Não complete palavras ilegíveis.
- Se a lista estiver cortada, parcial, com reflexo ou baixa legibilidade, inclua um aviso em warnings.
- Nunca afirme certeza absoluta sem evidência textual forte.
- Preserve claims literais quando visíveis, como: contém leite, contém glúten, pode conter, não contém lactose, vegano, vegetariano, plant based, sem lactose, sem glúten.
- Alergênicos devem vir apenas de ingredientes/claims detectados.
- Diferencie CONTÉM de PODE CONTER: avisos como ""pode conter"", ""traços de"" ou ""fabricado em equipamento"" devem ir em claims, não como ingrediente confirmado.
- Não extraia ingredientes da tabela nutricional, como sódio, carboidrato, gordura, kcal, mg, g ou %VD.
- Frases como ""equivale ao açúcar"", ""poder adoçante"" ou ""substitui açúcar"" não significam açúcar adicionado.

JSON de saída:
{
  ""productName"": string | null,
  ""brand"": string | null,
  ""ingredientsDetected"": [string],
  ""allergens"": [string],
  ""claims"": [string],
  ""rawExtractedText"": [string],
  ""warnings"": [string]
}

Não retorne texto fora do JSON.";

    private readonly HttpClient _httpClient;
    private readonly AzureOpenAiVisionOptions _options;
    private readonly ILogger<OpenAIIngredientAnalysisService> _logger;

    public OpenAIIngredientAnalysisService(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureOpenAiVisionOptions> options,
        ILogger<OpenAIIngredientAnalysisService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("OpenAI");
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.Endpoint))
            _httpClient.BaseAddress = new Uri(_options.Endpoint);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        _httpClient.Timeout = TimeSpan.FromSeconds(35);
    }

    public async Task<IngredientExtractionResult?> AnalyzeAsync(
        byte[] imageBytes,
        string? mimeType,
        string? ocrContext,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("[IngredientAnalysis.OpenAI] OpenAI Vision não configurado.");
            return null;
        }

        try
        {
            var base64Image = Convert.ToBase64String(imageBytes);
            var resolvedMimeType = ResolveMimeType(mimeType, base64Image);
            var prompt = string.IsNullOrWhiteSpace(ocrContext)
                ? UserPrompt
                : $"{UserPrompt}\n\nCONTEXTO OCR AUXILIAR, extraído da mesma imagem. Use apenas se compatível com a imagem:\n{ocrContext}";

            var requestBody = new
            {
                model = _options.Model,
                messages = new object[]
                {
                    new { role = "system", content = SystemPrompt },
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = prompt },
                            new
                            {
                                type = "image_url",
                                image_url = new { url = $"data:{resolvedMimeType};base64,{base64Image}", detail = "high" }
                            }
                        }
                    }
                },
                max_tokens = 1200,
                temperature = 0
            };

            using var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("", content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "[IngredientAnalysis.OpenAI] Falha na requisição. Status={Status}, Body={Body}",
                    response.StatusCode,
                    responseBody);
                return null;
            }

            return ParseResponse(responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IngredientAnalysis.OpenAI] Erro inesperado na análise de ingredientes.");
            return null;
        }
    }

    private IngredientExtractionResult? ParseResponse(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
                return null;

            var json = ExtractJson(content);
            return JsonSerializer.Deserialize<IngredientExtractionResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[IngredientAnalysis.OpenAI] Falha ao parsear resposta.");
            return null;
        }
    }

    private static string ExtractJson(string value)
    {
        var clean = value.Trim();
        if (clean.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = clean.IndexOf('\n');
            var lastFence = clean.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewLine >= 0 && lastFence > firstNewLine)
                clean = clean[(firstNewLine + 1)..lastFence].Trim();
        }

        var start = clean.IndexOf('{');
        var end = clean.LastIndexOf('}');
        return start >= 0 && end > start ? clean[start..(end + 1)] : clean;
    }

    private static string ResolveMimeType(string? mimeType, string base64Image)
    {
        if (!string.IsNullOrWhiteSpace(mimeType) && mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return mimeType;

        return ImageFormatHelper.DetectMimeTypeFromBase64(base64Image);
    }
}
