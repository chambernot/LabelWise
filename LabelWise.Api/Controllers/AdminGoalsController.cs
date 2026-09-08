using LabelWise.Domain.Entities.Nutrition;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

[ApiController]
[Route("api/admin/goals")]
public class AdminGoalsController : ControllerBase
{
    private readonly IMongoCollection<DailyNutritionGoal> _goalsCollection;
    private readonly IConfiguration _configuration;

    public AdminGoalsController(IMongoDatabase database, IConfiguration configuration)
    {
        _goalsCollection = database.GetCollection<DailyNutritionGoal>("DailyGoals");
        _configuration = configuration;
    }

    [HttpPost("set-target")]
    public async Task<IActionResult> SetUserTarget(
        [FromHeader(Name = "X-Admin-Secret")] string secret,
        [FromBody] UpdateUserTargetDto dto)
    {
        // Validação de segurança para o painel da nutricionista
        if (secret != _configuration["AdminSecret"])
            return Unauthorized(new { message = "Acesso negado." });

        // Filtro pelo telefone do usuário
        var filter = Builders<DailyNutritionGoal>.Filter.Eq(x => x.UserId, dto.Phone);

        var update = Builders<DailyNutritionGoal>.Update
    .Set(x => x.TargetCalories, dto.Calories)
    .Set("TargetProteinG", dto.Protein) // Usando string ou garantindo a propriedade exata
    .Set("TargetCarbsG", dto.Carbs)
    .Set("TargetFatG", dto.Fat)
    .Set("UpdatedAt", DateTime.UtcNow);

        // Atualiza ou insere caso não exista
        await _goalsCollection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });

        return Ok(new { success = true, message = $"Metas atualizadas com sucesso para o paciente {dto.Phone}!" });
    }
}

public record UpdateUserTargetDto(
    string Phone,
    double Calories,
    double Protein,
    double Carbs,
    double Fat
);