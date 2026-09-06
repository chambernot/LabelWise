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
using System.Threading;
using System.Threading.Tasks;

namespace LabelWise.Infrastructure.Services;

/// <summary>
/// Implementação de análise nutricional multimodal com estratégia de Fallback (Gemini -> OpenAI) e Tratamento Resiliente de Erros.
/// </summary>
public sealed class NutritionAgentService : INutritionAgentService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NutritionAgentService> _logger;

    // Configurações do Gemini (Primário)
    private readonly string _geminiApiKey;
    private readonly string _geminiEndpoint;
    private readonly string _geminiModel;

    // Configurações da OpenAI (Fallback)
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
════════════════════════════════════════════════════════════════
1. Identifique o nome geral do prato principal ou da refeição (ex: ""Espaguete ao alho e óleo"", ""Frango com batata doce"") e preencha no campo ""dishName"".
2. Identifique cada item alimentício individualmente na lista ""items"".
3. Estime o peso/volume individual em gramas (g) ou mililitros (ml). Caso a quantidade não seja informada, utilize porções padrão da culinária brasileira/latino-americana (ex: 1 colher de servir de arroz = 100g; 1 concha de feijão = 130g; 1 bife de frango = 120g; 1 ovo = 50g).
4. Considere métodos de preparo (frito em óleo aumenta gordura; grelhado/assado mantém padrão).
5. Calcule calorias e macronutrientes (proteínas, carboidratos, gorduras) com base na tabela TACO/TBCA/USDA tanto para os itens quanto para o total.
6. Atribua um score de confiança (confidenceScore de 0.0 a 1.0) para cada alimento.

════════════════════════════════════════════════════════════════
REGRA 2 — TRATAMENTO DE INCERTEZAS (CLARIFICAÇÃO)
════════════════════════════════════════════════════════════════
Se a confiança geral for menor que 0.60 (ex: molhos não identificados, recheios ocultos, imagem borrada):
- Defina ""requiresUserClarification"": true
- Adicione uma pergunta curta em ""clarificationQuestion"" (ex: ""O frango do seu prato é grelhado ou empanado?"").
Caso contrário, ""requiresUserClarification"": false e ""clarificationQuestion"": null.

