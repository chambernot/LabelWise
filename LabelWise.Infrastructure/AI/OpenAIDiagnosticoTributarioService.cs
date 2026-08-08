using LabelWise.Application.Configuration;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models;
using LabelWise.Domain.Models.Tributario;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LabelWise.Infrastructure.AI;

public sealed class OpenAIDiagnosticoTributarioService
    : IOpenAIDiagnosticoTributarioService
{
    private const string SystemPrompt = """
Você é um consultor tributário especialista na Reforma Tributária Brasileira.

Especialidades:

- CBS
- IBS
- Imposto Seletivo
- Lucro Presumido
- Lucro Real
- Simples Nacional
- Planejamento Tributário
- Compliance Fiscal

Nunca invente informações.

Caso faltem dados informe isso nas recomendações.

Retorne SOMENTE JSON.

Nunca utilize Markdown.
""";

    private readonly HttpClient _httpClient;
    private readonly AzureOpenAiVisionOptions _options;
    private readonly ILogger<OpenAIDiagnosticoTributarioService> _logger;
    private readonly TributarioPromptBuilder _promptBuilder;

    public OpenAIDiagnosticoTributarioService(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureOpenAiVisionOptions> options,
        TributarioPromptBuilder promptBuilder,
        ILogger<OpenAIDiagnosticoTributarioService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("OpenAI");
        _options = options.Value;
        _logger = logger;
        _promptBuilder = promptBuilder;

        if (!string.IsNullOrWhiteSpace(_options.Endpoint))
            _httpClient.BaseAddress = new Uri(_options.Endpoint);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        _httpClient.Timeout = TimeSpan.FromSeconds(60);
    }

    public async Task<DiagnosticoTributarioResult?> AnalyzeAsync(
        EmpresaDiagnosticoRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint))
            return null;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return null;

        try
        {
            var stopwatch = Stopwatch.StartNew();

            var prompt = _promptBuilder.Build(request);

            var body = new
            {
                model = _options.Model,
                temperature = 0,

                top_p = 0.1,

                presence_penalty = 0,

                frequency_penalty = 0,
                max_tokens = 2500,
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
                        content = prompt
                    }
                }
            };

            using var content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                "",
                content,
                cancellationToken);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(json);
                return null;
            }

            stopwatch.Stop();

            var result = ParseResponse(json);

            if (result != null)
            {
                result.ModeloIA = _options.Model;
                result.TempoProcessamentoMs = stopwatch.ElapsedMilliseconds;
                result.DataAnalise = DateTime.UtcNow;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar OpenAI.");
            return null;
        }
    }

    private DiagnosticoTributarioResult? ParseResponse(string response)
    {
        try
        {
            using var doc = JsonDocument.Parse(response);

            var content =
                doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
                return null;

            var json = ExtractJson(content);

            return JsonSerializer.Deserialize<DiagnosticoTributarioResult>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao converter resposta.");
            return null;
        }
    }

    private static string ExtractJson(string value)
    {
        var clean = value.Trim();

        if (clean.StartsWith("```"))
        {
            var first = clean.IndexOf('\n');
            var last = clean.LastIndexOf("```");

            if (first >= 0 && last > first)
                clean = clean[(first + 1)..last].Trim();
        }

        var start = clean.IndexOf('{');
        var end = clean.LastIndexOf('}');

        if (start >= 0 && end > start)
            return clean[start..(end + 1)];

        return clean;
    }
}