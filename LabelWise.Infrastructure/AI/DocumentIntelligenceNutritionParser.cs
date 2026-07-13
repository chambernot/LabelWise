using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using LabelWise.Application.Models.Nutrition;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.AI;

/// <summary>
/// Mapeia tabelas do Azure Document Intelligence para campos nutricionais.
///
/// Estratégia:
///   1. Itera result.Tables — sem regex, sem GPT, apenas células estruturadas.
///   2. Coluna 0 = rótulo; restantes = valores.
///   3. Detecta a coluna "100 g" varrendo TODAS as células (não apenas row 0).
///   4. Matching de rótulo por "contains" para tolerar sufixos de unidade "(g)", "(mg)".
///   5. Não sobrescreve campos já atribuídos (primeiro match vence).
///   6. Aplica validação de consistência calórica após extração.
/// </summary>
internal sealed class DocumentIntelligenceNutritionParser
{
    // ── Label → field key mapping (normalised, sem acentos) ─────────────────
    // Cobre PT + ES + EN. Ordenado do mais específico para o mais genérico para
    // evitar falsos positivos no match por "contains"
    // (ex: "gorduras saturadas" antes de "gorduras").
    private static readonly (string Key, string Field)[] LabelMap =
    [
        // ── Calorias / Energy ────────────────────────────────────────────
        ("valor energetico",        "calories"),
        ("valor calorico",          "calories"),
        ("energia",                 "calories"),
        ("calorias",                "calories"),
        ("calories",                "calories"),
        ("energy",                  "calories"),
        ("energia total",           "calories"),

        // ── Gorduras saturadas / Grasas saturadas / Saturated fat ────────
        ("gorduras saturadas",      "saturatedFat"),
        ("gordura saturada",        "saturatedFat"),
        ("grasas saturadas",        "saturatedFat"),
        ("grasa saturada",          "saturatedFat"),
        ("saturated fat",           "saturatedFat"),
        ("saturated fatty acids",   "saturatedFat"),
        ("lipidos saturados",       "saturatedFat"),

        // ── Gorduras totais / Grasas totales / Total fat ─────────────────
        ("gorduras totais",         "fat"),
        ("gordura total",           "fat"),
        ("grasas totales",          "fat"),
        ("grasas",                  "fat"),
        ("gorduras",                "fat"),
        ("gordura",                 "fat"),
        ("total fat",               "fat"),
        ("lipidos totais",          "fat"),
        ("lipidos",                 "fat"),

        // ── Açúcares adicionados / Azúcares añadidos / Added sugars ──────
        ("acucares adicionados",    "addedSugar"),
        ("acucar adicionado",       "addedSugar"),
        ("azucares anadidos",       "addedSugar"),
        ("azucar anadido",          "addedSugar"),
        ("added sugars",            "addedSugar"),
        ("added sugar",             "addedSugar"),

        // ── Açúcares / Azúcares / Sugars ─────────────────────────────────
        ("acucares totais",         "sugar"),
        ("acucares",                "sugar"),
        ("azucares totales",        "sugar"),
        ("azucares",                "sugar"),
        ("sugars",                  "sugar"),

        // ── Carboidratos / Carbohidratos / Carbohydrates ─────────────────
        ("carboidratos",            "carbs"),
        ("carboidrato",             "carbs"),
        ("carbohidratos",           "carbs"),
        ("hidratos de carbono",     "carbs"),
        ("carbohydrates",           "carbs"),
        ("carbohydrate",            "carbs"),
        ("total carbohydrate",      "carbs"),

        // ── Proteínas / Proteinas / Protein ──────────────────────────────
        ("proteinas",               "protein"),
        ("proteina",                "protein"),
        ("protein",                 "protein"),
        ("proteins",                "protein"),

        // ── Fibras / Fibra / Fiber ────────────────────────────────────────
        ("fibras alimentares",      "fiber"),
        ("fibra alimentar",         "fiber"),
        ("fibra dietetica",         "fiber"),
        ("fibras dieteticas",       "fiber"),
        ("fibras",                  "fiber"),
        ("fibra",                   "fiber"),
        ("dietary fiber",           "fiber"),
        ("dietary fibre",           "fiber"),

        // ── Sódio / Sodio / Sodium ────────────────────────────────────────
        ("sodio",                   "sodium"),
        ("sodium",                  "sodium"),
    ];

    private readonly ILogger _logger;

