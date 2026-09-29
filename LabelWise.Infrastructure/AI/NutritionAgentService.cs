using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace LabelWise.Infrastructure.Services;

public sealed class NutritionAgentService : INutritionAgentService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NutritionAgentService> _logger;

    private readonly string _geminiApiKey;
    private readonly string _geminiEndpoint;
    private readonly string _geminiModel;

    private readonly string _openAiApiKey;
    private readonly string _openAiEndpoint;
    private readonly string _openAiModel;

    private const string SystemPrompt =
        "Você é um especialista MÁSTER em nutrição, análise de alimentos e Visão Computacional. " +
        "Sua função é analisar entradas multimodais (imagens de pratos de comida, áudios transcritos ou textos livres) " +
        "e converter o conteúdo em uma estrutura de dados JSON precisa com contagem de calorias e macronutrientes. " +
        "Você sempre deve retornar APENAS um JSON válido, sem texto fora dele, sem markdown de bloco de código (```json).";

    private const string UserPromptInstructions = @"TAREFA: Analisar a refeição informada (imagem, texto ou áudio) e retornar a quebra nutricional em JSON.

════════════════════════════════════════════════════════════════
REGRA 1 — PROCESSAMENTO E ESTIMATIVA DE PORÇÕES
1. Identifique o nome geral do prato principal ou da refeição (ex: ""Espaguete ao alho e óleo"") e preencha no campo ""dishName"".
2. Identifique cada item alimentício individualmente na lista ""items"".
3. Estime o peso/volume individual em gramas (g) ou mililitros (ml).
4. Considere métodos de preparo.
5. Calcule calorias e macronutrientes com base na tabela TACO/TBCA.
6. Atribua um score de confiança (confidenceScore de 0.0 a 1.0) para cada alimento.

════════════════════════════════════════════════════════════════
REGRA 2 — TRATAMENTO DE INCERTEZAS (CLARIFICAÇÃO)
Se a confiança geral for menor que 0.60:
- Defina ""requiresUserClarification"": true
- Adicione uma pergunta curta em ""clarificationQuestion"".
Caso contrário, ""requiresUserClarification"": false e ""clarificationQuestion"": null.

ESTRUTURA DE SAÍDA OBRIGATÓRIA (JSON PURO)
{
  ""mealType"": ""Café da Manhã"" | ""Almoço"" | ""Lanche"" | ""Jantar"" | ""Ceia"",
  ""dishName"": ""string"",
  ""items"": [
    {
      ""foodName"": ""string"",
      ""portionDescription"": ""string"",
      ""estimatedWeightG"": number,
      ""calories"": number,
      ""proteinG"": number,
      ""carbsG"": number,
      ""fatG"": number,
      ""confidenceScore"": number
    }
  ],
  ""totalMeal"": {
    ""calories"": number,
    ""proteinG"": number,
    ""carbsG"": number,
    ""fatG"": number
  },
  ""requiresUserClarification"": boolean,
  ""clarificationQuestion"": string
}";

    private const string DietReaderSystemPrompt = @"Você é um Assistente Clínico Especialista em Nutrição e Extração de Dados.
