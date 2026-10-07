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
    [Route("api/patients-management")]
    [AllowAnonymous]
    public class PatientManagementController : ControllerBase
    {
        private readonly IMongoDatabase _database;

        public PatientManagementController(IMongoDatabase database)
        {
            _database = database;
        }

        // =========================================================================
        // 🔄 1. MOTOR DE ATUALIZAÇÃO DE STATUS E ALERTAS (Itens 2, 3 e 10)
        // =========================================================================
        [HttpPost("recalculate-statuses")]
        public async Task<IActionResult> RecalculateAllPatientStatuses([FromQuery] string nutritionistId)
        {
            var patientsCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("Nutrition_Patients");
            var mealsCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("Nutrition_MealLogs");

            var filter = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("ProfessionalId", nutritionistId);
            var patients = await patientsCol.Find(filter).ToListAsync();

            int evaluatedCount = 0;
            var now = DateTime.UtcNow;

            foreach (var patient in patients)
            {
                string phone = patient["_id"].AsString;
                bool isArchived = patient.Contains("IsArchived") && patient["IsArchived"].AsBoolean;

                if (isArchived) continue; // Ignora pacientes arquivados no motor de alerta ativo

                // Buscar refeições dos últimos 7 dias
                var sevenDaysAgo = now.AddDays(-7).Date;
                var mealFilter = Builders<MongoDB.Bson.BsonDocument>.Filter.And(
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("UserId", phone),
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Gte("LoggedAt", sevenDaysAgo)
                );
                var patientMeals = await mealsCol.Find(mealFilter).ToListAsync();

                // Regras de Avaliação de Status
                string calculatedStatus = "Em dia";
                string attentionReason = string.Empty;

                var lastMeal = patientMeals.OrderByDescending(m => m["LoggedAt"]).FirstOrDefault();

                if (lastMeal == null)
                {
                    calculatedStatus = "Inativo";
                    attentionReason = "Nunca registou refeições ou está inativo há mais de 7 dias.";
                }
                else
                {
                    var lastMealDate = lastMeal["LoggedAt"].ToUniversalTime();
                    int daysWithoutLog = (now.Date - lastMealDate.Date).Days;

                    if (daysWithoutLog >= 3)
                    {
                        calculatedStatus = "Inativo";
                        attentionReason = $"Sem registar refeições há {daysWithoutLog} dias.";
                    }
                    else if (daysWithoutLog >= 2)
                    {
                        calculatedStatus = "Atenção";
                        attentionReason = $"Sem registar refeições há {daysWithoutLog} dias.";
                    }
                    else
                    {
                        // Verificar adesão de proteína nos últimos dias
                        int daysChecked = 0;
                        int lowProteinDays = 0;

                        for (int i = 1; i <= 3; i++)
                        {
                            var targetDate = now.AddDays(-i).Date;
                            var dayMeals = patientMeals.Where(m => m["LoggedAt"].ToUniversalTime().Date == targetDate).ToList();

                            if (dayMeals.Any())
                            {
                                daysChecked++;
                                decimal dayProtein = dayMeals.Sum(m => m.Contains("ProteinG") ? m["ProteinG"].ToDecimal() : 0m);
                                if (dayProtein < 90m) // Limiar de segurança genérico de proteína
                                {
                                    lowProteinDays++;
                                }
                            }
                        }

                        if (daysChecked >= 2 && lowProteinDays >= 2)
                        {
                            calculatedStatus = "Atenção";
                            attentionReason = "Proteína abaixo da meta nos últimos dias consecutivos.";
                        }
                    }
                }

                // Atualizar na base de dados
                var updateDef = Builders<MongoDB.Bson.BsonDocument>.Update
                    .Set("Status", calculatedStatus)
                    .Set("AttentionReason", attentionReason)
                    .Set("LastEvaluationDate", now);

                await patientsCol.UpdateOneAsync(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", phone), updateDef);
                evaluatedCount++;
            }

            return Ok(new { success = true, message = $"Motor executado com sucesso. {evaluatedCount} pacientes avaliados." });
        }

        // =========================================================================
        // 📦 2. GESTÃO DE ARQUIVAMENTO DE PACIENTES (Item 26 - SaaS)
        // =========================================================================
        [HttpPost("{phone}/archive")]
        public async Task<IActionResult> ToggleArchivePatient(string phone, [FromQuery] bool archive)
        {
            var patientsCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("Nutrition_Patients");
            var filter = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", phone);

            var update = Builders<MongoDB.Bson.BsonDocument>.Update
                .Set("IsArchived", archive)
                .Set("Status", archive ? "Arquivado" : "Em dia");

            var result = await patientsCol.UpdateOneAsync(filter, update);

            if (result.MatchedCount == 0)
                return NotFound(new { success = false, message = "Paciente não encontrado." });

            return Ok(new { success = true, message = archive ? "Paciente arquivado com sucesso. Não consome o limite ativo." : "Paciente reativado com sucesso." });
        }
    }
}