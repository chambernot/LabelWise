using LabelWise.Application.DTOs.AI;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services.NutritionPipeline;

/// <summary>
/// Validador de sanidade pós-parser que executa entre a sanitização (Stage 6) e o cálculo
/// de score (Stage 7). Implementa as 13 regras de validação para garantir que a resposta
/// final seja coerente, defensiva e nunca baseada em dados claramente errados.
/// </summary>
public sealed class NutritionParseSanityValidator : INutritionParseSanityValidator
{
    // Nomes dos 9 nutrientes principais para cálculo de completude (Regra 11)
    private static readonly string[] MainNutrients =
    [
        "energy", "carbs", "totalSugars", "addedSugars",
        "proteins", "totalFat", "saturatedFat", "fiber", "sodium"
    ];

    // Sinais textuais de tabela nutricional presentes no resumo da IA (Regra 1)
    private static readonly string[] NutritionTableSignals =
    [
        "informação nutricional", "informacao nutricional",
        "porção", "porcao",
        "valor energético", "valor energetico",
        "carboidratos",
        "açúcares", "acucares",
        "proteínas", "proteinas",
        "gorduras",
        "sódio", "sodio",
        "100 g", "100g",
        "100 ml", "100ml",
        "%vd", "% vd",
        "fibra alimentar",
        "gorduras saturadas",
        "açúcar adicionado", "acucar adicionado"
    ];

    private readonly ILogger<NutritionParseSanityValidator> _logger;

    public NutritionParseSanityValidator(ILogger<NutritionParseSanityValidator> logger)
    {
        _logger = logger;
    }

