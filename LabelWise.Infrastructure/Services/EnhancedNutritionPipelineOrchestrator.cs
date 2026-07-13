using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LabelWise.Application.DTOs.AI;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Presentation;
using LabelWise.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services;

/// <summary>
/// Orquestrador principal do pipeline de análise nutricional com fallback inteligente.
/// </summary>
public class EnhancedNutritionPipelineOrchestrator : IEnhancedNutritionPipelineOrchestrator
{
    private readonly IVisualInterpreter _visualInterpreter;
    private readonly ICategoryNormalizationService _categoryNormalization;
    private readonly INutritionDataMergeService _dataMerge;
    private readonly IPrincipalOffenderDetector _offenderDetector;
    private readonly ILogger<EnhancedNutritionPipelineOrchestrator> _logger;

    public EnhancedNutritionPipelineOrchestrator(
        IVisualInterpreter visualInterpreter,
        ICategoryNormalizationService categoryNormalization,
        INutritionDataMergeService dataMerge,
        IPrincipalOffenderDetector offenderDetector,
        ILogger<EnhancedNutritionPipelineOrchestrator> logger)
    {
        _visualInterpreter = visualInterpreter;
        _categoryNormalization = categoryNormalization;
        _dataMerge = dataMerge;
        _offenderDetector = offenderDetector;
        _logger = logger;
    }

