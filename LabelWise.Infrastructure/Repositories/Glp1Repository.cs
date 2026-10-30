using LabelWise.Application.Interfaces.Persistence;
using LabelWise.Application.Services.Glp1;
using LabelWise.Domain.Entities.Glp1;
using LabelWise.Domain.Entities.Nutrition;
using LabelWise.Infrastructure.Persistence.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LabelWise.Infrastructure.Repositories
{
    public class Glp1Repository : IGlp1Repository
    {
        private readonly IMongoCollection<Glp1Profile> _profiles;
        private readonly IMongoCollection<Glp1SymptomLog> _symptomLogs;
        private readonly IMongoCollection<Glp1Alert> _alerts;
        private readonly IMongoCollection<MealLog> _mealLogs;
        private readonly IMongoCollection<BsonDocument> _waterLogs;
        private readonly IMongoCollection<BsonDocument> _weightLogs;
        private readonly IMongoCollection<BsonDocument> _patients; // só leitura

        public Glp1Repository(IMongoDatabase database)
        {
            _weightLogs = database.GetCollection<BsonDocument>("WeightLogs");
            _patients = database.GetCollection<BsonDocument>("Nutrition_Patients");
            _profiles = database.GetCollection<Glp1Profile>(MongoCollectionNames.Glp1Profiles);
            _symptomLogs = database.GetCollection<Glp1SymptomLog>(MongoCollectionNames.Glp1SymptomLogs);
            _alerts = database.GetCollection<Glp1Alert>(MongoCollectionNames.Glp1Alerts);
            _mealLogs = database.GetCollection<MealLog>("Nutrition_MealLogs");
            _waterLogs = database.GetCollection<BsonDocument>("WaterLogs");
            _reminderLogs = database.GetCollection<BsonDocument>(MongoCollectionNames.Glp1ReminderLogs);
            _userActivity = database.GetCollection<BsonDocument>("UserActivity");
        }

        private readonly IMongoCollection<BsonDocument> _reminderLogs;
        private readonly IMongoCollection<BsonDocument> _userActivity; // só leitura (gravada pelo UserActivityTracker)

        // ---------------------------------------------------------------- lembretes

        public async Task<List<Glp1Profile>> ListarPerfisAtivosAsync()
        {
            return await _profiles.Find(x => x.Enabled && x.ActivationStep == null).ToListAsync();
        }

        public async Task<Glp1Alert?> ObterUltimoAlertaAsync(string userId, Glp1AlertType tipo)
        {
            return await _alerts.Find(x => x.UserId == userId && x.Type == tipo)
                .SortByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();
        }

        private static string IdDoLembrete(string userId, string tipo, string diaBr) => $"{userId}_{tipo}_{diaBr}";

        public async Task<bool> LembreteJaEnviadoAsync(string userId, string tipo, string diaBr)
        {
            return await _reminderLogs.Find(Builders<BsonDocument>.Filter.Eq("_id", IdDoLembrete(userId, tipo, diaBr))).AnyAsync();
        }

        public async Task<bool> RegistrarEnvioLembreteAsync(string userId, string tipo, string diaBr, DateTime agoraUtc)
        {
            try
            {
                // _id único: duas execuções no mesmo dia não conseguem reservar o mesmo envio
                await _reminderLogs.InsertOneAsync(new BsonDocument
                {
                    { "_id", IdDoLembrete(userId, tipo, diaBr) },
                    { "UserId", userId },
                    { "Type", tipo },
                    { "Day", diaBr },
                    { "SentAt", agoraUtc }
                });
                return true;
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }

        public async Task<DateTime?> ObterUltimaMensagemRecebidaAsync(string userId)
        {
            var doc = await _userActivity.Find(Builders<BsonDocument>.Filter.Eq("_id", userId)).FirstOrDefaultAsync();
            return doc != null && doc.TryGetValue("LastInboundAt", out var v) && v.IsBsonDateTime ? v.ToUniversalTime() : null;
        }

        // ---------------------------------------------------------------- portal da nutricionista

        public async Task<List<Glp1Profile>> ObterPerfisAsync(IEnumerable<string> userIds)
        {
            var ids = userIds.Distinct().ToList();
            return ids.Count == 0 ? new List<Glp1Profile>() : await _profiles.Find(x => ids.Contains(x.Id)).ToListAsync();
        }

        public async Task<HashSet<string>> ObterTelefonesComAlertaNaoVistoAsync(IEnumerable<string> userIds)
        {
            var ids = userIds.Distinct().ToList();
            if (ids.Count == 0) return new HashSet<string>();

            var alertas = await _alerts.Find(x => ids.Contains(x.UserId) && x.SeenByNutritionistAt == null).ToListAsync();
            return alertas.Select(x => x.UserId).ToHashSet();
        }

        public async Task<List<Glp1Alert>> ObterAlertasParaPortalAsync(string userId, DateTime desdeUtc)
        {
            return await _alerts.Find(x => x.UserId == userId && (x.CreatedAt >= desdeUtc || x.SeenByNutritionistAt == null))
                .SortByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> MarcarAlertaVistoAsync(string userId, string alertId, DateTime agoraUtc)
        {
            var resultado = await _alerts.UpdateOneAsync(
                x => x.Id == alertId && x.UserId == userId,
                Builders<Glp1Alert>.Update.Set(x => x.SeenByNutritionistAt, agoraUtc));
            return resultado.MatchedCount > 0;
        }

        public async Task<List<Glp1MealEntry>> ObterRefeicoesDesdeAsync(string userId, DateTime desdeUtc)
        {
            var refeicoes = await _mealLogs.Find(x => x.UserId == userId && x.LoggedAt >= desdeUtc).ToListAsync();
            return refeicoes.Select(x => new Glp1MealEntry(x.LoggedAt, x.Calories, x.ProteinG, x.IsTreino)).ToList();
        }

        public async Task<List<Glp1WaterEntry>> ObterAguaDesdeAsync(string userId, DateTime desdeUtc)
        {
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("UserId", userId),
                Builders<BsonDocument>.Filter.Gte("LoggedAt", desdeUtc));

            return (await _waterLogs.Find(filter).ToListAsync())
                .Where(d => d.Contains("LoggedAt") && d["LoggedAt"].IsBsonDateTime)
                .Select(d => new Glp1WaterEntry(
                    d["LoggedAt"].ToUniversalTime(),
                    d.Contains("AmountMl") && d["AmountMl"].IsNumeric ? d["AmountMl"].ToInt32() : 0))
                .ToList();
        }

        public async Task<List<Glp1WeightEntry>> ObterPesosDesdeAsync(string userId, DateTime desdeUtc)
        {
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("UserId", userId),
                Builders<BsonDocument>.Filter.Gte("LoggedAt", desdeUtc));

            return (await _weightLogs.Find(filter).ToListAsync())
                .Where(d => d.Contains("LoggedAt") && d["LoggedAt"].IsBsonDateTime && d.Contains("WeightKg") && d["WeightKg"].IsNumeric)
                .Select(d => new Glp1WeightEntry(d["LoggedAt"].ToUniversalTime(), (decimal)d["WeightKg"].ToDouble()))
                .OrderBy(x => x.LoggedAt)
                .ToList();
        }

        public async Task<Glp1Profile?> ObterPerfilAsync(string userId)
        {
            return await _profiles.Find(x => x.Id == userId).FirstOrDefaultAsync();
        }

        public async Task SalvarPerfilAsync(Glp1Profile perfil)
        {
            await _profiles.ReplaceOneAsync(x => x.Id == perfil.Id, perfil, new ReplaceOptions { IsUpsert = true });
        }

        public async Task ApagarPerfilAsync(string userId)
        {
            await _profiles.DeleteOneAsync(x => x.Id == userId);
        }

        public async Task RegistrarPesoAsync(string userId, decimal pesoKg, DateTime agoraUtc)
        {
            var inicioDiaUtc = DateTime.SpecifyKind(agoraUtc.AddHours(-3).Date.AddHours(3), DateTimeKind.Utc);
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("UserId", userId),
                Builders<BsonDocument>.Filter.Gte("LoggedAt", inicioDiaUtc),
                Builders<BsonDocument>.Filter.Lt("LoggedAt", inicioDiaUtc.AddDays(1)));

            var existente = await _weightLogs.Find(filter).FirstOrDefaultAsync();
            var doc = new BsonDocument
            {
                { "_id", existente != null ? existente["_id"] : Guid.NewGuid().ToString() },
                { "UserId", userId },
                { "WeightKg", (double)pesoKg },
                { "LoggedAt", agoraUtc }
            };

            await _weightLogs.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]), doc, new ReplaceOptions { IsUpsert = true });
        }

        public async Task<int> ContarSintomasAsync(string userId)
        {
            return (int)await _symptomLogs.CountDocumentsAsync(x => x.UserId == userId);
        }

        public async Task InserirSintomasAsync(IEnumerable<Glp1SymptomLog> sintomas)
        {
            var lista = sintomas.ToList();
            if (lista.Count > 0)
            {
                await _symptomLogs.InsertManyAsync(lista);
            }
        }

        public async Task<List<Glp1SymptomLog>> ObterSintomasDesdeAsync(string userId, DateTime desdeUtc)
        {
            return await _symptomLogs
                .Find(x => x.UserId == userId && x.LoggedAt >= desdeUtc)
                .SortByDescending(x => x.LoggedAt)
                .ToListAsync();
        }

        public async Task InserirAlertaAsync(Glp1Alert alerta)
        {
            await _alerts.InsertOneAsync(alerta);
        }

        public async Task<(string? Restricoes, string? Aversoes)> ObterRestricoesEAversoesAsync(string userId)
        {
            var doc = await _patients.Find(Builders<BsonDocument>.Filter.Eq("_id", userId)).FirstOrDefaultAsync();
            if (doc == null) return (null, null);

            static string? Ler(BsonDocument d, string campo) =>
                d.TryGetValue(campo, out var v) && v.IsString ? v.AsString : null;

            return (Ler(doc, "MedicalRestrictions"), Ler(doc, "FoodAversions"));
        }

        public async Task<int> ContarAlertasAsync(string userId)
        {
            return (int)await _alerts.CountDocumentsAsync(x => x.UserId == userId);
        }

        public async Task<decimal> ObterProteinaDoDiaAsync(string userId, DateTime inicioDiaUtc)
        {
            return (await ObterConsumoDoDiaAsync(userId, inicioDiaUtc)).ProteinG;
        }

        public async Task<Glp1DayIntake> ObterConsumoDoDiaAsync(string userId, DateTime inicioDiaUtc)
        {
            var fimDiaUtc = inicioDiaUtc.AddDays(1);
            var refeicoes = (await _mealLogs
                .Find(x => x.UserId == userId && x.LoggedAt >= inicioDiaUtc && x.LoggedAt < fimDiaUtc)
                .ToListAsync())
                .Where(x => !x.IsTreino)
                .ToList();

            return new Glp1DayIntake(refeicoes.Sum(x => x.ProteinG), refeicoes.Sum(x => Math.Max(0, x.Calories)), refeicoes.Count);
        }

        // Mesma leitura do WhatsAppController.ObterAguaDoDiaAsync
        public async Task<int> ObterAguaDoDiaAsync(string userId, DateTime inicioDiaUtc)
        {
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("UserId", userId),
                Builders<BsonDocument>.Filter.Gte("LoggedAt", inicioDiaUtc),
                Builders<BsonDocument>.Filter.Lt("LoggedAt", inicioDiaUtc.AddDays(1)));

            var registros = await _waterLogs.Find(filter).ToListAsync();
            return registros.Sum(w => w.Contains("AmountMl") && w["AmountMl"].IsNumeric ? w["AmountMl"].ToInt32() : 0);
        }

        public async Task ApagarDadosAsync(string userId)
        {
            await _profiles.DeleteOneAsync(x => x.Id == userId);
            await _symptomLogs.DeleteManyAsync(x => x.UserId == userId);
            await _alerts.DeleteManyAsync(x => x.UserId == userId);
        }
    }
}
