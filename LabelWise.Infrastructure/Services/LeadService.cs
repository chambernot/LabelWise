using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using MongoDB.Driver;
using System;
using System.Threading.Tasks;

namespace LabelWise.Infrastructure.Services
{
    public class LeadService
    {
        private readonly ILeadRepository _repository;
        private readonly IMongoCollection<Lead> _collection; // Injetado ou acessado via repositório

        // Ajuste o construtor conforme sua injeção de dependência atual
        public LeadService(ILeadRepository repository, IMongoDatabase database)
        {
            _repository = repository;
            _collection = database.GetCollection<Lead>("Leads");
        }

        public async Task SalvarAsync(LeadRequest request)
        {
            // Verifica se já existe um lead com esse GuestId ou E-mail
            var filter = Builders<Lead>.Filter.Or(
                Builders<Lead>.Filter.Eq(x => x.Email, request.Email),
                Builders<Lead>.Filter.Eq(x => x.GuestId, request.GuestId)
            );

            var leadExistente = await _collection.Find(filter).FirstOrDefaultAsync();

            if (leadExistente != null)
            {
                // Atualiza os dados do lead anônimo com as informações reais informadas agora
                var update = Builders<Lead>.Update
                    .Set(x => x.Nome, request.Nome)
                    .Set(x => x.Email, request.Email)
                    .Set(x => x.Telefone, request.Telefone);

                await _collection.UpdateOneAsync(x => x.Id == leadExistente.Id, update);
            }
            else
            {
                // Cria um novo lead vinculado ao GuestId
                var lead = new Lead
                {
                    Nome = request.Nome,
                    Email = request.Email,
                    Telefone = request.Telefone,
                    GuestId = request.GuestId,
                    DataCadastro = DateTime.UtcNow
                };

                await _repository.InserirAsync(lead);
            }
        }
    }
}