using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using LabelWise.Infrastructure.Persistence.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace LabelWise.Infrastructure.Repositories
{
    /// <summary>
    /// Implementação do repositório de produtos conhecidos
    /// </summary>
    public class KnownProductRepository : IKnownProductRepository
    {
        private readonly MongoDbContext _context;
        private readonly ILogger<KnownProductRepository> _logger;

        public KnownProductRepository(
            MongoDbContext context,
            ILogger<KnownProductRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<KnownProduct?> GetByBarcodeAsync(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return null;

            return await _context.KnownProducts
                .Find(Builders<KnownProduct>.Filter.Eq(kp => kp.Barcode, barcode))
                .FirstOrDefaultAsync();
        }

        public async Task<KnownProduct?> GetByIdAsync(Guid id)
        {
            return await _context.KnownProducts
                .Find(Builders<KnownProduct>.Filter.Eq(kp => kp.Id, id))
                .FirstOrDefaultAsync();
        }

        public async Task<KnownProduct?> GetByNameAndBrandAsync(string name, string brand)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(brand))
                return null;

            return await _context.KnownProducts
                .Find(
                    Builders<KnownProduct>.Filter.Regex(kp => kp.Name, CreateExactRegex(name)) &
                    Builders<KnownProduct>.Filter.Regex(kp => kp.Brand, CreateExactRegex(brand)))
                .FirstOrDefaultAsync();
        }

        public async Task<KnownProduct> AddAsync(KnownProduct product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));

            // Garantir que SearchText está atualizado
            product.UpdateSearchText();

            await _context.KnownProducts.ReplaceOneAsync(
                Builders<KnownProduct>.Filter.Eq(kp => kp.Id, product.Id),
                product,
                new ReplaceOptions { IsUpsert = true });

            _logger.LogInformation(
                "Produto conhecido adicionado: {Name} - {Brand} (ID: {Id})",
                product.Name, product.Brand, product.Id);

            return product;
        }

        public async Task UpdateAsync(KnownProduct product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));

            // Garantir que SearchText está atualizado
            product.UpdateSearchText();
            product.SetUpdated();

            await _context.KnownProducts.ReplaceOneAsync(
                Builders<KnownProduct>.Filter.Eq(kp => kp.Id, product.Id),
                product,
                new ReplaceOptions { IsUpsert = true });

            _logger.LogInformation(
                "Produto conhecido atualizado: {Name} - {Brand} (ID: {Id})",
                product.Name, product.Brand, product.Id);
        }

        public async Task DeleteAsync(Guid id)
        {
            var product = await GetByIdAsync(id);
            if (product == null)
                return;

            await _context.KnownProducts.DeleteOneAsync(Builders<KnownProduct>.Filter.Eq(kp => kp.Id, id));

            _logger.LogInformation(
                "Produto conhecido removido: {Name} - {Brand} (ID: {Id})",
                product.Name, product.Brand, product.Id);
        }

        public async Task<List<KnownProduct>> GetByCategoryAsync(string category, int limit = 50)
        {
            if (string.IsNullOrWhiteSpace(category))
                return new List<KnownProduct>();

            return await _context.KnownProducts
                .Find(Builders<KnownProduct>.Filter.Regex(kp => kp.Category, CreateExactRegex(category)))
                .SortByDescending(kp => kp.IdentificationCount)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<List<KnownProduct>> GetMostPopularAsync(int limit = 20)
        {
            return await _context.KnownProducts
                .Find(Builders<KnownProduct>.Filter.Empty)
                .SortByDescending(kp => kp.IdentificationCount)
                .ThenByDescending(kp => kp.LastIdentifiedAt)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<List<KnownProduct>> GetValidatedProductsAsync(int limit = 100)
        {
            return await _context.KnownProducts
                .Find(kp => kp.IsValidated)
                .SortBy(kp => kp.Name)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<int> GetTotalCountAsync()
        {
            return (int)await _context.KnownProducts.CountDocumentsAsync(Builders<KnownProduct>.Filter.Empty);
        }

        public async Task<bool> ExistsAsync(string name, string brand)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(brand))
                return false;

            return await _context.KnownProducts
                .Find(
                    Builders<KnownProduct>.Filter.Regex(kp => kp.Name, CreateExactRegex(name)) &
                    Builders<KnownProduct>.Filter.Regex(kp => kp.Brand, CreateExactRegex(brand)))
                .AnyAsync();
        }

        private static MongoDB.Bson.BsonRegularExpression CreateExactRegex(string value)
        {
            return new MongoDB.Bson.BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(value.Trim())}$", "i");
        }
    }
}
