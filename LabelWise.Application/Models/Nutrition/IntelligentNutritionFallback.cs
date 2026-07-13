using System;
using System.Collections.Generic;
using System.Linq;
using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Models.Nutrition;

/// <summary>
/// Motor de fallback inteligente para dados nutricionais.
/// Usa perfis tipológicos para preencher dados faltantes de forma consistente.
/// </summary>
public class IntelligentNutritionFallback
{
    /// <summary>
    /// Aplica fallback inteligente em um perfil nutricional.
    /// </summary>
    /// <param name="partial">Dados nutricionais parciais (se houver).</param>
    /// <param name="category">Categoria detectada do produto.</param>
    /// <param name="visibleClaims">Alegações visíveis no rótulo.</param>
    /// <returns>Perfil nutricional completo com fallback aplicado.</returns>
    public static FallbackResult ApplyFallback(
        EstimatedNutritionProfileDto? partial,
        string? category,
        List<string>? visibleClaims)
    {
        // 1. Normalizar categoria para tipologia
        var partialData = CreatePartialData(partial);
        var typology = CategoryNormalizer.Normalize(category, visibleClaims, partialData);

        // 2. Obter perfil tipológico
        var typologicalProfile = TypologicalNutritionCatalog.GetProfile(typology);
        
        if (typologicalProfile == null)
        {
            return CreateUnknownFallback(partial);
        }

        // 3. Aplicar fallback inteligente
        var result = new EstimatedNutritionProfileDto();
        var fallbackSources = new Dictionary<string, string>();

        // Calorias
        if (HasValidValue(partial?.CaloriesPer100g))
        {
            result.CaloriesPer100g = partial!.CaloriesPer100g;
            fallbackSources["Calorias"] = "Dados reais extraídos";
        }
        else
        {
            result.CaloriesPer100g = typologicalProfile.Calories.Typical;
            fallbackSources["Calorias"] = $"Estimado por tipologia ({typology})";
        }

        // Açúcar
        if (HasValidValue(partial?.EstimatedSugarPer100g))
        {
            result.EstimatedSugarPer100g = partial!.EstimatedSugarPer100g;
            fallbackSources["Açúcar"] = "Dados reais extraídos";
        }
        else
        {
            result.EstimatedSugarPer100g = typologicalProfile.Sugar.Typical;
            fallbackSources["Açúcar"] = $"Estimado por tipologia ({typology})";
        }

        // Proteína
        if (HasValidValue(partial?.EstimatedProteinPer100g))
        {
            result.EstimatedProteinPer100g = partial!.EstimatedProteinPer100g;
            fallbackSources["Proteína"] = "Dados reais extraídos";
        }
        else
        {
            result.EstimatedProteinPer100g = typologicalProfile.Protein.Typical;
            fallbackSources["Proteína"] = $"Estimado por tipologia ({typology})";
        }

        // Gordura
        if (HasValidValue(partial?.EstimatedFatPer100g))
        {
            result.EstimatedFatPer100g = partial!.EstimatedFatPer100g;
            fallbackSources["Gordura"] = "Dados reais extraídos";
        }
        else
        {
            result.EstimatedFatPer100g = typologicalProfile.Fat.Typical;
            fallbackSources["Gordura"] = $"Estimado por tipologia ({typology})";
        }

        // Sódio
        if (HasValidValue(partial?.EstimatedSodiumPer100g))
        {
            result.EstimatedSodiumPer100g = partial!.EstimatedSodiumPer100g;
            fallbackSources["Sódio"] = "Dados reais extraídos";
        }
        else
        {
            result.EstimatedSodiumPer100g = typologicalProfile.Sodium.Typical;
            fallbackSources["Sódio"] = $"Estimado por tipologia ({typology})";
        }

        // Fibra
        if (HasValidValue(partial?.EstimatedFiberPer100g))
        {
            result.EstimatedFiberPer100g = partial!.EstimatedFiberPer100g;
            fallbackSources["Fibra"] = "Dados reais extraídos";
        }
        else
        {
            result.EstimatedFiberPer100g = typologicalProfile.Fiber.Typical;
            fallbackSources["Fibra"] = $"Estimado por tipologia ({typology})";
        }

        // Validar coerência nutricional
        ValidateNutritionalCoherence(result, typologicalProfile);

        // Construir basis descritivo
        result.Basis = BuildBasis(fallbackSources, typology, typologicalProfile.Confidence);

        // Calcular confiança geral
        var confidence = CalculateConfidence(partial, fallbackSources.Count, typologicalProfile.Confidence);

        return new FallbackResult
        {
            Profile = result,
            Typology = typology,
            Confidence = confidence,
            IsFullyEstimated = fallbackSources.All(s => s.Value.Contains("Estimado")),
            IsPartiallyEstimated = fallbackSources.Any(s => s.Value.Contains("Estimado")),
            FallbackSources = fallbackSources
        };
    }

