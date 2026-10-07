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
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class DashboardController : ControllerBase
    {
        private readonly IMongoDatabase _database;

        public DashboardController(IMongoDatabase database)
        {
            _database = database;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetDashboardSummary([FromQuery] string nutritionistId)
        {
            var patientsCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("Nutrition_Patients");
            var filter = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("ProfessionalId", nutritionistId);
            var patients = await patientsCol.Find(filter).ToListAsync();

            int totalPatients = patients.Count;
            int patientLimit = 30; // Limite padrão do plano Starter/Profissional

            int emDia = patients.Count(p => !p.Contains("Status") || p["Status"].AsString == "Em dia");
            int atencao = patients.Count(p => p.Contains("Status") && p["Status"].AsString == "Atenção");
            int inativos = patients.Count(p => p.Contains("Status") && p["Status"].AsString == "Inativo");

            return Ok(new
            {
                totalPatients,
                patientLimit,
                emDia,
                atencao,
                inativos,
                botStatus = "Online 🟢"
            });
        }

        [HttpGet("attention-list")]
        public async Task<IActionResult> GetAttentionPatients([FromQuery] string nutritionistId)
        {
            var patientsCol = _database.GetCollection<MongoDB.Bson.BsonDocument>("Nutrition_Patients");
            var filter = Builders<MongoDB.Bson.BsonDocument>.Filter.And(
                Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("ProfessionalId", nutritionistId),
                Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("Status", "Atenção")
            );

            var patients = await patientsCol.Find(filter).ToListAsync();
            var result = patients.Select(p => new
            {
                phone = p["_id"].AsString,
                name = p.Contains("FullName") ? p["FullName"].AsString : p["_id"].AsString,
                reason = p.Contains("AttentionReason") ? p["AttentionReason"].AsString : "Necessita de revisão do plano."
            });

            return Ok(result);
        }
    }
}