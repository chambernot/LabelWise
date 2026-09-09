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
    /// Método auxiliar centralizado para validar se a chave é a padrão ou se existe ativa no MongoDB.
    /// </summary>
    private async Task<bool> ValidarChaveAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;

        // Permite a chave padrão configurada no appsettings/Render
        if (key == _nutriKey) return true;

        // Valida se existe no banco de dados na collection "Nutritionists"
        try
        {
            var nutriCollection = _database.GetCollection<Nutritionist>("Nutritionists");
            var nutri = await nutriCollection.Find(x => x.ApiKey == key && x.IsActive).FirstOrDefaultAsync();
            return nutri != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Valida se a API Key informada no login é real e ativa.
    /// </summary>
    [HttpGet("verify-key")]
    public async Task<IActionResult> VerifyKey([FromHeader(Name = "X-Nutri-Key")] string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Unauthorized(new { success = false, message = "Chave não informada." });
        }

        // Se for a chave padrão do sistema
        if (key == _nutriKey)
        {
            return Ok(new { success = true, name = "Administradora (Padrão)", nutritionistId = "default_nutri_id" });
        }

        var nutriCollection = _database.GetCollection<Nutritionist>("Nutritionists");
        var nutricionista = await nutriCollection
            .Find(x => x.ApiKey == key && x.IsActive)
            .FirstOrDefaultAsync();

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
        if (!await ValidarChaveAsync(key))
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

            var goalsCollection = _database.GetCollection<DailyNutritionGoal>("DailyGoals");
            var goal = await goalsCollection
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
        if (!await ValidarChaveAsync(key))
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
    /// Lista todos os pacientes/metas cadastradas para visualização no dashboard.
    /// </summary>
    [HttpGet("patients")]
    public async Task<IActionResult> GetPatients([FromHeader(Name = "X-Nutri-Key")] string key)
    {
        if (!await ValidarChaveAsync(key))
        {
            return Unauthorized(new { success = false, message = "Chave de acesso inválida." });
        }

        try
        {
            var patients = await _goalsCollection
                .Find(_ => true)
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
        if (!await ValidarChaveAsync(key))
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
        if (!await ValidarChaveAsync(key))
        {
            return Unauthorized(new { success = false, message = "Chave de acesso da nutricionista inválida." });
        }

        try
        {
            var filter = Builders<DailyNutritionGoal>.Filter.Eq(x => x.UserId, dto.PatientPhone);
            var existingGoal = await _goalsCollection.Find(filter).FirstOrDefaultAsync();

            if (existingGoal != null)
            {
                existingGoal.UpdateGoals(dto.Calories, dto.Protein, dto.Carbs, dto.Fat, dto.NutritionistId);
                existingGoal.UpdatePreferences(dto.DietaryRestrictions, dto.FavoriteFoods);

                await _goalsCollection.ReplaceOneAsync(filter, existingGoal);
            }
            else
            {
                var novaMeta = new DailyNutritionGoal(
                    userId: dto.PatientPhone,
                    targetDate: DateTime.UtcNow,
                    targetCalories: dto.Calories,
                    targetProteinG: dto.Protein,
                    targetCarbsG: dto.Carbs,
                    targetFatG: dto.Fat,
                    nutritionistId: dto.NutritionistId,
                    dietaryRestrictions: dto.DietaryRestrictions,
                    favoriteFoods: dto.FavoriteFoods,
                    prescribedMealPlan: dto.PrescribedMealPlan
                );

                await _goalsCollection.InsertOneAsync(novaMeta);
            }

            _logger.LogInformation("✅ Dieta validada e salva com sucesso para o paciente {Phone} pela nutri {Nutri}", dto.PatientPhone, dto.NutritionistId);

            return Ok(new { success = true, message = "Dieta do paciente cadastrada/atualizada com sucesso!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar a dieta confirmada para o paciente {Phone}", dto.PatientPhone);
            return StatusCode(500, new { success = false, message = "Erro interno ao salvar a dieta." });
        }
    }

    /// <summary>
    /// Importação Direta (Caso a nutri já possua os valores mastigados sem passar pela IA).
    /// </summary>
    [HttpPost("import-diet")]
    public async Task<IActionResult> ImportarDietaPaciente(
        [FromHeader(Name = "X-Nutri-Key")] string key,
        [FromBody] ImportDietDto dto)
    {
        if (!await ValidarChaveAsync(key))
        {
            return Unauthorized(new { success = false, message = "Chave de acesso da nutricionista inválida." });
        }

        try
        {
            var filter = Builders<DailyNutritionGoal>.Filter.Eq(x => x.UserId, dto.PatientPhone);
            var existingGoal = await _goalsCollection.Find(filter).FirstOrDefaultAsync();

            if (existingGoal != null)
            {
                existingGoal.UpdateGoals(dto.Calories, dto.Protein, dto.Carbs, dto.Fat, dto.NutritionistId);
                existingGoal.UpdatePreferences(dto.DietaryRestrictions, dto.FavoriteFoods);

                await _goalsCollection.ReplaceOneAsync(filter, existingGoal);
            }
            else
            {
                var novaMeta = new DailyNutritionGoal(
                    userId: dto.PatientPhone,
                    targetDate: DateTime.UtcNow,
                    targetCalories: dto.Calories,
                    targetProteinG: dto.Protein,
                    targetCarbsG: dto.Carbs,
                    targetFatG: dto.Fat,
                    nutritionistId: dto.NutritionistId,
                    dietaryRestrictions: dto.DietaryRestrictions,
                    favoriteFoods: dto.FavoriteFoods,
                    prescribedMealPlan: dto.PrescribedMealPlan
                );

                await _goalsCollection.InsertOneAsync(novaMeta);
            }

            _logger.LogInformation("✅ Dieta importada diretamente para o paciente {Phone} pela nutri {Nutri}", dto.PatientPhone, dto.NutritionistId);

            return Ok(new { success = true, message = "Dieta do paciente cadastrada/atualizada com sucesso!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao importar dieta direta para o paciente {Phone}", dto.PatientPhone);
            return StatusCode(500, new { success = false, message = "Erro interno ao processar a importação da dieta." });
        }
    }
}

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