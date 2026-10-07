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
            "MODO 2 (MODO SOS / CONSELHO): Se o usuário fez uma PERGUNTA, DÚVIDA ou PEDIDO DE ORIENTAÇÃO, " +
            "você DEVE dar uma resposta hiper-personalizada. Diga exatamente com base no saldo calórico dele. Responda no campo 'adviceText' e defina 'isAdvice': true.\n" +
            "Retorne APENAS um JSON válido.";

        private const string UserPromptInstructions = @"INSTRUÇÕES RIGOROSAS PARA AVALIAÇÃO DE IMAGENS E REFEIÇÕES:
1. Identifique minuciosamente todos os alimentos visíveis (inclusive molhos, queijos ralados e temperos).
2. Estime o PESO REAL EM GRAMAS (g) de cada porção.
3. Considere GORDURAS DE COCÇÃO OCULTAS.
4. Seja conservador e realista nos macros.

MUITO IMPORTANTE: NÃO TRADUZA AS CHAVES DO JSON. USE EXATAMENTE OS NOMES EM INGLÊS ABAIXO.
ESTRUTURA DE SAÍDA OBRIGATÓRIA (JSON PURO):
{
  ""isAdvice"": false,
  ""adviceText"": null,
  ""mealType"": ""Café da Manhã"" | ""Almoço"" | ""Lanche"" | ""Jantar"" | ""Ceia"",
  ""dishName"": ""Nome do Prato (ex: Ovos Mexidos com Pão)"",
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
  ""requiresUserClarification"": false,
  ""clarificationQuestion"": null
}";

        private const string DietReaderSystemPrompt = @"Você é um Assistente Clínico Especialista em Nutrição e Extração de Dados.
