using LabelWise.Application.DTOs.IngredientAnalysis;
using LabelWise.Application.Models.IngredientAnalysis;

namespace LabelWise.Infrastructure.Services.IngredientAnalysis;

public sealed class AllergenDetector
{
    public List<string> Detect(IngredientAnalysisContext context, IReadOnlyList<string> ingredients, IReadOnlyList<string> claims)
    {
        return DetectRisks(context, ingredients, claims)
            .Where(risk => risk.RiskType == "contains")
            .Select(risk => risk.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public List<AllergenRiskDto> DetectRisks(IngredientAnalysisContext context, IReadOnlyList<string> ingredients, IReadOnlyList<string> claims)
    {
        var detected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var risks = new Dictionary<string, AllergenRiskDto>(StringComparer.OrdinalIgnoreCase);
        var absenceClaims = DetectAbsenceClaims(claims);

        var allSourcesText = string.Join("\n", context.VisionExtraction.Claims.Concat(claims).Concat(context.VisionExtraction.RawExtractedText));

        foreach (var allergen in context.VisionExtraction.Allergens)
        {
            if (string.IsNullOrWhiteSpace(allergen))
                continue;

                var canonical = ResolveCanonicalAllergen(allergen.Trim()) ?? allergen.Trim();
                if (!absenceClaims.Contains(canonical))
                {
                    var riskType = ResolveContextualRiskType(allSourcesText, canonical);
                    AddRisk(risks, canonical, riskType is "may_contain" or "cross_contamination" ? riskType : "possible_allergen", "low", allergen.Trim(), ResolveEvidenceType(riskType, EvidenceType.OpenAiInference));
                }
        }

        var ingredientSources = ingredients
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        var positiveClaims = claims.Where(IsPositiveAllergenClaim).ToList();

        foreach (var entry in IngredientDictionary.Allergens)
        {
            foreach (var source in ingredientSources)
            {
                if (!absenceClaims.Contains(entry.CanonicalName) && IngredientTextNormalizer.ContainsAny(source, entry.Synonyms))
                {
                    if (IsMilkDerivativeInferenceOnly(source))
                    {
                        AddRisk(risks, "lactose", "possible_lactose", "low", source, EvidenceType.OcrInference);
                    }
                    else
                    {
                        detected.Add(entry.CanonicalName);
                        AddRisk(risks, entry.CanonicalName, "contains", "high", source, EvidenceType.IngredientDetected);
                    }
                }
            }

            foreach (var claim in positiveClaims)
            {
                var riskType = ResolveRiskTypeFromClaim(claim);
                var confidence = "high";

                if (absenceClaims.Contains(entry.CanonicalName) && riskType != "may_contain")
                {
                    continue;
                }

                if (IngredientTextNormalizer.ContainsAny(claim, ["oleaginosas"]) && !IngredientTextNormalizer.ContainsAny(claim, ["castanha", "nozes", "amendoa"]))
                {
                    if (!risks.Keys.Any(k => k.Contains("castanha") || k.Contains("nozes") || k.Contains("amendoa")))
                    {
                        AddRisk(risks, "Oleaginosas", riskType, "medium", claim, ResolveEvidenceType(riskType, EvidenceType.ClaimDetected));
                    }
                }
                else if (IngredientTextNormalizer.ContainsAny(claim, entry.Synonyms))
                {
                    if (riskType == "contains")
                        detected.Add(entry.CanonicalName);

                    AddRisk(risks, entry.CanonicalName, riskType, confidence, claim, ResolveEvidenceType(riskType, EvidenceType.ClaimDetected));
                }
            }
        }

        return risks.Values
            .Where(risk => !AbsenceClaimOverridesRisk(absenceClaims, risk))
            .OrderBy(risk => risk.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(risk => risk.RiskType, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddRisk(
        IDictionary<string, AllergenRiskDto> risks,
        string name,
        string riskType,
        string confidence,
        string evidence,
        EvidenceType evidenceType)
    {
        var key = $"{IngredientTextNormalizer.Normalize(name)}:{riskType}";
        if (!risks.TryGetValue(key, out var risk))
        {
            var severity = ResolveSeverity(name, riskType);
            risk = new AllergenRiskDto
            {
                Allergen = name,
                Name = name,
                Severity = riskType is "may_contain" or "cross_contamination" ? "cross_contamination" : "contains",
                Source = evidence,
                RiskType = riskType,
                Confidence = severity.RegulatoryLevel == "low" && confidence == "high" ? "medium" : confidence,
                AllergenSeverity = severity,
                EvidenceType = evidenceType,
                EvidenceTypes = [evidenceType],
                TrustLevel = ResolveTrustLevel(evidenceType, riskType),
                OriginBlock = ResolveOriginBlock(evidenceType, riskType)
            };
            risks[key] = risk;
        }
        else if (!risk.EvidenceTypes.Contains(evidenceType))
        {
            risk.EvidenceTypes.Add(evidenceType);
        }

        if (!string.IsNullOrWhiteSpace(evidence) &&
            !risk.Evidence.Any(item => IngredientTextNormalizer.Normalize(item) == IngredientTextNormalizer.Normalize(evidence)))
        {
            risk.Evidence.Add(evidence);
            risk.SemanticEvidence.Add(new SemanticEvidenceDto
            {
                EvidenceType = evidenceType,
                Type = evidenceType.ToString(),
                Text = evidence,
                Confidence = confidence,
                    Source = riskType,
                    TrustLevel = ResolveTrustLevel(evidenceType, riskType),
                    OriginBlock = ResolveOriginBlock(evidenceType, riskType)
            });
            if (string.IsNullOrWhiteSpace(risk.Source))
                risk.Source = evidence;
        }
    }

    private static EvidenceType ResolveEvidenceType(string riskType, EvidenceType defaultType) =>
        riskType is "may_contain" or "cross_contamination"
            ? EvidenceType.CrossContamination
            : defaultType;

    private static EvidenceTrustLevel ResolveTrustLevel(EvidenceType evidenceType, string riskType) =>
        evidenceType switch
        {
            EvidenceType.ClaimDetected or EvidenceType.CrossContamination => EvidenceTrustLevel.ExplicitRegulatoryClaim,
            EvidenceType.IngredientDetected => EvidenceTrustLevel.ExplicitIngredient,
            EvidenceType.OpenAiInference => EvidenceTrustLevel.SemanticInference,
            _ when riskType.StartsWith("possible", StringComparison.OrdinalIgnoreCase) => EvidenceTrustLevel.SemanticInference,
            _ => EvidenceTrustLevel.WeakInference
        };

    private static string ResolveOriginBlock(EvidenceType evidenceType, string riskType) =>
        evidenceType switch
        {
            EvidenceType.ClaimDetected or EvidenceType.CrossContamination => "RegulatoryClaimBlock",
            EvidenceType.IngredientDetected => "IngredientBlock",
            _ when riskType.StartsWith("possible", StringComparison.OrdinalIgnoreCase) => "SemanticInferenceLayer",
            _ => "UnknownBlock"
        };

    private static HashSet<string> DetectAbsenceClaims(IReadOnlyList<string> claims)
    {
        var absenceClaims = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var claim in claims.Select(IngredientTextNormalizer.Normalize))
        {
            if ((claim.Contains("nao contem gluten", StringComparison.Ordinal) || claim.Contains("sem gluten", StringComparison.Ordinal)) &&
                !claim.Contains("pode conter", StringComparison.Ordinal))
            {
                absenceClaims.Add("glúten");
            }

            if ((claim.Contains("nao contem leite", StringComparison.Ordinal) || claim.Contains("sem leite", StringComparison.Ordinal)) &&
                !claim.Contains("pode conter", StringComparison.Ordinal))
            {
                absenceClaims.Add("leite");
            }

            if ((claim.Contains("nao contem lactose", StringComparison.Ordinal) || claim.Contains("sem lactose", StringComparison.Ordinal) || claim.Contains("zero lactose", StringComparison.Ordinal)) &&
                !claim.Contains("pode conter", StringComparison.Ordinal))
            {
                absenceClaims.Add("lactose");
                absenceClaims.Add("leite");
            }
        }

        return absenceClaims;
    }

    private static bool AbsenceClaimOverridesRisk(ISet<string> absenceClaims, AllergenRiskDto risk)
    {
        if (risk.RiskType is "may_contain" or "cross_contamination")
            return false;

        var canonical = ResolveCanonicalAllergen(risk.Name) ?? risk.Name;
        if (!absenceClaims.Contains(canonical) && !absenceClaims.Contains(risk.Name))
            return false;

        return risk.EvidenceType is EvidenceType.OpenAiInference or EvidenceType.OcrInference or EvidenceType.ClaimDetected;
    }

    private static bool IsPositiveAllergenClaim(string claim)
    {
        var normalized = IngredientTextNormalizer.Normalize(claim);
        return normalized.StartsWith("contem ", StringComparison.Ordinal) ||
               normalized.StartsWith("pode conter ", StringComparison.Ordinal) ||
               normalized.StartsWith("alergicos", StringComparison.Ordinal) ||
               normalized.StartsWith("alergenicos", StringComparison.Ordinal) ||
               normalized.StartsWith("tracos de ", StringComparison.Ordinal) ||
               normalized.Contains(" tracos de ", StringComparison.Ordinal) ||
               normalized.Contains("fabricado em equipamento", StringComparison.Ordinal) ||
               normalized.Contains("fabricado em linha", StringComparison.Ordinal);
    }

    private static string? ResolveCanonicalAllergen(string value)
    {
        foreach (var entry in IngredientDictionary.Allergens)
        {
            if (IngredientTextNormalizer.ContainsAny(value, entry.Synonyms.Concat([entry.CanonicalName])))
                return entry.CanonicalName;
        }

        return null;
    }

    private static AllergenSeverityDto ResolveSeverity(string name, string riskType)
    {
        var normalized = IngredientTextNormalizer.Normalize(name);
        var level = "low";

        if (IngredientDictionary.HighRegulatoryAllergens.Any(term => IngredientTextNormalizer.ContainsAny(normalized, [term])))
            level = "high";
        else if (IngredientDictionary.MediumRegulatoryAllergens.Any(term => IngredientTextNormalizer.ContainsAny(normalized, [term])))
            level = "medium";
        else if (IngredientDictionary.LowRegulatoryAllergens.Any(term => IngredientTextNormalizer.ContainsAny(normalized, [term])))
            level = "low";

        var baseWeight = level switch
        {
            "high" => 90,
            "medium" => 55,
            _ => 20
        };

        if (riskType is "may_contain" or "cross_contamination")
            baseWeight = Math.Max(10, baseWeight - 25);

        return new AllergenSeverityDto
        {
            RegulatoryLevel = level,
            RiskWeight = baseWeight
        };
    }

    private static string ResolveContextualRiskType(string source, string allergen)
    {
        if (string.IsNullOrWhiteSpace(source))
            return "contains";

        var normalized = IngredientTextNormalizer.Normalize(source);
        if (!IngredientTextNormalizer.ContainsAny(normalized, [allergen]))
            return "contains";

        if (normalized.Contains("fabricado em equipamento", StringComparison.Ordinal) ||
            normalized.Contains("fabricado em linha", StringComparison.Ordinal))
            return "cross_contamination";

        if (normalized.Contains("pode conter", StringComparison.Ordinal) ||
            normalized.Contains("tracos de", StringComparison.Ordinal))
            return "may_contain";

        return "contains";
    }

    private static string ResolveRiskTypeFromClaim(string claim)
    {
        var normalizedClaim = IngredientTextNormalizer.Normalize(claim);
        if (normalizedClaim.Contains("fabricado em equipamento", StringComparison.Ordinal) ||
            normalizedClaim.Contains("fabricado em linha", StringComparison.Ordinal))
            return "cross_contamination";

        if (normalizedClaim.StartsWith("pode conter ", StringComparison.Ordinal) ||
            normalizedClaim.Contains(" pode conter ", StringComparison.Ordinal) ||
            normalizedClaim.StartsWith("tracos de ", StringComparison.Ordinal) ||
            normalizedClaim.Contains(" tracos de ", StringComparison.Ordinal))
            return "may_contain";

        return "contains";
    }

    private static bool IsMilkDerivativeInferenceOnly(string source)
    {
        var normalized = IngredientTextNormalizer.Normalize(source);
        return normalized.Contains("derivado de leite", StringComparison.Ordinal) ||
            normalized.Contains("derivados de leite", StringComparison.Ordinal);
    }
}
