using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// Gera um resumo textual em português do perfil nutricional.
/// Baseado em sinais nutricionais — sem heurísticas por produto específico.
/// </summary>
public interface ISummaryService
{
    /// <summary>
    /// Retorna uma frase descritiva do perfil nutricional do produto.
    /// </summary>
    string Generate(NutritionProfile profile);
}
