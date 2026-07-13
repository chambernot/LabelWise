using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using LabelWise.Infrastructure.Persistence.Mongo;
using MongoDB.Driver;

namespace LabelWise.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly MongoDbContext _context;

    public ProductRepository(MongoDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Find(Builders<Product>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return null;
        }

        return await _context.Products
            .Find(Builders<Product>.Filter.Eq(x => x.Barcode, barcode.Trim()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        return UpsertAsync(product, cancellationToken);
    }

    public Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        return UpsertAsync(product, cancellationToken);
    }

    private Task UpsertAsync(Product product, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(product);

        return _context.Products.ReplaceOneAsync(
            Builders<Product>.Filter.Eq(x => x.Id, product.Id),
            product,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }
}
