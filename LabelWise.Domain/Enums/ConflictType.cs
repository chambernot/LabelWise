namespace LabelWise.Domain.Enums;

/// <summary>
/// Tipo de conflito detectado na análise.
/// </summary>
public enum ConflictType
{
    /// <summary>
    /// Sem conflito detectado
    /// </summary>
    None,

    /// <summary>
    /// Claim regulatório contradiz ingrediente detectado
    /// Exemplo: "SEM GLÚTEN" mas lista contém "farinha de trigo"
    /// </summary>
    ClaimIngredientMismatch,

    /// <summary>
    /// Claims regulatórios contraditórios
    /// Exemplo: "CONTÉM LEITE" e "SEM LACTOSE"
    /// </summary>
    ClaimConflict,

    /// <summary>
    /// Dados nutricionais inconsistentes com ingredientes
    /// Exemplo: "0g açúcar" mas ingrediente contém "açúcar"
    /// </summary>
    NutritionIngredientMismatch,

    /// <summary>
    /// Múltiplas fontes de dados contraditórias
    /// Exemplo: OCR vs Vision AI com resultados opostos
    /// </summary>
    MultiSourceConflict,

    /// <summary>
    /// Dados nutricionais impossíveis ou inválidos
    /// Exemplo: soma de macronutrientes > 100g por 100g
    /// </summary>
    InvalidNutritionData
}
