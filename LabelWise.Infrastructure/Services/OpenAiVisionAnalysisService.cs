using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;
using LabelWise.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace LabelWise.Infrastructure.Services;

/// <summary>
/// Implementação de análise de cartas (Trading Cards) via OpenAI Chat Completions API com Vision.
/// 
/// Fluxo:
///   1. Converte as imagens (Frente e Verso) para base64.
///   2. Envia para OpenAI /v1/chat/completions com system prompt estrito.
///   3. Recebe JSON estruturado com mapa de defeitos e condição.
///   4. Mapeia para o domínio (VisionConditionResult).
/// </summary>
public sealed class OpenAiVisionAnalysisService : IVisionAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiVisionAnalysisService> _logger;
    private readonly string _apiKey;
    private readonly string _endpoint;
    private readonly string _model;

    private const string SystemPrompt =
        "Você é um especialista MÁSTER em gradação de cartas colecionáveis (Trading Cards) e Visão Computacional. " +
        "Sua tarefa é analisar imagens da frente e do verso de uma carta e extrair dados visuais precisos. " +
        "Você NUNCA deve inventar defeitos. Se a imagem não for nítida, não suponha danos. " +
        "Você sempre deve retornar APENAS um JSON válido, sem texto fora dele, sem markdown de bloco de código (`​`​`json).";

    private const string UserPrompt = @"TAREFA: Analisar a condição da carta nas imagens fornecidas (Frente e Verso) e devolver o laudo como JSON.

════════════════════════════════════════════════════════════════
REGRA CRÍTICA — IDENTIFICAÇÃO DA CARTA
════════════════════════════════════════════════════════════════
Antes de qualquer extração, verifique: As imagens mostram claramente uma carta colecionável (ex: Pokémon, Magic, Yu-Gi-Oh)?
Se a resposta for NÃO: Retorne isAuthentic = false e condition = 'Damaged', com mapa de defeitos vazio.

════════════════════════════════════════════════════════════════
REGRA 1 — AUTENTICIDADE E IMPRESSÃO
════════════════════════════════════════════════════════════════
Analise o padrão visual, cores e bordas. Se notar anomalias grotescas de cor, fontes incorretas ou bordas mal cortadas que indiquem falsificação óbvia, marque isAuthentic como false. Caso pareça normal, true.

════════════════════════════════════════════════════════════════
REGRA 2 — MAPEAMENTO DE DEFEITOS (WHITENING, RISCOS, AMASSADOS)
════════════════════════════════════════════════════════════════
Você deve vasculhar as imagens, especialmente o VERSO (fundo escuro da carta), em busca de:
• Whitening (Desgaste branco nas bordas e cantos)
• Scratches (Riscos na superfície/holografia)
• Dents (Amassados ou marcas de unha)

Para cada defeito real encontrado, adicione um objeto na lista de 'defects'.
Use coordenadas relativas (0.0 a 1.0) onde:
X, Y = ponto inicial do defeito (0,0 é o topo-esquerda).
Width, Height = tamanho relativo do defeito.

Exemplo de Whitening no canto inferior direito:
{ ""defectType"": ""Whitening"", ""x"": 0.90, ""y"": 0.95, ""width"": 0.10, ""height"": 0.05 }

SE NÃO HOUVER DEFEITO VISÍVEL, retorne a lista vazia []. NÃO INVENTE DEFEITOS.

════════════════════════════════════════════════════════════════
REGRA 3 — CLASSIFICAÇÃO GERAL (CONDITION)
════════════════════════════════════════════════════════════════
Baseado nos defeitos, classifique estritamente como:
• 'NM' (Near Mint): Perfeita ou com no máximo 1-2 micro pontos brancos.
• 'SP' (Slightly Played): Desgaste leve nas bordas, riscos muito superficiais.
• 'MP' (Moderately Played): Bordas visivelmente brancas, cantos gastos.
• 'HP' (Heavily Played): Vincos severos, muito desgaste, sujeira.
• 'Damaged': Rasgada, dobrada ao meio, molhada.