    /// <summary>
    /// Valida coerência nutricional e ajusta valores inconsistentes.
    /// </summary>
    private static void ValidateNutritionalCoherence(
        EstimatedNutritionProfileDto result,
        TypologicalNutritionProfile profile)
    {
        // Ajustar valores fora do range esperado
        result.CaloriesPer100g = profile.Calories.ClampToRange(result.CaloriesPer100g ?? 0);
        result.EstimatedSugarPer100g = profile.Sugar.ClampToRange(result.EstimatedSugarPer100g ?? 0);
        result.EstimatedProteinPer100g = profile.Protein.ClampToRange(result.EstimatedProteinPer100g ?? 0);
        result.EstimatedFatPer100g = profile.Fat.ClampToRange(result.EstimatedFatPer100g ?? 0);
        result.EstimatedSodiumPer100g = profile.Sodium.ClampToRange(result.EstimatedSodiumPer100g ?? 0);
        result.EstimatedFiberPer100g = profile.Fiber.ClampToRange(result.EstimatedFiberPer100g ?? 0);

        // Validar que açúcar não excede carboidratos totais (se estimado)
        var sugar = result.EstimatedSugarPer100g ?? 0;
        var carbs = profile.Carbohydrates.Typical;
        if (sugar > carbs)
        {
            result.EstimatedSugarPer100g = Math.Min(sugar, carbs * 0.9); // 90% dos carbos no máximo
        }

        // Validar que proteína + gordura não ultrapassam valor calórico
        var protein = result.EstimatedProteinPer100g ?? 0;
        var fat = result.EstimatedFatPer100g ?? 0;
        var estimatedCalories = (protein * 4) + (fat * 9) + (carbs * 4);
        var actualCalories = result.CaloriesPer100g ?? 0;

        // Se há grande discrepância, ajustar calorias
        if (Math.Abs(estimatedCalories - actualCalories) > 100)
        {
            result.CaloriesPer100g = Math.Round(estimatedCalories);
        }
    }

    /// <summary>
    /// Constrói o texto de basis descritivo.
    /// </summary>
    private static string BuildBasis(
        Dictionary<string, string> sources,
        FoodTypology typology,
        double typologyConfidence)
    {
        var realCount = sources.Count(s => s.Value.Contains("real"));
        var estimatedCount = sources.Count(s => s.Value.Contains("Estimado"));
        var total = sources.Count;

        if (realCount == total)
        {
            return "Dados extraídos da tabela nutricional presente no rótulo";
        }
        else if (estimatedCount == total)
        {
            var confidenceText = typologyConfidence >= 0.8 ? "alto grau de confiança" :
                                 typologyConfidence >= 0.6 ? "confiança moderada" :
                                 "confiança limitada";
            
            return $"Valores estimados por tipologia alimentar ({typology}) com {confidenceText}. " +
                   "Para análise precisa, capture a tabela nutricional.";
        }
        else
        {
            var realNutrients = sources.Where(s => s.Value.Contains("real")).Select(s => s.Key.ToLower());
            var estimatedNutrients = sources.Where(s => s.Value.Contains("Estimado")).Select(s => s.Key.ToLower());

            var realText = string.Join(", ", realNutrients);
            var estimatedText = string.Join(", ", estimatedNutrients);

            return $"Leitura parcial da tabela nutricional: {realText} extraídos, {estimatedText} estimados por tipologia.";
        }
    }

