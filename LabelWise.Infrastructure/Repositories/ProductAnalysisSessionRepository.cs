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
    public class ProductAnalysisSessionRepository : IProductAnalysisSessionRepository
    {
        private readonly MongoDbContext _context;

        public ProductAnalysisSessionRepository(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<ProductAnalysisSession?> GetByIdAsync(
            Guid id, 
            CancellationToken cancellationToken = default)
        {
            return await _context.AnalysisSessions
                .Find(Builders<ProductAnalysisSession>.Filter.Eq(s => s.Id, id))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<ProductAnalysisSession?> GetByIdWithCapturesAsync(
            Guid id, 
            CancellationToken cancellationToken = default)
        {
            var session = await GetByIdAsync(id, cancellationToken);
            if (session == null)
            {
                return null;
            }

            var captures = await _context.ProductCaptures
                .Find(Builders<ProductCapture>.Filter.Eq(x => x.SessionId, id))
                .SortBy(x => x.CapturedAt)
                .ToListAsync(cancellationToken);

            session.LoadCaptures(captures);
            return session;
        }

        public async Task<IReadOnlyList<ProductAnalysisSession>> GetByUserIdAsync(
            Guid userId, 
            int skip = 0, 
            int take = 20, 
            CancellationToken cancellationToken = default)
        {
            return await _context.AnalysisSessions
                .Find(Builders<ProductAnalysisSession>.Filter.Eq(s => s.UserId, userId))
                .SortByDescending(s => s.StartedAt)
                .Skip(skip)
                .Limit(take)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<ProductAnalysisSession>> GetByProductIdAsync(
            Guid productId, 
            CancellationToken cancellationToken = default)
        {
            return await _context.AnalysisSessions
                .Find(Builders<ProductAnalysisSession>.Filter.Eq(s => s.ProductId, productId))
                .SortByDescending(s => s.StartedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<ProductAnalysisSession>> GetActiveSessionsAsync(
            Guid? userId = null, 
            CancellationToken cancellationToken = default)
        {
            var activeStatuses = new[] { SessionStatus.Started, SessionStatus.Capturing, SessionStatus.Processing };
            var filter = Builders<ProductAnalysisSession>.Filter.In(s => s.Status, activeStatuses);

            if (userId.HasValue)
            {
                filter &= Builders<ProductAnalysisSession>.Filter.Eq(s => s.UserId, userId.Value);
            }

            return await _context.AnalysisSessions
                .Find(filter)
                .SortByDescending(s => s.StartedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<ProductAnalysisSession?> GetLatestByUserIdAsync(
            Guid userId, 
            CancellationToken cancellationToken = default)
        {
            return await _context.AnalysisSessions
                .Find(Builders<ProductAnalysisSession>.Filter.Eq(s => s.UserId, userId))
                .SortByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task AddAsync(
            ProductAnalysisSession session, 
            CancellationToken cancellationToken = default)
        {
            await _context.AnalysisSessions.ReplaceOneAsync(
                Builders<ProductAnalysisSession>.Filter.Eq(x => x.Id, session.Id),
                session,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);
        }

        public async Task UpdateAsync(
            ProductAnalysisSession session, 
            CancellationToken cancellationToken = default)
        {
            await _context.AnalysisSessions.ReplaceOneAsync(
                Builders<ProductAnalysisSession>.Filter.Eq(x => x.Id, session.Id),
                session,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var session = await GetByIdAsync(id, cancellationToken);
            if (session is not null)
            {
                await _context.AnalysisSessions.DeleteOneAsync(
                    Builders<ProductAnalysisSession>.Filter.Eq(x => x.Id, id),
                    cancellationToken);
            }
        }
    }
}
