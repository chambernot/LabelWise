using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LabelWise.Application.DTOs.KnownProducts;
using LabelWise.Application.Interfaces;
using LabelWise.Infrastructure.Persistence.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LabelWise.Infrastructure.Services
{
    /// <summary>
    /// Implementação de busca de produtos conhecidos usando MongoDB.
    /// 
    /// ESTRATÉGIA DE BUSCA (PRIORIDADE):
    /// 1. Barcode exato → Score 1.0
    /// 2. Nome + Marca exatos → Score 0.95
    /// 3. Busca textual no campo SearchText → Score baseado em cobertura dos termos
    /// 4. Busca flexível por nome/marca/keywords → Score baseado em similaridade heurística
    /// 5. Busca por prefixo → Score baseado em posição
    /// 
    /// CARACTERÍSTICAS:
    /// - Scores normalizados (0.0 a 1.0)
    /// - DTOs independentes de tecnologia
    /// - Índices Mongo preparados no startup
    /// </summary>
    public class MongoKnownProductSearchService : IKnownProductSearchService
    {
        private readonly MongoDbContext _context;
        private readonly IKnownProductRepository _repository;
        private readonly ILogger<MongoKnownProductSearchService> _logger;

        private const double BarcodeMatchWeight = 1.0;
        private const double ExactNameMatchWeight = 0.95;
        private const double FullTextMatchWeight = 0.80;
        private const double FuzzyMatchWeight = 0.60;
        private const double PartialMatchWeight = 0.50;
        private const double PopularityBoost = 0.05;

        public MongoKnownProductSearchService(
            MongoDbContext context,
            IKnownProductRepository repository,
            ILogger<MongoKnownProductSearchService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<KnownProductSearchResponse> SearchAsync(KnownProductSearchRequest request)
        {
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation(
                "🔍 Buscando produtos conhecidos: Query='{Query}', Barcode='{Barcode}', MaxResults={Max}",
                request.SearchQuery, request.Barcode ?? "N/A", request.MaxResults);

            try
            {
                var results = new List<KnownProductSearchResult>();

                if (!string.IsNullOrWhiteSpace(request.Barcode))
                {
                    var barcodeResult = await SearchByBarcodeAsync(request.Barcode);
                    if (barcodeResult != null)
                    {
                        results.Add(barcodeResult);

                        _logger.LogInformation("✅ Match por barcode encontrado: {Name}", barcodeResult.Name);

                        stopwatch.Stop();
                        return new KnownProductSearchResponse
                        {
                            Success = true,
                            Results = results,
                            TotalMatches = 1,
                            SearchTimeSeconds = stopwatch.Elapsed.TotalSeconds,
                            OriginalQuery = request.SearchQuery
                        };
                    }
                }

                if (!string.IsNullOrWhiteSpace(request.SearchQuery))
                {
                    var textResults = await PerformTextSearchAsync(request);
                    results.AddRange(textResults);
                }

                if (request.MinConfidence > 0)
                {
                    results = results
                        .Where(r => r.RelevanceScore >= request.MinConfidence)
                        .ToList();
                }

                results = results
                    .OrderByDescending(r => r.RelevanceScore)
                    .ThenByDescending(r => r.IdentificationCount)
                    .Take(request.MaxResults)
                    .ToList();

                stopwatch.Stop();

                _logger.LogInformation(
                    "✅ Busca concluída: {Count} resultados em {Time:F3}s",
                    results.Count, stopwatch.Elapsed.TotalSeconds);

                return new KnownProductSearchResponse
                {
                    Success = true,
                    Results = results,
                    TotalMatches = results.Count,
                    SearchTimeSeconds = stopwatch.Elapsed.TotalSeconds,
                    OriginalQuery = request.SearchQuery
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(ex, "❌ Erro durante busca de produtos conhecidos no MongoDB");

                return new KnownProductSearchResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    SearchTimeSeconds = stopwatch.Elapsed.TotalSeconds,
                    OriginalQuery = request.SearchQuery
                };
            }
        }

        public async Task<KnownProductSearchResult?> SearchByBarcodeAsync(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
            {
                return null;
            }

            var product = await _repository.GetByBarcodeAsync(barcode);
            if (product == null)
            {
                return null;
            }

            return new KnownProductSearchResult
            {
                ProductId = product.Id,
                Name = product.Name,
                Brand = product.Brand,
                Category = product.Category,
                Barcode = product.Barcode,
                Keywords = product.Keywords,
                IsValidated = product.IsValidated,
                IdentificationCount = product.IdentificationCount,
                RelevanceScore = BarcodeMatchWeight,
                MatchReason = "Match exato por código de barras",
                MatchSource = KnownProductMatchSource.Barcode
            };
        }

        public async Task<KnownProductSearchResponse> SuggestAsync(string partialText, int maxResults = 5)
        {
            if (string.IsNullOrWhiteSpace(partialText))
            {
                return new KnownProductSearchResponse
                {
                    Success = true,
                    Results = new List<KnownProductSearchResult>(),
                    TotalMatches = 0,
                    OriginalQuery = partialText
                };
            }

            return await SearchAsync(new KnownProductSearchRequest
            {
                SearchQuery = partialText,
                MaxResults = maxResults,
                EnableFuzzySearch = true,
                MinConfidence = 0.3
            });
        }

        public async Task ReindexAllAsync()
        {
            _logger.LogInformation("🔄 Iniciando reindexação de todos os produtos conhecidos no MongoDB");

            var products = await _context.KnownProducts
                .Find(Builders<Domain.Entities.KnownProduct>.Filter.Empty)
                .ToListAsync();

            foreach (var product in products)
            {
                product.UpdateSearchText();
            }

            if (products.Count > 0)
            {
                var writes = products.Select(product =>
                    new ReplaceOneModel<Domain.Entities.KnownProduct>(
                        Builders<Domain.Entities.KnownProduct>.Filter.Eq(x => x.Id, product.Id),
                        product)
                    {
                        IsUpsert = true
                    });

                await _context.KnownProducts.BulkWriteAsync(writes);
            }

            _logger.LogInformation("✅ Reindexação concluída: {Count} produtos atualizados", products.Count);
        }

        private async Task<List<KnownProductSearchResult>> PerformTextSearchAsync(KnownProductSearchRequest request)
        {
            var results = new List<KnownProductSearchResult>();
            var searchTerm = request.SearchQuery.ToLowerInvariant().Trim();

            results.AddRange(await SearchExactNameAsync(searchTerm, request));
            results.AddRange(await SearchFullTextAsync(searchTerm, request));

            if (request.EnableFuzzySearch)
            {
                results.AddRange(await SearchFuzzyAsync(searchTerm, request));
            }

            results.AddRange(await SearchPartialAsync(searchTerm, request));

            return results
                .GroupBy(r => r.ProductId)
                .Select(g => g.OrderByDescending(r => r.RelevanceScore).First())
                .ToList();
        }

        private async Task<List<KnownProductSearchResult>> SearchExactNameAsync(string searchTerm, KnownProductSearchRequest request)
        {
            var filter = BuildBaseFilter(request) &
                         Builders<Domain.Entities.KnownProduct>.Filter.Or(
                             RegexFilter(nameof(Domain.Entities.KnownProduct.Name), searchTerm),
                             RegexFilter(nameof(Domain.Entities.KnownProduct.Brand), searchTerm),
                             RegexFilter(nameof(Domain.Entities.KnownProduct.SearchText), searchTerm));

            var products = await _context.KnownProducts
                .Find(filter)
                .Limit(request.MaxResults)
                .ToListAsync();

            return products.Select(MapExactMatch(searchTerm)).ToList();
        }

        private async Task<List<KnownProductSearchResult>> SearchFullTextAsync(string searchTerm, KnownProductSearchRequest request)
        {
            var searchWords = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var filter = BuildBaseFilter(request);

            foreach (var word in searchWords)
            {
                filter &= RegexFilter(nameof(Domain.Entities.KnownProduct.SearchText), word);
            }

            var products = await _context.KnownProducts
                .Find(filter)
                .SortByDescending(kp => kp.IdentificationCount)
                .Limit(request.MaxResults * 2)
                .ToListAsync();

            return products.Select(p => new KnownProductSearchResult
            {
                ProductId = p.Id,
                Name = p.Name,
                Brand = p.Brand,
                Category = p.Category,
                Barcode = p.Barcode,
                Keywords = p.Keywords,
                IsValidated = p.IsValidated,
                IdentificationCount = p.IdentificationCount,
                RelevanceScore = CalculateFullTextScore(p, searchWords),
                MatchReason = $"Match textual: {string.Join(", ", searchWords)}",
                MatchSource = KnownProductMatchSource.FullTextSearch
            }).ToList();
        }

        private async Task<List<KnownProductSearchResult>> SearchFuzzyAsync(string searchTerm, KnownProductSearchRequest request)
        {
            var filter = BuildBaseFilter(request) &
                         Builders<Domain.Entities.KnownProduct>.Filter.Or(
                             RegexFilter(nameof(Domain.Entities.KnownProduct.Name), searchTerm),
                             RegexFilter(nameof(Domain.Entities.KnownProduct.Brand), searchTerm),
                             RegexFilter(nameof(Domain.Entities.KnownProduct.Keywords), searchTerm));

            var products = await _context.KnownProducts
                .Find(filter)
                .Limit(request.MaxResults)
                .ToListAsync();

            return products.Select(p => new KnownProductSearchResult
            {
                ProductId = p.Id,
                Name = p.Name,
                Brand = p.Brand,
                Category = p.Category,
                Barcode = p.Barcode,
                Keywords = p.Keywords,
                IsValidated = p.IsValidated,
                IdentificationCount = p.IdentificationCount,
                RelevanceScore = FuzzyMatchWeight + CalculatePopularityBoost(p),
                MatchReason = $"Match flexível: '{searchTerm}'",
                MatchSource = KnownProductMatchSource.FuzzySearch
            }).ToList();
        }

        private async Task<List<KnownProductSearchResult>> SearchPartialAsync(string searchTerm, KnownProductSearchRequest request)
        {
            var prefixRegex = new BsonRegularExpression($"^{Regex.Escape(searchTerm)}", "i");
            var filter = BuildBaseFilter(request) &
                         Builders<Domain.Entities.KnownProduct>.Filter.Or(
                             Builders<Domain.Entities.KnownProduct>.Filter.Regex(x => x.Name, prefixRegex),
                             Builders<Domain.Entities.KnownProduct>.Filter.Regex(x => x.Brand, prefixRegex));

            var products = await _context.KnownProducts
                .Find(filter)
                .Limit(request.MaxResults)
                .ToListAsync();

            return products.Select(p => new KnownProductSearchResult
            {
                ProductId = p.Id,
                Name = p.Name,
                Brand = p.Brand,
                Category = p.Category,
                Barcode = p.Barcode,
                Keywords = p.Keywords,
                IsValidated = p.IsValidated,
                IdentificationCount = p.IdentificationCount,
                RelevanceScore = PartialMatchWeight + CalculatePopularityBoost(p),
                MatchReason = $"Match parcial (prefixo): '{searchTerm}'",
                MatchSource = KnownProductMatchSource.Keywords
            }).ToList();
        }

        private static Func<Domain.Entities.KnownProduct, KnownProductSearchResult> MapExactMatch(string searchTerm)
        {
            return p => new KnownProductSearchResult
            {
                ProductId = p.Id,
                Name = p.Name,
                Brand = p.Brand,
                Category = p.Category,
                Barcode = p.Barcode,
                Keywords = p.Keywords,
                IsValidated = p.IsValidated,
                IdentificationCount = p.IdentificationCount,
                RelevanceScore = CalculateExactMatchScore(p, searchTerm),
                MatchReason = $"Match em nome/marca: '{searchTerm}'",
                MatchSource = KnownProductMatchSource.ExactName
            };
        }

        private static double CalculateExactMatchScore(Domain.Entities.KnownProduct product, string searchTerm)
        {
            var nameLower = product.Name.ToLowerInvariant();
            var brandLower = product.Brand.ToLowerInvariant();

            if (nameLower == searchTerm || brandLower == searchTerm)
            {
                return ExactNameMatchWeight;
            }

            if (nameLower.StartsWith(searchTerm, StringComparison.Ordinal) || brandLower.StartsWith(searchTerm, StringComparison.Ordinal))
            {
                return ExactNameMatchWeight * 0.9;
            }

            if (nameLower.Contains(searchTerm, StringComparison.Ordinal) || brandLower.Contains(searchTerm, StringComparison.Ordinal))
            {
                return ExactNameMatchWeight * 0.8;
            }

            return ExactNameMatchWeight * 0.7 + CalculatePopularityBoost(product);
        }

        private static double CalculateFullTextScore(Domain.Entities.KnownProduct product, string[] searchWords)
        {
            var searchTextLower = product.SearchText.ToLowerInvariant();
            var matchCount = searchWords.Count(word => searchTextLower.Contains(word.ToLowerInvariant(), StringComparison.Ordinal));
            var matchRatio = searchWords.Length == 0 ? 0 : (double)matchCount / searchWords.Length;

            return FullTextMatchWeight * matchRatio + CalculatePopularityBoost(product);
        }

        private static double CalculatePopularityBoost(Domain.Entities.KnownProduct product)
        {
            if (product.IdentificationCount == 0)
            {
                return 0;
            }

            var normalizedCount = Math.Min(product.IdentificationCount, 1000);
            return PopularityBoost * Math.Log10(normalizedCount + 1) / 3.0;
        }

        private static FilterDefinition<Domain.Entities.KnownProduct> BuildBaseFilter(KnownProductSearchRequest request)
        {
            var filter = Builders<Domain.Entities.KnownProduct>.Filter.Empty;

            if (request.ValidatedOnly)
            {
                filter &= Builders<Domain.Entities.KnownProduct>.Filter.Eq(kp => kp.IsValidated, true);
            }

            if (!string.IsNullOrWhiteSpace(request.Category))
            {
                filter &= Builders<Domain.Entities.KnownProduct>.Filter.Regex(
                    x => x.Category,
                    new BsonRegularExpression($"^{Regex.Escape(request.Category.Trim())}$", "i"));
            }

            return filter;
        }

        private static FilterDefinition<Domain.Entities.KnownProduct> RegexFilter(string fieldName, string term)
        {
            return Builders<Domain.Entities.KnownProduct>.Filter.Regex(
                fieldName,
                new BsonRegularExpression(Regex.Escape(term), "i"));
        }
    }
}
