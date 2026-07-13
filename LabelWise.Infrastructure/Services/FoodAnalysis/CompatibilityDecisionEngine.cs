using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;
using LabelWise.Domain.Models;
using LabelWise.Infrastructure.Services.IngredientAnalysis;

namespace LabelWise.Infrastructure.Services.FoodAnalysis;

/// <summary>
/// Decide compatibilidade por perfil aplicando hierarquia absoluta de evidência.
/// </summary>
public sealed class CompatibilityDecisionEngine
{
    private readonly IngredientKnowledgeBase _knowledgeBase;
    private readonly RegulatoryCompatibilityResolver _regulatoryResolver;
    private readonly CompatibilityDeterministicResolver _deterministicResolver;

    public CompatibilityDecisionEngine(
        IngredientKnowledgeBase knowledgeBase,
        RegulatoryCompatibilityResolver regulatoryResolver,
        CompatibilityDeterministicResolver deterministicResolver)
    {
        _knowledgeBase = knowledgeBase;
        _regulatoryResolver = regulatoryResolver;
        _deterministicResolver = deterministicResolver;
    }

    public Dictionary<string, ProfileCompatibility> Evaluate(
        DecisionInput input,
        IReadOnlyList<string> requestedProfiles,
        FoodProcessingDecision processing,
        double decisionConfidence)
    {
        var profiles = requestedProfiles.Count > 0
            ? requestedProfiles
            : ["gluten_free", "lactose_free", "vegan", "vegetarian", "diabetic", "hypertension", "weight_loss", "muscle_gain", "child", "low_carb", "keto"];

        return _deterministicResolver.OrderProfiles(profiles)
            .ToDictionary(
                profile => profile,
                profile => EvaluateProfile(profile, input, processing, decisionConfidence),
                StringComparer.OrdinalIgnoreCase);
    }

    private ProfileCompatibility EvaluateProfile(
        string profile,
        DecisionInput input,
        FoodProcessingDecision processing,
        double decisionConfidence)
    {
        var normalizedProfile = NormalizeProfile(profile);
        var ingredients = input.ExplicitIngredients.ToList();
        var claims = input.RegulatoryInformation.ToList();

        if (input.AnalysisQuality == AnalysisQuality.Insufficient && ingredients.Count == 0 && claims.Count == 0)
            return Build(profile, FoodCompatibilityStatus.InsufficientData, 0.1, ["Dados insuficientes para avaliar este perfil."], [], []);

        var regulatory = _regulatoryResolver.Resolve(normalizedProfile, claims);
        if (regulatory is not null)
            return WithProfile(regulatory, profile);

        var ingredientDecision = EvaluateIngredientEvidence(normalizedProfile, ingredients);
        if (ingredientDecision is not null)
            return WithProfile(ingredientDecision, profile);

        var nutritionDecision = EvaluateNutritionProfile(normalizedProfile, input.NutritionalData, processing, decisionConfidence);
        if (nutritionDecision is not null)
            return WithProfile(nutritionDecision, profile);

        if (decisionConfidence < 0.55 || input.AnalysisQuality is AnalysisQuality.Partial or AnalysisQuality.Inconsistent)
        {
            return Build(
                profile,
                FoodCompatibilityStatus.Uncertain,
                Math.Min(decisionConfidence, 0.55),
                ["Resultado preliminar: os dados disponíveis não confirmam compatibilidade."],
                ["Ausência de evidência não deve ser interpretada como compatibilidade absoluta."],
                BuildEvidenceTrail(input));
        }

        return Build(
            profile,
            FoodCompatibilityStatus.LikelyCompatible,
            Math.Min(decisionConfidence, 0.75),
            ["Nenhuma evidência explícita de incompatibilidade foi detectada."],
            ["Compatibilidade provável, não certificação regulatória."],
            BuildEvidenceTrail(input));
    }

    private ProfileCompatibility? EvaluateIngredientEvidence(string profile, IReadOnlyList<Evidence> ingredients)
    {
        var explicitIngredients = ingredients
            .Where(evidence => evidence.Priority >= EvidencePriority.IngredientExplicit)
            .OrderByDescending(evidence => evidence.Confidence)
            .ToList();

        foreach (var ingredient in explicitIngredients)
        {
            if (IsIngredientIncompatible(profile, ingredient.Text))
            {
                return Build(
                    profile,
                    FoodCompatibilityStatus.Incompatible,
                    Math.Max(0.85, ingredient.Confidence),
                    [$"Incompatível porque o ingrediente confirmado '{ingredient.Text}' foi detectado."],
                    [],
                    [ingredient]);
            }
        }

        foreach (var ingredient in ingredients.Where(evidence => evidence.Priority < EvidencePriority.IngredientExplicit))
        {
            if (IsIngredientIncompatible(profile, ingredient.Text))
            {
                return Build(
                    profile,
                    FoodCompatibilityStatus.LikelyIncompatible,
                    Math.Min(ingredient.Confidence, 0.65),
                    [$"Possível incompatibilidade por inferência alimentar: {ingredient.Text}."],
                    ["A evidência não é tão forte quanto ingrediente explícito ou claim regulatório."],
                    [ingredient]);
            }
        }

        return null;
    }

