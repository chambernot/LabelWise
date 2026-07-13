using LabelWise.Application.Interfaces;

namespace LabelWise.Infrastructure.Services.NutritionPipeline.Scoring;

/// <summary>
/// Estratégia para barras proteicas.
/// Proteína alta é positivo; açúcar alto penaliza menos; gordura saturada ainda penaliza.
/// </summary>
public sealed class ProteinBarScoringStrategy : ICategoryScoringStrategy
{
    public string StrategyName => "ProteinBar";

    public ScoreAdjustmentResult Adjust(CategoryScoringContext ctx)
    {
        var rules = new List<string>();
        int delta = 0;

        var protein  = ctx.Protein    ?? 0;
        var sugar    = ctx.Sugar       ?? 0;
        var satFat   = ctx.SaturatedFat ?? 0;

        // Bônus: proteína alta
        if (protein > 20)
        {
            delta += 15;
            rules.Add("protein_bar:proteina_alta(+15)");
        }

        // Penalidade reduzida por açúcar (contexto proteico justifica mais açúcar)
        if (sugar > 15)
        {
            var rawPenalty = 15; // metade da penalidade padrão de -30
            // Se proteína alta: reduzir impacto negativo do açúcar em 40%
            if (protein > 20)
            {
                rawPenalty = (int)Math.Round(rawPenalty * 0.60);
                rules.Add("protein_bar:reducao_penalidade_acucar_proteina_alta");
            }
            delta -= rawPenalty;
            rules.Add($"protein_bar:acucar_alto(-{rawPenalty})");
        }

        // Gordura saturada ainda penaliza
        if (satFat > 5)
        {
            delta -= 15;
            rules.Add("protein_bar:gordura_saturada_alta(-15)");
        }

        var offender = BuildOffender(protein, sugar, satFat);

        return new ScoreAdjustmentResult
        {
            ScoreDelta            = delta,
            PrincipalOffender     = offender,
            AppliedRules          = rules,
            InferredProcessingLevel = "ultraprocessado"
        };
    }

    private static string? BuildOffender(double protein, double sugar, double satFat)
    {
        if (sugar > 15 && satFat > 5) return "açúcar e gordura saturada";
        if (sugar > 15) return "açúcar";
        if (satFat > 5) return "gordura saturada";
        if (protein > 20) return null; // sem offender dominante — proteína é o destaque
        return null;
    }
}