════════════════════════════════════════════════════════════════
ESTRUTURA DE SAÍDA OBRIGATÓRIA (JSON PURO)
════════════════════════════════════════════════════════════════
{
  ""isAuthentic"": boolean,
  ""condition"": ""NM"" | ""SP"" | ""MP"" | ""HP"" | ""Damaged"",
  ""defects"": [
    {
      ""defectType"": string,
      ""x"": number,
      ""y"": number,
      ""width"": number,
      ""height"": number
    }
  ],
  ""reasoning"": string (Breve explicação técnica da sua decisão)
}";

    public OpenAiVisionAnalysisService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OpenAiVisionAnalysisService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("OpenAI");
        _logger = logger;

        // Idealmente, pegue de um IOptions<AzureOpenAiVisionOptions> assim como fez no outro
        _apiKey = configuration["OpenAi:ApiKey"] ?? throw new ArgumentNullException("OpenAi:ApiKey");
        _endpoint = configuration["OpenAi:Endpoint"] ?? "https://api.openai.com/v1/chat/completions";
        _model = configuration["OpenAi:Model"] ?? "gpt-4-vision-preview";

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.Timeout = TimeSpan.FromSeconds(45); // Imagens pesadas podem demorar
    }

    public async Task<VisionConditionResult> AnalyzeCardConditionAsync(Stream frontImage, Stream backImage)
    {
        try
        {
            _logger.LogInformation("[OpenAI CardVision] ═══ Iniciando análise de Carta ═══");

            var frontBase64 = await ConvertStreamToBase64Async(frontImage);
            var backBase64 = await ConvertStreamToBase64Async(backImage);

            if (string.IsNullOrEmpty(frontBase64) || string.IsNullOrEmpty(backBase64))
            {
                _logger.LogError("[OpenAI CardVision] ❌ Imagens inválidas ou vazias.");
                throw new ArgumentException("As imagens da frente e do verso são obrigatórias.");
            }

            var requestBody = BuildRequestBody(frontBase64, backBase64);

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            _logger.LogInformation("[OpenAI CardVision] 🚀 Enviando imagens Frente e Verso para análise...");

            var response = await _httpClient.PostAsync(_endpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("[OpenAI CardVision] ❌ Falha na requisição. Status={Status}, Body={Body}", response.StatusCode, errorContent);
                throw new Exception("Falha ao comunicar com o serviço de Visão Computacional.");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            var jsonContent = ExtractJsonFromResponse(responseString);

            var aiResult = JsonSerializer.Deserialize<AiCardEvaluationResponse>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (aiResult == null)
            {
                _logger.LogWarning("[OpenAI CardVision] ⚠️ Falha ao parsear resposta do LLM.");
                throw new Exception("Não foi possível processar o laudo da imagem.");
            }

            _logger.LogInformation("[OpenAI CardVision] ✅ Análise concluída — Autêntica: {IsAuthentic}, Condição: {Condition}, Defeitos Encontrados: {DefectsCount}",
                aiResult.IsAuthentic, aiResult.Condition, aiResult.Defects?.Count ?? 0);

            _logger.LogInformation("[OpenAI CardVision] 🧠 Reasoning: {Reasoning}", aiResult.Reasoning);

            // Mapeando a resposta DTO do OpenAI para as entidades de Domínio
            var mappedCondition = Enum.TryParse<CardCondition>(aiResult.Condition, true, out var conditionEnum)
                ? conditionEnum
                : CardCondition.Damaged;

            var domainDefects = aiResult.Defects?.Select(d => new DefectMap(
                d.DefectType ?? "Unknown",
                d.X,
                d.Y,
                d.Width,
                d.Height)).ToList() ?? new List<DefectMap>();

            return new VisionConditionResult(aiResult.IsAuthentic, mappedCondition, domainDefects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OpenAI CardVision] ❌ Erro inesperado na análise");
            throw;
        }
    }

    private async Task<string> ConvertStreamToBase64Async(Stream stream)
    {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return Convert.ToBase64String(memoryStream.ToArray());
    }

    private object BuildRequestBody(string frontBase64, string backBase64)
    {
        return new
        {
            model = _model,
            temperature = 0.0, // Respostas determinísticas e estritas
            max_tokens = 800,
            // 👇 A MUDANÇA ESTÁ AQUI: alterado de new[] para new object[]
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = SystemPrompt
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = UserPrompt },
                        new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{frontBase64}", detail = "high" } },
                        new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{backBase64}", detail = "high" } }
                    }
                }
            }
        };
    }

    private string ExtractJsonFromResponse(string responseString)
    {
        // Parseia o payload padrão da OpenAI
        using var document = JsonDocument.Parse(responseString);
        var rawText = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

        // Remove markdown caso o modelo tenha ignorado a instrução (ex: ```json ... ```)
        rawText = rawText.Trim();
        if (rawText.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            rawText = rawText.Substring(7);
        if (rawText.StartsWith("```"))
            rawText = rawText.Substring(3);
        if (rawText.EndsWith("```"))
            rawText = rawText.Substring(0, rawText.Length - 3);

        return rawText.Trim();
    }

    private const string PreGradingSystemPrompt =
        "Você é um classificador profissional de cartas estilo PSA, BGS e CGC. " +
        "Sua função é avaliar imagens da frente e do verso de uma carta e dar notas de 1.0 a 10.0 (em incrementos de 0.5) " +
        "para as 4 categorias principais: Centering, Corners, Edges e Surface. Retorne APENAS um JSON válido.";

    private const string PreGradingUserPrompt = @"TAREFA: Avaliar as 4 sub-notas para gradação profissional.

Avalie rigorosamente (Nota 1.0 a 10.0):
1. Centering (Centralização): As bordas da frente e do verso são simétricas?
2. Corners (Cantos): Há whitening (branco), desfiamento ou amassados nos 4 cantos?
3. Edges (Bordas): As laterais apresentam desgaste ou cortes irregulares de fábrica?
4. Surface (Superfície): Há riscos, sujeira, marcas de impressão ou vincos?

ESTRUTURA DE SAÍDA OBRIGATÓRIA (JSON PURO):
{
  ""centering"": number,
  ""corners"": number,
  ""edges"": number,
  ""surface"": number
}";

    public async Task<PreGradingAiResult> AnalyzePreGradingAsync(Stream frontImage, Stream backImage)
    {
        _logger.LogInformation("[OpenAI PreGrading] 🔍 Iniciando simulação de sub-notas...");

        var frontBase64 = await ConvertStreamToBase64Async(frontImage);
        var backBase64 = await ConvertStreamToBase64Async(backImage);

        // Reaproveita o construtor de request, mas passa o prompt específico de gradação
        var requestBody = new
        {
            model = _model,
            temperature = 0.0,
            max_tokens = 300,
            messages = new object[]
            {
                new { role = "system", content = PreGradingSystemPrompt },
                new { role = "user", content = new object[]
                    {
                        new { type = "text", text = PreGradingUserPrompt },
                        new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{frontBase64}", detail = "high" } },
                        new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{backBase64}", detail = "high" } }
                    }
                }
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(_endpoint, content);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var jsonContent = ExtractJsonFromResponse(responseString);

        var result = JsonSerializer.Deserialize<PreGradingAiResult>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (result == null) throw new Exception("Falha ao processar notas da IA.");

        _logger.LogInformation("[OpenAI PreGrading] ✅ Notas geradas: Centering={C}, Corners={Co}, Edges={E}, Surface={S}",
            result.Centering, result.Corners, result.Edges, result.Surface);

        return result;
    }

    // --- Classes DTO Privadas para Mapeamento ---

    private class AiCardEvaluationResponse
    {
        public bool IsAuthentic { get; set; }
        public string? Condition { get; set; }
        public List<AiDefect>? Defects { get; set; }
        public string? Reasoning { get; set; }
    }

    private class AiDefect
    {
        public string? DefectType { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }
}