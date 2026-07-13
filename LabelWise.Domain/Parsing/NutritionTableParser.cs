namespace LabelWise.Domain.Parsing;

/// <summary>
/// Parser robusto de tabela nutricional a partir de texto OCR desestruturado.
/// 
/// ESTRATÉGIAS PROGRESSIVAS:
/// 1. Estruturada: Detecta colunas e extrai valores estruturados
/// 2. Contextual: Busca valores em linhas adjacentes a palavras-chave
/// 3. Heurística: Usa regra "maior valor = 100g"
/// 4. LLM Fallback: Usa Azure OpenAI para reconstruir tabela (opcional)
/// 
/// CARACTERÍSTICAS:
/// - Tolerante a erros de OCR
/// - Validação automática
/// - Score de confiança
/// - Múltiplos fallbacks
/// </summary>
public sealed class NutritionTableParser
{
    private readonly bool _enableLlmFallback;

    public NutritionTableParser(bool enableLlmFallback = false)
    {
        _enableLlmFallback = enableLlmFallback;
    }

    /// <summary>
    /// Parseia texto OCR e extrai dados nutricionais
    /// </summary>
    public NutritionData Parse(string ocrText)
    {
        Console.WriteLine($"[NutritionTableParser] Iniciando parse de {ocrText?.Length ?? 0} caracteres");

        if (string.IsNullOrWhiteSpace(ocrText))
        {
            Console.WriteLine("[NutritionTableParser] ❌ Texto vazio");
            return CreateEmptyResult("Texto vazio");
        }

        // Normalizar texto
        var normalizedText = NutritionNormalizer.NormalizeOcrText(ocrText);
        normalizedText = NutritionNormalizer.FixLineBreaksInValues(normalizedText);

        Console.WriteLine("[NutritionTableParser] Texto normalizado");

        // Detectar unidade
        var unit = NutritionNormalizer.DetectUnit(normalizedText);
        Console.WriteLine($"[NutritionTableParser] Unidade detectada: {unit}");

        // ESTRATÉGIA 1: Parsing estruturado (detecta colunas)
        Console.WriteLine("[NutritionTableParser] Tentando ESTRATÉGIA 1: Parsing estruturado...");
        var result = TryStructuredParsing(normalizedText, unit);
        if (result.HasMinimumData)
        {
            Console.WriteLine($"[NutritionTableParser] ✅ ESTRATÉGIA 1 sucedeu: {result.GetSummary()}");
            return FinalizeResult(result);
        }
        Console.WriteLine("[NutritionTableParser] ❌ ESTRATÉGIA 1 falhou");

        // ESTRATÉGIA 2: Parsing contextual (palavras-chave + linhas adjacentes)
        Console.WriteLine("[NutritionTableParser] Tentando ESTRATÉGIA 2: Parsing contextual...");
        result = TryContextualParsing(normalizedText, unit);
        if (result.HasMinimumData)
        {
            Console.WriteLine($"[NutritionTableParser] ✅ ESTRATÉGIA 2 sucedeu: {result.GetSummary()}");
            return FinalizeResult(result);
        }
        Console.WriteLine("[NutritionTableParser] ❌ ESTRATÉGIA 2 falhou");

        // ESTRATÉGIA 3: Parsing heurístico (maior valor = 100g)
        Console.WriteLine("[NutritionTableParser] Tentando ESTRATÉGIA 3: Parsing heurístico...");
        result = TryHeuristicParsing(normalizedText, unit);
        if (result.HasMinimumData)
        {
            Console.WriteLine($"[NutritionTableParser] ✅ ESTRATÉGIA 3 sucedeu: {result.GetSummary()}");
            return FinalizeResult(result);
        }
        Console.WriteLine("[NutritionTableParser] ❌ ESTRATÉGIA 3 falhou");

        // ESTRATÉGIA 4: LLM Fallback (opcional)
        if (_enableLlmFallback)
        {
            Console.WriteLine("[NutritionTableParser] Tentando ESTRATÉGIA 4: LLM Fallback...");
            result = TryLlmFallback(normalizedText, unit);
            if (result.HasMinimumData)
            {
                Console.WriteLine($"[NutritionTableParser] ✅ ESTRATÉGIA 4 sucedeu: {result.GetSummary()}");
                return FinalizeResult(result);
            }
            Console.WriteLine("[NutritionTableParser] ❌ ESTRATÉGIA 4 falhou");
        }

        Console.WriteLine("[NutritionTableParser] ❌ TODAS as estratégias falharam");
        return CreateEmptyResult("Nenhuma estratégia conseguiu extrair dados mínimos");
    }

