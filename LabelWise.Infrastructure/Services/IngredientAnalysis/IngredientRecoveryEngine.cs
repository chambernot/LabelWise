using System.Text.RegularExpressions;
using LabelWise.Application.DTOs.IngredientAnalysis;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services.IngredientAnalysis;

public sealed class IngredientAnchorDetector
{
    private static readonly Regex IngredientAnchorRegex = new(
        @"\b(?:INGR\.?|INGREDIENTES?|INGREDIENT|COMPOSI[CÇ][AÃ]O|LISTA\s+DE\s+INGREDIENTES)\s*[:：-]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] IngredientAnchors =
    [
        "ingredientes:",
        "ingr:",
        "ingr.:",
        "ingredient:",
        "composicao:",
        "composição:",
        "lista de ingredientes:"
    ];

    public IEnumerable<IngredientAnchorMatch> DetectAnchors(params string?[] texts)
    {
        foreach (var text in texts.Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            foreach (Match match in IngredientAnchorRegex.Matches(text!))
            {
                yield return new IngredientAnchorMatch
                {
                    Anchor = match.Value,
                    StartIndex = match.Index,
                    Text = text!,
                    Confidence = "high"
                };
            }

            var normalized = IngredientTextNormalizer.Normalize(text!);
            foreach (var anchor in IngredientAnchors)
            {
                if (!normalized.Contains(anchor, StringComparison.Ordinal))
                    continue;

                var originalIndex = FindApproximateOriginalAnchorIndex(text!, anchor);
                if (originalIndex < 0)
                    continue;

                yield return new IngredientAnchorMatch
                {
                    Anchor = text![originalIndex..Math.Min(text!.Length, originalIndex + anchor.Length)],
                    StartIndex = originalIndex,
                    Text = text!,
                    Confidence = "medium"
                };
            }
        }
    }

    private static int FindApproximateOriginalAnchorIndex(string text, string normalizedAnchor)
    {
        var patterns = normalizedAnchor.StartsWith("ingr", StringComparison.Ordinal)
            ? new[] { "INGR", "Ingr", "ingr" }
            : normalizedAnchor.StartsWith("compos", StringComparison.Ordinal)
                ? ["COMPOSI", "Composi", "composi"]
                : ["INGREDIENT", "Ingredient", "ingredient"];

        return patterns
            .Select(pattern => text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase))
            .Where(index => index >= 0)
            .DefaultIfEmpty(-1)
            .Min();
    }
}

public sealed class IngredientAnchorMatch
{
    public string Anchor { get; set; } = string.Empty;
    public int StartIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Confidence { get; set; } = "medium";
}

