using LabelWise.Application.DTOs.Access;
using LabelWise.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LabelWise.Api.Controllers
{
    [ApiController]
    [Route("api/subscription")]
    [AllowAnonymous]
    public class SubscriptionController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<SubscriptionController> _logger;

        public SubscriptionController(
            ISubscriptionService subscriptionService,
            ILogger<SubscriptionController> logger)
        {
            _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("activate")]
        [ProducesResponseType(typeof(AppAccessStateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Activate([FromBody] SubscriptionActivationRequest request)
        {
            try
            {
                var response = await _subscriptionService.ActivateAsync(request);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Ativação de assinatura inválida");
                return BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Ativação de assinatura sem sessão inicializada");
                return NotFound(new { error = ex.Message });
            }
        }

        [HttpPost("restore")]
        [ProducesResponseType(typeof(AppAccessStateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Restore([FromBody] SubscriptionRestoreRequest request)
        {
            try
            {
                var response = await _subscriptionService.RestoreAsync(request);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Restore de assinatura inválido");
                return BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Restore de assinatura sem sessão inicializada");
                return NotFound(new { error = ex.Message });
            }
        }
    }
}
