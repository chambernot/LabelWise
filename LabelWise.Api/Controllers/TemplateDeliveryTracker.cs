using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Threading.Tasks;

namespace LabelWise.Api.Controllers
{
    // Acompanha a entrega dos templates enviados pelo painel gerencial (coleção AdminMessageLog).
    // A Meta só confirma que aceitou o pedido na hora do envio; o andamento (sent → delivered → read, ou failed)
    // chega depois pelo webhook do WhatsApp, no campo "statuses".
    public static class TemplateDeliveryTracker
    {
        public const string ColecaoEnvios = "AdminMessageLog";

        public const string StatusAceito = "accepted";
        public const string StatusFalhou = "failed";

        // Ordem do andamento: um aviso atrasado (ex.: "sent" depois de "read") nunca faz o status voltar
        public static int Rank(string status) => status switch
        {
            "accepted" => 0,
            "sent" => 1,
            "delivered" => 2,
            "read" => 3,
            "failed" => 4,
            _ => -1
        };

        public static async Task RegistrarStatusAsync(
            IMongoDatabase database, string messageId, string status, DateTime quando, string? errorCode, string? errorMessage)
        {
            int rank = Rank(status);
            if (rank < 0 || string.IsNullOrWhiteSpace(messageId)) return;

            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("MessageId", messageId),
                Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.Exists("DeliveryStatusRank", false),
                    Builders<BsonDocument>.Filter.Lt("DeliveryStatusRank", rank)));

            var update = Builders<BsonDocument>.Update
                .Set("DeliveryStatus", status)
                .Set("DeliveryStatusRank", rank)
                .Set("DeliveryUpdatedAt", quando)
                .Set($"StatusTimes.{status}", quando);

            if (status == StatusFalhou)
            {
                update = update
                    .Set<BsonDocument, BsonValue>("ErrorCode", errorCode != null ? new BsonString(errorCode) : BsonNull.Value)
                    .Set<BsonDocument, BsonValue>("ErrorMessage", errorMessage != null ? new BsonString(errorMessage) : BsonNull.Value);
            }

            // Mensagens que não saíram do painel (respostas do bot, crons) simplesmente não casam com o filtro
            await database.GetCollection<BsonDocument>(ColecaoEnvios).UpdateOneAsync(filter, update);
        }
    }
}