public sealed class IngredientGrammarValidator
{
    private static readonly string[] FoodVocabulary =
    [
        "agua", "acucar", "sal", "oleo", "gordura", "leite", "farinha", "amido", "suco", "extrato",
        "cacau", "chocolate", "manteiga", "creme", "soro", "proteina", "caseinato", "lactose", "frutose",
        "glicose", "xarope", "mel", "aroma", "corante", "conservante", "acidulante", "estabilizante",
        "emulsificante", "antioxidante", "espessante", "umectante", "regulador", "fermento", "vitamina",
        "mineral", "acido", "citrico", "ascorbico", "benzoato", "sorbato", "nitrito", "nitrato",
        "gelificante", "realcador", "sabor", "cloreto", "fosfato", "carbonato", "sulfato", "citrato",
        "tartarato", "malato", "lactato", "acetato", "propionato", "butirato", "caramelo", "annatto",
        "curcuma", "cochonilha", "urucum", "carvao", "betacaroteno", "clorofila", "antocianina",
        "beterraba", "spirulina", "paprica", "riboflavina", "carmim", "lecitina", "pectina", "goma",
        "carragena", "agar", "alginato", "celulose", "dextrina", "maltodextrina", "polissorbato",
        "monoestearato", "diestearato", "triestearato", "mono", "di", "tri", "glicerideos", "glicerideo",
        "esteres", "ester", "sorbitol", "manitol", "xilitol", "maltitol", "eritritol", "isomalte",
        "sucralose", "aspartame", "acessulfame", "sacarina", "ciclamato", "stevia", "taumatina",
        "neotame", "advantame", "glicosideos", "rebaudiosideo", "esteviosideo", "tomate", "cebola",
        "alho", "pimenta", "oregano", "manjericao", "salsa", "cebolinha", "coentro", "cominho",
        "canela", "cravo", "noz", "castanha", "amendoa", "avela", "macadamia", "pistache", "amendoim",
        "soja", "trigo", "centeio", "cevada", "aveia", "milho", "arroz", "quinoa", "chia", "linhaca",
        "gergelim", "girassol", "abobora", "ovo", "clara", "gema", "albumina", "pescado", "peixe",
        "camarao", "lagosta", "caranguejo", "marisco", "molusco", "crustaceo", "gelatina", "colageno",
        "hidrolisado", "isolado", "concentrado", "desnatado", "integral", "parcialmente", "hidrogenado",
        "interesterificado", "refinado", "vegetal", "animal", "natural", "artificial", "sintetico",
        "organico", "transgenic", "nao transgenic", "livre", "isento", "zero", "reduzido", "light",
        "diet", "fit", "plus", "max", "ultra", "super", "extra", "premium", "gourmet", "caseiro"
    ];

    private static readonly string[] AdditiveKeywords =
    [
        "acidulante", "estabilizante", "corante", "aromatizante", "conservante", "antioxidante",
        "emulsificante", "espessante", "umectante", "realcador", "fermento", "gelificante",
        "regulador de acidez", "agente de massa", "antiespumante", "antiumectante", "sequestrante",
        "melhorador de farinha", "propelente", "agente de revestimento", "agente de volume"
    ];

    public IngredientGrammarScore EvaluateText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new IngredientGrammarScore { Score = 0, Confidence = "very_low" };

        var normalized = IngredientTextNormalizer.Normalize(text);
        var score = 0;
        var matches = new List<string>();

        var commaCount = text.Count(c => c == ',');
        var semicolonCount = text.Count(c => c == ';');
        var separatorDensity = (commaCount + semicolonCount) / Math.Max(1.0, text.Length / 50.0);
        if (separatorDensity >= 2)
        {
            score += 25;
            matches.Add("separator_density");
        }

        var foodTermMatches = FoodVocabulary.Count(term => normalized.Contains(term, StringComparison.Ordinal));
        score += Math.Min(40, foodTermMatches * 5);
        if (foodTermMatches > 0)
            matches.Add($"food_vocabulary:{foodTermMatches}");

        var additiveMatches = AdditiveKeywords.Count(term => normalized.Contains(term, StringComparison.Ordinal));
        score += Math.Min(20, additiveMatches * 10);
        if (additiveMatches > 0)
            matches.Add($"additives:{additiveMatches}");

        if (Regex.IsMatch(normalized, @"\b[a-z]{3,20}(?:\s+[a-z]{3,20}){0,3}\s*(?:,|;|\.|e\b)", RegexOptions.CultureInvariant))
        {
            score += 15;
            matches.Add("ingredient_pattern");
        }

        return new IngredientGrammarScore
        {
            Score = Math.Clamp(score, 0, 100),
            Confidence = score >= 60 ? "high" : score >= 35 ? "medium" : score >= 15 ? "low" : "very_low",
            Matches = matches
        };
    }
}

public sealed class IngredientGrammarScore
{
    public int Score { get; set; }
    public string Confidence { get; set; } = "very_low";
    public List<string> Matches { get; set; } = [];
}

public sealed class IngredientRegionPromoter
{
    private static readonly string[] StopKeywords =
    [
        "alergicos:", "alergenicos:", "pode conter", "nao contem", "contem gluten", "contem lactose",
        "produzido", "envasilhado", "fabricado", "cnpj", "distribuido", "industria", "ltda",
        "conservar", "conserve", "manter", "validade", "lote", "sac", "servico", "atendimento",
        "informacao nutricional", "tabela nutricional", "valor energetico", "porcao", "%vd"
    ];

