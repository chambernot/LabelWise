using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using LabelWise.Infrastructure.Persistence.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace LabelWise.Infrastructure.Repositories;

/// <summary>
/// Repositório para acesso aos perfis nutricionais por categoria.
/// </summary>
public class CategoryNutritionProfileRepository : ICategoryNutritionProfileRepository
{
    private readonly MongoDbContext _context;
    private readonly ILogger<CategoryNutritionProfileRepository> _logger;

    public CategoryNutritionProfileRepository(
        MongoDbContext context,
        ILogger<CategoryNutritionProfileRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CategoryNutritionProfile?> GetByCategoryCodeAsync(string categoryCode)
    {
        try
        {
            var document = await _context.CategoryNutritionProfiles
                .Find(Builders<CategoryNutritionProfileDocument>.Filter.Where(p => p.CategoryCode == categoryCode && p.IsActive))
                .FirstOrDefaultAsync();

            return document?.ToDomain();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching nutrition profile for category: {CategoryCode}", categoryCode);
            return null;
        }
    }

    public async Task<List<CategoryNutritionProfile>> GetAllActiveAsync()
    {
        try
        {
            var documents = await _context.CategoryNutritionProfiles
                .Find(Builders<CategoryNutritionProfileDocument>.Filter.Eq(p => p.IsActive, true))
                .ToListAsync();

            return documents.Select(x => x.ToDomain()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all active nutrition profiles");
            return new List<CategoryNutritionProfile>();
        }
    }

    public async Task<List<CategoryNutritionProfile>> GetByCategoryCodesAsync(IEnumerable<string> categoryCodes)
    {
        try
        {
            var codes = categoryCodes?.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();
            if (codes.Length == 0)
            {
                return new List<CategoryNutritionProfile>();
            }

            var documents = await _context.CategoryNutritionProfiles
                .Find(Builders<CategoryNutritionProfileDocument>.Filter.Where(p => codes.Contains(p.CategoryCode) && p.IsActive))
                .ToListAsync();

            return documents.Select(x => x.ToDomain()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching nutrition profiles for multiple categories");
            return new List<CategoryNutritionProfile>();
        }
    }
}
