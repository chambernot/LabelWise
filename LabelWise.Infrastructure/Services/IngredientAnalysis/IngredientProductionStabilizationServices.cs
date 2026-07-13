using System.Text.RegularExpressions;
using LabelWise.Application.DTOs.IngredientAnalysis;

namespace LabelWise.Infrastructure.Services.IngredientAnalysis;

public sealed record FoodTokenRepairResult(string Text, List<OcrCorrectionDto> Corrections)
{
    public bool Repaired => Corrections.Count > 0;
}

public sealed class FoodDictionaryNormalizer
{
    private static readonly IReadOnlyDictionary<string, string> Repairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ENTIOXIDANTE"] = "ANTIOXIDANTE",
        ["ANTIOXIDAMTE"] = "ANTIOXIDANTE",
        ["ANTIOX1DANTE"] = "ANTIOXIDANTE",
        ["ESTABILIZAMTE"] = "ESTABILIZANTE",
        ["ESTABIIIZANTE"] = "ESTABILIZANTE",
        ["CONSERVAMTE"] = "CONSERVANTE",
        ["AROMATIZAMTE"] = "AROMATIZANTE",
        ["ACIDUIANTE"] = "ACIDULANTE",
        ["CORAMTE"] = "CORANTE",
        ["EMULSIFICAMTE"] = "EMULSIFICANTE",
        ["ASCORB1CO"] = "ASCÓRBICO",
        ["C1TRICO"] = "CÍTRICO",
        ["MACA"] = "MAÇÃ",
        ["ACUCAR"] = "AÇÚCAR",
        ["SODIO"] = "SÓDIO",
        ["SODICA"] = "SÓDICA"
    };

    public static IReadOnlyDictionary<string, string[]> CategoryTerms { get; } = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["stabilizer"] = ["estabilizante", "espessante", "gelificante"],
        ["antioxidant"] = ["antioxidante"],
        ["acidulant"] = ["acidulante", "regulador de acidez"],
        ["preservative"] = ["conservante", "conservador"],
        ["colorant"] = ["corante"],
        ["flavoring"] = ["aromatizante", "aroma"],
        ["emulsifier"] = ["emulsificante"],
        ["sweetener"] = ["edulcorante", "adoçante", "adocante"]
    };

    public FoodTokenRepairResult Repair(string value)
    {
        var corrections = new List<OcrCorrectionDto>();
        var repaired = value;

        foreach (var repair in Repairs)
        {
            var pattern = $"\\b{Regex.Escape(repair.Key)}\\b";
            if (!Regex.IsMatch(repaired, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                continue;

            repaired = Regex.Replace(repaired, pattern, repair.Value, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            corrections.Add(new OcrCorrectionDto
            {
                Original = repair.Key,
                Corrected = repair.Value,
                Confidence = "medium",
                Reason = "Correção de token alimentar por dicionário"
            });
        }

        return new FoodTokenRepairResult(repaired, corrections);
    }

    public static string ResolveCategory(string term)
    {
        var normalized = IngredientTextNormalizer.Normalize(term);
        foreach (var category in CategoryTerms)
        {
            if (category.Value.Any(item => normalized == IngredientTextNormalizer.Normalize(item)))
                return category.Key;
        }

        return "unknown";
    }

    public static bool IsCategoryTerm(string value)
    {
        var normalized = IngredientTextNormalizer.Normalize(value);
        return CategoryTerms.Values.SelectMany(term => term).Any(term => normalized == IngredientTextNormalizer.Normalize(term));
    }
}

public sealed class FoodTokenRepairEngine
{
    private readonly FoodDictionaryNormalizer _dictionary = new();

    public FoodTokenRepairResult Repair(string value) => _dictionary.Repair(value);
}

public sealed class OCRSemanticSanitizer
{
    private readonly FoodTokenRepairEngine _repairEngine = new();

    public FoodTokenRepairResult SanitizeIngredient(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new FoodTokenRepairResult(string.Empty, []);

        var corrections = new List<OcrCorrectionDto>();
        var cleaned = value.Replace('\r', ' ').Replace('\n', ' ');

        cleaned = Regex.Replace(cleaned, @"\([A-Za-zÀ-ÿ0-9]?\s*-\s*\)", " ", RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"\([^\p{L}\d]{0,6}\)", " ", RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"\b\d+[,.]?\d*\s*%\b", " ", RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"\b\d+[,.]?\d*\s*(?:kcal|kj|mg|g|ml)\b", " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"(?<=\p{L})\s+\d+(?=\s*$|\s*[,.;])", match =>
        {
            corrections.Add(new OcrCorrectionDto
            {
                Original = match.Value.Trim(),
                Corrected = string.Empty,
                Confidence = "medium",
                Reason = "Remoção de resíduo numérico de OCR em ingrediente"
            });
            return " ";
        }, RegexOptions.CultureInvariant);

        cleaned = Regex.Replace(cleaned, @"\s+", " ", RegexOptions.CultureInvariant).Trim(' ', ',', ';', '.', ':', '-', '–', '—');

        var repaired = _repairEngine.Repair(cleaned);
        corrections.AddRange(repaired.Corrections);
        cleaned = Regex.Replace(repaired.Text, @"\s+", " ", RegexOptions.CultureInvariant).Trim(' ', ',', ';', '.', ':', '-', '–', '—');

        return new FoodTokenRepairResult(cleaned, corrections);
    }
}

public sealed record CompoundIngredientPart(string Category, string Ingredient, string RawText);

public sealed class CompoundIngredientSplitter
{
    private static readonly string CategoryPattern = string.Join("|",
        FoodDictionaryNormalizer.CategoryTerms.Values
            .SelectMany(value => value)
            .Select(Regex.Escape)
            .OrderByDescending(value => value.Length));

    public List<CompoundIngredientPart> Split(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        var candidates = Regex.Split(value, @"[;,]")
            .SelectMany(SplitCompoundSegment)
            .Select(item => item.Trim(' ', ',', ';', '.'))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();

        return candidates.Count == 0
            ? []
            : candidates.Select(ToPart).ToList();
    }

    private static IEnumerable<string> SplitCompoundSegment(string segment)
    {
        var trimmed = segment.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            yield break;

        var normalized = IngredientTextNormalizer.Normalize(trimmed);
        var categoryMatches = Regex.Matches(normalized, $@"\b(?:{CategoryPattern})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (categoryMatches.Count <= 1)
        {
            yield return trimmed;
            yield break;
        }

        var marked = Regex.Replace(trimmed, $@"\s+e\s+((?:{CategoryPattern})\b)", "\n$1", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        marked = Regex.Replace(marked, $@"(?<!^)\b((?:{CategoryPattern})\b)", "\n$1", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        foreach (var part in marked.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return part;
    }

    private static CompoundIngredientPart ToPart(string value)
    {
        var match = Regex.Match(value, $@"^(?<category>{CategoryPattern})\s+(?<ingredient>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
            return new CompoundIngredientPart("unknown", value, value);

        var categoryTerm = match.Groups["category"].Value;
        var ingredient = match.Groups["ingredient"].Value.Trim(' ', ',', ';', '.');
        var category = FoodDictionaryNormalizer.ResolveCategory(categoryTerm);

        return new CompoundIngredientPart(category, ingredient, value);
    }
}

public sealed class CompoundIngredientParser
{
    private readonly CompoundIngredientSplitter _splitter = new();

    public List<CompoundIngredientPart> Parse(string value) => _splitter.Split(value);
}

public sealed class SemanticRegionTransitionEngine
{
    public IEnumerable<string> SplitIntoSemanticSegments(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var value = text.Replace('\r', '\n');
        value = Regex.Replace(value, @"\b(INGREDIENTES?|INGREDIENT|COMPOSI[CÇ][AÃ]O|INGR\.?)\s*[:：-]", "\n$1:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        value = Regex.Replace(value, @"\b(AL[ÉE]RGICOS?|ALERG[ÊE]NICOS?)\b\s*[:：-]?", "\n$1: ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        value = Regex.Replace(value, @"\b(INFORMA[CÇ][AÃ]O NUTRICIONAL|TABELA NUTRICIONAL|NÃO CONTÉM|PODE CONTER|CONTÉM|PRODUZIDO|ENVASILHADO|FABRICADO|DISTRIBU[IÍ]DO|CONSERVAR|CONSERVE|MANTER|VALIDADE|LOTE)\b", "\n$1", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        foreach (var line in value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var segment in Regex.Split(line, @"(?<=[.;])\s+", RegexOptions.CultureInvariant).Where(item => !string.IsNullOrWhiteSpace(item)))
                yield return segment.Trim();
        }
    }

    public bool IsSectionTransition(TextRegionType currentRegion, TextRegionType nextRegion, string value)
    {
        if (currentRegion == TextRegionType.Unknown)
            return false;

        if (currentRegion != nextRegion)
            return true;

        var normalized = IngredientTextNormalizer.Normalize(value);
        return IsIngredientAnchor(normalized) ||
            normalized.StartsWith("alergico", StringComparison.Ordinal) ||
            normalized.StartsWith("alergenico", StringComparison.Ordinal) ||
            normalized.Contains("informacao nutricional", StringComparison.Ordinal) ||
            normalized.Contains("tabela nutricional", StringComparison.Ordinal) ||
            normalized.StartsWith("produzido", StringComparison.Ordinal) ||
            normalized.StartsWith("fabricado", StringComparison.Ordinal) ||
            normalized.StartsWith("conservar", StringComparison.Ordinal);
    }

    public static bool IsIngredientAnchor(string normalized) =>
        normalized.StartsWith("ingr", StringComparison.Ordinal) ||
        normalized.StartsWith("ingrediente", StringComparison.Ordinal) ||
        normalized.StartsWith("ingredient", StringComparison.Ordinal) ||
        normalized.StartsWith("composicao", StringComparison.Ordinal);
}

public sealed class BlockBoundaryResolver
{
    public bool ShouldTerminateForIngredientAnchor(TextRegionType currentRegion, string value)
    {
        var normalized = IngredientTextNormalizer.Normalize(value);
        return SemanticRegionTransitionEngine.IsIngredientAnchor(normalized) &&
            currentRegion is TextRegionType.NutritionTable or TextRegionType.ManufacturerInfo or TextRegionType.Unknown;
    }
}

public sealed record SemanticReliabilityScore(int Score, string Confidence, List<string> Reasons);

public sealed class ConfidencePenaltyRules
{
    public int CalculatePenalty(
        IReadOnlyList<OcrCorrectionDto> corrections,
        AnalysisCompletenessDto completeness,
        IReadOnlyList<string> warnings,
        bool boundaryLeakDetected,
        bool hasPartialReconstruction)
    {
        var penalty = 0;
        penalty += Math.Min(25, corrections.Count * 8);
        if (completeness.Status == "partial") penalty += 20;
        if (completeness.Status == "insufficient") penalty += 40;
        if (boundaryLeakDetected) penalty += 20;
        if (hasPartialReconstruction) penalty += 15;
        if (warnings.Any(warning => IngredientTextNormalizer.ContainsAny(warning, ["parcial", "inclina", "desfoc", "ilegível", "ilegiv", "reflexo"])))
            penalty += 15;

        return Math.Min(70, penalty);
    }
}

public sealed class ConfidenceCalibrationEngine
{
    private readonly ConfidencePenaltyRules _penaltyRules = new();

    public SemanticReliabilityScore Evaluate(
        string ocrQuality,
        AnalysisCompletenessDto completeness,
        IReadOnlyList<string> ingredients,
        IReadOnlyList<OcrCorrectionDto> corrections,
        IReadOnlyList<string> warnings,
        bool boundaryLeakDetected,
        bool hasPartialReconstruction)
    {
        var score = ocrQuality switch { "high" => 85, "medium" => 65, _ => 40 };
        score += Math.Min(10, ingredients.Count * 2);
        score -= _penaltyRules.CalculatePenalty(corrections, completeness, warnings, boundaryLeakDetected, hasPartialReconstruction);
        score = Math.Clamp(score, 0, 100);

        var reasons = new List<string>();
        if (corrections.Count > 0) reasons.Add("Correções OCR aplicadas reduzem a confiança.");
        if (boundaryLeakDetected) reasons.Add("Vazamento de fronteira semântica detectado e bloqueado.");
        if (hasPartialReconstruction) reasons.Add("Reconstrução semântica parcial aplicada.");
        if (completeness.Status != "complete") reasons.Add("Extração parcial limita a confiança.");

        return new SemanticReliabilityScore(score, score >= 75 ? "high" : score >= 45 ? "medium" : "low", reasons);
    }

    public string ApplyCeiling(string confidence, bool repaired, bool inferred, bool partial, bool boundaryLeak)
    {
        var ceiling = "high";
        if (repaired || boundaryLeak) ceiling = "medium";
        if (inferred || partial) ceiling = "low";
        return Rank(confidence) <= Rank(ceiling) ? confidence : ceiling;
    }

    private static int Rank(string confidence) => confidence switch
    {
        "high" => 3,
        "medium" => 2,
        "low" => 1,
        "very_low" => 0,
        _ => 1
    };
}

public sealed record IngredientProductionValidationResult(
    bool IsValid,
    List<string> Warnings,
    List<string> SanitizedIngredients,
    bool BoundaryLeakDetected);

public sealed record IngredientTextSanitizerResult(string Text, string Confidence, List<string> Reasons)
{
    public bool Accepted => Confidence is "high" or "medium";
}

public sealed class IngredientTextSanitizer
{
    private static readonly string[] GenericFoodTerms =
    [
        "agua", "acucar", "sal", "oleo", "gordura", "leite", "creme", "soro", "aroma", "natural", "extrato", "suco", "polpa",
        "farinha", "amido", "fibra", "proteina", "vitamina", "mineral", "acidulante", "antioxidante", "conservante", "estabilizante",
        "emulsificante", "corante", "edulcorante", "adocante", "goma", "cacau", "coco", "aveia", "milho", "soja"
    ];

    public IngredientTextSanitizerResult Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new IngredientTextSanitizerResult(string.Empty, "low", ["Ingrediente vazio."]);

        var text = Regex.Replace(value.Trim(), @"\s+", " ", RegexOptions.CultureInvariant);
        var reasons = new List<string>();
        var score = 50;

        if (Regex.IsMatch(text, @"[^A-Za-zÀ-ÿ0-9\s,.;:%()\-/]", RegexOptions.CultureInvariant))
        {
            score -= 35;
            reasons.Add("Caracteres incompatíveis com rótulo alimentar em português.");
        }

        if (Regex.IsMatch(text, @"[žđłßþð]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            score -= 45;
            reasons.Add("Possível mistura de idioma/ruído OCR.");
        }

        if (Regex.IsMatch(text, @"\b\p{L}{12,}\b", RegexOptions.CultureInvariant) && !HasKnownFoodSignal(text))
        {
            score -= 20;
            reasons.Add("Token longo sem similaridade alimentar.");
        }

        if (HasKnownFoodSignal(text))
            score += 35;
        else
            reasons.Add("Baixa similaridade com dicionário alimentar.");

        var hasDictionarySignal = HasDictionaryFoodSignal(text);

        var tokens = Regex.Matches(IngredientTextNormalizer.Normalize(text), @"\b[a-z]{3,}\b", RegexOptions.CultureInvariant)
            .Select(match => match.Value)
            .ToList();
        var unknownContentTokens = tokens
            .Where(token => !IsStopWord(token))
            .Where(token => !GenericFoodTerms.Any(term => token == term || term.Contains(token, StringComparison.Ordinal) || token.Contains(term, StringComparison.Ordinal)))
            .Where(token => !IngredientDictionary.IngredientNormalization.Concat(IngredientDictionary.Allergens)
                .Any(entry => entry.Synonyms.Concat([entry.CanonicalName]).Any(term => IngredientTextNormalizer.Normalize(term).Contains(token, StringComparison.Ordinal))))
            .ToList();

        if (tokens.Count > 0 && unknownContentTokens.Count >= Math.Max(1, tokens.Count - 1))
        {
            score -= 30;
            reasons.Add("Termos sem contexto alimentar suficiente.");
        }

        if (!hasDictionarySignal && unknownContentTokens.Count > 0)
        {
            score -= 20;
            reasons.Add("Ingrediente genérico com complemento não reconhecido.");
        }

        var confidence = score >= 70 ? "high" : score >= 45 ? "medium" : "low";
        return new IngredientTextSanitizerResult(text, confidence, reasons.DefaultIfEmpty("Ingrediente aceito pelo saneamento semântico.").ToList());
    }

    private static bool HasKnownFoodSignal(string value)
    {
        var normalized = IngredientTextNormalizer.Normalize(value);
        if (GenericFoodTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal)))
            return true;

        return IngredientDictionary.IngredientNormalization.Concat(IngredientDictionary.Allergens)
            .Any(entry => entry.Synonyms.Concat([entry.CanonicalName]).Any(term => normalized.Contains(IngredientTextNormalizer.Normalize(term), StringComparison.Ordinal)));
    }

    private static bool HasDictionaryFoodSignal(string value)
    {
        var normalized = IngredientTextNormalizer.Normalize(value);
        return IngredientDictionary.IngredientNormalization.Concat(IngredientDictionary.Allergens)
            .Any(entry => entry.Synonyms.Concat([entry.CanonicalName]).Any(term => normalized.Contains(IngredientTextNormalizer.Normalize(term), StringComparison.Ordinal)));
    }

    private static bool IsStopWord(string token) => token is "de" or "da" or "do" or "das" or "dos" or "com" or "sem" or "para" or "por" or "em" or "e";
}

public sealed class IngredientProductionValidator
{
    private readonly OCRSemanticSanitizer _sanitizer = new();
    private readonly IngredientTextSanitizer _textSanitizer = new();
    private readonly NutritionLeakBlocker _nutritionLeakBlocker = new();
    private readonly IngredientNoiseFilter _noiseFilter = new();

    public IngredientProductionValidationResult Validate(
        IReadOnlyList<string> ingredients,
        StructuredTextDocument structuredText,
        IReadOnlyList<IngredientConfidenceDto>? confidence = null)
    {
        var warnings = new List<string>();
        var sanitized = new List<string>();

        var boundaryLeakDetected = structuredText.Blocks.Any(block =>
            block.RegionType == TextRegionType.NutritionTable &&
            SemanticRegionTransitionEngine.IsIngredientAnchor(IngredientTextNormalizer.Normalize(block.Text)));

        if (boundaryLeakDetected)
            warnings.Add("Separação de regiões corrigiu possível mistura entre tabela nutricional e ingredientes.");

        foreach (var ingredient in ingredients)
        {
            var clean = _sanitizer.SanitizeIngredient(ingredient).Text;
            if (string.IsNullOrWhiteSpace(clean))
                continue;

            if (_nutritionLeakBlocker.IsNutritionLeak(clean) || _noiseFilter.IsNoise(clean))
            {
                warnings.Add($"Candidato removido por contaminação OCR/nutricional: {clean}");
                continue;
            }

            var textValidation = _textSanitizer.Sanitize(clean);
            if (!textValidation.Accepted)
            {
                warnings.Add($"Ingrediente removido por baixa confiança OCR/alimentar: {clean}");
                continue;
            }

            sanitized.Add(clean);
        }

        if (confidence is not null && confidence.Any(item => item.Confidence == "high" && item.TrustLevel is EvidenceTrustLevel.SemanticInference or EvidenceTrustLevel.WeakInference))
            warnings.Add("Confiança alta bloqueada para evidência inferida.");

        return new IngredientProductionValidationResult(
            sanitized.Count > 0 || ingredients.Count == 0,
            warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            sanitized.GroupBy(IngredientTextNormalizer.Normalize).Select(group => group.First()).ToList(),
            boundaryLeakDetected);
    }
}
