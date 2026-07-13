using System.Security.Claims;
using LabelWise.Application.DTOs.Access;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LabelWise.Api.Controllers
{
    [ApiController]
    [Route("api/nutrition")]
    [Authorize]
    public class NutritionComparisonController : ControllerBase
    {
        private readonly IAppAccessService _appAccessService;
        private readonly IProductComparisonService _productComparisonService;
        private readonly ILogger<NutritionComparisonController> _logger;

        public NutritionComparisonController(
            IAppAccessService appAccessService,
            IProductComparisonService productComparisonService,
            ILogger<NutritionComparisonController> logger)
        {
            _appAccessService = appAccessService ?? throw new ArgumentNullException(nameof(appAccessService));
            _productComparisonService = productComparisonService ?? throw new ArgumentNullException(nameof(productComparisonService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("compare")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ProductComparisonResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Compare([FromBody] CompareProductsRequest request)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Request de comparação é obrigatório"
                });
            }

            try
            {
                var deviceId = ResolveDeviceId(request.DeviceId);

                if (!string.IsNullOrWhiteSpace(deviceId))
                {
                    var accessState = await _appAccessService.GetAccessStateAsync(deviceId);
                    if (!accessState.CanUseComparison)
                    {
                        _logger.LogWarning("Comparação bloqueada por acesso expirado. DeviceId={DeviceId}", deviceId);
                        return StatusCode(StatusCodes.Status403Forbidden, CreateAccessDeniedResponse(accessState));
                    }
                }

                ProductComparisonResponse response;

                if (HasAnalysisIds(request))
                {
                    var userId = TryGetUserIdFromClaims();

                    _logger.LogInformation(
                        "POST /api/nutrition/compare - AnalysisIdA: {AnalysisIdA}, AnalysisIdB: {AnalysisIdB}, Authenticated: {Authenticated}",
                        request.AnalysisIdA,
                        request.AnalysisIdB,
                        userId.HasValue);

                    response = await _productComparisonService.CompareAsync(
                        request.AnalysisIdA!,
                        request.AnalysisIdB!,
                        userId,
                        deviceId);
                }
                else if (HasAnalysisPayload(request))
                {
                    _logger.LogInformation("POST /api/nutrition/compare - comparação por payload completo");

                    response = await _productComparisonService.CompareAsync(
                        request.ProductAAnalysis!,
                        request.ProductBAnalysis!);
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "Informe analysisIdA e analysisIdB ou envie productAAnalysis e productBAnalysis"
                    });
                }

                _logger.LogInformation("Comparação liberada com sucesso. DeviceId={DeviceId}", deviceId ?? "none");
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Request inválido para comparação de produtos");
                return BadRequest(new
                {
                    success = false,
                    error = ex.Message
                });
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Análise não encontrada para comparação");
                return NotFound(new
                {
                    success = false,
                    error = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Comparação impossível");
                return BadRequest(new
                {
                    success = false,
                    error = ex.Message
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Comparação negada por owner inválido");
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    success = false,
                    accessDenied = true,
                    reason = "ownership_mismatch",
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao comparar produtos analisados");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    error = "Erro interno ao comparar produtos",
                    details = ex.Message
                });
            }
        }

        private static bool HasAnalysisIds(CompareProductsRequest request)
        {
            return !string.IsNullOrWhiteSpace(request.AnalysisIdA)
                && !string.IsNullOrWhiteSpace(request.AnalysisIdB);
        }

        private static bool HasAnalysisPayload(CompareProductsRequest request)
        {
            return request.ProductAAnalysis != null && request.ProductBAnalysis != null;
        }

        private Guid? TryGetUserIdFromClaims()
        {
            var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            return Guid.TryParse(sub, out var userId) ? userId : null;
        }

        private AccessDeniedResponse CreateAccessDeniedResponse(AppAccessStateResponse accessState)
        {
            return new AccessDeniedResponse
            {
                Success = false,
                AccessDenied = true,
                Reason = accessState.IsPremium || accessState.IsTrialActive ? "access_denied" : "trial_expired",
                Message = accessState.Message,
                AccessState = accessState
            };
        }

        private string? ResolveDeviceId(string? bodyDeviceId)
        {
            if (!string.IsNullOrWhiteSpace(bodyDeviceId))
            {
                return bodyDeviceId.Trim();
            }

            if (Request.Headers.TryGetValue("X-Device-Id", out var headerValue))
            {
                var deviceId = headerValue.FirstOrDefault();
                return string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
            }

            return null;
        }
    }
}
