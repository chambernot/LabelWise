using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Interfaces.Persistence;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Entities.Nutrition;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LabelWise.Infrastructure.Repositories
{
    public class NutritionRepository : INutritionRepository
    {
        private readonly IMongoCollection<MealClarificationContext> _pendingClarifications;
        private readonly IMongoCollection<PatientDto> _patients;
        private readonly IMongoCollection<MealLog> _mealLogs;
        private readonly IMongoCollection<DailyNutritionGoal> _dailyGoals;

        public NutritionRepository(IMongoDatabase database)
        {
            _mealLogs = database.GetCollection<MealLog>("Nutrition_MealLogs");
            _dailyGoals = database.GetCollection<DailyNutritionGoal>("DailyGoals");
            _patients = database.GetCollection<PatientDto>("Nutrition_Patients");
            _pendingClarifications = database.GetCollection<MealClarificationContext>("Nutrition_PendingClarifications");
        }

        public async Task InserirPacienteAsync(PatientDto paciente)
        {
            var filter = Builders<PatientDto>.Filter.Eq(x => x.Id, paciente.Id);
            await _patients.ReplaceOneAsync(filter, paciente, new ReplaceOptions { IsUpsert = true });
        }

        public async Task<List<PatientDto>> ObterPacientesPorProfissionalAsync(string professionalId)
        {
            return await _patients.Find(x => x.ProfessionalId == professionalId).ToListAsync();
        }

        public async Task<List<string>> ObterTelefonesAtivosAsync()
        {
            using var cursor = await _dailyGoals.DistinctAsync(
                x => x.UserId,
                MongoDB.Driver.Builders<DailyNutritionGoal>.Filter.Empty);

            var telefones = await cursor.ToListAsync();
            return telefones.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        }

        public async Task<MealClarificationContext?> ObterClarificacaoPendenteAsync(string userId)
        {
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

            await _dailyGoals.ReplaceOneAsync(filter, goal, new ReplaceOptions { IsUpsert = true });
        }

        public async Task<List<MealLog>> ObterRefeicoesDoDiaAsync(string userId, DateTime data)
        {
            try
            {
                // Conversão precisa considerando o fuso horário do Brasil (UTC-3)
                // 00:00 no Brasil = 03:00 em UTC do mesmo dia
                var inicioDiaLocal = data.Date;
                var inicioDiaUtc = DateTime.SpecifyKind(inicioDiaLocal.AddHours(3), DateTimeKind.Utc);
                var fimDiaUtc = inicioDiaUtc.AddDays(1).AddTicks(-1);

                return await _mealLogs.Find(x => x.UserId == userId && x.LoggedAt >= inicioDiaUtc && x.LoggedAt <= fimDiaUtc)
                                      .ToListAsync();
            }
            catch (Exception)
            {
                return new List<MealLog>();
            }
        }

        public async Task InserirMealLogAsync(MealLog mealLog)
        {
            await _mealLogs.InsertOneAsync(mealLog);
        }

        public async Task SalvarClarificacaoPendenteAsync(MealClarificationContext context)
        {
            await _pendingClarifications.DeleteManyAsync(x => x.UserId == context.UserId);
            await _pendingClarifications.InsertOneAsync(context);
        }

        public async Task<DailyNutritionGoal?> ObterMetaDiariaAsync(string userId, DateTime data)
        {
            var targetDate = data.Date;

            // Tenta buscar a meta específica para o dia exato
            var metaExata = await _dailyGoals
                .Find(x => x.UserId == userId && x.TargetDate == targetDate)
                .FirstOrDefaultAsync();

            if (metaExata != null) return metaExata;

            // Fallback: se não houver meta cadastrada especificamente para hoje, pega a última cadastrada
            return await _dailyGoals
                .Find(x => x.UserId == userId)
                .SortByDescending(x => x.TargetDate)
                .FirstOrDefaultAsync();
        }
    }
}