using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Infrastructure.Persistence.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace LabelWise.Infrastructure.Services;

public class NutritionFingerprintService : INutritionFingerprintService
{
    private readonly MongoDbContext _dbContext;
    private readonly ILogger<NutritionFingerprintService> _logger;
    private const double MinCompatibleConfidence = 0.85;

    public NutritionFingerprintService(MongoDbContext dbContext, ILogger<NutritionFingerprintService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public string GenerateFingerprint(EstimatedNutritionProfileDto profile)
    {
        if (profile == null)
            return string.Empty;

        int calories = Round(profile.CaloriesPer100g ?? profile.CaloriesPer100ml);
        int carbs = Round(profile.EstimatedCarbsPer100g);
        int sugar = Round(profile.EstimatedSugarPer100g);
        int addedSugar = Round(profile.EstimatedAddedSugarPer100g);
        int protein = Round(profile.EstimatedProteinPer100g);
        int fat = Round(profile.EstimatedFatPer100g);
        int satFat = Round(profile.EstimatedSaturatedFatPer100g);
        int fiber = Round(profile.EstimatedFiberPer100g);
        int sodium = Round(profile.EstimatedSodiumPer100g);
        var unit = string.IsNullOrWhiteSpace(profile.NutritionUnit)
            ? "unknown"
            : profile.NutritionUnit.Trim().ToLowerInvariant();

        return $"v2-{unit}-{calories}-{carbs}-{sugar}-{addedSugar}-{protein}-{fat}-{satFat}-{fiber}-{sodium}";
    }

    private int Round(double? value)
    {
        if (!value.HasValue) return 0;
        return (int)Math.Round(value.Value / 2.0) * 2; // agrupa por múltiplos de 2
    }

    public async Task<EstimatedNutritionProfileDto?> FindByFingerprintAsync(string fingerprint, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
            return null;

        var filter = Builders<NutritionAnalysisCacheDocument>.Filter.And(
            Builders<NutritionAnalysisCacheDocument>.Filter.Eq(x => x.Fingerprint, fingerprint),
            Builders<NutritionAnalysisCacheDocument>.Filter.Gte(x => x.ConfidenceScore, 0.7)
        );

        var doc = await _dbContext.NutritionAnalysisCache
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);

        return doc?.Response;
    }

    public async Task<EstimatedNutritionProfileDto?> FindCompatibleMoreCompleteAsync(
        EstimatedNutritionProfileDto profile,
        CancellationToken cancellationToken = default)
    {
        if (profile is null || !HasAnyMissingNutritionField(profile))
            return null;

        var filter = Builders<NutritionAnalysisCacheDocument>.Filter.And(
            Builders<NutritionAnalysisCacheDocument>.Filter.Gte(x => x.ConfidenceScore, MinCompatibleConfidence),
            Builders<NutritionAnalysisCacheDocument>.Filter.Eq(x => x.Response.NutritionUnit, profile.NutritionUnit)
        );

        var candidates = await _dbContext.NutritionAnalysisCache
            .Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Limit(100)
            .ToListAsync(cancellationToken);

        var compatible = candidates
            .Select(x => x.Response)
            .Where(x => HasCompatibleServing(profile, x))
            .Where(x => HasCompatibleKnownNutritionValues(profile, x))
            .Where(x => CountNutritionFields(x) > CountNutritionFields(profile))
            .FirstOrDefault(x => FillsAtLeastOneMissingField(profile, x));

        if (compatible != null)
        {
            _logger.LogInformation(
                "Cache nutricional compatível encontrado para completar campos ausentes. CurrentFields={CurrentFields}, CachedFields={CachedFields}, Confidence={Confidence}",
                CountNutritionFields(profile),
                CountNutritionFields(compatible),
                compatible.NutritionConfidence?.GlobalScore);
        }

        return compatible;
    }

