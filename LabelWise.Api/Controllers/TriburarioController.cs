using LabelWise.Application.Interfaces;
using LabelWise.Application.Models;
using LabelWise.Domain.Models.Tributario;
using Microsoft.AspNetCore.Mvc;

namespace LabelWise.Api.Controllers;

[ApiController]
[Route("api/tributario")]
public sealed class TributarioController : ControllerBase
{
    private readonly IOpenAIDiagnosticoTributarioService _service;
    private readonly ILogger<TributarioController> _logger;

    public TributarioController(
        IOpenAIDiagnosticoTributarioService service,
        ILogger<TributarioController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Realiza um diagnóstico tributário utilizando IA.
    /// </summary>
    [HttpPost("analisar")]
    [ProducesResponseType(typeof(DiagnosticoTributarioResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Analisar(
        [FromBody] EmpresaDiagnosticoRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest("Requisição inválida.");

        if (string.IsNullOrWhiteSpace(request.RazaoSocial))
            return BadRequest("Razão Social é obrigatória.");

        if (string.IsNullOrWhiteSpace(request.RegimeTributario))
            return BadRequest("Regime Tributário é obrigatório.");

        try
        {
            var resultado = await _service.AnalyzeAsync(request, cancellationToken);

            if (resultado == null)
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "Não foi possível gerar o diagnóstico.");

            return Ok(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar diagnóstico tributário.");

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "Erro interno.");
        }
    }
}