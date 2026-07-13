namespace LabelWise.Domain.Enums;

/// <summary>
/// Nível de qualidade e confiabilidade da análise.
/// Determina se os resultados podem ser usados para decisões.
/// </summary>
public enum AnalysisQuality
{
    /// <summary>
    /// Análise confiável e completa
    /// Dados suficientes e consistentes
    /// </summary>
    Reliable,

    /// <summary>
    /// Análise parcial com dados limitados
    /// Alguns campos podem estar ausentes
    /// </summary>
    Partial,

    /// <summary>
    /// Análise insuficiente para decisões
    /// OCR de baixa qualidade ou dados muito limitados
    /// </summary>
    Insufficient,

    /// <summary>
    /// Análise inconsistente com conflitos detectados
    /// Dados contraditórios que requerem revisão manual
    /// </summary>
    Inconsistent
}
