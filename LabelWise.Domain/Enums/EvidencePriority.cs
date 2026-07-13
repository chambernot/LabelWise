namespace LabelWise.Domain.Enums;

/// <summary>
/// Define a hierarquia e o peso de cada tipo de evidência na análise alimentar.
/// Valores mais altos indicam maior prioridade e confiança.
/// </summary>
public enum EvidencePriority
{
    /// <summary>
    /// Alias oficial da Fase 2 para claim regulatório explícito.
    /// </summary>
    RegulatoryClaim = 100,

    /// <summary>
    /// Uma alegação regulatória explícita (ex: "CONTÉM GLÚTEN"). Prioridade máxima, inquestionável.
    /// </summary>
    RegulatoryClaimExplicit = 100,

    /// <summary>
    /// Alias oficial da Fase 2 para ingrediente confirmado.
    /// </summary>
    IngredientConfirmed = 90,

    /// <summary>
    /// Um ingrediente explicitamente listado na lista de ingredientes. Alta prioridade.
    /// </summary>
    IngredientExplicit = 90,

    /// <summary>
    /// Dado nutricional estruturado e confirmado.
    /// </summary>
    NutritionConfirmed = 85,

    /// <summary>
    /// Evidência OCR de alta confiança.
    /// </summary>
    OcrHighConfidence = 70,

    /// <summary>
    /// Evidência confirmada por OCR e validada semanticamente.
    /// </summary>
    OcrConfirmed = 80,

    /// <summary>
    /// Evidência confirmada por análise de imagem (IA visual).
    /// </summary>
    VisionConfirmed = 70,

    /// <summary>
    /// Inferência baseada em contexto semântico, mas sem confirmação explícita.
    /// </summary>
    SemanticInference = 50,

    /// <summary>
    /// Uma suposição baseada em similaridade de texto ou heurísticas fracas. Baixa prioridade.
    /// </summary>
    SimilarityGuess = 20,

    /// <summary>
    /// Alias oficial da Fase 2 para suposição probabilística.
    /// </summary>
    ProbabilisticGuess = 20,

    /// <summary>
    /// Nenhuma evidência ou fonte de dados. Prioridade nula.
    /// </summary>
    None = 0
}
