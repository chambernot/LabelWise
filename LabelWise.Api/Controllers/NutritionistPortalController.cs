using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces.AI;
using LabelWise.Domain.Entities.Nutrition;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Threading.Tasks;

namespace LabelWise.Api.Controllers;

[ApiController]
[Route("api/nutritionist")]
public class NutritionistPortalController : ControllerBase
{
    private readonly INutritionAgentService _aiService;
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<DailyNutritionGoal> _goalsCollection;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NutritionistPortalController> _logger;
    private readonly string _nutriKey;

    public NutritionistPortalController(
        INutritionAgentService aiService,
        IMongoDatabase database,
        IConfiguration configuration,
        ILogger<NutritionistPortalController> logger)
    {
        _aiService = aiService;
        _database = database;
        _goalsCollection = _database.GetCollection<DailyNutritionGoal>("DailyGoals");
        _configuration = configuration;
        _logger = logger;
        _nutriKey = configuration["NutritionistDefaultKey"] ?? "nutri_secret_123";
    }

    private async Task<AuthenticatedNutri?> ObterNutricionistaAutenticadoAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        if (key == _nutriKey)
        {
            return new AuthenticatedNutri(
                Id: "default_nutri_id",
                Name: "Administradora (Padrão)",
                Email: "admin@labelwise.com",
                MaxPatients: 999,
                IsMaster: true
            );
        }

