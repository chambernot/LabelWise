using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using LabelWise.Application.Configuration;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Infrastructure.AI.Parsers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabelWise.Infrastructure.AI;

/// <summary>
/// Chama o Azure Document Intelligence com o modelo "prebuilt-layout" e
/// delega o parse das tabelas para <see cref="DocumentIntelligenceNutritionParser"/>.
///
/// Timeout e retries são configuráveis via <see cref="AzureDocumentIntelligenceOptions"/>.
/// </summary>
public sealed class DocumentIntelligenceService : IDocumentIntelligenceService
{
    private readonly AzureDocumentIntelligenceOptions _options;
    private readonly ILogger<DocumentIntelligenceService> _logger;

    public DocumentIntelligenceService(
        IOptions<AzureDocumentIntelligenceOptions> options,
        ILogger<DocumentIntelligenceService> logger)
    {
        _options = options.Value;
        _logger  = logger;
    }

    public async Task<DocumentIntelligenceNutritionResult?> AnalyzeAsync(
        byte[] imageBytes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("[DocumentIntelligence] Endpoint ou ApiKey não configurados — serviço desabilitado.");
            return null;
        }

        int maxAttempts = Math.Max(1, _options.MaxRetries);
        int timeoutMs   = _options.TimeoutSeconds * 1000;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "[DocumentIntelligence] Tentativa {Attempt}/{Max} — imagem={Size}KB",
                    attempt, maxAttempts, imageBytes.Length / 1024);

                var client = new DocumentAnalysisClient(
                    new Uri(_options.Endpoint),
                    new AzureKeyCredential(_options.ApiKey));

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(timeoutMs);

                using var stream = new MemoryStream(imageBytes);
                var operation = await client.AnalyzeDocumentAsync(
                    WaitUntil.Completed,
                    "prebuilt-layout",
                    stream,
                    cancellationToken: cts.Token);

                var result = operation.Value;

                _logger.LogInformation(
                    "[DocumentIntelligence] Análise concluída — tabelas={Tables}, páginas={Pages}",
                    result.Tables.Count, result.Pages.Count);

                var orchestrator = new NutritionParserOrchestrator(_logger);
                return orchestrator.ParseBest(result);
            }
            catch (OperationCanceledException) when (attempt < maxAttempts)
            {
                _logger.LogWarning("[DocumentIntelligence] Timeout na tentativa {Attempt}, retentando...", attempt);
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(ex, "[DocumentIntelligence] Erro na tentativa {Attempt}, retentando...", attempt);
                await Task.Delay(500 * attempt, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DocumentIntelligence] Falha após {Max} tentativas.", maxAttempts);
                return null;
            }
        }

        return null;
    }

    public async Task<string?> ExtractTextAsync(
        byte[] imageBytes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("[DocumentIntelligence] Endpoint ou ApiKey não configurados — extração textual desabilitada.");
            return null;
        }

        try
        {
            var client = new DocumentAnalysisClient(
                new Uri(_options.Endpoint),
                new AzureKeyCredential(_options.ApiKey));

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.TimeoutSeconds * 1000);

            using var stream = new MemoryStream(imageBytes);
            var operation = await client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                "prebuilt-layout",
                stream,
                cancellationToken: cts.Token);

            var content = operation.Value.Content;
            return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DocumentIntelligence] Falha ao extrair texto bruto.");
            return null;
        }
    }
}