    public List<IngredientRegionCandidate> PromoteRegions(IEnumerable<IngredientAnchorMatch> anchors, IngredientGrammarValidator validator)
    {
        var candidates = new List<IngredientRegionCandidate>();

        foreach (var anchor in anchors)
        {
            var region = ExtractRegion(anchor);
            if (string.IsNullOrWhiteSpace(region))
                continue;

            var grammar = validator.EvaluateText(region);
            if (grammar.Score < 15)
                continue;

            candidates.Add(new IngredientRegionCandidate
            {
                Text = region,
                Confidence = grammar.Confidence,
                GrammarScore = grammar.Score,
                Source = "anchor_promoted",
                OriginAnchor = anchor.Anchor
            });
        }

        return candidates;
    }

    private static string ExtractRegion(IngredientAnchorMatch anchor)
    {
        var startIndex = anchor.StartIndex + anchor.Anchor.Length;
        if (startIndex >= anchor.Text.Length)
            return string.Empty;

        var text = anchor.Text[startIndex..];
        var normalized = IngredientTextNormalizer.Normalize(text);

        foreach (var stopKeyword in StopKeywords)
        {
            var stopIndex = normalized.IndexOf(stopKeyword, StringComparison.Ordinal);
            if (stopIndex > 10)
            {
                text = text[..stopIndex];
                break;
            }
        }

        return text.Trim().TrimEnd('.', ';', ',');
    }
}

public sealed class IngredientRegionCandidate
{
    public string Text { get; set; } = string.Empty;
    public string Confidence { get; set; } = "low";
    public int GrammarScore { get; set; }
    public string Source { get; set; } = "unknown";
    public string OriginAnchor { get; set; } = string.Empty;
}

