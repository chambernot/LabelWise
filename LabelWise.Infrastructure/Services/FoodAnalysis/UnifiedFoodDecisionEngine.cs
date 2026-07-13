using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;
using LabelWise.Domain.Models;
using LabelWise.Infrastructure.Services.IngredientAnalysis;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services.FoodAnalysis;

/// <summary>
/// Engine central de decisão alimentar. Toda decisão final deve nascer deste componente.
/// </summary>
public sealed class UnifiedFoodDecisionEngine : IDecisionEngine
{
    private readonly CompatibilityDecisionEngine _compatibilityEngine;
    private readonly FoodProcessingEngine _processingEngine;
    private readonly SemanticConsolidationEngine _semanticConsolidationEngine;
    private readonly ProductionSafetyValidator _productionSafetyValidator;
    private readonly ILogger<UnifiedFoodDecisionEngine> _logger;

    public UnifiedFoodDecisionEngine(
        CompatibilityDecisionEngine compatibilityEngine,
        FoodProcessingEngine processingEngine,
        SemanticConsolidationEngine semanticConsolidationEngine,
        ProductionSafetyValidator productionSafetyValidator,
        ILogger<UnifiedFoodDecisionEngine> logger)
    {
        _compatibilityEngine = compatibilityEngine;
        _processingEngine = processingEngine;
        _semanticConsolidationEngine = semanticConsolidationEngine;
        _productionSafetyValidator = productionSafetyValidator;
        _logger = logger;
    }

    public Task<FoodDecision> MakeDecisionAsync(DecisionInput input)
    {
        var evidenceTrail = BuildEvidenceTrail(input);
        var confidence = CalculateDecisionConfidence(evidenceTrail);
        var explicitIngredientNames = input.ExplicitIngredients.Select(evidence => evidence.Text).Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        var processing = _processingEngine.Evaluate(input, explicitIngredientNames, confidence);
        var compatibilities = _compatibilityEngine.Evaluate(input, input.RequestedProfiles, processing, confidence);
        var criticalConflicts = input.Conflicts.Where(conflict => conflict.Severity == ConflictSeverity.Critical).ToList();
        var alerts = BuildAlerts(compatibilities, criticalConflicts).ToList();
        var warnings = BuildWarnings(input, processing, compatibilities).ToList();
        var quality = DetermineQuality(input, criticalConflicts, confidence);
        var foodClassification = ClassifyFood(processing, compatibilities, quality);
        var nutritionalScore = CalculateGlobalScore(input.NutritionalData, processing, compatibilities, quality, confidence);
        var safety = _productionSafetyValidator.Validate(input, compatibilities, processing, quality, nutritionalScore, confidence);
        warnings.AddRange(safety.Warnings);
        quality = safety.Quality;
        nutritionalScore = safety.NutritionalScore;
        foodClassification = safety.FoodClassificationOverride ?? foodClassification;
        var profileScores = compatibilities.ToDictionary(pair => pair.Key, pair => ScoreCompatibility(pair.Value), StringComparer.OrdinalIgnoreCase);
        var analysisTrustScore = (int)Math.Round(confidence * 100, MidpointRounding.AwayFromZero);
        var safeModeRequired = processing.SafeModeBlocked || quality is AnalysisQuality.Insufficient or AnalysisQuality.Inconsistent or AnalysisQuality.Partial || analysisTrustScore < 60 || safety.SafeModeRequired;
        var safeToConclude = !safeModeRequired && analysisTrustScore >= 60 && quality == AnalysisQuality.Reliable;
        var draft = new FoodDecisionDraft(foodClassification, processing.Level, quality, alerts, warnings, safeToConclude, analysisTrustScore, safeModeRequired);
        var summaryCards = _semanticConsolidationEngine.BuildSummaryCards(draft);
        var quickInsights = _semanticConsolidationEngine.BuildQuickInsights(draft);
        var recommendations = _semanticConsolidationEngine.BuildRecommendations(draft);
        var presentationHints = summaryCards.Select(card => card.PresentationHint).ToList();

        var decision = new FoodDecision
        {
            FoodClassification = foodClassification,
            Alerts = alerts,
            Warnings = warnings,
            ProcessingScore = processing.ProcessingScore,
            ProfileScores = profileScores,
            PresentationHints = presentationHints,
            ProfileCompatibilities = compatibilities,
            NutritionalScore = nutritionalScore,
            ProcessingLevel = processing.Level,
            Quality = quality,
            OverallConfidence = confidence,
            SummaryCards = summaryCards,
            QuickInsights = quickInsights,
            Recommendations = recommendations,
            AssistantSummary = _semanticConsolidationEngine.BuildAssistantSummary(draft),
            CriticalConflicts = criticalConflicts,
            EvidenceTrail = evidenceTrail
        };

        _logger.LogInformation(
            "[UnifiedFoodDecision] Classification={Classification}; Score={Score}; Processing={Processing}; Quality={Quality}; Confidence={Confidence}",
            decision.FoodClassification,
            decision.NutritionalScore,
            decision.ProcessingLevel,
            decision.Quality,
            decision.OverallConfidence);

        return Task.FromResult(decision);
    }

    public bool CanMakeDecision(DecisionInput input)
    {
        return input.RegulatoryInformation.Count > 0 ||
               input.ExplicitIngredients.Count > 0 ||
               input.SemanticInferences.Count > 0 ||
               input.NutritionalData.Count > 0;
    }

