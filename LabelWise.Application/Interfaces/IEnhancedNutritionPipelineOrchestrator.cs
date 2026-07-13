using System.Threading.Tasks;
using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// Orquestrador principal do pipeline de análise nutricional enriquecido com fallback inteligente.
/// </summary>
public interface IEnhancedNutritionPipelineOrchestrator
{
    /// <summary>
    /// Analisa uma imagem de produto e retorna análise nutricional completa com metadata.
    /// Pipeline: IA → Normalização → Merge → Offender → Score → Summary.
    /// </summary>
    /// <param name="imageData">Dados binários da imagem.</param>
    /// <param name="additionalContext">Contexto adicional opcional.</param>
    /// <returns>Resultado enriquecido com metadata de origem dos dados.</returns>
    Task<EnhancedNutritionAnalysisResult> AnalyzeAsync(byte[] imageData, string? additionalContext = null);
}
