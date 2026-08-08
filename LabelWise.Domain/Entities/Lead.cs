using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace LabelWise.Domain.Entities
{
    public class Lead
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = default!;

        public string Nome { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Telefone { get; set; } = string.Empty;

        // Novo: Identificador único do navegador para vincular ações anônimas
        public string GuestId { get; set; } = string.Empty;

        public DateTime DataCadastro { get; set; } = DateTime.UtcNow;

        public string Origem { get; set; } = "TributoCopilot-LandingPage";
    }
}