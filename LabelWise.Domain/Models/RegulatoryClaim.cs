using LabelWise.Domain.Enums;

namespace LabelWise.Domain.Models;

/// <summary>
/// Representa um claim regulatório detectado no rótulo.
/// Claims regulatórios têm PRIORIDADE ABSOLUTA sobre inferências.
/// </summary>
public sealed class RegulatoryClaim
{
    /// <summary>
    /// Texto original do claim
    /// </summary>
    public required string OriginalText { get; init; }

    /// <summary>
    /// Texto normalizado
    /// </summary>
    public required string NormalizedText { get; init; }

    /// <summary>
    /// Tipo de claim regulatório
    /// </summary>
    public required RegulatoryClaimType ClaimType { get; init; }

    /// <summary>
    /// Substância/ingrediente ao qual o claim se refere
    /// Exemplo: "glúten", "lactose", "leite"
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Indica se é um claim positivo (contém) ou negativo (não contém)
    /// </summary>
    public required bool IsPositiveClaim { get; init; }

    /// <summary>
    /// Nível de certeza do claim (absoluto vs probabilístico)
    /// </summary>
    public required bool IsAbsolute { get; init; }

    /// <summary>
    /// Evidência que suporta este claim
    /// </summary>
    public required Evidence Evidence { get; init; }

    /// <summary>
    /// Confiança na detecção do claim (0.0 a 1.0)
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    /// Indica se este é um claim de contaminação cruzada
    /// </summary>
    public bool IsCrossContamination => ClaimType is RegulatoryClaimType.MayContain or RegulatoryClaimType.CrossContamination;
}
