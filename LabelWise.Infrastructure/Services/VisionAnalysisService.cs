using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;
using LabelWise.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services;

public class VisionAnalysisService : IVisionAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VisionAnalysisService> _logger;
    private readonly string _endpoint;
    private readonly string _model;
    private readonly string _apiKey;

    // PROMPTS ATUALIZADOS PARA LAUDO COMPLETO E DETALHADO
    private const string PreGradingSystemPrompt =
        "Você é um classificador profissional de cartas (Grader) nos padrões PSA, BGS e CGC. " +
        "Sua função é avaliar detalhadamente imagens de uma carta e fornecer um laudo analítico em português. " +
        "Você deve identificar a carta, analisar as 4 categorias dando notas de 1.0 a 10.0 (incrementos de 0.5) " +
        "e descrever em detalhes os defeitos visualizados na frente e no verso para cada item. " +
        "Retorne APENAS um JSON válido.";

    private const string PreGradingUserPrompt = @"TAREFA: Gerar um laudo de pré-gradação profissional detalhado.
Você receberá 4 imagens: 
1. Frente (Visão Reta)
2. Frente (Inclinada com luz)
3. Verso (Visão Reta)
4. Verso (Inclinada com luz)

REGRAS DE AVALIAÇÃO E PREENCHIMENTO DOS CAMPOS:
1. cardName: Identifique o nome completo da carta e sua numeração/coleção (ex: 'Jaula de Batalha (116/094)').
2. Centering:
   - centeringScore: Nota de 1.0 a 10.0.
   - centeringDetails: Descrição textual detalhada da proporção/simetria das bordas na Frente e no Verso.
3. Corners:
   - cornersScore: Nota de 1.0 a 10.0.
   - cornersDetails: Descrição textual dos cantos (presença de whitening, marcas de corte ou desgaste na Frente e no Verso).
4. Edges:
   - edgesScore: Nota de 1.0 a 10.0.
   - edgesDetails: Descrição textual do desgaste nas bordas (ex: silvering na frente, whitening ou desgaste na camada azul do verso).
5. Surface:
   - surfaceScore: Nota de 1.0 a 10.0.
   - surfaceDetails: Descrição textual da textura, brilho, riscos superficiais (scratches), amassados ou vincos revelados pelas imagens inclinadas.
6. estimatedGrade: Nota final estimada no formato padrão das certificadoras (ex: 'PSA/CGC 6.0 (EX-MT)'). A nota é guiada pela menor sub-nota se houver dano.
7. isWorthGrading: 'true' se a nota for alta (geralmente >= 9.0) e compensar financeiramente o envio. 'false' se o desgaste limitar a nota e não cobrir os custos.
8. verdictMessage: Resumo justificando a recomendação de forma direta.

ESTRUTURA DE SAÍDA OBRIGATÓRIA (JSON PURO COM ESSAS CHAVES EXATAS):
{
  ""cardName"": ""string"",
  ""centeringScore"": number,
  ""centeringDetails"": ""string"",
  ""cornersScore"": number,
  ""cornersDetails"": ""string"",
  ""edgesScore"": number,
  ""edgesDetails"": ""string"",
  ""surfaceScore"": number,
  ""surfaceDetails"": ""string"",
  ""estimatedGrade"": ""string"",
  ""isWorthGrading"": boolean,
  ""verdictMessage"": ""string""
}";

    public VisionAnalysisService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<VisionAnalysisService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("OpenAiVision");
        _logger = logger;
        _apiKey = configuration["OpenAiVision:ApiKey"] ?? throw new ArgumentNullException("OpenAiVision:ApiKey");
        _endpoint = configuration["OpenAiVision:Endpoint"] ?? "https://api.openai.com/v1/chat/completions";
        _model = configuration["OpenAiVision:Model"] ?? "gpt-4-vision-preview";

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
    }

    public async Task<VisionConditionResult> AnalyzeCardConditionAsync(Stream frontImage, Stream backImage)
    {
        await Task.Delay(1500);
        var mockDefects = new List<DefectMap>
        {
            new DefectMap("Whitening", 120.5f, 45.0f, 10.0f, 5.0f),
            new DefectMap("Scratch", 300.0f, 150.2f, 2.0f, 45.0f)
        };
        return new VisionConditionResult(true, CardCondition.SP, mockDefects);
    }

    public async Task<PreGradingAiResult> AnalyzePreGradingAsync(
        Stream frontStraight,
        Stream frontAngled,
        Stream backStraight,
        Stream backAngled)
    {
        try
        {
            _logger.LogInformation("[OpenAI PreGrading] 🔍 Iniciando simulação com lote de 4 imagens...");

            var b64FrontStraight = await ConvertStreamToBase64Async(frontStraight);
            var b64FrontAngled = await ConvertStreamToBase64Async(frontAngled);
            var b64BackStraight = await ConvertStreamToBase64Async(backStraight);
            var b64BackAngled = await ConvertStreamToBase64Async(backAngled);

            var requestBody = new
            {
                model = _model,
                temperature = 0.0,
                max_tokens = 1200, // Aumentado para comportar o laudo detalhado sem cortar o JSON
                messages = new object[]
                {
                    new { role = "system", content = PreGradingSystemPrompt },
                    new { role = "user", content = new object[]
                        {
                            new { type = "text", text = PreGradingUserPrompt },
                            new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{b64FrontStraight}", detail = "high" } },
                            new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{b64FrontAngled}", detail = "high" } },
                            new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{b64BackStraight}", detail = "high" } },
                            new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{b64BackAngled}", detail = "high" } }
                        }
                    }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_endpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("[OpenAI PreGrading] ❌ Falha na requisição: Status={Status}, Body={Body}", response.StatusCode, errorContent);
                throw new Exception("Falha ao comunicar com o serviço de Visão Computacional.");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            var jsonContent = ExtractJsonFromResponse(responseString);

            var result = JsonSerializer.Deserialize<PreGradingAiResult>(
                jsonContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null) throw new Exception("Não foi possível processar as notas da IA.");

            _logger.LogInformation("[OpenAI PreGrading] ✅ Laudo gerado para '{CardName}': Grade={G}, Centering={C}, Corners={Co}, Edges={E}, Surface={S}",
                result.CardName, result.EstimatedGrade, result.CenteringScore, result.CornersScore, result.EdgesScore, result.SurfaceScore);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OpenAI PreGrading] ❌ Erro inesperado na simulação de gradação");
            throw;
        }
    }

    private async Task<string> ConvertStreamToBase64Async(Stream stream)
    {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return Convert.ToBase64String(memoryStream.ToArray());
    }

    private string ExtractJsonFromResponse(string responseString)
    {
        using var document = JsonDocument.Parse(responseString);
        var rawText = document.RootElement
            .GetProperty("choices")[0]
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
}