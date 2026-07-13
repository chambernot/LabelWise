using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;
using LabelWise.Domain.Models;
using LabelWise.Infrastructure.Services.IngredientAnalysis;

namespace LabelWise.Infrastructure.Services.FoodAnalysis;

public sealed class RegulatoryCompatibilityResolver
{
    public ProfileCompatibility? Resolve(string profile, IReadOnlyList<RegulatoryClaim> claims)
    {
        var relevantClaims = claims
            .Where(claim => IsRelevantSubject(profile, claim.Subject))
            .OrderByDescending(claim => (int)claim.Evidence.Priority)
            .ThenByDescending(claim => ClaimTypeWeight(claim.ClaimType))
            .ThenByDescending(claim => claim.IsAbsolute)
            .ThenByDescending(claim => claim.Confidence)
            .ThenBy(claim => IngredientTextNormalizer.Normalize(claim.Subject), StringComparer.Ordinal)
            .ThenBy(claim => IngredientTextNormalizer.Normalize(claim.OriginalText), StringComparer.Ordinal)
            .ToList();

        foreach (var claim in relevantClaims)
        {
            if (claim.ClaimType is RegulatoryClaimType.Contains or RegulatoryClaimType.Prohibited or RegulatoryClaimType.Warning)
            {
                return Build(
                    profile,
                    FoodCompatibilityStatus.Incompatible,
                    1.0,
                    [$"Incompatível por claim regulatório explícito: {claim.OriginalText}."] ,
                    [],
                    [NormalizeRegulatoryEvidence(claim)]);
            }

            if (claim.IsCrossContamination)
            {
                return Build(
                    profile,
                    FoodCompatibilityStatus.CrossContaminationRisk,
                    Math.Max(0.95, claim.Confidence),
                    [$"Risco de contaminação cruzada indicado no rótulo: {claim.OriginalText}."] ,
                    ["Pode não ser adequado para pessoas com restrição severa."],
                    [NormalizeRegulatoryEvidence(claim)]);
            }

            if (claim.ClaimType is RegulatoryClaimType.FreeFrom or RegulatoryClaimType.Certified)
            {
                return Build(
                    profile,
                    FoodCompatibilityStatus.Compatible,
                    claim.IsAbsolute ? 1.0 : Math.Max(0.95, claim.Confidence),
                    [$"Compatibilidade confirmada por claim regulatório: {claim.OriginalText}."] ,
                    [],
                    [NormalizeRegulatoryEvidence(claim)]);
            }
        }

        return null;
    }

    private static int ClaimTypeWeight(RegulatoryClaimType type) => type switch
    {
        RegulatoryClaimType.Contains or RegulatoryClaimType.Prohibited or RegulatoryClaimType.Warning => 300,
        RegulatoryClaimType.MayContain or RegulatoryClaimType.CrossContamination => 200,
        RegulatoryClaimType.FreeFrom or RegulatoryClaimType.Certified => 100,
        _ => 0
    };

    private static bool IsRelevantSubject(string profile, string subject)
    {
        return profile switch
        {
            "gluten_free" => CompatibilityDecisionEngine.ContainsAny(subject, ["gluten", "glúten", "trigo", "cevada", "centeio", "malte"]),
            "lactose_free" => CompatibilityDecisionEngine.ContainsAny(subject, ["lactose", "leite", "derivados de leite"]),
            "vegan" => CompatibilityDecisionEngine.ContainsAny(subject, ["leite", "lactose", "ovo", "carne", "frango", "peixe", "mel", "gelatina"]),
            "vegetarian" => CompatibilityDecisionEngine.ContainsAny(subject, ["carne", "frango", "peixe", "gelatina"]),
            _ => CompatibilityDecisionEngine.ContainsAny(subject, [profile])
        };
    }

    private static Evidence NormalizeRegulatoryEvidence(RegulatoryClaim claim) => new()
    {
        Type = "regulatory_claim",
        Text = claim.OriginalText,
        Source = claim.Evidence.Source,
        Priority = EvidencePriority.RegulatoryClaimExplicit,
        Confidence = 1.0,
        OriginBlock = claim.Evidence.OriginBlock,
        Metadata = claim.Evidence.Metadata
    };

    private static ProfileCompatibility Build(
        string profile,
        FoodCompatibilityStatus status,
        double confidence,
        IReadOnlyList<string> reasons,
        IReadOnlyList<string> warnings,
        IReadOnlyList<Evidence> evidence) =>
        new()
        {
            ProfileName = profile,
            Status = status,
            Confidence = Math.Clamp(confidence, 0d, 1d),
            Reasons = reasons,
            Warnings = warnings,
            SupportingEvidence = evidence
        };
}

public sealed class CompatibilityDeterministicResolver
{
    public IReadOnlyList<string> OrderProfiles(IReadOnlyList<string> profiles) => profiles
        .Select(profile => new { Original = profile, Normalized = CompatibilityDecisionEngine.NormalizeProfile(profile) })
        .GroupBy(item => item.Normalized, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.OrderBy(item => item.Original, StringComparer.Ordinal).First())
        .OrderBy(item => item.Normalized, StringComparer.Ordinal)
        .Select(item => item.Original)
        .ToList();
}

