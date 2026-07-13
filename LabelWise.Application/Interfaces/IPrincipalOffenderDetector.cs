using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// Detecta o principal ofensor nutricional de um produto.
/// </summary>
public interface IPrincipalOffenderDetector
{
    /// <summary>
    /// Detecta o principal ofensor (açúcar, gordura, sódio, etc) baseado no perfil nutricional.
    /// </summary>
    /// <param name="profile">Perfil nutricional consolidado.</param>
    /// <returns>Resultado com o principal ofensor identificado.</returns>
    PrincipalOffenderResult Detect(EstimatedNutritionProfileDto profile);
}
