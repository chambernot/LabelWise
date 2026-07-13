using LabelWise.Api.Models;
using LabelWise.Application.DTOs.Access;
using LabelWise.Application.DTOs.NutritionConversation;
using LabelWise.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LabelWise.Api.Controllers;

[ApiController]
[Route("food/conversation")]
[Authorize]
public sealed class ConversationController : ControllerBase
{
    private readonly IAppAccessService _appAccessService;
    private readonly INutritionConversationService _conversationService;
    private readonly ILogger<ConversationController> _logger;

    public ConversationController(
        IAppAccessService appAccessService,
        INutritionConversationService conversationService,
        ILogger<ConversationController> logger)
    {
        _appAccessService = appAccessService ?? throw new ArgumentNullException(nameof(appAccessService));
        _conversationService = conversationService ?? throw new ArgumentNullException(nameof(conversationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("start")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ConversationStartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Start(
        [FromForm] NutritionAnalysisFormModel model,
        CancellationToken cancellationToken = default)
    {
        var deviceId = ResolveDeviceId(model.DeviceId);

        _logger.LogInformation(
            "POST food/conversation/start — File={File}, Size={Size}B, Device={Device}",
            model.File?.FileName ?? "N/A",
            model.File?.Length ?? 0,
            deviceId ?? "anon");

        try
        {
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                var accessState = await _appAccessService.GetAccessStateAsync(deviceId);
                if (!accessState.CanUseAnalysis)
                {
                    _logger.LogWarning("Acesso negado para conversa nutricional. DeviceId={DeviceId}", deviceId);
                    return StatusCode(StatusCodes.Status403Forbidden, CreateAccessDeniedResponse(accessState));
                }
            }

            var fileError = ValidateFile(model.File);
            if (fileError != null)
                return BadRequest(fileError);

            byte[] imageBytes;
            await using (var ms = new MemoryStream())
            {
                await model.File!.CopyToAsync(ms, cancellationToken);
                imageBytes = ms.ToArray();
            }

            var response = await _conversationService.StartAsync(
                imageBytes,
                model.File!.ContentType,
                deviceId,
                cancellationToken);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar conversa nutricional");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                error = "Erro interno ao iniciar conversa nutricional"
            });
        }
    }

    [HttpPost("message")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ConversationMessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Message(
        [FromBody] ConversationMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return BadRequest(new { success = false, error = "Requisição inválida" });

        if (string.IsNullOrWhiteSpace(request.ConversationId))
            return BadRequest(new { success = false, error = "conversationId é obrigatório" });

        if (string.IsNullOrWhiteSpace(request.AnalysisId))
            return BadRequest(new { success = false, error = "analysisId é obrigatório" });

        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { success = false, error = "message é obrigatório" });

        try
        {
            var response = await _conversationService.SendMessageAsync(request, cancellationToken);
            if (response is null)
                return NotFound(new { success = false, error = "Conversa não encontrada" });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Erro ao responder conversa nutricional. ConversationId={ConversationId}, AnalysisId={AnalysisId}",
                request.ConversationId,
                request.AnalysisId);

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                error = "Erro interno ao responder conversa nutricional"
            });
        }
    }

    private static object? ValidateFile(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return new { success = false, error = "Arquivo de imagem é obrigatório" };

        if (file.Length < 20_000)
            return new { success = false, error = "Imagem com baixa qualidade para OCR. Envie a imagem original, sem compressão agressiva." };

        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext))
            return new { success = false, error = $"Tipo de arquivo não suportado. Use: {string.Join(", ", allowed)}" };

        const long maxSize = 10 * 1024 * 1024;
        if (file.Length > maxSize)
            return new { success = false, error = "Arquivo muito grande. Tamanho máximo: 10MB" };

        return null;
    }

    private string? ResolveDeviceId(string? formDeviceId)
    {
        if (!string.IsNullOrWhiteSpace(formDeviceId)) return formDeviceId.Trim();

        if (Request.Headers.TryGetValue("X-Device-Id", out var headerValue))
        {
            var id = headerValue.FirstOrDefault();
            return string.IsNullOrWhiteSpace(id) ? null : id.Trim();
        }

        return null;
    }

    private static AccessDeniedResponse CreateAccessDeniedResponse(AppAccessStateResponse accessState) =>
        new()
        {
            Success = false,
            AccessDenied = true,
            Reason = accessState.IsPremium || accessState.IsTrialActive ? "access_denied" : "trial_expired",
            Message = accessState.Message,
            AccessState = accessState
        };
}
