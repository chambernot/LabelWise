using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using LabelWise.Infrastructure.Persistence.Mongo;
using MongoDB.Driver;

namespace LabelWise.Infrastructure.Repositories;

public class AnalysisWriteRepository : IAnalysisWriteRepository
{
    private readonly MongoDbContext _context;

    public AnalysisWriteRepository(MongoDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task AddAsync(ProductAnalysis analysis, CancellationToken cancellationToken = default)
    {
        return UpsertAsync(analysis, cancellationToken);
    }

    public Task UpdateAsync(ProductAnalysis analysis, CancellationToken cancellationToken = default)
    {
        return UpsertAsync(analysis, cancellationToken);
    }

    private Task UpsertAsync(ProductAnalysis analysis, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        return _context.Analyses.ReplaceOneAsync(
            Builders<ProductAnalysis>.Filter.Eq(x => x.Id, analysis.Id),
            analysis,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }
}
