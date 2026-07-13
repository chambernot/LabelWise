using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Infrastructure.AI.DTOs;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.AI;

/// <summary>
/// Pós-processa e valida os dados nutricionais extraídos pela IA antes de alimentar o pipeline.
///
/// Responsabilidades:
/// - Detectar mistura de colunas (valores de porção interpretados como 100g)
/// - Inferir açúcar adicionado quando ausente e há evidência de ultraprocessamento
/// - Corrigir calorias da embalagem usando lógica confiável
/// - Calcular e retornar parserConfidence baseado em campos presentes
/// - Emitir warnings técnicos rastreáveis
/// </summary>
internal sealed class NutritionExtractionPostProcessor
{
    private readonly ILogger _logger;

    public NutritionExtractionPostProcessor(ILogger logger) => _logger = logger;

    public NutritionExtractionResult Process(
        NutritionProfileResponse? raw,
        string? packageWeightRaw,
        string? category,
        bool isUltraProcessed)
    {
        var result = new NutritionExtractionResult();

        if (raw == null)
        {
            result.Profile = new EstimatedNutritionProfileDto
            {
                Basis = "Perfil nutricional não extraído da imagem.",
                ParserConfidence = "low"
            };
            result.Warnings.Add("Perfil nutricional ausente na resposta da IA.");
            return result;
        }

        // ── 1. Normalizar valores mínimos (null safety) ──────────────
        var profile = MapRawToDto(raw);

        // ── 2. Detectar mistura de coluna porção × 100g ───────────────
        ApplyColumnMixingCorrection(profile, raw, result);

        // ── 3. Detecção de deslocamento de linha (row displacement) ───
        ApplyRowDisplacementDetection(profile, raw, result);

        // ── 4. Validação de coerência matemática ──────────────────────
        ApplyMathCoherenceValidation(profile, result);

        // ── 5. Inferência de açúcar adicionado ────────────────────────
        ApplyAddedSugarInference(profile, category, isUltraProcessed, result);

        // ── 6. Corrigir packageWeight usando portionWeightG ───────────
        var resolvedPackageWeight = ResolvePackageWeight(packageWeightRaw, raw.PortionWeightG, profile, result);
        result.ResolvedPackageWeight = resolvedPackageWeight;

        // ── 7. Corrigir calorias da embalagem ─────────────────────────
        CorrectPackageCalories(profile, raw, resolvedPackageWeight, result);

        // ── 8. Validações de integridade por categoria ────────────────
        ApplyCategoryIntegrityRules(profile, category, result);

        // ── 9. Calcular parserConfidence ──────────────────────────────
        profile.ParserConfidence = ComputeConfidence(profile, result);

        result.Profile = profile;
        return result;
    }

    // ─────────────────────────────────────────────────────────────────
    // 1. Mapeamento raw → DTO
    // ─────────────────────────────────────────────────────────────────

    private static EstimatedNutritionProfileDto MapRawToDto(NutritionProfileResponse raw) =>
        new()
        {
            CaloriesPer100g            = raw.CaloriesPer100g,
            EstimatedPackageCalories   = raw.EstimatedPackageCalories,
            EstimatedCarbsPer100g      = raw.EstimatedCarbsPer100g,
            EstimatedSugarPer100g      = raw.EstimatedSugarPer100g,
            EstimatedAddedSugarPer100g = raw.EstimatedAddedSugarPer100g,
            EstimatedSaturatedFatPer100g = raw.EstimatedSaturatedFatPer100g,
            EstimatedProteinPer100g    = raw.EstimatedProteinPer100g,
            EstimatedSodiumPer100g     = raw.EstimatedSodiumPer100g,
            EstimatedFiberPer100g      = raw.EstimatedFiberPer100g,
            EstimatedFatPer100g        = raw.EstimatedFatPer100g,
            Basis                      = raw.Basis ?? "Dados extraídos da tabela nutricional."
        };

    // ─────────────────────────────────────────────────────────────────
    // 2. Detecção e correção de mistura de coluna porção × 100g
    // ─────────────────────────────────────────────────────────────────

