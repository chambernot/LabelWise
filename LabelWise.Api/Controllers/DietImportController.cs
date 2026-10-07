using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System;
using System.Threading.Tasks;

namespace LabelWise.Api.Controllers
{
    [ApiController]
    [Route("api/diet-import")]
    [AllowAnonymous]
    public class DietImportController : ControllerBase
    {
        private readonly IMongoDatabase _database;
        private readonly INutritionAgentService _agentService;

        public DietImportController(
            IMongoDatabase database,
            INutritionAgentService agentService)
        {
            _database = database;
            _agentService = agentService;
        }

        [HttpPost("parse-and-save")]
        public async Task<IActionResult> ParseAndSaveDiet([FromBody] ImportDietRequestDto request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.TextInput))
                {
                    return BadRequest(new { success = false, message = "UserId e TextInput são obrigatórios." });
                }

                // 1. Extrai os alvos e dados da dieta usando a IA
                var extractionRequest = new ExtractDietGoalRequestDto(request.TextInput, request.Base64Image);
                var extractedGoals = await _agentService.ExtractDietGoalsFromDocumentAsync(extractionRequest);

                // 2. Grava ou atualiza as metas diárias na coleção DailyGoals do MongoDB
                var goalsCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("DailyGoals");
                var goalFilter = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("UserId", request.UserId);
                var existingGoal = await goalsCollection.Find(goalFilter).FirstOrDefaultAsync();

                string goalId = existingGoal != null && existingGoal.Contains("_id")
                    ? existingGoal["_id"].AsString
                    : Guid.NewGuid().ToString();

                var goalDoc = new MongoDB.Bson.BsonDocument
                {
                    { "_id", goalId },
                    { "UserId", request.UserId },
                    { "NutritionistId", request.NutritionistId ?? "system_import" },
                    { "TargetDate", DateTime.UtcNow.AddHours(-3).Date },
                    { "TargetCalories", extractedGoals.TargetCalories },
                    { "TargetProteinG", (double)extractedGoals.TargetProteinG },
                    { "TargetCarbsG", (double)extractedGoals.TargetCarbsG },
                    { "TargetFatG", (double)extractedGoals.TargetFatG },
                    { "DietaryRestrictions", extractedGoals.DietaryRestrictions ?? "" },
                    { "FavoriteFoods", extractedGoals.FavoriteFoods ?? "" },
                    { "PrescribedMealPlan", extractedGoals.PrescribedMealPlan ?? "Plano importado via documento" }
                };

                await goalsCollection.ReplaceOneAsync(goalFilter, goalDoc, new ReplaceOptions { IsUpsert = true });

                // 3. Atualiza também as restrições clínicas no perfil do paciente se houver
                var patientsCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("Nutrition_Patients");
                var updatePatient = Builders<MongoDB.Bson.BsonDocument>.Update
                    .Set("ClinicalProtocol", extractedGoals.DietaryRestrictions)
                    .Set("FoodAversions", extractedGoals.FavoriteFoods);

                await patientsCollection.UpdateOneAsync(
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", request.UserId),
                    updatePatient
                );

                return Ok(new
                {
                    success = true,
                    message = "Dieta importada e processada com sucesso!",
                    data = extractedGoals
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Erro ao importar dieta: {ex.Message}" });
            }
        }
    }

    public class ImportDietRequestDto
    {
        public string UserId { get; set; } = string.Empty;
        public string? NutritionistId { get; set; }
        public string TextInput { get; set; } = string.Empty;
        public string? Base64Image { get; set; }
    }
}