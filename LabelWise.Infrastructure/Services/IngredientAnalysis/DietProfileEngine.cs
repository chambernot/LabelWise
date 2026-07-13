using LabelWise.Application.DTOs.IngredientAnalysis;

namespace LabelWise.Infrastructure.Services.IngredientAnalysis;

public sealed class DietProfileEngine
{
    public DietProfilesDto Evaluate(IReadOnlyList<string> ingredients, IReadOnlyList<string> allergens, IReadOnlyList<string> claims)
    {
        var allergenRisks = allergens
            .Select(allergen => new AllergenRiskDto { Name = allergen, RiskType = "contains", Confidence = "high" })
            .ToList();

        return Evaluate(ingredients, allergenRisks, claims);
    }

    public DietProfilesDto Evaluate(IReadOnlyList<string> ingredients, IReadOnlyList<AllergenRiskDto> allergenRisks, IReadOnlyList<string> claims)
    {
        var containedAllergens = allergenRisks
            .Where(risk => risk.RiskType == "contains")
            .Select(risk => risk.Name)
            .ToList();
        var mayContainAllergens = allergenRisks
            .Where(risk => risk.RiskType is "may_contain" or "cross_contamination")
            .Select(risk => risk.Name)
            .ToList();
        var directClaimSources = claims
            .Where(claim => !IsCrossContaminationClaim(claim))
            .ToList();
        var sources = ingredients.Concat(containedAllergens).Concat(directClaimSources).ToList();
        var hasReadableEvidence = ingredients.Concat(claims).Concat(allergenRisks.SelectMany(risk => risk.Evidence)).Any(source => !string.IsNullOrWhiteSpace(source));

        return new DietProfilesDto
        {
            Vegan = EvaluateVegan(
                ingredients,
                containedAllergens,
                mayContainAllergens,
                claims,
                hasReadableEvidence),

            Vegetarian = EvaluateBlockedTerms(
                sources,
                IngredientDictionary.VegetarianBlockedTerms,
                positiveClaims: ["vegetariano", "vegano", "plant based"],
                defaultReason: "Não foram detectados carne, peixe ou gelatina.",
                hasReadableEvidence),

            LactoseFree = EvaluateLactoseFree(ingredients, claims, containedAllergens, mayContainAllergens),
            GlutenFree = EvaluateGlutenFree(ingredients, claims, containedAllergens, mayContainAllergens),
            DiabeticFriendly = EvaluateDiabeticFriendly(sources, claims)
        };
    }

    private static DietProfileCompatibilityDto EvaluateBlockedTerms(
        IReadOnlyList<string> sources,
        IReadOnlyList<string> blockedTerms,
        IReadOnlyList<string> positiveClaims,
        string defaultReason,
        bool hasReadableEvidence)
    {
        var matches = FindMatches(sources, blockedTerms);
        if (matches.Count > 0)
        {
            return BuildProfile(false, "high", "blocked", DietCompatibilityStatuses.NotCompatible, matches.Select(match => $"Contém {match}").ToList(), reasonSources: ["ingredient_detected"], evidenceTypes: [EvidenceType.IngredientDetected]);
        }

        var claim = FindMatches(sources, positiveClaims).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(claim))
            return BuildProfile(true, "high", "high", DietCompatibilityStatuses.ConfirmedCompatible, [$"Claim visível: {claim}"], reasonSources: ["claim_detected"], evidenceTypes: [EvidenceType.ClaimDetected]);

        if (!hasReadableEvidence)
            return BuildProfile(false, "low", "unknown", DietCompatibilityStatuses.Uncertain, ["Não há texto suficiente para uma conclusão segura."], ["Análise parcial; não trate ausência de detecção como garantia."], ["ocr_text"], [EvidenceType.OcrInference]);

