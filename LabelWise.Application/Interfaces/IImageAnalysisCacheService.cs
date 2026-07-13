using System.Threading;
using System.Threading.Tasks;
using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Interfaces;

public interface IImageAnalysisCacheService
{
    ulong ComputePerceptualHash(byte[] imageBytes);
    IReadOnlyList<ulong> ComputePerceptualHashes(byte[] imageBytes);
    string ComputeExactHash(byte[] imageBytes);
    Task<EstimatedNutritionProfileDto?> GetByExactHashAsync(string exactHash, CancellationToken cancellationToken = default);
    Task<EstimatedNutritionProfileDto?> FindSimilarAsync(IReadOnlyCollection<ulong> perceptualHashes, string cacheVersion, CancellationToken cancellationToken = default);
    Task<bool> SaveCacheAsync(string exactHash, IReadOnlyCollection<ulong> perceptualHashes, string cacheVersion, EstimatedNutritionProfileDto response, CancellationToken cancellationToken = default);
}