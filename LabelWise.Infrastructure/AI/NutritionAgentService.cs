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
2. Estime o PESO REAL EM GRAMAS (g) de cada porção baseando-se na proporção visual do prato/copo ou na descrição informada.
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
  ""clarificationQuestion"": ""string ou null""
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

                return await ExecuteWithFailoverAsync(
                    _geminiEndpoint, _geminiApiKey, _geminiModel, "Gemini",
                    _openAiEndpoint, _openAiApiKey, _openAiModel, "OpenAiVision",
                    requestBody,
                    (responseStr) =>
                    {
                        var jsonContent = ExtractContentFromResponse(responseStr, requireJson: true);
                        using var document = JsonDocument.Parse(jsonContent);
                        if (document.RootElement.TryGetProperty("suggestions", out var suggestionsElement))
                        {
                            return JsonSerializer.Deserialize<List<string>>(suggestionsElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? GetFallbackSuggestions();
                        }
                        throw new Exception("Chave 'suggestions' não encontrada no JSON.");
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NutritionAgentService] Erro proativo. Retornando fallback.");
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
                var requestBody = BuildRequestBodyWithHistory(chatHistory, request, _geminiModel, SystemPrompt, UserPromptInstructions);

                var result = await ExecuteWithFailoverAsync(
                    _geminiEndpoint, _geminiApiKey, _geminiModel, "Gemini",
                    _openAiEndpoint, _openAiApiKey, _openAiModel, "OpenAiVision",
                    requestBody,
                    ParseMealResponse); // 💡 A validação do JSON e os falhanços agora acontecem DENTRO do fluxo de fallback!

                // 🛡️ RECALCULADOR DE SEGURANÇA FINAL
                int cal = result.TotalMeal?.Calories ?? 0;
                decimal prot = result.TotalMeal?.ProteinG ?? 0;
                decimal carb = result.TotalMeal?.CarbsG ?? 0;
                decimal fat = result.TotalMeal?.FatG ?? 0;

                if (cal == 0 && result.Items != null && result.Items.Any())
                {
                    cal = result.Items.Sum(x => x.Calories);
                    prot = result.Items.Sum(x => x.ProteinG);
                    carb = result.Items.Sum(x => x.CarbsG);
                    fat = result.Items.Sum(x => x.FatG);
                }

                var totalMealCorrigido = new MacroSummaryDto(cal, prot, carb, fat);

                string dishNameCorrigido = !string.IsNullOrWhiteSpace(result.DishName) && result.DishName != "Indefinido"
                    ? result.DishName
                    : (result.Items != null && result.Items.Any()
                        ? string.Join(", ", result.Items.Select(i => i.FoodName))
                        : "Refeição Registrada");

                string mealTypeCorrigido = (!string.IsNullOrWhiteSpace(result.MealType) && result.MealType != "Indefinido")
                    ? result.MealType
                    : "Refeição";

                return new MealAnalysisResponseDto(
                    MealType: mealTypeCorrigido,
                    DishName: dishNameCorrigido,
                    Items: result.Items ?? new List<FoodItemDto>(),
                    TotalMeal: totalMealCorrigido,
                    RequiresUserClarification: result.RequiresUserClarification,
                    ClarificationQuestion: result.ClarificationQuestion,
                    IsAdvice: result.IsAdvice,
                    AdviceText: result.AdviceText
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro crítico no processamento da refeição (Ambos os provedores de IA falharam).");
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
                    messages = new object[] { new { role = "user", content = prompt } }
                };

                return await ExecuteWithFailoverAsync(
                    _geminiEndpoint, _geminiApiKey, _geminiModel, "Gemini",
                    _openAiEndpoint, _openAiApiKey, _openAiModel, "OpenAiVision",
                    requestBody,
                    (responseStr) =>
                    {
                        // Aqui não exigimos JSON, extraímos apenas o texto
                        return ExtractContentFromResponse(responseStr, requireJson: false).Replace("\"", "").Trim();
                    });
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

            return await ExecuteWithFailoverAsync(
                _geminiEndpoint, _geminiApiKey, _geminiModel, "Gemini",
                _openAiEndpoint, _openAiApiKey, _openAiModel, "OpenAiVision",
                requestBody,
                (responseStr) =>
                {
                    var jsonContent = ExtractContentFromResponse(responseStr, requireJson: true);
                    return JsonSerializer.Deserialize<ExtractDietGoalResponseDto>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? throw new Exception("Falha ao extrair dieta (JSON nulo).");
                });
        }

        // 🛡️ MOTOR DE FALLBACK ATUALIZADO (A VALIDAÇÃO OCORRE DENTRO DO LOOP)
        private async Task<T> ExecuteWithFailoverAsync<T>(
            string primaryEndpoint, string primaryKey, string primaryModel, string primaryClient,
            string fallbackEndpoint, string fallbackKey, string fallbackModel, string fallbackClient,
            object baseRequestBody,
            Func<string, T> parseAndValidateFunc)
        {
            try
            {
                var primaryBody = UpdateModelInBody(baseRequestBody, primaryModel);
                var responseStr = await SendRequestAsync(primaryEndpoint, primaryKey, primaryModel, primaryClient, primaryBody, TimeSpan.FromSeconds(45));
                return parseAndValidateFunc(responseStr);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"[NutritionAgentService] Falha no provedor primário ({primaryClient} - texto puro ou erro HTTP). Tentando fallback para {fallbackClient}...");
                var fallbackBody = UpdateModelInBody(baseRequestBody, fallbackModel);
                var fallbackStr = await SendRequestAsync(fallbackEndpoint, fallbackKey, fallbackModel, fallbackClient, fallbackBody, TimeSpan.FromSeconds(60));
                return parseAndValidateFunc(fallbackStr);
            }
        }

        private MealAnalysisResponseDto ParseMealResponse(string responseString)
        {
            // Tenta forçar a extração de um JSON. Se não encontrar as chaves '{}', lança exceção automaticamente.
            var jsonContent = ExtractContentFromResponse(responseString, requireJson: true);

            if (jsonContent == "{}" || string.IsNullOrWhiteSpace(jsonContent))
                throw new JsonException("A IA retornou um objeto JSON completamente vazio.");

            var result = JsonSerializer.Deserialize<MealAnalysisResponseDto>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null)
                throw new JsonException("Falha de conversão do JSON para o objeto MealAnalysisResponseDto.");

            // Deteção do bug do Gemini: Retornar estrutura com tudo a 0 ignorando o pedido.
            int cal = result.TotalMeal?.Calories ?? 0;
            if (cal == 0 && (result.Items == null || !result.Items.Any()) && !result.IsAdvice)
            {
                throw new JsonException("A IA retornou a estrutura JSON, mas com itens vazios e calorias zeradas.");
            }

            return result;
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

            object userContent;

            if (!string.IsNullOrWhiteSpace(request.Base64Image))
            {
                var imageBase64 = request.Base64Image.Contains(",") ? request.Base64Image : $"data:image/jpeg;base64,{request.Base64Image}";
                var contentList = new List<object>();

                if (!string.IsNullOrWhiteSpace(request.TextInput))
                {
                    contentList.Add(new { type = "text", text = request.TextInput });
                }

                contentList.Add(new { type = "image_url", image_url = new { url = imageBase64 } });
                userContent = contentList.ToArray();
            }
            else
            {
                userContent = !string.IsNullOrWhiteSpace(request.TextInput) ? request.TextInput : "Analise esta refeição.";
            }

            messagesList.Add(new { role = "user", content = userContent });

            return new
            {
                model = targetModel,
                temperature = 0.2,
                max_tokens = 3000,
                response_format = new { type = "json_object" }, // DE VOLTA! O fallback proteger-nos-á do bug dos JSONs vazios.
                messages = messagesList.ToArray()
            };
        }

        private string ExtractContentFromResponse(string responseString, bool requireJson)
        {
            using var document = JsonDocument.Parse(responseString);
            var choice = document.RootElement.GetProperty("choices")[0];

            var rawText = choice.GetProperty("message").GetProperty("content").GetString() ?? "";
            rawText = rawText.Trim();

            if (requireJson)
            {
                int firstBrace = rawText.IndexOf('{');
                int lastBrace = rawText.LastIndexOf('}');

                if (firstBrace >= 0 && lastBrace > firstBrace)
                {
                    return rawText.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
                }

                throw new JsonException($"A IA falhou em devolver um JSON e devolveu texto puro: {rawText}");
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