using LabelWise.Application.DTOs.IngredientAnalysis;

namespace LabelWise.Infrastructure.Services.IngredientAnalysis;

public sealed class FoodCompatibilityContext
{
    public IReadOnlyList<string> Ingredients { get; init; } = [];
    public IReadOnlyList<IngredientNormalizedDto> NormalizedIngredients { get; init; } = [];
    public IReadOnlyList<string> Claims { get; init; } = [];
    public IReadOnlyList<AllergenRiskDto> AllergenRisks { get; init; } = [];
    public string? RawOcrText { get; init; }
    public string? NutritionContext { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed class FoodCompatibilityEngine(DietProfileEngine dietProfileEngine)
{
    public DietProfilesDto Evaluate(FoodCompatibilityContext context)
    {
        var profiles = dietProfileEngine.Evaluate(context.Ingredients, context.AllergenRisks, context.Claims);
        var sources = context.Ingredients
            .Concat(context.NormalizedIngredients.Select(item => item.Raw))
            .Concat(context.NormalizedIngredients.Select(item => item.Normalized))
            .Concat(context.Claims.Where(IsPositiveRiskClaim))
            .Concat(context.AllergenRisks.Select(risk => risk.Name))
            .Concat(context.AllergenRisks.SelectMany(risk => risk.Evidence))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToList();

        ApplyConservativeBlocks(profiles, sources, context.AllergenRisks, context.Warnings);
        return profiles;
    }

    private static void ApplyConservativeBlocks(
        DietProfilesDto profiles,
        IReadOnlyList<string> sources,
        IReadOnlyList<AllergenRiskDto> allergenRisks,
        IReadOnlyList<string> warnings)
    {
        var partial = warnings.Any(warning => IngredientTextNormalizer.ContainsAny(warning, ["parcial", "reflexo", "desfoc", "ilegível", "ilegiv", "curva", "inclina", "obstru"]));

        if (HasAnyMilkRisk(sources, allergenRisks))
        {
            Block(profiles.LactoseFree, DietCompatibilityStatuses.Uncertain, "Há leite, lactose, derivados ou risco de traços; não é seguro classificar como sem lactose.", EvidenceType.CrossContamination);
            Block(profiles.Vegan, DietCompatibilityStatuses.Uncertain, "Há sinal de leite/lactose/derivados ou possível contaminação; compatibilidade vegana não é segura.", EvidenceType.CrossContamination);
        }

        if (HasAnyRisk(sources, allergenRisks, ["glúten", "gluten", "trigo", "cevada", "centeio", "malte"]))
            Block(profiles.GlutenFree, DietCompatibilityStatuses.Uncertain, "Há fonte, claim ou risco de traços de glúten; não é seguro classificar como sem glúten.", EvidenceType.CrossContamination);

        if (HasAnyRisk(sources, allergenRisks, ["ovo", "ovos", "albumina", "peixe", "crustáceo", "crustaceo", "camarão", "camarao", "gelatina", "colágeno", "colageno", "mel"]))
            Block(profiles.Vegan, DietCompatibilityStatuses.Uncertain, "Há sinal de ingrediente animal ou risco de traços; compatibilidade vegana não é segura.", EvidenceType.CrossContamination);

        if (HasAnyRisk(sources, allergenRisks, ["peixe", "crustáceo", "crustaceo", "camarão", "camarao", "gelatina", "colágeno", "colageno", "carne", "frango", "bacon", "banha"]))
            Block(profiles.Vegetarian, DietCompatibilityStatuses.Uncertain, "Há sinal de ingrediente animal incompatível ou risco de traços; compatibilidade vegetariana não é segura.", EvidenceType.CrossContamination);

        if (partial)
            DowngradePositiveProfiles(profiles, "A leitura parece parcial. Recomendamos nova foto mais próxima e alinhada.");
    }

    private static bool HasAnyMilkRisk(IReadOnlyList<string> sources, IReadOnlyList<AllergenRiskDto> allergenRisks) =>
        HasAnyRisk(sources, allergenRisks, ["leite", "lactose", "derivado de leite", "derivados de leite", "derivado do leite", "derivados do leite", "fermento lácteo", "fermento lacteo", "creme de leite", "soro de leite", "caseína", "caseina", "whey"]);

    private static bool HasAnyRisk(IReadOnlyList<string> sources, IReadOnlyList<AllergenRiskDto> allergenRisks, IReadOnlyList<string> terms) =>
        sources.Any(source => !IsNegativeFreeFromClaim(source, terms) && IngredientTextNormalizer.ContainsAny(source, terms)) ||
        allergenRisks.Any(risk => IngredientTextNormalizer.ContainsAny(risk.Name, terms) || risk.Evidence.Any(evidence => IngredientTextNormalizer.ContainsAny(evidence, terms)));

    private static bool IsPositiveRiskClaim(string claim)
    {
        var normalized = IngredientTextNormalizer.Normalize(claim);
        return normalized.StartsWith("contem ", StringComparison.Ordinal) ||
            normalized.StartsWith("pode conter ", StringComparison.Ordinal) ||
            normalized.StartsWith("tracos de ", StringComparison.Ordinal) ||
            normalized.StartsWith("alergico", StringComparison.Ordinal) ||
            normalized.StartsWith("alergenico", StringComparison.Ordinal) ||
            normalized.Contains("fabricado em equipamento", StringComparison.Ordinal) ||
            normalized.Contains("fabricado em linha", StringComparison.Ordinal);
    }

    private static bool IsNegativeFreeFromClaim(string source, IReadOnlyList<string> terms)
    {
        var normalized = IngredientTextNormalizer.Normalize(source);
        if (!normalized.Contains("nao contem", StringComparison.Ordinal) &&
            !normalized.Contains("sem ", StringComparison.Ordinal) &&
            !normalized.Contains("livre de", StringComparison.Ordinal))
        {
            return false;
        }

        return terms.Any(term => normalized.Contains(IngredientTextNormalizer.Normalize(term), StringComparison.Ordinal));
    }

    private static void Block(DietProfileCompatibilityDto profile, string status, string reason, EvidenceType evidenceType)
    {
        profile.Compatible = false;
        profile.CompatibilityStatus = status;
        profile.Status = status == DietCompatibilityStatuses.NotCompatible ? CompatibilityStatus.NotCompatible : CompatibilityStatus.Uncertain;
        profile.CompatibilityLevel = "unknown";
        profile.Confidence = MinConfidence(profile.Confidence, "medium");

        // Remove prior "no ingredient detected" style reasons that contradict the new blocking reason.
        // These are optimistic defaults from DietProfileEngine that are superseded once a risk signal is found.
        profile.Reasons.RemoveAll(item =>
            IngredientTextNormalizer.Normalize(item).Contains("nao foram detectados", StringComparison.Ordinal) ||
            IngredientTextNormalizer.Normalize(item).Contains("nenhum ingrediente animal", StringComparison.Ordinal) ||
            IngredientTextNormalizer.Normalize(item).Contains("nenhum ingrediente de origem animal", StringComparison.Ordinal));

        if (!profile.Reasons.Any(item => item.Equals(reason, StringComparison.OrdinalIgnoreCase)))
            profile.Reasons.Insert(0, reason);

        // Only add to warnings if it is not already present (by exact match or as a substring of an existing warning)
        // to avoid duplicating reasons that were already emitted by DietProfileEngine.
        var alreadyInWarnings = profile.Warnings.Any(item =>
            item.Equals(reason, StringComparison.OrdinalIgnoreCase) ||
            item.Contains(reason, StringComparison.OrdinalIgnoreCase) ||
            reason.Contains(item, StringComparison.OrdinalIgnoreCase));
        if (!alreadyInWarnings)
            profile.Warnings.Add(reason);
        if (!profile.EvidenceTypes.Contains(evidenceType))
            profile.EvidenceTypes.Add(evidenceType);
        if (!profile.ReasonSources.Contains("food_compatibility_engine", StringComparer.OrdinalIgnoreCase))
            profile.ReasonSources.Add("food_compatibility_engine");
    }

    private static void DowngradePositiveProfiles(DietProfilesDto profiles, string warning)
    {
        foreach (var profile in new[] { profiles.Vegan, profiles.Vegetarian, profiles.LactoseFree, profiles.GlutenFree })
        {
            if (!profile.Compatible)
                continue;

            profile.Compatible = false;
            profile.CompatibilityStatus = DietCompatibilityStatuses.Uncertain;
            profile.Status = CompatibilityStatus.Uncertain;
            profile.Confidence = "low";
            if (!profile.Warnings.Any(item => item.Equals(warning, StringComparison.OrdinalIgnoreCase)))
                profile.Warnings.Add(warning);
        }
    }

    private static string MinConfidence(string first, string second) => Rank(first) <= Rank(second) ? first : second;

    private static int Rank(string confidence) => confidence switch
    {
        "high" => 3,
        "medium" => 2,
        "low" => 1,
        "very_low" => 0,
        _ => 1
    };
}
