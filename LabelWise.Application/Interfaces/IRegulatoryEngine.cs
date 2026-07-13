using LabelWise.Domain.Enums;
using LabelWise.Domain.Models;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// Engine regulatória responsável por interpretar claims oficiais.
/// Claims regulatórios têm PRIORIDADE ABSOLUTA sobre qualquer inferência.
/// </summary>
public interface IRegulatoryEngine
{
    /// <summary>
    /// Detecta claims regulatórios em texto OCR.
    /// </summary>
    /// <param name="ocrText">Texto completo do OCR</param>
    /// <param name="ocrBlocks">Blocos estruturados do OCR (opcional)</param>
    /// <returns>Lista de claims regulatórios detectados</returns>
    Task<IReadOnlyList<RegulatoryClaim>> DetectClaimsAsync(
        string ocrText,
        IReadOnlyList<string>? ocrBlocks = null);

    /// <summary>
    /// Valida se um claim é regulatoriamente válido.
    /// </summary>
    /// <param name="claimText">Texto do claim</param>
    /// <returns>True se o claim é válido</returns>
    bool ValidateClaim(string claimText);

    /// <summary>
    /// Classifica o tipo de claim regulatório.
    /// </summary>
    /// <param name="claimText">Texto normalizado do claim</param>
    /// <returns>Tipo de claim detectado</returns>
    RegulatoryClaimType ClassifyClaimType(string claimText);

    /// <summary>
    /// Extrai o sujeito de um claim (ex: "glúten", "lactose").
    /// </summary>
    /// <param name="claimText">Texto do claim</param>
    /// <returns>Sujeito do claim</returns>
    string ExtractClaimSubject(string claimText);

    /// <summary>
    /// Determina se um claim é absoluto ou probabilístico.
    /// </summary>
    /// <param name="claimType">Tipo de claim</param>
    /// <returns>True se o claim é absoluto</returns>
    bool IsAbsoluteClaim(RegulatoryClaimType claimType);
}
