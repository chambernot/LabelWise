using LabelWise.Application.DTOs.IngredientAnalysis;
using System.Text.RegularExpressions;

namespace LabelWise.Infrastructure.Services.IngredientAnalysis;

public sealed class IngredientNormalizer
{
    public List<IngredientNormalizedDto> Normalize(IReadOnlyList<string> ingredients)
    {
        return ingredients
            .Select(NormalizeOne)
            .GroupBy(item => IngredientTextNormalizer.Normalize(item.Raw))
            .Select(group => group.First())
            .ToList();
    }

    private static IngredientNormalizedDto NormalizeOne(string raw)
    {
        raw = NormalizeRawFoodText(raw);
        if (IngredientTextNormalizer.ContainsAny(raw, ["ácido ascórbico", "acido ascorbico", "ascorbico"]))
        {
            return new IngredientNormalizedDto
            {
                Raw = raw,
                Normalized = "Ácido ascórbico",
                Category = "antioxidant",
                Confidence = "medium",
                DietaryRisk = "ultra_processing_marker",
                SemanticEvidence = [CreateEvidence(raw, "antioxidant")]
            };
        }

        if (IngredientTextNormalizer.ContainsAny(raw, ["proteína láctea", "proteina lactea", "proteína do leite", "proteina do leite"]))
        {
            return new IngredientNormalizedDto
            {
                Raw = raw,
                Normalized = "derivado de leite",
                Category = "milk_derivative",
                Confidence = "high",
                AnimalOrigin = true,
                DietaryRisk = "lactose_milk",
                SemanticEvidence = [CreateEvidence(raw, "milk_derivative")]
            };
        }

        var match = IngredientDictionary.IngredientNormalization.FirstOrDefault(entry =>
            MatchesAnyTerm(raw, entry.Synonyms));

        if (match is not null)
        {
            return new IngredientNormalizedDto
            {
                Raw = raw,
                Normalized = match.CanonicalName,
                Category = match.Category,
                Confidence = "medium",
                AnimalOrigin = IsAnimalOrigin(match.Category, match.CanonicalName),
                DietaryRisk = ResolveDietaryRisk(match.Category, match.CanonicalName),
                SemanticEvidence = [CreateEvidence(raw, match.Category)]
            };
        }

        var allergenMatch = IngredientDictionary.Allergens.FirstOrDefault(entry =>
            MatchesAnyTerm(raw, entry.Synonyms));

        if (allergenMatch is not null)
        {
            return new IngredientNormalizedDto
            {
                Raw = raw,
                Normalized = allergenMatch.CanonicalName,
                Category = allergenMatch.Category,
                Confidence = "high",
                AnimalOrigin = IsAnimalOrigin(allergenMatch.Category, allergenMatch.CanonicalName),
                DietaryRisk = ResolveDietaryRisk(allergenMatch.Category, allergenMatch.CanonicalName),
                SemanticEvidence = [CreateEvidence(raw, allergenMatch.Category)]
            };
        }

        return new IngredientNormalizedDto
        {
            Raw = raw,
            Normalized = raw.Trim(),
            Category = "unknown",
            Confidence = "low",
            DetectionType = "suspected",
            SemanticEvidence = [CreateEvidence(raw, "unknown")]
        };
    }

    private static string NormalizeRawFoodText(string raw)
    {
        var value = raw.Trim().Replace('-', ' ');
        while (value.Contains("  ", StringComparison.Ordinal))
            value = value.Replace("  ", " ", StringComparison.Ordinal);
        return value;
    }

    private static SemanticEvidenceDto CreateEvidence(string text, string category) => new()
    {
        EvidenceType = EvidenceType.IngredientDetected,
        Type = category,
        Text = text,
        Confidence = "medium",
        Source = "ingredient_normalizer",
        TrustLevel = EvidenceTrustLevel.ExplicitIngredient,
        OriginBlock = "IngredientBlock"
    };

    private static bool IsAnimalOrigin(string category, string value)
    {
        var source = IngredientTextNormalizer.Normalize($"{category} {value}");
        // Use boundary-protected check for short terms to avoid false positives:
        // "mel" inside "vermelho", "ovo" inside "couve", "sal" inside "salsa".
        string[] shortTerms = ["mel", "ovo"];
        string[] longTerms = ["milk", "leite", "lactose", "egg", "peixe", "fish", "crustacean", "gelatina", "colageno", "colágeno"];

        foreach (var term in shortTerms)
        {
            if (Regex.IsMatch(source, $@"(?<![\p{{L}}]){Regex.Escape(term)}(?![\p{{L}}])", RegexOptions.CultureInvariant))
                return true;
        }

        return IngredientTextNormalizer.ContainsAny(source, longTerms);
    }

    /// <summary>
    /// Matches a raw ingredient text against a list of dictionary synonyms using word-boundary
    /// protection for short terms (≤4 chars) to prevent false-positive substring hits such as
    /// "sal" matching inside "salsa", or "mel" matching inside "Amélia".
    /// </summary>
    private static bool MatchesAnyTerm(string raw, IEnumerable<string> synonyms)
    {
        var normalizedRaw = IngredientTextNormalizer.Normalize(raw);
        foreach (var synonym in synonyms)
        {
            var normalizedTerm = IngredientTextNormalizer.Normalize(synonym);
            if (normalizedTerm.Length <= 4)
            {
                if (Regex.IsMatch(normalizedRaw, $@"(?<![\p{{L}}]){Regex.Escape(normalizedTerm)}(?![\p{{L}}])", RegexOptions.CultureInvariant))
                    return true;
            }
            else
            {
                if (normalizedRaw.Contains(normalizedTerm, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    private static string ResolveDietaryRisk(string category, string value)
    {
        var source = $"{category} {value}";
        if (IngredientTextNormalizer.ContainsAny(source, ["milk", "leite", "lactose"])) return "lactose_milk";
        if (IngredientTextNormalizer.ContainsAny(source, ["gluten", "glúten", "trigo", "cevada", "centeio", "malte"])) return "gluten";
        if (IngredientTextNormalizer.ContainsAny(source, ["soy", "soja"])) return "soy";
        if (IngredientTextNormalizer.ContainsAny(source, ["tree_nut", "castanha", "amendoim", "peanut"])) return "nuts";
        if (IngredientTextNormalizer.ContainsAny(source, ["egg", "ovo"])) return "egg";
        if (IngredientDictionary.UltraProcessingCategories.Contains(category, StringComparer.OrdinalIgnoreCase)) return "ultra_processing_marker";
        if (category == "sugar" || category == "processed_carbohydrate") return "added_sugar_or_refined_carbohydrate";
        return "unknown";
    }
}
