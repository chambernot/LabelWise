using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LabelWise.Application.Configuration;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.IngredientAnalysis;
using LabelWise.Infrastructure.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabelWise.Infrastructure.AI;

public sealed class OpenAIIngredientAnalysisService : IOpenAIIngredientAnalysisService
{
    private const string SystemPrompt = "Você é um mecanismo profissional de OCR especializado em rótulos de alimentos.\r\n\r\nSua primeira responsabilidade é LER. Sua segunda responsabilidade é EXTRAIR.\r\n\r\nREGRAS ABSOLUTAS\r\n\r\n- Nunca invente texto.\r\n- Nunca complete palavras ilegíveis.\r\n- Nunca utilize conhecimento prévio do produto.\r\n- Nunca deduza ingredientes.\r\n- Nunca substitua palavras.\r\n- Nunca normalize nomes.\r\n- Nunca corrija erros de impressão.\r\n- Preserve exatamente a grafia encontrada.\r\n- Preserve maiúsculas, minúsculas e acentuação quando legíveis.\r\n- Preserve exatamente a ordem em que os ingredientes aparecem.\r\n\r\nPROCESSO OBRIGATÓRIO\r\n\r\nAntes de responder:\r\n\r\n1. Faça uma leitura completa da imagem.\r\n2. Faça uma segunda leitura procurando textos pequenos.\r\n3. Faça uma terceira leitura nas regiões próximas ao rodapé, laterais e abaixo da tabela nutricional.\r\n4. Somente após concluir a leitura completa extraia as informações solicitadas.\r\n\r\nIMPORTANTE\r\n\r\nSua prioridade é OCR.\r\n\r\nNão resuma.\r\n\r\nNão interprete antes de terminar a leitura.\r\n\r\nNão ignore textos por parecerem irrelevantes.\r\n\r\nQuando houver dúvida sobre um caractere, mantenha apenas o que puder ler com confiança.\r\n\r\nResponda exclusivamente com JSON válido.";

    private const string UserPrompt = @"Você está analisando um rótulo de alimento.

OBJETIVO

Localizar e extrair com a maior precisão possível a LISTA DE INGREDIENTES da embalagem.

Antes de gerar o JSON execute mentalmente este processo:

PASSO 1

Leia toda a imagem do canto superior esquerdo até o canto inferior direito.

PASSO 2

Faça uma segunda leitura procurando especificamente qualquer bloco que contenha:

- INGREDIENTES
- INGREDIENTE
- COMPOSIÇÃO
- COMPOSIÇÃO DO PRODUTO
- INGREDIENTES:
- ingredientes
- ingrediente
- 

Também considere títulos parcialmente visíveis ou parcialmente cortados.

PASSO 3

Quando localizar esse bloco, releia SOMENTE essa região cuidadosamente.

Leia linha por linha.

Não pule nenhuma linha.

Não pule ingredientes escritos em fonte pequena.

Leia inclusive linhas próximas ao rodapé.

Leia inclusive texto abaixo da tabela nutricional.

Leia inclusive texto em outras cores.

PASSO 4

Continue lendo até encontrar um dos seguintes delimitadores:

- ALÉRGICOS
- INFORMAÇÃO NUTRICIONAL
- TABELA NUTRICIONAL
- MODO DE PREPARO
- CONSERVAÇÃO
- ARMAZENAMENTO
- fim da imagem

PASSO 5

Extraia também qualquer informação sobre alergênicos.

Considere expressões como:

- CONTÉM
- PODE CONTER
- PODE CONTER TRAÇOS
- FABRICADO EM EQUIPAMENTO
- ALÉRGICOS

Essas informações devem ser retornadas apenas em ""allergens"".

Nunca transforme alergênicos em ingredientes.

PASSO 6

Extraia literalmente qualquer claim encontrado.

Exemplos:

- vegano
- vegetariano
- plant based
- sem lactose
- não contém lactose
- sem glúten
- contém glúten
- zero açúcar
- zero adição de açúcar
- alto teor de proteína
- fonte de proteína
- integral
- orgânico
- sem conservantes
- sem corantes

Não invente claims.

PASSO 7

Preencha rawExtractedText com todas as linhas efetivamente utilizadas para montar a resposta.

IMPORTANTE

Se existir qualquer parte legível da lista de ingredientes, extraia tudo o que conseguir.

Nunca deixe ingredientsDetected vazio apenas porque parte do texto está ilegível.

Somente deixe ingredientsDetected vazio quando tiver absoluta certeza de que a embalagem não possui nenhuma seção de ingredientes visível.

Não utilize informações da tabela nutricional para criar ingredientes.

Não utilize conhecimento prévio do produto.

Não complete palavras parcialmente ilegíveis.

JSON

{
  ""productName"": string | null,
  ""brand"": string | null,
  ""ingredientsDetected"": [string],
  ""allergens"": [string],
  ""claims"": [string],
  ""rawExtractedText"": [string],
  ""warnings"": [string]
}

Retorne exclusivamente o JSON.";

