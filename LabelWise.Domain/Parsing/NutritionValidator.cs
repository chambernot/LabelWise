namespace LabelWise.Domain.Parsing;

/// <summary>
/// Validador de dados nutricionais extraídos.
/// Garante consistência e detecta valores suspeitos.
/// </summary>
public static class NutritionValidator
{
    /// <summary>
    /// Valida e corrige inconsistências nos dados nutricionais
    /// </summary>
    public static void ValidateAndCorrect(NutritionData data)
    {
        if (data == null) return;

        // REGRA 1: Nenhum valor pode ser negativo
        EnsureNonNegative(data);

        // REGRA 2: Açúcar adicionado não pode ser maior que açúcar total
        if (data.AddedSugarPer100g.HasValue && data.SugarPer100g.HasValue)
        {
            if (data.AddedSugarPer100g.Value > data.SugarPer100g.Value)
            {
                data.Warnings.Add("Açúcar adicionado maior que açúcar total - corrigido");
                data.SugarPer100g = data.AddedSugarPer100g;
            }
        }

        // REGRA 3: Gordura saturada não pode ser maior que gordura total
        if (data.SaturatedFatPer100g.HasValue && data.FatPer100g.HasValue)
        {
            if (data.SaturatedFatPer100g.Value > data.FatPer100g.Value)
            {
                data.Warnings.Add("Gordura saturada maior que gordura total - corrigido");
                data.FatPer100g = data.SaturatedFatPer100g;
            }
        }

        // REGRA 4: Validação de calorias vs macronutrientes
        ValidateCalorieConsistency(data);

        // REGRA 5: Valores fora de range razoável
        ValidateRanges(data);

        // REGRA 6: Detectar erros comuns de OCR
        DetectOcrErrors(data);
    }

    /// <summary>
    /// Garante que nenhum valor seja negativo
    /// </summary>
    private static void EnsureNonNegative(NutritionData data)
    {
        if (data.CaloriesPer100g < 0) data.CaloriesPer100g = null;
        if (data.CarbsPer100g < 0) data.CarbsPer100g = null;
        if (data.SugarPer100g < 0) data.SugarPer100g = null;
        if (data.AddedSugarPer100g < 0) data.AddedSugarPer100g = null;
        if (data.ProteinPer100g < 0) data.ProteinPer100g = null;
        if (data.FatPer100g < 0) data.FatPer100g = null;
        if (data.SaturatedFatPer100g < 0) data.SaturatedFatPer100g = null;
        if (data.FiberPer100g < 0) data.FiberPer100g = null;
        if (data.SodiumPer100g < 0) data.SodiumPer100g = null;
    }

    /// <summary>
    /// Valida consistência calórica (calorias = 4*carbs + 4*protein + 9*fat)
    /// </summary>
    private static void ValidateCalorieConsistency(NutritionData data)
    {
        if (!data.CaloriesPer100g.HasValue) return;
        if (!data.CarbsPer100g.HasValue || !data.ProteinPer100g.HasValue || !data.FatPer100g.HasValue)
            return;

        var expectedCalories = 
            (data.CarbsPer100g.Value * 4) +
            (data.ProteinPer100g.Value * 4) +
            (data.FatPer100g.Value * 9);

        var actualCalories = data.CaloriesPer100g.Value;
        var delta = Math.Abs(actualCalories - expectedCalories) / expectedCalories;

        // Se divergência > 30%, marcar warning
        if (delta > 0.30)
        {
            data.Warnings.Add($"Inconsistência calórica detectada: {actualCalories:F0} kcal vs {expectedCalories:F0} kcal esperado (delta: {delta:P0})");
        }
    }

