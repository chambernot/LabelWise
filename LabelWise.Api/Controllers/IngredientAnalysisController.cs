using LabelWise.Api.Models;
using LabelWise.Application.DTOs.Access;
using LabelWise.Application.DTOs.IngredientAnalysis;
using LabelWise.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LabelWise.Api.Controllers;

[ApiController]
[Route("food")]
[Authorize]
public sealed class IngredientAnalysisController : ControllerBase
{
    private readonly IAppAccessService _appAccessService;
    private readonly IIngredientAnalysisService _ingredientAnalysisService;
    private readonly ILogger<IngredientAnalysisController> _logger;

    public IngredientAnalysisController(
        IAppAccessService appAccessService,
        IIngredientAnalysisService ingredientAnalysisService,
        ILogger<IngredientAnalysisController> logger)
    {
        _appAccessService = appAccessService ?? throw new ArgumentNullException(nameof(appAccessService));
        _ingredientAnalysisService = ingredientAnalysisService ?? throw new ArgumentNullException(nameof(ingredientAnalysisService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("ingredient-analysis")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(IngredientAnalysisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Analyze(
        [FromForm] NutritionAnalysisFormModel model,
        CancellationToken cancellationToken = default)
    {
        var deviceId = ResolveDeviceId(model.DeviceId);

        _logger.LogInformation(
            "POST food/ingredient-analysis — File={File}, Size={Size}B, Device={Device}",
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
                    _logger.LogWarning("Acesso negado para análise de ingredientes. DeviceId={DeviceId}", deviceId);
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

            var response = await _ingredientAnalysisService.AnalyzeAsync(
                imageBytes,
                model.File!.ContentType,
                cancellationToken);

            // Log ingredient detection summary for debugging
            _logger.LogInformation(
                "[IngredientAnalysis.Result] Ingredients={Count}, Claims={ClaimCount}, Blocks={BlockCount}",
                response.IngredientsDetected.Count,
                response.ClaimsDetected.Count,
                response.StructuredTextBlocks.Count);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar análise de ingredientes");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                error = "Erro interno ao processar análise de ingredientes"
            });
        }
    }

    [HttpPost("ingredient-analysis-debug")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AnalyzeDebug(
        [FromForm] NutritionAnalysisFormModel model,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fileError = ValidateFile(model.File);
            if (fileError != null)
                return BadRequest(fileError);

            byte[] imageBytes;
            await using (var ms = new MemoryStream())
            {
                await model.File!.CopyToAsync(ms, cancellationToken);
                imageBytes = ms.ToArray();
            }

            var response = await _ingredientAnalysisService.AnalyzeAsync(
                imageBytes,
                model.File!.ContentType,
                cancellationToken);

            return Ok(new
            {
                success = true,
                debug = new
                {
                    ocrText = response.CleanedSemanticText,
                    structuredBlocks = response.StructuredTextBlocks.Select(block => new
                    {
                        type = block.Type,
                        regionType = block.RegionType.ToString(),
                        text = block.Text,
                        confidence = block.Confidence
                    }),
                    rawIngredients = response.IngredientsDetected,
                    normalizedIngredients = response.NormalizedIngredients.Select(item => new
                    {
                        raw = item.Raw,
                        normalized = item.Normalized,
                        category = item.Category,
                        confidence = item.Confidence
                    }),
                    claims = response.Claims,
                    allergenRisks = response.AllergenRisks.Select(risk => new
                    {
                        name = risk.Name,
                        riskType = risk.RiskType,
                        evidence = risk.Evidence,
                        originBlock = risk.OriginBlock
                    }),
                    diagnostics = response.Diagnostics
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no debug de análise");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    private static object? ValidateFile(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return new { success = false, error = "Arquivo de imagem é obrigatório" };

        if (file.Length < 10_000)
            return new { success = false, error = "Imagem muito pequena para leitura de ingredientes. Envie uma foto mais nítida da embalagem." };

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