    private void ApplyColumnMixingCorrection(
        EstimatedNutritionProfileDto profile,
        NutritionProfileResponse raw,
        NutritionExtractionResult result)
    {
        var portionG = raw.PortionWeightG;
        if (portionG is null or <= 0 or >= 100)
            return; // Sem porção declarada ou porção >= 100g: sem base para correção

        var factor = 100.0 / portionG.Value;

        // Heurística: se proteína é suspeita (< 1g/100g em alimento sólido)
        // e multiplicando pelo fator daria valor plausível → corrigir
        if (SuspectPortionValue(profile.EstimatedProteinPer100g, portionG.Value, 1.0, 45.0))
        {
            var corrected = Math.Round(profile.EstimatedProteinPer100g!.Value * factor, 1);
            result.Warnings.Add($"Proteína corrigida: {profile.EstimatedProteinPer100g:F1}g (porção) → {corrected}g (por 100g). Porção declarada: {portionG}g.");
            profile.EstimatedProteinPer100g = corrected;
            result.ColumnMixingDetected = true;
        }

        if (SuspectPortionValue(profile.EstimatedSugarPer100g, portionG.Value, 0.5, 80.0))
        {
            var corrected = Math.Round(profile.EstimatedSugarPer100g!.Value * factor, 1);
            result.Warnings.Add($"Açúcar corrigido: {profile.EstimatedSugarPer100g:F1}g (porção) → {corrected}g (por 100g).");
            profile.EstimatedSugarPer100g = corrected;
            result.ColumnMixingDetected = true;
        }

        if (SuspectPortionValue(profile.EstimatedFatPer100g, portionG.Value, 1.0, 60.0))
        {
            var corrected = Math.Round(profile.EstimatedFatPer100g!.Value * factor, 1);
            result.Warnings.Add($"Gordura corrigida: {profile.EstimatedFatPer100g:F1}g (porção) → {corrected}g (por 100g).");
            profile.EstimatedFatPer100g = corrected;
            result.ColumnMixingDetected = true;
        }

        if (SuspectPortionValue(profile.EstimatedSodiumPer100g, portionG.Value, 30.0, 3000.0))
        {
            var corrected = Math.Round(profile.EstimatedSodiumPer100g!.Value * factor, 0);
            result.Warnings.Add($"Sódio corrigido: {profile.EstimatedSodiumPer100g:F0}mg (porção) → {corrected}mg (por 100g).");
            profile.EstimatedSodiumPer100g = corrected;
            result.ColumnMixingDetected = true;
        }

        if (SuspectPortionValue(profile.CaloriesPer100g, portionG.Value, 30.0, 700.0))
        {
            var corrected = Math.Round(profile.CaloriesPer100g!.Value * factor, 0);
            result.Warnings.Add($"Calorias corrigidas: {profile.CaloriesPer100g:F0}kcal (porção) → {corrected}kcal (por 100g).");
            profile.CaloriesPer100g = corrected;
            result.ColumnMixingDetected = true;
        }

        if (result.ColumnMixingDetected)
            result.Warnings.Add("Mistura de coluna porção × 100g detectada e corrigida automaticamente.");
    }

    /// <summary>
    /// Retorna true se o valor é suspeito de ser da coluna de porção em vez de 100g.
    /// Critério: valor existe + está abaixo do limiar mínimo esperado para 100g
    ///           + quando multiplicado pelo fator (100/porção) resultaria em valor plausível.
    /// </summary>
    private static bool SuspectPortionValue(double? value, double portionG, double minPer100g, double maxPer100g)
    {
        if (value is null or <= 0) return false;
        if (value >= minPer100g) return false; // Já parece 100g

        var projected = value.Value * (100.0 / portionG);
        return projected >= minPer100g && projected <= maxPer100g;
    }

    // ─────────────────────────────────────────────────────────────────
    // 3. Validação de coerência matemática
    // ─────────────────────────────────────────────────────────────────

