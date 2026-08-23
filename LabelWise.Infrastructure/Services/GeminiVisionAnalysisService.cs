using System.Text;
using System.Text.Json;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;
using LabelWise.Domain.ValueObjects;
using LabelWise.Infrastructure.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services;

public class GeminiVisionAnalysisService : IVisionAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiVisionAnalysisService> _logger;
    private readonly string _apiKey;
    private readonly string _model;

    private const string PreGradingSystemPrompt =
    "Você é um classificador profissional de cartas (Grader) altamente rigoroso e objetivo nos padrões PSA, BGS e CGC. " +
    "Sua função é avaliar a condição física de imagens de uma carta e fornecer um laudo analítico. " +
    "DIRETRIZES CRÍTICAS DE SEGURANÇA: " +
    "1. AUTENTICIDADE: Assuma sempre que a carta enviada é 100% autêntica e oficial. NUNCA mencione proxy, reprodução ou customizada. " +
    "2. SEJA CONSERVADOR (INOCENTE ATÉ QUE SE PROVE O CONTRÁRIO): Só aponte um defeito se for inegável. Na dúvida, NÃO penalize a nota. " +
    "3. O PROBLEMA DA LUZ VS DESGASTE: Cartas brilhantes e bordas (especialmente as bordas azuis do verso) refletem luz facilmente. " +
    "NÃO confunda uma linha reta ou mancha de reflexo branco brilhante com 'whitening' (esbranquiçamento) ou 'chipping' (descascado). " +
    "O desgaste real tem uma textura irregular, revelando as fibras do papel. Reflexo de luz é liso. NA DÚVIDA SE É LUZ OU DEFEITO, ASSUMA QUE É LUZ e ignore. " +
    "Retorne APENAS um JSON válido em português.";

    private const string PreGradingUserPrompt = @"TAREFA: Gerar um laudo de pré-gradação físico rigoroso.
Você receberá 7 IMAGENS no total:
1. Frente Reta
2. Frente Inclinada
3. Verso Reto
4. Verso Inclinado
5. Zoom Cantos (Grid)
6. Zoom Bordas (Grid)
7. ZOOM DA ILUSTRAÇÃO (Foco total na arte central)

REGRAS OBRIGATÓRIAS PARA PREENCHIMENTO DO JSON:
1. cardName: Identifique o nome da carta e numeração.
2. Centering: OBRIGATÓRIO preencher 'centeringScore' e 'centeringDetails'.
3. Corners: OBRIGATÓRIO preencher 'cornersScore' e 'cornersDetails'. Use APENAS a foto 5. Ignore reflexos de luz.
4. Edges: OBRIGATÓRIO preencher 'edgesScore' e 'edgesDetails'. Use APENAS a foto 6. Ignore linhas finas de reflexo nas bordas azuis.
5. Surface: OBRIGATÓRIO preencher 'surfaceScore' e 'surfaceDetails'. USE EXCLUSIVAMENTE A FOTO 7 e as inclinadas. 
   - ATENÇÃO: Ignore completamente qualquer texto ou cabeçalho superior da carta. 
   - Foque estritamente no dano físico estrutural (o vinco/amassado localizado na bordinha superior externa) e na arte central.
6. estimatedGrade: Nota final estimada (ex: 'PSA 3.0').
7. isWorthGrading: true ou false.
8. verdictMessage: Resumo final.

ATENÇÃO: PREENCHA TODAS AS 12 CHAVES DO JSON. NENHUM CAMPO PODE FICAR ZERADO OU NULO.

