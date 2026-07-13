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
/// Repositório para mapeamentos de categorias (aliases).
/// </summary>
public class CategoryMappingRepository : ICategoryMappingRepository
{
    private readonly MongoDbContext _context;
    private readonly ILogger<CategoryMappingRepository> _logger;

    public CategoryMappingRepository(
        MongoDbContext context,
        ILogger<CategoryMappingRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CategoryMapping?> GetMappingAsync(string rawCategoryName)
    {
        try
        {
            var normalized = rawCategoryName.Trim().ToLowerInvariant();
            var exactRegex = new MongoDB.Bson.BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(normalized)}$", "i");
            
            var document = await _context.CategoryMappings
                .Find(Builders<CategoryMappingDocument>.Filter.Regex(m => m.RawCategoryName, exactRegex) &
                      Builders<CategoryMappingDocument>.Filter.Eq(m => m.IsActive, true))
                .SortByDescending(m => m.Confidence)
                .FirstOrDefaultAsync();

            return document?.ToDomain();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching category mapping for: {RawCategoryName}", rawCategoryName);
            return null;
        }
    }

    public async Task<List<CategoryMapping>> GetFuzzyMappingsAsync(string rawCategoryName, double minConfidence = 0.7)
    {
        try
        {
            var normalized = rawCategoryName.Trim().ToLowerInvariant();
            var exactRegex = new MongoDB.Bson.BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(normalized)}$", "i");
            var containsRegex = new MongoDB.Bson.BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(normalized), "i");
            
            // Busca exata
            var exactMatch = await _context.CategoryMappings
                .Find(Builders<CategoryMappingDocument>.Filter.Regex(m => m.RawCategoryName, exactRegex) &
                      Builders<CategoryMappingDocument>.Filter.Eq(m => m.IsActive, true) &
                      Builders<CategoryMappingDocument>.Filter.Gte(m => m.Confidence, (decimal)minConfidence))
                .ToListAsync();

            if (exactMatch.Any())
            {
                return exactMatch.Select(x => x.ToDomain()).ToList();
            }

            // Busca aproximada (contains)
            var fuzzyMatches = await _context.CategoryMappings
                .Find(Builders<CategoryMappingDocument>.Filter.Regex(m => m.RawCategoryName, containsRegex) &
                      Builders<CategoryMappingDocument>.Filter.Eq(m => m.IsActive, true) &
                      Builders<CategoryMappingDocument>.Filter.Gte(m => m.Confidence, (decimal)minConfidence))
                .SortByDescending(m => m.Confidence)
                .Limit(5)
                .ToListAsync();

            if (fuzzyMatches.Any())
            {
                return fuzzyMatches.Select(x => x.ToDomain()).ToList();
            }

            // Busca reversa (se o input contém algum mapping)
            var reverseMatches = await _context.CategoryMappings
                .Find(Builders<CategoryMappingDocument>.Filter.Regex(m => m.RawCategoryName, new MongoDB.Bson.BsonRegularExpression(".*", "i")) &
                      Builders<CategoryMappingDocument>.Filter.Eq(m => m.IsActive, true) &
                      Builders<CategoryMappingDocument>.Filter.Gte(m => m.Confidence, (decimal)minConfidence))
                .SortByDescending(m => m.Confidence)
                .Limit(5)
                .ToListAsync();

            return reverseMatches
                .Where(m => normalized.Contains(m.RawCategoryName, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.ToDomain())
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching fuzzy category mappings for: {RawCategoryName}", rawCategoryName);
            return new List<CategoryMapping>();
        }
    }

    public async Task<List<CategoryMapping>> GetAllActiveAsync()
    {
        try
        {
            var documents = await _context.CategoryMappings
                .Find(Builders<CategoryMappingDocument>.Filter.Eq(m => m.IsActive, true))
                .SortBy(m => m.RawCategoryName)
                .ToListAsync();

            return documents.Select(x => x.ToDomain()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all active category mappings");
            return new List<CategoryMapping>();
        }
    }
}
