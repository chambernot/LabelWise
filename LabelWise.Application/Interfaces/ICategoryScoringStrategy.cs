namespace LabelWise.Application.Interfaces;

/// <summary>
/// Estratégia de ajuste de score por categoria de alimento.
/// Cada categoria aplica suas próprias regras de penalidade e bônus,
/// tornando o score sensível ao contexto do produto.
/// </summary>
public interface ICategoryScoringStrategy
{
    /// <summary>Identificador descritivo da estratégia.</summary>
    string StrategyName { get; }

    /// <summary>
    /// Aplica os ajustes de score específicos da categoria.
    /// </summary>
    /// <param name="context">Dados nutricionais do produto e score base calculado.</param>
    /// <returns>Resultado com delta de pontos, offender e regras aplicadas.</returns>
    ScoreAdjustmentResult Adjust(CategoryScoringContext context);
}

/// <summary>Contexto nutricional passado para a estratégia de score por categoria.</summary>
public sealed class CategoryScoringContext
{
    public string Category { get; init; } = string.Empty;
    public double? Sugar { get; init; }
    public double? AddedSugar { get; init; }
    public double? Protein { get; init; }
    public double? Fat { get; init; }
    public double? SaturatedFat { get; init; }
    public double? Sodium { get; init; }
    public double? Calories { get; init; }
    public double? Fiber { get; init; }

    /// <summary>Score já calculado pelas regras genéricas (base + penalidades/bônus padrão).</summary>
    public int BaseScore { get; init; }
}

/// <summary>Resultado do ajuste de score aplicado pela estratégia de categoria.</summary>
public sealed class ScoreAdjustmentResult
{
    /// <summary>Delta positivo ou negativo a somar ao score base.</summary>
    public int ScoreDelta { get; init; }

    /// <summary>Offender específico da categoria, ou null para não sobrescrever.</summary>
    public string? PrincipalOffender { get; init; }

    /// <summary>Regras semânticas aplicadas para rastreabilidade.</summary>
    public IReadOnlyList<string> AppliedRules { get; init; } = [];

    /// <summary>Processamento inferido pela estratégia, ou null para não sobrescrever.</summary>
    public string? InferredProcessingLevel { get; init; }

    public static ScoreAdjustmentResult NoOp() => new();
}
