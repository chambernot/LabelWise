using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace LabelWise.Api.Controllers
{
    // Tokens de acesso à página do paciente (paciente.html), enviados pelo comando "minha dieta".
    // Substituem o link antigo por telefone (?phone=), que permitia ver os dados de qualquer número.
    public static class PatientAccessTokens
    {
        public const int ValidadeDias = 30;
        private const string Colecao = "PatientAccessTokens";

        public static async Task<string> CriarAsync(IMongoDatabase database, string phone)
        {
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            var agora = DateTime.UtcNow;
            var doc = new BsonDocument
            {
                { "_id", token },
                { "Phone", phone },
                { "CreatedAt", agora },
                { "ExpiresAt", agora.AddDays(ValidadeDias) }
            };

            await database.GetCollection<BsonDocument>(Colecao).InsertOneAsync(doc);
            return token;
        }

        // Retorna o telefone dono do token, ou null se o token não existir ou estiver expirado
        public static async Task<string?> ObterTelefoneAsync(IMongoDatabase database, string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;

            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("_id", token),
                Builders<BsonDocument>.Filter.Gt("ExpiresAt", DateTime.UtcNow));

            var doc = await database.GetCollection<BsonDocument>(Colecao).Find(filter).FirstOrDefaultAsync();
            return doc != null && doc.TryGetValue("Phone", out var phone) && phone.IsString ? phone.AsString : null;
        }
    }
}
