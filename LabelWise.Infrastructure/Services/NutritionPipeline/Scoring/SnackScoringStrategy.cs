using LabelWise.Application.Interfaces;

namespace LabelWise.Infrastructure.Services.NutritionPipeline.Scoring;

/// <summary>
/// Estratégia para snacks e biscoitos.
/// Gordura + açúcar são criticamente penalizados.
/// </summary>
public sealed class SnackScoringStrategy : ICategoryScoringStrategy
{
    public string StrategyName => "Snack";

    public ScoreAdjustmentResult Adjust(CategoryScoringContext ctx)
    {
        var rules = new List<string>();
        int delta = 0;

        var sugar = ctx.Sugar ?? 0;
        var fat   = ctx.Fat   ?? 0;

        if (sugar > 15)
        {
            delta -= 30;
            rules.Add("snack:acucar_alto(-30)");
        }

        if (fat > 15)
        {
            delta -= 25;
            rules.Add("snack:gordura_alta(-25)");
        }

        string? offender = (sugar > 15 && fat > 15) ? "açúcar e gordura"
            : sugar > 15 ? "açúcar"
            : fat > 15   ? "gordura"
            : null;

        return new ScoreAdjustmentResult
        {
            ScoreDelta            = delta,
            PrincipalOffender     = offender,
            AppliedRules          = rules,
            InferredProcessingLevel = "ultraprocessado"
        };
    }
}
