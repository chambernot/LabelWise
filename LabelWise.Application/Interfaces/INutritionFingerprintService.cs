using System.Threading;
using System.Threading.Tasks;
using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Interfaces;

public interface INutritionFingerprintService
{
    string GenerateFingerprint(EstimatedNutritionProfileDto profile);
    Task<EstimatedNutritionProfileDto?> FindByFingerprintAsync(string fingerprint, CancellationToken cancellationToken = default);
    Task<EstimatedNutritionProfileDto?> FindCompatibleMoreCompleteAsync(EstimatedNutritionProfileDto profile, CancellationToken cancellationToken = default);
    Task SaveAsync(string fingerprint, EstimatedNutritionProfileDto response, double confidenceScore, CancellationToken cancellationToken = default);
}