        return BuildProfile(true, "low", "low", DietCompatibilityStatuses.LikelyCompatible, [defaultReason], reasonSources: ["ingredient_detected", "openai_inference"], evidenceTypes: [EvidenceType.IngredientDetected, EvidenceType.OpenAiInference]);
    }

    private static DietProfileCompatibilityDto EvaluateVegan(
        IReadOnlyList<string> ingredients,
        IReadOnlyList<string> containedAllergens,
        IReadOnlyList<string> mayContainAllergens,
        IReadOnlyList<string> claims,
        bool hasReadableEvidence)
    {
        var directMatches = FindMatches(ingredients, IngredientDictionary.VeganBlockedTerms);
        if (directMatches.Count > 0)
            return BuildProfile(false, "high", "blocked", DietCompatibilityStatuses.NotCompatible, directMatches.Select(match => $"Contém ingrediente de origem animal: {match}").ToList(), reasonSources: ["ingredient_detected"], evidenceTypes: [EvidenceType.IngredientDetected]);

        var lactoseClaimMatches = claims
            .Where(claim => !IsCrossContaminationClaim(claim) && FindMatches([claim], ["contém lactose", "contem lactose"]).Count > 0)
            .ToList();
        if (lactoseClaimMatches.Count > 0)
            return BuildProfile(false, "high", "attention", DietCompatibilityStatuses.LikelyNotCompatible, ["Claim regulatório indica presença de lactose."], ["Lactose sugere derivado lácteo, mas não substitui ingrediente animal explícito na lista."], ["claim_detected"], [EvidenceType.ClaimDetected]);

        var containedAnimalAllergens = FindMatches(containedAllergens, IngredientDictionary.VeganBlockedTerms.Where(term => !IngredientTextNormalizer.ContainsAny(term, ["lactose"])).ToList());
        if (containedAnimalAllergens.Count > 0)
            return BuildProfile(false, "high", "blocked", DietCompatibilityStatuses.NotCompatible, containedAnimalAllergens.Select(match => $"Claim regulatório indica presença de {HumanizeAllergen(match)}.").ToList(), reasonSources: ["claim_detected"], evidenceTypes: [EvidenceType.ClaimDetected]);

        var crossContaminationMatches = FindMatches(mayContainAllergens, IngredientDictionary.VeganBlockedTerms);
        if (crossContaminationMatches.Count == 0)
        {
            crossContaminationMatches = claims
                .Where(IsCrossContaminationClaim)
                .Where(claim => IngredientTextNormalizer.ContainsAny(claim, IngredientDictionary.VeganBlockedTerms) || IngredientTextNormalizer.ContainsAny(claim, ["derivados animais", "derivado animal"]))
                .SelectMany(claim => IngredientDictionary.VeganBlockedTerms.Where(term => IngredientTextNormalizer.ContainsAny(claim, [term])))
                .GroupBy(IngredientTextNormalizer.Normalize)
                .Select(group => group.First())
                .ToList();
        }

        if (crossContaminationMatches.Count > 0)
        {
            var warnings = crossContaminationMatches
                .Select(match => $"Pode haver contaminação cruzada com {HumanizeAllergen(match)}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return BuildProfile(
                false,
                "medium",
                "unknown",
                DietCompatibilityStatuses.Uncertain,
                ["Não foram detectados ingredientes animais diretos, mas há alerta de possível presença."],
                warnings,
                ["claim_detected", "cross_contamination"],
                [EvidenceType.CrossContamination]);
        }

        var claim = FindMatches(ingredients.Concat(claims).ToList(), ["vegano", "plant based"]).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(claim))
            return BuildProfile(true, "high", "high", DietCompatibilityStatuses.ConfirmedCompatible, [$"Claim visível: {claim}"], reasonSources: ["claim_detected"], evidenceTypes: [EvidenceType.ClaimDetected]);

        if (!hasReadableEvidence)
            return BuildProfile(false, "low", "unknown", DietCompatibilityStatuses.Uncertain, ["Não há texto suficiente para uma conclusão segura."], ["Análise parcial; não trate ausência de detecção como garantia."], ["ocr_text"], [EvidenceType.OcrInference]);

        return BuildProfile(true, "low", "low", DietCompatibilityStatuses.LikelyCompatible, ["Não foram identificados ingredientes animais na lista visível."], reasonSources: ["ingredient_detected", "openai_inference"], evidenceTypes: [EvidenceType.IngredientDetected, EvidenceType.OpenAiInference]);
    }

    private static DietProfileCompatibilityDto EvaluateLactoseFree(
        IReadOnlyList<string> sources,
        IReadOnlyList<string> claims,
        IReadOnlyList<string> containedAllergens,
        IReadOnlyList<string> mayContainAllergens)
    {
        if (FindMatches(containedAllergens, ["leite", "lactose"]).Count > 0)
            return BuildProfile(false, "high", "blocked", DietCompatibilityStatuses.NotCompatible, ["Contém leite, lactose ou derivados."], reasonSources: ["claim_detected"], evidenceTypes: [EvidenceType.ClaimDetected]);

        if (FindMatches(mayContainAllergens, ["leite", "lactose"]).Count > 0)
            return BuildProfile(false, "medium", "unknown", DietCompatibilityStatuses.Uncertain, ["Não há leite como ingrediente detectado."], ["Pode conter leite ou lactose por contaminação cruzada."], ["claim_detected", "cross_contamination"], [EvidenceType.CrossContamination]);

        if (FindMatches(claims, ["não contém lactose", "nao contem lactose", "sem lactose", "zero lactose"]).Count > 0)
            return BuildProfile(true, "high", "high", DietCompatibilityStatuses.ConfirmedCompatible, ["Claim visível indica ausência de lactose."], reasonSources: ["claim_detected"], evidenceTypes: [EvidenceType.ClaimDetected]);

        var matches = FindMatches(sources, ["lactose", "leite", "whey", "caseína", "caseina", "caseinato", "proteína láctea", "proteina lactea", "soro de leite", "leite em pó", "leite em po"]);
        return matches.Count > 0
            ? BuildProfile(false, "medium", "blocked", DietCompatibilityStatuses.NotCompatible, matches.Select(match => $"Contém derivado do leite: {match}").ToList(), reasonSources: ["ingredient_detected"], evidenceTypes: [EvidenceType.IngredientDetected])
            : BuildProfile(true, "low", "low", DietCompatibilityStatuses.LikelyCompatible, ["Não foram identificados ingredientes lácteos na lista visível."], reasonSources: ["ingredient_detected", "openai_inference"], evidenceTypes: [EvidenceType.IngredientDetected, EvidenceType.OpenAiInference]);
    }

    private static DietProfileCompatibilityDto EvaluateGlutenFree(
        IReadOnlyList<string> sources,
        IReadOnlyList<string> claims,
        IReadOnlyList<string> containedAllergens,
        IReadOnlyList<string> mayContainAllergens)
    {
        if (claims.Any(IsExplicitContainsGlutenClaim))
        {
            return BuildProfile(false, "very_high", "blocked", DietCompatibilityStatuses.NotCompatible, ["Declaração explícita 'CONTÉM GLÚTEN' encontrada."], reasonSources: ["claim_detected"], evidenceTypes: [EvidenceType.ClaimDetected]);
        }

        if (FindMatches(containedAllergens, ["glúten", "gluten", "trigo", "cevada", "malte"]).Count > 0)
            return BuildProfile(false, "high", "blocked", DietCompatibilityStatuses.NotCompatible, ["Contém glúten ou fonte de glúten."], reasonSources: ["claim_detected"], evidenceTypes: [EvidenceType.ClaimDetected]);

        if (FindMatches(mayContainAllergens, ["glúten", "gluten", "trigo", "cevada", "malte"]).Count > 0)
            return BuildProfile(false, "medium", "unknown", DietCompatibilityStatuses.Uncertain, ["Não há glúten como ingrediente detectado."], ["Pode conter glúten por contaminação cruzada."], ["claim_detected", "cross_contamination"], [EvidenceType.CrossContamination]);

        if (FindMatches(claims, ["não contém glúten", "nao contem gluten", "sem glúten", "sem gluten"]).Count > 0)
            return BuildProfile(true, "high", "high", DietCompatibilityStatuses.ConfirmedCompatible, ["Claim visível indica ausência de glúten."], reasonSources: ["claim_detected"], evidenceTypes: [EvidenceType.ClaimDetected]);

        var matches = FindMatches(sources, ["glúten", "gluten", "trigo", "cevada", "centeio", "malte", "farinha de trigo"]);
        return matches.Count > 0
            ? BuildProfile(false, "medium", "blocked", DietCompatibilityStatuses.NotCompatible, matches.Select(match => $"Contém fonte de glúten: {match}").ToList(), reasonSources: ["ingredient_detected"], evidenceTypes: [EvidenceType.IngredientDetected])
            : BuildProfile(true, "low", "low", DietCompatibilityStatuses.LikelyCompatible, ["Não foram identificadas fontes comuns de glúten na lista visível."], reasonSources: ["ingredient_detected", "openai_inference"], evidenceTypes: [EvidenceType.IngredientDetected, EvidenceType.OpenAiInference]);
    }

    private static DietProfileCompatibilityDto EvaluateDiabeticFriendly(IReadOnlyList<string> sources, IReadOnlyList<string> claims)
    {
        if (FindMatches(claims, ["sem açúcar", "sem acucar", "zero açúcar", "zero acucar", "não contém açúcar", "nao contem acucar"]).Count > 0)
            return BuildProfile(false, "low", "unknown", DietCompatibilityStatuses.Uncertain, ["Claim visível indica ausência de açúcar."], ["Compatibilidade para diabéticos não conclusiva sem tabela nutricional."], ["claim_detected", "nutrition_inference"], [EvidenceType.ClaimDetected, EvidenceType.NutritionInference]);

        var sugarSources = sources
            .Where(source => !IngredientClassifier.IsContextualSugarReference(source))
            .ToList();
        var matches = FindMatches(sugarSources, IngredientDictionary.SugarTerms);
        return matches.Count > 0
            ? BuildProfile(false, "medium", "attention", DietCompatibilityStatuses.LikelyNotCompatible, matches.Select(match => $"Contém ingrediente açucarado: {match}").ToList(), reasonSources: ["ingredient_detected", "nutrition_inference"], evidenceTypes: [EvidenceType.IngredientDetected, EvidenceType.NutritionInference])
            : BuildProfile(false, "low", "unknown", DietCompatibilityStatuses.Uncertain, ["Não há dados nutricionais neste fluxo; compatibilidade para diabetes é apenas indicativa."], reasonSources: ["nutrition_inference"], evidenceTypes: [EvidenceType.NutritionInference]);
    }

    private static List<string> FindMatches(IReadOnlyList<string> sources, IReadOnlyList<string> terms) =>
        terms
            .Where(term => sources.Any(source => IngredientTextNormalizer.ContainsAny(source, [term])))
            .GroupBy(IngredientTextNormalizer.Normalize)
            .Select(group => group.First())
            .ToList();

    private static bool IsCrossContaminationClaim(string claim)
    {
        var normalized = IngredientTextNormalizer.Normalize(claim);
        return normalized.Contains("pode conter", StringComparison.Ordinal) ||
               normalized.Contains("tracos de", StringComparison.Ordinal) ||
               normalized.Contains("fabricado em equipamento", StringComparison.Ordinal) ||
               normalized.Contains("fabricado em linha", StringComparison.Ordinal);
    }

    private static bool IsExplicitContainsGlutenClaim(string claim)
    {
        var normalized = IngredientTextNormalizer.Normalize(claim);
        if (normalized.Contains("nao contem gluten", StringComparison.Ordinal) ||
            normalized.Contains("sem gluten", StringComparison.Ordinal) ||
            normalized.Contains("livre de gluten", StringComparison.Ordinal))
        {
            return false;
        }

        return normalized.StartsWith("contem gluten", StringComparison.Ordinal) ||
            normalized.Contains(" contem gluten", StringComparison.Ordinal);
    }

    private static string HumanizeAllergen(string value)
    {
        var normalized = IngredientTextNormalizer.Normalize(value);
        if (normalized.Contains("ovo", StringComparison.Ordinal)) return "ovo";
        if (normalized.Contains("leite", StringComparison.Ordinal) || normalized.Contains("lactose", StringComparison.Ordinal)) return "leite";
        return value;
    }

    private static DietProfileCompatibilityDto Compatible(string confidence, string compatibilityLevel, string compatibilityStatus, List<string> reasons, List<string>? warnings = null, List<string>? reasonSources = null) =>
        BuildProfile(compatibilityStatus is DietCompatibilityStatuses.ConfirmedCompatible or DietCompatibilityStatuses.LikelyCompatible, confidence, compatibilityLevel, compatibilityStatus, reasons, warnings, reasonSources, ResolveEvidenceTypes(reasonSources));

    private static DietProfileCompatibilityDto NotCompatible(string confidence, string compatibilityLevel, List<string> reasons, List<string>? warnings = null, List<string>? reasonSources = null) =>
        BuildProfile(false, confidence, compatibilityLevel, DietCompatibilityStatuses.NotCompatible, reasons, warnings, reasonSources, ResolveEvidenceTypes(reasonSources));

    private static DietProfileCompatibilityDto BuildProfile(
        bool compatible,
        string confidence,
        string compatibilityLevel,
        string compatibilityStatus,
        List<string> reasons,
        List<string>? warnings = null,
        List<string>? reasonSources = null,
        List<EvidenceType>? evidenceTypes = null)
    {
        var resolvedEvidenceTypes = evidenceTypes is { Count: > 0 }
            ? evidenceTypes.Distinct().ToList()
            : ResolveEvidenceTypes(reasonSources);

        var calibratedConfidence = ApplyConfidenceCeiling(confidence, resolvedEvidenceTypes);

        return new DietProfileCompatibilityDto
        {
            Compatible = compatible,
            CompatibilityLevel = compatibilityLevel,
            CompatibilityStatus = compatibilityStatus,
            Status = ToCompatibilityStatus(compatibilityStatus),
            Confidence = calibratedConfidence,
            Reasons = reasons,
            Warnings = warnings ?? new(),
            ReasonSources = reasonSources ?? new(),
            EvidenceTypes = resolvedEvidenceTypes,
            Evidence = reasons.Select(reason => new SemanticEvidenceDto
            {
                EvidenceType = resolvedEvidenceTypes.FirstOrDefault(),
                Type = resolvedEvidenceTypes.FirstOrDefault().ToString(),
                Text = reason,
                Confidence = calibratedConfidence,
                Source = compatibilityStatus,
                TrustLevel = ResolveTrustLevel(resolvedEvidenceTypes),
                OriginBlock = ResolveOriginBlock(resolvedEvidenceTypes)
            }).ToList()
        };
    }

    private static string ApplyConfidenceCeiling(string confidence, IReadOnlyList<EvidenceType> evidenceTypes)
    {
        var ceiling = ResolveConfidenceCeiling(evidenceTypes);
        return ConfidenceRank(confidence) <= ConfidenceRank(ceiling) ? confidence : ceiling;
    }

    private static string ResolveConfidenceCeiling(IReadOnlyList<EvidenceType> evidenceTypes)
    {
        if (evidenceTypes.Contains(EvidenceType.ClaimDetected) || evidenceTypes.Contains(EvidenceType.CrossContamination)) return "high";
        if (evidenceTypes.Contains(EvidenceType.IngredientDetected)) return "medium";
        if (evidenceTypes.Contains(EvidenceType.OcrInference) || evidenceTypes.Contains(EvidenceType.OpenAiInference) || evidenceTypes.Contains(EvidenceType.NutritionInference)) return "low";
        return "very_low";
    }

    private static int ConfidenceRank(string confidence) => confidence switch
    {
        "high" => 4,
        "medium" => 3,
        "low" => 2,
        "very_low" => 1,
        _ => 0
    };

    private static EvidenceTrustLevel ResolveTrustLevel(IReadOnlyList<EvidenceType> evidenceTypes)
    {
        if (evidenceTypes.Contains(EvidenceType.ClaimDetected) || evidenceTypes.Contains(EvidenceType.CrossContamination)) return EvidenceTrustLevel.ExplicitRegulatoryClaim;
        if (evidenceTypes.Contains(EvidenceType.IngredientDetected)) return EvidenceTrustLevel.ExplicitIngredient;
        if (evidenceTypes.Contains(EvidenceType.OcrInference)) return EvidenceTrustLevel.StructuredText;
        if (evidenceTypes.Contains(EvidenceType.OpenAiInference) || evidenceTypes.Contains(EvidenceType.NutritionInference)) return EvidenceTrustLevel.SemanticInference;
        return EvidenceTrustLevel.Unknown;
    }

    private static string ResolveOriginBlock(IReadOnlyList<EvidenceType> evidenceTypes)
    {
        if (evidenceTypes.Contains(EvidenceType.ClaimDetected) || evidenceTypes.Contains(EvidenceType.CrossContamination)) return "RegulatoryClaimBlock";
        if (evidenceTypes.Contains(EvidenceType.IngredientDetected)) return "IngredientBlock";
        if (evidenceTypes.Contains(EvidenceType.OcrInference)) return "StructuredOCR";
        return "SemanticInferenceLayer";
    }

    private static List<EvidenceType> ResolveEvidenceTypes(List<string>? reasonSources) =>
        (reasonSources ?? new())
            .Select(source => source switch
            {
                "ingredient_detected" => EvidenceType.IngredientDetected,
                "claim_detected" => EvidenceType.ClaimDetected,
                "cross_contamination" => EvidenceType.CrossContamination,
                "openai_inference" => EvidenceType.OpenAiInference,
                "nutrition_inference" => EvidenceType.NutritionInference,
                _ => EvidenceType.OcrInference
            })
            .Distinct()
            .DefaultIfEmpty(EvidenceType.OcrInference)
            .ToList();

    private static CompatibilityStatus ToCompatibilityStatus(string status) =>
        status switch
        {
            DietCompatibilityStatuses.ConfirmedCompatible => CompatibilityStatus.ConfirmedCompatible,
            DietCompatibilityStatuses.LikelyCompatible => CompatibilityStatus.LikelyCompatible,
            DietCompatibilityStatuses.LikelyNotCompatible or DietCompatibilityStatuses.Attention => CompatibilityStatus.LikelyNotCompatible,
            DietCompatibilityStatuses.NotCompatible => CompatibilityStatus.NotCompatible,
            _ => CompatibilityStatus.Uncertain
        };
}
