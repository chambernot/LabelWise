using LabelWise.Application.DTOs.IngredientAnalysis;

namespace LabelWise.Infrastructure.Services.IngredientAnalysis;

public sealed class AssistantSummaryBuilder
{
    public AssistantSummaryDto Build(
        DietProfilesDto dietProfiles,
        IReadOnlyList<AllergenRiskDto> allergenRisks,
        IReadOnlyList<ClaimDetectionDto> claims,
        ProcessingLevelDto processingLevel,
        LabelWise.Application.DTOs.Nutrition.IngredientContextDto ingredientContext,
        IReadOnlyList<string> ingredients,
        IReadOnlyList<PositiveIngredientDto> positiveIngredients,
        AnalysisCompletenessDto analysisCompleteness,
        string overallConfidence)
    {
        var highlights = new List<string>();
        var warnings = new List<string>();
        var evidence = new List<SemanticEvidenceDto>();

        evidence.AddRange(dietProfiles.Vegan.Evidence);
        evidence.AddRange(dietProfiles.LactoseFree.Evidence);
        evidence.AddRange(dietProfiles.GlutenFree.Evidence);
        evidence.AddRange(allergenRisks.SelectMany(risk => risk.SemanticEvidence));
        evidence.AddRange(claims.SelectMany(claim => claim.Evidence));

        AddCompatibilityText("vegana", dietProfiles.Vegan, highlights, warnings);
        AddCompatibilityText("sem lactose", dietProfiles.LactoseFree, highlights, warnings);
        AddCompatibilityText("sem glúten", dietProfiles.GlutenFree, highlights, warnings);

        if (dietProfiles.DiabeticFriendly.CompatibilityStatus == DietCompatibilityStatuses.LikelyNotCompatible)
            warnings.Add("Compatibilidade para diabéticos é provavelmente baixa por ingrediente açucarado detectado.");
        else if (dietProfiles.DiabeticFriendly.CompatibilityStatus == DietCompatibilityStatuses.Uncertain)
            warnings.Add("Compatibilidade para diabéticos não conclusiva sem dados nutricionais suficientes.");

        if (positiveIngredients.Count > 0)
            highlights.Add($"inclui {JoinHumanized(positiveIngredients.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToList())}");

        var naturalMain = ingredients.FirstOrDefault(item => IngredientDictionary.NaturalPredominantTerms.Any(term => IngredientTextNormalizer.ContainsAny(item, [term])));
        if (!string.IsNullOrWhiteSpace(naturalMain) && ingredientContext.FoodNature is "natural" or "minimally_processed")
            highlights.Add($"base de {naturalMain.ToLowerInvariant()}");

        if (processingLevel.Value == "ultra_processed")
            warnings.Add(HumanizeProcessingWarning(processingLevel));
        else if (processingLevel.Value == "processed")
            warnings.Add("Apresenta sinais de processamento alimentar.");
        else if (processingLevel.Value == "minimally_processed" && processingLevel.Reasons.Any(reason => reason.Contains("aditivo", StringComparison.OrdinalIgnoreCase)))
            warnings.Add("Apesar de conter aditivo, o produto parece pouco processado.");

        if (analysisCompleteness.Status != "complete")
            warnings.Add("a leitura parece parcial; evite interpretar como garantia absoluta");

        foreach (var risk in allergenRisks.Where(risk => risk.RiskType is "may_contain" or "cross_contamination" && risk.AllergenSeverity.RegulatoryLevel != "low"))
            warnings.Add($"Pode conter {risk.Name} por possível contaminação cruzada.");

        foreach (var risk in allergenRisks.Where(risk => risk.RiskType == "contains"))
        {
            if (risk.AllergenSeverity.RegulatoryLevel == "low")
                warnings.Add($"{risk.Name} foi identificado como ingrediente sensível de baixo risco regulatório.");
            else
                warnings.Add($"Contém {risk.Name}.");
        }

        var summary = BuildText(highlights, warnings, allergenRisks, analysisCompleteness.Status);

        return new AssistantSummaryDto
        {
            Text = summary,
            Confidence = overallConfidence,
            Highlights = highlights,
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            EvidenceTypes = evidence.Select(item => item.EvidenceType).Distinct().ToList(),
            Evidence = evidence
                .GroupBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(12)
                .ToList()
        };
    }

    private static void AddCompatibilityText(string label, DietProfileCompatibilityDto profile, List<string> highlights, List<string> warnings)
    {
        var hasExplicitClaim = profile.EvidenceTypes.Contains(EvidenceType.ClaimDetected) || profile.EvidenceTypes.Contains(EvidenceType.CrossContamination);
        switch (profile.CompatibilityStatus)
        {
            case DietCompatibilityStatuses.ConfirmedCompatible:
                if (hasExplicitClaim)
                    highlights.Add($"compatibilidade {label} confirmada por claim visível");
                break;
            case DietCompatibilityStatuses.LikelyCompatible:
                if (hasExplicitClaim && profile.Confidence is "medium" or "high")
                    highlights.Add($"provavelmente compatível com perfil {label}");
                else
                    warnings.Add(BuildWeakCompatibilityEvidenceText(label));
                break;
            case DietCompatibilityStatuses.LikelyNotCompatible:
                warnings.Add($"Compatibilidade {label} provavelmente baixa: {profile.Reasons.FirstOrDefault() ?? "há evidência relevante"}.");
                break;
            case DietCompatibilityStatuses.NotCompatible:
                warnings.Add($"Incompatível com perfil {label}: {profile.Reasons.FirstOrDefault() ?? "ingrediente confirmado"}.");
                break;
            default:
                warnings.Add($"Compatibilidade {label} incerta.");
                break;
        }
    }