    public async Task SaveAsync(string fingerprint, EstimatedNutritionProfileDto response, double confidenceScore, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fingerprint) || confidenceScore < 0.7)
            return;

        try
        {
            var doc = new NutritionAnalysisCacheDocument
            {
                Fingerprint = fingerprint,
                Response = response,
                ConfidenceScore = confidenceScore,
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.NutritionAnalysisCache.InsertOneAsync(doc, new InsertOneOptions(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar NutritionAnalysisCacheDocument");
        }
    }

    private static bool HasCompatibleServing(EstimatedNutritionProfileDto current, EstimatedNutritionProfileDto cached)
    {
        if (!string.Equals(current.NutritionUnit, cached.NutritionUnit, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!NullableNearlyEquals(current.ServingAmount, cached.ServingAmount, tolerance: 0.01))
            return false;

        if (!string.Equals(current.ServingUnit?.Trim(), cached.ServingUnit?.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        return string.Equals(
            NormalizeServingDescription(current.ServingDescription),
            NormalizeServingDescription(cached.ServingDescription),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool NullableNearlyEquals(double? left, double? right, double tolerance)
    {
        if (!left.HasValue && !right.HasValue)
            return true;

        if (!left.HasValue || !right.HasValue)
            return false;

        return Math.Abs(left.Value - right.Value) <= tolerance;
    }

    private static string NormalizeServingDescription(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value.Trim().ToLowerInvariant(), "\\s+", " ");

    private static bool HasCompatibleKnownNutritionValues(EstimatedNutritionProfileDto current, EstimatedNutritionProfileDto cached)
    {
        var compared = 0;

        foreach (var field in GetComparableNutritionFields(current, cached))
        {
            if (!field.Current.HasValue || !field.Cached.HasValue)
                continue;

            compared++;

            if (!NearlyEquals(field.Current.Value, field.Cached.Value, field.AbsoluteTolerance, field.RelativeTolerance))
                return false;
        }

        return compared >= 5;
    }

    private static bool FillsAtLeastOneMissingField(EstimatedNutritionProfileDto current, EstimatedNutritionProfileDto cached) =>
        GetComparableNutritionFields(current, cached).Any(x => !x.Current.HasValue && x.Cached.HasValue);

    private static bool HasAnyMissingNutritionField(EstimatedNutritionProfileDto profile) =>
        GetNutritionFieldValues(profile).Any(x => !x.HasValue);

    private static int CountNutritionFields(EstimatedNutritionProfileDto profile) =>
        GetNutritionFieldValues(profile).Count(x => x.HasValue);

    private static IEnumerable<double?> GetNutritionFieldValues(EstimatedNutritionProfileDto profile)
    {
        yield return profile.CaloriesPer100g ?? profile.CaloriesPer100ml;
        yield return profile.EstimatedCarbsPer100g;
        yield return profile.EstimatedSugarPer100g;
        yield return profile.EstimatedAddedSugarPer100g;
        yield return profile.EstimatedPolyolsPer100g;
        yield return profile.EstimatedProteinPer100g;
        yield return profile.EstimatedFatPer100g;
        yield return profile.EstimatedSaturatedFatPer100g;
        yield return profile.EstimatedTransFatPer100g;
        yield return profile.EstimatedFiberPer100g;
        yield return profile.EstimatedSodiumPer100g;
    }

    private static IEnumerable<(double? Current, double? Cached, double AbsoluteTolerance, double RelativeTolerance)> GetComparableNutritionFields(
        EstimatedNutritionProfileDto current,
        EstimatedNutritionProfileDto cached)
    {
        yield return (current.CaloriesPer100g ?? current.CaloriesPer100ml, cached.CaloriesPer100g ?? cached.CaloriesPer100ml, 2.0, 0.08);
        yield return (current.EstimatedCarbsPer100g, cached.EstimatedCarbsPer100g, 0.5, 0.12);
        yield return (current.EstimatedSugarPer100g, cached.EstimatedSugarPer100g, 0.5, 0.12);
        yield return (current.EstimatedAddedSugarPer100g, cached.EstimatedAddedSugarPer100g, 0.5, 0.12);
        yield return (current.EstimatedPolyolsPer100g, cached.EstimatedPolyolsPer100g, 0.5, 0.12);
        yield return (current.EstimatedProteinPer100g, cached.EstimatedProteinPer100g, 0.5, 0.12);
        yield return (current.EstimatedFatPer100g, cached.EstimatedFatPer100g, 0.5, 0.12);
        yield return (current.EstimatedSaturatedFatPer100g, cached.EstimatedSaturatedFatPer100g, 0.3, 0.15);
        yield return (current.EstimatedTransFatPer100g, cached.EstimatedTransFatPer100g, 0.2, 0.20);
        yield return (current.EstimatedFiberPer100g, cached.EstimatedFiberPer100g, 0.5, 0.12);
        yield return (current.EstimatedSodiumPer100g, cached.EstimatedSodiumPer100g, 8.0, 0.12);
    }

    private static bool NearlyEquals(double current, double cached, double absoluteTolerance, double relativeTolerance)
    {
        var diff = Math.Abs(current - cached);
        if (diff <= absoluteTolerance)
            return true;

        var max = Math.Max(1.0, Math.Max(Math.Abs(current), Math.Abs(cached)));
        return diff / max <= relativeTolerance;
    }
}