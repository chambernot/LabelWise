using System.Threading.Tasks;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// Serviço para fazer merge inteligente entre dados reais e fallback por categoria.
/// </summary>
public interface INutritionDataMergeService
{
    /// <summary>
    /// Faz merge entre dados extraídos pela IA e perfil da categoria.
    /// REGRA: Nunca sobrescrever dados reais com estimativas.
    /// </summary>
    /// <param name="aiExtractedData">Dados extraídos pela IA (podem estar incompletos).</param>
    /// <param name="normalizedCategoryCode">Código da categoria normalizada.</param>
    /// <param name="analysisMode">Modo de análise.</param>
    /// <returns>Resultado do merge com dados consolidados.</returns>
    Task<NutritionDataMergeResult> MergeAsync(
        EstimatedNutritionProfileDto? aiExtractedData,
        string? normalizedCategoryCode,
        AnalysisMode analysisMode);
}
