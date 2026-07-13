using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using LabelWise.Infrastructure.Persistence.Mongo;
using MongoDB.Driver;

namespace LabelWise.Infrastructure.Repositories
{
    public class ValidatedProductRepository : IValidatedProductRepository
    {
        private readonly MongoDbContext _context;

        public ValidatedProductRepository(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<ValidatedProduct?> GetByIdAsync(
            Guid id, 
            CancellationToken cancellationToken = default)
        {
            return await _context.ValidatedProducts
                .Find(Builders<ValidatedProduct>.Filter.Eq(v => v.Id, id))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<ValidatedProduct?> GetByProductIdAsync(
            Guid productId, 
            CancellationToken cancellationToken = default)
        {
            return await _context.ValidatedProducts
                .Find(Builders<ValidatedProduct>.Filter.Eq(v => v.ProductId, productId))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<ValidatedProduct?> GetByBarcodeAsync(
            string barcode, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return null;

            return await _context.ValidatedProducts
                .Find(Builders<ValidatedProduct>.Filter.Eq(v => v.ValidatedBarcode, barcode))
                .SortByDescending(v => v.ValidationConfidence)
                .ThenByDescending(v => v.ValidationLevel)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<ValidatedProduct>> GetByValidationLevelAsync(
            ValidationLevel level, 
            int skip = 0, 
            int take = 20, 
            CancellationToken cancellationToken = default)
        {
            return await _context.ValidatedProducts
                .Find(Builders<ValidatedProduct>.Filter.Eq(v => v.ValidationLevel, level))
                .SortByDescending(v => v.LastValidatedAt)
                .Skip(skip)
                .Limit(take)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<ValidatedProduct>> GetMostReusedAsync(
            int top = 10, 
            CancellationToken cancellationToken = default)
        {
            return await _context.ValidatedProducts
                .Find(Builders<ValidatedProduct>.Filter.Empty)
                .SortByDescending(v => v.ReuseCount)
                .Limit(top)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> ExistsByBarcodeAsync(
            string barcode, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return false;

            return await _context.ValidatedProducts
                .Find(Builders<ValidatedProduct>.Filter.Eq(v => v.ValidatedBarcode, barcode))
                .AnyAsync(cancellationToken);
        }

        public async Task<bool> ExistsByProductIdAsync(
            Guid productId, 
            CancellationToken cancellationToken = default)
        {
            return await _context.ValidatedProducts
                .Find(Builders<ValidatedProduct>.Filter.Eq(v => v.ProductId, productId))
                .AnyAsync(cancellationToken);
        }

        public async Task AddAsync(
            ValidatedProduct validatedProduct, 
            CancellationToken cancellationToken = default)
        {
            await _context.ValidatedProducts.ReplaceOneAsync(
                Builders<ValidatedProduct>.Filter.Eq(v => v.Id, validatedProduct.Id),
                validatedProduct,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);
        }

        public async Task<IReadOnlyList<ValidatedProduct>> GetByUserIdAsync(
            int userId,
            int skip = 0,
            int take = 20,
            CancellationToken cancellationToken = default)
        {
            return await _context.ValidatedProducts
                .Find(Builders<ValidatedProduct>.Filter.Eq(v => v.ValidatedByUserId, userId))
                .SortByDescending(v => v.LastValidatedAt)
                .ThenByDescending(v => v.ReuseCount)
                .Skip(skip)
                .Limit(take)
                .ToListAsync(cancellationToken);
        }

        public async Task UpdateAsync(
            ValidatedProduct validatedProduct, 
            CancellationToken cancellationToken = default)
        {
            await _context.ValidatedProducts.ReplaceOneAsync(
                Builders<ValidatedProduct>.Filter.Eq(v => v.Id, validatedProduct.Id),
                validatedProduct,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var validatedProduct = await GetByIdAsync(id, cancellationToken);
            if (validatedProduct is not null)
            {
                await _context.ValidatedProducts.DeleteOneAsync(
                    Builders<ValidatedProduct>.Filter.Eq(v => v.Id, id),
                    cancellationToken);
            }
        }
    }
}
