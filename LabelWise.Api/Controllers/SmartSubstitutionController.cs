using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces.AI;
using LabelWise.Application.Interfaces.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LabelWise.Api.Controllers
{
    [ApiController]
    [Route("api/smart-substitution")]
    [AllowAnonymous]
    public class SmartSubstitutionController : ControllerBase
    {
        private readonly IMongoDatabase _database;
        private readonly INutritionAgentService _agentService;
        private readonly INutritionRepository _nutritionRepository;

        public SmartSubstitutionController(
            IMongoDatabase database,
            INutritionAgentService agentService,
            INutritionRepository nutritionRepository)
        {
            _database = database;
            _agentService = agentService;
            _nutritionRepository = nutritionRepository;
        }

        [HttpPost("request")]
        public async Task<IActionResult> RequestSubstitution([FromBody] SubstitutionRequestDto request)
        {
            try
            {
                var patientsCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("Nutrition_Patients");
                var patientDoc = await patientsCol.Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", request.UserId)).FirstOrDefaultAsync();

                string clinicalProtocol = patientDoc != null && patientDoc.Contains("ClinicalProtocol") ? patientDoc["ClinicalProtocol"].AsString : "";
                string foodAversions = patientDoc != null && patientDoc.Contains("FoodAversions") ? patientDoc["FoodAversions"].AsString : "";

                var dataHojeBr = DateTime.UtcNow.AddHours(-3);
                var logs = await _nutritionRepository.ObterRefeicoesDoDiaAsync(request.UserId, dataHojeBr);
                var goal = await _nutritionRepository.ObterMetaDiariaAsync(request.UserId, dataHojeBr.Date);

                int targetCal = goal?.TargetCalories ?? 2000;
                int consumedCal = logs.Sum(x => x.Calories);
                decimal targetProt = goal?.TargetProteinG ?? 150m;
                decimal consumedProt = logs.Sum(x => x.ProteinG);
                decimal targetCarbs = goal?.TargetCarbsG ?? 200m;
                decimal consumedCarbs = logs.Sum(x => x.CarbsG);
                decimal targetFat = goal?.TargetFatG ?? 60m;
                decimal consumedFat = logs.Sum(x => x.FatG);

                var remainingBalance = new MacroSummaryDto(
                    Math.Max(0, targetCal - consumedCal),
                    Math.Max(0, targetProt - consumedProt),
                    Math.Max(0, targetCarbs - consumedCarbs),
                    Math.Max(0, targetFat - consumedFat)
                );

                string substitutionResult = await _agentService.GenerateSmartSubstitutionAsync(
                    request.FoodToSubstitute, remainingBalance, clinicalProtocol, foodAversions
                );

                return Ok(new { success = true, suggestion = substitutionResult });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Erro ao processar substituição inteligente." });
            }
        }
    }

    public class SubstitutionRequestDto
    {
        public string UserId { get; set; } = string.Empty;
        public string FoodToSubstitute { get; set; } = string.Empty;
    }
}