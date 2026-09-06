using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Interfaces.Persistence;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LabelWise.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProfessionalController : ControllerBase
    {
        private readonly INutritionRepository _repository;
        private readonly INutritionService _nutritionService;

        public ProfessionalController(INutritionRepository repository, INutritionService nutritionService)
        {
            _repository = repository;
            _nutritionService = nutritionService;
        }

        [HttpPost("patients")]
        public async Task<IActionResult> RegisterPatient([FromBody] PatientDto request)
        {
            // O Id do paciente será o próprio número do WhatsApp para facilitar o vínculo do Webhook
            var paciente = request with { Id = request.WhatsAppNumber };
            await _repository.InserirPacienteAsync(paciente);

            return Ok(new { success = true, message = "Paciente vinculado com sucesso." });
        }

        [HttpGet("{professionalId}/patients-status")]
        public async Task<IActionResult> GetDashboardCards(string professionalId, [FromQuery] DateTime? date)
        {
            var targetDate = date ?? DateTime.UtcNow;
            var patients = await _repository.ObterPacientesPorProfissionalAsync(professionalId);

            var dashboardList = new List<object>();

            // Para cada paciente, calcula como está o dia dele para o nutricionista visualizar
            foreach (var p in patients)
            {
                var status = await _nutritionService.GetDailyStatusAndSuggestionAsync(p.Id, targetDate);

                dashboardList.Add(new
                {
                    patientName = p.Name,
                    whatsapp = p.WhatsAppNumber,
                    targetCalories = status.Target.Calories,
                    consumedCalories = status.Consumed.Calories,
                    remainingCalories = status.Remaining.Calories,
                    isOvertarget = status.Consumed.Calories > status.Target.Calories
                });
            }

            return Ok(new { success = true, data = dashboardList });
        }
    }
}