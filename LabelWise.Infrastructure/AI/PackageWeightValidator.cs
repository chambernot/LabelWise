using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Infrastructure.AI.DTOs;
using System.Text.RegularExpressions;

namespace LabelWise.Infrastructure.AI;

/// <summary>
/// Valida o campo <c>packageWeight</c> extraído pela IA, bloqueando valores que
/// na verdade vieram da tabela nutricional (coluna 100g, porção, nutrientes ou %VD).
///
/// Regras aplicadas:
/// 1. Valor "100 g" / "100 ml" → sempre inválido (coluna de referência da tabela).
/// 2. Valor coincide com nutriente da tabela → inválido.
/// 3. Valor coincide com o peso de porção sem evidência de peso total → inválido.
/// 4. Valor não possui evidência textual fora da tabela → inválido.
/// 5. Em caso de invalidação: zera <c>packageWeight</c>, zera
///    <c>estimatedPackageCalories</c> e reduz <c>parserConfidence</c> para "medium".
/// </summary>
internal sealed class PackageWeightValidator
{
    // Palavras-chave que indicam peso total da embalagem fora da tabela nutricional
    private static readonly string[] _validWeightKeywords =
    [
        "peso líquido", "peso liquido",
        "conteúdo líquido", "conteudo liquido",
        "conteúdo", "conteudo",
        "líquido", "liquido",
        "net weight", "net wt",
        "peso:", "peso "
    ];

