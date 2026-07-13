using System.Text.RegularExpressions;
using LabelWise.Application.Models.IngredientAnalysis;

namespace LabelWise.Infrastructure.Services.IngredientAnalysis;

public sealed partial class IngredientClassifier
{
    private static readonly OCRSemanticSanitizer SemanticSanitizer = new();
    private static readonly CompoundIngredientSplitter CompoundSplitter = new();

    public List<string> ClassifyIngredients(IngredientAnalysisContext context)
    {
        var values = new List<string>();
        values.AddRange(context.VisionExtraction.IngredientsDetected);
        values.AddRange(ParseIngredientsFromText(context.OcrText));
        values.AddRange(ParseIngredientsFromText(context.DocumentIntelligenceText));

        return NormalizeDistinct(values);
    }

    public List<string> ClassifyClaims(IngredientAnalysisContext context)
    {
        var values = new List<string>();
        values.AddRange(context.VisionExtraction.Claims);
        values.AddRange(ExtractClaims(context.OcrText));
        values.AddRange(ExtractClaims(context.DocumentIntelligenceText));

        return NormalizeDistinctClaims(values);
    }

    private static IEnumerable<string> ParseIngredientsFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var match = IngredientsRegex().Match(text);
        if (!match.Success)
            yield break;

        var block = NormalizeIngredientBlock(match.Groups[1].Value);
        foreach (var item in SplitIngredientCandidates(block))
        {
            foreach (var candidate in ExpandCompoundIngredient(item))
            {
                var sanitized = SemanticSanitizer.SanitizeIngredient(candidate).Text;
                var normalized = IngredientTextNormalizer.Normalize(sanitized);
                if (sanitized.Length is >= 2 and <= 80 &&
                !normalized.StartsWith("contem ", StringComparison.Ordinal) &&
                !IsPackagingInstruction(normalized))
                {
                    yield return CanonicalizeIngredient(sanitized);
                }
            }
        }
    }

    private static IEnumerable<string> ExtractClaims(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        foreach (Match match in ClaimsRegex().Matches(text))
        {
            var value = match.Value.Trim().Trim('.', ';', ',');
            if (!string.IsNullOrWhiteSpace(value) && IsAllowedRegulatoryClaim(value))
                yield return value;
        }
    }

    private static List<string> NormalizeDistinct(IEnumerable<string> values)
    {
        var candidates = values
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value) && IsRelevantClaimOrIngredient(value))
            .Select(value => CanonicalizeIngredient(value!))
            .GroupBy(IngredientTextNormalizer.Normalize)
            .Select(group => group.First()!)
            .ToList();

        return RemoveSemanticDuplicates(candidates)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> NormalizeDistinctClaims(IEnumerable<string> values) =>
        values
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value) && IsRelevantClaimOrIngredient(value))
            .Select(CanonicalizeClaim)
            .GroupBy(IngredientTextNormalizer.Normalize)
            .Select(group => group.First())
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string CanonicalizeClaim(string value)
    {
        var normalized = IngredientTextNormalizer.Normalize(value);

        if (normalized.StartsWith("alergicos", StringComparison.Ordinal) ||
            normalized.StartsWith("alergenicos", StringComparison.Ordinal))
        {
            return value.Trim().TrimEnd('.', ';', ',');
        }

        if (normalized.Contains("tracos de", StringComparison.Ordinal))
            return value.Trim().TrimEnd('.', ';', ',');

        if (normalized.Contains("nao contem gluten", StringComparison.Ordinal) ||
            normalized.Contains("sem gluten", StringComparison.Ordinal))
        {
            return "NÃO CONTÉM GLÚTEN";
        }

        if (normalized.Contains("nao contem lactose", StringComparison.Ordinal) ||
            normalized.Contains("sem lactose", StringComparison.Ordinal) ||
            normalized.Contains("zero lactose", StringComparison.Ordinal))
        {
            return "NÃO CONTÉM LACTOSE";
        }

        if (normalized.Contains("sem adicao de acucar", StringComparison.Ordinal))
            return "SEM ADIÇÃO DE AÇÚCAR";

        if (normalized.Contains("zero acucar", StringComparison.Ordinal) ||
            normalized.Contains("sem acucar", StringComparison.Ordinal) ||
            normalized.Contains("nao contem acucar", StringComparison.Ordinal))
        {
            return "ZERO AÇÚCAR";
        }

        if (normalized == "vegano" || normalized.Contains("produto vegano", StringComparison.Ordinal))
            return "VEGANO";

        if (normalized == "vegetariano" || normalized.Contains("produto vegetariano", StringComparison.Ordinal))
            return "VEGETARIANO";

        if (normalized.Contains("plant based", StringComparison.Ordinal))
            return "PLANT BASED";

        if (normalized.Contains("organico", StringComparison.Ordinal))
            return "ORGÂNICO";

        return value;
    }

    private static string CanonicalizeIngredient(string value)
    {
        var trimmed = SemanticSanitizer.SanitizeIngredient(Regex.Replace(value.Trim(), "\\s+", " ").Trim(' ', ',', ';', '.')).Text;
        trimmed = IngredientPrefixRegex().Replace(trimmed, string.Empty).Trim(' ', ',', ';', '.');
        var normalized = IngredientTextNormalizer.Normalize(trimmed);

        if (normalized is "coco rala" or "coco ralad" || normalized.StartsWith("coco rala ", StringComparison.Ordinal))
            return "Coco ralado";

        if (normalized.Contains("conservador ins", StringComparison.Ordinal))
            return Regex.Replace(trimmed, "conservador", "Conservador", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (normalized == "ciclamato de sodio")
            return "ciclamato de sódio";

        if (normalized == "sacarina sodica")
            return "sacarina sódica";

        return trimmed;
    }

    private static string NormalizeIngredientBlock(string block)
    {
        var value = block.Replace("\r", "\n");
        value = Regex.Replace(
            value,
            @"\b(?:AL[ÉE]RGICOS?|ALERG[ÊE]NICOS?|INFORMA[CÇ][AÃ]O\s+NUTRICIONAL|TABELA\s+NUTRICIONAL|NÃO\s+CONT[ÉE]M|NAO\s+CONT[ÉE]M|PODE\s+CONTER|CONT[ÉE]M\s+GL[ÚU]TEN)\b.*$",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);

        return Regex.Replace(
            value,
            "(?<!^)(?=\\b(?:conservador|conservante|acidulante|aromatizante|corante|estabilizante|emulsificante)\\b)",
            "\n",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static IEnumerable<string> SplitIngredientCandidates(string block)
    {
        foreach (var line in block.Split(['\n', ',', ';', '•', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var cleaned = Regex.Replace(line, "\\s{2,}", "|").Trim();
            foreach (var item in cleaned.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var value = item.Trim(' ', ',', ';', '.');
                if (!string.IsNullOrWhiteSpace(value))
                    yield return value;
            }
        }
    }

    private static IEnumerable<string> ExpandCompoundIngredient(string value)
    {
        var parts = CompoundSplitter.Split(value);
        if (parts.Count <= 1)
        {
            yield return value;
            yield break;
        }

        foreach (var part in parts)
            yield return part.Ingredient;
    }

    private static bool IsRelevantClaimOrIngredient(string value)
    {
        var normalized = IngredientTextNormalizer.Normalize(value);
        return !normalized.StartsWith("nao contem quantidades significativas", StringComparison.Ordinal) &&
               !normalized.StartsWith("contem quantidades significativas", StringComparison.Ordinal) &&
               !IsNutritionFactText(normalized) &&
               !IsContextualSugarReference(normalized) &&
               !IsPackagingInstruction(normalized) &&
               normalized is not "nao contem" and not "nao contem:" and not "sem";
    }

    internal static bool IsContextualSugarReference(string value)
    {
        var normalized = IngredientTextNormalizer.Normalize(value);
        return IngredientTextNormalizer.ContainsAny(normalized, IngredientDictionary.SugarTerms) &&
            IngredientTextNormalizer.ContainsAny(normalized, IngredientDictionary.ContextualSugarExclusionTerms) &&
            !IsRealSugarIngredient(normalized);
    }

    internal static bool IsRealSugarIngredient(string value)
    {
        var normalized = IngredientTextNormalizer.Normalize(value).Trim(' ', '.', ',', ';', ':');
        if (!IngredientTextNormalizer.ContainsAny(normalized, IngredientDictionary.SugarTerms))
            return false;

        if (IngredientTextNormalizer.ContainsAny(normalized, IngredientDictionary.ContextualSugarExclusionTerms) &&
            !normalized.StartsWith("acucar", StringComparison.Ordinal) &&
            !normalized.StartsWith("açucar", StringComparison.Ordinal) &&
            !normalized.StartsWith("sacarose", StringComparison.Ordinal) &&
            !normalized.StartsWith("glicose", StringComparison.Ordinal) &&
            !normalized.StartsWith("glucose", StringComparison.Ordinal) &&
            !normalized.StartsWith("xarope", StringComparison.Ordinal) &&
            !normalized.StartsWith("maltodextrina", StringComparison.Ordinal) &&
            !normalized.StartsWith("dextrose", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool IsNutritionFactText(string normalized)
    {
        if (normalized.Contains("informacao nutricional", StringComparison.Ordinal) ||
            normalized.Contains("tabela nutricional", StringComparison.Ordinal) ||
            normalized.Contains("%vd", StringComparison.Ordinal) ||
            NutritionUnitRegex().IsMatch(normalized))
        {
            return true;
        }

        return IngredientDictionary.NutritionFactTerms.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsAllowedRegulatoryClaim(string value)
    {
        var normalized = IngredientTextNormalizer.Normalize(value);
        return normalized.Contains("nao contem gluten", StringComparison.Ordinal) ||
               normalized.Contains("nao contem lactose", StringComparison.Ordinal) ||
               normalized.Contains("sem gluten", StringComparison.Ordinal) ||
               normalized.Contains("sem lactose", StringComparison.Ordinal) ||
               normalized.Contains("zero lactose", StringComparison.Ordinal) ||
               normalized.Contains("zero acucar", StringComparison.Ordinal) ||
               normalized.Contains("sem acucar", StringComparison.Ordinal) ||
               normalized.Contains("sem adicao de acucar", StringComparison.Ordinal) ||
               normalized.StartsWith("alergicos", StringComparison.Ordinal) ||
               normalized.StartsWith("alergenicos", StringComparison.Ordinal) ||
                (normalized.StartsWith("pode conter", StringComparison.Ordinal) && ContainsKnownAllergen(normalized)) ||
                (normalized.StartsWith("tracos de", StringComparison.Ordinal) && ContainsKnownAllergen(normalized)) ||
               normalized.Contains("fabricado em equipamento", StringComparison.Ordinal) ||
               normalized.Contains("fabricado em linha", StringComparison.Ordinal) ||
               normalized.Contains("alto em fibras", StringComparison.Ordinal) ||
               normalized.Contains("fonte de proteina", StringComparison.Ordinal) ||
               normalized.Contains("vegano", StringComparison.Ordinal) ||
               normalized.Contains("vegetariano", StringComparison.Ordinal) ||
               normalized.Contains("plant based", StringComparison.Ordinal) ||
               normalized.Contains("organico", StringComparison.Ordinal) ||
               IsExplicitContainsAllergenClaim(normalized);
    }

    private static bool IsExplicitContainsAllergenClaim(string normalized) =>
        normalized.StartsWith("contem ", StringComparison.Ordinal) &&
        IngredientDictionary.Allergens.Any(entry => IngredientTextNormalizer.ContainsAny(normalized, entry.Synonyms.Concat([entry.CanonicalName])));

    private static bool ContainsKnownAllergen(string normalized) =>
        IngredientDictionary.Allergens.Any(entry => IngredientTextNormalizer.ContainsAny(normalized, entry.Synonyms.Concat([entry.CanonicalName])));

    private static List<string> RemoveSemanticDuplicates(IReadOnlyList<string> values)
    {
        var incompleteSuffixes = new[] { " e", " de", " com" };

        var validIngredients = values
            .Where(value =>
            {
                var normalized = IngredientTextNormalizer.Normalize(value);
                if (incompleteSuffixes.Any(s => normalized.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
                {
                    // Invalida se não houver uma continuação
                    return values.Any(other => !ReferenceEquals(value, other) && IngredientTextNormalizer.Normalize(other).StartsWith(normalized, StringComparison.OrdinalIgnoreCase));
                }
                return true;
            })
            .ToList();

        return validIngredients
            .Where(value => !validIngredients.Any(other => IsShorterDuplicate(value, other)))
            .ToList();
    }

    private static bool IsIncompleteFragment(string value, IReadOnlyList<string> allValues)
    {
        var normalized = IngredientTextNormalizer.Normalize(value);

        // Regra para sufixos de ligação como "e", "de", "com"
        var linkingSuffixes = new[] { " e", " de", " com" };
        if (linkingSuffixes.Any(s => normalized.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
        {
            // É um fragmento se NÃO houver uma versão mais longa
            bool isContinued = allValues.Any(other => 
                !ReferenceEquals(value, other) && 
                IngredientTextNormalizer.Normalize(other).StartsWith(normalized, StringComparison.OrdinalIgnoreCase));

            return !isContinued; // Invalida se não for continuado
        }

        // Regra original para outros sufixos incompletos
        return IngredientDictionary.IncompleteIngredientSuffixes.Any(suffix => normalized.EndsWith(suffix, StringComparison.Ordinal)) &&
            allValues.Any(other => !ReferenceEquals(value, other) && IngredientTextNormalizer.Normalize(other).StartsWith(normalized + " ", StringComparison.Ordinal));
    }

    private static bool IsShorterDuplicate(string value, string other)
    {
        var normalized = IngredientTextNormalizer.Normalize(value);
        var normalizedOther = IngredientTextNormalizer.Normalize(other);
        return normalized.Length + 3 < normalizedOther.Length &&
            (normalizedOther.StartsWith(normalized + " ", StringComparison.Ordinal) || normalizedOther.Contains(" " + normalized + " ", StringComparison.Ordinal));
    }

    private static string DetermineProcessingLevel(IReadOnlyList<string> ingredients)
    {
        var markers = ingredients.Select(i => IngredientTextNormalizer.Normalize(i)).ToList();

        var ultraProcessedMarkers = new[]
        {
            "aromatizante artificial", "corante artificial", "emulsificante", "realçador de sabor", "xarope de"
        };

        if (markers.Any(m => ultraProcessedMarkers.Any(ump => m.Contains(ump))))
        {
            return "ultra-processed";
        }

        var processedMarkers = new[]
        {
            "farinha refinada", "gordura vegetal", "açúcar", "conservador", "antioxidante"
        };

        var processedCount = markers.Count(m => processedMarkers.Any(pm => m.Contains(pm)));

        if (processedCount >= 2)
        {
            return "processed";
        }

        if (markers.Any(m => m.Contains("conservador") || m.Contains("antioxidante")))
        {
            return "processed";
        }

        return "minimally-processed";
    }

    private static bool IsPackagingInstruction(string normalized) =>
        normalized.StartsWith("conserve", StringComparison.Ordinal) ||
        normalized.StartsWith("conservar", StringComparison.Ordinal) ||
        normalized.StartsWith("manter", StringComparison.Ordinal) ||
        normalized.StartsWith("modo de preparo", StringComparison.Ordinal) ||
        normalized.StartsWith("preparo", StringComparison.Ordinal) ||
        normalized.StartsWith("validade", StringComparison.Ordinal) ||
        normalized.StartsWith("lote", StringComparison.Ordinal) ||
        normalized.StartsWith("fabricado", StringComparison.Ordinal);

    [GeneratedRegex("(?:ingredientes?|ingredient|composi[cç][aã]o|ingr\\.?)\\s*[:：-]?\\s*(.+?)(?:alerg[eê]nicos?|al[eé]rgicos?|cont[eé]m|pode conter|n[aã]o cont[eé]m|informa[cç][aã]o nutricional|tabela nutricional|produzido|fabricado|envasilhado|conservar|validade|lote|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex IngredientsRegex();

    [GeneratedRegex("(al[eé]rgicos?\\s*[:：-]?\\s*[^.;\\n]+|alerg[eê]nicos?\\s*[:：-]?\\s*[^.;\\n]+|tra[cç]os\\s+de\\s+[^.;\\n]+|pode conter(?: tra[cç]os de)?\\s+[^.;\\n]+|fabricado em (?:equipamento|linha)[^.;\\n]+|n[aã]o cont[eé]m\\s+(?:gl[uú]ten|lactose|a[cç][uú]car)|cont[eé]m\\s+(?:leite|lactose|gl[uú]ten|trigo|soja|ovo|ovos|amendoim|castanhas?|peixe|crust[aá]ceos?)[^.;\\n]*|sem\\s+adi[cç][aã]o\\s+de\\s+a[cç][uú]car|sem\\s+a[cç][uú]car|zero\\s+a[cç][uú]car|sem\\s+lactose|zero\\s+lactose|sem\\s+gl[uú]ten|alto em fibras|fonte de prote[ií]na|vegano|vegetariano|plant\\s*based|org[aâ]nico)", RegexOptions.IgnoreCase)]
    private static partial Regex ClaimsRegex();

    [GeneratedRegex("\\b\\d+(?:[,.]\\d+)?\\s*(?:kcal|kj|mg|g)\\b", RegexOptions.IgnoreCase)]
    private static partial Regex NutritionUnitRegex();

    [GeneratedRegex("^(?:edulcorantes?|conservantes?|conservadores?|acidulantes?|aromatizantes?|corantes?|estabilizantes?|emulsificantes?)\\s*[:：-]\\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IngredientPrefixRegex();
}
