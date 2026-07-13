using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LabelWise.Application.Validation;

/// <summary>
/// Aplica regras de validação e normalização determinísticas sobre a resposta da análise nutricional da IA.
/// </summary>
public static class NutritionAnalysisValidator
{
    /// <summary>
    /// Método principal que executa todas as correções e normalizações.
    /// </summary>
    public static void Apply(NutritionAnalysisResponseDto response)
    {
        if (response == null) return;

        EnsureNotNulls(response);
        NormalizeBrand(response);
        NormalizeProductName(response);
        NormalizeClaims(response);
        FixClassification(response);
        NormalizeConfidence(response);
        FixNutritionBasis(response);
        FixSummaryByCategory(response); // Executar por último para usar dados já normalizados
    }

    /// <summary>
    /// Garante que propriedades de coleção e objetos complexos não sejam nulos.
    /// </summary>
    private static void EnsureNotNulls(NutritionAnalysisResponseDto response)
    {
        response.VisibleClaims ??= new List<string>();
        response.Warnings ??= new List<string>();
        response.ConfidenceDetails ??= new ConfidenceDetailsDto();
        response.Classification ??= new ProductClassificationDto();
        response.EstimatedNutritionProfile ??= new EstimatedNutritionProfileDto();

        var defaultReason = "Classificação não pôde ser determinada a partir da imagem.";
        response.Classification.Diabetic ??= new HealthProfileResult { Status = "indeterminado", Reason = defaultReason };
        response.Classification.BloodPressure ??= new HealthProfileResult { Status = "indeterminado", Reason = defaultReason };
        response.Classification.WeightLoss ??= new HealthProfileResult { Status = "indeterminado", Reason = defaultReason };
        response.Classification.MuscleGain ??= new HealthProfileResult { Status = "indeterminado", Reason = defaultReason };
    }

    /// <summary>
    /// Normaliza o nome da marca para evitar valores genéricos ou incorretos.
    /// </summary>
    private static void NormalizeBrand(NutritionAnalysisResponseDto response)
    {
        var invalidBrandTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Creapure", "Marca não encontrada", "N/A", "Genérico", "Alimento", "Bebida"
        };

