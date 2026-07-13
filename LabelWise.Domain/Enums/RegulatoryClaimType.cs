namespace LabelWise.Domain.Enums;

/// <summary>
/// Tipo de claim regulatório detectado.
/// Claims regulatórios são ABSOLUTOS e têm a maior prioridade.
/// </summary>
public enum RegulatoryClaimType
{
    /// <summary>
    /// Não identificado ou claim genérico
    /// </summary>
    Unknown,

    /// <summary>
    /// Claim "CONTÉM" (absoluto)
    /// Exemplo: "CONTÉM GLÚTEN", "CONTÉM LEITE"
    /// </summary>
    Contains,

    /// <summary>
    /// Claim "PODE CONTER" (probabilístico)
    /// Exemplo: "PODE CONTER TRAÇOS DE LEITE"
    /// </summary>
    MayContain,

    /// <summary>
    /// Claim "LIVRE DE" (absoluto positivo)
    /// Exemplo: "SEM GLÚTEN", "ZERO LACTOSE"
    /// </summary>
    FreeFrom,

    /// <summary>
    /// Certificação oficial
    /// Exemplo: "CERTIFICADO VEGANO", "ORGÂNICO"
    /// </summary>
    Certified,

    /// <summary>
    /// Proibição explícita para público específico
    /// Exemplo: "NÃO RECOMENDADO PARA DIABÉTICOS"
    /// </summary>
    Prohibited,

    /// <summary>
    /// Aviso regulatório
    /// Exemplo: "ALÉRGICOS: CONTÉM..."
    /// </summary>
    Warning,

    /// <summary>
    /// Contaminação cruzada
    /// Exemplo: "FABRICADO EM EQUIPAMENTO QUE PROCESSA..."
    /// </summary>
    CrossContamination
}
