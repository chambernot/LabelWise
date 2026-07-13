using LabelWise.Application.DTOs.Access;
using LabelWise.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LabelWise.Api.Controllers
{
    [ApiController]
    [Route("api/app-user")]
    [AllowAnonymous]
    public class AppUserController : ControllerBase
    {
        private readonly IAppAccessService _appAccessService;
        private readonly ILogger<AppUserController> _logger;

        public AppUserController(
            IAppAccessService appAccessService,
            ILogger<AppUserController> logger)
        {
            _appAccessService = appAccessService ?? throw new ArgumentNullException(nameof(appAccessService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("session")]
        [ProducesResponseType(typeof(AppSessionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> InitializeSession([FromBody] AppSessionRequest request)
        {
            try
            {
                var response = await _appAccessService.InitializeSessionAsync(request.DeviceId, request.Platform);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Sessão inválida para app-user/session");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("access-state")]
        [ProducesResponseType(typeof(AppAccessStateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAccessState([FromQuery] string deviceId)
        {
            try
            {
                var response = await _appAccessService.GetAccessStateAsync(deviceId);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Consulta inválida de access-state");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("access-state")]
        [ProducesResponseType(typeof(AppAccessStateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> PostAccessState([FromBody] AppAccessStateRequest request)
        {
            try
            {
                var response = await _appAccessService.GetAccessStateAsync(request.DeviceId);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Consulta inválida de access-state via POST");
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
