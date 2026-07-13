using System;
using System.Threading.Tasks;
using LabelWise.Application.DTOs.AI;
using LabelWise.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LabelWise.Api.Examples
{
    /// <summary>
    /// Example controller demonstrating the use of the IVisualInterpreter.
    /// </summary>
    [ApiController]
    [Route("api/examples/[controller]")]
    public class VisualInterpreterExampleController : ControllerBase
    {
        private readonly IVisualInterpreter _visualInterpreter;
        private readonly ILogger<VisualInterpreterExampleController> _logger;

        public VisualInterpreterExampleController(
            IVisualInterpreter visualInterpreter,
            ILogger<VisualInterpreterExampleController> logger)
        {
            _visualInterpreter = visualInterpreter;
            _logger = logger;
        }

        /// <summary>
        /// Analyzes an image from a local path and returns the visual interpretation.
        /// </summary>
        /// <param name="imagePath">The full local path to the image file.</param>
        /// <returns>The visual interpretation result.</returns>
        [HttpPost("interpret")]
        public async Task<ActionResult<VisualInterpretationResult>> InterpretImage([FromQuery] string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return BadRequest("Image path cannot be empty.");
            }

            // In a real application, you would get the image from an upload or a storage service.
            // This example uses a local path for simplicity.
            var request = new VisualInterpretationRequest { ImagePath = imagePath };

            try
            {
                var result = await _visualInterpreter.InterpretImageAsync(request);
                _logger.LogInformation("Visual interpretation completed successfully for path: {ImagePath}", imagePath);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while interpreting the image at path: {ImagePath}", imagePath);
                return StatusCode(500, "An internal server error occurred.");
            }
        }
    }
}
