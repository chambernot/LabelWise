using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Infrastructure.Persistence.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LabelWise.Infrastructure.Services;

public class ImageAnalysisCacheService : IImageAnalysisCacheService
{
    private readonly MongoDbContext _dbContext;
    private readonly ILogger<ImageAnalysisCacheService> _logger;
    private const int MaxHammingDistance = 4;
    private const double MinUsableConfidence = 0.7;
    private const double MinVisualMatchConfidence = 0.85;

    public ImageAnalysisCacheService(MongoDbContext dbContext, ILogger<ImageAnalysisCacheService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;

        // Diagnóstico: qual DB/coleção está realmente sendo usada em runtime.
        _logger.LogInformation(
            "[Cache] Usando MongoDB → DB='{Db}', Collection='{Collection}'",
            _dbContext.ImageAnalysisCache.Database.DatabaseNamespace.DatabaseName,
            _dbContext.ImageAnalysisCache.CollectionNamespace.CollectionName);
    }

    public string ComputeExactHash(byte[] imageBytes)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(imageBytes);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    public ulong ComputePerceptualHash(byte[] imageBytes)
    {
        try
        {
            using var image = Image.Load<Rgba32>(imageBytes);
            return ComputePerceptualHash(image);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao computar pHash da imagem");
            return 0; // fallback if image load fails
        }
    }

    public IReadOnlyList<ulong> ComputePerceptualHashes(byte[] imageBytes)
    {
        try
        {
            using var image = Image.Load<Rgba32>(imageBytes);
            var hashes = new List<ulong>(4);

            AddPerceptualHashVariant(image, null, hashes);
            AddPerceptualHashVariant(image, RotateMode.Rotate90, hashes);
            AddPerceptualHashVariant(image, RotateMode.Rotate180, hashes);
            AddPerceptualHashVariant(image, RotateMode.Rotate270, hashes);

            return hashes.Where(x => x != 0).Distinct().ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao computar variantes de pHash da imagem");
            return [];
        }
    }

