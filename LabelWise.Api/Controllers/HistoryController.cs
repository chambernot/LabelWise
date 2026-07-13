using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LabelWise.Application.DTOs.Access;
using LabelWise.Application.Interfaces;

namespace LabelWise.Api.Controllers
{
    [ApiController]
    [Route("api/history")]
    [Route("api/analysis/history")]
    [Authorize]
    public class HistoryController : ControllerBase
    {
        private readonly IAppAccessService _appAccessService;
        private readonly IAnalysisHistoryService _historyService;
        private readonly ILogger<HistoryController> _logger;

        public HistoryController(
            IAppAccessService appAccessService,
            IAnalysisHistoryService historyService,
            ILogger<HistoryController> logger)
        {
            _appAccessService = appAccessService ?? throw new ArgumentNullException(nameof(appAccessService));
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Lista todas as análises realizadas pelo usuário autenticado
        /// </summary>
        /// <returns>Lista de análises ordenadas por data decrescente</returns>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetHistory([FromQuery] string? deviceId = null)
        {
            try
            {
                var resolvedDeviceId = ResolveDeviceId(deviceId);
                if (!string.IsNullOrWhiteSpace(resolvedDeviceId))
                {
                    var accessState = await _appAccessService.GetAccessStateAsync(resolvedDeviceId);
                    if (!accessState.CanUseHistory)
                    {
                        _logger.LogWarning("Histórico bloqueado por trial expirado. DeviceId={DeviceId}", resolvedDeviceId);
                        return StatusCode(StatusCodes.Status403Forbidden, CreateAccessDeniedResponse(accessState));
                    }

                    var deviceHistory = await _historyService.GetDeviceAnalysisHistoryAsync(resolvedDeviceId);
                    return Ok(deviceHistory);
                }

                var userId = GetUserIdFromClaims();
                var history = await _historyService.GetUserAnalysisHistoryAsync(userId);
                return Ok(history);
            }
            catch (InvalidOperationException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erro ao buscar histórico de análises.", details = ex.Message });
            }
        }

        /// <summary>
        /// Obtém os detalhes de uma análise específica
        /// </summary>
        /// <param name="id">ID da análise</param>
        /// <returns>Detalhes completos da análise</returns>
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAnalysisDetail(Guid id, [FromQuery] string? deviceId = null)
        {
            try
            {
                var resolvedDeviceId = ResolveDeviceId(deviceId);
                if (!string.IsNullOrWhiteSpace(resolvedDeviceId))
                {
                    var accessState = await _appAccessService.GetAccessStateAsync(resolvedDeviceId);
                    if (!accessState.CanUseHistory)
                    {
                        _logger.LogWarning("Detalhe de histórico bloqueado por trial expirado. DeviceId={DeviceId}, AnalysisId={AnalysisId}", resolvedDeviceId, id);
                        return StatusCode(StatusCodes.Status403Forbidden, CreateAccessDeniedResponse(accessState));
                    }

                    var deviceDetail = await _historyService.GetAnalysisDetailByDeviceAsync(id, resolvedDeviceId);
                    if (deviceDetail == null)
                    {
                        return NotFound(new { error = "Análise não encontrada ou não pertence ao deviceId informado." });
                    }

                    return Ok(deviceDetail);
                }

                var userId = GetUserIdFromClaims();
                var detail = await _historyService.GetAnalysisDetailAsync(id, userId);

                if (detail == null)
                    return NotFound(new { error = "Análise não encontrada ou não pertence ao usuário." });

                return Ok(detail);
            }
            catch (InvalidOperationException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erro ao buscar detalhes da análise.", details = ex.Message });
            }
        }

        private Guid GetUserIdFromClaims()
        {
            var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                      ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            
            if (string.IsNullOrWhiteSpace(sub))
                throw new InvalidOperationException("Não foi possível determinar o ID do usuário a partir do token.");
            
            return Guid.Parse(sub);
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

        private string? ResolveDeviceId(string? queryDeviceId)
        {
            if (!string.IsNullOrWhiteSpace(queryDeviceId))
            {
                return queryDeviceId.Trim();
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
