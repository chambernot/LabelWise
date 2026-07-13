using System.Collections.Generic;
using System.Threading.Tasks;
using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// Serviço para normalizar categorias detectadas pela IA.
/// </summary>
public interface ICategoryNormalizationService
{
    /// <summary>
    /// Normaliza uma categoria detectada para uma categoria do banco de dados.
    /// </summary>
    /// <param name="detectedCategory">Categoria detectada pela IA.</param>
    /// <param name="productName">Nome do produto.</param>
    /// <param name="visibleClaims">Claims visíveis da embalagem.</param>
    /// <param name="brand">Marca identificada.</param>
    /// <returns>Resultado da normalização.</returns>
    Task<CategoryNormalizationResult> NormalizeAsync(
        string? detectedCategory,
        string? productName,
        IEnumerable<string>? visibleClaims = null,
        string? brand = null);
}