    public async Task<EnhancedNutritionAnalysisResult> AnalyzeAsync(byte[] imageData, string? additionalContext = null)
    {
        var stopwatch = Stopwatch.StartNew();
        string? tempFilePath = null;

        try
        {
            tempFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".jpg");
            await File.WriteAllBytesAsync(tempFilePath, imageData);

            var visionResult = await _visualInterpreter.InterpretImageAsync(new VisualInterpretationRequest
            {
                ImagePath = tempFilePath
            });

            if (!string.IsNullOrWhiteSpace(visionResult.ErrorMessage))
            {
                return CreateErrorResult(visionResult.ErrorMessage);
            }

            var analysisMode = visionResult.ProbableCaptureType == CaptureType.NutritionTable
                ? AnalysisMode.FullNutritionLabel
                : AnalysisMode.FrontOfPackageOnly;

            // === STEP 1: Normalizar categoria ===
            var categoryNormalization = await _categoryNormalization.NormalizeAsync(
                visionResult.Category ?? visionResult.ProbableCategory,
                visionResult.ProductName ?? visionResult.ProbableProductName ?? additionalContext,
                visionResult.VisibleClaims,
                visionResult.Brand ?? visionResult.ProbableBrand);

            _logger.LogInformation(
                "[NutritionPipeline] Category normalization complete. " +
                "IaCategory={IaCategory}, ProductName={ProductName}, " +
                "NormalizedCode={NormalizedCode}, NormalizedName={NormalizedName}, " +
                "Confidence={Confidence}, IsNormalized={IsNormalized}, MatchType={MatchType}, " +
                "Evidence={Evidence}, Candidates={Candidates}",
                visionResult.Category ?? visionResult.ProbableCategory,
                visionResult.ProductName ?? visionResult.ProbableProductName,
                categoryNormalization.NormalizedCategoryCode,
                categoryNormalization.NormalizedCategoryName,
                categoryNormalization.Confidence,
                categoryNormalization.IsNormalized,
                categoryNormalization.MatchType,
                string.Join("; ", categoryNormalization.Evidence ?? []),
                string.Join(", ", categoryNormalization.CandidateCategories ?? []));

            // === STEP 2: Merge dados nutricionais com fallback ===
            var mergeResult = await _dataMerge.MergeAsync(
                visionResult.EstimatedNutritionProfile,
                categoryNormalization.NormalizedCategoryCode,
                analysisMode);

            // === STEP 3: Validação de consistência pós-merge ===
            if (categoryNormalization.IsNormalized &&
                mergeResult.CategoryCode != null &&
                !string.Equals(mergeResult.CategoryCode, categoryNormalization.NormalizedCategoryCode, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError(
                    "[NutritionPipeline] POST-MERGE CONSISTENCY ERROR: " +
                    "Normalization resolved to '{NormalizedCode}' but merge applied profile from '{MergeCode}'. " +
                    "ProductName={ProductName}, IaCategory={IaCategory}. " +
                    "This is a critical data integrity issue.",
                    categoryNormalization.NormalizedCategoryCode,
                    mergeResult.CategoryCode,
                    visionResult.ProductName,
                    visionResult.Category);
            }

            // === STEP 4: Detectar ofensores e classificar ===
            var offenderResult = _offenderDetector.Detect(mergeResult.FinalProfile);
            var classification = BuildClassification(mergeResult.FinalProfile, mergeResult.DataSourceType);
            var nutritionalScore = CalculateWeightedNutritionalScore(mergeResult, offenderResult);
            var (summary, alerts) = BuildSummaryAndAlerts(mergeResult, offenderResult, nutritionalScore);

            _logger.LogInformation(
                "[NutritionPipeline] Pipeline complete. " +
                "ProductName={ProductName}, IaCategory={IaCategory}, " +
                "NormalizedCategory={NormalizedCategory}, AppliedProfileCategory={AppliedProfileCategory}, " +
                "DataSourceType={DataSourceType}, RealFields={RealFields}, EstimatedFields={EstimatedFields}, " +
                "PrincipalOffender={PrincipalOffender}, Score={Score}, " +
                "FallbackApplied={FallbackApplied}, Inconsistencies={Inconsistencies}, " +
                "Basis={Basis}",
                visionResult.ProductName,
                visionResult.Category,
                categoryNormalization.NormalizedCategoryCode,
                mergeResult.CategoryCode,
                mergeResult.DataSourceType,
                mergeResult.RealFieldsCount,
                mergeResult.EstimatedFieldsCount,
                offenderResult.PrincipalOffender?.Type.ToString() ?? "none",
                nutritionalScore,
                mergeResult.FallbackApplied,
                string.Join(" | ", mergeResult.Inconsistencies),
                mergeResult.FinalProfile?.Basis);

            stopwatch.Stop();

            var result = new EnhancedNutritionAnalysisResult
            {
                Success = true,
                ProductName = visionResult.ProductName,
                Brand = visionResult.Brand,
                Category = visionResult.Category,
                PackageWeight = visionResult.PackageWeight,
                AnalysisMode = analysisMode,
                VisibleClaims = visionResult.VisibleClaims?.ToArray(),
                EstimatedNutritionProfile = mergeResult.FinalProfile,
                Classification = classification,
                Summary = summary,
                Alerts = alerts,
                ConfidenceDetails = visionResult.ConfidenceDetails,
                ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds,
                NutritionalScore = nutritionalScore,
                DataSourceType = mergeResult.DataSourceType,
                RealFieldsCount = mergeResult.RealFieldsCount,
                EstimatedFieldsCount = mergeResult.EstimatedFieldsCount,
                FallbackApplied = mergeResult.FallbackApplied,
                NormalizedCategoryCode = mergeResult.CategoryCode,
                NormalizedCategoryName = mergeResult.CategoryName,
                CategoryNormalizationConfidence = categoryNormalization.Confidence,
                PrincipalOffender = MapPrincipalOffender(offenderResult),
                FieldSources = mergeResult.FieldSources,
                Inconsistencies = mergeResult.Inconsistencies.ToArray(),
                CategoryNormalizationEvidence = categoryNormalization.Evidence
            };

            NutritionTextPresentationBuilder.Apply(result);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error in enhanced nutrition analysis pipeline");
            return CreateErrorResult($"Analysis failed: {ex.Message}");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempFilePath) && File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                }
            }
        }
    }

    private static PrincipalOffenderDto? MapPrincipalOffender(PrincipalOffenderResult offenderResult)
    {
        return offenderResult.PrincipalOffender == null
            ? null
            : new PrincipalOffenderDto
            {
                Type = offenderResult.PrincipalOffender.Type.ToString(),
                Value = offenderResult.PrincipalOffender.Value,
                Severity = offenderResult.PrincipalOffender.Severity.ToString(),
                Score = offenderResult.PrincipalOffender.Score
            };
    }

    private static ProductClassificationDto BuildClassification(EstimatedNutritionProfileDto profile, DataSourceType sourceType)
    {
        var sugar = profile.EstimatedSugarPer100g ?? 0;
        var sodium = profile.EstimatedSodiumPer100g ?? 0;
        var calories = profile.CaloriesPer100g ?? 0;
        var protein = profile.EstimatedProteinPer100g ?? 0;
        var fiber = profile.EstimatedFiberPer100g ?? 0;
        var fat = profile.EstimatedFatPer100g ?? 0;

        return new ProductClassificationDto
        {
            Diabetic = BuildHealthProfile(
                sugar > 15 ? "nao_recomendado" : sugar > 7 ? "consumo_moderado" : "favoravel",
                sugar > 15
                    ? "Alto teor de açúcar no perfil consolidado."
                    : sugar > 7
                        ? "Teor moderado de açúcar exige atenção no consumo."
                        : "Baixo teor de açúcar no perfil consolidado."),
            BloodPressure = BuildHealthProfile(
                sodium > 600 ? "nao_recomendado" : sodium > 300 ? "consumo_moderado" : "favoravel",
                sodium > 600
                    ? "Teor alto de sódio no perfil consolidado."
                    : sodium > 300
                        ? "Sódio moderado pede atenção para consumo frequente."
                        : "Teor de sódio controlado no perfil consolidado."),
            WeightLoss = BuildHealthProfile(
                calories > 350 || sugar > 15 || fat > 17.5 ? "nao_recomendado" :
                calories > 220 || sugar > 8 || fat > 10 ? "consumo_moderado" : "favoravel",
                calories > 350 || sugar > 15 || fat > 17.5
                    ? "Alta densidade energética ou excesso de açúcar/gordura no perfil final."
                    : calories > 220 || sugar > 8 || fat > 10
                        ? "Perfil calórico moderado exige controle de porção."
                        : "Perfil mais favorável para controle energético."),
            MuscleGain = BuildHealthProfile(
                protein >= 20 ? "favoravel" : protein >= 10 ? "consumo_moderado" : "fraco",
                protein >= 20
                    ? BuildProteinReason(sourceType, "Boa densidade proteica no perfil consolidado.")
                    : protein >= 10
                        ? BuildProteinReason(sourceType, "Proteína moderada no perfil consolidado.")
                        : "Baixo teor de proteína para objetivo de ganho muscular.")
        };
    }

    private static HealthProfileResult BuildHealthProfile(string status, string reason)
        => new() { Status = status, Reason = reason };

    private static string BuildProteinReason(DataSourceType sourceType, string baseReason)
    {
        return sourceType == DataSourceType.Real
            ? baseReason
            : $"{baseReason} Interpretação deve considerar que parte dos valores pode ter sido estimada.";
    }

    private static double CalculateWeightedNutritionalScore(NutritionDataMergeResult mergeResult, PrincipalOffenderResult offenderResult)
    {
        var profile = mergeResult.FinalProfile;
        var proteinSourceWeight = IsStrongPositiveSignal("protein", mergeResult) ? 1.0 : 0.45;
        var fiberSourceWeight = IsStrongPositiveSignal("fiber", mergeResult) ? 1.0 : 0.50;

        double score = 82.0;
        score -= (profile.EstimatedSugarPer100g ?? 0) switch
        {
            > 25 => 26,
            > 15 => 18,
            > 8 => 10,
            > 4 => 4,
            _ => 0
        };

        score -= (profile.EstimatedFatPer100g ?? 0) switch
        {
            > 25 => 18,
            > 17.5 => 12,
            > 10 => 6,
            _ => 0
        };

        score -= (profile.EstimatedSodiumPer100g ?? 0) switch
        {
            > 900 => 22,
            > 600 => 15,
            > 300 => 8,
            > 150 => 3,
            _ => 0
        };

        score -= (profile.CaloriesPer100g ?? 0) switch
        {
            > 450 => 14,
            > 320 => 9,
            > 220 => 4,
            _ => 0
        };

        score += Math.Min(8, (profile.EstimatedProteinPer100g ?? 0) * 0.40) * proteinSourceWeight;
        score += Math.Min(8, (profile.EstimatedFiberPer100g ?? 0) * 1.20) * fiberSourceWeight;
        score -= mergeResult.Inconsistencies.Count * 3;

        if (mergeResult.DataSourceType == DataSourceType.EstimatedByCategory)
        {
            score *= Math.Max(0.72, mergeResult.FallbackConfidence);
        }
        else if (mergeResult.DataSourceType == DataSourceType.Mixed)
        {
            score *= 0.90;
        }

        if (mergeResult.ProfileRejected)
        {
            score *= 0.85;
        }

        if (offenderResult.PrincipalOffender?.Severity == OffenderSeverity.Critical)
        {
            score -= 5;
        }

        return Math.Round(Math.Clamp(score, 0, 100), 1);
    }

    private static (string summary, string[] alerts) BuildSummaryAndAlerts(
        NutritionDataMergeResult mergeResult,
        PrincipalOffenderResult offenderResult,
        double nutritionalScore)
    {
        var profile = mergeResult.FinalProfile;
        var alerts = new List<string>();
        var summaryParts = new List<string>();

        summaryParts.Add(mergeResult.DataSourceType switch
        {
            DataSourceType.Real => "Analise baseada principalmente em dados reais da tabela nutricional.",
            DataSourceType.Mixed => "Analise baseada em leitura parcial complementada por perfil da categoria.",
            DataSourceType.EstimatedByCategory => "Analise baseada em estimativa por categoria nutricional.",
            _ => "Analise baseada em dados limitados."
        });

        if (offenderResult.PrincipalOffender != null)
        {
            summaryParts.Add(BuildPrimaryOffenderSummary(offenderResult.PrincipalOffender));
            alerts.Add(BuildPrimaryOffenderAlert(offenderResult.PrincipalOffender));
        }

        if (IsStrongPositiveSignal("protein", mergeResult) && (profile.EstimatedProteinPer100g ?? 0) >= 12)
        {
            summaryParts.Add("O perfil consolidado indica boa densidade proteica.");
        }

        if (IsStrongPositiveSignal("fiber", mergeResult) && (profile.EstimatedFiberPer100g ?? 0) >= 5)
        {
            summaryParts.Add("O perfil consolidado indica bom aporte de fibras.");
        }

        if (mergeResult.DataSourceType == DataSourceType.Mixed)
        {
            alerts.Add($"Leitura parcial: {mergeResult.RealFieldsCount} campos reais e {mergeResult.EstimatedFieldsCount} estimados.");
        }

        if (mergeResult.DataSourceType == DataSourceType.EstimatedByCategory)
        {
            alerts.Add("Valores nutricionais estimados por categoria. Para maior precisao, envie a tabela nutricional.");
        }

        foreach (var inconsistency in mergeResult.Inconsistencies)
        {
            alerts.Add(inconsistency);
        }

        if (nutritionalScore < 40)
        {
            alerts.Add($"Score nutricional baixo ({nutritionalScore:F1}/100).");
        }

        return (string.Join(" ", summaryParts.Distinct()), alerts.Distinct().ToArray());
    }

    private static string BuildPrimaryOffenderSummary(OffenderScore offender)
    {
        return offender.Type switch
        {
            OffenderType.Sugar => $"O principal ponto de atencao e o acucar elevado ({offender.Value:F1} g/100g).",
            OffenderType.Fat => $"O principal ponto de atencao e a gordura elevada ({offender.Value:F1} g/100g).",
            OffenderType.Sodium => $"O principal ponto de atencao e o sodio elevado ({offender.Value:F1} mg/100g).",
            OffenderType.CalorieDensity => $"A densidade calorica do produto merece atencao ({offender.Value:F1} kcal/100g).",
            OffenderType.LowProtein => "O teor de proteina e baixo para um melhor equilibrio nutricional.",
            OffenderType.LowFiber => "O teor de fibras e baixo no perfil consolidado.",
            _ => "Ha um ponto de atencao relevante no perfil nutricional."
        };
    }

    private static string BuildPrimaryOffenderAlert(OffenderScore offender)
    {
        return offender.Type switch
        {
            OffenderType.Sugar => "Alerta: teor alto de acucar.",
            OffenderType.Fat => "Alerta: teor alto de gordura.",
            OffenderType.Sodium => "Alerta: teor alto de sodio.",
            OffenderType.CalorieDensity => "Alerta: densidade calorica elevada.",
            OffenderType.LowProtein => "Alerta: baixo teor de proteina.",
            OffenderType.LowFiber => "Alerta: baixo teor de fibras.",
            _ => "Alerta nutricional relevante."
        };
    }

    private static bool IsStrongPositiveSignal(string fieldName, NutritionDataMergeResult mergeResult)
    {
        return mergeResult.FieldSources.TryGetValue(fieldName, out var source) && source == "Real";
    }

    private static EnhancedNutritionAnalysisResult CreateErrorResult(string errorMessage)
    {
        return new EnhancedNutritionAnalysisResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            DataSourceType = DataSourceType.Unknown,
            Alerts = Array.Empty<string>(),
            FieldSources = new Dictionary<string, string>()
        };
    }
}
