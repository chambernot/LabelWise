using System.Text.Json.Serialization;

namespace LabelWise.Domain.Enums;

/// <summary>
/// Define o status de compatibilidade de um perfil alimentar, de forma semântica e não ambígua.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CompatibilityStatus
{
    /// <summary>
    /// O produto é totalmente compatível com o perfil alimentar, baseado em evidências fortes.
    /// </summary>
    Compatible,

    /// <summary>
    /// O produto é provavelmente compatível, mas a análise não é 100% conclusiva.
    /// </summary>
    LikelyCompatible,

    /// <summary>
    /// O produto é incompatível com o perfil alimentar, baseado em evidências regulatórias ou ingredientes explícitos.
    /// </summary>
    Incompatible,

    /// <summary>
    /// O produto é provavelmente incompatível, baseado em inferências ou ingredientes de risco.
    /// </summary>
    LikelyIncompatible,

    /// <summary>
    /// O produto apresenta risco de contaminação cruzada para o perfil alimentar.
    /// </summary>
    CrossContaminationRisk,

    /// <summary>
    /// A compatibilidade é incerta devido a dados parciais ou conflitantes.
    /// </summary>
    Uncertain,

    /// <summary>
    /// Não há dados suficientes para determinar a compatibilidade.
    /// </summary>
    InsufficientData
}