Extraia os alvos nutricionais e retorne APENAS um JSON válido.
FORMATO: { ""targetCalories"": number, ""targetProteinG"": number, ""targetCarbsG"": number, ""targetFatG"": number, ""dietaryRestrictions"": ""string"", ""favoriteFoods"": ""string"", ""prescribedMealPlan"": ""string"" }";

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
                    : "Nenhum alimento registrado.";

                const string systemPrompt = @"Você é um Copiloto Nutricional Proativo. Retorne APENAS um JSON com a chave ""suggestions"" contendo um array de 3 strings.";
                var userPrompt = $"PRÓXIMA REFEIÇÃO: {nextMealType}\nSALDO: {remainingBalance.Calories} kcal...\nALIMENTOS CONSUMIDOS: {historicoPratos}";

                var requestBody = new
                {
                    model = _geminiModel,
                    temperature = 0.6,
                    max_tokens = 4000,
                    messages = new object[] { new { role = "user", content = $"{systemPrompt}\n\n{userPrompt}" } }
                };

                return await CallGeminiAsync(requestBody, (responseStr) =>
                {
                    var jsonContent = ExtractContentFromResponse(responseStr, requireJson: true);
                    using var document = JsonDocument.Parse(jsonContent);
                    if (TryGetPropertyCaseInsensitive(document.RootElement, "suggestions", out var suggs))
                    {
                        return JsonSerializer.Deserialize<List<string>>(suggs.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? GetFallbackSuggestions();
                    }
                    return GetFallbackSuggestions();
                });
            }
            catch { return GetFallbackSuggestions(); }
        }

        public async Task<MealAnalysisResponseDto> ExtractMealDataAsync(ParseMealRequestDto request) => await ExtractMealDataAsync(request, null);

        public async Task<MealAnalysisResponseDto> ExtractMealDataAsync(ParseMealRequestDto request, List<ChatMessageLog>? chatHistory)
        {
            try
            {
                var requestBody = BuildRequestBodyWithHistory(chatHistory, request, _geminiModel, SystemPrompt, UserPromptInstructions);
                return await CallGeminiAsync(requestBody, ParseMealResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro crítico ao processar refeição com Gemini.");
                string nomePrato = !string.IsNullOrWhiteSpace(request.TextInput) && request.TextInput.Length < 30 ? request.TextInput : "Refeição Registrada";
                return new MealAnalysisResponseDto("Refeição", nomePrato, new List<FoodItemDto>(), new MacroSummaryDto(400, 18, 45, 12), false, null);
            }
        }

        public async Task<string> GenerateDailyFeedbackMessageAsync(string goal, int tCal, int cCal, decimal tProt, decimal cProt, List<string> meals)
        {
            try
            {
                string status = (tCal - cCal) switch { > 200 => $"Faltaram {tCal - cCal} kcal.", < -200 => $"Ultrapassou em {Math.Abs(tCal - cCal)} kcal.", _ => "Atingiu a meta!" };
                var prompt = $"Escreva uma mensagem curta (máximo 4 frases) para o WhatsApp com emojis motivando o paciente.\nMeta Calórica: {tCal} | Consumido: {cCal} ({status}). Apenas o texto pronto.";

                var requestBody = new { model = _geminiModel, temperature = 0.7, messages = new object[] { new { role = "user", content = prompt } } };
                return await CallGeminiAsync(requestBody, (r) => ExtractContentFromResponse(r, false).Replace("\"", "").Trim());
            }
            catch { return "🌙 Boa noite! Passando para lembrar de conferir as suas refeições. Amanhã seguimos juntos no foco! 🥗"; }
        }

        public async Task<ExtractDietGoalResponseDto> ExtractDietGoalsFromDocumentAsync(ExtractDietGoalRequestDto request)
        {
            var userContentList = new List<object> { new { type = "text", text = $"{DietReaderSystemPrompt}\nDIETA: {request.TextInput}" } };
            if (!string.IsNullOrWhiteSpace(request.Base64Image))
            {
                string imageBase64 = request.Base64Image.Contains(",") ? request.Base64Image : $"data:image/jpeg;base64,{request.Base64Image}";
                userContentList.Add(new { type = "image_url", image_url = new { url = imageBase64 } });
            }

            var requestBody = new { model = _geminiModel, temperature = 0.1, max_tokens = 1500, messages = new object[] { new { role = "user", content = userContentList.ToArray() } } };
            return await CallGeminiAsync(requestBody, (responseStr) =>
            {
                var jsonContent = ExtractContentFromResponse(responseStr, true);
                return JsonSerializer.Deserialize<ExtractDietGoalResponseDto>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new ExtractDietGoalResponseDto(2000, 150, 200, 60, null, null, null);
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
                throw new HttpRequestException($"Gemini API request failed: {errorContent}");
            }

            return parseFunc(await response.Content.ReadAsStringAsync());
        }

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

                int cal = 0; decimal prot = 0m, carb = 0m, fat = 0m;
                if (TryGetPropertyCaseInsensitive(root, "totalMeal", out var totalMealProp) && totalMealProp.ValueKind == JsonValueKind.Object)
                {
                    cal = (int)GetDecimalSafe(totalMealProp, "calories");
                    prot = GetDecimalSafe(totalMealProp, "proteinG");
                    carb = GetDecimalSafe(totalMealProp, "carbsG");
                    fat = GetDecimalSafe(totalMealProp, "fatG");
                }

                var items = new List<FoodItemDto>();
                var itemNames = new List<string>();
                int sumCal = 0; decimal sumProt = 0m, sumCarb = 0m, sumFat = 0m;

                if (TryGetPropertyCaseInsensitive(root, "items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in itemsProp.EnumerateArray())
                    {
                        string foodName = GetStringSafe(item, "foodName", "Alimento");
                        string portion = GetStringSafe(item, "portionDescription", "1 porção");
                        decimal weight = GetDecimalSafe(item, "estimatedWeightG");
                        int itemCal = (int)GetDecimalSafe(item, "calories");
                        decimal itemProt = GetDecimalSafe(item, "proteinG");
                        decimal itemCarb = GetDecimalSafe(item, "carbsG");
                        decimal itemFat = GetDecimalSafe(item, "fatG");

                        sumCal += itemCal; sumProt += itemProt; sumCarb += itemCarb; sumFat += itemFat;
                        itemNames.Add(foodName);
                        items.Add(new FoodItemDto(foodName, portion, weight, itemCal, itemProt, itemCarb, itemFat, 1m));
                    }
                }

                if (string.IsNullOrWhiteSpace(dishName) || dishName == "Indefinido")
                    dishName = itemNames.Any() ? string.Join(", ", itemNames) : "Refeição Registrada";

                if (cal <= 0 && sumCal > 0)
                {
                    cal = sumCal; prot = sumProt; carb = sumCarb; fat = sumFat;
                }

                if (cal <= 0 && !isAdvice)
                {
                    cal = 400; prot = 18m; carb = 45m; fat = 12m;
                }

                return new MealAnalysisResponseDto(mealType, dishName, items, new MacroSummaryDto(cal, prot, carb, fat), requiresClarification, clarificationQuestion, isAdvice, adviceText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ParseMealResponse] Erro fatal no parser ao ler: {Json}", jsonContent);
                return new MealAnalysisResponseDto("Refeição", "Refeição (Recuperada)", new List<FoodItemDto>(), new MacroSummaryDto(400, 18, 45, 12), false, null);
            }
        }

        // =========================================================================
        // UTILITÁRIOS IMUNES A MAIÚSCULAS/MINÚSCULAS E TRADUÇÕES ACIDENTAIS
        // =========================================================================
        private bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                value = default;
                return false;
            }

            if (element.TryGetProperty(propertyName, out value)) return true;

            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name.Replace("_", ""), propertyName.Replace("_", ""), StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }

        private string GetStringSafe(JsonElement element, string propertyName, string defaultValue = null)
        {
            if (TryGetPropertyCaseInsensitive(element, propertyName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.String) return prop.GetString();
                if (prop.ValueKind == JsonValueKind.Number) return prop.GetRawText();
            }
            return defaultValue;
        }

        private bool GetBoolSafe(JsonElement element, string propertyName)
        {
            if (TryGetPropertyCaseInsensitive(element, propertyName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.True) return true;
                if (prop.ValueKind == JsonValueKind.False) return false;
                if (prop.ValueKind == JsonValueKind.String) return prop.GetString().Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private decimal GetDecimalSafe(JsonElement element, string propertyName)
        {
            if (TryGetPropertyCaseInsensitive(element, propertyName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number)
                {
                    if (prop.TryGetDecimal(out decimal dec)) return dec;
                    if (prop.TryGetDouble(out double dbl)) return (decimal)dbl;
                }
                else if (prop.ValueKind == JsonValueKind.String)
                {
                    if (decimal.TryParse(prop.GetString()?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal decStr))
                        return decStr;
                }
            }
            return 0m;
        }

        private object BuildRequestBodyWithHistory(List<ChatMessageLog>? history, ParseMealRequestDto request, string targetModel, string sysPrompt, string userPromptInstructions)
        {
            var messagesList = new List<object> { new { role = "system", content = sysPrompt } };

            if (history != null && history.Any())
            {
                foreach (var h in history) messagesList.Add(new { role = h.Role, content = h.Content });
            }

            object userContent;

            // O request.TextInput já traz o contexto clínico gerado pelo NutritionService.
            // Aqui blindamos adicionando a instrução do JSON diretamente onde a IA não a pode ignorar.
            string textoBase = request.TextInput ?? "Analise esta refeição.";
            string textoFinalComRegras = $"{textoBase}\n\n{userPromptInstructions}";

            if (!string.IsNullOrWhiteSpace(request.Base64Image))
            {
                string imageBase64 = request.Base64Image.Contains(",") ? request.Base64Image : $"data:image/jpeg;base64,{request.Base64Image}";
                userContent = new List<object>
                {
                    new { type = "text", text = textoFinalComRegras },
                    new { type = "image_url", image_url = new { url = imageBase64 } }
                }.ToArray();
            }
            else
            {
                userContent = textoFinalComRegras;
            }

            messagesList.Add(new { role = "user", content = userContent });

            return new
            {
                model = targetModel,
                temperature = 0.2,
                max_tokens = 3000,
                // O response_format foi retirado intencionalmente para evitar o bug de colapso "{}" do Gemini.
                messages = messagesList.ToArray()
            };
        }

        private string ExtractContentFromResponse(string responseString, bool requireJson)
        {
            using var document = JsonDocument.Parse(responseString);
            string rawText = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
            rawText = rawText.Trim();

            if (requireJson)
            {
                int firstBrace = rawText.IndexOf('{');
                int lastBrace = rawText.LastIndexOf('}');

                if (firstBrace >= 0 && lastBrace > firstBrace)
                {
                    return rawText.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
                }

                _logger.LogWarning("[Gemini Vision Fallback] Texto retornado não possui chaves JSON.");

                string safeText = !string.IsNullOrWhiteSpace(rawText) ? rawText.Replace("\"", "'").Replace("\n", " ") : "Alimento Registrado";
                if (safeText.Length > 80) safeText = safeText.Substring(0, 80) + "...";

                return $"{{\"dishName\":\"{safeText}\",\"totalMeal\":{{\"calories\":350,\"proteinG\":15,\"carbsG\":40,\"fatG\":12}},\"items\":[{{\"foodName\":\"{safeText}\",\"portionDescription\":\"Estimativa visual\",\"estimatedWeightG\":250,\"calories\":350,\"proteinG\":15,\"carbsG\":40,\"fatG\":12,\"confidenceScore\":0.7}}]}}";
            }
            return rawText;
        }

        private static List<string> GetFallbackSuggestions() => new List<string> { "Omelete de 3 ovos", "Frango grelhado e salada", "Shake de whey protein" };

        private static string? GetConfigValue(IConfiguration config, string primaryKey, string secondaryKey)
        {
            var val = config[primaryKey];
            if (!string.IsNullOrWhiteSpace(val)) return val;
            return config[secondaryKey];
        }
    }
}