    private void ApplyMathCoherenceValidation(
        EstimatedNutritionProfileDto profile,
        NutritionExtractionResult result)
    {
        var calories = profile.CaloriesPer100g;
        var protein  = profile.EstimatedProteinPer100g;
        var carbs    = profile.EstimatedCarbsPer100g;
        var fat      = profile.EstimatedFatPer100g;

        if (calories is null || protein is null || carbs is null || fat is null)
            return;

        var calculated = protein.Value * 4 + carbs.Value * 4 + fat.Value * 9;
        var diff = Math.Abs(calories.Value - calculated) / Math.Max(calories.Value, 1.0);

        if (diff > 0.35)
        {
            result.MathCoherenceFailure = true;
            result.Warnings.Add(
                $"Incoerência matemática: calorias declaradas={calories:F0}kcal, " +
                $"calculadas por macros={calculated:F0}kcal (divergência {diff:P0}). " +
                "Possível erro de extração ou mistura de colunas.");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 4. Inferência de açúcar adicionado
    // ─────────────────────────────────────────────────────────────────

    private static void ApplyAddedSugarInference(
        EstimatedNutritionProfileDto profile,
        string? category,
        bool isUltraProcessed,
        NutritionExtractionResult result)
    {
        if (profile.EstimatedAddedSugarPer100g.HasValue)
            return; // Já extraído da tabela

        var sugar = profile.EstimatedSugarPer100g ?? 0;
        if (sugar <= 0) return;

        var cat = (category ?? string.Empty).ToLowerInvariant();
        var isHighSugarCategory = cat.Contains("refrigerante") || cat.Contains("achocolatado")
            || cat.Contains("biscoito recheado") || cat.Contains("chocolate") || cat.Contains("sobremesa")
            || cat.Contains("suco") || cat.Contains("néctar") || cat.Contains("nectar");

        // Se produto ultraprocessado E açúcar total > 8g: inferir açúcar adicionado
        if (isUltraProcessed && sugar > 8)
        {
            // Conservador: assumir 70–90% do açúcar total como adicionado em ultraprocessados
            var inferred = Math.Round(sugar * 0.8, 1);
            profile.EstimatedAddedSugarPer100g = inferred;
            result.AddedSugarInferred = true;
            result.Warnings.Add(
                $"Açúcar adicionado não declarado no rótulo. Estimado em {inferred}g/100g " +
                $"(≈80% do açúcar total) por se tratar de produto ultraprocessado com {sugar}g de açúcar total.");
            return;
        }

        // Categoria de risco + açúcar alto → inferência conservadora
        if (isHighSugarCategory && sugar > 5)
        {
            var inferred = Math.Round(sugar * 0.75, 1);
            profile.EstimatedAddedSugarPer100g = inferred;
            result.AddedSugarInferred = true;
            result.Warnings.Add(
                $"Açúcar adicionado ausente no rótulo. Estimado em {inferred}g/100g para categoria '{category}' com {sugar}g de açúcar total.");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 3. Detecção de deslocamento de linha (row displacement)
    // ─────────────────────────────────────────────────────────────────

    private void ApplyRowDisplacementDetection(
        EstimatedNutritionProfileDto profile,
        NutritionProfileResponse raw,
        NutritionExtractionResult result)
    {
        // Se açúcar adicionado == proteínas por 100g, provavelmente houve deslocamento de linha.
        // A IA atribuiu o valor de proteína ao campo de açúcar adicionado.
        if (profile.EstimatedAddedSugarPer100g.HasValue
            && profile.EstimatedProteinPer100g.HasValue
            && Math.Abs(profile.EstimatedAddedSugarPer100g.Value - profile.EstimatedProteinPer100g.Value) < 0.05)
        {
            result.Warnings.Add(
                $"Possível deslocamento de linha: estimatedAddedSugarPer100g ({profile.EstimatedAddedSugarPer100g:F1}g) " +
                $"igual a estimatedProteinPer100g ({profile.EstimatedProteinPer100g:F1}g). " +
                "Açúcar adicionado zerado para evitar score incorreto.");
            profile.EstimatedAddedSugarPer100g = null;
            result.RowDisplacementDetected = true;
        }

        // Se gordura total == proteína ou == açúcar adicionado (suspeita de deslocamento)
        if (profile.EstimatedFatPer100g.HasValue
            && profile.EstimatedProteinPer100g.HasValue
            && Math.Abs(profile.EstimatedFatPer100g.Value - profile.EstimatedProteinPer100g.Value) < 0.05
            && profile.EstimatedFatPer100g.Value > 0.5)
        {
            result.Warnings.Add(
                $"Possível deslocamento de linha: estimatedFatPer100g ({profile.EstimatedFatPer100g:F1}g) " +
                $"igual a estimatedProteinPer100g ({profile.EstimatedProteinPer100g:F1}g). " +
                "Gordura total zerada para evitar score incorreto.");
            profile.EstimatedFatPer100g = null;
            result.RowDisplacementDetected = true;
        }

        // Proteína > açúcar total em produto claramente doce (achocolatado, biscoito recheado etc.)
        // é suspeito quando os valores parecem trocados entre linhas de açúcar adicionado e proteína
        if (profile.EstimatedAddedSugarPer100g.HasValue
            && profile.EstimatedSugarPer100g.HasValue
            && profile.EstimatedAddedSugarPer100g.Value > profile.EstimatedSugarPer100g.Value + 0.5)
        {
            result.Warnings.Add(
                $"Deslocamento: açúcar adicionado ({profile.EstimatedAddedSugarPer100g:F1}g) " +
                $"> açúcar total ({profile.EstimatedSugarPer100g:F1}g). Valor removido.");
            profile.EstimatedAddedSugarPer100g = null;
            result.RowDisplacementDetected = true;
        }

        // Detecção específica de contaminação por %VD na gordura total.
        // Padrão observado: linha "Gorduras totais 0,3 0,5 1" → AI retorna 1.5 = 0.5 + 1
        // (soma da coluna porção com o %VD em vez de usar a coluna 100g).
        // Sinal: gordura total > 1g AND gordura saturada < 0.3g AND razão > 8x
        // (para alimentos reais, gordura saturada costuma ser 15–70% da gordura total).
        if (profile.EstimatedFatPer100g is > 1.0 and < 5.0
            && profile.EstimatedSaturatedFatPer100g is > 0 and < 0.3
            && (profile.EstimatedFatPer100g / profile.EstimatedSaturatedFatPer100g) > 8.0)
        {
            result.Warnings.Add(
                $"Gordura total suspeita ({profile.EstimatedFatPer100g:F1}g) com gordura saturada de apenas " +
                $"{profile.EstimatedSaturatedFatPer100g:F2}g — razão {profile.EstimatedFatPer100g / profile.EstimatedSaturatedFatPer100g:F0}x. " +
                "Possível soma de coluna porção + %VD. Valor zerado para evitar score incorreto.");
            profile.EstimatedFatPer100g = null;
            result.RowDisplacementDetected = true;
        }

        if (result.RowDisplacementDetected)
            result.Warnings.Add("Deslocamento de linha detectado no parser. parserConfidence reduzida para medium.");
    }

    // ─────────────────────────────────────────────────────────────────
    // 6. Resolução de packageWeight
    // ─────────────────────────────────────────────────────────────────

    private string? ResolvePackageWeight(
        string? packageWeightRaw,
        double? portionWeightG,
        EstimatedNutritionProfileDto profile,
        NutritionExtractionResult result)
    {
        // Estratégia 1: packageWeight declarado e parece plausível (não coincide com um macro)
        if (!string.IsNullOrWhiteSpace(packageWeightRaw))
        {
            var grams = ParsePackageWeightGrams(packageWeightRaw);
            if (grams.HasValue && !LooksLikeAMacroValue(grams.Value, profile))
                return packageWeightRaw;

            if (grams.HasValue && LooksLikeAMacroValue(grams.Value, profile))
            {
                result.Warnings.Add(
                    $"packageWeight '{packageWeightRaw}' coincide com valor de nutriente e foi ignorado. " +
                    "Usando portionWeightG como fallback.");
                result.PackageWeightSuspect = true;
            }
        }

        // Estratégia 2: usar portionWeightG como packageWeight (porção declarada no rótulo)
        if (portionWeightG is > 0 and < 500)
            return $"{portionWeightG:G} g";

        return packageWeightRaw; // Manter original se não há alternativa
    }

    /// <summary>
    /// Retorna true se o valor em gramas coincide com um macro do perfil nutricional,
    /// sugerindo que o AI extraiu um valor de nutriente como peso da embalagem.
    /// </summary>
    private static bool LooksLikeAMacroValue(double grams, EstimatedNutritionProfileDto profile)
    {
        var macros = new[]
        {
            profile.EstimatedCarbsPer100g,
            profile.EstimatedSugarPer100g,
            profile.EstimatedProteinPer100g,
            profile.EstimatedFatPer100g,
            profile.EstimatedFiberPer100g
        };

        return macros.Any(m => m.HasValue && Math.Abs(m.Value - grams) < 0.5);
    }

    // ─────────────────────────────────────────────────────────────────
    // 7. Correção de calorias da embalagem (por porção)
    // ─────────────────────────────────────────────────────────────────

    private static void CorrectPackageCalories(
        EstimatedNutritionProfileDto profile,
        NutritionProfileResponse raw,
        string? resolvedPackageWeight,
        NutritionExtractionResult result)
    {
        var calories100g = profile.CaloriesPer100g;
        if (calories100g is null or <= 0)
        {
            profile.EstimatedPackageCalories = null;
            return;
        }

        // Estratégia 1: usar caloriesPerPortion direto da coluna de porção (prioridade absoluta).
        // Este é o único valor confiável quando a tabela usa base por 100ml de bebida preparada
        // em vez de 100g do produto puro — situação comum em achocolatados, shakes e pós solúveis.
        if (raw.CaloriesPerPortion is > 0)
        {
            var portionCal = raw.CaloriesPerPortion.Value;

            // Sanity check: caloriesPerPortion deve ser <= caloriesPer100g * 3
            // (porções maiores que 300ml/300g são raras; acima disso é suspeito)
            if (portionCal <= calories100g.Value * 3.0)
            {
                profile.EstimatedPackageCalories = portionCal;
                return;
            }

            result.Warnings.Add(
                $"caloriesPerPortion ({portionCal}) suspeito (> 3× caloriesPer100g={calories100g:F0}). Ignorado.");
        }

        // ESTRATÉGIA 2 REMOVIDA INTENCIONALMENTE.
        // Calcular calorias via portionWeightG × caloriesPer100g / 100 gera resultados errados
        // quando a coluna "100g" da tabela representa "100ml de bebida preparada" e a porção
        // é feita com pó + leite (ex: achocolatado). O valor correto só pode vir de caloriesPerPortion.

        // Estratégia 3: calcular via resolvedPackageWeight — apenas se for peso real da embalagem
        // (não quando foi derivado de portionWeightG como fallback).
        if (result.PackageWeightSuspect)
        {
            // packageWeight veio de portionWeightG: o mesmo problema de cálculo se aplicaria.
            profile.EstimatedPackageCalories = null;
            result.Warnings.Add(
                "Calorias da porção não puderam ser calculadas: packageWeight é a porção declarada, não o peso total. " +
                "Valor caloriesPerPortion não disponível na resposta do modelo.");
            return;
        }

        var packageG = ParsePackageWeightGrams(resolvedPackageWeight);
        if (packageG is > 0)
        {
            var calculated = Math.Round(calories100g.Value * packageG.Value / 100.0, 0);

            if (profile.EstimatedPackageCalories.HasValue)
            {
                var existing = profile.EstimatedPackageCalories.Value;
                var diff = Math.Abs(existing - calculated) / Math.Max(calculated, 1.0);
                if (diff > 0.30)
                {
                    result.Warnings.Add(
                        $"Calorias da embalagem corrigidas: {existing:F0}kcal → {calculated:F0}kcal " +
                        $"({calories100g:F0}kcal/100g × {packageG}g).");
                }
            }

            profile.EstimatedPackageCalories = calculated;
            return;
        }

        profile.EstimatedPackageCalories = null;
    }

    private static double? ParsePackageWeightGrams(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var match = System.Text.RegularExpressions.Regex.Match(
            raw.Trim(),
            @"(\d+(?:[.,]\d+)?)\s*(g|kg|ml|l)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!match.Success) return null;

        var value = double.Parse(match.Groups[1].Value.Replace(',', '.'),
            System.Globalization.CultureInfo.InvariantCulture);

        var unit = match.Groups[2].Value.ToLowerInvariant();
        return unit switch
        {
            "kg" => value * 1000,
            "l"  => value * 1000, // 1L ≈ 1000ml → assume mesma densidade
            _    => value           // g ou ml: direto
        };
    }

    // ─────────────────────────────────────────────────────────────────
    // 6. Regras de integridade por categoria
    // ─────────────────────────────────────────────────────────────────

    private void ApplyCategoryIntegrityRules(
        EstimatedNutritionProfileDto profile,
        string? category,
        NutritionExtractionResult result)
    {
        var cat = (category ?? string.Empty).ToLowerInvariant();

        // Achocolatado/bebida achocolatada: proteína < 0.5g/100g é erro
        if ((cat.Contains("achocolatado") || cat.Contains("nescau") || cat.Contains("toddy"))
            && profile.EstimatedProteinPer100g is < 0.5)
        {
            result.Warnings.Add(
                $"Proteína suspeita ({profile.EstimatedProteinPer100g:F2}g/100g) para categoria '{category}'. " +
                "Achocolatados geralmente têm 2–5g/100g. Verifique se houve mistura de coluna.");
            result.SuspectFields.Add("proteína");
        }

        // Suplemento de proteína: proteína deve ser > 50g/100g
        if (cat.Contains("whey") || cat.Contains("proteína em pó") || cat.Contains("suplemento proteico"))
        {
            if (profile.EstimatedProteinPer100g is < 40)
            {
                result.Warnings.Add(
                    $"Proteína suspeita ({profile.EstimatedProteinPer100g:F1}g/100g) para suplemento proteico. Esperado > 40g/100g.");
                result.SuspectFields.Add("proteína");
            }
        }

        // Sódio em bebidas: mg/100ml — valores > 500 são incomuns exceto para repositores
        if (IsBeverageCategory(cat) && profile.EstimatedSodiumPer100g > 500
            && !cat.Contains("isotônico") && !cat.Contains("repositor"))
        {
            result.Warnings.Add(
                $"Sódio suspeito ({profile.EstimatedSodiumPer100g:F0}mg/100ml) para bebida '{category}'. Pode ser valor de porção em vez de 100ml.");
            result.SuspectFields.Add("sódio");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 9. Cálculo de parserConfidence
    // ─────────────────────────────────────────────────────────────────

    private static string ComputeConfidence(EstimatedNutritionProfileDto profile, NutritionExtractionResult result)
    {
        // Campos principais obrigatórios para alta confiança
        var present = new[]
        {
            profile.CaloriesPer100g.HasValue,
            profile.EstimatedProteinPer100g.HasValue,
            profile.EstimatedFatPer100g.HasValue,
            profile.EstimatedCarbsPer100g.HasValue,
            profile.EstimatedSodiumPer100g.HasValue
        };

        var presentCount = present.Count(p => p);

        // Fatores que reduzem confiança
        var hasCriticalMissing = !profile.CaloriesPer100g.HasValue
            || !profile.EstimatedProteinPer100g.HasValue
            || !profile.EstimatedSugarPer100g.HasValue;

        // Deslocamento de linha é degradação severa — nunca high
        if (result.RowDisplacementDetected)
            return presentCount >= 4 ? "medium" : "low";

        if (result.MathCoherenceFailure || result.ColumnMixingDetected || hasCriticalMissing)
            return presentCount >= 4 ? "medium" : "low";

        if (result.SuspectFields.Count > 0 || result.AddedSugarInferred || result.PackageWeightSuspect)
            return "medium";

        return presentCount switch
        {
            5 => "high",
            >= 3 => "medium",
            _ => "low"
        };
    }

    private static bool IsBeverageCategory(string norm) =>
        norm.Contains("suco") || norm.Contains("néctar") || norm.Contains("nectar")
        || norm.Contains("bebida") || norm.Contains("refrigerante") || norm.Contains("achocolatado");
}

/// <summary>Resultado do pós-processamento de extração nutricional.</summary>
internal sealed class NutritionExtractionResult
{
    public EstimatedNutritionProfileDto Profile { get; set; } = new();
    public List<string> Warnings { get; set; } = [];
    public List<string> SuspectFields { get; set; } = [];
    public bool ColumnMixingDetected { get; set; }
    public bool MathCoherenceFailure { get; set; }
    public bool AddedSugarInferred { get; set; }
    public bool RowDisplacementDetected { get; set; }
    public bool PackageWeightSuspect { get; set; }
    /// <summary>packageWeight resolvido após validação (pode vir de portionWeightG quando o original é suspeito).</summary>
    public string? ResolvedPackageWeight { get; set; }
}