        if (string.IsNullOrWhiteSpace(response.Brand) || invalidBrandTerms.Contains(response.Brand) || response.Brand.Length < 2)
        {
            response.Brand = "Marca não identificada";
        }
    }

    /// <summary>
    /// Normaliza o nome do produto para ser mais descritivo e evitar repetições.
    /// </summary>
    private static void NormalizeProductName(NutritionAnalysisResponseDto response)
    {
        var productName = response.ProductName;

        var isGeneric = IsGenericProductName(productName, response.Category);

        var isRepetitive = !isGeneric &&
                           !string.IsNullOrWhiteSpace(response.Brand) &&
                           productName!.Equals(response.Brand, StringComparison.OrdinalIgnoreCase);

        if (isGeneric || isRepetitive)
        {
            response.ProductName = BuildProductNameFromCategory(response.Category, response.VisibleClaims);
        }
    }

    /// <summary>
    /// Normaliza a lista de alegações visíveis para remover duplicatas e padronizar o formato.
    /// </summary>
    private static void NormalizeClaims(NutritionAnalysisResponseDto response)
    {
        if (response.VisibleClaims == null || !response.VisibleClaims.Any())
        {
            response.VisibleClaims = new List<string>();
            return;
        }

        var cleanedClaims = response.VisibleClaims
            .Select(c => c?.Trim('"', '\'', '•', '-').Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c) && c.Length > 2)
            .Select(c => char.ToUpperInvariant(c[0]) + c[1..])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        response.VisibleClaims = cleanedClaims;
    }

    /// <summary>
    /// Aplica regras de negócio para corrigir a classificação de perfis de consumidor.
    /// </summary>
    private static void FixClassification(NutritionAnalysisResponseDto response)
    {
        var categoryLower = response.Category?.ToLowerInvariant() ?? string.Empty;

        // Regra para carboidratos
        if (categoryLower.Contains("arroz") || categoryLower.Contains("pão") || categoryLower.Contains("massa") || categoryLower.Contains("macarrão"))
        {
            ApplyClassificationRule(response.Classification.Diabetic, "consumo_moderado", "Fonte de carboidratos que impacta a glicemia.");
            ApplyClassificationRule(response.Classification.WeightLoss, "consumo_moderado", "Fonte de calorias que deve ser controlada em dietas de emagrecimento.");
        }

        // Regra para ultraprocessados
        if (categoryLower.Contains("biscoito") || categoryLower.Contains("bolacha") || categoryLower.Contains("achocolatado") || categoryLower.Contains("refrigerante"))
        {
            ApplyClassificationRule(response.Classification.WeightLoss, "nao_recomendado", "Produto ultraprocessado, geralmente rico em açúcares e gorduras.");
        }

        // Regra para laticínios cremosos
        if (categoryLower.Contains("requeijão") || categoryLower.Contains("cream cheese"))
        {
            ApplyClassificationRule(response.Classification.BloodPressure, "consumo_moderado", "Pode conter alto teor de sódio, o que exige moderação para controle da pressão arterial.");
        }

        // Ajuste específico para MuscleGain
        FixMuscleGain(response);
    }

    /// <summary>
    /// Arredonda os valores de confiança para 2 casas decimais para evitar problemas de float.
    /// </summary>
    private static void NormalizeConfidence(NutritionAnalysisResponseDto response)
    {
        if (response.ConfidenceDetails == null) return;

        response.ConfidenceDetails.ProductIdentification = Math.Round(response.ConfidenceDetails.ProductIdentification, 2);
        response.ConfidenceDetails.VisibleClaimsExtraction = Math.Round(response.ConfidenceDetails.VisibleClaimsExtraction, 2);
        response.ConfidenceDetails.EstimatedNutritionProfile = Math.Round(response.ConfidenceDetails.EstimatedNutritionProfile, 2);
        response.ConfidenceDetails.Classification = Math.Round(response.ConfidenceDetails.Classification, 2);
    }

    /// <summary>
    /// Garante que o resumo da análise seja informativo e contextualizado com base na categoria.
    /// </summary>
    private static void FixSummaryByCategory(NutritionAnalysisResponseDto response)
    {
        var productName = response.ProductName ?? "Produto";
        var isGenericSummary = string.IsNullOrWhiteSpace(response.Summary)
            || response.Summary.Length < productName.Length + 12
            || response.Summary.Equals(productName, StringComparison.OrdinalIgnoreCase);

        if (isGenericSummary)
        {
            var qualityHint = BuildCategoryQualityHint(response.Category, response.VisibleClaims);
            var analysisMethod = response.AnalysisMode == AnalysisMode.FullNutritionLabel
                ? "com informações nutricionais detalhadas identificadas na embalagem"
                : "com leitura estimada da embalagem frontal e da categoria";

            response.Summary = $"{productName} analisado {analysisMethod}, com {qualityHint}.";
        }
    }

    /// <summary>
    /// Corrige a base do perfil nutricional se nenhum valor foi extraído.
    /// </summary>
    private static void FixNutritionBasis(NutritionAnalysisResponseDto response)
    {
        var profile = response.EstimatedNutritionProfile;
        if (profile == null) return;

        bool allValuesAreNull = profile.CaloriesPer100g == null &&
                                profile.EstimatedPackageCalories == null &&
                                profile.EstimatedSugarPer100g == null &&
                                profile.EstimatedProteinPer100g == null &&
                                profile.EstimatedSodiumPer100g == null &&
                                profile.EstimatedFiberPer100g == null &&
                                profile.EstimatedFatPer100g == null;

        if (allValuesAreNull)
        {
            profile.Basis = "Não há tabela nutricional visível; valores não foram extraídos.";
        }
    }

    /// <summary>
    /// Ajusta a classificação de "MuscleGain" com base na categoria do produto.
    /// </summary>
    private static void FixMuscleGain(NutritionAnalysisResponseDto response)
    {
        var categoryLower = response.Category?.ToLowerInvariant() ?? string.Empty;
        var classification = response.Classification.MuscleGain;

        if (categoryLower.Contains("queijo") || categoryLower.Contains("achocolatado"))
        {
            ApplyClassificationRule(classification, "fraco", "Baixo teor de proteína ou perfil nutricional não ideal para hipertrofia.");
        }
        else if (categoryLower.Contains("arroz"))
        {
            ApplyClassificationRule(classification, "consumo_moderado", "Fonte de energia para treinos, mas não é uma fonte primária de proteína.");
        }
        else if (categoryLower.Contains("creatina"))
        {
            ApplyClassificationRule(classification, "adequado", "Suplemento com eficácia comprovada para ganho de força e massa muscular.");
        }
    }

    #region Private Helpers

    private static string BuildProductNameFromCategory(string? category, List<string> claims)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return "Produto alimentício";
        }

        var categoryLower = category.ToLowerInvariant();

        if (categoryLower.Contains("arroz"))
        {
            var riceType = "Arroz Branco";
            if (claims.Any(c => c.Contains("integral", StringComparison.OrdinalIgnoreCase))) riceType = "Arroz Integral";
            else if (claims.Any(c => c.Contains("parboilizado", StringComparison.OrdinalIgnoreCase))) riceType = "Arroz Parboilizado";

            var grade = claims.FirstOrDefault(c => c.Contains("tipo 1", StringComparison.OrdinalIgnoreCase)) ??
                        claims.FirstOrDefault(c => c.Contains("tipo 2", StringComparison.OrdinalIgnoreCase)) ?? "";

            return $"{riceType} {grade}".Trim();
        }

        if (categoryLower.Contains("biscoito") || categoryLower.Contains("bolacha"))
        {
            if (categoryLower.Contains("rechead", StringComparison.OrdinalIgnoreCase)) return "Biscoito Recheado";
            if (categoryLower.Contains("cream cracker", StringComparison.OrdinalIgnoreCase)) return "Biscoito Cream Cracker";
            if (categoryLower.Contains("integral", StringComparison.OrdinalIgnoreCase)) return "Biscoito Integral";
            if (claims.Any(c => c.Contains("rechead", StringComparison.OrdinalIgnoreCase))) return "Biscoito Recheado";
            if (claims.Any(c => c.Contains("cream cracker", StringComparison.OrdinalIgnoreCase))) return "Biscoito Cream Cracker";
            if (claims.Any(c => c.Contains("integral", StringComparison.OrdinalIgnoreCase))) return "Biscoito Integral";
            return "Biscoito";
        }

        if (categoryLower.Contains("achocolatado"))
        {
            var fortified = claims.Any(c => c.Contains("vitamina", StringComparison.OrdinalIgnoreCase) ||
                                            c.Contains("fortificado", StringComparison.OrdinalIgnoreCase));
            return fortified ? "Achocolatado em Pó Fortificado" : "Achocolatado em Pó";
        }

        if (categoryLower.Contains("pão"))
        {
            if (claims.Any(c => c.Contains("forma", StringComparison.OrdinalIgnoreCase))) return "Pão de Forma";
            if (claims.Any(c => c.Contains("integral", StringComparison.OrdinalIgnoreCase))) return "Pão Integral";
            return "Pão";
        }

        if (categoryLower.Contains("iogurte"))
        {
            if (claims.Any(c => c.Contains("proteic", StringComparison.OrdinalIgnoreCase) ||
                                c.Contains("protein", StringComparison.OrdinalIgnoreCase)))
            {
                return "Iogurte Proteico";
            }

            return "Iogurte";
        }

        if (categoryLower.Contains("queijo") || categoryLower.Contains("requeijão") || categoryLower.Contains("cream cheese"))
        {
            if (categoryLower.Contains("requeijão")) return "Requeijão Cremoso";
            if (categoryLower.Contains("cream cheese")) return "Cream Cheese";
            if (categoryLower.Contains("minas")) return "Queijo Minas";
            return claims.Any(c => c.Contains("light", StringComparison.OrdinalIgnoreCase)) ? "Queijo Light" : "Queijo";
        }

        if (categoryLower.Contains("cereal"))
        {
            return claims.Any(c => c.Contains("vitamina", StringComparison.OrdinalIgnoreCase) ||
                                   c.Contains("fortificado", StringComparison.OrdinalIgnoreCase))
                ? "Cereal Matinal Fortificado"
                : "Cereal Matinal";
        }

        if (categoryLower.Contains("creatina")) return "Suplemento de Creatina Monohidratada";
        if (categoryLower.Contains("whey protein")) return "Suplemento Proteico (Whey Protein)";

        return CapitalizeCategory(category);
    }

    private static bool IsGenericProductName(string? productName, string? category)
    {
        if (string.IsNullOrWhiteSpace(productName) || productName.Length < 4)
        {
            return true;
        }

        var normalizedProductName = NormalizeComparableText(productName);
        var normalizedCategory = NormalizeComparableText(category);

        var genericTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "suplemento alimentar",
            "produto alimenticio",
            "produto alimentício",
            "alimento",
            "biscoito",
            "bolacha",
            "pao",
            "pão",
            "arroz",
            "queijo",
            "iogurte",
            "cereal",
            "macarrao",
            "macarrão",
            "massa",
            "chocolate"
        };

        if (genericTerms.Contains(normalizedProductName))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(normalizedCategory))
        {
            if (string.Equals(normalizedProductName, normalizedCategory, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!normalizedProductName.Contains(' ') && normalizedCategory.Contains(normalizedProductName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildCategoryQualityHint(string? category, List<string>? claims)
    {
        var categoryLower = category?.ToLowerInvariant() ?? string.Empty;
        var visibleClaims = claims ?? new List<string>();

        if (categoryLower.Contains("biscoito") || categoryLower.Contains("bolacha"))
        {
            return "perfil nutricional geralmente rico em açúcar, gordura e calorias";
        }

        if (categoryLower.Contains("achocolatado"))
        {
            return "perfil com tendência a alto teor de açúcar e baixa densidade proteica";
        }

        if (categoryLower.Contains("queijo") || categoryLower.Contains("requeijão") || categoryLower.Contains("cream"))
        {
            return "perfil com proteína relevante, mas possível concentração de gordura e sódio";
        }

        if (categoryLower.Contains("arroz"))
        {
            return visibleClaims.Any(c => c.Contains("integral", StringComparison.OrdinalIgnoreCase))
                ? "predomínio de carboidratos com melhor potencial de fibras"
                : "predomínio de carboidratos e baixa densidade proteica";
        }

        if (categoryLower.Contains("iogurte"))
        {
            return visibleClaims.Any(c => c.Contains("proteic", StringComparison.OrdinalIgnoreCase) ||
                                          c.Contains("protein", StringComparison.OrdinalIgnoreCase))
                ? "boa densidade proteica para a categoria"
                : "equilíbrio moderado entre calorias, proteína e açúcar";
        }

        return "leitura nutricional geral ainda dependente do contexto da categoria";
    }

    private static string NormalizeComparableText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Trim()
            .Replace("á", "a", StringComparison.OrdinalIgnoreCase)
            .Replace("à", "a", StringComparison.OrdinalIgnoreCase)
            .Replace("ã", "a", StringComparison.OrdinalIgnoreCase)
            .Replace("â", "a", StringComparison.OrdinalIgnoreCase)
            .Replace("é", "e", StringComparison.OrdinalIgnoreCase)
            .Replace("ê", "e", StringComparison.OrdinalIgnoreCase)
            .Replace("í", "i", StringComparison.OrdinalIgnoreCase)
            .Replace("ó", "o", StringComparison.OrdinalIgnoreCase)
            .Replace("ô", "o", StringComparison.OrdinalIgnoreCase)
            .Replace("õ", "o", StringComparison.OrdinalIgnoreCase)
            .Replace("ú", "u", StringComparison.OrdinalIgnoreCase)
            .Replace("ç", "c", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();
    }

    private static string CapitalizeCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category)) return "Produto alimentício";

        var words = category.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpperInvariant(words[i][0]) + (words[i].Length > 1 ? words[i].Substring(1).ToLowerInvariant() : "");
            }
        }
        return string.Join(" ", words);
    }

    private static void ApplyClassificationRule(HealthProfileResult classification, string status, string reason)
    {
        if (classification.Status is "indeterminado" or null)
        {
            classification.Status = status;
            classification.Reason = reason;
        }
    }

    #endregion
}
