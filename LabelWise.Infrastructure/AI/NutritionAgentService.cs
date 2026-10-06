using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces.AI;
using LabelWise.Domain.Entities.Nutrition;
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

namespace LabelWise.Infrastructure.Services
{
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
            "Você é um especialista MÁSTER em nutrição clínica, análise de alimentos e Visão Computacional. " +
            "Sua função é analisar entradas multimodais mantendo total coerência com o histórico da conversa recente.\n" +
            "ATENÇÃO AOS DOIS MODOS DE OPERAÇÃO:\n" +
            "MODO 1 (REGISTRO): Se o usuário enviou uma foto, áudio ou texto descrevendo o que COMEU, preencha os dados e defina 'isAdvice': false.\n" +
            "MODO 2 (MODO SOS / CONSELHO): Se o usuário fez uma PERGUNTA, DÚVIDA ou PEDIDO DE ORIENTAÇÃO (ex: 'o que peço na churrascaria?', 'posso comer chocolate?', 'como não prejudicar meu objetivo hoje?'), " +
            "você DEVE utilizar obrigatoriamente o bloco '[CONTEXTO CLÍNICO DO PACIENTE HOJE]' e o histórico de mensagens anteriores para dar uma resposta hiper-personalizada, fluida e contínua. " +
            "Diga exatamente com base no saldo calórico dele (ex: 'Como ainda te restam X calorias hoje...'). Responda no campo 'adviceText' e defina 'isAdvice': true.\n" +
            "Retorne APENAS um JSON válido, sem texto fora dele, sem markdown de bloco de código (```json).";

