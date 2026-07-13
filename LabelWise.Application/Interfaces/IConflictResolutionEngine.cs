using LabelWise.Domain.Enums;
using LabelWise.Domain.Models;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// Engine de detecção e resolução de conflitos.
/// </summary>
public interface IConflictResolutionEngine
{
    /// <summary>
    /// Detecta conflitos entre diferentes fontes de evidência.
    /// </summary>
    /// <param name="regulatoryClaims">Claims regulatórios detectados</param>
    /// <param name="ingredients">Ingredientes detectados</param>
    /// <param name="inferences">Inferências realizadas</param>
    /// <returns>Lista de conflitos detectados</returns>
    IReadOnlyList<AnalysisConflict> DetectConflicts(
        IReadOnlyList<RegulatoryClaim> regulatoryClaims,
        IReadOnlyList<Evidence> ingredients,
        IReadOnlyList<Evidence> inferences);

    /// <summary>
    /// Resolve conflitos aplicando hierarquia de evidência.
    /// Prioridade: Regulatory > Explicit Ingredient > OCR > Vision > Semantic > Guess
    /// </summary>
    /// <param name="conflicts">Lista de conflitos</param>
    /// <returns>Resoluções aplicadas</returns>
    IReadOnlyDictionary<AnalysisConflict, ConflictResolution> ResolveConflicts(
        IReadOnlyList<AnalysisConflict> conflicts);

    /// <summary>
    /// Determina se um conflito invalida a análise.
    /// </summary>
    /// <param name="conflict">Conflito a avaliar</param>
    /// <returns>True se o conflito é crítico</returns>
    bool IsCriticalConflict(AnalysisConflict conflict);

    /// <summary>
    /// Calcula a qualidade geral da análise baseado em conflitos.
    /// </summary>
    /// <param name="conflicts">Lista de conflitos</param>
    /// <returns>Qualidade da análise</returns>
    AnalysisQuality EvaluateAnalysisQuality(IReadOnlyList<AnalysisConflict> conflicts);
}

/// <summary>
/// Resolução aplicada a um conflito.
/// </summary>
public sealed class ConflictResolution
{
    /// <summary>
    /// Evidência vencedora (maior prioridade)
    /// </summary>
    public required Evidence WinningEvidence { get; init; }

    /// <summary>
    /// Evidência descartada
    /// </summary>
    public required Evidence DiscardedEvidence { get; init; }

    /// <summary>
    /// Motivo da resolução
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Impacto na confiança geral
    /// </summary>
    public required double ConfidenceImpact { get; init; }
}