Retorne EXATAMENTE este JSON:
{
  ""cardName"": ""string"",
  ""centeringScore"": 0.0,
  ""centeringDetails"": ""string"",
  ""cornersScore"": 0.0,
  ""cornersDetails"": ""string"",
  ""edgesScore"": 0.0,
  ""edgesDetails"": ""string"",
  ""surfaceScore"": 0.0,
  ""surfaceDetails"": ""string"",
  ""estimatedGrade"": ""string"",
  ""isWorthGrading"": false,
  ""verdictMessage"": ""string""
}";

    public GeminiVisionAnalysisService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GeminiVisionAnalysisService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("GeminiVision");
        _logger = logger;
        _apiKey = configuration["GeminiApiKey"] ?? throw new ArgumentNullException("GeminiVision:ApiKey");
        _model = configuration["Model"] ?? "gemini-1.5-flash";
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
    }

    public async Task<VisionConditionResult> AnalyzeCardConditionAsync(Stream frontImage, Stream backImage)
    {
        await Task.Delay(500);
        return new VisionConditionResult(true, CardCondition.SP, new List<DefectMap>());
    }

    public async Task<PreGradingAiResult> AnalyzePreGradingAsync(
        Stream frontStraight,
        Stream frontAngled,
        Stream backStraight,
        Stream backAngled)
    {
        try
        {
            _logger.LogInformation("[Gemini PreGrading] 🔍 Lendo streams e gerando zooms de cantos, bordas e arte...");

            var bytesFrontStraight = await StreamToBytesAsync(frontStraight);
            var bytesFrontAngled = await StreamToBytesAsync(frontAngled);
            var bytesBackStraight = await StreamToBytesAsync(backStraight);
            var bytesBackAngled = await StreamToBytesAsync(backAngled);

            // Geração automática dos zooms de alta precisão via OpenCV no backend
            var bytesCornersGrid = CardCroppingHelper.GenerateCornersZoomGrid(bytesFrontStraight, bytesBackStraight);
            var bytesEdgesGrid = CardCroppingHelper.GenerateEdgesZoomGrid(bytesFrontStraight, bytesBackStraight);
            var bytesArtBoxZoom = CardCroppingHelper.GenerateArtBoxZoom(bytesFrontStraight);

            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

            var requestBody = new
            {
                systemInstruction = new
                {
                    parts = new[] { new { text = PreGradingSystemPrompt } }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new { text = PreGradingUserPrompt },
                            new { inlineData = new { mimeType = "image/jpeg", data = Convert.ToBase64String(bytesFrontStraight) } },
                            new { inlineData = new { mimeType = "image/jpeg", data = Convert.ToBase64String(bytesFrontAngled) } },
                            new { inlineData = new { mimeType = "image/jpeg", data = Convert.ToBase64String(bytesBackStraight) } },
                            new { inlineData = new { mimeType = "image/jpeg", data = Convert.ToBase64String(bytesBackAngled) } },
                            new { inlineData = new { mimeType = "image/jpeg", data = Convert.ToBase64String(bytesCornersGrid) } },
                            new { inlineData = new { mimeType = "image/jpeg", data = Convert.ToBase64String(bytesEdgesGrid) } },
                            new { inlineData = new { mimeType = "image/jpeg", data = Convert.ToBase64String(bytesArtBoxZoom) } }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.0,
                    responseMimeType = "application/json"
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(endpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("[Gemini PreGrading] ❌ Status={Status}, Body={Body}", response.StatusCode, errorContent);
                throw new Exception("Falha ao comunicar com o serviço Gemini Vision.");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            var jsonText = ExtractGeminiTextResponse(responseString);

            var result = JsonSerializer.Deserialize<PreGradingAiResult>(
                jsonText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null) throw new Exception("Erro ao desserializar resposta do Gemini.");

            _logger.LogInformation("[Gemini PreGrading] ✅ Laudo completo gerado com sucesso para '{CardName}': Grade={G}", result.CardName, result.EstimatedGrade);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Gemini PreGrading] ❌ Erro inesperado na análise");
            throw;
        }
    }

    private async Task<byte[]> StreamToBytesAsync(Stream stream)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }

    private string ExtractGeminiTextResponse(string responseString)
    {
        using var doc = JsonDocument.Parse(responseString);
        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "{}";
    }
}