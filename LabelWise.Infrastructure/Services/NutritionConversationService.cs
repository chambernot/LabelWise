using LabelWise.Application.DTOs.NutritionConversation;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.NutritionConversation;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services;

public sealed class NutritionConversationService : INutritionConversationService
{
    private readonly INutritionAnalysisOrchestrator _orchestrator;
    private readonly IIngredientAnalysisService _ingredientAnalysisService;
    private readonly IConversationRepository _repository;
    private readonly IOpenAIConversationService _openAIConversationService;
    private readonly ILogger<NutritionConversationService> _logger;

    public NutritionConversationService(
        INutritionAnalysisOrchestrator orchestrator,
        IIngredientAnalysisService ingredientAnalysisService,
        IConversationRepository repository,
        IOpenAIConversationService openAIConversationService,
        ILogger<NutritionConversationService> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _ingredientAnalysisService = ingredientAnalysisService ?? throw new ArgumentNullException(nameof(ingredientAnalysisService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _openAIConversationService = openAIConversationService ?? throw new ArgumentNullException(nameof(openAIConversationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ConversationStartResponse> StartAsync(
        byte[] imageBytes,
        string? contentType,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var nutritionTask = _orchestrator.AnalyzeAsync(imageBytes, contentType, cancellationToken);
        var ingredientTask = AnalyzeIngredientsSafelyAsync(imageBytes, contentType, cancellationToken);

        await Task.WhenAll(nutritionTask, ingredientTask);

        var analysis = await nutritionTask;
        var ingredientAnalysis = await ingredientTask;
        var conversationId = Guid.NewGuid().ToString("N");
        var analysisId = analysis.AnalysisId?.ToString("N") ?? Guid.NewGuid().ToString("N");

        if (analysis.AnalysisId is null && Guid.TryParseExact(analysisId, "N", out var parsedAnalysisId))
            analysis.AnalysisId = parsedAnalysisId;

        var context = NutritionConversationContext.FromAnalysis(analysis, ingredientAnalysis);
        var initialMessage = await _openAIConversationService.GenerateInitialMessageAsync(context, cancellationToken);
        if (string.IsNullOrWhiteSpace(initialMessage))
            initialMessage = BuildFallbackInitialMessage(context);

        var session = new NutritionConversationSession
        {
            Id = conversationId,
            ConversationId = conversationId,
            AnalysisId = analysisId,
            DeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim(),
            Context = context,
            Messages =
            [
                new ConversationMessage
                {
                    Role = "assistant",
                    Content = initialMessage,
                    CreatedAt = DateTime.UtcNow
                }
            ],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.SaveAsync(session, cancellationToken);

        _logger.LogInformation(
            "Conversa nutricional criada. ConversationId={ConversationId}, AnalysisId={AnalysisId}, DeviceId={DeviceId}, IngredientWarnings={IngredientWarnings}, Claims={Claims}",
            conversationId,
            analysisId,
            session.DeviceId ?? "anon",
            ingredientAnalysis is null ? "none" : string.Join(" | ", ingredientAnalysis.AllergenRisks.Select(risk => $"{risk.RiskType}:{risk.Name}")),
            ingredientAnalysis is null ? "none" : string.Join(" | ", ingredientAnalysis.Claims));

        return new ConversationStartResponse
        {
            ConversationId = conversationId,
            AnalysisId = analysisId,
            NutritionSummary = context.NutritionSummary,
            Scores = context.Score,
            Profiles = context.Profiles,
            InitialAssistantMessage = initialMessage
        };
    }

    public async Task<ConversationMessageResponse?> SendMessageAsync(
        ConversationMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = await _repository.GetAsync(request.ConversationId, request.AnalysisId, cancellationToken);
        if (session is null)
            return null;

        var userMessage = new ConversationMessage
        {
            Role = "user",
            Content = request.Message.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var history = session.Messages
            .Concat([userMessage])
            .OrderBy(x => x.CreatedAt)
            .ToList();

        var assistantContent = await _openAIConversationService.GenerateReplyAsync(
            session.Context,
            history,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(assistantContent))
            assistantContent = "Não consegui gerar uma resposta agora. Posso ajudar com outra pergunta sobre os dados nutricionais já identificados.";

        var assistantMessage = new ConversationMessage
        {
            Role = "assistant",
            Content = assistantContent,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AppendMessagesAsync(
            request.ConversationId,
            request.AnalysisId,
            [userMessage, assistantMessage],
            cancellationToken);

        return new ConversationMessageResponse
        {
            ConversationId = request.ConversationId,
            AnalysisId = request.AnalysisId,
            AssistantMessage = assistantContent
        };
    }

    private static string BuildFallbackInitialMessage(NutritionConversationContext context)
    {
        var summary = context.NutritionSummary;
        var weaknesses = summary.Weaknesses.Count > 0
            ? string.Join(" e ", summary.Weaknesses.Take(2))
            : "os dados nutricionais identificados";
        var strengths = summary.Strengths.Count > 0
            ? $" Também observei {string.Join(" e ", summary.Strengths.Take(2))}."
            : string.Empty;

        return $"Analisei o produto enviado. O principal ponto de atenção é {weaknesses}.{strengths} Você pode me perguntar se ele é indicado para emagrecimento, diabetes, hipertrofia ou alimentação infantil.";
    }

    private async Task<Application.DTOs.IngredientAnalysis.IngredientAnalysisResponse?> AnalyzeIngredientsSafelyAsync(
        byte[] imageBytes,
        string? contentType,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _ingredientAnalysisService.AnalyzeAsync(imageBytes, contentType, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao enriquecer conversa com análise de ingredientes. A conversa seguirá com contexto nutricional.");
            return null;
        }
    }
}
