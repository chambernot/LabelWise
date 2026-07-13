using System.Collections.Generic;
using System.Threading.Tasks;
using LabelWise.Domain.Entities;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// Repositório para acesso aos perfis nutricionais por categoria.
/// </summary>
public interface ICategoryNutritionProfileRepository
{
    /// <summary>
    /// Obtém o perfil nutricional para uma categoria específica.
    /// </summary>
    Task<CategoryNutritionProfile?> GetByCategoryCodeAsync(string categoryCode);
    
    /// <summary>
    /// Obtém todos os perfis nutricionais ativos.
    /// </summary>
    Task<List<CategoryNutritionProfile>> GetAllActiveAsync();
    
    /// <summary>
    /// Obtém perfis nutricionais por códigos de categorias.
    /// </summary>
    Task<List<CategoryNutritionProfile>> GetByCategoryCodesAsync(IEnumerable<string> categoryCodes);
}
