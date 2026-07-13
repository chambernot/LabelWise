using LabelWise.Application.DTOs.FoodAnalysisTrust;
using LabelWise.Application.DTOs.IngredientAnalysis;
using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Infrastructure.Services;

public sealed class FoodAnalysisQualityGate
{
    private static readonly string[] StrongTerms =
    [
        "produto saudável", "produto ruim", "evite", "excelente", "seguro", "saudável", "ultraprocessado", "vegano", "sem lactose", "sem glúten"
    ];

    public void Apply(IngredientAnalysisResponse response, FoodAnalysisTrustReport trust)
    {
        response.Trust = trust;
        response.AnalysisQuality = trust.TrustLevel;
        response.SafeForPreciseNutritionAnalysis = trust.SafeToScore;
        response.RetryRecommended = !trust.SafeToConclude;
        response.ConfidenceSummary = new ConfidenceSummaryDto
        {
            GlobalConfidence = trust.TrustLevel,
            OcrConfidence = response.Diagnostics.OcrConfidence,
            Completeness = trust.AnalysisMode,
            IsPartialReading = trust.AnalysisMode != "complete",
            BlocksAbsoluteConclusions = !trust.SafeToConclude,
            ScoreIsEstimated = !trust.SafeToScore,
            Reasons = trust.Reasons.ToList()
        };
        response.NutritionAnalysis = new NutritionAnalysisSafetyDto
        {
            DefinitiveScoreAllowed = trust.SafeToScore,
            ScoreEstimated = !trust.SafeToScore,
            Status = trust.SafeToScore ? "score_definitivo_permitido" : "resultado_preliminar",
            Warnings = trust.SafeToScore ? [] : ["Estimativa baseada nas informações visíveis; score definitivo bloqueado."]
        };

        if (!trust.SafeModeRequired)
            return;

        response.ProductionSafeModeApplied = true;
        Add(response.Warnings, "Modo seguro aplicado: resultado preliminar baseado nas informações visíveis.");
        Add(response.Recommendations, "Se precisar confirmar perfis alimentares, envie uma foto mais nítida e completa do rótulo.");

        DowngradePositiveProfile(response.DietProfiles.Vegan, "Não foi possível confirmar que o produto é vegano com a leitura atual.");
        DowngradePositiveProfile(response.DietProfiles.Vegetarian, "Não foi possível confirmar compatibilidade vegetariana com a leitura atual.");
        DowngradePositiveProfile(response.DietProfiles.LactoseFree, "Não foi possível confirmar ausência de lactose com a leitura atual.");
        DowngradePositiveProfile(response.DietProfiles.GlutenFree, "Não foi possível confirmar ausência de glúten com a leitura atual.");
        DowngradeProcessing(response.ProcessingLevel, response.ProcessingClassification, response.ProcessingAnalysis);
        SanitizeAssistantSummary(response.AssistantSummary, trust);
    }

    public void Apply(UnifiedNutritionAnalysisResponse response, FoodAnalysisTrustReport trust)
    {
        response.Trust = trust;
        response.NutritionReliabilityScore = Math.Min(response.NutritionReliabilityScore, trust.AnalysisTrustScore);
        response.AnalysisQuality = new NutritionAnalysisQualityDto
        {
            Mode = trust.AnalysisMode,
            Confidence = trust.TrustLevel,
            Reason = trust.SafeToScore
                ? "Leitura suficiente para score nutricional."
                : "Resultado preliminar: não foi possível confirmar todos os dados nutricionais."
        };

        response.ImageQuality.SafeForPreciseNutritionAnalysis = response.ImageQuality.SafeForPreciseNutritionAnalysis && trust.SafeToScore;
        response.ImageQuality.RetryRequested = response.ImageQuality.RetryRequested || trust.SafeModeRequired;
        response.ImageQuality.ReasonCode = trust.SafeModeRequired ? "low_trust" : response.ImageQuality.ReasonCode;
        response.ImageQuality.Reason ??= trust.SafeModeRequired ? "Leitura parcial ou inconsistente." : null;
        response.ImageQuality.Warnings = response.ImageQuality.Warnings
            .Concat(trust.Reasons)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (trust.SafeToScore || response.Score is null)
            return;

        response.Score.Confidence = "low";
        response.Score.Reliability = "unsafe_read";
        response.Score.Label = "Resultado preliminar";
        response.Score.Color = "yellow";
        response.Score.Warnings = response.Score.Warnings
            .Concat(["Score definitivo bloqueado por baixa confiança da leitura."])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        response.QuickFlags = response.QuickFlags
            .Where(flag => !ContainsStrongTerm(flag.Label))
            .Concat([new NutritionQuickFlagDto { Type = "warning", Label = "Leitura parcial: confirme com nova foto." }])
            .GroupBy(flag => flag.Label, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static void SanitizeAssistantSummary(AssistantSummaryDto summary, FoodAnalysisTrustReport trust)
    {
        summary.Confidence = trust.TrustLevel;
        summary.Text = "Análise parcial: resultado estimado com base nas informações visíveis. Não foi possível confirmar conclusões absolutas.";
        summary.Highlights = summary.Highlights.Where(item => !ContainsStrongTerm(item)).ToList();
        summary.Warnings = summary.Warnings
            .Select(Soften)
            .Concat(trust.Reasons.Take(3))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void DowngradePositiveProfile(DietProfileCompatibilityDto profile, string warning)
    {
        if (profile.CompatibilityStatus is DietCompatibilityStatuses.NotCompatible or DietCompatibilityStatuses.LikelyNotCompatible or DietCompatibilityStatuses.Attention)
            return;

        profile.Compatible = false;
        profile.CompatibilityStatus = DietCompatibilityStatuses.Uncertain;
        profile.CompatibilityLevel = "unknown";
        profile.Confidence = "low";
        profile.Status = CompatibilityStatus.Uncertain;
        Add(profile.Warnings, warning);
    }

    private static void DowngradeProcessing(ProcessingLevelDto processingLevel, ProcessingClassificationDto processingClassification, ProcessingAnalysisDto processingAnalysis)
    {
        if (processingLevel.Value == "ultra_processed")
        {
            processingLevel.Value = "unknown";
            processingLevel.Confidence = "low";
            Add(processingLevel.Reasons, "Classificação forte bloqueada por modo seguro.");
        }

        if (processingClassification.Level == "ultra_processed")
        {
            processingClassification.Level = "unknown";
            processingClassification.Confidence = "low";
            Add(processingClassification.Reasons, "Classificação forte bloqueada por modo seguro.");
        }

        if (processingAnalysis.Level == "ultra_processed")
        {
            processingAnalysis.Level = "unknown";
            processingAnalysis.Confidence = "low";
            Add(processingAnalysis.Reasons, "Classificação forte bloqueada por modo seguro.");
        }
    }

    private static string Soften(string value)
    {
        var text = value;
        text = text.Replace("Produto saudável", "Resultado preliminar", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("produto saudável", "resultado preliminar", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("excelente", "não confirmado", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("seguro", "não confirmado", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("evite", "avalie com cautela", StringComparison.OrdinalIgnoreCase);
        return text;
    }

    private static bool ContainsStrongTerm(string value) =>
        StrongTerms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static void Add(List<string> values, string value)
    {
        if (!values.Contains(value, StringComparer.OrdinalIgnoreCase))
            values.Add(value);
    }
}
