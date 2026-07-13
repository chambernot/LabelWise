using System.Text.RegularExpressions;

namespace LabelWise.Infrastructure.Services.OcrParsing;

/// <summary>
/// Banco de padrões multilíngua (PT + ES) para identificação de nutrientes em texto OCR.
/// Utilizado por todos os parsers da camada determinística.
/// </summary>
internal static class NutrientPatternBank
{
    /// <summary>
    /// Padrões ordenados por especificidade (mais específico primeiro).
    /// A ordem importa: addedSugar antes de sugar, saturatedFat antes de fat.
    /// </summary>
    public static readonly (string Key, Regex Pattern)[] Patterns =
    [
        // ── Calorias ────────────────────────────────────────────────────
        ("calories", new Regex(
            @"(?:valor\s+energ[eé]tico|calorias?|energia|energ[ií]a)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // ── Carboidratos ────────────────────────────────────────────────
        ("carbs", new Regex(
            @"(?:carboidratos?(?:\s+(?:totais?|dispon[íi]veis?))?|carbohidratos?(?:\s+totales?)?|hidratos?\s+de\s+carbono)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // ── Açúcares adicionados (deve vir ANTES de açúcares) ──────────
        ("addedSugar", new Regex(
            @"(?:a[çc][úu]cares?\s+adicionad(?:os?|as?)|az[uú]cares?\s+a[ñn]adid(?:os?|as?))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // ── Açúcares totais ────────────────────────────────────────────
        ("sugar", new Regex(
            @"(?:a[çc][úu]cares?(?:\s+totais?)?|az[uú]cares?(?:\s+totales?)?)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // ── Proteínas ──────────────────────────────────────────────────
        ("protein", new Regex(
            @"prote[íi]nas?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // ── Gorduras saturadas (deve vir ANTES de gorduras totais) ─────
        ("saturatedFat", new Regex(
            @"(?:gorduras?\s+saturadas?|grasas?\s+saturadas?)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // ── Gorduras totais ────────────────────────────────────────────
        ("fat", new Regex(
            @"(?:gorduras?\s+(?:totais?)?|grasas?\s+(?:totales?)?|l[íi]pidos?(?:\s+totais?)?)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // ── Fibras ─────────────────────────────────────────────────────
        ("fiber", new Regex(
            @"fibras?(?:\s+(?:alimentares?|diet[eé]ticas?|sol[uú]veis?|insol[uú]veis?))?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // ── Sódio ──────────────────────────────────────────────────────
        ("sodium", new Regex(
            @"s[oó]dio",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),
    ];

    /// <summary>
    /// Retorna o par (chave, padrão) da primeira correspondência encontrada na linha,
    /// ou null se nenhum nutriente for detectado.
    /// </summary>
    public static (string Key, Regex Pattern)? Detect(string normalizedLine)
    {
        foreach (var entry in Patterns)
            if (entry.Pattern.IsMatch(normalizedLine))
                return entry;
        return null;
    }

    // ── Padrões auxiliares ───────────────────────────────────────────────────

    /// <summary>
    /// Remove porcentagens (%VD) antes de extrair números para evitar falsos positivos.
    /// </summary>
    public static readonly Regex PercentValue = new(
        @"\b\d+(?:[,\.]\d+)?\s*%(?:\s*vd)?\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Extrai números numéricos com unidades opcionais, ignorando porcentagens já removidas.
    /// </summary>
    public static readonly Regex NumberExtractor = new(
        @"(\d+(?:[,\.]\d+)?)\s*(?:g|mg|kcal|kj)?(?!\s*%)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Detecta marcador de coluna "100 g / 100 ml" no cabeçalho.
    /// </summary>
    public static readonly Regex Per100gMarker = new(
        @"\b100\s*(?:g|ml)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Detecta tamanho de porção: "porção de 30 g", "por porção (30g)", etc.
    /// Grupo 1 = número da porção.
    /// </summary>
    public static readonly Regex ServingSizeDetector = new(
        @"por[çc][aã]o\s*(?:de\s*)?[\(\[]?\s*(\d+(?:[,\.]\d+)?)\s*g",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Detecta marcador explícito de coluna "por porção" (sem o número da porção).
    /// </summary>
    public static readonly Regex PerPortionColumnMarker = new(
        @"por\s+por[çc][aã]o",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Linhas de ruído que devem ser ignoradas por todos os parsers.
    /// </summary>
    public static readonly string[] NoiseTerms =
    [
        "ingredientes", "alérgicos", "alergenicos", "contém glúten", "contem gluten",
        "não contém glúten", "nao contem gluten", "modo de preparo", "conservação",
        "conservacao", "sugestão de consumo", "sugestao de consumo",
        "informações do fabricante", "informacoes do fabricante",
        "marketing", "promoção", "promocao", "imagem meramente ilustrativa",
        "ingredientes:", "alérgenos:", "alergenos:"
    ];

    /// <summary>
    /// Cabeçalhos estruturais da tabela nutricional que não contêm valores nutricionais.
    /// </summary>
    public static readonly Regex HeaderNoise = new(
        @"^(?:%\s*vd|vd|valores?\s+di[aá]rios?|quantidade\s+por\s+por[çc][aã]o|100\s*(?:g|ml)|informa[çc][õo]es?\s+nutricionais?)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Retorna true se a linha deve ser ignorada por todos os parsers.
    /// </summary>
    public static bool ShouldIgnore(string normalizedLine)
    {
        if (string.IsNullOrWhiteSpace(normalizedLine)) return true;
        if (HeaderNoise.IsMatch(normalizedLine)) return true;
        if (NoiseTerms.Any(t => normalizedLine.Contains(t, StringComparison.OrdinalIgnoreCase))) return true;
        return false;
    }

    /// <summary>
    /// Normaliza uma linha: lower-case, espaços múltiplos → único.
    /// </summary>
    public static string Normalize(string line) =>
        System.Text.RegularExpressions.Regex.Replace(line.Trim().ToLowerInvariant(), @"\s+", " ");

    /// <summary>
    /// Extrai todos os valores numéricos de uma string (após remover %VD).
    /// </summary>
    public static List<double> ExtractNumbers(string text)
    {
        text = PercentValue.Replace(text, " ");
        var result = new List<double>();
        foreach (Match m in NumberExtractor.Matches(text))
        {
            var raw = m.Groups[1].Value.Replace(',', '.');
            if (double.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var val))
                result.Add(val);
        }
        return result;
    }
}
