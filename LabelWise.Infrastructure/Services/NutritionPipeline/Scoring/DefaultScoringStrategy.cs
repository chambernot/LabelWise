using LabelWise.Application.Interfaces;

namespace LabelWise.Infrastructure.Services.NutritionPipeline.Scoring;

/// <summary>
/// Estratégia padrão — não aplica ajuste adicional; mantém o score genérico já calculado.
/// </summary>
public sealed class DefaultScoringStrategy : ICategoryScoringStrategy
{
    public string StrategyName => "Default";

    public ScoreAdjustmentResult Adjust(CategoryScoringContext ctx) => ScoreAdjustmentResult.NoOp();
}
