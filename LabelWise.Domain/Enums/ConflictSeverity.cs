namespace LabelWise.Domain.Enums;

/// <summary>
/// Severidade do conflito detectado.
/// </summary>
public enum ConflictSeverity
{
    /// <summary>
    /// Sem conflito
    /// </summary>
    None,

    /// <summary>
    /// Conflito menor que não afeta decisões principais
    /// Exemplo: pequena inconsistência em dados secundários
    /// </summary>
    Minor,

    /// <summary>
    /// Conflito moderado que reduz confiança
    /// Exemplo: dados parciais contraditórios
    /// </summary>
    Moderate,

    /// <summary>
    /// Conflito crítico que invalida análise
    /// Exemplo: claim regulatório contradiz ingrediente explícito
    /// </summary>
    Critical
}
