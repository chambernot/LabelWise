using System.Collections.Generic;
using System.Threading.Tasks;
using LabelWise.Domain.Entities;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// Repositório para mapeamentos de categorias (aliases).
/// </summary>
public interface ICategoryMappingRepository
{
    /// <summary>
    /// Busca categoria normalizada para um nome detectado pela IA.
    /// </summary>
    Task<CategoryMapping?> GetMappingAsync(string rawCategoryName);
    
    /// <summary>
    /// Busca mapeamentos fuzzy (aproximados) para um nome.
    /// </summary>
    Task<List<CategoryMapping>> GetFuzzyMappingsAsync(string rawCategoryName, double minConfidence = 0.7);
    
    /// <summary>
    /// Obtém todos os mapeamentos ativos.
    /// </summary>
    Task<List<CategoryMapping>> GetAllActiveAsync();
}
