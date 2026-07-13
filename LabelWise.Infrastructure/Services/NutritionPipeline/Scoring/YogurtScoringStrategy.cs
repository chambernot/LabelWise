using LabelWise.Application.Interfaces;

namespace LabelWise.Infrastructure.Services.NutritionPipeline.Scoring;

/// <summary>
/// Estratégia para iogurtes.
/// Equilíbrio proteína vs açúcar; iogurte natural com baixo açúcar recebe bônus alto.
/// </summary>
public sealed class YogurtScoringStrategy : ICategoryScoringStrategy
{
    public string StrategyName => "Yogurt";

    public ScoreAdjustmentResult Adjust(CategoryScoringContext ctx)
    {
        var rules = new List<string>();
        int delta = 0;

        var sugar   = ctx.Sugar   ?? 0;
        var protein = ctx.Protein ?? 0;

        // Penalidade por açúcar elevado
        if (sugar > 10)
        {
            delta -= 25;
            rules.Add("yogurt:acucar_elevado(-25)");
        }

        // Bônus por proteína razoável
        if (protein > 5)
        {
            delta += 10;
            rules.Add("yogurt:proteina_adequada(+10)");
        }

        // Bônus extra: iogurte natural (baixo açúcar)
        if (sugar <= 5)
        {
            delta += 20;
            rules.Add("yogurt:natural_baixo_acucar(+20)");
        }

        var offender = sugar > 10 && protein <= 5
            ? "açúcar"
            : sugar > 10 ? "açúcar" : null;

        return new ScoreAdjustmentResult
        {
            ScoreDelta            = delta,
            PrincipalOffender     = offender,
            AppliedRules          = rules,
            InferredProcessingLevel = "processado"
        };
    }
}