        private const string UserPromptInstructions = @"TAREFA: Analisar a entrada atual do usuário considerando o histórico da conversa e o contexto clínico, retornando o JSON correspondente.

INSTRUÇÕES RIGOROSAS PARA AVALIAÇÃO DE IMAGENS E REFEIÇÕES:
1. Identifique minuciosamente todos os alimentos visíveis (inclusive molhos, queijos ralados e temperos).
2. Estime o PESO REAL EM GRAMAS (g) de cada porção baseando-se na proporção visual do prato/copo.
3. Considere GORDURAS DE COCÇÃO OCULTAS (ex: óleo, azeite, manteiga) no cálculo de calorias e gorduras.
4. Cruze a refeição com o [CONTEXTO CLÍNICO E GUARDRAILS] fornecidos. Se houver alguma violação (ex: contém glúten, contém item proibido ou restrição violada), ative o alerta.
5. Seja conservador e realista nos macros.

ESTRUTURA DE SAÍDA OBRIGATÓRIA (JSON PURO)
{
  ""isAdvice"": boolean,
  ""adviceText"": ""string ou null (preencher se for dúvida do usuário OU se houver um ALERTA DE GUARDRAIL/RESTRIÇÃO CLÍNICA na refeição)"",
  ""mealType"": ""Café da Manhã"" | ""Almoço"" | ""Lanche"" | ""Jantar"" | ""Ceia"" | ""Indefinido"",
  ""dishName"": ""string"",
  ""items"": [
    {
      ""foodName"": ""string"",
      ""portionDescription"": ""string (incluir peso estimado em gramas e modo de preparo)"",
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
  ""clarificationQuestion"": ""string ou null (preencher se a foto for ambígua. Ex: 'O bife foi feito no óleo ou na airfryer?')""
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
                    response_format = new { type = "json_object" },
                    messages = new object[]
                    {
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
            return await ExtractMealDataAsync(request, null);
        }

        public async Task<MealAnalysisResponseDto> ExtractMealDataAsync(ParseMealRequestDto request, List<ChatMessageLog>? chatHistory)
        {
            try
            {
                var geminiBody = BuildRequestBodyWithHistory(chatHistory, request, _geminiModel, SystemPrompt, UserPromptInstructions);
                string responseString;

                try
                {
                    responseString = await SendRequestAsync(_geminiEndpoint, _geminiApiKey, _geminiModel, "Gemini", geminiBody, TimeSpan.FromSeconds(45));
                }
                catch (Exception exGemini)
                {
                    _logger.LogWarning(exGemini, "[NutritionAgentService] Falha no Gemini. Acionando OpenAI Fallback...");
                    var openAiBody = BuildRequestBodyWithHistory(chatHistory, request, _openAiModel, SystemPrompt, UserPromptInstructions);
                    responseString = await SendRequestAsync(_openAiEndpoint, _openAiApiKey, _openAiModel, "OpenAiVision", openAiBody, TimeSpan.FromSeconds(60));
                }

                var jsonContent = ExtractJsonFromResponse(responseString);
                var result = JsonSerializer.Deserialize<MealAnalysisResponseDto>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return result ?? throw new Exception("Falha ao parsear JSON");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no processamento da refeição.");
                return new MealAnalysisResponseDto(
                    MealType: "Indefinido",
                    DishName: "Indefinido",
                    Items: new List<FoodItemDto>(),
                    TotalMeal: new MacroSummaryDto(0, 0, 0, 0),
                    RequiresUserClarification: false,
                    ClarificationQuestion: "⚠️ Os nossos serviços de IA estão instáveis no momento."
                );
            }
        }

        public async Task<string> GenerateDailyFeedbackMessageAsync(
            string patientGoal,
            int targetCalories,
            int consumedCalories,
            decimal targetProtein,
            decimal consumedProtein,
            List<string> mealsLogged)
        {
            try
            {
                var mealsSummary = (mealsLogged != null && mealsLogged.Any())
                    ? string.Join(", ", mealsLogged)
                    : "Nenhuma refeição registrada hoje.";

                int diff = targetCalories - consumedCalories;
                string statusCalorico = diff switch
                {
                    > 200 => $"Faltaram {diff} kcal para atingir a meta.",
                    < -200 => $"Ultrapassou a meta em {Math.Abs(diff)} kcal.",
                    _ => "Atingiu a meta calórica com excelente precisão!"
                };

                var prompt = $@"
Você é um assistente nutricional motivacional, empático e amigável enviando uma mensagem no WhatsApp ao final do dia.
DADOS DO DIA DO PACIENTE:
- Objetivo: {patientGoal}
- Meta de Calorias: {targetCalories} kcal | Consumido: {consumedCalories} kcal ({statusCalorico})
- Meta Proteína: {targetProtein}g | Consumido: {consumedProtein:F0}g
- Refeições Registradas: {mealsSummary}

INSTRUÇÕES:
1. Escreva uma mensagem curta (máximo 3 a 4 frases) para o WhatsApp com emojis.
2. Seja encorajador se ele esteve perto da meta, ou acolhedor se ele ultrapassou/esqueceu de registrar.
3. Não use termos técnicos complexos. Termine com uma palavra de incentivo para o dia seguinte.
Retorne APENAS o texto da mensagem pronto para envio.
";

                var requestBody = new
                {
                    model = _geminiModel,
                    temperature = 0.7,
                    messages = new object[]
                    {
                        new { role = "user", content = prompt }
                    }
                };

                var responseString = await ExecuteWithFailoverAsync(
                    _geminiEndpoint, _geminiApiKey, _geminiModel, "Gemini",
                    _openAiEndpoint, _openAiApiKey, _openAiModel, "OpenAiVision", requestBody);

                return ExtractJsonFromResponse(responseString).Replace("\"", "").Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar feedback diário com IA.");
                return "🌙 Boa noite! Passando para lembrar de conferir suas refeições registradas de hoje. Amanhã seguimos juntos no foco! 🥗";
            }
        }

        public async Task<ExtractDietGoalResponseDto> ExtractDietGoalsFromDocumentAsync(ExtractDietGoalRequestDto request)
        {
            var userContentList = new List<object>
            {
                new { type = "text", text = $"{DietReaderSystemPrompt}\n\nExtraia os alvos nutricionais e retorne APENAS um JSON válido." }
            };

            if (!string.IsNullOrWhiteSpace(request.TextInput))
                userContentList.Add(new { type = "text", text = $"DIETA: {request.TextInput}" });

            if (!string.IsNullOrWhiteSpace(request.Base64Image))
            {
                var imageBase64 = request.Base64Image.Contains(",") ? request.Base64Image : $"data:image/jpeg;base64,{request.Base64Image}";
                userContentList.Add(new { type = "image_url", image_url = new { url = imageBase64 } });
            }

            var requestBody = new
            {
                model = _geminiModel,
                temperature = 0.1,
                max_tokens = 1500,
                response_format = new { type = "json_object" },
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

        private object BuildRequestBodyWithHistory(List<ChatMessageLog>? history, ParseMealRequestDto request, string targetModel, string sysPrompt, string userPromptInstructions)
        {
            var messagesList = new List<object>
            {
                new { role = "system", content = $"{sysPrompt}\n\n{userPromptInstructions}" }
            };

            string contextPaciente = $@"
[CONTEXTO CLÍNICO DO PACIENTE HOJE]
- Meta Calórica Diária: {request.TargetCalories ?? 0} kcal
- Guardrails (Protocolo Estrito): {(string.IsNullOrWhiteSpace(request.ClinicalProtocol) ? "Nenhum" : request.ClinicalProtocol)}
- Alergias/Aversões: {(string.IsNullOrWhiteSpace(request.DietaryRestrictions) ? "Nenhuma" : request.DietaryRestrictions)}";

            messagesList.Add(new { role = "system", content = contextPaciente });

            if (history != null && history.Any())
            {
                foreach (var h in history)
                {
                    messagesList.Add(new { role = h.Role, content = h.Content });
                }
            }

            var currentUserContent = new List<object>();

            if (!string.IsNullOrWhiteSpace(request.TextInput))
            {
                currentUserContent.Add(new { type = "text", text = request.TextInput });
            }

            if (!string.IsNullOrWhiteSpace(request.Base64Image))
            {
                var imageBase64 = request.Base64Image.Contains(",") ? request.Base64Image : $"data:image/jpeg;base64,{request.Base64Image}";
                currentUserContent.Add(new { type = "image_url", image_url = new { url = imageBase64 } });
            }

            if (currentUserContent.Count == 0)
            {
                currentUserContent.Add(new { type = "text", text = "Analise esta entrada." });
            }

            messagesList.Add(new { role = "user", content = currentUserContent.ToArray() });

            return new
            {
                model = targetModel,
                temperature = 0.2,
                max_tokens = 3000,
                response_format = new { type = "json_object" },
                messages = messagesList.ToArray()
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
            else if (rawText.StartsWith("```"))
                rawText = rawText.Substring(3);

            if (rawText.EndsWith("```"))
                rawText = rawText.Substring(0, rawText.Length - 3);

            rawText = rawText.Trim();

            int firstBrace = rawText.IndexOf('{');
            int lastBrace = rawText.LastIndexOf('}');

            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return rawText.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
            }

            return rawText;
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
}