    // Regex para extrair valor numérico + unidade de uma string de peso
    private static readonly Regex _weightValueRegex = new(
        @"(\d+(?:[.,]\d+)?)\s*(g|kg|ml|l)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Valida e, se necessário, corrige o <c>packageWeight</c> na resposta do modelo.
    /// Modifica <paramref name="modelResponse"/> e <paramref name="profile"/> in-place.
    /// </summary>
    /// <param name="modelResponse">Resposta bruta do modelo de visão.</param>
    /// <param name="profile">Perfil nutricional já mapeado para DTO.</param>
    /// <param name="rawOcrText">Texto OCR bruto da imagem (pode ser null).</param>
    /// <param name="warnings">Lista de warnings onde mensagens de invalidação serão adicionadas.</param>
    public void Validate(
        NutritionVisionModelResponse modelResponse,
        EstimatedNutritionProfileDto profile,
        string? rawOcrText,
        List<string> warnings)
    {
        var result = ValidateCore(
            modelResponse.PackageWeight,
            profile,
            portionWeightG: modelResponse.EstimatedNutritionProfile?.PortionWeightG,
            rawOcrText,
            warnings);

        if (result == ValidationOutcome.Invalid)
        {
            modelResponse.PackageWeight = null;
        }
    }

    /// <summary>
    /// Valida o <c>packageWeight</c> operando diretamente sobre strings e o DTO de perfil,
    /// sem depender de <see cref="NutritionVisionModelResponse"/>.
    /// Retorna o packageWeight validado (null se invalidado).
    /// Ideal para uso no pipeline de análise onde o DTO interno da IA não está disponível.
    /// </summary>
    /// <param name="packageWeight">Valor bruto do campo packageWeight.</param>
    /// <param name="profile">Perfil nutricional já normalizado.</param>
    /// <param name="portionWeightG">Peso de porção em gramas, se disponível.</param>
    /// <param name="rawOcrText">Texto OCR bruto (pode ser null).</param>
    /// <param name="warnings">Lista de warnings onde mensagens de invalidação serão adicionadas.</param>
    /// <returns>Valor validado (mesmo string original se válido, null se invalidado).</returns>
    public string? ValidateAndReturn(
        string? packageWeight,
        EstimatedNutritionProfileDto? profile,
        double? portionWeightG,
        string? rawOcrText,
        List<string> warnings)
    {
        var result = ValidateCore(packageWeight, profile, portionWeightG, rawOcrText, warnings);

        if (result == ValidationOutcome.Invalid)
        {
            if (profile != null)
            {
                profile.EstimatedPackageCalories = null;
                if (profile.ParserConfidence == "high")
                    profile.ParserConfidence = "medium";
            }
            return null;
        }

        return packageWeight;
    }

    private ValidationOutcome ValidateCore(
        string? raw,
        EstimatedNutritionProfileDto? profile,
        double? portionWeightG,
        string? rawOcrText,
        List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ValidationOutcome.Valid;

        var grams = ParseGrams(raw);

        // ── Regra 1: valor "100 g" ou "100 ml" é sempre a coluna da tabela ──────────
        if (IsTableReferenceValue(grams))
        {
            warnings.Add($"packageWeight '{raw}' invalido: valor 100g/100ml é a coluna de referência da tabela nutricional.");
            return ValidationOutcome.Invalid;
        }

        // ── Regra 2: valor coincide com nutriente da tabela ───────────────────────
        var matchedNutrient = FindMatchingNutrientFromProfile(grams, profile);
        if (matchedNutrient != null)
        {
            warnings.Add($"packageWeight '{raw}' invalido: valor {grams:G}g coincide com {matchedNutrient} da tabela nutricional.");
            return ValidationOutcome.Invalid;
        }

        // ── Regra 3: valor coincide com o peso de porção declarado ────────────────
        if (portionWeightG.HasValue && grams.HasValue && Math.Abs(grams.Value - portionWeightG.Value) < 0.5)
        {
            if (!HasValidWeightEvidence(raw, rawOcrText))
            {
                warnings.Add($"packageWeight '{raw}' invalido: coincide com o peso de porção ({portionWeightG}g) " +
                    "e não há evidência de peso total da embalagem no OCR.");
                return ValidationOutcome.Invalid;
            }
        }

        // ── Regra 4: sem evidência textual confiável fora da tabela ───────────────
        // Só aplicar quando OCR está disponível. Sem OCR (ex: Vision direto), as regras
        // estruturais (1, 2, 3) são suficientes; não é possível confirmar nem negar a origem.
        if (!string.IsNullOrWhiteSpace(rawOcrText) && !HasValidWeightEvidence(raw, rawOcrText))
        {
            warnings.Add($"packageWeight '{raw}' invalido: nenhuma evidência de peso total da embalagem " +
                "encontrada no texto OCR (ex: 'Peso líquido', 'Conteúdo').");
            return ValidationOutcome.Invalid;
        }

        return ValidationOutcome.Valid;
    }

    private enum ValidationOutcome { Valid, Invalid }

    // ─────────────────────────────────────────────────────────────────
    // Helpers privados
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Zera packageWeight, zera estimatedPackageCalories e reduz parserConfidence.
    /// Usado apenas no overload com NutritionVisionModelResponse.
    /// </summary>
    private static void Invalidate(
        NutritionVisionModelResponse modelResponse,
        EstimatedNutritionProfileDto profile,
        List<string> warnings,
        string reason)
    {
        modelResponse.PackageWeight = null;
        profile.EstimatedPackageCalories = null;

        if (profile.ParserConfidence == "high")
            profile.ParserConfidence = "medium";

        warnings.Add(reason);
    }

    /// <summary>
    /// Retorna true se o valor em gramas corresponde à coluna de referência da tabela (100g/100ml).
    /// </summary>
    private static bool IsTableReferenceValue(double? grams) =>
        grams.HasValue && Math.Abs(grams.Value - 100.0) < 0.5;

    /// <summary>
    /// Procura o nome do nutriente cujo valor coincide com <paramref name="grams"/>,
    /// operando sobre <see cref="NutritionProfileResponse"/> (DTO interno da IA).
    /// </summary>
    private static string? FindMatchingNutrient(double? grams, NutritionProfileResponse? nutrition)
    {
        if (grams is null || nutrition is null) return null;
        return FindMatchingNutrientCore(
            grams.Value,
            nutrition.EstimatedCarbsPer100g,
            nutrition.EstimatedSugarPer100g,
            nutrition.EstimatedAddedSugarPer100g,
            nutrition.EstimatedFatPer100g,
            nutrition.EstimatedSaturatedFatPer100g,
            nutrition.EstimatedProteinPer100g,
            nutrition.EstimatedFiberPer100g,
            nutrition.EstimatedSodiumPer100g,
            nutrition.CaloriesPer100g);
    }

    /// <summary>
    /// Procura o nome do nutriente cujo valor coincide com <paramref name="grams"/>,
    /// operando sobre <see cref="EstimatedNutritionProfileDto"/> (DTO da aplicação).
    /// </summary>
    private static string? FindMatchingNutrientFromProfile(double? grams, EstimatedNutritionProfileDto? profile)
    {
        if (grams is null || profile is null) return null;
        return FindMatchingNutrientCore(
            grams.Value,
            profile.EstimatedCarbsPer100g,
            profile.EstimatedSugarPer100g,
            profile.EstimatedAddedSugarPer100g,
            profile.EstimatedFatPer100g,
            profile.EstimatedSaturatedFatPer100g,
            profile.EstimatedProteinPer100g,
            profile.EstimatedFiberPer100g,
            profile.EstimatedSodiumPer100g,
            profile.CaloriesPer100g);
    }

    private static string? FindMatchingNutrientCore(
        double grams,
        double? carbs, double? sugar, double? addedSugar,
        double? fat, double? satFat, double? protein,
        double? fiber, double? sodiumMg, double? calories)
    {
        var nutrients = new (string Nome, double? Valor)[]
        {
            ("carboidratos",        carbs),
            ("açúcar total",        sugar),
            ("açúcar adicionado",   addedSugar),
            ("gordura total",       fat),
            ("gordura saturada",    satFat),
            ("proteína",            protein),
            ("fibra",               fiber),
            // Sódio está em mg → converter para g para comparar
            ("sódio",               sodiumMg.HasValue ? sodiumMg / 1000.0 : null),
        };

        foreach (var (nome, valor) in nutrients)
        {
            if (valor.HasValue && Math.Abs(valor.Value - grams) < 0.5)
                return nome;
        }

        // Calorias: comparar com tolerância de 1 kcal
        if (calories.HasValue && Math.Abs(calories.Value - grams) < 1.0)
            return "calorias por 100g";

        return null;
    }

    /// <summary>
    /// Verifica se o texto OCR contém evidência de peso total da embalagem fora da tabela nutricional.
    /// Aceita formatos como "Peso líquido 90 g", "Conteúdo: 120g", "Peso 500 ml".
    /// </summary>
    private static bool HasValidWeightEvidence(string packageWeightRaw, string? ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
            return false; // Sem OCR: não é possível confirmar a origem

        var ocr = ocrText;

        // Extrair o valor numérico do packageWeight declarado pela IA
        var match = _weightValueRegex.Match(packageWeightRaw);
        if (!match.Success)
            return false;

        var numericPart = match.Groups[1].Value.Replace(',', '.');
        var unit = match.Groups[2].Value;

        // Montar padrão de busca: alguma keyword de peso + o valor + unidade próximos no texto
        // Ex: "peso líquido 90 g" ou "conteúdo 120g"
        foreach (var keyword in _validWeightKeywords)
        {
            // Regex: keyword ... valor ... unidade, com até 30 chars entre eles
            var evidencePattern = new Regex(
                Regex.Escape(keyword) + @"[^0-9]{0,30}" +
                Regex.Escape(numericPart) + @"\s*" + Regex.Escape(unit),
                RegexOptions.IgnoreCase);

            if (evidencePattern.IsMatch(ocr))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Extrai o valor numérico em gramas de uma string de peso (ex: "90 g" → 90, "1 kg" → 1000).
    /// </summary>
    private static double? ParseGrams(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var match = _weightValueRegex.Match(raw.Trim());
        if (!match.Success)
            return null;

        var value = double.Parse(
            match.Groups[1].Value.Replace(',', '.'),
            System.Globalization.CultureInfo.InvariantCulture);

        return match.Groups[2].Value.ToLowerInvariant() switch
        {
            "kg" => value * 1000,
            "l"  => value * 1000,
            _    => value
        };
    }
}