    // ═══════════════════════════════════════════════════════════════════
    // ESTRATÉGIA 1: Parsing Estruturado
    // Detecta colunas (100g, porção, %VD) e extrai valores estruturados
    // ═══════════════════════════════════════════════════════════════════

    // Mapeamento estrito: nutriente → palavras-chave inclusivas + exclusivas.
    // A chave é o identificador de debug; IncludeKeywords são normalizados sem acento.
    private static readonly NutrientDefinition[] NutrientMap =
    [
        new("calories",      Include: ["valor energetico", "energia", "calorias"],
                             Exclude: []),
        new("carbs",         Include: ["carboidrato"],
                             Exclude: ["fibra"]),
        new("sugar",         Include: ["acucares totais", "acucar total", "acucares"],
                             Exclude: ["adicionado"]),
        new("addedSugar",    Include: ["acucares adicionados", "acucar adicionado"],
                             Exclude: []),
        new("protein",       Include: ["proteina", "proteinas"],
                             Exclude: []),
        new("fat",           Include: ["gorduras totais", "gordura total"],
                             Exclude: ["saturada", "trans", "insaturada", "monoinsaturada", "poliinsaturada"]),
        new("saturatedFat",  Include: ["gorduras saturadas", "gordura saturada"],
                             Exclude: ["trans"]),
        new("fiber",         Include: ["fibra alimentar", "fibra dietetica", "fibras", "fibra"],
                             Exclude: []),
        new("sodium",        Include: ["sodio"],
                             Exclude: []),
    ];

    private sealed record NutrientDefinition(
        string Key,
        string[] Include,
        string[] Exclude);

    private NutritionData TryStructuredParsing(string text, string unit)
    {
        var data = new NutritionData
        {
            Unit = unit,
            ParsingStrategy = "Structured"
        };

        var lines = text.Split('\n');

        int headerIndex = FindTableHeader(lines);
        if (headerIndex == -1)
        {
            data.Warnings.Add("Header da tabela não encontrado");
            return data;
        }

        foreach (var def in NutrientMap)
        {
            var match = ExtractNutrientStrict(lines, headerIndex, def.Include, def.Exclude, def.Key);
            if (match != null)
            {
                data.RowMatches[def.Key] = match;
                AssignExtractedValue(data, def.Key, match.ExtractedValue);
            }
        }

        // Garantir sódio mesmo que a estratégia estruturada tenha falhado nesse campo
        if (!data.SodiumPer100g.HasValue)
            ForceSodiumExtraction(data, lines);

        ValidateFatRelationship(data);

        data.ExtractedNutrientsCount = CountExtractedNutrients(data);
        return data;
    }