    public double CalculateDecisionConfidence(IReadOnlyList<Evidence> evidences)
    {
        if (evidences.Count == 0)
            return 0.1;

        var weightedTotal = evidences.Sum(evidence => (int)evidence.Priority * Math.Clamp(evidence.Confidence, 0d, 1d));
        var priorityTotal = evidences.Sum(evidence => Math.Max(1, (int)evidence.Priority));
        var confidence = weightedTotal / priorityTotal;

        if (evidences.Any(evidence => evidence.Priority == EvidencePriority.RegulatoryClaimExplicit && evidence.Confidence >= 0.90))
            confidence = Math.Max(confidence, 0.92);

        return Math.Clamp(confidence, 0.1, 1.0);
    }

    private static IReadOnlyList<Evidence> BuildEvidenceTrail(DecisionInput input)
    {
        return input.RegulatoryInformation.Select(claim => claim.Evidence)
            .Concat(input.ExplicitIngredients)
            .Concat(input.SemanticInferences)
            .OrderByDescending(evidence => evidence.Priority)
            .ThenByDescending(evidence => evidence.Confidence)
            .ToList();
    }

    private static AnalysisQuality DetermineQuality(DecisionInput input, IReadOnlyList<AnalysisConflict> criticalConflicts, double confidence)
    {
        if (criticalConflicts.Count > 0)
            return AnalysisQuality.Inconsistent;

        if (input.AnalysisQuality == AnalysisQuality.Insufficient || confidence < 0.35)
            return AnalysisQuality.Insufficient;

        if (input.AnalysisQuality == AnalysisQuality.Partial || confidence < 0.60)
            return AnalysisQuality.Partial;

        return input.AnalysisQuality == AnalysisQuality.Inconsistent ? AnalysisQuality.Inconsistent : AnalysisQuality.Reliable;
    }

    private static IEnumerable<string> BuildAlerts(
        Dictionary<string, ProfileCompatibility> compatibilities,
        IReadOnlyList<AnalysisConflict> criticalConflicts)
    {
        foreach (var conflict in criticalConflicts)
            yield return conflict.Description;

        foreach (var compatibility in compatibilities.Values.Where(item => item.Status == FoodCompatibilityStatus.Incompatible))
            yield return $"{compatibility.ProfileName}: {compatibility.Reasons.FirstOrDefault() ?? "incompatibilidade detectada"}";
    }

    private static IEnumerable<string> BuildWarnings(
        DecisionInput input,
        FoodProcessingDecision processing,
        Dictionary<string, ProfileCompatibility> compatibilities)
    {
        foreach (var warning in compatibilities.Values.SelectMany(item => item.Warnings))
            yield return warning;

        foreach (var reason in processing.Reasons)
            yield return reason;

        if (input.AnalysisQuality != AnalysisQuality.Reliable)
            yield return "Use linguagem cautelosa: resultado pode ser parcial ou preliminar.";
    }

    private static string ClassifyFood(
        FoodProcessingDecision processing,
        Dictionary<string, ProfileCompatibility> compatibilities,
        AnalysisQuality quality)
    {
        if (quality is AnalysisQuality.Insufficient or AnalysisQuality.Inconsistent)
            return "preliminary_analysis";

        if (compatibilities.Values.Any(item => item.Status == FoodCompatibilityStatus.Incompatible))
            return "restricted_for_some_profiles";

        return processing.Level switch
        {
            ProcessingLevel.UltraProcessed => "ultra_processed_food",
            ProcessingLevel.Processed => "processed_food",
            ProcessingLevel.MinimallyProcessed => "minimally_processed_food",
            _ => "unknown"
        };
    }

    private static int CalculateGlobalScore(
        Dictionary<string, double> nutrition,
        FoodProcessingDecision processing,
        Dictionary<string, ProfileCompatibility> compatibilities,
        AnalysisQuality quality,
        double confidence)
    {
        if (quality is AnalysisQuality.Insufficient or AnalysisQuality.Inconsistent)
            return 0;

        var score = processing.ProcessingScore == 0 ? 50 : processing.ProcessingScore;

        if (nutrition.TryGetValue("sugar", out var sugar) || nutrition.TryGetValue("sugars", out sugar))
            score -= sugar >= 15 ? 20 : sugar >= 8 ? 10 : 0;

        if (nutrition.TryGetValue("sodium", out var sodium))
            score -= sodium >= 600 ? 20 : sodium >= 300 ? 10 : 0;

        if (nutrition.TryGetValue("saturated_fat", out var saturatedFat))
            score -= saturatedFat >= 6 ? 15 : saturatedFat >= 3 ? 8 : 0;

        score -= compatibilities.Values.Count(item => item.Status == FoodCompatibilityStatus.Incompatible) * 8;
        score -= compatibilities.Values.Count(item => item.Status == FoodCompatibilityStatus.CrossContaminationRisk) * 4;

        if (confidence < 0.60)
            score = Math.Min(score, 55);

        return Math.Clamp(score, 0, 100);
    }

    private static int ScoreCompatibility(ProfileCompatibility compatibility)
    {
        return compatibility.Status switch
        {
            FoodCompatibilityStatus.Compatible => 100,
            FoodCompatibilityStatus.LikelyCompatible => 75,
            FoodCompatibilityStatus.Uncertain => 45,
            FoodCompatibilityStatus.CrossContaminationRisk => 35,
            FoodCompatibilityStatus.LikelyIncompatible => 25,
            FoodCompatibilityStatus.Incompatible => 0,
            FoodCompatibilityStatus.InsufficientData => 0,
            _ => 0
        };
    }
}
