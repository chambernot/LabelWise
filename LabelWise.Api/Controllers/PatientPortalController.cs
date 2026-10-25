using LabelWise.Domain.Entities.Nutrition;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LabelWise.Api.Controllers
{
    [ApiController]
    [Route("api/patient")]
    [AllowAnonymous]
    public class PatientPortalController : ControllerBase
    {
        private const string MensagemLinkInvalido = "Este link expirou ou não é mais válido. Envie *minha dieta* no WhatsApp para receber um novo link seguro.";

        private readonly IMongoDatabase _database;

        public PatientPortalController(IMongoDatabase database)
        {
            _database = database;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetPatientDashboardByToken([FromQuery] string token)
        {
            var phone = await PatientAccessTokens.ObterTelefoneAsync(_database, token);
            if (phone == null)
            {
                return Unauthorized(new { success = false, message = MensagemLinkInvalido });
            }

            return await MontarDashboardAsync(phone);
        }

        // Acesso antigo pelo telefone na URL: desativado, pois expunha os dados de qualquer número
        [HttpGet("dashboard/{phone}")]
        public IActionResult GetPatientDashboard(string phone)
        {
            return StatusCode(StatusCodes.Status410Gone, new { success = false, message = MensagemLinkInvalido });
        }

        private async Task<IActionResult> MontarDashboardAsync(string phone)
        {
            // 1. Busca a meta/dieta do paciente
            var goalsCollection = _database.GetCollection<DailyNutritionGoal>("DailyGoals");
            var goal = await goalsCollection
                .Find(x => x.UserId == phone)
                .SortByDescending(x => x.TargetDate)
                .FirstOrDefaultAsync();

            if (goal == null)
            {
                return NotFound(new { success = false, message = "Nenhuma dieta encontrada para este número." });
            }

            // 2. Busca as refeições registradas hoje (00:00 de Brasília = 03:00 UTC)
            var logsCollection = _database.GetCollection<MealLog>("Nutrition_MealLogs");
            var todayStart = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-3).Date.AddHours(3), DateTimeKind.Utc);

            var logs = await logsCollection
                .Find(x => x.UserId == phone && x.LoggedAt >= todayStart)
                .ToListAsync();

            var consumedCalories = logs.Sum(x => x.Calories);
            var consumedProtein = logs.Sum(x => x.ProteinG);
            var consumedCarbs = logs.Sum(x => x.CarbsG);
            var consumedFat = logs.Sum(x => x.FatG);

            return Ok(new
            {
                success = true,
                data = new
                {
                    phone = goal.UserId,
                    targetCalories = goal.TargetCalories,
                    targetProtein = goal.TargetProteinG,
                    targetCarbs = goal.TargetCarbsG,
                    targetFat = goal.TargetFatG,
                    dietaryRestrictions = goal.DietaryRestrictions,
                    prescribedMealPlan = goal.PrescribedMealPlan,
                    consumed = new
                    {
                        calories = consumedCalories,
                        protein = consumedProtein,
                        carbs = consumedCarbs,
                        fat = consumedFat
                    },
                    recentLogs = logs.OrderByDescending(x => x.LoggedAt).Take(10).ToList()
                }
            });
        }
    }
}
