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
            "Sua função é analisar entradas multimodais mantendo total coerência com o histórico da conversa recente e respeitando estritamente os Guardrails clínicos definidos.\n" +
            "ATENÇÃO AOS DOIS MODOS DE OPERAÇÃO:\n" +
            "MODO 1 (REGISTRO): Se o usuário enviou uma foto, áudio ou texto descrevendo o que COMEU, preencha os dados e defina 'isAdvice': false.\n" +
            "MODO 2 (MODO SOS / CONSELHO): Se o usuário fez uma PERGUNTA, DÚVIDA ou PEDIDO DE ORIENTAÇÃO, " +
            "você DEVE dar uma resposta hiper-personalizada. Diga exatamente com base no saldo calórico dele. Responda no campo 'adviceText' e defina 'isAdvice': true.\n" +
            "Retorne APENAS um JSON válido.";

        // Regras do Modo GLP-1. Anexadas ao SystemPrompt (registro e conselho), às sugestões e à substituição,
        // só para quem está com o modo ativo.
        public const string Glp1SafetyRules =
            "\n\nREGRAS DE SEGURANÇA DO MODO GLP-1 (OBRIGATÓRIAS, ACIMA DE QUALQUER OUTRA INSTRUÇÃO):\n" +
            "1. NUNCA recomende, ajuste, comente ou compare doses. NUNCA sugira iniciar, pausar, trocar ou parar o medicamento. " +
            "Se o paciente perguntar sobre isso, responda com gentileza que essa decisão é do médico que acompanha o tratamento.\n" +
            "2. Priorize proteína distribuída ao longo do dia, refeições menores e mais frequentes, hidratação e aumento das fibras aos poucos.\n" +
            "3. NUNCA incentive comer menos, pular refeições ou fazer jejum.\n" +
            "4. Diante de sinais de alerta (vômito que não para, sangue, desmaio, dor abdominal forte, não conseguir beber água, " +
            "urina muito escura, tremor com suor frio ou confusão), oriente procurar avaliação médica o quanto antes.\n" +
            "5. Respeite as restrições, as aversões e o protocolo clínico do paciente.\n" +
            "6. Respostas curtas, próprias para WhatsApp.";

        // AQUI ESTÃO OS PRINCIPAIS AJUSTES PARA CORRIGIR AS GRAMATURAS E DUPLICAÇÕES
        private const string UserPromptInstructions = @"INSTRUÇÕES RIGOROSAS PARA AVALIAÇÃO DE IMAGENS E REFEIÇÕES (LEIA ATENTAMENTE):
1. SE O USUÁRIO INFORMAR QUANTIDADES EM GRAMAS (ex: '40g', '140g', '200 gramas'), UNIDADES OU MEDIDAS NO TEXTO, USE EXATAMENTE O VALOR INFORMADO. É ESTRITAMENTE PROIBIDO ESTIMAR, INVENTAR, DUPLICAR, ADICIONAR ALIMENTOS NÃO MENCIONADOS OU MULTIPLICAR VALORES.
2. Identifique minuciosamente todos os alimentos visíveis ou mencionados no texto. Não junte alimentos diferentes no mesmo item (Ex: se o usuário disser 'peito de peru', registre apenas isso, não invente 'lanche de frango e peru' a menos que ele mencione frango).
3. FOTO sem quantidade no texto: estime o PESO REAL EM GRAMAS (g) de cada porção com base na foto.
4. SÓ TEXTO OU ÁUDIO, SEM QUANTIDADE: se o alimento tiver uma porção padrão óbvia (1 banana, 1 ovo, 1 pão francês, 1 fatia de queijo, 1 copo de leite), use essa porção e informe-a em portionDescription. Se a quantidade mudar muito as calorias e não houver porção padrão (ex.: 'arroz', 'macarrão', 'carne', 'batata frita'), NÃO ESTIME: pergunte (regra 6).
5. Seja conservador e realista nos macros.
6. PERGUNTE EM VEZ DE ADIVINHAR. Defina ""requiresUserClarification"": true, deixe ""items"": [] e ""totalMeal"" com zeros, e escreva em ""clarificationQuestion"" UMA pergunta curta, em português do Brasil, com exemplos de resposta, quando:
   - não der para saber o que foi consumido (ex.: 'comi um lanche', 'bebi 600 ml', 'comi aquilo de ontem', 'tomei mais um');
   - o alimento ou a bebida for genérico e as calorias variarem muito (ex.: 'um suco' sem dizer qual, 'um doce', 'um prato de comida', 'uma marmita');
   - faltar a quantidade conforme a regra 4;
   - a mensagem for confusa ou parecer incompleta.
