using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Enums;
using LabelWise.Infrastructure.Persistence.Mongo;
using MongoDB.Driver;

namespace LabelWise.Infrastructure.Repositories
{
    public class ProductCaptureRepository : IProductCaptureRepository
    {
        private readonly MongoDbContext _context;

        public ProductCaptureRepository(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<ProductCapture?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.ProductCaptures
                .Find(Builders<ProductCapture>.Filter.Eq(c => c.Id, id))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<ProductCapture>> GetBySessionIdAsync(
            Guid sessionId, 
            CancellationToken cancellationToken = default)
        {
            return await _context.ProductCaptures
                .Find(Builders<ProductCapture>.Filter.Eq(c => c.SessionId, sessionId))
                .SortBy(c => c.CapturedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<ProductCapture>> GetByProductIdAsync(
            Guid productId, 
            CancellationToken cancellationToken = default)
        {
            return await _context.ProductCaptures
                .Find(Builders<ProductCapture>.Filter.Eq(c => c.ProductId, productId))
                .SortByDescending(c => c.CapturedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<ProductCapture>> GetByProductIdAndTypeAsync(
            Guid productId, 
            CaptureType captureType, 
            CancellationToken cancellationToken = default)
        {
            return await _context.ProductCaptures
                .Find(Builders<ProductCapture>.Filter.Where(c => c.ProductId == productId && c.CaptureType == captureType))
                .SortByDescending(c => c.CapturedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<ProductCapture?> GetLatestByProductIdAndTypeAsync(
            Guid productId, 
            CaptureType captureType, 
            CancellationToken cancellationToken = default)
        {
            return await _context.ProductCaptures
                .Find(Builders<ProductCapture>.Filter.Where(c => c.ProductId == productId && c.CaptureType == captureType))
                .SortByDescending(c => c.CapturedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<int> GetCaptureCountByProductIdAsync(
            Guid productId, 
            CancellationToken cancellationToken = default)
        {
            return (int)await _context.ProductCaptures
                .CountDocumentsAsync(Builders<ProductCapture>.Filter.Eq(c => c.ProductId, productId), cancellationToken: cancellationToken);
        }

        public async Task AddAsync(ProductCapture capture, CancellationToken cancellationToken = default)
        {
            await _context.ProductCaptures.ReplaceOneAsync(
                Builders<ProductCapture>.Filter.Eq(c => c.Id, capture.Id),
                capture,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);
        }

        public async Task UpdateAsync(ProductCapture capture, CancellationToken cancellationToken = default)
        {
            await _context.ProductCaptures.ReplaceOneAsync(
                Builders<ProductCapture>.Filter.Eq(c => c.Id, capture.Id),
                capture,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var capture = await GetByIdAsync(id, cancellationToken);
            if (capture is not null)
            {
                await _context.ProductCaptures.DeleteOneAsync(
                    Builders<ProductCapture>.Filter.Eq(c => c.Id, id),
                    cancellationToken);
            }
        }
    }
}