    private int FindTableHeader(string[] lines)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].ToLowerInvariant();
            if (line.Contains("informacao nutricional") || 
                line.Contains("informação nutricional") ||
                line.Contains("nutricional"))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Encontra a linha que contém os keywords inclusivos (sem nenhum dos exclusivos)
    /// e seleciona o valor correspondente à coluna de 100g.
    ///
    /// Heurística de seleção de coluna:
    ///   Rótulos brasileiros tipicamente têm: [valor 100g] [valor porção] [%VD]
    ///   OU: [valor porção] [valor 100g] [%VD]
    ///   O valor 100g é sempre o MAIOR dos dois primeiros valores numéricos em g,
    ///   pois qualquer porção típica &lt; 100g.
    ///   Exceção: sódio (mg) — pode ser grande; usa a mesma lógica pois mg/100g > mg/porção.
    ///   Exceção: calorias — o maior entre valores ≥ 50 e ≤ 900 kcal.
    /// </summary>
    private NutrientRowMatch? ExtractNutrientStrict(
        string[] lines,
        int startIndex,
        string[] includeKeywords,
        string[] excludeKeywords,
        string nutrientKey)
    {
        for (int i = startIndex; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var normalized = NutritionNormalizer.RemoveDiacritics(rawLine.ToLowerInvariant());

            // Verificar se algum keyword inclusivo está presente
            bool included = includeKeywords.Any(kw => normalized.Contains(kw));
            if (!included) continue;

            // Verificar que nenhum keyword exclusivo está presente — evita colisões
            // ex: "gordura total" não deve corresponder a "gordura saturada"
            bool excluded = excludeKeywords.Any(kw => normalized.Contains(kw));
            if (excluded) continue;

            // Extrair números da linha (e da próxima se necessário)
            var numbers = NutritionNormalizer.ExtractNumbers(rawLine);

            if (numbers.Count == 0 && i + 1 < lines.Length)
                numbers = NutritionNormalizer.ExtractNumbers(lines[i + 1]);

            if (numbers.Count == 0)
                continue;

            double selected = SelectColumnValue(numbers, nutrientKey);
            return new NutrientRowMatch(rawLine.Trim(), selected, numbers.AsReadOnly());
        }

        return null;
    }

    /// <summary>
    /// Seleciona o valor correto da coluna 100g a partir de múltiplos candidatos numa linha.
    ///
    /// Regras por tipo de nutriente:
    ///   calorias   → maior valor entre 50–900 (porção &lt; 100 kcal, 100g tipicamente ≥ 100)
    ///   sódio      → maior valor entre 0–5000 (mg/100g sempre ≥ mg/porção)
    ///   demais (g) → maior valor entre 0–100  (g/100g sempre ≥ g/porção &lt; 100g)
    ///   fallback   → numbers[0]
    /// </summary>
    private static double SelectColumnValue(IReadOnlyList<double> numbers, string nutrientKey)
    {
        if (numbers.Count == 1)
            return numbers[0];

        return nutrientKey switch
        {
            "calories" =>
                numbers.Where(n => n is >= 50 and <= 900)
                       .DefaultIfEmpty(numbers[0])
                       .Max(),

            "sodium" =>
                numbers.Where(n => n is >= 0 and <= 5000)
                       .DefaultIfEmpty(numbers[0])
                       .Max(),

            _ => // fat, carbs, protein, sugar, fiber, saturatedFat (all in grams, 0–100)
                numbers.Where(n => n is >= 0 and <= 100)
                       .DefaultIfEmpty(numbers[0])
                       .Max()
        };
    }

    /// <summary>
    /// Atribui o valor extraído ao campo correspondente em <see cref="NutritionData"/>.
    /// </summary>
    private static void AssignExtractedValue(NutritionData data, string key, double value)
    {
        switch (key)
        {
            case "calories":     data.CaloriesPer100g      = value; break;
            case "carbs":        data.CarbsPer100g         = value; break;
            case "sugar":        data.SugarPer100g         = value; break;
            case "addedSugar":   data.AddedSugarPer100g    = value; break;
            case "protein":      data.ProteinPer100g       = value; break;
            case "fat":          data.FatPer100g           = value; break;
            case "saturatedFat": data.SaturatedFatPer100g  = value; break;
            case "fiber":        data.FiberPer100g         = value; break;
            case "sodium":       data.SodiumPer100g        = value; break;
        }
    }

    /// <summary>
    /// Extração garantida de sódio: varre TODAS as linhas procurando "sodio" e força a
    /// extração caso a estratégia estruturada não tenha encontrado nada.
    /// Sódio está em mg e raramente fica na mesma linha de outros nutrientes no OCR.
    /// </summary>
    private static void ForceSodiumExtraction(NutritionData data, string[] lines)
    {
        foreach (var rawLine in lines)
        {
            var norm = NutritionNormalizer.RemoveDiacritics(rawLine.ToLowerInvariant());
            if (!norm.Contains("sodio")) continue;

            var numbers = NutritionNormalizer.ExtractNumbers(rawLine)
                .Where(n => n is >= 0 and <= 5000)
                .ToList();

            if (numbers.Count > 0)
            {
                var selected = numbers.Max(); // mg/100g > mg/porção
                data.SodiumPer100g = selected;
                data.RowMatches["sodium_forced"] =
                    new NutrientRowMatch(rawLine.Trim(), selected, numbers.AsReadOnly());

                Console.WriteLine($"[NutritionTableParser] 🔧 Sódio forçado: {selected}mg — linha: {rawLine.Trim()}");
                break;
            }
        }
    }

    /// <summary>
    /// Valida a relação gordura total ≥ gordura saturada.
    /// Se a condição for violada (saturada > total), os valores foram lidos de linhas trocadas
    /// — faz o swap e registra warning.
    /// </summary>
    private static void ValidateFatRelationship(NutritionData data)
    {
        if (!data.FatPer100g.HasValue || !data.SaturatedFatPer100g.HasValue)
            return;

        if (data.SaturatedFatPer100g.Value <= data.FatPer100g.Value)
            return;

        // Gordura saturada não pode ser maior que gordura total — swap
        Console.WriteLine(
            $"[NutritionTableParser] ⚠️ Gordura saturada ({data.SaturatedFatPer100g:F1}g) > total ({data.FatPer100g:F1}g) — " +
            "valores trocados. Possível erro de linha no OCR.");

        (data.FatPer100g, data.SaturatedFatPer100g) = (data.SaturatedFatPer100g, data.FatPer100g);
        data.Warnings.Add(
            $"Gordura total e saturada invertidas pelo validador (saturada era maior). " +
            $"Verifique a imagem da tabela.");
    }

    private int CountExtractedNutrients(NutritionData data)
    {
        int count = 0;
        if (data.CaloriesPer100g.HasValue) count++;
        if (data.CarbsPer100g.HasValue) count++;
        if (data.SugarPer100g.HasValue) count++;
        if (data.AddedSugarPer100g.HasValue) count++;
        if (data.ProteinPer100g.HasValue) count++;
        if (data.FatPer100g.HasValue) count++;
        if (data.SaturatedFatPer100g.HasValue) count++;
        if (data.FiberPer100g.HasValue) count++;
        if (data.SodiumPer100g.HasValue) count++;
        return count;
    }

    private NutritionData FinalizeResult(NutritionData data)
    {
        NutritionValidator.ValidateAndCorrect(data);
        data.ConfidenceScore = NutritionValidator.CalculateConfidenceScore(data);

        // Upgrade de confiança: parsing estruturado + ≥5 nutrientes + sem warnings = alta
        if (data.ParsingStrategy == "Structured"
            && data.ExtractedNutrientsCount >= 5
            && data.Warnings.Count == 0)
        {
            data.ConfidenceScore = Math.Max(data.ConfidenceScore, 90);
            Console.WriteLine("[NutritionTableParser] ✅ Confiança promovida para ALTA (Structured + 5+ campos + sem warnings)");
        }

        Console.WriteLine($"[NutritionTableParser] Score de confiança: {data.ConfidenceScore}/100");
        Console.WriteLine($"[NutritionTableParser] Nutrientes extraídos: {data.ExtractedNutrientsCount}");
        Console.WriteLine($"[NutritionTableParser] Warnings: {data.Warnings.Count}");

        // Log de debug de mapeamentos
        foreach (var (key, match) in data.RowMatches)
        {
            Console.WriteLine(
                $"[NutritionTableParser] 🔍 {key}: valor={match.ExtractedValue}, " +
                $"candidatos=[{string.Join(", ", match.CandidateValues)}], " +
                $"linha=\"{match.MatchedRowText}\"");
        }

        return data;
    }

    private NutritionData CreateEmptyResult(string reason)
    {
        return new NutritionData
        {
            ConfidenceScore = 0,
            ExtractedNutrientsCount = 0,
            Warnings = new List<string> { reason }
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // ESTRATÉGIA 2: Parsing Contextual
    // Busca valores próximos a palavras-chave específicas
    // ═══════════════════════════════════════════════════════════════════

    private NutritionData TryContextualParsing(string text, string unit)
    {
        var data = new NutritionData
        {
            Unit = unit,
            ParsingStrategy = "Contextual"
        };

        data.CaloriesPer100g = ExtractUsingContext(text, "calorias", "valor energetico", "energia");
        data.CarbsPer100g = ExtractUsingContext(text, "carboidrato");
        data.SugarPer100g = ExtractUsingContext(text, "acucares totais", "acucar total");
        data.AddedSugarPer100g = ExtractUsingContext(text, "acucares adicionados", "acucar adicionado");
        data.ProteinPer100g = ExtractUsingContext(text, "proteina");
        data.FatPer100g = ExtractUsingContext(text, "gorduras totais", "gordura total");
        data.SaturatedFatPer100g = ExtractUsingContext(text, "gorduras saturadas", "gordura saturada");
        data.FiberPer100g = ExtractUsingContext(text, "fibra");
        data.SodiumPer100g = ExtractUsingContext(text, "sodio");

        var lines = text.Split('\n');
        if (!data.SodiumPer100g.HasValue)
            ForceSodiumExtraction(data, lines);

        ValidateFatRelationship(data);

        data.ExtractedNutrientsCount = CountExtractedNutrients(data);
        return data;
    }

    private double? ExtractUsingContext(string text, params string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            var numbers = NutritionNormalizer.ExtractNumbersNearKeyword(text, keyword, maxLinesToSearch: 3);
            if (numbers.Count > 0)
            {
                return NutritionNormalizer.SelectMostLikelyValue100g(numbers, keyword);
            }
        }
        return null;
    }

    // ═══════════════════════════════════════════════════════════════════
    // ESTRATÉGIA 3: Parsing Heurístico
    // ═══════════════════════════════════════════════════════════════════

    private NutritionData TryHeuristicParsing(string text, string unit)
    {
        var data = new NutritionData { Unit = unit, ParsingStrategy = "Heuristic" };
        var lines = text.Split('\n');

        foreach (var line in lines)
        {
            var norm = NutritionNormalizer.RemoveDiacritics(line.ToLowerInvariant());

            if (norm.Contains("energia") || norm.Contains("caloria"))
            {
                var nums = NutritionNormalizer.ExtractNumbers(line).Where(n => n is >= 50 and <= 900).ToList();
                data.CaloriesPer100g ??= nums.Count > 0 ? nums.Max() : null;
            }
            if (norm.Contains("carboidrato"))
            {
                var nums = NutritionNormalizer.ExtractNumbers(line).Where(n => n is >= 0 and <= 100).ToList();
                data.CarbsPer100g ??= nums.Count > 0 ? nums.Max() : null;
            }
            if (norm.Contains("proteina"))
            {
                var nums = NutritionNormalizer.ExtractNumbers(line).Where(n => n is >= 0 and <= 100).ToList();
                data.ProteinPer100g ??= nums.Count > 0 ? nums.Max() : null;
            }
            if (norm.Contains("gordura") && !norm.Contains("saturada") && !norm.Contains("trans"))
            {
                var nums = NutritionNormalizer.ExtractNumbers(line).Where(n => n is >= 0 and <= 100).ToList();
                data.FatPer100g ??= nums.Count > 0 ? nums.Max() : null;
            }
            if (norm.Contains("gordura") && norm.Contains("saturada"))
            {
                var nums = NutritionNormalizer.ExtractNumbers(line).Where(n => n is >= 0 and <= 100).ToList();
                data.SaturatedFatPer100g ??= nums.Count > 0 ? nums.Max() : null;
            }
            if (norm.Contains("sodio"))
            {
                var nums = NutritionNormalizer.ExtractNumbers(line).Where(n => n is >= 0 and <= 5000).ToList();
                data.SodiumPer100g ??= nums.Count > 0 ? nums.Max() : null;
            }
        }

        if (!data.SodiumPer100g.HasValue)
            ForceSodiumExtraction(data, lines);

        ValidateFatRelationship(data);

        data.ExtractedNutrientsCount = CountExtractedNutrients(data);
        return data;
    }

    // ═══════════════════════════════════════════════════════════════════
    // ESTRATÉGIA 4: LLM Fallback (OPCIONAL)
    // ═══════════════════════════════════════════════════════════════════

    private NutritionData TryLlmFallback(string text, string unit) =>
        new()
        {
            Unit = unit,
            ParsingStrategy = "LLM_Fallback",
            Warnings = new List<string> { "LLM fallback não implementado" }
        };
}
