using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Threading.Tasks;

namespace LabelWise.Api.Controllers
{
    // Registra cada mensagem recebida no WhatsApp, para o painel gerencial (admin.html)
    // saber a última interação e em quantos dias o usuário conversou com o assistente.
    // - UserActivity: um documento por telefone (primeira/última mensagem e total)
    // - UserActivityDays: um documento por telefone por dia (horário de Brasília)
    public static class UserActivityTracker
    {
        public const string ColecaoAtividade = "UserActivity";
        public const string ColecaoAtividadeDias = "UserActivityDays";

        public static async Task RegistrarMensagemRecebidaAsync(IMongoDatabase database, string phone, string messageType)
        {
            var agora = DateTime.UtcNow;
            var dia = agora.AddHours(-3).ToString("yyyy-MM-dd");

            await database.GetCollection<BsonDocument>(ColecaoAtividade).UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", phone),
                Builders<BsonDocument>.Update
                    .SetOnInsert("FirstSeenAt", agora)
                    .Set("LastInboundAt", agora)
                    .Set("LastMessageType", messageType)
                    .Inc("InboundCount", 1),
                new UpdateOptions { IsUpsert = true });

            await database.GetCollection<BsonDocument>(ColecaoAtividadeDias).UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", $"{phone}_{dia}"),
                Builders<BsonDocument>.Update
                    .SetOnInsert("UserId", phone)
                    .SetOnInsert("Day", dia)
                    .Set("LastAt", agora)
                    .Inc("Count", 1),
                new UpdateOptions { IsUpsert = true });
        }
    }
}
