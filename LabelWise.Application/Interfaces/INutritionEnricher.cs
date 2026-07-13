using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// Enriquece o perfil validado com fallback por categoria, nível de processamento e confiança.
///
/// Responsabilidades:
///   - Aplicar fallback (médias por categoria) quando dados são insuficientes
///   - Determinar nível de processamento (in_natura / processado / ultraprocessado)
///   - Calcular confiança final (alta / media / baixa)
///
/// NÃO valida, NÃO sanitiza, NÃO calcula score.
/// </summary>
public interface INutritionEnricher
{
    NutritionEnrichedData Enrich(
        NutritionSanitizationResult validated,
        string? category,
        AnalysisMode analysisMode,
        IReadOnlyList<string>? ingredients);
}
