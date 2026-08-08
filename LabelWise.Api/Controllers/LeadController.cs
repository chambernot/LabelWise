using LabelWise.Domain.Entities;
using LabelWise.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace LabelWise.Api.Controllers
{
    [ApiController]
    [Route("api/leads")]
    public class LeadController : ControllerBase
    {
        private readonly LeadService _service;

        public LeadController(LeadService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Post(LeadRequest request)
        {
            await _service.SalvarAsync(request);

            return Ok(new
            {
                sucesso = true,
                mensagem = "Lead salvo com sucesso."
            });
        }
    }
}
