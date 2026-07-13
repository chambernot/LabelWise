using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// Calcula o nível de confiança da extração nutricional com base
/// na completude dos campos e na presença de warnings de correção.
/// </summary>
public interface IConfidenceService
{
    /// <summary>
    /// Retorna "alta", "media" ou "baixa".
    /// </summary>
    string Calculate(NutritionProfile profile, IReadOnlyList<string> warnings);
}