    private static string BuildWeakCompatibilityEvidenceText(string label)
    {
        if (label.Contains("lactose", StringComparison.OrdinalIgnoreCase))
            return "Não foram identificados ingredientes lácteos na lista visível, mas não há claim explícito de ausência de lactose.";
        if (label.Contains("vegana", StringComparison.OrdinalIgnoreCase))
            return "Não foram identificados ingredientes animais na lista visível, mas não há certificação ou claim vegano explícito.";
        if (label.Contains("glúten", StringComparison.OrdinalIgnoreCase) || label.Contains("gluten", StringComparison.OrdinalIgnoreCase))
            return "Não foram identificadas fontes comuns de glúten na lista visível, mas não há claim explícito de ausência de glúten.";

        return "Não há evidência explícita suficiente para confirmar compatibilidade alimentar.";
    }

    private static string BuildText(
        IReadOnlyList<string> highlights,
        IReadOnlyList<string> warnings,
        IReadOnlyList<AllergenRiskDto> allergenRisks,
        string completenessStatus)
    {
        if (highlights.Count == 0 && warnings.Count == 0)
            return completenessStatus == "complete"
                ? "Não foram detectados alertas alimentares relevantes na lista visível."
                : "A leitura parece parcial; não há evidência suficiente para uma conclusão forte.";

        var containedAllergens = allergenRisks
            .Where(risk => risk.RiskType == "contains")
            .Select(risk => risk.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();
        var mayContainAllergens = allergenRisks
            .Where(risk => risk.RiskType is "may_contain" or "cross_contamination")
            .Select(risk => risk.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();

        if (containedAllergens.Count > 0 || mayContainAllergens.Count > 0)
        {
            var parts = new List<string>();
            if (containedAllergens.Count > 0)
                parts.Add($"O produto contém {JoinHumanized(containedAllergens)}.");
            if (mayContainAllergens.Count > 0)
                parts.Add($"Também há alerta de possível contaminação cruzada com {JoinHumanized(mayContainAllergens)}.");

            var nonAllergenWarning = warnings
                .Where(warning => !warning.StartsWith("Contém ", StringComparison.OrdinalIgnoreCase) &&
                                  !warning.StartsWith("Pode conter ", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(WarningPriority)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(nonAllergenWarning))
                parts.Add(nonAllergenWarning.Trim().TrimEnd('.') + ".");

            if (completenessStatus != "complete")
                parts.Add("Como a análise é parcial, evite usar isso como garantia absoluta.");
            else
                parts.Add("Avalie conforme seu perfil alimentar.");

            return string.Join(" ", parts);
        }

        var mainWarnings = warnings
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(WarningPriority)
            .Take(3)
            .ToList();
        var mainHighlights = highlights.Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToList();

        var text = mainWarnings.Count > 0
            ? $"{JoinSentences(mainWarnings)}"
            : $"Produto {JoinHumanized(mainHighlights)}.";

        if (mainHighlights.Count > 0 && mainWarnings.Count > 0)
            text += $" Também é {JoinHumanized(mainHighlights)}.";

        if (completenessStatus != "complete")
            text += " Como a análise é parcial, evite usar isso como garantia absoluta.";
        else if (mainWarnings.Count > 0)
            text += " Avalie conforme seu perfil alimentar.";

        return text;
    }

    private static int WarningPriority(string warning)
    {
        if (warning.StartsWith("Contém ", StringComparison.OrdinalIgnoreCase)) return 0;
        if (warning.StartsWith("Pode conter ", StringComparison.OrdinalIgnoreCase)) return 1;
        if (warning.Contains("ultraprocess", StringComparison.OrdinalIgnoreCase) || warning.Contains("aditivo", StringComparison.OrdinalIgnoreCase)) return 2;
        return 3;
    }

    private static string JoinSentences(IReadOnlyList<string> values) =>
        string.Join(" ", values.Select(value => value.Trim().TrimEnd('.') + "."));

    private static string HumanizeProcessingWarning(ProcessingLevelDto processingLevel)
    {
        if (processingLevel.Reasons.Any(reason => reason.Contains("adoçante", StringComparison.OrdinalIgnoreCase)) &&
            processingLevel.Reasons.Any(reason => reason.Contains("conservante", StringComparison.OrdinalIgnoreCase)))
        {
            return "Contém adoçantes e conservantes artificiais.";
        }

        return "Contém múltiplos aditivos artificiais.";
    }

    private static string JoinHumanized(IReadOnlyList<string> values)
    {
        if (values.Count == 1)
            return values[0];

        return string.Join(", ", values.Take(values.Count - 1)) + " e " + values[^1];
    }
}
