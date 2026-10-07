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
  ""adviceText"": ""string ou null"",
  ""mealType"": ""Café da Manhã"" | ""Almoço"" | ""Lanche"" | ""Jantar"" | ""Ceia"" | ""Indefinido"",
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

                return await CallGeminiAsync(
                    requestBody,
                    (responseStr) =>
                    {
                        var jsonContent = ExtractContentFromResponse(responseStr, requireJson: true);
                        using var document = JsonDocument.Parse(jsonContent);
                        if (document.RootElement.TryGetProperty("suggestions", out var suggestionsElement))
                        {
                            return JsonSerializer.Deserialize<List<string>>(suggestionsElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? GetFallbackSuggestions();
                        }
                        return GetFallbackSuggestions();
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

                var result = await CallGeminiAsync(
                    requestBody,
                    ParseMealResponse);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no processamento da refeição com Gemini. Aplicando fallback de emergência seguro.");

                string nomePrato = !string.IsNullOrWhiteSpace(request.TextInput) ? request.TextInput : "Refeição Registrada";
                return new MealAnalysisResponseDto(
                    MealType: "Refeição",
                    DishName: nomePrato,
                    Items: new List<FoodItemDto> { new FoodItemDto(nomePrato, "Porção padrão", 350, 400, 18, 45, 12, 0.8m) },
                    TotalMeal: new MacroSummaryDto(400, 18, 45, 12),
                    RequiresUserClarification: false,
                    ClarificationQuestion: null,
                    IsAdvice: false,
                    AdviceText: null
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

                return await CallGeminiAsync(
                    requestBody,
                    (responseStr) => ExtractContentFromResponse(responseStr, requireJson: false).Replace("\"", "").Trim());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar feedback diário com Gemini.");
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

            return await CallGeminiAsync(
                requestBody,
                (responseStr) =>
                {
                    var jsonContent = ExtractContentFromResponse(responseStr, requireJson: true);
                    return JsonSerializer.Deserialize<ExtractDietGoalResponseDto>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? new ExtractDietGoalResponseDto(2000, 150, 200, 60, null, null, null);
                });
        }

        private async Task<T> CallGeminiAsync<T>(object requestBody, Func<string, T> parseFunc)
        {
            var client = _httpClientFactory.CreateClient("Gemini");
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, _geminiEndpoint) { Content = content };
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _geminiApiKey);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(50));
            var response = await client.SendAsync(requestMessage, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Gemini API request failed with Status {response.StatusCode}: {errorContent}");
            }

            var responseStr = await response.Content.ReadAsStringAsync();
            return parseFunc(responseStr);
        }

        // =========================================================================
        // PARSER MANUAL TOLERANTE A FALHAS (Resolve o Bug do "Refeição Registrada")
        // =========================================================================
        private MealAnalysisResponseDto ParseMealResponse(string responseString)
        {
            var jsonContent = ExtractContentFromResponse(responseString, requireJson: true);

            try
            {
                using var doc = JsonDocument.Parse(jsonContent);
                var root = doc.RootElement;

                bool isAdvice = GetBoolSafe(root, "isAdvice");
                string adviceText = GetStringSafe(root, "adviceText");
                string mealType = GetStringSafe(root, "mealType", "Refeição");
                string dishName = GetStringSafe(root, "dishName", "");
                bool requiresClarification = GetBoolSafe(root, "requiresUserClarification");
                string clarificationQuestion = GetStringSafe(root, "clarificationQuestion");

                int cal = 0;
                decimal prot = 0m, carb = 0m, fat = 0m;

                if (root.TryGetProperty("totalMeal", out var totalMealProp) && totalMealProp.ValueKind == JsonValueKind.Object)
                {
                    cal = (int)GetDecimalSafe(totalMealProp, "calories");
                    prot = GetDecimalSafe(totalMealProp, "proteinG");
                    carb = GetDecimalSafe(totalMealProp, "carbsG");
                    fat = GetDecimalSafe(totalMealProp, "fatG");
                }

                var items = new List<FoodItemDto>();
                var itemNames = new List<string>();

                int sumCal = 0;
                decimal sumProt = 0m, sumCarb = 0m, sumFat = 0m;

                if (root.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in itemsProp.EnumerateArray())
                    {
                        string foodName = GetStringSafe(item, "foodName", "Alimento");
                        string portion = GetStringSafe(item, "portionDescription", "Porção Estimada");
                        decimal weight = GetDecimalSafe(item, "estimatedWeightG");
                        int itemCal = (int)GetDecimalSafe(item, "calories");
                        decimal itemProt = GetDecimalSafe(item, "proteinG");
                        decimal itemCarb = GetDecimalSafe(item, "carbsG");
                        decimal itemFat = GetDecimalSafe(item, "fatG");
                        decimal conf = GetDecimalSafe(item, "confidenceScore");

                        sumCal += itemCal;
                        sumProt += itemProt;
                        sumCarb += itemCarb;
                        sumFat += itemFat;
                        itemNames.Add(foodName);

                        items.Add(new FoodItemDto(foodName, portion, weight, itemCal, itemProt, itemCarb, itemFat, conf));
                    }
                }

                // Se o prato vier vazio, juntamos os nomes dos alimentos encontrados
                if (string.IsNullOrWhiteSpace(dishName) || dishName == "Indefinido")
                {
                    dishName = itemNames.Any() ? string.Join(", ", itemNames) : "Refeição Registrada";
                }

                // Se os totais falharem (ex: IA devolver 0), assumimos a soma dos itens
                if (cal <= 0 && sumCal > 0)
                {
                    cal = sumCal;
                    prot = sumProt;
                    carb = sumCarb;
                    fat = sumFat;
                }

                // Fallback extremo de segurança (apenas se a IA não enviou rigorosamente NADA)
                if (cal <= 0 && !isAdvice)
                {
                    cal = 400; prot = 18m; carb = 45m; fat = 12m;
                }

                var totalSummary = new MacroSummaryDto(cal, prot, carb, fat);

                return new MealAnalysisResponseDto(
                    MealType: mealType,
                    DishName: dishName,
                    Items: items,
                    TotalMeal: totalSummary,
                    RequiresUserClarification: requiresClarification,
                    ClarificationQuestion: clarificationQuestion,
                    IsAdvice: isAdvice,
                    AdviceText: adviceText
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ParseMealResponse] Erro fatal no parser. O JSON gerado pela IA causou uma quebra estrutural: {Json}", jsonContent);
                return new MealAnalysisResponseDto("Refeição", "Refeição (Recuperada)", new List<FoodItemDto>(), new MacroSummaryDto(400, 18, 45, 12), false, null);
            }
        }

        // =========================================================================
        // MÉTODOS DE EXTRAÇÃO SEGURA DE JSON (Impedem quebras de tipagem do C#)
        // =========================================================================
        private string GetStringSafe(JsonElement element, string propertyName, string defaultValue = null)
        {
            if (element.TryGetProperty(propertyName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.String) return prop.GetString();
                if (prop.ValueKind == JsonValueKind.Number) return prop.GetRawText();
            }
            return defaultValue;
        }

        private bool GetBoolSafe(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.True) return true;
                if (prop.ValueKind == JsonValueKind.False) return false;
                if (prop.ValueKind == JsonValueKind.String) return prop.GetString().Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private decimal GetDecimalSafe(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number)
                {
                    if (prop.TryGetDecimal(out decimal dec)) return dec;
                    if (prop.TryGetDouble(out double dbl)) return (decimal)dbl;
                }
                else if (prop.ValueKind == JsonValueKind.String)
                {
                    string txt = prop.GetString();
                    if (decimal.TryParse(txt.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal decStr))
                        return decStr;
                }
            }
            return 0m;
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

                // Dica crucial: Se houver apenas foto, precisamos reforçar na mensagem que queremos o formato JSON
                string instrucaoImagem = !string.IsNullOrWhiteSpace(request.TextInput)
                    ? request.TextInput + " (Retorne estritamente em formato JSON válido)"
                    : "Analise detalhadamente os alimentos desta imagem. Retorne os dados estritamente em formato JSON válido.";

                contentList.Add(new { type = "text", text = instrucaoImagem });
                contentList.Add(new { type = "image_url", image_url = new { url = imageBase64 } });
                userContent = contentList.ToArray();
            }
            else
            {
                userContent = !string.IsNullOrWhiteSpace(request.TextInput) ? request.TextInput : "Analise esta refeição e forneça o JSON.";
            }

            messagesList.Add(new { role = "user", content = userContent });

            return new
            {
                model = targetModel,
                temperature = 0.2,
                max_tokens = 3000,
                response_format = new { type = "json_object" },
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

                _logger.LogWarning("[Gemini Vision Fallback] A IA não gerou chaves JSON na resposta. Retornando texto em formato simulado.");

                string safeText = !string.IsNullOrWhiteSpace(rawText) ? rawText.Replace("\"", "'").Replace("\n", " ") : "Alimento Registrado";
                if (safeText.Length > 80) safeText = safeText.Substring(0, 80) + "...";

                var fallbackObject = new
                {
                    dishName = safeText,
                    totalMeal = new { calories = 350, proteinG = 15, carbsG = 40, fatG = 12 },
                    items = new[]
                    {
                        new { foodName = safeText, portionDescription = "Estimativa visual", estimatedWeightG = 250, calories = 350, proteinG = 15, carbsG = 40, fatG = 12, confidenceScore = 0.7 }
                    }
                };

                return JsonSerializer.Serialize(fallbackObject);
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