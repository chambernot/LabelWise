using System.Text.RegularExpressions;

namespace LabelWise.Infrastructure.Services.IngredientAnalysis;

public sealed class IngredientPurificationLayer(
    IngredientNoiseFilter noiseFilter,
    NutritionLeakBlocker nutritionLeakBlocker,
    IngredientSemanticValidator semanticValidator)
{
    private static readonly OCRSemanticSanitizer Sanitizer = new();
    private static readonly CompoundIngredientSplitter CompoundSplitter = new();

    public IngredientPurificationResult Purify(IEnumerable<string> candidates)
    {
        var accepted = new List<string>();
        var rejected = new List<RejectedIngredientCandidate>();

        foreach (var candidate in candidates.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var initial = CleanCandidate(candidate);
            if (string.IsNullOrWhiteSpace(initial))
                continue;

            var splitParts = CompoundSplitter.Split(initial);
            var values = splitParts.Count > 1
                ? splitParts.Select(part => part.Ingredient)
                : [initial];

            foreach (var value in values)
            {
                var cleaned = Sanitizer.SanitizeIngredient(CleanCandidate(value)).Text;
                if (string.IsNullOrWhiteSpace(cleaned))
                    continue;

                var nutritionLeak = nutritionLeakBlocker.IsNutritionLeak(cleaned);
                var noise = noiseFilter.IsNoise(cleaned);
                var semantic = semanticValidator.Validate(cleaned);

                if (nutritionLeak || noise || semantic.IngredientSemanticScore < IngredientSemanticValidator.AcceptanceThreshold)
                {
                    rejected.Add(new RejectedIngredientCandidate
                    {
                        Text = cleaned,
                        IngredientSemanticScore = semantic.IngredientSemanticScore,
                        Reason = nutritionLeak ? "nutrition_leak" : noise ? "ocr_noise" : "low_semantic_score"
                    });
                    continue;
                }

                accepted.Add(cleaned);
            }
        }

        return new IngredientPurificationResult
        {
            Ingredients = accepted
                .GroupBy(IngredientTextNormalizer.Normalize)
                .Select(group => group.First())
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Rejected = rejected
        };
    }

    private static readonly Regex IngredientAnchorMidRegex = new(
        @"\b(?:ingr\.?|ingredientes?|ingredient|composi[cç][aã]o|lista\s+de\s+ingredientes)\s*[:：\-]\s*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex HardBoundaryRegex = new(
        @"\b(?:al[ée]rgicos?|alerg[êe]nicos?|cont[ée]m\b|n[ãa]o\s+cont[ée]m\b|pode\s+conter\b|tra[cç]os\s+de\b|informa[cç][ãa]o\s+nutricional|tabela\s+nutricional|valor\s+energ[ée]tico|porcao|por[cç][ãa]o\b|conservar\b|conserve\b|manter\b|validade\b|lote\b|cnpj\b|fabricado\b|produzido\b|envasilhado\b|distribu[ií]do\b|ind[uú]stria\b|sac\b|servi[cç]o\s+de\s+atendimento)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string CleanCandidate(string value)
    {
        var cleaned = Regex.Replace(value, @"\s+", " ", RegexOptions.CultureInvariant)
            .Trim(' ', ',', ';', '.', ':', '-', '–', '—', '|');

        // Rejeitar frases de sugestão de uso / marketing que vazam como "ingrediente":
        // ex. "basta adicionar água", "para sua preparação adicione", "misture com água"
        var normalizedCheck = IngredientTextNormalizer.Normalize(cleaned);
        if (Regex.IsMatch(normalizedCheck,
                @"\b(?:basta|adicionar|adicione|preparo|preparacao|misture|misturar|dissolver|dissolva|consumir|modo de uso|sugestao de uso|instrucoes|instrucao|diluir|disolva|como usar|uso:|forma de preparo)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return string.Empty;

        // Rejeitar candidatos que contêm lote/código de rastreio mesmo que tenham palavras de ingrediente
        if (Regex.IsMatch(normalizedCheck,
                @"\b(?:lot|lote|loto|vald|val[\s:]|codigo|cnpj|sac\b)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return string.Empty;

        // Rejeitar tokens com dados de fabricante: email, telefone, CNPJ fragmentado
        // ex: "Email: marp F:(15)3282 C.N.P.J. 43.470 Cebola em flocos"
        if (Regex.IsMatch(normalizedCheck,
                @"(?:@|\bemail\b|\bwww\.|\.com\.br|f:\s*\(|\(\d{2}\)\s*\d|c\.\s*n\.\s*p\.\s*j|\d{2}\.\d{3}\.\d{3})",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return string.Empty;

        // FASE 2: se o candidato contém o anchor "INGREDIENTES:" no meio do texto
        // (resíduo de OCR/Vision que misturou marketing + lista), descartamos tudo
        // antes do ÚLTIMO anchor e mantemos apenas a região válida.
        var anchorMatches = IngredientAnchorMidRegex.Matches(cleaned);
        if (anchorMatches.Count > 0)
        {
            var lastAnchor = anchorMatches[^1];
            cleaned = cleaned[(lastAnchor.Index + lastAnchor.Length)..]
                .Trim(' ', ',', ';', '.', ':', '-', '–', '—', '|');
        }

        // FASE 2: corta o candidato em qualquer marcador de bloco não-ingrediente
        // (claim regulatório, fabricante, armazenamento, tabela nutricional).
        // Funciona para qualquer categoria — não depende do produto.
        var boundary = HardBoundaryRegex.Match(cleaned);
        if (boundary.Success && boundary.Index > 0)
            cleaned = cleaned[..boundary.Index].Trim(' ', ',', ';', '.', ':', '-', '–', '—', '|');
        else if (boundary.Success && boundary.Index == 0)
            cleaned = string.Empty;

        return cleaned.Trim(' ', ',', ';', '.', ':', '-', '–', '—', '|');
    }
}

public sealed class IngredientPurificationResult
{
    public List<string> Ingredients { get; set; } = [];
    public List<RejectedIngredientCandidate> Rejected { get; set; } = [];
}

public sealed class RejectedIngredientCandidate
{
    public string Text { get; set; } = string.Empty;
    public int IngredientSemanticScore { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class IngredientNoiseFilter
{
    private static readonly Regex NumericOnlyRegex = new(@"^\d+(?:\s+\d+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex IsolatedUnitRegex = new(@"^\d+[,.]?\d*\s?(?:mg|g|ml|kcal|kj)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex NumericTableCellRegex = new(@"^(?:\d+[,.]?\d*\s+){1,6}[\p{L}\s()/%]+(?:\s+\d+[,.]?\d*)*$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex MostlyNumericRegex = new(@"[\d]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public bool IsNoise(string candidate)
    {
        var normalized = IngredientTextNormalizer.Normalize(candidate);
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length < 3)
            return true;

        if (NumericOnlyRegex.IsMatch(normalized) || IsolatedUnitRegex.IsMatch(normalized))
            return true;

        if (normalized.Contains("%vd", StringComparison.Ordinal) || normalized.Contains("% vd", StringComparison.Ordinal))
            return true;

        if (NumericTableCellRegex.IsMatch(candidate) && NutritionVocabulary.ContainsNutritionTerm(normalized))
            return true;

        var digits = MostlyNumericRegex.Count(candidate);
        var letters = candidate.Count(char.IsLetter);
        if (digits > 0 && digits >= letters)
            return true;

        if (digits > 0 && NutritionVocabulary.ContainsNutritionTerm(normalized))
            return true;

        if (Regex.IsMatch(normalized, @"\b\d+[,.]?\d*\s*(?:kcal|kj|mg|g|ml)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) && NutritionVocabulary.ContainsNutritionTerm(normalized))
            return true;

        return false;
    }
}

public sealed class NutritionLeakBlocker
{
    public bool IsNutritionLeak(string candidate)
    {
        var normalized = IngredientTextNormalizer.Normalize(candidate);
        if (IsAdditiveSalt(normalized))
            return false;

        if (NutritionVocabulary.IsExactNutritionOnlyTerm(normalized))
            return true;

        if (NutritionVocabulary.ContainsStrongNutritionLeak(normalized))
            return true;

        if (Regex.IsMatch(normalized, @"(?:^|\s)(?:kcal|kj|%vd|mg|g|ml)(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) && NutritionVocabulary.ContainsNutritionTerm(normalized))
            return true;

        if (Regex.IsMatch(normalized, @"^\d+(?:\s+\d+)*\s+[a-zçãõáéíóúâêô()/%\s]+\s+\d+(?:\s+\d+)*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return true;

        return false;
    }

    private static bool IsAdditiveSalt(string normalized) =>
        IngredientTextNormalizer.ContainsAny(normalized,
        [
            "ciclamato de sodio",
            "sacarina sodica",
            "benzoato de sodio",
            "sorbato de potassio",
            "metabissulfito de sodio",
            "citrato de sodio"
        ]);
}

public sealed class IngredientSemanticValidator
{
    public const int AcceptanceThreshold = 15;

    private static readonly string[] PositiveTerms =
    [
        "agua", "sal", "acucar", "farinha", "leite", "cacau", "suco", "extrato", "aromatizante",
        "aroma", "acidulante", "estabilizante", "conservante", "corante", "emulsificante", "oleo",
        "gordura vegetal", "soja", "proteina isolada", "proteina de soja", "vitamina", "mineral",
        "amido", "xarope", "glicose", "frutose", "maltodextrina", "dextrose", "lecitina", "goma",
        "pectina", "gelatina", "colageno", "whey", "caseina", "caseinato", "soro de leite",
        "aveia", "trigo", "cevada", "centeio", "milho", "arroz", "amendoim", "castanha", "avela",
        "macadamia", "amendoa", "ovo", "albumina", "mel", "manteiga", "creme", "polpa", "pure",
        "concentrado", "polpa", "preparado", "fermento", "antioxidante", "espessante", "umectante",
        "acido ascorbico", "ascorbico", "goma guar", "acido citrico",
        // Hortifrutícolas, especiarias e condimentos — frequentes em temperos, molhos e misturas.
        "cebola", "alho", "tomate", "pimentao", "pimenta", "salsa", "cebolinha", "oregano",
        "tomilho", "alecrim", "louro", "coentro", "cominho", "curcuma", "paprica", "pimentao vermelho",
        "pimentao amarelo", "pimentao verde", "tomate seco", "cebola em flocos", "alho granulado",
        "glutamato monossodico", "glutamato", "vinagre", "azeite", "mostarda", "curry", "gengibre",
        "canela", "cravo", "noz moscada", "erva doce", "manjericao", "salsinha",
        // Edulcorantes / polióis (genérico, vale para adoçantes, doces diet/light, bebidas).
        "sucralose", "aspartame", "acesulfame", "sacarina", "ciclamato", "stevia", "neotame",
        "taumatina", "advantame", "rebaudiosideo", "sorbitol", "manitol", "xilitol", "maltitol",
        "eritritol", "isomalte", "edulcorante", "adocante",
        // Conservantes / acidulantes nominais (benzoatos, sorbatos, parabenos, ácidos comuns).
        "benzoato", "benzoico", "sorbato", "sorbico", "nitrito", "nitrato", "metabissulfito",
        "metilparabeno", "propilparabeno", "parabeno", "acido benzoico", "acido sorbico",
        "acido lactico", "acido malico", "acido fosforico"
    ];

    private static readonly string[] PositivePrefixes =
    [
        "acidulante", "aromatizante", "conservante", "corante", "emulsificante", "estabilizante",
        "antioxidante", "espessante", "umectante", "regulador", "fermento", "vitamina", "mineral",
        "edulcorante", "adocante", "acido"
    ];

    public IngredientSemanticValidationResult Validate(string candidate)
    {
        var normalized = IngredientTextNormalizer.Normalize(candidate);
        var score = 0;
        var reasons = new List<string>();
        var additiveSalt = IsAdditiveSalt(normalized);

        foreach (var term in PositiveTerms)
        {
            if (!IngredientTextNormalizer.ContainsAny(normalized, [term]))
                continue;

            score += 25;
            reasons.Add($"positive:{term}");
            break;
        }

        foreach (var prefix in PositivePrefixes)
        {
            if (!normalized.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            score += 20;
            reasons.Add($"prefix:{prefix}");
            break;
        }

        if (Regex.IsMatch(candidate, @"^[\p{L}]+(?:\s+[\p{L}]+){0,5}$", RegexOptions.CultureInvariant))
        {
            score += 8;
            reasons.Add("simple_ingredient_shape");
        }

        if (!additiveSalt && NutritionVocabulary.ContainsNutritionTerm(normalized))
        {
            score -= NutritionVocabulary.IsExactNutritionOnlyTerm(normalized) ? 80 : 35;
            reasons.Add("negative:nutrition_term");
        }

        if (!additiveSalt && Regex.IsMatch(normalized, @"\b(?:kcal|%vd|carboidratos|proteinas|gorduras totais|gorduras saturadas|fibra alimentar|sodio|tabela nutricional)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            score -= 60;
            reasons.Add("negative:nutrition_leak");
        }

        if (Regex.IsMatch(normalized, @"^\d+(?:\s+\d+)*$", RegexOptions.CultureInvariant))
        {
            score -= 100;
            reasons.Add("negative:numeric_only");
        }

        return new IngredientSemanticValidationResult
        {
            IngredientSemanticScore = Math.Clamp(score, 0, 100),
            Reasons = reasons
        };
    }

    private static bool IsAdditiveSalt(string normalized) =>
        IngredientTextNormalizer.ContainsAny(normalized,
        [
            "ciclamato de sodio",
            "sacarina sodica",
            "benzoato de sodio",
            "sorbato de potassio",
            "metabissulfito de sodio",
            "citrato de sodio"
        ]);
}

public sealed class IngredientSemanticValidationResult
{
    public int IngredientSemanticScore { get; set; }
    public List<string> Reasons { get; set; } = [];
}

internal static class NutritionVocabulary
{
    private static readonly string[] ExactNutritionOnlyTerms =
    [
        "valor energetico", "gorduras totais", "gordura total", "gorduras saturadas", "gordura saturada",
        "gorduras trans", "gordura trans", "acucares", "açucares", "acucares totais", "açucares totais", "acucares adicionados",
        "açucares adicionados", "fibra alimentar", "fibras alimentares", "sodio", "proteinas",
        "proteina", "carboidratos", "carboidrato", "calorias", "porcao", "porcoes"
    ];

    private static readonly string[] NutritionLeakTerms =
    [
        "valor energetico", "gorduras totais", "gordura total", "gorduras saturadas", "gordura saturada",
        "gorduras trans", "gordura trans", "acucares", "açucares", "acucares totais", "açucares totais", "acucares adicionados",
        "açucares adicionados", "fibra alimentar", "fibras alimentares", "sodio", "carboidratos",
        "proteinas", "tabela nutricional", "informacao nutricional", "%vd", "valor diario", "kcal", "kj"
    ];

    public static bool IsExactNutritionOnlyTerm(string normalized) =>
        ExactNutritionOnlyTerms.Contains(normalized.Trim(' ', '.', ',', ';', ':'), StringComparer.OrdinalIgnoreCase);

    public static bool ContainsStrongNutritionLeak(string normalized) =>
        NutritionLeakTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal));

    public static bool ContainsNutritionTerm(string normalized) =>
        NutritionLeakTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal));
}