    public async Task<EstimatedNutritionProfileDto?> GetByExactHashAsync(string exactHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exactHash))
            return null;

        // Sempre retorna a entrada mais recente para o mesmo hash (defensivo contra
        // duplicatas históricas) e exige confidence mínima — alinhado à regra do
        // FindSimilarAsync e ao threshold usado no SaveCacheAsync.
        var filter = Builders<ImageAnalysisCacheDocument>.Filter.Eq(x => x.ImageHash, exactHash);
        var sort = Builders<ImageAnalysisCacheDocument>.Sort.Descending(x => x.CreatedAt);

        var document = await _dbContext.ImageAnalysisCache
            .Find(filter)
            .Sort(sort)
            .FirstOrDefaultAsync(cancellationToken);

        if (document == null)
            return null;

        var score = document.Response?.NutritionConfidence?.GlobalScore ?? 0d;
        if (score < MinUsableConfidence)
        {
            _logger.LogInformation(
                "Cache (hash exato) ignorado por baixa confidence: {Score}",
                score);
            return null;
        }

        return document.Response;
    }

    public async Task<EstimatedNutritionProfileDto?> FindSimilarAsync(
        IReadOnlyCollection<ulong> perceptualHashes,
        string cacheVersion,
        CancellationToken cancellationToken = default)
    {
        var hashes = perceptualHashes?.Where(x => x != 0).Distinct().ToArray() ?? [];
        if (hashes.Length == 0 || string.IsNullOrWhiteSpace(cacheVersion))
            return null;

        var filter = Builders<ImageAnalysisCacheDocument>.Filter.Eq(x => x.CacheVersion, cacheVersion);
        var sort = Builders<ImageAnalysisCacheDocument>.Sort.Descending(x => x.CreatedAt);
        var candidates = await _dbContext.ImageAnalysisCache
            .Find(filter)
            .Sort(sort)
            .Limit(200)
            .ToListAsync(cancellationToken);

        foreach (var item in candidates)
        {
            var score = item.Response?.NutritionConfidence?.GlobalScore ?? 0d;
            if (score < MinVisualMatchConfidence)
                continue;

            var candidateHashes = item.PerceptualHashes is { Count: > 0 }
                ? item.PerceptualHashes.Select(x => unchecked((ulong)x)).Where(x => x != 0).Distinct().ToArray()
                : [unchecked((ulong)item.PerceptualHash)];

            var distance = hashes
                .SelectMany(inputHash => candidateHashes.Select(candidateHash => HammingDistance(inputHash, candidateHash)))
                .DefaultIfEmpty(int.MaxValue)
                .Min();

            if (distance <= MaxHammingDistance)
            {
                _logger.LogInformation(
                    "Cache HIT visual por pHash. Distance={Distance}, Confidence={Confidence}, Version={Version}",
                    distance,
                    score,
                    cacheVersion);
                return item.Response;
            }
        }

        return null;
    }

    public async Task<bool> SaveCacheAsync(
        string exactHash,
        IReadOnlyCollection<ulong> perceptualHashes,
        string cacheVersion,
        EstimatedNutritionProfileDto response,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exactHash) || string.IsNullOrWhiteSpace(cacheVersion) || response is null)
            return false;

        // Blindagem: nunca persistir resultados com baixa confidence, mesmo que o
        // chamador esqueça de filtrar. Mantém a base de cache limpa.
        var score = response.NutritionConfidence?.GlobalScore ?? 0d;
        if (score < MinUsableConfidence)
        {
            _logger.LogInformation(
                "SaveCacheAsync ignorado por baixa confidence ({Score} < {Min}).",
                score, MinUsableConfidence);
            return false;
        }

        try
        {
            var hashes = perceptualHashes?.Where(x => x != 0).Distinct().ToArray() ?? [];
            var primaryHash = hashes.FirstOrDefault();
            var storedHashes = hashes.Select(x => unchecked((long)x)).ToList();

            var filter = Builders<ImageAnalysisCacheDocument>.Filter.Eq(x => x.ImageHash, exactHash);
            var update = Builders<ImageAnalysisCacheDocument>.Update
                .Set(x => x.ImageHash, exactHash)
                .Set(x => x.PerceptualHash, unchecked((long)primaryHash))
                .Set(x => x.PerceptualHashes, storedHashes)
                .Set(x => x.CacheVersion, cacheVersion)
                .Set(x => x.Response, response)
                .Set(x => x.CreatedAt, DateTime.UtcNow);

            var result = await _dbContext.ImageAnalysisCache.UpdateOneAsync(
                filter,
                update,
                new UpdateOptions { IsUpsert = true },
                cancellationToken);

            _logger.LogInformation(
                "Cache de imagem salvo via upsert. Hash={Hash}, PHashVariants={PHashVariants}, Version={Version}, Confidence={Score}, Matched={Matched}, Modified={Modified}, Upserted={Upserted}",
                exactHash,
                storedHashes.Count,
                cacheVersion,
                score,
                result.MatchedCount,
                result.ModifiedCount,
                result.UpsertedId?.ToString() ?? "none");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar cache de análise de imagem. Hash={Hash}, Confidence={Score}", exactHash, score);
            return false;
        }
    }

    private int HammingDistance(ulong hash1, ulong hash2)
    {
        ulong xor = hash1 ^ hash2;
        return System.Numerics.BitOperations.PopCount(xor);
    }

    private static void AddPerceptualHashVariant(Image<Rgba32> source, RotateMode? rotation, List<ulong> hashes)
    {
        using var variant = source.Clone(ctx =>
        {
            if (rotation.HasValue)
                ctx.Rotate(rotation.Value);
        });

        hashes.Add(ComputePerceptualHash(variant));
    }

    private static ulong ComputePerceptualHash(Image<Rgba32> source)
    {
        using var image = source.Clone(x => x.Resize(8, 8).Grayscale());

        Span<Rgba32> pixels = stackalloc Rgba32[64];
        image.CopyPixelDataTo(pixels);

        double total = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            total += pixels[i].R;
        }

        double avg = total / pixels.Length;
        ulong hash = 0;

        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].R >= avg)
            {
                hash |= 1UL << i;
            }
        }

        return hash;
    }
}