    public NutritionSanityResult Validate(NutritionAnalysisContext context, VisualInterpretationResult visionResult)
    {
        var warnings = new List<string>();
        var inconsistencies = new List<string>();

        // Trabalhar com uma cópia defensiva do perfil nutricional
        var nutrition = CloneNutrition(context.FinalNutritionProfile ?? new EstimatedNutritionProfileDto());

        var correctedAnalysisMode = context.AnalysisMode;
        var correctedPublicAnalysisMode = context.PublicAnalysisMode;

        // ── Regra 1: Validação do modo de análise ───────────────────────────────
        var tableSignalCount = CountTableSignals(visionResult);
        var isTableDetectedBySignals = tableSignalCount >= 3
            || visionResult.ProbableCaptureType == CaptureType.NutritionTable;

        if (isTableDetectedBySignals
            && context.PublicAnalysisMode == AnalysisMode.FrontOfPackageOnly)
        {
            correctedAnalysisMode = NutritionDecisionMode.FullNutritionLabel;
            correctedPublicAnalysisMode = AnalysisMode.FullNutritionLabel;
            inconsistencies.Add("Modo de análise corrigido: a imagem contém tabela nutricional visível.");
            _logger.LogInformation("[SanityValidator.Rule1] AnalysisMode promovido para FullNutritionLabel. Sinais={Count}", tableSignalCount);
        }

        // Usar o modo corrigido para as regras seguintes
        var effectiveIsFullTable = correctedPublicAnalysisMode == AnalysisMode.FullNutritionLabel
            || visionResult.ProbableCaptureType == CaptureType.NutritionTable;

        // ── Regra 2: Não descartar parser parcial ───────────────────────────────
        var extractedCount = CountExtractedNutrients(nutrition);
        if (extractedCount >= 3 && correctedPublicAnalysisMode == AnalysisMode.FrontOfPackageOnly)
        {
            correctedAnalysisMode = NutritionDecisionMode.FullNutritionLabel;
            correctedPublicAnalysisMode = AnalysisMode.FullNutritionLabel;
            effectiveIsFullTable = true;
            if (string.IsNullOrEmpty(nutrition.ParserConfidence) || nutrition.ParserConfidence == "low")
                nutrition.ParserConfidence = "medium";
            _logger.LogInformation("[SanityValidator.Rule2] {Count} nutrientes extraídos — fallback por categoria bloqueado.", extractedCount);
        }

        // ── Regra 3: Detecção de deslocamento de linha ──────────────────────────
        ApplyLineDisplacementCheck(nutrition, warnings, inconsistencies);

        // ── Regra 4: Campos obrigatórios quando tabela visível ──────────────────
        if (effectiveIsFullTable)
            ApplyMandatoryFieldsCheck(nutrition, warnings, inconsistencies);

        // ── Regra 5: Coerência nutricional ──────────────────────────────────────
        ApplyNutritionalCoherenceChecks(nutrition, context, warnings, inconsistencies);

        // ── Regra 6: Porção e embalagem ─────────────────────────────────────────
        ApplyPortionPackageCheck(nutrition, context, warnings, inconsistencies);

        // ── Regra 7: Calorias da porção / estimativa de embalagem ───────────────
        ApplyCaloriesConsistencyCheck(nutrition, warnings, inconsistencies);

        // ── Regra 9: Deduplica PrincipalOffender (apenas sinaliza) ──────────────
        // A consolidação final é feita pelo pipeline; aqui apenas registramos se necessário.

        // ── Regra 10: Recalcular ParserConfidence ───────────────────────────────
        var parserConfidence = RecalculateParserConfidence(nutrition, effectiveIsFullTable, inconsistencies.Count);
        nutrition.ParserConfidence = parserConfidence;

        // ── Regra 11: ShouldReprocess ───────────────────────────────────────────
        var shouldReprocess = EvaluateShouldReprocess(nutrition, effectiveIsFullTable, inconsistencies);

        // ── Regra 8: CanScoreReliably ────────────────────────────────────────────
        var canScoreReliably = EvaluateCanScoreReliably(nutrition, effectiveIsFullTable, parserConfidence);

        _logger.LogInformation(
            "[SanityValidator] Mode={Mode}, Confidence={Conf}, CanScore={CanScore}, ShouldReprocess={Reprocess}, " +
            "Warnings={Warnings}, Inconsistencies={Inconsistencies}",
            correctedPublicAnalysisMode, parserConfidence, canScoreReliably, shouldReprocess,
            warnings.Count, inconsistencies.Count);

        return new NutritionSanityResult
        {
            ValidatedNutrition = nutrition,
            CorrectedAnalysisMode = correctedAnalysisMode != context.AnalysisMode ? correctedAnalysisMode : null,
            CorrectedPublicAnalysisMode = correctedPublicAnalysisMode != context.PublicAnalysisMode ? correctedPublicAnalysisMode : null,
            ParserConfidence = parserConfidence,
            Warnings = warnings,
            Inconsistencies = inconsistencies,
            ShouldReprocess = shouldReprocess,
            CanScoreReliably = canScoreReliably
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // Regra 1 — Sinais de tabela nutricional
    // ═══════════════════════════════════════════════════════════════════

    private static int CountTableSignals(VisualInterpretationResult vision)
    {
        var text = BuildSearchableText(vision);
        if (string.IsNullOrWhiteSpace(text)) return 0;

        return NutritionTableSignals.Count(signal =>
            text.Contains(signal, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildSearchableText(VisualInterpretationResult vision)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(vision.InterpretationSummary)) parts.Add(vision.InterpretationSummary);
        if (vision.Warnings?.Count > 0) parts.AddRange(vision.Warnings);
        if (vision.VisibleClaims?.Count > 0) parts.AddRange(vision.VisibleClaims);
        if (!string.IsNullOrWhiteSpace(vision.ProbableCategory)) parts.Add(vision.ProbableCategory);
        return string.Join(" ", parts);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Regra 2 — Contar nutrientes extraídos
    // ═══════════════════════════════════════════════════════════════════

    private static int CountExtractedNutrients(EstimatedNutritionProfileDto n) =>
        (n.CaloriesPer100g.HasValue ? 1 : 0)
        + (n.EstimatedCarbsPer100g.HasValue ? 1 : 0)
        + (n.EstimatedSugarPer100g.HasValue ? 1 : 0)
        + (n.EstimatedAddedSugarPer100g.HasValue ? 1 : 0)
        + (n.EstimatedProteinPer100g.HasValue ? 1 : 0)
        + (n.EstimatedFatPer100g.HasValue ? 1 : 0)
        + (n.EstimatedSaturatedFatPer100g.HasValue ? 1 : 0)
        + (n.EstimatedFiberPer100g.HasValue ? 1 : 0)
        + (n.EstimatedSodiumPer100g.HasValue ? 1 : 0);

    // ═══════════════════════════════════════════════════════════════════
    // Regra 3 — Detecção de deslocamento de linha
    // ═══════════════════════════════════════════════════════════════════

    private static void ApplyLineDisplacementCheck(
        EstimatedNutritionProfileDto n,
        List<string> warnings,
        List<string> inconsistencies)
    {
        // Açúcar adicionado não pode ser maior que açúcar total
        if (n.EstimatedAddedSugarPer100g.HasValue && n.EstimatedSugarPer100g.HasValue
            && n.EstimatedAddedSugarPer100g.Value > n.EstimatedSugarPer100g.Value + 0.1)
        {
            inconsistencies.Add(
                $"Possível deslocamento de linha: açúcar adicionado ({n.EstimatedAddedSugarPer100g:F1}g) " +
                $"maior que açúcar total ({n.EstimatedSugarPer100g:F1}g). Valor de açúcar adicionado removido.");
            n.EstimatedAddedSugarPer100g = null;
        }

        // Açúcar total não pode ser maior que carboidratos totais
        if (n.EstimatedSugarPer100g.HasValue && n.EstimatedCarbsPer100g.HasValue
            && n.EstimatedSugarPer100g.Value > n.EstimatedCarbsPer100g.Value + 0.5)
        {
            inconsistencies.Add(
                $"Possível deslocamento: açúcar total ({n.EstimatedSugarPer100g:F1}g) " +
                $"excede carboidratos ({n.EstimatedCarbsPer100g:F1}g). Valor de açúcar suspeito.");
            // Correção segura: limitar açúcar ao valor de carboidratos
            n.EstimatedSugarPer100g = n.EstimatedCarbsPer100g;
        }

        // Gordura saturada não pode ser maior que gordura total
        if (n.EstimatedSaturatedFatPer100g.HasValue && n.EstimatedFatPer100g.HasValue
            && n.EstimatedSaturatedFatPer100g.Value > n.EstimatedFatPer100g.Value + 0.1)
        {
            inconsistencies.Add(
                $"Possível deslocamento: gordura saturada ({n.EstimatedSaturatedFatPer100g:F1}g) " +
                $"maior que gordura total ({n.EstimatedFatPer100g:F1}g). Valor de gordura saturada removido.");
            n.EstimatedSaturatedFatPer100g = null;
        }

        // Dois nutrientes com valor idêntico (suspeita de deslocamento) — apenas warning
        CheckDuplicateValues(n, warnings);
    }

    private static void CheckDuplicateValues(EstimatedNutritionProfileDto n, List<string> warnings)
    {
        var fields = new (string Name, double? Value)[]
        {
            ("proteínas", n.EstimatedProteinPer100g),
            ("gordura total", n.EstimatedFatPer100g),
            ("fibra", n.EstimatedFiberPer100g),
            ("sódio (g equiv.)", n.EstimatedSodiumPer100g > 0 ? n.EstimatedSodiumPer100g / 1000.0 : null),
            ("carboidratos", n.EstimatedCarbsPer100g),
            ("açúcar total", n.EstimatedSugarPer100g),
        };

        for (var i = 0; i < fields.Length - 1; i++)
        {
            if (!fields[i].Value.HasValue) continue;
            for (var j = i + 1; j < fields.Length; j++)
            {
                if (!fields[j].Value.HasValue) continue;
                if (Math.Abs(fields[i].Value.Value - fields[j].Value.Value) < 0.01 && fields[i].Value.Value > 0.5)
                {
                    warnings.Add(
                        $"Valores idênticos para {fields[i].Name} e {fields[j].Name} ({fields[i].Value:F1}). " +
                        "Possível deslocamento de linha no parser — verifique a imagem.");
                    break;
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Regra 4 — Campos obrigatórios quando tabela visível
    // ═══════════════════════════════════════════════════════════════════

    private static void ApplyMandatoryFieldsCheck(
        EstimatedNutritionProfileDto n,
        List<string> warnings,
        List<string> inconsistencies)
    {
        if (!n.CaloriesPer100g.HasValue)
            inconsistencies.Add("Tabela nutricional detectada, mas valor energético (calorias) está ausente.");
        if (!n.EstimatedSugarPer100g.HasValue)
            warnings.Add("Açúcar total ausente apesar de tabela nutricional visível.");
        if (!n.EstimatedProteinPer100g.HasValue)
            warnings.Add("Proteínas ausentes apesar de tabela nutricional visível.");
        if (!n.EstimatedSodiumPer100g.HasValue)
            warnings.Add("Sódio ausente apesar de tabela nutricional visível.");
        if (!n.EstimatedFatPer100g.HasValue)
            warnings.Add("Gordura total ausente apesar de tabela nutricional visível.");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Regra 5 — Coerência nutricional
    // ═══════════════════════════════════════════════════════════════════

    private static void ApplyNutritionalCoherenceChecks(
        EstimatedNutritionProfileDto n,
        NutritionAnalysisContext context,
        List<string> warnings,
        List<string> inconsistencies)
    {
        var category = Norm(context.CategoryNormalized ?? context.CategoryRaw);

        // Achocolatado em pó com proteína < 1g/100g é fisicamente implausível (tipicamente 10-20g)
        if (category.Contains("achocolatado") && n.EstimatedProteinPer100g.HasValue
            && n.EstimatedProteinPer100g.Value < 1.0)
        {
            inconsistencies.Add(
                $"Proteína suspeita para achocolatado em pó: {n.EstimatedProteinPer100g:F1}g/100g. " +
                "Valor zerado para evitar score incorreto.");
            n.EstimatedProteinPer100g = null;
        }

        // Carboidratos presentes mas açúcar nulo na tabela
        if (n.EstimatedCarbsPer100g.HasValue && n.EstimatedCarbsPer100g.Value > 5
            && !n.EstimatedSugarPer100g.HasValue)
            warnings.Add(
                $"Carboidratos detectados ({n.EstimatedCarbsPer100g:F1}g) mas açúcar total ausente. " +
                "Score pode subestimar impacto glicêmico.");

        // Calorias nulas com tabela detectada
        if (!n.CaloriesPer100g.HasValue)
            warnings.Add("Valor energético ausente. Score baseado apenas em macronutrientes parciais.");

        // Calorias muito baixas para sólido não dietético
        if (n.CaloriesPer100g.HasValue && n.CaloriesPer100g.Value < 5
            && !IsLowCalorieCategory(category))
            inconsistencies.Add(
                $"Valor energético de {n.CaloriesPer100g:F0} kcal/100g é suspeito para esta categoria. " +
                "Possível confusão entre coluna por porção e coluna por 100g.");

        // Sódio > 5000 mg/100g é fisicamente impossível em alimentos
        if (n.EstimatedSodiumPer100g.HasValue && n.EstimatedSodiumPer100g.Value > 5000)
        {
            inconsistencies.Add(
                $"Sódio de {n.EstimatedSodiumPer100g:F0}mg/100g é impossível. Valor removido.");
            n.EstimatedSodiumPer100g = null;
        }

        // Proteína > 100g/100g é impossível
        if (n.EstimatedProteinPer100g.HasValue && n.EstimatedProteinPer100g.Value > 100)
        {
            inconsistencies.Add($"Proteína de {n.EstimatedProteinPer100g:F0}g/100g é impossível. Valor removido.");
            n.EstimatedProteinPer100g = null;
        }

        // Soma de macronutrientes não pode exceder 100g/100g por larga margem
        var macroSum = (n.EstimatedCarbsPer100g ?? 0)
            + (n.EstimatedFatPer100g ?? 0)
            + (n.EstimatedProteinPer100g ?? 0)
            + (n.EstimatedFiberPer100g ?? 0);

        if (macroSum > 110)
            inconsistencies.Add(
                $"Soma dos macronutrientes ({macroSum:F1}g/100g) excede 100g. " +
                "Possível mistura de colunas (por porção vs 100g).");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Regra 6 — Porção e embalagem
    // ═══════════════════════════════════════════════════════════════════

    private static void ApplyPortionPackageCheck(
        EstimatedNutritionProfileDto n,
        NutritionAnalysisContext context,
        List<string> warnings,
        List<string> inconsistencies)
    {
        if (string.IsNullOrWhiteSpace(context.PackageWeight)) return;

        // Tentar extrair gramagem da embalagem
        var packageGrams = ExtractGrams(context.PackageWeight);
        if (packageGrams <= 0) return;

        // EstimatedPackageCalories baseadas em 100g
        if (n.EstimatedPackageCalories.HasValue && n.CaloriesPer100g.HasValue && packageGrams > 0)
        {
            var expected = n.CaloriesPer100g.Value * packageGrams / 100.0;
            var ratio = n.EstimatedPackageCalories.Value / (expected + 0.001);

            // Se a divergência for maior que 3x em qualquer direção
            if (ratio > 3.0 || ratio < 0.33)
            {
                warnings.Add(
                    $"Calorias estimadas da embalagem ({n.EstimatedPackageCalories:F0} kcal) incompatíveis " +
                    $"com {n.CaloriesPer100g:F0} kcal/100g × {packageGrams}g = {expected:F0} kcal esperados. " +
                    "Valor de calorias da embalagem removido.");
                n.EstimatedPackageCalories = null;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Regra 7 — Calorias: consistência interna
    // ═══════════════════════════════════════════════════════════════════

    private static void ApplyCaloriesConsistencyCheck(
        EstimatedNutritionProfileDto n,
        List<string> warnings,
        List<string> inconsistencies)
    {
        // Se não há calorias mas há macros, podemos estimar para validação
        if (!n.CaloriesPer100g.HasValue
            && (n.EstimatedCarbsPer100g.HasValue || n.EstimatedFatPer100g.HasValue || n.EstimatedProteinPer100g.HasValue))
        {
            var estimated = ((n.EstimatedCarbsPer100g ?? 0) * 4.0)
                + ((n.EstimatedProteinPer100g ?? 0) * 4.0)
                + ((n.EstimatedFatPer100g ?? 0) * 9.0);

            if (estimated > 10)
                warnings.Add(
                    $"Valor energético ausente, mas macronutrientes sugerem ~{estimated:F0} kcal/100g. " +
                    "Score será calculado com base nos macros disponíveis.");
        }

        // EstimatedPackageCalories sem CaloriesPer100g → remover por não ter base confiável
        if (n.EstimatedPackageCalories.HasValue && !n.CaloriesPer100g.HasValue)
        {
            n.EstimatedPackageCalories = null;
            warnings.Add("Calorias totais da embalagem removidas: valor energético por 100g não disponível.");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Regra 8 — CanScoreReliably
    // ═══════════════════════════════════════════════════════════════════

    private static bool EvaluateCanScoreReliably(
        EstimatedNutritionProfileDto n,
        bool tableDetected,
        string parserConfidence)
    {
        if (!tableDetected) return false;

        // Precisamos de pelo menos: energia OU (carbs + fat) E um dos marcadores de risco
        var hasEnergy = n.CaloriesPer100g.HasValue;
        var hasCarbs = n.EstimatedCarbsPer100g.HasValue || n.EstimatedSugarPer100g.HasValue;
        var hasFat = n.EstimatedFatPer100g.HasValue;
        var hasSodiumOrProtein = n.EstimatedSodiumPer100g.HasValue || n.EstimatedProteinPer100g.HasValue;

        return parserConfidence != "low"
            && (hasEnergy || (hasCarbs && hasFat))
            && hasSodiumOrProtein;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Regra 10 — Recalcular ParserConfidence
    // ═══════════════════════════════════════════════════════════════════

    private static string RecalculateParserConfidence(
        EstimatedNutritionProfileDto n,
        bool tableDetected,
        int inconsistencyCount)
    {
        if (!tableDetected) return "low";

        var present = CountExtractedNutrients(n);
        // Dos 9 nutrientes principais, quantos estão presentes?
        // HIGH: >= 7 + sem inconsistências graves (< 2)
        // MEDIUM: >= 4 + inconsistências leves (< 4)
        // LOW: caso contrário

        if (present >= 7 && inconsistencyCount < 2) return "high";
        if (present >= 4 && inconsistencyCount < 4) return "medium";
        return "low";
    }

    // ═══════════════════════════════════════════════════════════════════
    // Regra 11 — ShouldReprocess
    // ═══════════════════════════════════════════════════════════════════

    private static bool EvaluateShouldReprocess(
        EstimatedNutritionProfileDto n,
        bool tableDetected,
        List<string> inconsistencies)
    {
        if (!tableDetected) return false;

        var present = CountExtractedNutrients(n);

        // < 50% dos 9 nutrientes principais (< 5)
        if (present < 5) return true;

        // Campos críticos ausentes apesar de tabela detectada
        var criticalMissing = !n.CaloriesPer100g.HasValue
            || (!n.EstimatedSugarPer100g.HasValue && !n.EstimatedCarbsPer100g.HasValue)
            || !n.EstimatedProteinPer100g.HasValue
            || !n.EstimatedSodiumPer100g.HasValue;

        if (criticalMissing) return true;

        // Suspeita de deslocamento detectada
        var hasDisplacementSuspicion = inconsistencies.Any(i =>
            i.Contains("deslocamento", StringComparison.OrdinalIgnoreCase)
            || i.Contains("mistura de colunas", StringComparison.OrdinalIgnoreCase));

        return hasDisplacementSuspicion;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════

    private static EstimatedNutritionProfileDto CloneNutrition(EstimatedNutritionProfileDto src) =>
        new()
        {
            CaloriesPer100g = src.CaloriesPer100g,
            EstimatedPackageCalories = src.EstimatedPackageCalories,
            EstimatedCarbsPer100g = src.EstimatedCarbsPer100g,
            EstimatedSugarPer100g = src.EstimatedSugarPer100g,
            EstimatedAddedSugarPer100g = src.EstimatedAddedSugarPer100g,
            EstimatedSaturatedFatPer100g = src.EstimatedSaturatedFatPer100g,
            EstimatedProteinPer100g = src.EstimatedProteinPer100g,
            EstimatedSodiumPer100g = src.EstimatedSodiumPer100g,
            EstimatedFiberPer100g = src.EstimatedFiberPer100g,
            EstimatedFatPer100g = src.EstimatedFatPer100g,
            Basis = src.Basis,
            ParserConfidence = src.ParserConfidence
        };

    private static string Norm(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static bool IsLowCalorieCategory(string normCategory) =>
        normCategory.Contains("light") || normCategory.Contains("diet")
        || normCategory.Contains("zero") || normCategory.Contains("água")
        || normCategory.Contains("agua") || normCategory.Contains("chá")
        || normCategory.Contains("cha");

    private static double ExtractGrams(string? packageWeight)
    {
        if (string.IsNullOrWhiteSpace(packageWeight)) return 0;

        // Ex: "200 g", "200g", "1,5 kg", "1.5kg", "500 ml"
        var norm = packageWeight.Replace(',', '.').ToLowerInvariant();

        var match = System.Text.RegularExpressions.Regex.Match(norm, @"(\d+(?:\.\d+)?)");
        if (!match.Success || !double.TryParse(match.Value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var value))
            return 0;

        if (norm.Contains("kg")) return value * 1000.0;
        if (norm.Contains("ml") || norm.Contains("l") && !norm.Contains("ml"))
        {
            // ml → aproximar como gramas para líquidos (densidade ~1)
            if (norm.Contains("ml")) return value;
            if (norm.Contains(" l") || norm.EndsWith("l")) return value * 1000.0;
        }
        return value; // assume gramas
    }
}