    private static ProfileCompatibility? EvaluateNutritionProfile(
        string profile,
        Dictionary<string, double> nutrition,
        FoodProcessingDecision processing,
        double decisionConfidence)
    {
        var sugar = GetNutrition(nutrition, "sugar", "sugars", "acucar", "açúcar");
        var carbs = GetNutrition(nutrition, "carbs", "carbohydrates", "carboidratos");
        var sodium = GetNutrition(nutrition, "sodium", "sodio", "sódio");
        var protein = GetNutrition(nutrition, "protein", "proteina", "proteína");
        var saturatedFat = GetNutrition(nutrition, "saturated_fat", "gordura_saturada");

        if (profile == "diabetic" && (sugar >= 15 || carbs >= 45))
            return Build(profile, FoodCompatibilityStatus.LikelyIncompatible, decisionConfidence, ["Açúcar ou carboidratos elevados não favorecem perfil diabético."], [], []);

        if (profile == "hypertension" && sodium >= 600)
            return Build(profile, FoodCompatibilityStatus.LikelyIncompatible, decisionConfidence, ["Sódio elevado não favorece perfil de hipertensão."], [], []);

        if ((profile == "low_carb" || profile == "keto") && carbs >= (profile == "keto" ? 10 : 25))
            return Build(profile, FoodCompatibilityStatus.LikelyIncompatible, decisionConfidence, ["Carboidratos acima do esperado para este perfil."], [], []);

        if (profile == "weight_loss" && (processing.Level == ProcessingLevel.UltraProcessed || sugar >= 15 || saturatedFat >= 6))
            return Build(profile, FoodCompatibilityStatus.Uncertain, decisionConfidence, ["Processamento, açúcar ou gordura saturada pedem cautela para emagrecimento."], [], []);

        if (profile == "muscle_gain" && protein >= 10 && sugar < 15 && processing.Level != ProcessingLevel.UltraProcessed)
            return Build(profile, FoodCompatibilityStatus.LikelyCompatible, decisionConfidence, ["Proteína adequada sem sinais nutricionais críticos detectados."], [], []);

        if (profile == "child" && (processing.Level == ProcessingLevel.UltraProcessed || sugar >= 15 || sodium >= 600))
            return Build(profile, FoodCompatibilityStatus.LikelyIncompatible, decisionConfidence, ["Produto exige cautela para público infantil por açúcar, sódio ou ultraprocessamento."], [], []);

        return null;
    }

    private bool IsIngredientIncompatible(string profile, string ingredient)
    {
        return profile switch
        {
            "gluten_free" => _knowledgeBase.ContainsGluten(ingredient) || ContainsAny(ingredient, ["trigo", "cevada", "centeio", "malte", "gluten"]),
            "lactose_free" => _knowledgeBase.ContainsLactose(ingredient) || ContainsAny(ingredient, ["leite", "lactose", "soro de leite", "caseina"]),
            "vegan" => !_knowledgeBase.IsVeganCompatible(ingredient),
            "vegetarian" => !_knowledgeBase.IsVegetarianCompatible(ingredient),
            "diabetic" => ContainsAny(ingredient, ["acucar", "xarope de milho", "xarope de glicose", "maltodextrina", "dextrose"]),
            _ => false
        };
    }

    private static IReadOnlyList<Evidence> BuildEvidenceTrail(DecisionInput input) =>
        input.RegulatoryInformation.Select(claim => claim.Evidence)
            .Concat(input.ExplicitIngredients)
            .Concat(input.SemanticInferences)
            .OrderByDescending(evidence => evidence.Priority)
            .ThenByDescending(evidence => evidence.Confidence)
            .Take(8)
            .ToList();

    internal static string NormalizeProfile(string profile)
    {
        var normalized = IngredientTextNormalizer.Normalize(profile).Replace("-", "_").Replace(" ", "_");
        return normalized switch
        {
            "glutenfree" or "sem_gluten" or "celiaco" or "celiac" => "gluten_free",
            "lactosefree" or "sem_lactose" or "intolerancia_lactose" => "lactose_free",
            "vegano" => "vegan",
            "vegetariano" => "vegetarian",
            "diabetico" => "diabetic",
            "hipertensao" or "hipertenso" => "hypertension",
            "emagrecimento" => "weight_loss",
            "ganho_de_massa" or "massa_muscular" => "muscle_gain",
            "infantil" or "crianca" => "child",
            _ => normalized
        };
    }

    private static double GetNutrition(Dictionary<string, double> nutrition, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (nutrition.TryGetValue(key, out var value))
                return value;
        }

        return 0d;
    }

    internal static bool ContainsAny(string value, IEnumerable<string> terms)
    {
        var normalized = IngredientTextNormalizer.Normalize(value);
        return terms.Any(term => normalized.Contains(IngredientTextNormalizer.Normalize(term), StringComparison.OrdinalIgnoreCase));
    }

    private static ProfileCompatibility WithProfile(ProfileCompatibility compatibility, string profile) =>
        new()
        {
            ProfileName = profile,
            Status = compatibility.Status,
            Confidence = compatibility.Confidence,
            Reasons = compatibility.Reasons,
            Warnings = compatibility.Warnings,
            SupportingEvidence = compatibility.SupportingEvidence
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
