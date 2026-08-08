using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Text;

namespace LabelWise.Infrastructure.Repositories
{
    public class LeadRepository : ILeadRepository
    {
        private readonly IMongoCollection<Lead> _collection;

        public LeadRepository(IMongoDatabase database)
        {
            _collection = database.GetCollection<Lead>("Leads");
        }

        public async Task InserirAsync(Lead lead)
        {
            await _collection.InsertOneAsync(lead);
        }

        public async Task<bool> ExisteEmailAsync(string email)
        {
            return await _collection.Find(x => x.Email == email)
                                    .AnyAsync();
        }
    }
}
