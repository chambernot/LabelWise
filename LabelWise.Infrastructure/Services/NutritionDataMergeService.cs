using System;
using System.Linq;
using System.Threading.Tasks;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services;

/// <summary>
/// Serviço responsável por fazer o merge inteligente entre dados reais e fallback por categoria.
/// </summary>
public class NutritionDataMergeService : INutritionDataMergeService
{
    private readonly IDatabaseNutritionFallbackService _fallbackService;
    private readonly ILogger<NutritionDataMergeService> _logger;

    public NutritionDataMergeService(
        IDatabaseNutritionFallbackService fallbackService,
        ILogger<NutritionDataMergeService> logger)
    {
        _fallbackService = fallbackService;
        _logger = logger;
    }

    public async Task<NutritionDataMergeResult> MergeAsync(
        EstimatedNutritionProfileDto? aiExtractedData,
        string? normalizedCategoryCode,
        AnalysisMode analysisMode)
    {
        try
        {
            _logger.LogInformation(
                "[NutritionMerge] Starting merge. NormalizedCategoryCode={NormalizedCategoryCode}, AnalysisMode={AnalysisMode}, HasAiData={HasAiData}",
                normalizedCategoryCode,
                analysisMode,
                aiExtractedData != null);

            var dataQuality = AssessDataQuality(aiExtractedData, analysisMode);

            _logger.LogInformation(
                "[NutritionMerge] Data quality assessed: {DataQuality}",
                dataQuality);

            if (dataQuality == DataQuality.Complete)
            {
                var realSources = BuildRealFieldSources(aiExtractedData!);
                _logger.LogInformation(
                    "[NutritionMerge] All required fields present. Using real data without fallback. RealFields={RealFields}",
                    realSources.Count);

                return new NutritionDataMergeResult
                {
                    FinalProfile = aiExtractedData!,
                    DataSourceType = DataSourceType.Real,
                    RealFieldsCount = realSources.Count,
                    EstimatedFieldsCount = 0,
                    FallbackApplied = false,
                    CategoryCode = normalizedCategoryCode,
                    FieldSources = realSources
                };
            }

            if (string.IsNullOrWhiteSpace(normalizedCategoryCode))
            {
                var realSources = aiExtractedData == null ? [] : BuildRealFieldSources(aiExtractedData);
                _logger.LogWarning(
                    "[NutritionMerge] No normalized category code available for fallback. Returning partial data only. RealFields={RealFields}",
                    realSources.Count);

                return new NutritionDataMergeResult
                {
                    FinalProfile = aiExtractedData ?? new EstimatedNutritionProfileDto(),
                    DataSourceType = realSources.Count > 0 ? DataSourceType.Real : DataSourceType.Unknown,
                    RealFieldsCount = realSources.Count,
                    EstimatedFieldsCount = 0,
                    FallbackApplied = false,
                    FieldSources = realSources
                };
            }

            _logger.LogInformation(
                "[NutritionMerge] Requesting fallback for category code '{NormalizedCategoryCode}'",
                normalizedCategoryCode);

            var fallbackResult = await _fallbackService.ApplyFallbackAsync(
                aiExtractedData,
                normalizedCategoryCode,
                analysisMode.ToString());

            var realFields = fallbackResult.FallbackSources.Count(x => x.Value == "Real");
            var estimatedFields = fallbackResult.FallbackSources.Count(x => x.Value == "EstimatedByCategory");
            var dataSourceType = DetermineDataSourceType(realFields, estimatedFields, fallbackResult.ProfileRejected);

            // === VALIDAÇÃO DE CONSISTÊNCIA: verificar se o perfil aplicado corresponde à categoria solicitada ===
            if (fallbackResult.NormalizedCategoryCode != null &&
                !string.Equals(fallbackResult.NormalizedCategoryCode, normalizedCategoryCode, StringComparison.OrdinalIgnoreCase) &&
                !fallbackResult.UsedParentCategoryFallback)
            {
                _logger.LogError(
                    "[NutritionMerge] CONSISTENCY ERROR: Requested category '{RequestedCode}' but fallback applied profile from '{AppliedCode}'. This indicates a profile resolution mismatch.",
                    normalizedCategoryCode,
                    fallbackResult.NormalizedCategoryCode);
            }

            _logger.LogInformation(
                "[NutritionMerge] Merge completed. " +
                "RequestedCategory={RequestedCategory}, AppliedCategory={AppliedCategory}, " +
                "SourceType={SourceType}, RealFields={RealFields}, EstimatedFields={EstimatedFields}, " +
                "Rejected={Rejected}, UsedParent={UsedParent}, Confidence={Confidence}",
                normalizedCategoryCode,
                fallbackResult.NormalizedCategoryCode,
                dataSourceType,
                realFields,
                estimatedFields,
                fallbackResult.ProfileRejected,
                fallbackResult.UsedParentCategoryFallback,
                fallbackResult.Confidence);

            return new NutritionDataMergeResult
            {
                FinalProfile = fallbackResult.Profile,
                DataSourceType = dataSourceType,
                RealFieldsCount = realFields,
                EstimatedFieldsCount = estimatedFields,
                FallbackApplied = estimatedFields > 0,
                CategoryCode = fallbackResult.NormalizedCategoryCode,
                CategoryName = fallbackResult.NormalizedCategoryName,
                FallbackConfidence = fallbackResult.Confidence,
                FieldSources = fallbackResult.FallbackSources,
                Inconsistencies = fallbackResult.Inconsistencies,
                ProfileRejected = fallbackResult.ProfileRejected
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NutritionMerge] Error merging nutrition data for category: {Category}", normalizedCategoryCode);
            return new NutritionDataMergeResult
            {
                FinalProfile = aiExtractedData ?? new EstimatedNutritionProfileDto(),
                DataSourceType = DataSourceType.Unknown,
                RealFieldsCount = 0,
                EstimatedFieldsCount = 0,
                FallbackApplied = false,
                FieldSources = []
            };
        }
    }