════════════════════════════════════════════════════════════════
ESTRUTURA DE SAÍDA OBRIGATÓRIA (JSON PURO)
════════════════════════════════════════════════════════════════
{
  ""mealType"": ""Café da Manhã"" | ""Almoço"" | ""Lanche"" | ""Jantar"" | ""Ceia"",
  ""dishName"": ""string (ex: Espaguete ao alho e óleo)"",
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

    public NutritionAgentService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<NutritionAgentService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        // Resgate das chaves do Gemini (Primário)
        _geminiApiKey = GetConfigValue(configuration, "GeminiApiKey", "GeminiApiKey")
            ?? throw new ArgumentNullException("ApiKey de IA (Gemini) não encontrada no appsettings.");

        _geminiEndpoint = GetConfigValue(configuration, "Gemini:Endpoint", "OpenAi:Endpoint")
            ?? "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";

        _geminiModel = GetConfigValue(configuration, "Model", "Model")
            ?? "gemini-1.5-flash";

        // Resgate das chaves da OpenAI (Fallback)
        _openAiApiKey = configuration["OpenAiVision:ApiKey"]
            ?? throw new ArgumentNullException("OpenAiVision:ApiKey não encontrada no appsettings.");

        _openAiEndpoint = configuration["OpenAiVision:Endpoint"]
            ?? "https://api.openai.com/v1/chat/completions";

        _openAiModel = configuration["OpenAiVision:Model"]
            ?? "gpt-4o";
    }

    public async Task<List<string>> GenerateProactiveSuggestionsAsync(
    MacroSummaryDto remainingBalance,
    string nextMealType,
    List<string>? pratosJaConsumidos = null)
    {
        try
        {
            _logger.LogInformation("[NutritionAgentService] 🤖 Gerando sugestões proativas de refeição...");

            var historicoPratos = (pratosJaConsumidos != null && pratosJaConsumidos.Any())
                ? string.Join(", ", pratosJaConsumidos)
                : "Nenhum alimento registrado até o momento.";

            const string systemPrompt = @"Você é um Copiloto Nutricional Proativo.
Sua função é analisar o saldo restante de macronutrientes do dia e o histórico de refeições para gerar exatamente 3 opções práticas de refeição para a culinária brasileira/latino-americana.
Retorne APENAS um JSON com a chave ""suggestions"" contendo um array de 3 strings, sem textos adicionais ou blocos markdown (```json).

Exemplo de formato esperado:
{
  ""suggestions"": [
    ""Opção 1 Rápida: Omelete de 3 ovos com 50g de queijo minas (~300 kcal | 26g Prot)"",
    ""Opção 2 Completa: 150g de peito de frango grelhado + salada verde (~320 kcal | 40g Prot)"",
    ""Opção 3 Delivery: 2 espetinhos de carne/frango sem acompanhamento pesado (~350 kcal | 38g Prot)""
  ]
}";

            var userPrompt = $@"PRÓXIMA REFEIÇÃO: {nextMealType}
SALDO RESTANTE PARA HOJE:
- Calorias: {remainingBalance.Calories} kcal
- Proteínas: {remainingBalance.ProteinG}g
- Carboidratos: {remainingBalance.CarbsG}g
- Gorduras: {remainingBalance.FatG}g

ALIMENTOS JÁ CONSUMIDOS HOJE (EVITE REPETIR OS MESMOS ALIMENTOS OU PROTEÍNAS):
{historicoPratos}

Gere 3 opções realistas e práticas alinhadas a esse saldo e ao histórico de refeições.";

            var requestBody = new
            {
                model = _geminiModel,
                temperature = 0.6,
                max_tokens = 4000,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                }
            };

            var responseString = await ExecuteWithFailoverAsync(
                _geminiEndpoint, _geminiApiKey, _geminiModel, "Gemini",
                _openAiEndpoint, _openAiApiKey, _openAiModel, "OpenAiVision",
                requestBody);

            var jsonContent = ExtractJsonFromResponse(responseString);

            using var document = JsonDocument.Parse(jsonContent);
            if (document.RootElement.TryGetProperty("suggestions", out var suggestionsElement))
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var list = JsonSerializer.Deserialize<List<string>>(suggestionsElement.GetRawText(), options);
                if (list != null && list.Count > 0) return list;
            }

            return GetFallbackSuggestions();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NutritionAgentService] ❌ Erro ao gerar sugestões proativas.");
            return GetFallbackSuggestions();
        }
    }

    public async Task<MealAnalysisResponseDto> ExtractMealDataAsync(ParseMealRequestDto request)
    {
        string responseString;

        try
        {
            _logger.LogInformation("[NutritionAgentService] ═══ Iniciando extração nutricional com Failover ═══");

            var geminiBody = BuildRequestBody(request, _geminiModel);

            try
            {
                _logger.LogInformation("[NutritionAgentService] 🚀 Tentando Gemini ({Model})...", _geminiModel);
                responseString = await SendRequestAsync(_geminiEndpoint, _geminiApiKey, _geminiModel, "Gemini", geminiBody, TimeSpan.FromSeconds(45));
            }
            catch (Exception exGemini)
            {
                _logger.LogWarning(exGemini, "[NutritionAgentService] ⚠️ Falha ou limite excedido no Gemini. Acionando Fallback para OpenAI imediatamente...");

                var openAiBody = BuildRequestBody(request, _openAiModel);
                _logger.LogInformation("[NutritionAgentService] 🔄 Executando OpenAI Fallback ({Model})...", _openAiModel);

                responseString = await SendRequestAsync(_openAiEndpoint, _openAiApiKey, _openAiModel, "OpenAiVision", openAiBody, TimeSpan.FromSeconds(60));
            }

            var jsonContent = ExtractJsonFromResponse(responseString);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<MealAnalysisResponseDto>(jsonContent, options);

            if (result == null)
            {
                _logger.LogWarning("[NutritionAgentService] ⚠️ Falha ao parsear JSON da IA.");
                throw new Exception("Não foi possível processar o laudo nutricional da refeição.");
            }

            _logger.LogInformation("[NutritionAgentService] ✅ Análise concluída — Tipo: {MealType}, Prato: {DishName}, Calorias: {Calories}",
                result.MealType, result.DishName, result.TotalMeal?.Calories ?? 0);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NutritionAgentService] ❌ Erro crítico: Tanto Gemini quanto OpenAI falharam no processamento da refeição.");

            return new MealAnalysisResponseDto(
                "Almoço",
                "Indefinido",
                new List<FoodItemDto>(),
                new MacroSummaryDto(0, 0, 0, 0),
                true,
                "Desculpe, nossos serviços de IA estão instáveis no momento. Poderia tentar enviar a foto ou texto novamente em instantes?"
            );
        }
    }

    private async Task<string> SendRequestAsync(string endpoint, string apiKey, string model, string clientName, object requestBodyObj, TimeSpan timeout)
    {
        var client = _httpClientFactory.CreateClient(clientName);

        var content = new StringContent(
            JsonSerializer.Serialize(requestBodyObj),
            Encoding.UTF8,
            "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = content
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var cts = new CancellationTokenSource(timeout);

        try
        {
            var response = await client.SendAsync(requestMessage, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API request to {clientName} ({model}) failed with Status {response.StatusCode}: {errorContent}");
            }

            return await response.Content.ReadAsStringAsync();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException($"A requisição para {clientName} ({model}) excedeu o tempo limite de {timeout.TotalSeconds} segundos.");
        }
    }

    private async Task<string> ExecuteWithFailoverAsync(
        string primaryEndpoint, string primaryKey, string primaryModel, string primaryClient,
        string fallbackEndpoint, string fallbackKey, string fallbackModel, string fallbackClient,
        object baseRequestBody)
    {
        try
        {
            var primaryBody = UpdateModelInBody(baseRequestBody, primaryModel);
            return await SendRequestAsync(primaryEndpoint, primaryKey, primaryModel, primaryClient, primaryBody, TimeSpan.FromSeconds(45));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NutritionAgentService] ⚠️ Falha no provedor primário. Tentando fallback...");
            var fallbackBody = UpdateModelInBody(baseRequestBody, fallbackModel);
            return await SendRequestAsync(fallbackEndpoint, fallbackKey, fallbackModel, fallbackClient, fallbackBody, TimeSpan.FromSeconds(60));
        }
    }

    private object UpdateModelInBody(object originalBody, string newModel)
    {
        var json = JsonSerializer.Serialize(originalBody);
        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        if (dict != null)
        {
            dict["model"] = newModel;
        }
        return dict ?? originalBody;
    }

    private object BuildRequestBody(ParseMealRequestDto request, string targetModel)
    {
        var userContentList = new List<object>
        {
            new { type = "text", text = UserPromptInstructions }
        };

        if (!string.IsNullOrWhiteSpace(request.TextInput))
        {
            userContentList.Add(new { type = "text", text = $"ENTRADA DE TEXTO DO USUÁRIO: {request.TextInput}" });
        }

        if (!string.IsNullOrWhiteSpace(request.Base64Image))
        {
            var imageBase64 = request.Base64Image.Contains(",")
                ? request.Base64Image
                : $"data:image/jpeg;base64,{request.Base64Image}";

            userContentList.Add(new
            {
                type = "image_url",
                image_url = new { url = imageBase64, detail = "high" }
            });
        }

        if (!string.IsNullOrWhiteSpace(request.AudioUrl))
        {
            userContentList.Add(new { type = "text", text = $"TRANSCRIÇÃO DE ÁUDIO DO USUÁRIO: {request.AudioUrl}" });
        }

        return new
        {
            model = targetModel,
            temperature = 0.1,
            max_tokens = 3000,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = userContentList.ToArray() }
            }
        };
    }

    private string ExtractJsonFromResponse(string responseString)
    {
        using var document = JsonDocument.Parse(responseString);
        var choice = document.RootElement.GetProperty("choices")[0];

        if (choice.TryGetProperty("finish_reason", out var finishReason) && finishReason.GetString() == "length")
        {
            _logger.LogError("[NutritionAgentService] ❌ A resposta da IA foi truncada por atingir o limite de max_tokens.");
            throw new InvalidOperationException("A IA não conseguiu finalizar a geração completa dos alimentos. Tente novamente.");
        }

        var rawText = choice
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

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
            "Opção Rápida: Omelete de 3 ovos com queijo minas",
            "Opção Completa: Peito de frango grelhado com salada folhosa",
            "Opção Prática: Shake de whey protein com água e aveia"
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