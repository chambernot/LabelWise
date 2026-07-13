namespace LabelWise.Domain.Enums;

/// <summary>
/// Status de compatibilidade alimentar baseado em evidências.
/// Deve refletir a confiança e o tipo de evidência disponível.
/// </summary>
public enum FoodCompatibilityStatus
{
    /// <summary>
    /// Compatível confirmado por evidência regulatória ou explícita
    /// Exemplo: "SEM GLÚTEN" para perfil gluten-free
    /// </summary>
    Compatible,

    /// <summary>
    /// Provavelmente compatível baseado em análise de ingredientes
    /// Sem evidência contrária, mas sem confirmação regulatória
    /// </summary>
    LikelyCompatible,

    /// <summary>
    /// Dados insuficientes para determinar compatibilidade
    /// OCR de baixa qualidade ou informação incompleta
    /// </summary>
    Uncertain,

    /// <summary>
    /// Provavelmente incompatível baseado em sinais indiretos
    /// Exemplo: Inferência de ingrediente animal sem confirmação
    /// </summary>
    LikelyIncompatible,

    /// <summary>
    /// Risco de contaminação cruzada detectado
    /// Exemplo: "PODE CONTER LEITE" para perfil lactose-free
    /// </summary>
    CrossContaminationRisk,

    /// <summary>
    /// Incompatível confirmado por evidência explícita
    /// Exemplo: "CONTÉM GLÚTEN" para perfil gluten-free
    /// </summary>
    Incompatible,

    /// <summary>
    /// Dados insuficientes para análise
    /// Nenhuma informação útil disponível
    /// </summary>
    InsufficientData
}
