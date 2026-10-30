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

        public Glp1Repository(IMongoDatabase database)
        {
            _weightLogs = database.GetCollection<BsonDocument>("WeightLogs");
            _profiles = database.GetCollection<Glp1Profile>(MongoCollectionNames.Glp1Profiles);
            _symptomLogs = database.GetCollection<Glp1SymptomLog>(MongoCollectionNames.Glp1SymptomLogs);
            _alerts = database.GetCollection<Glp1Alert>(MongoCollectionNames.Glp1Alerts);
            _mealLogs = database.GetCollection<MealLog>("Nutrition_MealLogs");
            _waterLogs = database.GetCollection<BsonDocument>("WaterLogs");
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
