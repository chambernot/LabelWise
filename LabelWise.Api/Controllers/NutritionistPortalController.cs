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

    /// <summary>
    /// Método auxiliar centralizado que valida a chave e retorna os dados da sessão autenticada.
    /// </summary>
    private async Task<AuthenticatedNutri?> ObterNutricionistaAutenticadoAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        // Se for a chave master/padrão do sistema
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

        // Valida se existe no banco de dados na collection "Nutritionists"
        try
        {
            var nutriCollection = _database.GetCollection<Nutritionist>("Nutritionists");
            var nutri = await nutriCollection.Find(x => x.ApiKey == key && x.IsActive).FirstOrDefaultAsync();
            if (nutri == null) return null;

            return new AuthenticatedNutri(
                Id: nutri.Id ?? "unknown",
                Name: nutri.Name,
                Email: nutri.Email,
                MaxPatients: nutri.MaxPatients > 0 ? nutri.MaxPatients : 30, // Padrão de 30 pacientes por plano
                IsMaster: false
            );
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Valida se a API Key informada no login é real e ativa.
    /// </summary>
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

    /// <summary>
    /// Retorna o histórico de refeições de um paciente filtrado por período para avaliação mensal/diária.
    /// </summary>
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

    /// <summary>
    /// Exclui o paciente e revoga o acesso dele ao bot do WhatsApp.
    /// </summary>
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

            return Ok(new { success = true, message = "Paciente excluído com sucesso." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir o paciente {Phone}", phone);
            return StatusCode(500, new { success = false, message = "Erro interno ao excluir paciente." });
        }
    }

    /// <summary>
    /// Lista apenas os pacientes vinculados à nutricionista autenticada (ou todos se for master).
    /// </summary>
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

            var patients = await _goalsCollection
                .Find(filter)
                .SortByDescending(x => x.TargetDate)
                .ToListAsync();

            return Ok(new { success = true, data = patients });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar pacientes no portal.");
            return StatusCode(500, new { success = false, message = "Erro interno ao buscar pacientes." });
        }
    }

    /// <summary>
    /// CADASTRO DE NUTRICIONISTA: Cria um novo perfil profissional e gera sua API Key exclusiva.
    /// </summary>
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

            _logger.LogInformation("✅ Nutricionista cadastrada com sucesso: {Email}", dto.Email);

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

    /// <summary>
    /// ETAPA 1: A nutricionista envia a foto do cardápio/dieta ou texto. 
    /// O Gemini lê, estrutura e devolve os macros calculados para revisão na tela.
    /// </summary>
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

    /// <summary>
    /// ETAPA 2: Após a nutricionista revisar (ou preencher manualmente), 
    /// ela confirma e o sistema salva ou atualiza definitivamente na base do paciente.
    /// </summary>
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
            dto.DietaryRestrictions,
            dto.FavoriteFoods,
            dto.PrescribedMealPlan
        );
    }

    /// <summary>
    /// Importação Direta com validação de limite do plano da nutricionista.
    /// </summary>
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
            dto.DietaryRestrictions,
            dto.FavoriteFoods,
            dto.PrescribedMealPlan
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

            // Atualiza o MaxPatients e opcionalmente o nome do plano
            var update = Builders<Nutritionist>.Update
                .Set(x => x.MaxPatients, dto.NewMaxPatients);

            var result = await nutriCollection.UpdateOneAsync(x => x.Id == nutritionistId, update);

            if (result.MatchedCount == 0)
            {
                return NotFound(new { success = false, message = "Nutricionista não encontrada." });
            }

            _logger.LogInformation("✅ Plano atualizado para a nutri ID {Id}. Novo limite: {Limit}", nutritionistId, dto.NewMaxPatients);
            return Ok(new { success = true, message = $"Limite atualizado para {dto.NewMaxPatients} pacientes com sucesso!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar limite da nutricionista.");
            return StatusCode(500, new { success = false, message = "Erro interno ao atualizar plano." });
        }
    }

    public record UpdateLimitDto(int NewMaxPatients);

    /// <summary>
    /// Método auxiliar privado que aplica a regra de limite de pacientes e persiste os dados com segurança.
    /// </summary>
    private async Task<IActionResult> SalvarOuAtualizarDietaAsync(
        AuthenticatedNutri nutri,
        string patientPhone,
        int calories,
        decimal protein,
        decimal carbs,
        decimal fat,
        string? dietaryRestrictions,
        string? favoriteFoods,
        string? prescribedMealPlan)
    {
        try
        {
            var filter = Builders<DailyNutritionGoal>.Filter.Eq(x => x.UserId, patientPhone);
            var existingGoal = await _goalsCollection.Find(filter).FirstOrDefaultAsync();

            // Se for um paciente novo, validamos o teto máximo de vagas do plano contratado
            if (existingGoal == null)
            {
                var currentPatientCount = await _goalsCollection.CountDocumentsAsync(x => x.NutritionistId == nutri.Id);

                if (currentPatientCount >= nutri.MaxPatients)
                {
                    _logger.LogWarning("⚠️ Nutricionista {Email} atingiu o limite de pacientes ({Count}/{Limit})", nutri.Email, currentPatientCount, nutri.MaxPatients);
                    return BadRequest(new
                    {
                        success = false,
                        message = $"❌ Limite de pacientes atingido ({currentPatientCount}/{nutri.MaxPatients}). Faça upgrade no seu plano para cadastrar mais."
                    });
                }
            }

            if (existingGoal != null)
            {
                existingGoal.UpdateGoals(calories, protein, carbs, fat, nutri.Id);
                existingGoal.UpdatePreferences(dietaryRestrictions, favoriteFoods);

                await _goalsCollection.ReplaceOneAsync(filter, existingGoal);
            }
            else
            {
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

            _logger.LogInformation("✅ Dieta salva com sucesso para o paciente {Phone} pela nutri {Nutri}", patientPhone, nutri.Id);

            return Ok(new { success = true, message = "Dieta do paciente cadastrada/atualizada com sucesso!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar a dieta para o paciente {Phone}", patientPhone);
            return StatusCode(500, new { success = false, message = "Erro interno ao salvar a dieta." });
        }
    }
}

// Record auxiliar para gerenciar a sessão autenticada com segurança
public record AuthenticatedNutri(
    string Id,
    string Name,
    string Email,
    int MaxPatients,
    bool IsMaster
);

// DTOs auxiliares do Controller
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
    string? DietaryRestrictions,
    string? FavoriteFoods,
    string? PrescribedMealPlan
);

public record ImportDietDto(
    string PatientPhone,
    string NutritionistId,
    int Calories,
    decimal Protein,
    decimal Carbs,
    decimal Fat,
    string? DietaryRestrictions,
    string? FavoriteFoods,
    string? PrescribedMealPlan
);