    // ── Label keyword map for proximity parsing (PT + ES + EN) ─────────────
    // Ordered most-specific first so "gorduras saturadas" beats "gorduras".
    private static readonly (string Key, string Field)[] ProximityLabelMap =
    [
        ("valor energetico",    "calories"),
        ("energia",             "calories"),
        ("calorias",            "calories"),
        ("calories",            "calories"),
        ("energy",              "calories"),
        ("gorduras saturadas",  "saturatedFat"),
        ("gordura saturada",    "saturatedFat"),
        ("grasas saturadas",    "saturatedFat"),
        ("grasa saturada",      "saturatedFat"),
        ("saturated fat",       "saturatedFat"),
        ("gorduras totais",     "fat"),
        ("gordura total",       "fat"),
        ("grasas totales",      "fat"),
        ("grasas",              "fat"),
        ("gorduras",            "fat"),
        ("gordura",             "fat"),
        ("total fat",           "fat"),
        ("acucares adicionados","addedSugar"),
        ("acucar adicionado",   "addedSugar"),
        ("azucares anadidos",   "addedSugar"),
        ("added sugars",        "addedSugar"),
        ("acucares totais",     "sugar"),
        ("acucares",            "sugar"),
        ("azucares totales",    "sugar"),
        ("azucares",            "sugar"),
        ("sugars",              "sugar"),
        ("carboidratos",        "carbs"),
        ("carbohidratos",       "carbs"),
        ("carbohydrates",       "carbs"),
        ("carbs",               "carbs"),
        ("proteinas",           "protein"),
        ("proteina",            "protein"),
        ("protein",             "protein"),
        ("fibras alimentares",  "fiber"),
        ("fibra alimentar",     "fiber"),
        ("fibra dietetica",     "fiber"),
        ("fibras",              "fiber"),
        ("dietary fiber",       "fiber"),
        ("fibre",               "fiber"),
        ("sodio",               "sodium"),
        ("sodium",              "sodium"),
    ];

    // ── OCR noise tokens to strip before matching ────────────────────────
    private static readonly string[] OcrNoiseTokens =
        ["(a)", "(o)", "(b)", "totals", "total:", "amount", "daily value", "%", "dv", "vd", "*"];

    // ── OCR error corrections applied during normalization ───────────────
    // Ordered longest-first to avoid partial replacements.
    // Each entry is (misspelled pattern, correct form).
    private static readonly (string From, string To)[] OcrCorrections =
    [
        // Açúcares — OCR variants
        ("lasucares",               "acucares"),
        ("lacucares",               "acucares"),
        ("acucar adicionado",       "acucares adicionados"),
        ("azucares anadidos",       "acucares adicionados"),
        ("azucares totales",        "acucares totais"),
        ("azucares",                "acucares"),
        ("acucar",                  "acucares"),
        // Calorias / Energia
        ("calor energetico",        "calorias"),
        ("valor energetico",        "calorias"),
        ("valor calorico",          "calorias"),
        ("energia total",           "calorias"),
        ("energia kcal",            "calorias"),
        ("energia",                 "calorias"),
        // Gorduras
        ("roupas totais",           "gorduras"),
        ("gord totais",             "gorduras"),
        ("gorduras totais",         "gorduras"),
        ("gordura total",           "gorduras"),
        ("lipidos totais",          "gorduras"),
        ("lipidos",                 "gorduras"),
        ("grasas totales",          "gorduras"),
        ("grasas",                  "gorduras"),
        // Gorduras saturadas — must come after "gorduras totais"
        ("gorduras saturadas",      "gordura saturada"),
        ("grasas saturadas",        "gordura saturada"),
        ("grasa saturada",          "gordura saturada"),
        ("saturated fat",           "gordura saturada"),
        ("lipidos saturados",       "gordura saturada"),
        // Carboidratos
        ("carboidratos dos quais",  "carboidratos"),
        ("hidratos de carbono",     "carboidratos"),
        ("carbohidratos",           "carboidratos"),
        ("carbohydrates",           "carboidratos"),
        ("carbohydrate",            "carboidratos"),
        ("total carbohydrate",      "carboidratos"),
        // Proteínas
        ("proteinas",               "proteina"),
        ("proteins",                "proteina"),
        ("protein",                 "proteina"),
        // Fibras
        ("fibras alimentares",      "fibra"),
        ("fibra alimentar",         "fibra"),
        ("fibra dietetica",         "fibra"),
        ("fibras dieteticas",       "fibra"),
        ("dietary fiber",           "fibra"),
        ("dietary fibre",           "fibra"),
        ("fibras",                  "fibra"),
        // Sódio
        ("sodium",                  "sodio"),
    ];

    public DocumentIntelligenceNutritionParser(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extrai dados nutricionais do texto bruto do OCR quando nenhuma tabela estruturada foi detectada.
    ///
    /// Três passagens de extração em ordem de prioridade:
    ///   1. Proximidade por linha  — varredura linha a linha com lookahead de 2.
    ///   2. Regex por campo        — padrões específicos sobre o texto normalizado completo.
    ///   3. Fallback global        — detecta kcal e ordem posicional de macros.
    ///
    /// Correções OCR são aplicadas durante a normalização antes de qualquer passagem.
    /// </summary>
    public DocumentIntelligenceNutritionResult ParseFromRawText(string text)
    {
        var output = new DocumentIntelligenceNutritionResult();
        var raw    = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(text))
            return output;

        _logger.LogInformation("[TEXT_PARSER] Iniciando — texto bruto ({Length} chars).", text.Length);

        // ── Normalize all lines (including OCR corrections) ───────────────
        string[] normLines = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeOcrLine)
            .ToArray();