        try
        {
            var nutriCollection = _database.GetCollection<Nutritionist>("Nutritionists");
            var nutri = await nutriCollection.Find(x => x.ApiKey == key && x.IsActive).FirstOrDefaultAsync();
            if (nutri == null) return null;

            return new AuthenticatedNutri(
                Id: nutri.Id ?? "unknown",
                Name: nutri.Name,
                Email: nutri.Email,
                MaxPatients: nutri.MaxPatients > 0 ? nutri.MaxPatients : 30,
                IsMaster: false
            );
        }
        catch
        {
            return null;
        }
    }

    [HttpGet("verify-key")]
    public async Task<IActionResult> VerifyKey([FromHeader(Name = "X-Nutri-Key")] string key)
    {
        var nutricionista = await ObterNutricionistaAutenticadoAsync(key);
        if (nutricionista == null)
        {
            return Unauthorized(new { success = false, message = "Chave de acesso inválida ou inativa." });
        }

        return Ok(new { success = true, name = nutricionista.Name, nutritionistId = nutricionista.Id });
    }

    [HttpGet("patient-logs/{phone}")]
    public async Task<IActionResult> GetPatientLogs(
        [FromHeader(Name = "X-Nutri-Key")] string key,
        string phone,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null)
    {
        var nutri = await ObterNutricionistaAutenticadoAsync(key);
        if (nutri == null)
        {
            return Unauthorized(new { success = false, message = "Chave de acesso inválida." });
        }

        try
        {
            var logsCollection = _database.GetCollection<MealLog>("Nutrition_MealLogs");

            var builder = Builders<MealLog>.Filter;
            var filter = builder.Eq(x => x.UserId, phone);

            if (DateTime.TryParse(startDate, out var start))
            {
                filter = builder.And(filter, builder.Gte(x => x.LoggedAt, start.Date));
            }
            else
            {
                var defaultStart = DateTime.UtcNow.AddDays(-30).Date;
                filter = builder.And(filter, builder.Gte(x => x.LoggedAt, defaultStart));
            }

            if (DateTime.TryParse(endDate, out var end))
            {
                filter = builder.And(filter, builder.Lt(x => x.LoggedAt, end.Date.AddDays(1)));
            }

            var logs = await logsCollection
                .Find(filter)
                .SortByDescending(x => x.LoggedAt)
                .ToListAsync();

            var goal = await _goalsCollection
                .Find(x => x.UserId == phone)
                .SortByDescending(x => x.TargetDate)
                .FirstOrDefaultAsync();

            return Ok(new
            {
                success = true,
                data = logs,
                targetCalories = goal?.TargetCalories ?? 2000,
                targetProtein = goal?.TargetProteinG ?? 150,
                targetCarbs = goal?.TargetCarbsG ?? 200,
                targetFat = goal?.TargetFatG ?? 60
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar histórico de consumo do paciente {Phone}", phone);
            return StatusCode(500, new { success = false, message = "Erro interno ao buscar histórico." });
        }
    }

    [HttpDelete("patient/{phone}")]
    public async Task<IActionResult> DeletePatient(
        [FromHeader(Name = "X-Nutri-Key")] string key,
        string phone)
    {
        var nutri = await ObterNutricionistaAutenticadoAsync(key);
        if (nutri == null)
        {
            return Unauthorized(new { success = false, message = "Chave de acesso inválida." });
        }

        try
        {
            await _goalsCollection.DeleteManyAsync(x => x.UserId == phone);

            var logsCollection = _database.GetCollection<MealLog>("Nutrition_MealLogs");
            await logsCollection.DeleteManyAsync(x => x.UserId == phone);

            var patientsCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("Nutrition_Patients");
            await patientsCollection.DeleteOneAsync(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", phone));

            return Ok(new { success = true, message = "Paciente excluído com sucesso." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir o paciente {Phone}", phone);
            return StatusCode(500, new { success = false, message = "Erro interno ao excluir paciente." });
        }
    }

    [HttpGet("patients")]
    public async Task<IActionResult> GetPatients([FromHeader(Name = "X-Nutri-Key")] string key)
    {
        var nutri = await ObterNutricionistaAutenticadoAsync(key);
        if (nutri == null)
        {
            return Unauthorized(new { success = false, message = "Chave de acesso inválida." });
        }

        try
        {
            var filter = nutri.IsMaster
                ? Builders<DailyNutritionGoal>.Filter.Empty
                : Builders<DailyNutritionGoal>.Filter.Eq(x => x.NutritionistId, nutri.Id);

            var goals = await _goalsCollection
                .Find(filter)
                .SortByDescending(x => x.TargetDate)
                .ToListAsync();

            // 🛡️ Consultar a coleção de pacientes para extrair Protocolo e Objetivo
            var patientsCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("Nutrition_Patients");
            var resultList = new System.Collections.Generic.List<object>();

            foreach (var g in goals)
            {
                string clinicalProtocol = "";
                string mainGoal = "Emagrecimento"; // Valor padrão caso não encontre

                var patientDoc = await patientsCollection.Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", g.UserId)).FirstOrDefaultAsync();
                if (patientDoc != null)
                {
                    if (patientDoc.Contains("ClinicalProtocol"))
                    {
                        clinicalProtocol = patientDoc["ClinicalProtocol"].AsString;
                    }
                    if (patientDoc.Contains("MainGoal"))
                    {
                        mainGoal = patientDoc["MainGoal"].AsString;
                    }
                }

                resultList.Add(new
                {
                    UserId = g.UserId,
                    TargetCalories = g.TargetCalories,
                    TargetProteinG = g.TargetProteinG,
                    TargetCarbsG = g.TargetCarbsG,
                    TargetFatG = g.TargetFatG,
                    MainGoal = mainGoal, // 🛡️ Atribuído corretamente a partir do BsonDocument
                    DietaryRestrictions = g.DietaryRestrictions,
                    FavoriteFoods = g.FavoriteFoods,
                    PrescribedMealPlan = g.PrescribedMealPlan,
                    ClinicalProtocol = clinicalProtocol
                });
            }

            return Ok(new { success = true, data = resultList });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar pacientes no portal.");
            return StatusCode(500, new { success = false, message = "Erro interno ao buscar pacientes." });
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterNutritionist(
        [FromHeader(Name = "X-Admin-Secret")] string adminSecret,
        [FromBody] RegisterNutritionistDto dto)
    {
        var expectedAdminSecret = _configuration["AdminSecret"] ?? "admin_master_secret_123";
        if (adminSecret != expectedAdminSecret)
        {
            return Unauthorized(new { success = false, message = "Acesso negado." });
        }

        try
        {
            var collection = _database.GetCollection<Nutritionist>("Nutritionists");

            var existing = await collection.Find(x => x.Email == dto.Email.ToLowerInvariant()).FirstOrDefaultAsync();
            if (existing != null)
            {
                return BadRequest(new { success = false, message = "Já existe uma nutricionista cadastrada com este e-mail." });
            }

            var novaNutri = new Nutritionist(dto.Name, dto.Email, dto.ApiKey);
            await collection.InsertOneAsync(novaNutri);

            return Ok(new
            {
                success = true,
                message = "Nutricionista cadastrada com sucesso!",
                nutritionistId = novaNutri.Id,
                apiKey = novaNutri.ApiKey
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao cadastrar nutricionista.");
            return StatusCode(500, new { success = false, message = "Erro interno ao cadastrar nutricionista." });
        }
    }

    [HttpPost("extract")]
    public async Task<IActionResult> ExtractDietData(
        [FromHeader(Name = "X-Nutri-Key")] string key,
        [FromBody] ExtractDietGoalRequestDto request)
    {
        var nutri = await ObterNutricionistaAutenticadoAsync(key);
        if (nutri == null)
        {
            return Unauthorized(new { success = false, message = "Chave de acesso da nutricionista inválida." });
        }

        try
        {
            var extractedData = await _aiService.ExtractDietGoalsFromDocumentAsync(request);
            return Ok(new { success = true, data = extractedData });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao extrair dados da dieta via IA.");
            return StatusCode(500, new { success = false, message = "Erro ao processar o documento da dieta com IA." });
        }
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmAndSaveDiet(
        [FromHeader(Name = "X-Nutri-Key")] string key,
        [FromBody] ConfirmDietRequestDto dto)
    {
        var nutri = await ObterNutricionistaAutenticadoAsync(key);
        if (nutri == null)
        {
            return Unauthorized(new { success = false, message = "Chave de acesso da nutricionista inválida." });
        }

        return await SalvarOuAtualizarDietaAsync(
            nutri,
            dto.PatientPhone,
            dto.Calories,
            dto.Protein,
            dto.Carbs,
            dto.Fat,
            dto.MainGoal,
            dto.DietaryRestrictions,
            dto.FavoriteFoods,
            dto.PrescribedMealPlan,
            dto.ClinicalProtocol // 🛡️ Guardrail
        );
    }

    [HttpPost("import-diet")]
    public async Task<IActionResult> ImportarDietaPaciente(
        [FromHeader(Name = "X-Nutri-Key")] string key,
        [FromBody] ImportDietDto dto)
    {
        var nutri = await ObterNutricionistaAutenticadoAsync(key);
        if (nutri == null)
        {
            return Unauthorized(new { success = false, message = "Chave de acesso da nutricionista inválida." });
        }

        return await SalvarOuAtualizarDietaAsync(
            nutri,
            dto.PatientPhone,
            dto.Calories,
            dto.Protein,
            dto.Carbs,
            dto.Fat,
            dto.MainGoal,
            dto.DietaryRestrictions,
            dto.FavoriteFoods,
            dto.PrescribedMealPlan,
            dto.ClinicalProtocol // 🛡️ Guardrail
        );
    }

    [HttpPut("admin/update-limit/{nutritionistId}")]
    public async Task<IActionResult> UpdateNutritionistLimit(
    [FromHeader(Name = "X-Admin-Secret")] string adminSecret,
    string nutritionistId,
    [FromBody] UpdateLimitDto dto)
    {
        var expectedAdminSecret = _configuration["AdminSecret"] ?? "admin_master_secret_123";
        if (adminSecret != expectedAdminSecret)
        {
            return Unauthorized(new { success = false, message = "Acesso negado." });
        }

        try
        {
            var nutriCollection = _database.GetCollection<Nutritionist>("Nutritionists");
            var update = Builders<Nutritionist>.Update.Set(x => x.MaxPatients, dto.NewMaxPatients);
            var result = await nutriCollection.UpdateOneAsync(x => x.Id == nutritionistId, update);

            if (result.MatchedCount == 0)
            {
                return NotFound(new { success = false, message = "Nutricionista não encontrada." });
            }

            return Ok(new { success = true, message = $"Limite atualizado para {dto.NewMaxPatients} pacientes com sucesso!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar limite da nutricionista.");
            return StatusCode(500, new { success = false, message = "Erro interno ao atualizar plano." });
        }
    }

    [HttpGet("patient-report/{phone}")]
    public async Task<IActionResult> GeneratePatientReport(
    [FromHeader(Name = "X-Nutri-Key")] string key,
    string phone)
    {
        var nutri = await ObterNutricionistaAutenticadoAsync(key);
        if (nutri == null)
        {
            return Unauthorized(new { success = false, message = "Chave de acesso inválida." });
        }

        try
        {
            // 1. Buscar dados do paciente e protocolo
            var patientsCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("Nutrition_Patients");
            var patientDoc = await patientsCollection.Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", phone)).FirstOrDefaultAsync();

            string nomePaciente = phone; // Pode ajustar se tiver campo Name na collection
            string clinicalProtocol = patientDoc != null && patientDoc.Contains("ClinicalProtocol") ? patientDoc["ClinicalProtocol"].AsString : "Nenhum protocolo definido.";
            string mainGoal = patientDoc != null && patientDoc.Contains("MainGoal") ? patientDoc["MainGoal"].AsString : "Não especificado";

            // 2. Buscar metas diárias
            var goal = await _goalsCollection
                .Find(x => x.UserId == phone)
                .SortByDescending(x => x.TargetDate)
                .FirstOrDefaultAsync();

            int targetCal = goal?.TargetCalories ?? 2000;
            decimal targetProt = goal?.TargetProteinG ?? 150;
            decimal targetCarb = goal?.TargetCarbsG ?? 200;
            decimal targetFat = goal?.TargetFatG ?? 60;

            // 3. Buscar logs dos últimos 30 dias
            var logsCollection = _database.GetCollection<MealLog>("Nutrition_MealLogs");
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30).Date;
            var logs = await logsCollection
                .Find(x => x.UserId == phone && x.LoggedAt >= thirtyDaysAgo)
                .SortByDescending(x => x.LoggedAt)
                .ToListAsync();

            int totalRefeicoes = logs.Count;
            int mediaCalorias = totalRefeicoes > 0 ? (int)logs.Average(x => x.Calories) : 0;

            // 4. Construir um Relatório em HTML formatado para Impressão/PDF
            var htmlReport = $@"
        <!DOCTYPE html>
        <html lang='pt-BR'>
        <head>
            <meta charset='UTF-8'>
            <title>Relatório Pré-Consulta - {phone}</title>
            <style>
                body {{ font-family: Arial, sans-serif; margin: 40px; color: #333; }}
                h1 {{ color: #059669; border-bottom: 2px solid #059669; padding-bottom: 10px; }}
                .section {{ margin-top: 20px; padding: 15px; background: #f9fafb; border-radius: 8px; border: 1px solid #e5e7eb; }}
                .grid {{ display: flex; gap: 20px; }}
                .card {{ flex: 1; background: #fff; padding: 15px; border-radius: 6px; border: 1px solid #d1d5db; text-align: center; }}
                table {{ width: 100%; border-collapse: collapse; margin-top: 15px; }}
                th, td {{ border: 1px solid #d1d5db; padding: 8px; text-align: left; font-size: 12px; }}
                th {{ background: #059669; color: white; }}
            </style>
        </head>
        <body>
            <h1>🥗 Nutrição Facil - Relatório Pré-Consulta</h1>
            <p><strong>Paciente (WhatsApp):</strong> {phone}</p>
            <p><strong>Objetivo Principal:</strong> {mainGoal}</p>
            <p><strong>Data de Emissão:</strong> {DateTime.UtcNow.AddHours(-3):dd/MM/yyyy HH:mm}</p>

            <div class='section'>
                <h3>🛡️ Protocolo Clínico & Guardrails Ativos</h3>
                <p><em>{clinicalProtocol}</em></p>
            </div>

            <div class='grid' style='margin-top: 20px;'>
                <div class='card'>
                    <h4>Meta Calórica</h4>
                    <p style='font-size: 20px; font-weight: bold; color: #059669;'>{targetCal} kcal</p>
                </div>
                <div class='card'>
                    <h4>Média Consumida (30d)</h4>
                    <p style='font-size: 20px; font-weight: bold; color: #2563eb;'>{mediaCalorias} kcal</p>
                </div>
                <div class='card'>
                    <h4>Total de Registos</h4>
                    <p style='font-size: 20px; font-weight: bold; color: #7c3aed;'>{totalRefeicoes} refeições</p>
                </div>
            </div>

            <div class='section'>
                <h3>📊 Histórico Recente de Refeições</h3>
                <table>
                    <thead>
                        <tr>
                            <th>Data/Hora</th>
                            <th>Tipo</th>
                            <th>Prato / Alimento</th>
                            <th>Calorias</th>
                            <th>Macros (P / C / G)</th>
                        </tr>
                    </thead>
                    <tbody>";

            foreach (var l in logs.Take(20))
            {
                htmlReport += $@"
                        <tr>
                            <td>{l.LoggedAt.AddHours(-3):dd/MM/yyyy HH:mm}</td>
                            <td>{l.MealType}</td>
                            <td>{l.DishName}</td>
                            <td><strong>{l.Calories} kcal</strong></td>
                            <td>{l.ProteinG}g / {l.CarbsG}g / {l.FatG}g</td>
                        </tr>";
            }

            htmlReport += $@"
                    </tbody>
                </table>
            </div>
            <script>
                window.onload = function() {{ window.print(); }}
            </script>
        </body>
        </html>";

            var bytes = System.Text.Encoding.UTF8.GetBytes(htmlReport);
            return File(bytes, "text/html", $"Relatorio_Paciente_{phone}.html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar relatório para o paciente {Phone}", phone);
            return StatusCode(500, new { success = false, message = "Erro interno ao gerar relatório." });
        }
    }
    public record UpdateLimitDto(int NewMaxPatients);

    private async Task<IActionResult> SalvarOuAtualizarDietaAsync(
        AuthenticatedNutri nutri,
        string patientPhone,
        int calories,
        decimal protein,
        decimal carbs,
        decimal fat,
        string? mainGoal,
        string? dietaryRestrictions,
        string? favoriteFoods,
        string? prescribedMealPlan,
        string? clinicalProtocol) // 🛡️ Novo parâmetro
    {
        try
        {
            // 1. FORÇAR A GRAVAÇÃO NA TABELA DE PACIENTES COM O PROTOCOLO CLÍNICO (GUARDRAILS)
            var patientsCollection = _database.GetCollection<MongoDB.Bson.BsonDocument>("Nutrition_Patients");
            var patientUpdate = Builders<MongoDB.Bson.BsonDocument>.Update
                .Set("ProfessionalId", nutri.Id)
                .Set("MainGoal", mainGoal ?? "Emagrecimento")
                .Set("MedicalRestrictions", dietaryRestrictions ?? "")
                .Set("FoodAversions", favoriteFoods ?? "")
                .Set("ClinicalProtocol", clinicalProtocol ?? ""); // 🛡️ O Guardrail gravado aqui

            await patientsCollection.UpdateOneAsync(
                Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", patientPhone),
                patientUpdate,
                new UpdateOptions { IsUpsert = true }
            );

            // 2. BUSCAR A META E INSERIR CASO NÃO EXISTA
            var filter = Builders<DailyNutritionGoal>.Filter.Eq(x => x.UserId, patientPhone);
            var existingGoal = await _goalsCollection.Find(filter).FirstOrDefaultAsync();

            if (existingGoal == null)
            {
                var currentPatientCount = await _goalsCollection.CountDocumentsAsync(x => x.NutritionistId == nutri.Id);
                if (currentPatientCount >= nutri.MaxPatients)
                {
                    return BadRequest(new { success = false, message = $"❌ Limite atingido ({currentPatientCount}/{nutri.MaxPatients})." });
                }

                var novaMeta = new DailyNutritionGoal(
                    userId: patientPhone,
                    targetDate: DateTime.UtcNow,
                    targetCalories: calories,
                    targetProteinG: protein,
                    targetCarbsG: carbs,
                    targetFatG: fat,
                    nutritionistId: nutri.Id,
                    dietaryRestrictions: dietaryRestrictions,
                    favoriteFoods: favoriteFoods,
                    prescribedMealPlan: prescribedMealPlan
                );
                await _goalsCollection.InsertOneAsync(novaMeta);
            }

            // 3. UPDATE FORÇADO NO BANCO DE DADOS
            var forceUpdate = Builders<DailyNutritionGoal>.Update
                .Set(x => x.TargetCalories, calories)
                .Set(x => x.TargetProteinG, protein)
                .Set(x => x.TargetCarbsG, carbs)
                .Set(x => x.TargetFatG, fat)
                .Set(x => x.NutritionistId, nutri.Id)
                .Set("DietaryRestrictions", dietaryRestrictions ?? "")
                .Set("FavoriteFoods", favoriteFoods ?? "")
                .Set("PrescribedMealPlan", prescribedMealPlan ?? "");

            await _goalsCollection.UpdateOneAsync(filter, forceUpdate);

            _logger.LogInformation("✅ Dieta e Protocolo Clínico (Guardrails) gravados para o paciente {Phone}", patientPhone);

            return Ok(new { success = true, message = "Dieta e protocolo clínico atualizados com sucesso!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar a dieta para o paciente {Phone}", patientPhone);
            return StatusCode(500, new { success = false, message = "Erro interno ao salvar a dieta." });
        }
    }
}

public record AuthenticatedNutri(
    string Id,
    string Name,
    string Email,
    int MaxPatients,
    bool IsMaster
);

public record RegisterNutritionistDto(
    string Name,
    string Email,
    string? ApiKey
);

public record ConfirmDietRequestDto(
    string PatientPhone,
    string NutritionistId,
    int Calories,
    decimal Protein,
    decimal Carbs,
    decimal Fat,
    string? MainGoal,
    string? DietaryRestrictions,
    string? FavoriteFoods,
    string? PrescribedMealPlan,
    string? ClinicalProtocol // 🛡️ Novo campo DTO
);

public record ImportDietDto(
    string PatientPhone,
    string NutritionistId,
    int Calories,
    decimal Protein,
    decimal Carbs,
    decimal Fat,
    string? MainGoal,
    string? DietaryRestrictions,
    string? FavoriteFoods,
    string? PrescribedMealPlan,
    string? ClinicalProtocol // 🛡️ Novo campo DTO
);