public sealed class UserProfileFoodRestrictions
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Restrictions = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["gluten"] = ["celiac", "gluten_free"],
        ["glúten"] = ["celiac", "gluten_free"],
        ["trigo"] = ["celiac", "gluten_free"],
        ["leite"] = ["lactose_free", "vegan"],
        ["lactose"] = ["lactose_free", "vegan"],
        ["ovo"] = ["vegan"],
        ["carne"] = ["vegan", "vegetarian"]
    };

    public FoodRestriction Resolve(string ingredient)
    {
        var normalized = IngredientTextNormalizer.Normalize(ingredient);
        var restrictedFor = Restrictions
            .Where(pair => normalized.Contains(IngredientTextNormalizer.Normalize(pair.Key), StringComparison.OrdinalIgnoreCase))
            .SelectMany(pair => pair.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToList();

        return new FoodRestriction(ingredient, restrictedFor);
    }

    public bool IsUniversallyTolerated(string ingredient) => Resolve(ingredient).RestrictedFor.Count == 0;
}

public sealed record FoodRestriction(string Ingredient, IReadOnlyList<string> RestrictedFor);

public sealed class ProductionSafetyValidator
{
    public ProductionSafetyResult Validate(
        DecisionInput input,
        Dictionary<string, ProfileCompatibility> compatibilities,
        FoodProcessingDecision processing,
        AnalysisQuality quality,
        int nutritionalScore,
        double confidence)
    {
        var warnings = new List<string>();
        var safeModeRequired = false;
        var adjustedQuality = quality;
        var adjustedScore = nutritionalScore;
        string? classificationOverride = null;

        if (processing.SafeModeBlocked && processing.Level != ProcessingLevel.Unknown)
            warnings.Add("Classificação de processamento bloqueada por baixa confiança.");

        if (processing.SafeModeBlocked)
            safeModeRequired = true;

        if (processing.Level == ProcessingLevel.UltraProcessed && nutritionalScore >= 80)
        {
            adjustedScore = 65;
            warnings.Add("Score definitivo bloqueado por conflito com ultraprocessamento.");
        }

        if (processing.Level == ProcessingLevel.MinimallyProcessed && HasIndustrialFormulation(input))
        {
            adjustedQuality = AnalysisQuality.Inconsistent;
            classificationOverride = "preliminary_analysis";
            safeModeRequired = true;
            warnings.Add("Conflito detectado: sinais industriais incompatíveis com classificação minimamente processada.");
        }

        if (confidence < 0.60 && nutritionalScore > 55)
        {
            adjustedScore = 55;
            safeModeRequired = true;
            warnings.Add("Score definitivo limitado por baixo nível de confiança.");
        }

        ValidateRegulatoryContradictions(input, compatibilities, warnings, ref adjustedQuality, ref safeModeRequired, ref classificationOverride);

        return new ProductionSafetyResult(
            adjustedQuality,
            adjustedScore,
            classificationOverride,
            safeModeRequired,
            warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static void ValidateRegulatoryContradictions(
        DecisionInput input,
        Dictionary<string, ProfileCompatibility> compatibilities,
        List<string> warnings,
        ref AnalysisQuality quality,
        ref bool safeModeRequired,
        ref string? classificationOverride)
    {
        foreach (var claim in input.RegulatoryInformation)
        {
            if (claim.ClaimType != RegulatoryClaimType.Contains)
                continue;

            if (IsSubject(claim.Subject, ["gluten", "glúten", "trigo"]) && IsCompatible("gluten_free", compatibilities))
            {
                warnings.Add("Conflito crítico: gluten_free não pode ser compatível com claim CONTÉM GLÚTEN.");
                quality = AnalysisQuality.Inconsistent;
                safeModeRequired = true;
                classificationOverride = "preliminary_analysis";
            }

            if (IsSubject(claim.Subject, ["leite", "lactose"]) && IsCompatible("lactose_free", compatibilities))
            {
                warnings.Add("Conflito crítico: lactose_free não pode ser compatível com claim CONTÉM LEITE/LACTOSE.");
                quality = AnalysisQuality.Inconsistent;
                safeModeRequired = true;
                classificationOverride = "preliminary_analysis";
            }

            if (IsSubject(claim.Subject, ["leite", "lactose", "ovo", "carne", "peixe", "mel", "gelatina"]) && IsCompatible("vegan", compatibilities))
            {
                warnings.Add("Conflito crítico: vegan não pode ser compatível com claim de ingrediente animal.");
                quality = AnalysisQuality.Inconsistent;
                safeModeRequired = true;
                classificationOverride = "preliminary_analysis";
            }
        }
    }

    private static bool HasIndustrialFormulation(DecisionInput input)
    {
        var ingredients = input.ExplicitIngredients.Select(evidence => IngredientTextNormalizer.Normalize(evidence.Text)).ToList();
        return FoodProcessingEngine.UltraProcessedSignals.Concat(FoodProcessingEngine.ProcessedSignals)
            .Count(signal => ingredients.Any(ingredient => ingredient.Contains(signal, StringComparison.OrdinalIgnoreCase))) >= 2;
    }

    private static bool IsCompatible(string profile, Dictionary<string, ProfileCompatibility> compatibilities) =>
        compatibilities.TryGetValue(profile, out var compatibility) &&
        compatibility.Status is FoodCompatibilityStatus.Compatible or FoodCompatibilityStatus.LikelyCompatible;

    private static bool IsSubject(string subject, IEnumerable<string> terms)
    {
        var normalized = IngredientTextNormalizer.Normalize(subject);
        return terms.Any(term => normalized.Contains(IngredientTextNormalizer.Normalize(term), StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record ProductionSafetyResult(
    AnalysisQuality Quality,
    int NutritionalScore,
    string? FoodClassificationOverride,
    bool SafeModeRequired,
    IReadOnlyList<string> Warnings);
