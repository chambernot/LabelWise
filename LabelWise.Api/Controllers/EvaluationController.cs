using LabelWise.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace LabelWise.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
// [Authorize] -> Descomente quando integrar o JWT
public class EvaluationController : ControllerBase
{
    private readonly ConditionEvaluationService _evaluationService;
    private readonly PreGradingService _preGradingService;

    public EvaluationController(ConditionEvaluationService evaluationService, PreGradingService preGradingService)
    {
        _evaluationService = evaluationService;
        _preGradingService = preGradingService;
    }

    [HttpPost("{id:guid}/pre-grade")]
    public async Task<IActionResult> PreGradeCard(
        Guid id,
        [FromForm] decimal currentRawValue,
        [FromForm] IFormFile frontImage,
        [FromForm] IFormFile backImage)
    {
        if (frontImage == null || backImage == null)
            return BadRequest("As fotos da frente e do verso em alta qualidade são obrigatórias para a pré-gradação.");

        try
        {
            // O serviço agora deve ser injetado via construtor no Controller!
            // Para o exemplo de onde injetar: 
            // var preGradingService = HttpContext.RequestServices.GetRequiredService<PreGradingService>();

            using var frontStream = frontImage.OpenReadStream();
            using var backStream = backImage.OpenReadStream();

            var result = await _preGradingService.SimulateGradingAsync(id, currentRawValue, frontStream, backStream);

            return Ok(new
            {
                Message = "Simulação de Gradação concluída com inteligência artificial.",
                Data = result
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpPost("condition")]
    public async Task<IActionResult> EvaluateCondition(
        [FromForm] string cardName,
        [FromForm] IFormFile frontImage,
        [FromForm] IFormFile backImage)
    {
        if (frontImage == null || backImage == null)
            return BadRequest("As fotos da frente e do verso são obrigatórias.");

        try
        {
            // Simula pegar o ID do usuário logado via Token JWT
            var userId = Guid.NewGuid();

            using var frontStream = frontImage.OpenReadStream();
            using var backStream = backImage.OpenReadStream();

            var result = await _evaluationService.EvaluateConditionAsync(userId, cardName, frontStream, backStream);

            return Ok(new
            {
                Message = "Avaliação concluída com sucesso.",
                Data = result
            });
        }
        catch (Exception ex)
        {
            // Em produção, use um middleware de tratamento global de exceções
            return BadRequest(new { Error = ex.Message });
        }
    }
}