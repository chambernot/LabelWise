using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LabelWise.Infrastructure.Repositories
{
    public class EmpresaRepository : IEmpresaRepository
    {
        private readonly IMongoCollection<Empresa> _collection;

        public EmpresaRepository(IMongoDatabase database)
        {
            _collection = database.GetCollection<Empresa>("Empresas");
        }

        public async Task InserirAsync(Empresa empresa)
        {
            await _collection.InsertOneAsync(empresa);
        }

        public async Task<IEnumerable<Empresa>> ObterPorGuestIdAsync(string guestId)
        {
            return await _collection.Find(x => x.GuestId == guestId).ToListAsync();
        }

        public async Task<bool> AtualizarAsync(string id, Empresa empresa)
        {
            // Garante que o ID da entidade corresponde ao ID da rota
            empresa.Id = id;

            // Filtra pelo ID do registro no MongoDB
            var filter = Builders<Empresa>.Filter.Eq(x => x.Id, id);

            // Substitui o documento existente pelos novos dados enviados
            var result = await _collection.ReplaceOneAsync(filter, empresa);

            return result.ModifiedCount > 0 || result.MatchedCount > 0;
        }
    }
}