Sua função é analisar prescrições dietéticas e extrair:
1. Os ALVOS NUTRICIONAIS DIÁRIOS TOTAIS do paciente.
2. O PLANO DE REFEIÇÕES DETALHADO.
Retorne APENAS um JSON válido, sem texto fora dele, sem blocos markdown (```json).

FORMATO DE SAÍDA OBRIGATÓRIO:
{
  ""targetCalories"": number,
  ""targetProteinG"": number,
  ""targetCarbsG"": number,
  ""targetFatG"": number,
  ""dietaryRestrictions"": ""string ou null"",
  ""favoriteFoods"": ""string ou null"",
  ""prescribedMealPlan"": ""string ou null""
}";

    public NutritionAgentService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<NutritionAgentService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        _geminiApiKey = (GetConfigValue(configuration, "GeminiApiKey", "Gemini:ApiKey") ?? throw new ArgumentNullException("ApiKey do Gemini ausente.")).Trim();
        _geminiEndpoint = (GetConfigValue(configuration, "GeminiEndpoint", "Gemini:Endpoint") ?? "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions").Trim();

        // Mantém a leitura da sua chave original "Model" do appsettings.
        _geminiModel = (GetConfigValue(configuration, "Model", "Gemini:Model") ?? "gemini-1.5-flash").Trim();

        _openAiApiKey = (configuration["OpenAiVision:ApiKey"] ?? "fallback-key").Trim();
        _openAiEndpoint = (configuration["OpenAiVision:Endpoint"] ?? "https://api.openai.com/v1/chat/completions").Trim();
        _openAiModel = (configuration["OpenAiVision:Model"] ?? "gpt-4o").Trim();
    }

    public async Task<List<string>> GenerateProactiveSuggestionsAsync(MacroSummaryDto remainingBalance, string nextMealType, List<string>? pratosJaConsumidos = null)
    {
        try
        {
            var historicoPratos = (pratosJaConsumidos != null && pratosJaConsumidos.Any())
                ? string.Join(", ", pratosJaConsumidos)
                : "Nenhum alimento registrado até o momento.";

            const string systemPrompt = @"Você é um Copiloto Nutricional Proativo. Retorne APENAS um JSON com a chave ""suggestions"" contendo um array de 3 strings.";
            var userPrompt = $"PRÓXIMA REFEIÇÃO: {nextMealType}\nSALDO: {remainingBalance.Calories} kcal...\nALIMENTOS JÁ CONSUMIDOS: {historicoPratos}";

            var requestBody = new
            {
                model = _geminiModel,
                temperature = 0.6,
                max_tokens = 4000,
                messages = new object[]
                {
                    // Unificado para evitar bugs de roteamento
                    new { role = "user", content = $"{systemPrompt}\n\n{userPrompt}" }
                }
            };

            var responseString = await ExecuteWithFailoverAsync(_geminiEndpoint, _geminiApiKey, _geminiModel, "Gemini", _openAiEndpoint, _openAiApiKey, _openAiModel, "OpenAiVision", requestBody);
            var jsonContent = ExtractJsonFromResponse(responseString);

            using var document = JsonDocument.Parse(jsonContent);
            if (document.RootElement.TryGetProperty("suggestions", out var suggestionsElement))
            {
                return JsonSerializer.Deserialize<List<string>>(suggestionsElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? GetFallbackSuggestions();
            }

            return GetFallbackSuggestions();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NutritionAgentService] Erro proativo.");
            return GetFallbackSuggestions();
        }
    }

    public async Task<MealAnalysisResponseDto> ExtractMealDataAsync(ParseMealRequestDto request)
    {
        try
        {
            var geminiBody = BuildRequestBody(request, _geminiModel, SystemPrompt, UserPromptInstructions);
            string responseString;

            try
            {
                responseString = await SendRequestAsync(_geminiEndpoint, _geminiApiKey, _geminiModel, "Gemini", geminiBody, TimeSpan.FromSeconds(45));
            }
            catch (Exception exGemini)
            {
                _logger.LogWarning(exGemini, "[NutritionAgentService] Falha no Gemini. Acionando OpenAI Fallback...");
                var openAiBody = BuildRequestBody(request, _openAiModel, SystemPrompt, UserPromptInstructions);
                responseString = await SendRequestAsync(_openAiEndpoint, _openAiApiKey, _openAiModel, "OpenAiVision", openAiBody, TimeSpan.FromSeconds(60));
            }

            var jsonContent = ExtractJsonFromResponse(responseString);
            var result = JsonSerializer.Deserialize<MealAnalysisResponseDto>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return result ?? throw new Exception("Falha ao parsear JSON");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no processamento da refeição.");
            return new MealAnalysisResponseDto("Indefinido", "Indefinido", new List<FoodItemDto>(), new MacroSummaryDto(0, 0, 0, 0), true, "Desculpe, a IA está instável.");
        }
    }

    public async Task<ExtractDietGoalResponseDto> ExtractDietGoalsFromDocumentAsync(ExtractDietGoalRequestDto request)
    {
        var userContentList = new List<object>
        { 
            // Unificado no User Role
            new { type = "text", text = $"{DietReaderSystemPrompt}\n\nExtraia os alvos nutricionais e retorne APENAS um JSON válido." }
        };

        if (!string.IsNullOrWhiteSpace(request.TextInput))
            userContentList.Add(new { type = "text", text = $"DIETA: {request.TextInput}" });

        if (!string.IsNullOrWhiteSpace(request.Base64Image))
        {
            var imageBase64 = request.Base64Image.Contains(",") ? request.Base64Image : $"data:image/jpeg;base64,{request.Base64Image}";
            // Removido o 'detail = "high"' que causava o crash no Gemini
            userContentList.Add(new { type = "image_url", image_url = new { url = imageBase64 } });
        }

        var requestBody = new
        {
            model = _geminiModel,
            temperature = 0.1,
            max_tokens = 1500,
            messages = new object[]
            {
                new { role = "user", content = userContentList.ToArray() }
            }
        };

        var responseString = await ExecuteWithFailoverAsync(_geminiEndpoint, _geminiApiKey, _geminiModel, "Gemini", _openAiEndpoint, _openAiApiKey, _openAiModel, "OpenAiVision", requestBody);
        var jsonContent = ExtractJsonFromResponse(responseString);

        return JsonSerializer.Deserialize<ExtractDietGoalResponseDto>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? throw new Exception("Falha ao extrair dieta.");
    }

    private async Task<string> SendRequestAsync(string endpoint, string apiKey, string model, string clientName, object requestBodyObj, TimeSpan timeout)
    {
        var client = _httpClientFactory.CreateClient(clientName);
        var content = new StringContent(JsonSerializer.Serialize(requestBodyObj), Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var cts = new CancellationTokenSource(timeout);
        var response = await client.SendAsync(requestMessage, cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"API request to {clientName} ({model}) failed with Status {response.StatusCode}: {errorContent}");
        }

        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> ExecuteWithFailoverAsync(string primaryEndpoint, string primaryKey, string primaryModel, string primaryClient, string fallbackEndpoint, string fallbackKey, string fallbackModel, string fallbackClient, object baseRequestBody)
    {
        try
        {
            var primaryBody = UpdateModelInBody(baseRequestBody, primaryModel);
            return await SendRequestAsync(primaryEndpoint, primaryKey, primaryModel, primaryClient, primaryBody, TimeSpan.FromSeconds(45));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha no provedor primário. Tentando fallback...");
            var fallbackBody = UpdateModelInBody(baseRequestBody, fallbackModel);
            return await SendRequestAsync(fallbackEndpoint, fallbackKey, fallbackModel, fallbackClient, fallbackBody, TimeSpan.FromSeconds(60));
        }
    }

    private object UpdateModelInBody(object originalBody, string newModel)
    {
        var json = JsonSerializer.Serialize(originalBody);
        var node = JsonNode.Parse(json);
        if (node != null)
        {
            node["model"] = newModel;
            return node;
        }
        return originalBody;
    }

    private object BuildRequestBody(ParseMealRequestDto request, string targetModel, string sysPrompt, string userPrompt)
    {
        var userContentList = new List<object>
        {
            new { type = "text", text = $"{sysPrompt}\n\n{userPrompt}" }
        };

        if (!string.IsNullOrWhiteSpace(request.TextInput))
            userContentList.Add(new { type = "text", text = $"ENTRADA: {request.TextInput}" });

        if (!string.IsNullOrWhiteSpace(request.Base64Image))
        {
            var imageBase64 = request.Base64Image.Contains(",") ? request.Base64Image : $"data:image/jpeg;base64,{request.Base64Image}";
            userContentList.Add(new { type = "image_url", image_url = new { url = imageBase64 } });
        }

        return new
        {
            model = targetModel,
            temperature = 0.1,
            max_tokens = 3000,
            messages = new object[]
            {
                new { role = "user", content = userContentList.ToArray() }
            }
        };
    }

    private string ExtractJsonFromResponse(string responseString)
    {
        using var document = JsonDocument.Parse(responseString);
        var choice = document.RootElement.GetProperty("choices")[0];

        var rawText = choice.GetProperty("message").GetProperty("content").GetString() ?? "";

        rawText = rawText.Trim();
        if (rawText.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            rawText = rawText.Substring(7);
        if (rawText.StartsWith("```"))
            rawText = rawText.Substring(3);
        if (rawText.EndsWith("```"))
            rawText = rawText.Substring(0, rawText.Length - 3);

        return rawText.Trim();
    }

    private static List<string> GetFallbackSuggestions()
    {
        return new List<string>
        {
            "Opção Rápida: Omelete de 3 ovos",
            "Opção Completa: Frango grelhado e salada",
            "Opção Prática: Shake de whey protein"
        };
    }

    private static string? GetConfigValue(IConfiguration config, string primaryKey, string secondaryKey)
    {
        var val = config[primaryKey];
        if (!string.IsNullOrWhiteSpace(val)) return val;

        val = config[secondaryKey];
        if (!string.IsNullOrWhiteSpace(val)) return val;

        return null;
    }
}