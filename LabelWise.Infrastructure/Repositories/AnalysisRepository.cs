using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using LabelWise.Infrastructure.Persistence.Mongo;
using MongoDB.Driver;

namespace LabelWise.Infrastructure.Repositories
{
    public class AnalysisRepository : IAnalysisRepository
    {
        private readonly MongoDbContext _context;

        public AnalysisRepository(MongoDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<ProductAnalysis?> GetByIdAsync(Guid analysisId)
        {
            return await _context.Analyses
                .Find(Builders<ProductAnalysis>.Filter.Eq(x => x.Id, analysisId))
                .FirstOrDefaultAsync();
        }

        public async Task<IReadOnlyCollection<ProductAnalysis>> GetByDeviceIdAsync(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return Array.Empty<ProductAnalysis>();
            }

            return await _context.Analyses
                .Find(Builders<ProductAnalysis>.Filter.Eq(x => x.DeviceId, deviceId.Trim()))
                .SortByDescending(x => x.AnalyzedAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyCollection<ProductAnalysis>> GetByUserIdAsync(Guid userId)
        {
            return await _context.Analyses
                .Find(Builders<ProductAnalysis>.Filter.Eq(x => x.UserId, userId))
                .SortByDescending(x => x.AnalyzedAt)
                .ToListAsync();
        }
    }
}
