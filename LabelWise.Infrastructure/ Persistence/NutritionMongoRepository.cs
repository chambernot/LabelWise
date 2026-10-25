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

            // 2. Telefones dos utilizadores em trial ativo (com conversão segura de _id)
            var limiteTrial = DateTime.UtcNow.AddDays(-15);
            var filterTrial = Builders<BsonDocument>.Filter.Gte("TrialStartDate", limiteTrial);
            var trialDocs = await _trialUsers.Find(filterTrial).ToListAsync();

            var telefonesTrial = new List<string>();
            foreach (var doc in trialDocs)
            {
                if (doc.Contains("_id"))
                {
                    var idElement = doc["_id"];
                    // Garante a leitura correta independentemente de estar gravado como string ou ObjectId
                    string phone = idElement.IsString ? idElement.AsString : idElement.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(phone))
                    {
                        telefonesTrial.Add(phone);
                    }
                }
            }

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

        // Um dia conta como "no foco" quando tem refeição registrada e o saldo líquido
        // (ingerido - gasto em treinos) fica entre 70% e 110% da meta calórica do dia.
        // O piso de 70% é o mesmo usado pelo PatientAdherenceBackgroundService.
        private const double FaixaMinimaDoPlano = 0.70;
        private const double FaixaMaximaDoPlano = 1.10;
        private const int JanelaOfensivaDias = 60;

        public async Task<int> CalcularOfensivaStreakAsync(string userId)
        {
            try
            {
                var inicioJanelaUtc = DateTime.UtcNow.AddDays(-JanelaOfensivaDias);
                var logs = await _mealLogs
                    .Find(x => x.UserId == userId && x.LoggedAt >= inicioJanelaUtc)
                    .ToListAsync();

                if (!logs.Any()) return 0;

                var metas = await _dailyGoals
                    .Find(x => x.UserId == userId)
                    .SortByDescending(x => x.TargetDate)
                    .ToListAsync();

                var diasNoPlano = logs
                    .GroupBy(x => x.LoggedAt.AddHours(-3).Date)
                    .Where(dia => DiaDentroDoPlano(dia, ObterMetaCaloricaDoDia(metas, dia.Key)))
                    .Select(dia => dia.Key)
                    .ToHashSet();

                var todayBrazil = DateTime.UtcNow.AddHours(-3).Date;
                int streak = 0;
                var checkDate = todayBrazil;

                // O dia de hoje ainda está em andamento: se ainda não fechou no plano, a sequência conta a partir de ontem
                if (!diasNoPlano.Contains(checkDate))
                {
                    checkDate = todayBrazil.AddDays(-1);
                    if (!diasNoPlano.Contains(checkDate))
                    {
                        return 0;
                    }
                }

                while (diasNoPlano.Contains(checkDate))
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

        private static bool DiaDentroDoPlano(IEnumerable<MealLog> logsDoDia, int metaCalorica)
        {
            var refeicoes = logsDoDia.Where(x => !x.IsTreino && x.Calories > 0).ToList();
            if (!refeicoes.Any() || metaCalorica <= 0) return false;

            int ingerido = refeicoes.Sum(x => x.Calories);
            int gastoTreinos = logsDoDia.Where(x => x.Calories < 0).Sum(x => Math.Abs(x.Calories));
            int saldoLiquido = ingerido - gastoTreinos;

            return saldoLiquido >= metaCalorica * FaixaMinimaDoPlano &&
                   saldoLiquido <= metaCalorica * FaixaMaximaDoPlano;
        }

        // Mesma regra do ObterMetaDiariaAsync: meta exata do dia, senão a mais recente
        private static int ObterMetaCaloricaDoDia(List<DailyNutritionGoal> metasOrdenadasDesc, DateTime dia)
        {
            var meta = metasOrdenadasDesc.FirstOrDefault(x => x.TargetDate.Date == dia.Date)
                       ?? metasOrdenadasDesc.FirstOrDefault();
            return meta?.TargetCalories ?? 2000;
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