    private readonly HttpClient _httpClient;
    private readonly AzureOpenAiVisionOptions _options;
    private readonly ILogger<OpenAIIngredientAnalysisService> _logger;

    public OpenAIIngredientAnalysisService(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureOpenAiVisionOptions> options,
        ILogger<OpenAIIngredientAnalysisService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("OpenAI");
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.Endpoint))
            _httpClient.BaseAddress = new Uri(_options.Endpoint);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        _httpClient.Timeout = TimeSpan.FromSeconds(35);
    }

    public async Task<IngredientExtractionResult?> AnalyzeAsync(
        byte[] imageBytes,
        string? mimeType,
        string? ocrContext,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("[IngredientAnalysis.OpenAI] OpenAI Vision não configurado.");
            return null;
        }

        try
        {

        

            var base64Image = Convert.ToBase64String(imageBytes);
            var resolvedMimeType = ResolveMimeType(mimeType, base64Image);
            var prompt = string.IsNullOrWhiteSpace(ocrContext)
    ? UserPrompt
    : $"""
{UserPrompt}


{ocrContext}
""";
            var requestBody = new
            {
                model = _options.Model,
                messages = new object[]
                {
                    new { role = "system", content = SystemPrompt },
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = prompt },
                            new
                            {
                                type = "image_url",
                                image_url = new { url = $"data:{resolvedMimeType};base64,{base64Image}", detail = "high" }
                            }
                        }
                    }
                },
                max_tokens = 1800,
                temperature = 0,
                top_p = 0.1
            };
            
            using var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("", content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "[IngredientAnalysis.OpenAI] Falha na requisição. Status={Status}, Body={Body}",
                    response.StatusCode,
                    responseBody);
                return null;
            }

            return ParseResponse(responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IngredientAnalysis.OpenAI] Erro inesperado na análise de ingredientes.");
            return null;
        }
    }

    private IngredientExtractionResult? ParseResponse(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
                return null;

            var json = ExtractJson(content);
            return JsonSerializer.Deserialize<IngredientExtractionResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[IngredientAnalysis.OpenAI] Falha ao parsear resposta.");
            return null;
        }
    }

    private static string ExtractJson(string value)
    {
        var clean = value.Trim();
        if (clean.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = clean.IndexOf('\n');
            var lastFence = clean.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewLine >= 0 && lastFence > firstNewLine)
                clean = clean[(firstNewLine + 1)..lastFence].Trim();
        }

        var start = clean.IndexOf('{');
        var end = clean.LastIndexOf('}');
        return start >= 0 && end > start ? clean[start..(end + 1)] : clean;
    }

    private static string ResolveMimeType(string? mimeType, string base64Image)
    {
        if (!string.IsNullOrWhiteSpace(mimeType) && mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return mimeType;

        return ImageFormatHelper.DetectMimeTypeFromBase64(base64Image);
    }
}
