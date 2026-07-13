using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LabelWise.Application.Configuration;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.NutritionConversation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabelWise.Infrastructure.AI;

public sealed class OpenAIConversationService : IOpenAIConversationService
{
    private const int MaxHistoryMessages = 10;

    private const string SystemPrompt = """
Você é o assistente nutricional conversacional do Nutrição Certa.
Responda em português simples, direto e acolhedor.
Use SOMENTE os dados estruturados enviados no contexto: tabela nutricional, score, perfis, ingredientes, alergênicos, claims, nível de processamento, pontos positivos e pontos de atenção.
Não invente nutrientes, não recalcule scores, não estime valores ausentes e não diga que viu algo que não está no contexto.
Se a pergunta exigir um dado ausente, explique que esse dado não foi identificado na análise.
Considere possíveis contradições entre score alto e alertas graves: quando houver alergênicos, ultraprocessamento, muito sódio, açúcar ou muitos aditivos, recomende moderação mesmo que existam pontos positivos.
Se imageQuality, analysisQuality, score.reliability ou nutritionReliabilityScore indicarem leitura parcial/baixa confiança, responda com cautela, diga que a leitura pode estar imprecisa e evite conclusões absolutas.
Não substitua orientação de nutricionista ou médico.
""";

    private readonly HttpClient _httpClient;
    private readonly AzureOpenAiVisionOptions _options;
    private readonly ILogger<OpenAIConversationService> _logger;

    public OpenAIConversationService(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureOpenAiVisionOptions> options,
        ILogger<OpenAIConversationService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("OpenAI");
        _options = options.Value;
        _logger = logger;
        ConfigureHttpClient();
    }

    public Task<string> GenerateInitialMessageAsync(
        NutritionConversationContext context,
        CancellationToken cancellationToken = default)
    {
        const string instruction = "Gere a primeira mensagem do assistente em até 3 frases curtas. Combine tabela nutricional, ingredientes, alergênicos, processamento, claims e qualidade da leitura. Se a leitura for parcial, avise com naturalidade. Termine convidando o usuário a perguntar sobre emagrecimento, diabetes, hipertrofia ou alimentação infantil.";
        return SendAsync(context, [], instruction, cancellationToken);
    }

    public Task<string> GenerateReplyAsync(
        NutritionConversationContext context,
        IReadOnlyList<ConversationMessage> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        const string instruction = "Responda à última pergunta do usuário usando apenas o contexto combinado de tabela nutricional, ingredientes, alergênicos, claims, processamento, qualidade da leitura e histórico da conversa. Se a análise for parcial ou insegura, deixe isso claro.";
        return SendAsync(context, conversationHistory, instruction, cancellationToken);
    }

    private void ConfigureHttpClient()
    {
        if (!Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var endpoint))
        {
            _logger.LogWarning("OpenAI conversacional não configurado: endpoint inválido.");
            return;
        }

        _httpClient.BaseAddress = endpoint;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    private async Task<string> SendAsync(
        NutritionConversationContext context,
        IReadOnlyList<ConversationMessage> conversationHistory,
        string instruction,
        CancellationToken cancellationToken)
    {
        if (_httpClient.BaseAddress is null || string.IsNullOrWhiteSpace(_options.ApiKey))
            return string.Empty;

        try
        {
            var messages = BuildMessages(context, conversationHistory, instruction);
            var requestBody = new
            {
                model = _options.Model,
                messages,
                max_tokens = 500,
                temperature = 0.2
            };

            using var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(string.Empty, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Falha na chamada OpenAI conversacional. Status={Status}, Body={Body}",
                    response.StatusCode,
                    responseBody);
                return string.Empty;
            }

            return ExtractAssistantContent(responseBody);
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Timeout na chamada OpenAI conversacional.");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado na chamada OpenAI conversacional.");
            return string.Empty;
        }
    }

    private static object[] BuildMessages(
        NutritionConversationContext context,
        IReadOnlyList<ConversationMessage> conversationHistory,
        string instruction)
    {
        var contextJson = JsonSerializer.Serialize(context, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        var messages = new List<object>
        {
            new { role = "system", content = SystemPrompt },
            new
            {
                role = "user",
                content = $"Contexto nutricional estruturado para esta conversa (não use nenhuma informação fora dele):\n{contextJson}\n\nInstrução: {instruction}"
            }
        };

        foreach (var message in conversationHistory
            .Where(x => IsAllowedRole(x.Role) && !string.IsNullOrWhiteSpace(x.Content))
            .TakeLast(MaxHistoryMessages))
        {
            messages.Add(new
            {
                role = message.Role,
                content = message.Content
            });
        }

        return messages.ToArray();
    }

    private static bool IsAllowedRole(string role) =>
        string.Equals(role, "user", StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase);

    private static string ExtractAssistantContent(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;

        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            return string.Empty;

        var message = choices[0].GetProperty("message");
        return message.TryGetProperty("content", out var content)
            ? content.GetString()?.Trim() ?? string.Empty
            : string.Empty;
    }
}