    private static DataQuality AssessDataQuality(EstimatedNutritionProfileDto? profile, AnalysisMode mode)
    {
        if (profile == null)
        {
            return DataQuality.Missing;
        }

        if (mode == AnalysisMode.FullNutritionLabel)
        {
            var required = new[]
            {
                profile.CaloriesPer100g,
                profile.EstimatedProteinPer100g,
                profile.EstimatedFatPer100g,
                profile.EstimatedSugarPer100g,
                profile.EstimatedSodiumPer100g
            };

            return required.All(value => value.HasValue && value.Value >= 0)
                ? DataQuality.Complete
                : DataQuality.Partial;
        }

        return DataQuality.Partial;
    }

    private static DataSourceType DetermineDataSourceType(int realFields, int estimatedFields, bool profileRejected)
    {
        if (profileRejected)
        {
            return realFields > 0 ? DataSourceType.Real : DataSourceType.Unknown;
        }

        if (realFields == 0 && estimatedFields == 0)
        {
            return DataSourceType.Unknown;
        }

        if (realFields > 0 && estimatedFields == 0)
        {
            return DataSourceType.Real;
        }

        if (realFields == 0)
        {
            return DataSourceType.EstimatedByCategory;
        }

        return DataSourceType.Mixed;
    }

    private static System.Collections.Generic.Dictionary<string, string> BuildRealFieldSources(EstimatedNutritionProfileDto profile)
    {
        var sources = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (profile.CaloriesPer100g.HasValue) sources["calories"] = "Real";
        if (profile.EstimatedProteinPer100g.HasValue) sources["protein"] = "Real";
        if (profile.EstimatedFatPer100g.HasValue) sources["fat"] = "Real";
        if (profile.EstimatedSugarPer100g.HasValue) sources["sugar"] = "Real";
        if (profile.EstimatedSodiumPer100g.HasValue) sources["sodium"] = "Real";
        if (profile.EstimatedFiberPer100g.HasValue) sources["fiber"] = "Real";
        return sources;
    }
}
