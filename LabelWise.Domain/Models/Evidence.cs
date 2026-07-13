using LabelWise.Domain.Enums;

namespace LabelWise.Domain.Models;

/// <summary>
/// Representa uma evidência estruturada com prioridade e confiança.
/// </summary>
public sealed class Evidence
{
    /// <summary>
    /// Tipo de evidência
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Texto da evidência
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Fonte da evidência (ex: "ingredient_list", "regulatory_claim", "ocr_block_3")
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Prioridade da evidência na hierarquia
    /// </summary>
    public required EvidencePriority Priority { get; init; }

    /// <summary>
    /// Nível de confiança (0.0 a 1.0)
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    /// Bloco de origem no OCR (se aplicável)
    /// </summary>
    public string? OriginBlock { get; init; }

    /// <summary>
    /// Metadados adicionais
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// Timestamp da detecção
    /// </summary>
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;
}