    /// <summary>
    /// Valida se valores estão dentro de ranges razoáveis
    /// </summary>
    private static void ValidateRanges(NutritionData data)
    {
        // Calorias: 0-900 kcal/100g
        if (data.CaloriesPer100g > 900)
        {
            data.Warnings.Add($"Calorias muito altas ({data.CaloriesPer100g:F0}), possível erro OCR");
            data.CaloriesPer100g = null;
        }

        // Macronutrientes: 0-100g
        if (data.CarbsPer100g > 100) data.CarbsPer100g = null;
        if (data.ProteinPer100g > 100) data.ProteinPer100g = null;
        if (data.FatPer100g > 100) data.FatPer100g = null;
        if (data.SugarPer100g > 100) data.SugarPer100g = null;
        if (data.FiberPer100g > 100) data.FiberPer100g = null;

        // Sódio: 0-5000mg
        if (data.SodiumPer100g > 5000)
        {
            data.Warnings.Add($"Sódio muito alto ({data.SodiumPer100g:F0}mg), possível erro OCR");
            data.SodiumPer100g = null;
        }
    }

    /// <summary>
    /// Detecta e corrige erros comuns de OCR
    /// </summary>
    private static void DetectOcrErrors(NutritionData data)
    {
        // ERRO COMUM 1: Carboidratos muito baixo para calorias altas
        // Ex: 519 kcal com 14g carbs → provável erro, deveria ser 46g
        if (data.CaloriesPer100g > 400 && data.CarbsPer100g < 20)
        {
            // Tentar inferir carbs: (calorias - 4*protein - 9*fat) / 4
            if (data.ProteinPer100g.HasValue && data.FatPer100g.HasValue)
            {
                var inferredCarbs = (data.CaloriesPer100g.Value - 
                                    (data.ProteinPer100g.Value * 4) - 
                                    (data.FatPer100g.Value * 9)) / 4;

                if (inferredCarbs > 0 && inferredCarbs <= 100)
                {
                    data.Warnings.Add($"Carboidratos corrigidos de {data.CarbsPer100g:F1}g para {inferredCarbs:F1}g (inferido por calorias)");
                    data.CarbsPer100g = Math.Round(inferredCarbs, 1);
                }
            }
        }

        // ERRO COMUM 2: Sódio em gramas ao invés de miligramas
        // Ex: 0.095g lido como 0.095mg → deveria ser 95mg
        if (data.SodiumPer100g.HasValue && data.SodiumPer100g.Value < 1 && data.SodiumPer100g.Value > 0)
        {
            var correctedSodium = data.SodiumPer100g.Value * 1000;
            data.Warnings.Add($"Sódio corrigido de {data.SodiumPer100g:F3}mg para {correctedSodium:F0}mg (provável conversão g→mg)");
            data.SodiumPer100g = Math.Round(correctedSodium, 0);
        }
    }

    /// <summary>
    /// Calcula score de confiança baseado em quantidade e consistência
    /// </summary>
    public static int CalculateConfidenceScore(NutritionData data)
    {
        int score = 0;

        // CRITÉRIO 1: Quantidade de nutrientes extraídos (0-50 pontos)
        score += data.ExtractedNutrientsCount * 5; // 10 nutrientes * 5 = 50 pontos

        // CRITÉRIO 2: Presença de valores críticos (0-30 pontos)
        if (data.CaloriesPer100g.HasValue) score += 10;
        if (data.CarbsPer100g.HasValue) score += 5;
        if (data.ProteinPer100g.HasValue) score += 5;
        if (data.FatPer100g.HasValue) score += 5;
        if (data.SodiumPer100g.HasValue) score += 5;

        // CRITÉRIO 3: Consistência calórica (0-20 pontos)
        if (data.CaloriesPer100g.HasValue && 
            data.CarbsPer100g.HasValue && 
            data.ProteinPer100g.HasValue && 
            data.FatPer100g.HasValue)
        {
            var expected = 
                (data.CarbsPer100g.Value * 4) +
                (data.ProteinPer100g.Value * 4) +
                (data.FatPer100g.Value * 9);

            var delta = Math.Abs(data.CaloriesPer100g.Value - expected) / expected;

            if (delta <= 0.10) score += 20; // Excelente (≤10%)
            else if (delta <= 0.20) score += 15; // Bom (≤20%)
            else if (delta <= 0.30) score += 10; // Aceitável (≤30%)
            else score += 0; // Ruim (>30%)
        }

        // PENALIDADE: Warnings reduzem confiança
        score -= data.Warnings.Count * 5;

        return Math.Clamp(score, 0, 100);
    }
}
