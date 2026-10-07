using LabelWise.Application.Interfaces.AI;
using LabelWise.Application.Interfaces.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LabelWise.Api.Controllers
{
    [ApiController]
    [Route("api/progress-report")]
    [AllowAnonymous]
    public class ProgressReportController : ControllerBase
    {
        private readonly IMongoDatabase _database;
        private readonly INutritionAgentService _agentService;
        private readonly INutritionRepository _nutritionRepository;

        public ProgressReportController(
            IMongoDatabase database,
            INutritionAgentService agentService,
            INutritionRepository nutritionRepository)
        {
            _database = database;
            _agentService = agentService;
            _nutritionRepository = nutritionRepository;
        }

        [HttpGet("weekly/{userId}")]
        public async Task<IActionResult> GetWeeklyReport(string userId)
        {
            try
            {
                var now = DateTime.UtcNow.AddHours(-3);
                var sevenDaysAgo = now.AddDays(-7);

                // 1. Obter dados do paciente
                var patientsCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("Nutrition_Patients");
                var patientDoc = await patientsCol.Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", userId)).FirstOrDefaultAsync();
                string patientName = patientDoc != null && patientDoc.Contains("Name") ? patientDoc["Name"].AsString : "Paciente";

                // 2. Obter pesos da semana
                var weightCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("WeightLogs");
                var weightFilter = Builders<MongoDB.Bson.BsonDocument>.Filter.And(
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("UserId", userId),
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Gte("LoggedAt", sevenDaysAgo)
                );
                var weightLogs = await weightCol.Find(weightFilter).SortBy(w => w["LoggedAt"]).ToListAsync();

                decimal initialWeight = weightLogs.Any() ? (decimal)weightLogs.First()["WeightKg"].AsDouble : 70.0m;
                decimal currentWeight = weightLogs.Any() ? (decimal)weightLogs.Last()["WeightKg"].AsDouble : initialWeight;

                // 3. Obter metas e refeições dos últimos 7 dias
                var goal = await _nutritionRepository.ObterMetaDiariaAsync(userId, now.Date);
                int targetCalories = goal?.TargetCalories ?? 2000;

                var mealLogsCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("MealLogs");
                var mealFilter = Builders<MongoDB.Bson.BsonDocument>.Filter.And(
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("UserId", userId),
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Gte("LoggedAt", sevenDaysAgo)
                );
                var mealLogs = await mealLogsCol.Find(mealFilter).ToListAsync();

                double avgCalories = mealLogs.Any() ? mealLogs.Average(m => m.Contains("Calories") ? m["Calories"].AsInt32 : 0) : targetCalories;

                // 4. Obter água da semana
                var waterCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("WaterLogs");
                var waterFilter = Builders<MongoDB.Bson.BsonDocument>.Filter.And(
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("UserId", userId),
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Gte("LoggedAt", sevenDaysAgo)
                );
                var waterLogs = await waterCol.Find(waterFilter).ToListAsync();
                int avgWater = waterLogs.Any() ? (int)waterLogs.Average(w => w["AmountMl"].AsInt32) : 2000;

                // 5. Gerar resumo com IA
                string aiSummary = await _agentService.GenerateWeeklyProgressSummaryAsync(
                    patientName, initialWeight, currentWeight, targetCalories, avgCalories, avgWater, streakDays: 5
                );

                return Ok(new
                {
                    success = true,
                    patientName,
                    initialWeight,
                    currentWeight,
                    avgConsumedCalories = avgCalories,
                    avgWaterMl = avgWater,
                    weeklyAiReport = aiSummary
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Erro ao gerar relatório semanal: {ex.Message}" });
            }
        }
    }
}