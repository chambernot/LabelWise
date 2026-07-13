using LabelWise.Domain.Models;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// Engine de inferência semântica responsável por análises probabilísticas.
/// Inferências NUNCA podem sobrescrever claims regulatórios.
/// </summary>
public interface ISemanticInferenceEngine
{
    /// <summary>
    /// Infere possíveis ingredientes baseado em contexto semântico.
    /// </summary>
    /// <param name="context">Contexto completo (OCR, ingredientes, claims)</param>
    /// <returns>Lista de inferências com evidências</returns>
    Task<IReadOnlyList<Evidence>> InferIngredientsAsync(SemanticContext context);

    /// <summary>
    /// Infere riscos de contaminação cruzada.
    /// </summary>
    /// <param name="context">Contexto completo</param>
    /// <returns>Lista de riscos inferidos</returns>
    Task<IReadOnlyList<Evidence>> InferCrossContaminationRisksAsync(SemanticContext context);

    /// <summary>
    /// Infere nível de processamento baseado em ingredientes e contexto.
    /// </summary>
    /// <param name="ingredients">Lista de ingredientes detectados</param>
    /// <param name="context">Contexto adicional</param>
    /// <returns>Inferência de nível de processamento</returns>
    Task<Evidence> InferProcessingLevelAsync(
        IReadOnlyList<string> ingredients,
        SemanticContext context);

    /// <summary>
    /// Valida se uma inferência tem evidência suficiente.
    /// </summary>
    /// <param name="evidence">Evidência a validar</param>
    /// <param name="minimumConfidence">Confiança mínima aceitável</param>
    /// <returns>True se a evidência é suficiente</returns>
    bool HasSufficientEvidence(Evidence evidence, double minimumConfidence = 0.6);
}

/// <summary>
/// Contexto semântico para inferências.
/// </summary>
public sealed class SemanticContext
{
    public required string RawOcrText { get; init; }
    public IReadOnlyList<string> OcrBlocks { get; init; } = [];
    public IReadOnlyList<string> DetectedIngredients { get; init; } = [];
    public IReadOnlyList<RegulatoryClaim> RegulatoryInformation { get; init; } = [];
    public IReadOnlyList<Evidence> ExistingEvidence { get; init; } = [];
    public string? ProductCategory { get; init; }
    public Dictionary<string, string> NutritionalData { get; init; } = new();
}
