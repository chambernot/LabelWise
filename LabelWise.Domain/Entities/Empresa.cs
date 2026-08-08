using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace LabelWise.Domain.Entities
{
    public class Empresa
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = default!;

        public string GuestId { get; set; } = string.Empty; // Vincula ao navegador/visitante anônimo

        public string RazaoSocial { get; set; } = string.Empty;

        public string NomeFantasia { get; set; } = string.Empty;

        public string Cnpj { get; set; } = string.Empty;

        public string RegimeTributario { get; set; } = string.Empty;

        public string CnaePrincipal { get; set; } = string.Empty;

        public string Cidade { get; set; } = string.Empty;

        public string Uf { get; set; } = string.Empty;

        public decimal FaturamentoAnual { get; set; }

        public string Situacao { get; set; } = string.Empty;

        public DateTime DataCadastro { get; set; } = DateTime.UtcNow;
    }
}