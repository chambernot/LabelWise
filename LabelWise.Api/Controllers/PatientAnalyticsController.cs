using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LabelWise.Api.Controllers
{
    [ApiController]
    [Route("api/patient-analytics")]
    [AllowAnonymous]
    public class PatientAnalyticsController : ControllerBase
    {
        private readonly IMongoDatabase _database;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PatientAnalyticsController> _logger;

        public PatientAnalyticsController(
            IMongoDatabase database,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<PatientAnalyticsController> logger)
        {
            _database = database;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        // =========================================================================
        // 📊 1. GRÁFICOS DE EVOLUÇÃO (Item 7 - 7, 30 ou 90 dias)
        // =========================================================================
        [HttpGet("{phone}/evolution")]
        public async Task<IActionResult> GetEvolutionData(string phone, [FromQuery] int days = 7)
        {
            var startDate = DateTime.UtcNow.AddDays(-days).Date;

            var mealsCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("Nutrition_MealLogs");
            var waterCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("WaterLogs");
            var weightCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("WeightLogs");

            var filter = Builders<MongoDB.Bson.BsonDocument>.Filter.And(
                Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("UserId", phone),
                Builders<MongoDB.Bson.BsonDocument>.Filter.Gte("LoggedAt", startDate)
            );

            var meals = await mealsCol.Find(filter).ToListAsync();
            var waters = await waterCol.Find(filter).ToListAsync();
            var weights = await weightCol.Find(filter).ToListAsync();

            var timeline = Enumerable.Range(0, days)
                .Select(i => startDate.AddDays(i))
                .Select(date => {
                    var dayMeals = meals.Where(m => m["LoggedAt"].ToUniversalTime().Date == date).ToList();
                    var dayWater = waters.Where(w => w["LoggedAt"].ToUniversalTime().Date == date).Sum(w => w["AmountMl"].AsInt32);
                    var dayWeight = weights.Where(w => w["LoggedAt"].ToUniversalTime().Date == date).OrderByDescending(w => w["LoggedAt"]).FirstOrDefault()?["WeightKg"].ToDouble();

                    return new
                    {
                        date = date.ToString("yyyy-MM-dd"),
                        calories = dayMeals.Sum(m => m.Contains("Calories") ? m["Calories"].AsInt32 : 0),
                        protein = dayMeals.Sum(m => m.Contains("ProteinG") ? m["ProteinG"].ToDecimal() : 0m),
                        carbs = dayMeals.Sum(m => m.Contains("CarbsG") ? m["CarbsG"].ToDecimal() : 0m),
                        fat = dayMeals.Sum(m => m.Contains("FatG") ? m["FatG"].ToDecimal() : 0m),
                        mealsCount = dayMeals.Count,
                        waterMl = dayWater,
                        weightKg = dayWeight
                    };
                });

            return Ok(new { success = true, periodDays = days, data = timeline });
        }

        // =========================================================================
        // ✨ 2. RESUMO SEMANAL COM IA (Item 9)
        // =========================================================================
        [HttpGet("{phone}/weekly-summary")]
        public async Task<IActionResult> GetWeeklySummary(string phone)
        {
            try
            {
                var startDate = DateTime.UtcNow.AddDays(-7).Date;
                var mealsCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("Nutrition_MealLogs");
                var goalsCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("DailyGoals");

                var mealsFilter = Builders<MongoDB.Bson.BsonDocument>.Filter.And(
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("UserId", phone),
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Gte("LoggedAt", startDate)
                );

                var meals = await mealsCol.Find(mealsFilter).ToListAsync();
                var goal = await goalsCol.Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("UserId", phone)).FirstOrDefaultAsync();

                int targetCal = goal != null && goal.Contains("TargetCalories") ? goal["TargetCalories"].AsInt32 : 2000;
                decimal targetProt = goal != null && goal.Contains("TargetProteinG") ? goal["TargetProteinG"].ToDecimal() : 150m;

                int totalMeals = meals.Count;
                double avgCalories = totalMeals > 0 ? meals.Average(m => m.Contains("Calories") ? m["Calories"].AsInt32 : 0) : 0;
                double avgProtein = totalMeals > 0 ? (double)meals.Average(m => m.Contains("ProteinG") ? m["ProteinG"].ToDecimal() : 0m) : 0;

                string prompt = $@"
Você é um analista de dados clínicos e nutrição. Analise o desempenho dos últimos 7 dias do paciente:
- Total de refeições registadas: {totalMeals}
- Média calórica diária: {avgCalories:F0} kcal (Meta: {targetCal} kcal)
- Média diária de proteínas: {avgProtein:F0}g (Meta: {targetProt}g)

Gere um resumo analítico contendo:
1. Visão geral da consistência (Quantas refeições foram feitas, regularidade).
2. Pontos positivos.
3. Pontos de atenção (ex: proteína baixa, picos calóricos).
4. Comportamentos recorrentes ou sugestão de melhoria para a consulta.
Retorne o texto organizado, profissional e direto ao ponto para o nutricionista ler.
";

                string aiSummary = await CallGeminiForAnalyticsAsync(prompt);

                return Ok(new
                {
                    success = true,
                    totalMealsLogged = totalMeals,
                    averageCalories = Math.Round(avgCalories),
                    averageProtein = Math.Round(avgProtein),
                    aiQualitativeSummary = aiSummary
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar resumo semanal para o paciente {Phone}", phone);
                return StatusCode(500, new { success = false, message = "Erro ao processar resumo analítico com IA." });
            }
        }

        private async Task<string> CallGeminiForAnalyticsAsync(string prompt)
        {
            var apiKey = _configuration["GeminiApiKey"] ?? _configuration["Gemini:ApiKey"];
            var endpoint = _configuration["Gemini:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
            var model = _configuration["Model"] ?? "gemini-1.5-flash";

            var client = _httpClientFactory.CreateClient("Gemini");
            var requestBody = new
            {
                model = model,
                temperature = 0.4,
                messages = new object[] { new { role = "user", content = prompt } }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await client.SendAsync(requestMessage);
            if (!response.IsSuccessStatusCode) return "Não foi possível gerar a análise analítica no momento.";

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? string.Empty;
        }
    }
}