        // Full normalized text used by regex passes
        string fullNorm = string.Join(" ", normLines);

        // ── Detect serving size and 100g column presence ─────────────────
        double? servingGrams = DetectServingSize(normLines);
        bool    has100gCol   = normLines.Any(l => Regex.IsMatch(l, @"\b100\s*g\b"));

        if (servingGrams.HasValue)
            _logger.LogDebug("[TEXT_PARSER] Porção detectada: {Serving}g, coluna 100g: {Has100g}",
                servingGrams.Value, has100gCol);

        // ════════════════════════════════════════════════════════════════
        // PASSAGEM 1 — Proximidade por linha (principal)
        // ════════════════════════════════════════════════════════════════
        for (int i = 0; i < normLines.Length; i++)
        {
            string line = normLines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            string? fieldKey = ResolveProximityFieldKey(line);
            if (fieldKey is null || raw.ContainsKey(fieldKey)) continue;

            double? value = ExtractNextNumber(normLines, i, lookahead: 2);
            if (!value.HasValue || !IsPlausible(fieldKey, value.Value)) continue;

            if (!has100gCol && servingGrams is { } portion && portion > 0)
            {
                double converted = value.Value * (100.0 / portion);
                _logger.LogDebug("[TEXT_PARSER] {Field}: {Raw}/porção → {Conv}/100g",
                    fieldKey, value.Value, Math.Round(converted, 2));
                value = converted;
                if (!IsPlausible(fieldKey, value.Value)) continue;
            }

            raw[fieldKey] = value.Value;
            _logger.LogDebug("[TEXT_PARSER] campo detectado (proximidade): {Field} = {Value}", fieldKey, value.Value);
        }

        // ════════════════════════════════════════════════════════════════
        // PASSAGEM 2 — Regex específico por campo (complementa lacunas)
        // ════════════════════════════════════════════════════════════════
        ExtractByFieldPatterns(fullNorm, raw);

        // ════════════════════════════════════════════════════════════════
        // PASSAGEM 3 — Fallback global (kcal + posição de macros)
        // ════════════════════════════════════════════════════════════════
        if (raw.Count < 2)
        {
            _logger.LogWarning("[TEXT_PARSER] fallback ativado — passagens 1 e 2 extraíram < 2 campos.");
            TryGlobalFallback(fullNorm, raw);
        }

        // ── Inferência "dos quais açúcares" ──────────────────────────────
        // Quando o OCR quebra "carboidratos, dos quais açúcares X g" em linhas
        // separadas, o parser captura sugar mas perde carbs.
        // Regra: se sugar foi extraído e carbs está ausente, carbs = sugar.
        // Também aplicado quando o texto contém explicitamente "dos quais".
        if (raw.TryGetValue("sugar", out double sugarVal) && !raw.ContainsKey("carbs"))
        {
            raw["carbs"] = sugarVal;
            bool hasDosQuais = fullNorm.Contains("dos quais", StringComparison.OrdinalIgnoreCase);
            _logger.LogInformation(
                "[TEXT_PARSER] carbs inferido a partir de sugar={Sugar} ({Reason}).",
                sugarVal, hasDosQuais ? "estrutura 'dos quais'" : "fallback sugar→carbs");
        }

        // ── Validar plausibilidade entre campos ───────────────────────────
        EnforceFieldConstraints(raw);

        MapRawToResult(raw, output);
        output.ExtractionMode    = "TEXT_ONLY";
        output.HasNutritionTable = false;

        ValidateCalorieConsistency(output);

        // ── Logging de confiança ──────────────────────────────────────────
        string confidence = raw.Count >= 5 ? "alta" : raw.Count >= 3 ? "media" : "baixa";
        _logger.LogInformation(
            "[TEXT_PARSER] confiança: {Confidence} — {Count} campo(s) extraído(s): {Fields}",
            confidence, raw.Count, string.Join(", ", raw.Keys));

        if (raw.Count < 2)
            output.Warnings.Add("Poucos campos nutricionais detectados — análise pode ser imprecisa.");