public sealed class IngredientRecoveryEngine(
    IngredientAnchorDetector anchorDetector,
    IngredientGrammarValidator grammarValidator,
    IngredientRegionPromoter regionPromoter,
    ILogger<IngredientRecoveryEngine> logger)
{
    public IngredientRecoveryResult Recover(
        IReadOnlyList<string> structuredIngredients,
        StructuredTextDocument structuredText,
        params string?[] rawOcrTexts)
    {
        if (structuredIngredients.Count > 0)
        {
            logger.LogDebug("[IngredientRecovery] Structured extraction succeeded with {Count} ingredients, skipping recovery.", structuredIngredients.Count);
            return new IngredientRecoveryResult
            {
                Ingredients = structuredIngredients.ToList(),
                Source = "structured",
                Confidence = "high",
                RecoveryApplied = false
            };
        }

        logger.LogWarning("[IngredientRecovery] Structured extraction returned empty, attempting recovery.");

        var allTexts = structuredText.Blocks
            .Select(block => block.Text)
            .Concat(rawOcrTexts.Where(t => !string.IsNullOrWhiteSpace(t)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var anchors = anchorDetector.DetectAnchors(allTexts).ToList();
        logger.LogDebug("[IngredientRecovery] Found {AnchorCount} ingredient anchors.", anchors.Count);

        if (anchors.Count == 0)
            return FallbackToGrammarScan(allTexts);

        var candidates = regionPromoter.PromoteRegions(anchors, grammarValidator);
        logger.LogDebug("[IngredientRecovery] Promoted {CandidateCount} regions with grammar score >= 15.", candidates.Count);

        var bestCandidate = candidates
            .OrderByDescending(c => c.GrammarScore)
            .ThenBy(c => ConfidenceRank(c.Confidence))
            .FirstOrDefault();

        if (bestCandidate is null || bestCandidate.GrammarScore < 15)
            return FallbackToGrammarScan(allTexts);

        var recoveredIngredients = ParseIngredients(bestCandidate.Text);
        logger.LogInformation(
            "[IngredientRecovery] Recovered {Count} ingredients from anchor '{Anchor}' with grammar score {Score}.",
            recoveredIngredients.Count,
            bestCandidate.OriginAnchor,
            bestCandidate.GrammarScore);

        return new IngredientRecoveryResult
        {
            Ingredients = recoveredIngredients,
            Source = "anchor_recovery",
            Confidence = bestCandidate.Confidence,
            RecoveryApplied = true,
            GrammarScore = bestCandidate.GrammarScore
        };
    }

    private IngredientRecoveryResult FallbackToGrammarScan(string[] allTexts)
    {
        logger.LogWarning("[IngredientRecovery] No valid anchors found, attempting grammar-based fallback scan.");

        var grammarScores = allTexts
            .Select(text => (Text: text, Grammar: grammarValidator.EvaluateText(text)))
            .Where(item => item.Grammar.Score >= 35)
            .OrderByDescending(item => item.Grammar.Score)
            .ToList();

        if (grammarScores.Count == 0)
        {
            logger.LogError("[IngredientRecovery] Fallback scan found no text with grammar score >= 35. Recovery failed.");
            return new IngredientRecoveryResult
            {
                Ingredients = [],
                Source = "fallback_failed",
                Confidence = "very_low",
                RecoveryApplied = true,
                GrammarScore = 0
            };
        }

        var best = grammarScores.First();
        var recoveredIngredients = ParseIngredients(best.Text);
        logger.LogInformation("[IngredientRecovery] Fallback recovered {Count} ingredients with grammar score {Score}.", recoveredIngredients.Count, best.Grammar.Score);

        return new IngredientRecoveryResult
        {
            Ingredients = recoveredIngredients,
            Source = "grammar_fallback",
            Confidence = best.Grammar.Confidence,
            RecoveryApplied = true,
            GrammarScore = best.Grammar.Score
        };
    }

    private static List<string> ParseIngredients(string text)
    {
        return Regex.Split(text, @"[,;]")
            .Select(ingredient => Regex.Replace(ingredient, @"^(?:ingr\.?|ingredientes?|ingredient|composi[cç][aã]o)\s*[:：-]\s*", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim().Trim('.', ':', '-'))
            .Where(ingredient => !string.IsNullOrWhiteSpace(ingredient) && ingredient.Length >= 3 && ingredient.Length <= 80)
            .GroupBy(IngredientTextNormalizer.Normalize)
            .Select(group => group.First())
            .ToList();
    }

    private static int ConfidenceRank(string confidence) => confidence switch
    {
        "high" => 0,
        "medium" => 1,
        "low" => 2,
        _ => 3
    };
}

public sealed class IngredientRecoveryResult
{
    public List<string> Ingredients { get; set; } = [];
    public string Source { get; set; } = "unknown";
    public string Confidence { get; set; } = "very_low";
    public bool RecoveryApplied { get; set; }
    public int GrammarScore { get; set; }
}

public sealed class ProcessingSignalRecovery
{
    private static readonly string[] ProcessingMarkers =
    [
        "aromatizante", "acidulante", "estabilizante", "corante", "conservante", "antioxidante",
        "emulsificante", "espessante", "umectante", "realcador", "fermento", "gelificante"
    ];

    public int RecoverProcessingScore(int currentScore, IReadOnlyList<string> ingredients, params string?[] rawTexts)
    {
        if (currentScore > 0)
            return currentScore;

        var allText = string.Join(" ", ingredients.Concat(rawTexts.Where(t => !string.IsNullOrWhiteSpace(t))));
        var normalized = IngredientTextNormalizer.Normalize(allText);
        var markerCount = ProcessingMarkers.Count(marker => normalized.Contains(marker, StringComparison.Ordinal));

        if (markerCount == 0)
            return 0;

        var recoveredScore = Math.Min(70, markerCount * 16 + 14);
        return recoveredScore;
    }
}
