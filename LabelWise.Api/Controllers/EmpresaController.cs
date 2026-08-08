using LabelWise.Domain.Entities;
using LabelWise.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace LabelWise.Api.Controllers
{
    [ApiController]
    [Route("api/empresas")]
    public class EmpresaController : ControllerBase
    {
        private readonly EmpresaService _service;

        public EmpresaController(EmpresaService service)
        {
            _service = service;
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Atualizar(string id, [FromBody] Empresa empresaPayload)
        {
            if (string.IsNullOrEmpty(id) || empresaPayload == null)
            {
                return BadRequest("Dados inválidos para atualização.");
            }

            var atualizado = await _service.AtualizarAsync(id, empresaPayload);

            if (!atualizado)
            {
                return NotFound(new { mensagem = "Empresa não encontrada para atualização." });
            }

            return Ok(new { sucesso = true, mensagem = "Empresa atualizada com sucesso!" });
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] EmpresaRequest request)
        {
            await _service.SalvarAsync(request);

            return Ok(new
            {
                sucesso = true,
                mensagem = "Empresa salva com sucesso no MongoDB."
            });
        }

        [HttpGet("{guestId}")]
        public async Task<IActionResult> GetByGuestId(string guestId)
        {
            var empresas = await _service.ObterPorGuestIdAsync(guestId);
            return Ok(empresas);
        }
    }
}