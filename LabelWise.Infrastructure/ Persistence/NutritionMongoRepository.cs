using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Interfaces.Persistence;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Entities.Nutrition;
using MongoDB.Driver;
using System;
using System.Threading.Tasks;

namespace LabelWise.Infrastructure.Repositories
{
    public class NutritionRepository : INutritionRepository
    {
        private readonly IMongoCollection<MealClarificationContext> _pendingClarifications;
        private readonly IMongoCollection<PatientDto> _patients;
        private readonly IMongoCollection<MealLog> _mealLogs;
        private readonly IMongoCollection<DailyNutritionGoal> _dailyGoals;

        public async Task InserirPacienteAsync(PatientDto paciente)
        {
            var filter = Builders<PatientDto>.Filter.Eq(x => x.Id, paciente.Id);
            await _patients.ReplaceOneAsync(filter, paciente, new ReplaceOptions { IsUpsert = true });
        }

        public async Task<List<PatientDto>> ObterPacientesPorProfissionalAsync(string professionalId)
        {
            return await _patients.Find(x => x.ProfessionalId == professionalId).ToListAsync();
        }

        public NutritionRepository(IMongoDatabase database)
        {
            _mealLogs = database.GetCollection<MealLog>("Nutrition_MealLogs");
            _dailyGoals = database.GetCollection<DailyNutritionGoal>("Nutrition_DailyGoals");
            _patients = database.GetCollection<PatientDto>("Nutrition_Patients");
            _pendingClarifications = database.GetCollection<MealClarificationContext>("Nutrition_PendingClarifications");
        }

        
// Métodos a serem adicionados:
        public async Task SalvarClarificacaoPendenteAsync(MealClarificationContext context)
        {
            var filter = Builders<MealClarificationContext>.Filter.Eq(x => x.UserId, context.UserId);
            await _pendingClarifications.ReplaceOneAsync(filter, context, new ReplaceOptions { IsUpsert = true });
        }

        public async Task<MealClarificationContext?> ObterClarificacaoPendenteAsync(string userId)
        {
            // Expira automaticamente dúvidas com mais de 15 minutos
            var limiteTempo = DateTime.UtcNow.AddMinutes(-15);

            return await _pendingClarifications
                .Find(x => x.UserId == userId && x.CreatedAt >= limiteTempo)
                .FirstOrDefaultAsync();
        }

        public async Task RemoverClarificacaoPendenteAsync(string userId)
        {
            await _pendingClarifications.DeleteManyAsync(x => x.UserId == userId);
        }

        public async Task ExcluirMealLogAsync(string id)
        {
            var filter = Builders<MealLog>.Filter.Eq(x => x.Id, id);
            await _mealLogs.DeleteOneAsync(filter);
        }

        public async Task SalvarMetaDiariaAsync(DailyNutritionGoal goal)
        {
            var filter = Builders<DailyNutritionGoal>.Filter.And(
                Builders<DailyNutritionGoal>.Filter.Eq(x => x.UserId, goal.UserId),
                Builders<DailyNutritionGoal>.Filter.Eq(x => x.TargetDate, goal.TargetDate)
            );

            // Substitui o documento se já existir para aquele dia ou insere um novo
            await _dailyGoals.ReplaceOneAsync(filter, goal, new ReplaceOptions { IsUpsert = true });
        }

        public async Task<List<MealLog>> ObterRefeicoesDoDiaAsync(string userId, DateTime data)
        {
            try
            {
                var inicioDia = data.Date;
                var fimDia = inicioDia.AddDays(1).AddTicks(-1);

                return await _mealLogs.Find(x => x.UserId == userId && x.LoggedAt >= inicioDia && x.LoggedAt <= fimDia)
                                      .ToListAsync();
            }
            catch (Exception ex)
            {
                // Se você já tiver o ILogger injetado no seu NutritionRepository, descomente a linha abaixo:
                // _logger.LogError(ex, "[NutritionRepository] ❌ Erro ao buscar refeições do dia para o usuário {UserId}", userId);

                // Retorna uma lista vazia para que o sistema não quebre o fluxo do WhatsApp 
                // e considere temporariamente 0 calorias extras consumidas no dia.
                return new List<MealLog>();
            }
        }

        public async Task InserirMealLogAsync(MealLog mealLog)
        {
            // Padrão simplificado, sem passagem de tokens, idêntico ao seu InserirAsync
            await _mealLogs.InsertOneAsync(mealLog);
        }

        public async Task<DailyNutritionGoal> ObterMetaDiariaAsync(string userId, DateTime data)
        {
            var targetDate = data.Date;

            // Expressão LINQ direta, seguindo a mesma sintaxe do seu ExisteEmailAsync
            return await _dailyGoals.Find(x => x.UserId == userId && x.TargetDate == targetDate)
                                    .FirstOrDefaultAsync();
        }
    }
}