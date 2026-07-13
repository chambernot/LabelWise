using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Enums;
using LabelWise.Infrastructure.Persistence.Mongo;
using MongoDB.Driver;

namespace LabelWise.Infrastructure.Repositories
{
    public class AnalysisHistoryRepository : IAnalysisHistoryRepository
    {
        private static readonly Regex ServingSizeRegex = new(@"(?<value>\d+(?:[\.,]\d+)?)\s*(?<unit>kg|g|ml|l)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly MongoDbContext _context;

        public AnalysisHistoryRepository(MongoDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<ProductComparisonAnalysisInputDto?> GetByIdAsync(Guid analysisId, Guid? userId = null)
        {
            var filter = Builders<ProductAnalysis>.Filter.Eq(x => x.Id, analysisId);

            if (userId.HasValue)
            {
                filter &= Builders<ProductAnalysis>.Filter.Eq(x => x.UserId, userId.Value);
            }

            var analysis = await _context.Analyses
                .Find(filter)
                .FirstOrDefaultAsync();

            if (analysis == null)
            {
                return null;
            }

            var metadata = DeserializeSnapshot(analysis.Product?.Label?.ExtractedData);

            return new ProductComparisonAnalysisInputDto
            {
                AnalysisId = analysis.Id.ToString(),
                ProductName = analysis.Product?.Name,
                Brand = analysis.Product?.Brand,
                Category = metadata?.Category,
                AnalysisMode = metadata?.AnalysisMode
                    ?? (HasStructuredNutritionData(analysis.Product?.NutritionalInfo)
                        ? AnalysisMode.FullNutritionLabel
                        : AnalysisMode.FrontOfPackageOnly),
                VisibleClaims = metadata?.VisibleClaims ?? new List<string>(),
                Score = metadata?.Score,
                ScoreLabel = metadata?.ScoreLabel,
                PrincipalOffender = metadata?.PrincipalOffender,
                Classification = metadata?.Classification,
                EstimatedNutritionProfile = BuildEstimatedNutritionProfile(analysis.Product?.NutritionalInfo),
                ConfidenceDetails = metadata?.ConfidenceDetails,
                Summary = metadata?.Summary ?? analysis.Summary
            };
        }

        private static PersistedNutritionAnalysisSnapshot? DeserializeSnapshot(string? extractedData)
        {
            if (string.IsNullOrWhiteSpace(extractedData))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<PersistedNutritionAnalysisSnapshot>(extractedData);
            }
            catch
            {
                return null;
            }
        }

        private static bool HasStructuredNutritionData(NutritionalInfo? nutritionalInfo)
        {
            if (nutritionalInfo == null)
            {
                return false;
            }

            return nutritionalInfo.Calories.HasValue
                || nutritionalInfo.SugarsGrams.HasValue
                || nutritionalInfo.ProteinGrams.HasValue
                || nutritionalInfo.SodiumMg.HasValue
                || nutritionalInfo.DietaryFiberGrams.HasValue
                || nutritionalInfo.TotalFatGrams.HasValue;
        }

        private static EstimatedNutritionProfileDto? BuildEstimatedNutritionProfile(NutritionalInfo? nutritionalInfo)
        {
            if (!HasStructuredNutritionData(nutritionalInfo))
            {
                return null;
            }

            var multiplier = ResolvePer100gMultiplier(nutritionalInfo?.ServingSize);

            return new EstimatedNutritionProfileDto
            {
                CaloriesPer100g = Scale(nutritionalInfo?.Calories, multiplier),
                EstimatedSugarPer100g = Scale(nutritionalInfo?.SugarsGrams, multiplier),
                EstimatedProteinPer100g = Scale(nutritionalInfo?.ProteinGrams, multiplier),
                EstimatedSodiumPer100g = Scale(nutritionalInfo?.SodiumMg, multiplier),
                EstimatedFiberPer100g = Scale(nutritionalInfo?.DietaryFiberGrams, multiplier),
                EstimatedFatPer100g = Scale(nutritionalInfo?.TotalFatGrams, multiplier),
                EstimatedPackageCalories = ResolveEstimatedPackageCalories(nutritionalInfo, multiplier),
                Basis = string.IsNullOrWhiteSpace(nutritionalInfo?.ServingSize)
                    ? "Valores convertidos a partir da porção disponível no histórico, sem tamanho de porção normalizado."
                    : $"Valores convertidos para 100g com base na porção informada no histórico ({nutritionalInfo!.ServingSize})."
            };
        }

        private static double? ResolveEstimatedPackageCalories(NutritionalInfo? nutritionalInfo, double multiplier)
        {
            if (nutritionalInfo?.Calories == null)
            {
                return null;
            }

            if (nutritionalInfo.ServingsPerContainer.HasValue)
            {
                return Math.Round((double)(nutritionalInfo.Calories.Value * nutritionalInfo.ServingsPerContainer.Value), 1);
            }

            var per100gCalories = Scale(nutritionalInfo.Calories, multiplier);
            return per100gCalories;
        }

        private static double ResolvePer100gMultiplier(string? servingSize)
        {
            if (string.IsNullOrWhiteSpace(servingSize))
            {
                return 1d;
            }

            var match = ServingSizeRegex.Match(servingSize);
            if (!match.Success)
            {
                return 1d;
            }

            if (!double.TryParse(
                match.Groups["value"].Value.Replace(',', '.'),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var value) || value <= 0)
            {
                return 1d;
            }

            var unit = match.Groups["unit"].Value.ToLowerInvariant();
            var grams = unit switch
            {
                "kg" or "l" => value * 1000d,
                _ => value
            };

            return grams <= 0 ? 1d : 100d / grams;
        }

        private static double? Scale(decimal? value, double multiplier)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return Math.Round((double)value.Value * multiplier, 1);
        }

        private sealed class PersistedNutritionAnalysisSnapshot
        {
            public string? Category { get; set; }
            public AnalysisMode AnalysisMode { get; set; }
            public int? Score { get; set; }
            public string? ScoreLabel { get; set; }
            public string? PrincipalOffender { get; set; }
            public ProductClassificationDto? Classification { get; set; }
            public ConfidenceDetailsDto? ConfidenceDetails { get; set; }
            public string? Summary { get; set; }
            public List<string> VisibleClaims { get; set; } = new();
        }
    }
}