    /// <summary>
    /// Calcula a confiança geral do perfil nutricional.
    /// </summary>
    private static double CalculateConfidence(
        EstimatedNutritionProfileDto? partial,
        int totalNutrients,
        double typologyConfidence)
    {
        var realFields = 0;
        
        if (HasValidValue(partial?.CaloriesPer100g)) realFields++;
        if (HasValidValue(partial?.EstimatedSugarPer100g)) realFields++;
        if (HasValidValue(partial?.EstimatedProteinPer100g)) realFields++;
        if (HasValidValue(partial?.EstimatedFatPer100g)) realFields++;
        if (HasValidValue(partial?.EstimatedSodiumPer100g)) realFields++;
        if (HasValidValue(partial?.EstimatedFiberPer100g)) realFields++;

        // Confiança é proporcional aos dados reais + confiança da tipologia
        var realDataWeight = 0.7;
        var typologyWeight = 0.3;

        var realConfidence = totalNutrients > 0 ? (double)realFields / totalNutrients : 0.0;
        var estimatedConfidence = typologyConfidence;

        return (realConfidence * realDataWeight) + (estimatedConfidence * typologyWeight);
    }

    private static bool HasValidValue(double? value)
    {
        return value.HasValue && value.Value >= 0;
    }

    private static PartialNutritionData? CreatePartialData(EstimatedNutritionProfileDto? dto)
    {
        if (dto == null) return null;

        return new PartialNutritionData
        {
            Protein = dto.EstimatedProteinPer100g,
            Fat = dto.EstimatedFatPer100g,
            Sugar = dto.EstimatedSugarPer100g,
            Sodium = dto.EstimatedSodiumPer100g,
            Fiber = dto.EstimatedFiberPer100g
        };
    }

    private static FallbackResult CreateUnknownFallback(EstimatedNutritionProfileDto? partial)
    {
        var result = partial ?? new EstimatedNutritionProfileDto
        {
            CaloriesPer100g = 0,
            EstimatedSugarPer100g = 0,
            EstimatedProteinPer100g = 0,
            EstimatedFatPer100g = 0,
            EstimatedSodiumPer100g = 0,
            EstimatedFiberPer100g = 0,
            Basis = "Categoria não identificada. Não foi possível estimar valores nutricionais."
        };

        return new FallbackResult
        {
            Profile = result,
            Typology = FoodTypology.Unknown,
            Confidence = 0.1,
            IsFullyEstimated = true,
            IsPartiallyEstimated = false,
            FallbackSources = new Dictionary<string, string>()
        };
    }
}

/// <summary>
/// Resultado do processo de fallback inteligente.
/// </summary>
public class FallbackResult
{
    /// <summary>
    /// Perfil nutricional completo (com fallback aplicado).
    /// </summary>
    public EstimatedNutritionProfileDto Profile { get; set; } = new();

    /// <summary>
    /// Tipologia alimentar identificada.
    /// </summary>
    public FoodTypology Typology { get; set; }

    /// <summary>
    /// Confiança geral do perfil (0.0 a 1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Indica se todos os valores são estimados (sem dados reais).
    /// </summary>
    public bool IsFullyEstimated { get; set; }

    /// <summary>
    /// Indica se alguns valores são estimados (dados parciais).
    /// </summary>
    public bool IsPartiallyEstimated { get; set; }

    /// <summary>
    /// Fonte de cada nutriente (real ou estimado).
    /// </summary>
    public Dictionary<string, string> FallbackSources { get; set; } = new();
}
