namespace LabelWise.Domain.Enums;

/// <summary>
/// Classificação de processamento alimentar baseada em NOVA.
/// </summary>
public enum ProcessingLevel
{
    /// <summary>
    /// Desconhecido ou não classificado
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Alimentos naturais ou minimamente processados (NOVA 1)
    /// Exemplo: frutas, vegetais, grãos, carnes frescas, arroz, feijão
    /// </summary>
    MinimallyProcessed = 1,

    /// <summary>
    /// Ingredientes culinários processados (NOVA 2)
    /// Exemplo: óleos, manteiga, açúcar, sal
    /// </summary>
    ProcessedCulinaryIngredients = 2,

    /// <summary>
    /// Alimentos processados (NOVA 3)
    /// Exemplo: conservas, queijos, pães simples
    /// </summary>
    Processed = 3,

    /// <summary>
    /// Alimentos ultraprocessados (NOVA 4)
    /// Exemplo: refrigerantes, salgadinhos, biscoitos recheados
    /// </summary>
    UltraProcessed = 4
}
