using LabelWise.Application.Interfaces;

namespace LabelWise.Infrastructure.Services.NutritionPipeline.Scoring;

/// <summary>
/// Estratégia para bebidas (refrigerantes, sucos industrializados, achocolatados).
/// Açúcar é criticamente penalizado; zero açúcar recebe bônus.
/// </summary>
public sealed class BeverageScoringStrategy : ICategoryScoringStrategy
{
    public string StrategyName => "Beverage";

    public ScoreAdjustmentResult Adjust(CategoryScoringContext ctx)
    {
        var rules = new List<string>();
        int delta = 0;

        var sugar = ctx.Sugar ?? 0;

        if (sugar > 8)
        {
            delta -= 40;
            rules.Add("beverage:acucar_critico(-40)");
        }
        else if (sugar == 0)
        {
            delta += 20;
            rules.Add("beverage:zero_acucar(+20)");
        }

        var offender = sugar > 8 ? "açúcar" : null;

        return new ScoreAdjustmentResult
        {
            ScoreDelta            = delta,
            PrincipalOffender     = offender,
            AppliedRules          = rules,
            InferredProcessingLevel = "ultraprocessado"
        };
    }
}