        return output;
    }

    // ════════════════════════════════════════════════════════════════════════
    // PASSAGEM 2 — Padrões regex por campo
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aplica padrões regex específicos por campo sobre o texto normalizado completo.
    /// Preenche apenas campos ainda ausentes em <paramref name="raw"/>.
    /// </summary>
    private void ExtractByFieldPatterns(string normText, Dictionary<string, double> raw)
    {
        // ── Calorias: prioridade para "NNN kcal" ─────────────────────────
        if (!raw.ContainsKey("calories"))
        {
            double? v = TryPattern(normText, @"(\d+(?:\.\d+)?)\s*kcal", "calories");
            if (v is null)
                v = TryLabelPattern(normText, @"calorias[^\d]{0,25}(\d+(?:\.\d+)?)", "calories");
            if (v.HasValue)
            {
                raw["calories"] = v.Value;
                _logger.LogDebug("[TEXT_PARSER] campo detectado (regex): calories = {Value}", v.Value);
            }
        }

        // ── Carboidratos ──────────────────────────────────────────────────
        if (!raw.ContainsKey("carbs"))
        {
            double? v = TryLabelPattern(normText, @"carboidratos[^\d]{0,25}(\d+(?:\.\d+)?)", "carbs");
            if (v.HasValue)
            {
                raw["carbs"] = v.Value;
                _logger.LogDebug("[TEXT_PARSER] campo detectado (regex): carbs = {Value}", v.Value);
            }
        }

        // ── Açúcares adicionados — antes de açúcares totais ──────────────
        if (!raw.ContainsKey("addedSugar"))
        {
            double? v = TryLabelPattern(normText, @"acucares adicionados[^\d]{0,25}(\d+(?:\.\d+)?)", "addedSugar");
            if (v.HasValue)
            {
                raw["addedSugar"] = v.Value;
                _logger.LogDebug("[TEXT_PARSER] campo detectado (regex): addedSugar = {Value}", v.Value);
            }
        }

        // ── Açúcares ──────────────────────────────────────────────────────
        if (!raw.ContainsKey("sugar"))
        {
            double? v = TryLabelPattern(normText, @"acucares[^\d]{0,25}(\d+(?:\.\d+)?)", "sugar");
            if (v.HasValue)
            {
                raw["sugar"] = v.Value;
                _logger.LogDebug("[TEXT_PARSER] campo detectado (regex): sugar = {Value}", v.Value);
            }
        }

        // ── Proteína ──────────────────────────────────────────────────────
        if (!raw.ContainsKey("protein"))
        {
            double? v = TryLabelPattern(normText, @"proteina[^\d]{0,25}(\d+(?:\.\d+)?)", "protein");
            if (v.HasValue)
            {
                raw["protein"] = v.Value;
                _logger.LogDebug("[TEXT_PARSER] campo detectado (regex): protein = {Value}", v.Value);
            }
        }

        // ── Gordura saturada — antes de gordura total ─────────────────────
        if (!raw.ContainsKey("saturatedFat"))
        {
            double? v = TryLabelPattern(normText, @"gordura saturada[^\d]{0,25}(\d+(?:\.\d+)?)", "saturatedFat");
            if (v.HasValue)
            {
                raw["saturatedFat"] = v.Value;
                _logger.LogDebug("[TEXT_PARSER] campo detectado (regex): saturatedFat = {Value}", v.Value);
            }
        }

        // ── Gordura total ─────────────────────────────────────────────────
        if (!raw.ContainsKey("fat"))
        {
            double? v = TryLabelPattern(normText, @"gorduras?[^\d]{0,25}(\d+(?:\.\d+)?)", "fat");
            if (v.HasValue)
            {
                raw["fat"] = v.Value;
                _logger.LogDebug("[TEXT_PARSER] campo detectado (regex): fat = {Value}", v.Value);
            }
        }

        // ── Sódio ─────────────────────────────────────────────────────────
        if (!raw.ContainsKey("sodium"))
        {
            double? v = TryLabelPattern(normText, @"sodio[^\d]{0,25}(\d+(?:\.\d+)?)", "sodium");
            if (v.HasValue)
            {
                raw["sodium"] = v.Value;
                _logger.LogDebug("[TEXT_PARSER] campo detectado (regex): sodium = {Value}", v.Value);
            }
        }

        // ── Fibra ─────────────────────────────────────────────────────────
        if (!raw.ContainsKey("fiber"))
        {
            double? v = TryLabelPattern(normText, @"fibra[^\d]{0,25}(\d+(?:\.\d+)?)", "fiber");
            if (v.HasValue)
            {
                raw["fiber"] = v.Value;
                _logger.LogDebug("[TEXT_PARSER] campo detectado (regex): fiber = {Value}", v.Value);
            }
        }
    }

    /// <summary>
    /// Executa um padrão regex e valida o valor contra o campo.
    /// </summary>
    private static double? TryPattern(string text, string pattern, string fieldKey)
    {
        var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        if (!double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double v))
            return null;
        return IsPlausible(fieldKey, v) ? v : null;
    }

    /// <summary>
    /// Executa um padrão label+valor, normalizando separador decimal e retornando
    /// o último grupo numérico capturado. Retorna null se fora dos limites do campo.
    /// </summary>
    private static double? TryLabelPattern(string text, string pattern, string fieldKey)
    {
        var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        string raw = m.Groups[1].Value.Replace(',', '.');
        if (!double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double v))
            return null;
        return IsPlausible(fieldKey, v) ? v : null;
    }

    // ════════════════════════════════════════════════════════════════════════
    // PASSAGEM 3 — Fallback global
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Último recurso quando passagens 1 e 2 extraíram menos de 2 campos.
    ///
    /// Estratégia:
    ///   a) Busca "NNN kcal" para calorias se ainda ausente.
    ///   b) Coleta todos os números plausíveis presentes no texto e tenta
    ///      inferir macros pela ordem canônica: calorias, carbs, proteína, gordura.
    /// </summary>
    private void TryGlobalFallback(string normText, Dictionary<string, double> raw)
    {
        // (a) kcal pattern
        if (!raw.ContainsKey("calories"))
        {
            var mKcal = Regex.Match(normText, @"(\d+(?:\.\d+)?)\s*kcal", RegexOptions.IgnoreCase);
            if (mKcal.Success &&
                double.TryParse(mKcal.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double kcal) &&
                IsPlausible("calories", kcal))
            {
                raw["calories"] = kcal;
                _logger.LogDebug("[TEXT_PARSER] fallback global: calories = {Value} (kcal pattern)", kcal);
            }
        }

        // (b) Positional inference: collect all plausible numbers
        var allNumbers = Regex
            .Matches(normText, @"(?<![.\d])(\d+(?:\.\d+)?)(?![.\d])")
            .Select(m =>
            {
                double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double d);
                return d;
            })
            .Where(d => d > 0 && d < 1000)
            .Distinct()
            .ToList();

        if (allNumbers.Count < 3) return;

        // Canonical order: calories (>= 50), carbs, protein, fat
        var candidates = allNumbers.Where(n => n >= 50 && n <= 900).ToList();
        var macros     = allNumbers.Where(n => n > 0  && n < 100).ToList();

        if (!raw.ContainsKey("calories") && candidates.Count > 0)
        {
            raw["calories"] = candidates[0];
            _logger.LogDebug("[TEXT_PARSER] fallback global: calories = {Value} (posicional)", candidates[0]);
        }

        if (macros.Count >= 3)
        {
            if (!raw.ContainsKey("carbs"))
            {
                raw["carbs"] = macros[0];
                _logger.LogDebug("[TEXT_PARSER] fallback global: carbs = {Value} (posicional)", macros[0]);
            }
            if (!raw.ContainsKey("protein"))
            {
                raw["protein"] = macros[1];
                _logger.LogDebug("[TEXT_PARSER] fallback global: protein = {Value} (posicional)", macros[1]);
            }
            if (!raw.ContainsKey("fat"))
            {
                raw["fat"] = macros[2];
                _logger.LogDebug("[TEXT_PARSER] fallback global: fat = {Value} (posicional)", macros[2]);
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // VALIDAÇÃO de restrições entre campos
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aplica regras de consistência entre campos antes de mapear para o resultado.
    ///   - Calorias devem ser >= carboidratos (descarta carbs se inconsistente).
    ///   - Gordura saturada não pode ser maior que gordura total.
    ///   - Açúcares não podem ser maiores que carboidratos.
    /// </summary>
    private void EnforceFieldConstraints(Dictionary<string, double> raw)
    {
        if (raw.TryGetValue("calories", out double cal) &&
            raw.TryGetValue("carbs", out double carbs) &&
            cal < carbs)
        {
            _logger.LogWarning("[TEXT_PARSER] descartando carbs={Carbs} — maior que calories={Cal}.", carbs, cal);
            raw.Remove("carbs");
        }

        if (raw.TryGetValue("fat", out double fat) &&
            raw.TryGetValue("saturatedFat", out double satFat) &&
            satFat > fat)
        {
            _logger.LogWarning("[TEXT_PARSER] descartando saturatedFat={Sat} — maior que fat={Fat}.", satFat, fat);
            raw.Remove("saturatedFat");
        }

        if (raw.TryGetValue("carbs", out double c) &&
            raw.TryGetValue("sugar", out double s))
        {
            if (s > c + 0.1)
            {
                // Açúcar maior que carboidratos é impossível fisicamente —
                // o valor de carbs provavelmente foi lido errado: ajusta para sugar.
                _logger.LogWarning(
                    "[TEXT_PARSER] sugar={Sugar} > carbs={Carbs} — corrigindo carbs para {Sugar}.", s, c, s);
                raw["carbs"] = s;
            }
            else if (Math.Abs(s - c) < 0.1)
            {
                // Todos os carboidratos são açúcares — caso válido (ex: refrigerante).
                _logger.LogDebug("[TEXT_PARSER] sugar ≈ carbs ({Value}) — todos os carboidratos são açúcares.", s);
            }
        }
    }

    // ── Proximity helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Normalizes a single OCR line: strips accents, lower-cases, replaces comma
    /// decimal separators, removes known OCR noise tokens, and applies OCR error
    /// corrections so that common misreads map to canonical label forms.
    /// </summary>
    private static string NormalizeOcrLine(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        string decomposed = raw.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);

        string result = sb.ToString()
                          .Normalize(NormalizationForm.FormC)
                          .ToLowerInvariant()
                          .Trim();

        foreach (var token in OcrNoiseTokens)
            result = result.Replace(token, " ", StringComparison.OrdinalIgnoreCase);

        // Normalize comma decimal separator: "2,5 g" → "2.5 g"
        result = Regex.Replace(result, @"(\d),(\d)", "$1.$2");

        result = Regex.Replace(result, @"\s{2,}", " ").Trim();

        // Apply OCR corrections after base normalization
        foreach (var (from, to) in OcrCorrections)
            result = result.Replace(from, to, StringComparison.OrdinalIgnoreCase);

        return result;
    }

    /// <summary>
    /// Resolves a field key from a normalized line using the proximity label map.
    /// </summary>
    private static string? ResolveProximityFieldKey(string normalizedLine)
    {
        foreach (var (key, field) in ProximityLabelMap)
        {
            if (normalizedLine.Contains(key))
                return field;
        }
        return null;
    }

    /// <summary>
    /// Extracts the first valid positive number found from <paramref name="startIndex"/>
    /// through up to <paramref name="lookahead"/> additional lines.
    /// </summary>
    private static double? ExtractNextNumber(string[] lines, int startIndex, int lookahead)
    {
        int end = Math.Min(startIndex + lookahead, lines.Length - 1);
        for (int i = startIndex; i <= end; i++)
        {
            var m = Regex.Match(lines[i], @"(?<![.\d])(\d+(?:\.\d+)?)(?![.\d])");
            if (!m.Success) continue;

            if (double.TryParse(m.Groups[1].Value, NumberStyles.Any,
                                CultureInfo.InvariantCulture, out double val) && val > 0)
                return val;
        }
        return null;
    }

    /// <summary>
    /// Detects the serving size in grams from normalized lines.
    /// Matches patterns like "porcao 40g", "serving size 30 g", "tamano de porcion 45g".
    /// </summary>
    private static double? DetectServingSize(string[] normLines)
    {
        var portionPattern = new Regex(
            @"por[cç][aã]o[^0-9]*(\d+(?:\.\d+)?)\s*g" +
            @"|serving\s+size[^0-9]*(\d+(?:\.\d+)?)\s*g" +
            @"|tama[nn]o\s+de\s+porci[oo]n[^0-9]*(\d+(?:\.\d+)?)\s*g",
            RegexOptions.IgnoreCase);

        foreach (var line in normLines)
        {
            var m = portionPattern.Match(line);
            if (!m.Success) continue;

            for (int g = 1; g <= 3; g++)
            {
                if (!string.IsNullOrEmpty(m.Groups[g].Value) &&
                    double.TryParse(m.Groups[g].Value, NumberStyles.Any,
                                    CultureInfo.InvariantCulture, out double v) && v > 0)
                    return v;
            }
        }
        return null;
    }

    public DocumentIntelligenceNutritionResult Parse(AnalyzeResult result)
    {
        var output = new DocumentIntelligenceNutritionResult();
        var raw = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        double? portionGrams = ExtractPortion(result);

        foreach (var table in result.Tables)
        {
            var rows = table.Cells
                .GroupBy(c => c.RowIndex)
                .ToDictionary(g => g.Key, g => g.OrderBy(c => c.ColumnIndex).ToList());

            foreach (var row in rows)
            {
                foreach (var cell in row.Value)
                {
                    string label = Normalize(cell.Content);

                    if (!IsNutritionLabel(label)) continue;

                    string? field = ResolveFieldKey(label);
                    if (field == null) continue;

                    double? value = ExtractValueFromRowOrNearby(rows, row.Key, cell.ColumnIndex, field);

                    if (!value.HasValue) continue;

                    if (!raw.ContainsKey(field))
                    {
                        raw[field] = value.Value;
                    }
                }
            }
        }

        NormalizeTo100g(raw, portionGrams);

        MapRawToResult(raw, output);

        ValidateCalories(output);

        return output;
    }

    private void ValidateCalories(DocumentIntelligenceNutritionResult r)
    {
        if (r?.Calories?.Value is not { } actual) return;

        double? carbs   = r.Carbs?.Value;
        double? protein = r.Protein?.Value;
        double? fat     = r.Fat?.Value;

        if (carbs is null || protein is null || fat is null) return;

        double expected = carbs.Value * 4 + protein.Value * 4 + fat.Value * 9;
        double diff     = Math.Abs(actual - expected);

        if (diff > 50)
        {
            r.HasCaloriesInconsistency = true;
            r.Warnings.Add(
                $"Inconsistência calórica: declarado={actual:0} kcal, " +
                $"calculado={expected:0} kcal (diferença {diff:0} kcal)");
            _logger.LogWarning("[DI_PARSER] Inconsistência calórica: declarado={Actual}, calculado={Expected}",
                actual, expected);
        }
    }

    private string RemoveAccents(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var unicodeCategory = Char.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private bool IsNutritionLabel(string label)
    {
        return label.Contains("calor") ||
               label.Contains("carb") ||
               label.Contains("prote") ||
               label.Contains("gord") ||
               label.Contains("gras") ||
               label.Contains("sodio") ||
               label.Contains("fibra") ||
               label.Contains("azuc") ||
               label.Contains("sugar");
    }
    private string Normalize(string text)
    {
        text = text.ToLower();
        text = text.Replace(",", ".");
        text = RemoveAccents(text);

        text = text
            .Replace("totals", "totais")
            .Replace("grasas", "gorduras");

        return text;
    }
    private static string? ResolveFieldKey(string label)
    {
        if (label.Contains("calor")) return "calories";
        if (label.Contains("carb")) return "carbs";
        if (label.Contains("prote")) return "protein";
        if (label.Contains("gord") || label.Contains("gras")) return "fat";
        if (label.Contains("satur")) return "saturatedFat";
        if (label.Contains("sodio")) return "sodium";
        if (label.Contains("fibra")) return "fiber";

        return null;
    }
    private double? ExtractPortion(AnalyzeResult result)
    {
        foreach (var page in result.Pages)
        {
            foreach (var line in page.Lines)
            {
                var text = line.Content.ToLower();

                var match = Regex.Match(text, @"(\d+)\s*g");

                if (text.Contains("porc") && match.Success)
                {
                    return double.Parse(match.Groups[1].Value);
                }
            }
        }

        return null;
    }
    private void NormalizeTo100g(Dictionary<string, double> raw, double? portion)
    {
        if (!portion.HasValue || portion == 0) return;

        double factor = 100.0 / portion.Value;

        foreach (var key in raw.Keys.ToList())
        {
            raw[key] = raw[key] * factor;
        }
    }
    private double? ParseNumberWithUnit(string text, string field)
    {
        text = text.ToLower();

        var match = Regex.Match(text, @"\d+([.,]\d+)?");

        if (!match.Success) return null;

        double value = double.Parse(match.Value.Replace(",", "."), CultureInfo.InvariantCulture);

        if (field == "calories")
        {
            if (!text.Contains("kcal")) return null; // só aceita kcal
        }
        else
        {
            if (text.Contains("kcal")) return null; // ignora kcal em macros
        }

        return value;
    }
    private double? ExtractValueFromRowOrNearby(
    Dictionary<int, List<DocumentTableCell>> rows,
    int rowIndex,
    int columnIndex,
    string field)
    {
        // 1. tentar na mesma linha (à direita)
        var row = rows[rowIndex];

        var candidates = row
            .Where(c => c.ColumnIndex > columnIndex)
            .Select(c => ParseNumberWithUnit(c.Content, field))
            .Where(v => v.HasValue)
            .ToList();

        if (candidates.Any())
            return candidates.First();

        // 2. fallback: próximas linhas (até 2)
        for (int i = 1; i <= 2; i++)
        {
            if (!rows.ContainsKey(rowIndex + i)) continue;

            var nextRow = rows[rowIndex + i];

            var val = nextRow
                .Select(c => ParseNumberWithUnit(c.Content, field))
                .FirstOrDefault(v => v.HasValue);

            if (val.HasValue)
                return val;
        }

        return null;
    }

    // ── Column selection ─────────────────────────────────────────────────────

    /// <summary>
    /// FIX 1: Scans ALL cells (any row) for a header containing "100g" or "100 g".
    /// Returns the leftmost matching column index, or null if not found.
    /// </summary>
    private static int? FindTargetColumn(DocumentTable table)
    {
        int? best = null;

        foreach (var cell in table.Cells)
        {
            if (cell.ColumnIndex == 0) continue;   // label column, skip

            string header = NormalizeLabel(cell.Content);
            if (!header.Contains("100g") && !header.Contains("100 g")) continue;

            // Prefer the leftmost matching column
            if (best is null || cell.ColumnIndex < best.Value)
                best = cell.ColumnIndex;
        }

        return best;
    }

    /// <summary>
    /// FIX 5: Finds the leftmost column (after col 0) that contains a parseable number
    /// on the given row, using a pre-built cell map to avoid repeated iteration.
    /// </summary>
    private static int FindFirstNumericColumn(
        Dictionary<(int Row, int Col), string> cellMap,
        int totalColumns,
        int rowIndex)
    {
        for (int col = 1; col < totalColumns; col++)
        {
            if (cellMap.TryGetValue((rowIndex, col), out string? content) &&
                ParseNumber(content).HasValue)
                return col;
        }
        return -1;
    }

    /// <summary>
    /// Searches columns to the right of <paramref name="labelCol"/> in the same row
    /// for the first plausible numeric value.
    /// </summary>
    private static double? FindValueInSameRow(
        Dictionary<(int Row, int Col), string> cellMap,
        int totalColumns,
        int rowIndex,
        int labelCol,
        string fieldKey)
    {
        for (int col = labelCol + 1; col < totalColumns; col++)
        {
            if (!cellMap.TryGetValue((rowIndex, col), out string? content)) continue;
            if (ParseNumber(content) is { } v && IsPlausible(fieldKey, v))
                return v;
        }
        return null;
    }

    /// <summary>
    /// Searches up to <paramref name="lookahead"/> rows below the label cell for a
    /// plausible numeric value, scanning the label column and its immediate neighbours.
    /// Skips cells that are themselves nutrition labels to avoid mis-attribution.
    /// </summary>
    private static double? FindValueInNextRows(
        Dictionary<(int Row, int Col), string> cellMap,
        int totalColumns,
        int totalRows,
        int labelRow,
        int labelCol,
        string fieldKey,
        int lookahead = 2)
    {
        int colStart = Math.Max(0, labelCol - 1);
        int colEnd   = Math.Min(totalColumns - 1, labelCol + 2);

        for (int row = labelRow + 1; row <= Math.Min(labelRow + lookahead, totalRows - 1); row++)
        {
            for (int col = colStart; col <= colEnd; col++)
            {
                if (!cellMap.TryGetValue((row, col), out string? content)) continue;

                // Skip cells that are themselves nutrition labels
                if (ResolveFieldKey(NormalizeLabel(content)) is not null) continue;

                if (ParseNumber(content) is { } v && IsPlausible(fieldKey, v))
                    return v;
            }
        }
        return null;
    }

    // ── Label matching ───────────────────────────────────────────────────────

    /// <summary>
    /// FIX 2: Resolves the field key using a "contains" check on the normalised label.
    /// The map is ordered most-specific-first to prevent e.g. "gorduras" matching
    /// before "gorduras saturadas".
    /// </summary>
    //private static string? ResolveFieldKey(string normalizedLabel)
    //{
    //    foreach (var (key, field) in LabelMap)
    //    {
    //        if (normalizedLabel.Contains(key))
    //            return field;
    //    }
    //    return null;
    //}

    // ── Parsing helpers ──────────────────────────────────────────────────────

    private static string NormalizeLabel(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        // Strip accents (NFD → remove NonSpacingMark → NFC)
        string decomposed = raw.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        string result = sb.ToString()
                          .Normalize(NormalizationForm.FormC)
                          .ToLowerInvariant()
                          .Trim();

        // Strip OCR noise tokens shared with the proximity parser
        foreach (var token in OcrNoiseTokens)
            result = result.Replace(token, " ", StringComparison.OrdinalIgnoreCase);

        // Normalise comma decimal separators and collapse whitespace
        result = Regex.Replace(result, @"(\d),(\d)", "$1.$2");
        result = Regex.Replace(result, @"\s{2,}", " ").Trim();

        return result;
    }

    /// <summary>
    /// FIX 3: Robust number parsing — replaces comma, then strips everything
    /// except digits and dot, so "2,7 g", "263 mg", "< 0,5 g" all parse correctly.
    /// </summary>
    private static double? ParseNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        string cleaned = text.Replace(',', '.');
        cleaned = Regex.Replace(cleaned, @"[^0-9.]", string.Empty);

        // Discard strings with multiple dots (artefacts like "1.2.3")
        if (cleaned.Count(c => c == '.') > 1) return null;

        if (string.IsNullOrEmpty(cleaned)) return null;

        if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
            return value;

        return null;
    }

    private static bool IsPlausible(string fieldKey, double value)
    {
        return fieldKey switch
        {
            "calories"     => value >= 10  && value <= 900,
            "fat"          => value >= 0   && value <= 100,
            "saturatedFat" => value >= 0   && value <= 100,
            "protein"      => value >= 0   && value <= 100,
            "carbs"        => value >= 0   && value <= 100,
            "sugar"        => value >= 0   && value <= 100,
            "addedSugar"   => value >= 0   && value <= 100,
            "sodium"       => value >= 0   && value <= 5000,
            "fiber"        => value >= 0   && value <= 100,
            _              => true
        };
    }

    // ── Mapping ──────────────────────────────────────────────────────────────

    private static void MapRawToResult(Dictionary<string, double> raw, DocumentIntelligenceNutritionResult r)
    {
        NutritionField? Field(string key) =>
            raw.TryGetValue(key, out double v) ? new NutritionField { Value = v } : null;

        r.Calories     = Field("calories");
        r.Carbs        = Field("carbs");
        r.Sugar        = Field("sugar");
        r.AddedSugar   = Field("addedSugar");
        r.Protein      = Field("protein");
        r.Fat          = Field("fat");
        r.SaturatedFat = Field("saturatedFat");
        r.Sodium       = Field("sodium");
        r.Fiber        = Field("fiber");
    }

    // ── Validation ───────────────────────────────────────────────────────────

    private void ValidateCalorieConsistency(DocumentIntelligenceNutritionResult r)
    {
        if (r.Calories?.Value is null) return;

        double? carbs   = r.Carbs?.Value;
        double? protein = r.Protein?.Value;
        double? fat     = r.Fat?.Value;

        if (carbs is null || protein is null || fat is null) return;

        double expected = carbs.Value * 4 + protein.Value * 4 + fat.Value * 9;
        double actual   = r.Calories.Value!.Value;
        double diff     = Math.Abs(actual - expected);

        if (expected > 0 && diff / expected > 0.20)
        {
            string warning =
                $"Inconsistência calórica: declarado={actual:0} kcal, " +
                $"calculado={expected:0} kcal (diferença {diff / expected:P0})";

            _logger.LogWarning("[DI_PARSER] {Warning}", warning);
            r.Warnings.Add(warning);
        }
    }
}
