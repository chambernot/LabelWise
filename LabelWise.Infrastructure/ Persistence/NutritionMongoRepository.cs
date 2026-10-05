using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Interfaces.Persistence;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Entities.Nutrition;
using MongoDB.Bson;
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
        private readonly IMongoCollection<ChatMessageLog> _chatHistory; // 👈 Declarado

        private readonly IMongoCollection<BsonDocument> _trialUsers; // 👈 Coleção de utilizadores Trial B2C
        public async Task<PatientDto?> ObterPacientePorIdAsync(string patientId)
        {
            return await _patients.Find(x => x.Id == patientId).FirstOrDefaultAsync();
        }
        public NutritionRepository(IMongoDatabase database)
        {
            _mealLogs = database.GetCollection<MealLog>("Nutrition_MealLogs");
            _dailyGoals = database.GetCollection<DailyNutritionGoal>("DailyGoals");
            _patients = database.GetCollection<PatientDto>("Nutrition_Patients");
            _pendingClarifications = database.GetCollection<MealClarificationContext>("Nutrition_PendingClarifications");

            // 🚀 CORRIGIDO: Inicialização ativada para evitar o NullReferenceException
            _chatHistory = database.GetCollection<ChatMessageLog>("WhatsAppChatHistory");
            _trialUsers = database.GetCollection<BsonDocument>("B2C_Trial_Users"); // 👈 Inicialização da coleção trial
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
            // 1. Telefones com metas cadastradas (B2B / principais) usando string explícita
            using var cursorGoals = await _dailyGoals.DistinctAsync<string>(
                "UserId",
                Builders<DailyNutritionGoal>.Filter.Empty);

            var telefonesGoals = await cursorGoals.ToListAsync();

            // 2. Telefones dos utilizadores em trial ativo (dentro do prazo de 15 dias)
            var limiteTrial = DateTime.UtcNow.AddDays(-15);
            var filterTrial = Builders<BsonDocument>.Filter.Gte("TrialStartDate", limiteTrial);
            var trialDocs = await _trialUsers.Find(filterTrial).ToListAsync();

            var telefonesTrial = trialDocs
                .Select(doc => doc.Contains("_id") ? doc["_id"].AsString : string.Empty)
                .ToList();

            // 3. Combina as duas listas, remove nulos/vazios e elimina duplicados
            var telefonesAtivos = telefonesGoals
                .Concat(telefonesTrial)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();

            return telefonesAtivos;
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

        public async Task<int> CalcularOfensivaStreakAsync(string userId)
        {
            try
            {
                var thirtyDaysAgoUtc = DateTime.UtcNow.AddDays(-30);
                var logs = await _mealLogs
                    .Find(x => x.UserId == userId && x.LoggedAt >= thirtyDaysAgoUtc)
                    .ToListAsync();

                if (!logs.Any()) return 0;

                var loggedDays = logs
                    .Select(x => x.LoggedAt.AddHours(-3).Date)
                    .Distinct()
                    .OrderByDescending(d => d)
                    .ToList();

                var todayBrazil = DateTime.UtcNow.AddHours(-3).Date;
                int streak = 0;
                var checkDate = todayBrazil;

                if (!loggedDays.Contains(checkDate))
                {
                    checkDate = todayBrazil.AddDays(-1);
                    if (!loggedDays.Contains(checkDate))
                    {
                        return 0;
                    }
                }

                while (loggedDays.Contains(checkDate))
                {
                    streak++;
                    checkDate = checkDate.AddDays(-1);
                }

                return streak;
            }
            catch
            {
                return 0;
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

            var metaExata = await _dailyGoals
                .Find(x => x.UserId == userId && x.TargetDate == targetDate)
                .FirstOrDefaultAsync();

            if (metaExata != null) return metaExata;

            return await _dailyGoals
                .Find(x => x.UserId == userId)
                .SortByDescending(x => x.TargetDate)
                .FirstOrDefaultAsync();
        }

        public async Task SalvarMensagemHistoricoAsync(string userId, string role, string content)
        {
            var msg = new ChatMessageLog
            {
                UserId = userId,
                Role = role,
                Content = content,
                Timestamp = DateTime.UtcNow
            };
            await _chatHistory.InsertOneAsync(msg);
        }

        public async Task<List<ChatMessageLog>> ObterUltimasMensagensAsync(string userId, int limite = 6)
        {
            var corteTempo = DateTime.UtcNow.AddHours(-2);

            return await _chatHistory
                .Find(x => x.UserId == userId && x.Timestamp >= corteTempo)
                .SortBy(x => x.Timestamp)
                .Limit(limite)
                .ToListAsync();
        }
    }
}