using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;
using LabelWise.Domain.Models;
using LabelWise.Infrastructure.Services.FoodAnalysis;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LabelWise.Tests.Services;

public sealed class UnifiedFoodDecisionEngineTests
{
    [Fact]
    public async Task MakeDecisionAsync_RegulatoryContainsGluten_AlwaysReturnsIncompatible()
    {
        var engine = CreateEngine();
        var claimEvidence = new Evidence
        {
            Type = "regulatory_claim",
            Text = "CONTÉM GLÚTEN",
            Source = "label",
            Priority = EvidencePriority.RegulatoryClaimExplicit,
            Confidence = 0.98
        };

        var decision = await engine.MakeDecisionAsync(new DecisionInput
        {
            RegulatoryInformation =
            [
                new RegulatoryClaim
                {
                    OriginalText = "CONTÉM GLÚTEN",
                    NormalizedText = "contem gluten",
                    ClaimType = RegulatoryClaimType.Contains,
                    Subject = "glúten",
                    IsPositiveClaim = true,
                    IsAbsolute = true,
                    Evidence = claimEvidence,
                    Confidence = 0.98
                }
            ],
            ExplicitIngredients = [],
            SemanticInferences = [],
            Conflicts = [],
            AnalysisQuality = AnalysisQuality.Reliable,
            RequestedProfiles = ["gluten_free"]
        });

        var glutenFree = decision.ProfileCompatibilities["gluten_free"];

        Assert.Equal(FoodCompatibilityStatus.Incompatible, glutenFree.Status);
        Assert.Equal(1.0, glutenFree.Confidence);
        Assert.Contains(decision.Alerts, alert => alert.Contains("CONTÉM GLÚTEN", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MakeDecisionAsync_MayContainMilk_ReturnsCrossContaminationRisk()
    {
        var engine = CreateEngine();
        var claimEvidence = new Evidence
        {
            Type = "regulatory_claim",
            Text = "PODE CONTER LEITE",
            Source = "label",
            Priority = EvidencePriority.RegulatoryClaimExplicit,
            Confidence = 0.95
        };

        var decision = await engine.MakeDecisionAsync(new DecisionInput
        {
            RegulatoryInformation =
            [
                new RegulatoryClaim
                {
                    OriginalText = "PODE CONTER LEITE",
                    NormalizedText = "pode conter leite",
                    ClaimType = RegulatoryClaimType.MayContain,
                    Subject = "leite",
                    IsPositiveClaim = true,
                    IsAbsolute = false,
                    Evidence = claimEvidence,
                    Confidence = 0.95
                }
            ],
            ExplicitIngredients = [],
            SemanticInferences = [],
            Conflicts = [],
            AnalysisQuality = AnalysisQuality.Reliable,
            RequestedProfiles = ["lactose_free"]
        });

        Assert.Equal(FoodCompatibilityStatus.CrossContaminationRisk, decision.ProfileCompatibilities["lactose_free"].Status);
    }

    [Fact]
    public async Task MakeDecisionAsync_UltraProcessedSignals_PreventHealthyScoreInflation()
    {
        var engine = CreateEngine();

        var decision = await engine.MakeDecisionAsync(new DecisionInput
        {
            RegulatoryInformation = [],
            ExplicitIngredients =
            [
                IngredientEvidence("proteína isolada"),
                IngredientEvidence("emulsificante"),
                IngredientEvidence("xarope de glicose"),
                IngredientEvidence("aromatizante")
            ],
            SemanticInferences = [],
            Conflicts = [],
            AnalysisQuality = AnalysisQuality.Reliable,
            NutritionalData = new Dictionary<string, double> { ["protein"] = 20, ["sugar"] = 18 },
            RequestedProfiles = ["muscle_gain"]
        });

        Assert.Equal(ProcessingLevel.UltraProcessed, decision.ProcessingLevel);
        Assert.True(decision.NutritionalScore <= 30);
        Assert.NotEqual(FoodCompatibilityStatus.Compatible, decision.ProfileCompatibilities["muscle_gain"].Status);
    }

    [Fact]
    public async Task MakeDecisionAsync_RegulatoryContainsOverridesSemanticInferenceAndFreeFromClaim()
    {
        var engine = CreateEngine();
        var containsEvidence = RegulatoryEvidence("CONTÉM GLÚTEN");
        var freeFromEvidence = RegulatoryEvidence("SEM GLÚTEN");

        var decision = await engine.MakeDecisionAsync(new DecisionInput
        {
            RegulatoryInformation =
            [
                new RegulatoryClaim
                {
                    OriginalText = "SEM GLÚTEN",
                    NormalizedText = "sem gluten",
                    ClaimType = RegulatoryClaimType.FreeFrom,
                    Subject = "glúten",
                    IsPositiveClaim = false,
                    IsAbsolute = true,
                    Evidence = freeFromEvidence,
                    Confidence = 0.99
                },
                new RegulatoryClaim
                {
                    OriginalText = "CONTÉM GLÚTEN",
                    NormalizedText = "contem gluten",
                    ClaimType = RegulatoryClaimType.Contains,
                    Subject = "glúten",
                    IsPositiveClaim = true,
                    IsAbsolute = true,
                    Evidence = containsEvidence,
                    Confidence = 0.95
                }
            ],
            ExplicitIngredients = [],
            SemanticInferences =
            [
                new Evidence
                {
                    Type = "semantic_inference",
                    Text = "sem sinais de glúten",
                    Source = "semantic",
                    Priority = EvidencePriority.SemanticInference,
                    Confidence = 1.0
                }
            ],
            Conflicts = [],
            AnalysisQuality = AnalysisQuality.Reliable,
            RequestedProfiles = ["gluten_free"]
        });

        var glutenFree = decision.ProfileCompatibilities["gluten_free"];

        Assert.Equal(FoodCompatibilityStatus.Incompatible, glutenFree.Status);
        Assert.Equal(1.0, glutenFree.Confidence);
        Assert.Contains(glutenFree.SupportingEvidence, evidence => evidence.Priority == EvidencePriority.RegulatoryClaimExplicit && evidence.Confidence == 1.0);
    }

    [Fact]
    public async Task MakeDecisionAsync_LowTrustProcessing_IsHiddenAndUnknown()
    {
        var engine = CreateEngine();

        var decision = await engine.MakeDecisionAsync(new DecisionInput
        {
            RegulatoryInformation = [],
            ExplicitIngredients = [IngredientEvidence("água")],
            SemanticInferences = [],
            Conflicts = [],
            AnalysisQuality = AnalysisQuality.Partial,
            RequestedProfiles = []
        });

        Assert.Equal(ProcessingLevel.Unknown, decision.ProcessingLevel);
        Assert.Equal(0, decision.ProcessingScore);
        Assert.Contains(decision.Warnings, warning => warning.Contains("bloqueia classificação", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(decision.QuickInsights, insight => insight.Text.Contains("natural", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(decision.QuickInsights, insight => insight.Type == "limited_analysis");
    }

    [Fact]
    public void UserProfileFoodRestrictions_GlutenIsRestrictedNotUniversallyTolerated()
    {
        var restrictions = new UserProfileFoodRestrictions();

        var gluten = restrictions.Resolve("glúten");

        Assert.Contains("celiac", gluten.RestrictedFor, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("gluten_free", gluten.RestrictedFor, StringComparer.OrdinalIgnoreCase);
        Assert.False(restrictions.IsUniversallyTolerated("glúten"));
    }

    private static UnifiedFoodDecisionEngine CreateEngine()
    {
        var knowledgeBase = new IngredientKnowledgeBase();
        return new UnifiedFoodDecisionEngine(
            new CompatibilityDecisionEngine(knowledgeBase, new RegulatoryCompatibilityResolver(), new CompatibilityDeterministicResolver()),
            new FoodProcessingEngine(new ProcessingConfidenceGate()),
            new SemanticConsolidationEngine(new QuickInsightSafetyFilter()),
            new ProductionSafetyValidator(),
            NullLogger<UnifiedFoodDecisionEngine>.Instance);
    }

    private static Evidence IngredientEvidence(string text) =>
        new()
        {
            Type = "ingredient_confirmed",
            Text = text,
            Source = "ingredient_list",
            Priority = EvidencePriority.IngredientExplicit,
            Confidence = 0.9
        };

    private static Evidence RegulatoryEvidence(string text) =>
        new()
        {
            Type = "regulatory_claim",
            Text = text,
            Source = "label",
            Priority = EvidencePriority.RegulatoryClaimExplicit,
            Confidence = 0.98
        };
}