7. Nunca registre item com 0 kcal, a não ser que ele de fato não tenha calorias (água, café ou chá sem açúcar). O ""totalMeal"" deve ser exatamente a soma dos ""items"".

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
      ""estimatedWeightG"": 0,
      ""calories"": 0,
      ""proteinG"": 0,
      ""carbsG"": 0,
      ""fatG"": 0,
      ""confidenceScore"": 0
    }
  ],
  ""totalMeal"": {
    ""calories"": 0,
    ""proteinG"": 0,
    ""carbsG"": 0,
    ""fatG"": 0
  },
  ""requiresUserClarification"": false,
  ""clarificationQuestion"": null
}";

        private const string DietReaderSystemPrompt = @"Você é um Assistente Clínico Especialista em Nutrição e Extração de Dados.
Extraia os alvos nutricionais e retorne APENAS um JSON válido.
FORMATO: { ""targetCalories"": 0, ""targetProteinG"": 0, ""targetCarbsG"": 0, ""targetFatG"": 0, ""dietaryRestrictions"": ""string"", ""favoriteFoods"": ""string"", ""prescribedMealPlan"": ""string"" }";

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

        public async Task<List<string>> GenerateProactiveSuggestionsAsync(MacroSummaryDto remainingBalance, string nextMealType, List<string>? pratosJaConsumidos = null, Glp1SuggestionContext? glp1 = null)
        {
            if (glp1 != null)
            {
                return await GenerateGlp1SuggestionsAsync(glp1, nextMealType, pratosJaConsumidos);
            }

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

        public const int MaxSugestoesGlp1 = 2;

        // Modo GLP-1: no máximo 2 sugestões de porção pequena, ricas em proteína e fáceis de digerir.
        // Nunca sugerir pular refeição ou comer menos. Sem fallback fixo: uma sugestão genérica poderia
        // ferir restrições ou aversões, então em caso de erro não mostramos nada.
        private async Task<List<string>> GenerateGlp1SuggestionsAsync(Glp1SuggestionContext glp1, string nextMealType, List<string>? pratosJaConsumidos)
        {
            try
            {
                var historicoPratos = (pratosJaConsumidos != null && pratosJaConsumidos.Any())
                    ? string.Join(", ", pratosJaConsumidos)
                    : "Nenhum alimento registrado.";

                var prompt =
                    "Você é um copiloto nutricional para uma pessoa que usa medicamento GLP-1 com acompanhamento médico. " +
                    $"Sugira NO MÁXIMO {MaxSugestoesGlp1} opções para a próxima refeição.\n\n" +
                    "REGRAS OBRIGATÓRIAS:\n" +
                    "- Porções PEQUENAS, ricas em proteína e fáceis de digerir (evite frituras, alimentos muito gordurosos e pratos volumosos).\n" +
                    "- Cada opção em uma frase curta, com a quantidade (ex.: '1 iogurte natural com 1 colher de whey').\n" +
                    $"- Respeite as restrições/alergias: {(string.IsNullOrWhiteSpace(glp1.Restricoes) ? "Nenhuma" : glp1.Restricoes)}\n" +
                    $"- Evite as aversões: {(string.IsNullOrWhiteSpace(glp1.Aversoes) ? "Nenhuma" : glp1.Aversoes)}\n" +
                    "- NUNCA sugira pular refeição, fazer jejum, comer menos ou reduzir porções.\n" +
                    "- Não fale de medicamento nem de dose.\n\n" +
                    $"PRÓXIMA REFEIÇÃO: {nextMealType}\n" +
                    $"PROTEÍNA QUE AINDA FALTA HOJE: {glp1.ProteinaRestanteG:F0} g\n" +
                    $"ALIMENTOS JÁ CONSUMIDOS HOJE: {historicoPratos}\n\n" +
                    @"Retorne APENAS um JSON: { ""suggestions"": [""opção 1"", ""opção 2""] }" +
                    Glp1SafetyRules;

                var requestBody = new
                {
                    model = _geminiModel,
                    temperature = 0.5,
                    max_tokens = 2000,
                    messages = new object[] { new { role = "user", content = prompt } }
                };

                var sugestoes = await CallGeminiAsync(requestBody, (responseStr) =>
                {
                    var jsonContent = ExtractContentFromResponse(responseStr, requireJson: true);
                    using var document = JsonDocument.Parse(jsonContent);
                    return TryGetPropertyCaseInsensitive(document.RootElement, "suggestions", out var suggs)
                        ? JsonSerializer.Deserialize<List<string>>(suggs.GetRawText()) ?? new List<string>()
                        : new List<string>();
                });

                return FiltrarSugestoesGlp1(sugestoes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Não foi possível gerar sugestões do Modo GLP-1.");
                return new List<string>();
            }
        }

        // Segunda barreira, independente do prompt: descarta sugestão de pular refeição, jejum ou comer menos
        public static List<string> FiltrarSugestoesGlp1(IEnumerable<string>? sugestoes)
        {
            if (sugestoes == null) return new List<string>();

            return sugestoes
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Where(s => !System.Text.RegularExpressions.Regex.IsMatch(
                    RemoverAcentos(s).ToLowerInvariant(),
                    @"\b(pul(e|ar|ando)|jejum|jejuar|coma menos|comer menos|reduz(a|ir)|diminu(a|ir)|evite comer|nao coma|deixe de comer)\b"))
                .Take(MaxSugestoesGlp1)
                .ToList();
        }

        public async Task<MealAnalysisResponseDto> ExtractMealDataAsync(ParseMealRequestDto request) => await ExtractMealDataAsync(request, null);

        public async Task<MealAnalysisResponseDto> ExtractMealDataAsync(ParseMealRequestDto request, List<ChatMessageLog>? chatHistory)
        {
            try
            {
                var systemPrompt = string.IsNullOrWhiteSpace(request.Glp1Context) ? SystemPrompt : SystemPrompt + Glp1SafetyRules;
                var requestBody = BuildRequestBodyWithHistory(chatHistory, request, _geminiModel, systemPrompt, UserPromptInstructions);
                return await CallGeminiAsync(requestBody, ParseMealResponse);
            }
            catch (Exception ex)
            {
                // Sem análise não há refeição: nada é gravado e o usuário é convidado a mandar de novo
                _logger.LogError(ex, "Erro crítico ao processar refeição com Gemini.");
                return MealAnalysisResponseDto.Falha();
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
            string textPayload = request.TextInput ?? string.Empty;
            string? imageBase64Final = request.Base64Image;

            // 📄 TRATAMENTO INTELIGENTE DE PDF VS IMAGEM
            if (!string.IsNullOrWhiteSpace(imageBase64Final))
            {
                // Remove prefixos data:uri caso existam para validar o conteúdo base64 puro
                string cleanBase64 = imageBase64Final.Contains(",") ? imageBase64Final.Split(',')[1] : imageBase64Final;

                // Se o base64 começar com a assinatura de um PDF (%PDF -> JVBERi0)
                if (cleanBase64.StartsWith("JVBERi0"))
                {
                    try
                    {
                        var pdfBytes = Convert.FromBase64String(cleanBase64);
                        using var pdf = UglyToad.PdfPig.PdfDocument.Open(pdfBytes);
                        var extractedText = string.Join("\n", pdf.GetPages().Select(p => p.Text));

                        // Adiciona o texto extraído ao payload e anula a imagem para evitar erro na API
                        textPayload += $"\n\n[Conteúdo Extraído do PDF do Cardápio]:\n{extractedText}";
                        imageBase64Final = null;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Erro ao extrair texto do PDF enviado na importação da dieta.");
                    }
                }
            }

            var userContentList = new List<object>
    {
        new { type = "text", text = $"{DietReaderSystemPrompt}\nDIETA: {textPayload}" }
    };

            // Se for uma imagem real (JPG/PNG), mantém o envio como image_url
            if (!string.IsNullOrWhiteSpace(imageBase64Final))
            {
                string imageBase64 = imageBase64Final.Contains(",") ? imageBase64Final : $"data:image/jpeg;base64,{imageBase64Final}";
                userContentList.Add(new { type = "image_url", image_url = new { url = imageBase64 } });
            }

            var requestBody = new
            {
                model = _geminiModel,
                temperature = 0.1,
                max_tokens = 1500,
                messages = new object[] { new { role = "user", content = userContentList.ToArray() } }
            };

            return await CallGeminiAsync(requestBody, (responseStr) =>
            {
                var jsonContent = ExtractContentFromResponse(responseStr, true);
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

                // Registro com 0 kcal só é legítimo quando todos os itens são água, café ou chá.
                // Qualquer outro 0 kcal é falha da IA: não inventamos valores.
                if (cal <= 0 && !isAdvice && !requiresClarification &&
                    !(items.Any() && items.All(i => EhBebidaSemCaloria(i.FoodName))))
                {
                    _logger.LogWarning("[ParseMealResponse] Registro sem calorias e sem itens válidos; tratado como falha: {Json}", jsonContent);
                    return MealAnalysisResponseDto.Falha();
                }

                return new MealAnalysisResponseDto(mealType, dishName, items, new MacroSummaryDto(cal, prot, carb, fat), requiresClarification, clarificationQuestion, isAdvice, adviceText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ParseMealResponse] Erro fatal no parser ao ler: {Json}", jsonContent);
                return MealAnalysisResponseDto.Falha();
            }
        }

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

            string textoBase = request.TextInput ?? "Analise esta refeição.";

            string protocoloContexto = string.IsNullOrWhiteSpace(request.ClinicalProtocol)
                ? string.Empty
                : $"\n\n🚨 [PROTOCOLO CLÍNICO / GUARDRAILS OBRIGATÓRIOS DO NUTRICIONISTA]:\n{request.ClinicalProtocol}\n" +
                  $"INSTRUÇÃO DE SEGURANÇA: Verifique rigorosamente se a refeição acima viola qualquer restrição, alergénio ou alimento proibido neste protocolo. " +
                  "Se houver violação, defina 'requiresUserClarification': true e preencha 'clarificationQuestion' alertando o paciente sobre a quebra do protocolo.";

            string textoFinalComRegras = $"{textoBase}{protocoloContexto}\n\n{userPromptInstructions}";

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
                temperature = 0.0, // <-- IMPORTANTE: Temperatura reduzida para 0 para forçar extração rigorosa de dados.
                max_tokens = 3000,
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

                // Sem JSON não há dado nenhum. Devolve um objeto vazio: na refeição vira falha (ParseMealResponse);
                // nos outros usos (dieta, metas, sugestões) o resultado é o mesmo de antes, porque a refeição
                // inventada que era devolvida aqui não tinha nenhum campo que eles leiam.
                _logger.LogWarning("[Gemini] Texto retornado não possui chaves JSON.");
                return "{}";
            }
            return rawText;
        }

        // Água, café ou chá (sem acento e sem diferenciar maiúsculas): os únicos itens que podem ter 0 kcal
        private static bool EhBebidaSemCaloria(string? foodName)
        {
            if (string.IsNullOrWhiteSpace(foodName)) return false;
            var nome = RemoverAcentos(foodName).ToLowerInvariant();
            return System.Text.RegularExpressions.Regex.IsMatch(nome, @"\b(agua|cafe|cha|water|coffee|tea)\b");
        }

        private static string RemoverAcentos(string texto)
        {
            var decomposto = texto.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposto.Length);
            foreach (var c in decomposto)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static List<string> GetFallbackSuggestions() => new List<string> { "Omelete de 3 ovos", "Frango grelhado e salada", "Shake de whey protein" };

        private static string? GetConfigValue(IConfiguration config, string primaryKey, string secondaryKey)
        {
            var val = config[primaryKey];
            if (!string.IsNullOrWhiteSpace(val)) return val;
            return config[secondaryKey];
        }

        public async Task<string> GenerateSmartSubstitutionAsync(string foodToSubstitute, MacroSummaryDto remainingBalance, string clinicalProtocol, string foodAversions, bool modoGlp1 = false)
        {
            try
            {
                var prompt = $@"
Você é um Nutricionista Clínico Especialista em Substituições Alimentares e Culinária Prática.
O paciente deseja substituir o seguinte item da dieta: '{foodToSubstitute}'.

CONTEXTO CLÍNICO E REGRAS:
- Saldo calórico e macros restantes no dia: {remainingBalance.Calories} kcal | Proteína: {remainingBalance.ProteinG}g | Carbs: {remainingBalance.CarbsG}g | Gordura: {remainingBalance.FatG}g
- Protocolo Clínico / Guardrails (OBRIGATÓRIO RESPEITAR): {(string.IsNullOrWhiteSpace(clinicalProtocol) ? "Nenhum restritivo" : clinicalProtocol)}
- Aversões / Alimentos que o paciente odeia (EVITAR): {(string.IsNullOrWhiteSpace(foodAversions) ? "Nenhuma" : foodAversions)}

INSTRUÇÕES:
1. Forneça 3 opções práticas e equivalentes de substituição que respeitem rigidamente as restrições clínicas e aversões.
2. Para cada opção, indique a quantidade aproximada e os macronutrientes estimados.
3. Escreva de forma amigável, curta e direta para o WhatsApp, usando emojis.
Retorne APENAS o texto pronto para envio.
";
                if (modoGlp1)
                {
                    prompt += Glp1SafetyRules + "\n- Nas substituições, prefira porções pequenas, ricas em proteína e fáceis de digerir.";
                }

                var requestBody = new
                {
                    model = _geminiModel,
                    temperature = 0.5,
                    messages = new object[] { new { role = "user", content = prompt } }
                };

                return await CallGeminiAsync(requestBody, (r) => ExtractContentFromResponse(r, false).Trim());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar substituição inteligente.");
                return "⚠️ Não consegui calcular a substituição neste momento. Tente novamente em instantes!";
            }
        }

        public async Task<string> GenerateWeeklyProgressSummaryAsync(string patientName, decimal initialWeight, decimal currentWeight, int targetCalories, double avgConsumedCalories, int avgWaterMl, int streakDays)
        {
            try
            {
                decimal weightDiff = currentWeight - initialWeight;
                string weightStatus = weightDiff <= 0 ? $"Perdeu {Math.Abs(weightDiff):F1} kg" : $"Ganhou {weightDiff:F1} kg";

                var prompt = $@"
Você é um Nutricionista Clínico Especialista em Comportamento Alimentar e Motivação.
Escreva um relatório de progresso semanal amigável, empático e profissional para o paciente {patientName}.

DADOS DA SEMANA:
- Variação de Peso: {weightStatus} (Peso atual: {currentWeight} kg)
- Média de Calorias Consumidas: {avgConsumedCalories:F0} kcal (Meta diária: {targetCalories} kcal)
- Média de Ingestão de Água: {avgWaterMl} ml/dia
- Ofensiva (Dias seguidos a registar): {streakDays} dias

INSTRUÇÕES:
1. Comemore as pequenas vitórias e mantenha um tom encorajador.
2. Dê orientações práticas para a próxima semana com base na média calórica e água.
3. Use emojis e formate para o WhatsApp (máximo 5 a 6 parágrafos curtos).
Retorne APENAS o texto pronto da mensagem.
";

                var requestBody = new
                {
                    model = _geminiModel,
                    temperature = 0.6,
                    messages = new object[] { new { role = "user", content = prompt } }
                };

                return await CallGeminiAsync(requestBody, (r) => ExtractContentFromResponse(r, false).Trim());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar resumo semanal de progresso.");
                return $"{patientName}, parabéns pela dedicação esta semana! Continue firme nos seus objetivos. 🥗💪";
            }
        }

        public async Task<AutoCalculateGoalDto> CalculateNutritionalGoalsAsync(string userProfileText, string mainGoal)
        {
            try
            {
                var prompt = $@"
Você é um Nutricionista Clínico Esportivo MÁSTER. Sua tarefa é calcular as metas nutricionais diárias de um paciente com base no relato dele.

RELATO DO PACIENTE: '{userProfileText}'
OBJETIVO PRINCIPAL: '{mainGoal}'

INSTRUÇÕES DE CÁLCULO MENTAL (Não mostre, apenas aplique):
1. Extraia o Peso (kg), Altura (cm/m), Idade, Sexo e Nível de Atividade Física.
2. Calcule a Taxa Metabólica Basal (TMB) usando a fórmula de Mifflin-St Jeor. Se faltar algum dado, assuma valores médios sensatos (ex: 70kg, 170cm, 30 anos).
3. Calcule o Gasto Energético Total (GET) multiplicando pelo fator de atividade (1.2 sedentário a 1.9 muito ativo).
4. Ajuste as Calorias pelo Objetivo:
   - Emagrecimento: Déficit de 300 a 500 kcal.
   - Hipertrofia: Superávit de 300 a 500 kcal.
   - Manutenção: Igual ao GET.
5. Distribua os Macronutrientes:
   - Proteína: ~2.0g por kg de peso corporal.
   - Gordura: ~0.8g a 1.0g por kg de peso corporal.
   - Carboidratos: O restante das calorias (1g carbo = 4kcal, 1g prot = 4kcal, 1g gordura = 9kcal).
6. Água: 35ml a 40ml por kg de peso corporal.

Retorne APENAS um JSON válido neste formato rigoroso:
{{
    ""weightKg"": 0,
    ""targetCalories"": 0,
    ""targetProteinG"": 0,
    ""targetCarbsG"": 0,
    ""targetFatG"": 0,
    ""targetWaterMl"": 0,
    ""shortExplanation"": ""Uma frase curta explicando amigavelmente o cálculo""
}}
";

                var requestBody = new
                {
                    model = _geminiModel,
                    temperature = 0.1, // Temperatura baixa para cálculos exatos
                    messages = new object[] { new { role = "user", content = prompt } }
                };

                return await CallGeminiAsync(requestBody, (responseStr) =>
                {
                    var jsonContent = ExtractContentFromResponse(responseStr, true);
                    return JsonSerializer.Deserialize<AutoCalculateGoalDto>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? new AutoCalculateGoalDto { TargetCalories = 2000, TargetProteinG = 150, TargetCarbsG = 200, TargetFatG = 60, TargetWaterMl = 2500, WeightKg = 70 };
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao calcular metas nutricionais com IA.");
                return new AutoCalculateGoalDto { TargetCalories = 2000, TargetProteinG = 150, TargetCarbsG = 200, TargetFatG = 60, TargetWaterMl = 2500, WeightKg = 70 };
            }
        }
    }
}