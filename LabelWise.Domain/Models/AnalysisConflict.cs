using LabelWise.Domain.Enums;

namespace LabelWise.Domain.Models;

/// <summary>
/// Representa um conflito detectado durante a análise.
/// </summary>
public sealed class AnalysisConflict
{
    /// <summary>
    /// Tipo de conflito
    /// </summary>
    public required ConflictType Type { get; init; }

    /// <summary>
    /// Severidade do conflito
    /// </summary>
    public required ConflictSeverity Severity { get; init; }

    /// <summary>
    /// Descrição do conflito
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Evidência A (conflitante)
    /// </summary>
    public required Evidence EvidenceA { get; init; }

    /// <summary>
    /// Evidência B (conflitante)
    /// </summary>
    public required Evidence EvidenceB { get; init; }

    /// <summary>
    /// Resolução aplicada (se houver)
    /// </summary>
    public string? Resolution { get; init; }

    /// <summary>
    /// Indica se requer revisão manual
    /// </summary>
    public required bool RequiresManualReview { get; init; }

    /// <summary>
    /// Timestamp da detecção
    /// </summary>
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;
}
