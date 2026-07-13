using LabelWise.Application.Interfaces;
using LabelWise.Infrastructure.Services.NutritionPipeline.Scoring;
using Xunit;

namespace LabelWise.Application.Tests.NutritionPipeline;

/// <summary>
/// Testes das strategies de score por categoria nutricional.
/// </summary>
public class CategoryScoringStrategyTests
{
    // ──────────────────────────────────────────────────────────────────
    // Resolver
    // ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("barra proteica", "ProteinBar")]
    [InlineData("Barra de Proteína Whey", "ProteinBar")]
    [InlineData("iogurte grego", "Yogurt")]
    [InlineData("Iogurte Natural", "Yogurt")]
    [InlineData("refrigerante cola", "Beverage")]
    [InlineData("suco de laranja industrializado", "Beverage")]
    [InlineData("achocolatado", "Beverage")]
    [InlineData("biscoito recheado", "Snack")]
    [InlineData("salgadinho de milho", "Snack")]
    [InlineData("arroz integral", "Default")]
    [InlineData("feijão preto", "Default")]
    public void Resolver_ReturnsCorrectStrategy(string category, string expectedStrategy)
    {
        var strategy = CategoryScoringStrategyResolver.Resolve(category);
        Assert.Equal(expectedStrategy, strategy.StrategyName);
    }

    // ──────────────────────────────────────────────────────────────────
    // ProteinBar
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void ProteinBar_AltaProteina_ScoreSobe()
    {
        var strategy = new ProteinBarScoringStrategy();
        var ctx = BuildCtx("barra proteica", protein: 25, sugar: 5, satFat: 2, baseScore: 50);

        var result = strategy.Adjust(ctx);

        Assert.True(result.ScoreDelta > 0, "Proteína alta deve resultar em delta positivo");
        Assert.Contains(result.AppliedRules, r => r.Contains("proteina_alta"));
    }

    [Fact]
    public void ProteinBar_AltaProteinaAltaAcucar_PenalidadeReduzida()
    {
        var strategy = new ProteinBarScoringStrategy();

        // Com proteína alta: penalidade do açúcar reduzida em 40%
        var ctxComProteina    = BuildCtx("barra proteica", protein: 25, sugar: 20, satFat: 1, baseScore: 50);
        var ctxSemProteina    = BuildCtx("barra proteica", protein: 5,  sugar: 20, satFat: 1, baseScore: 50);

        var resultComProteina = strategy.Adjust(ctxComProteina);
        var resultSemProteina = strategy.Adjust(ctxSemProteina);

        // Com proteína alta o delta total deve ser maior (menos penalidade)
        Assert.True(resultComProteina.ScoreDelta > resultSemProteina.ScoreDelta,
            "Proteína alta deve reduzir o impacto negativo do açúcar");
    }

    [Fact]
    public void ProteinBar_GorduraSaturadaAlta_Penaliza()
    {
        var strategy = new ProteinBarScoringStrategy();
        var ctx = BuildCtx("barra proteica", protein: 10, sugar: 5, satFat: 8, baseScore: 60);

        var result = strategy.Adjust(ctx);

        Assert.True(result.ScoreDelta < 0);
        Assert.Contains(result.AppliedRules, r => r.Contains("gordura_saturada"));
    }

    // ──────────────────────────────────────────────────────────────────
    // Yogurt
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Yogurt_Natural_BaixoAcucar_ScoreAlto()
    {
        var strategy = new YogurtScoringStrategy();
        var ctx = BuildCtx("iogurte natural", protein: 6, sugar: 3, fat: 2, baseScore: 60);

        var result = strategy.Adjust(ctx);

        Assert.True(result.ScoreDelta > 0, "Iogurte natural equilibrado deve ter delta positivo");
        Assert.Contains(result.AppliedRules, r => r.Contains("natural_baixo_acucar"));
    }

    [Fact]
    public void Yogurt_AcucarElevado_Penaliza()
    {
        var strategy = new YogurtScoringStrategy();
        var ctx = BuildCtx("iogurte", protein: 3, sugar: 15, fat: 2, baseScore: 60);

        var result = strategy.Adjust(ctx);

        Assert.True(result.ScoreDelta < 0);
        Assert.Equal("açúcar", result.PrincipalOffender);
    }

    // ──────────────────────────────────────────────────────────────────
    // Beverage
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Beverage_AcucarAlto_ScoreMuitoBaixo()
    {
        var strategy = new BeverageScoringStrategy();
        var ctx = BuildCtx("refrigerante", sugar: 12, baseScore: 50);

        var result = strategy.Adjust(ctx);

        Assert.Equal(-40, result.ScoreDelta);
        Assert.Equal("açúcar", result.PrincipalOffender);
        Assert.True(50 + result.ScoreDelta <= 20, "Score final deve ficar baixo");
    }

    [Fact]
    public void Beverage_ZeroAcucar_Bonus()
    {
        var strategy = new BeverageScoringStrategy();
        var ctx = BuildCtx("refrigerante zero", sugar: 0, baseScore: 40);

        var result = strategy.Adjust(ctx);

        Assert.Equal(20, result.ScoreDelta);
        Assert.Null(result.PrincipalOffender);
    }

    // ──────────────────────────────────────────────────────────────────
    // Snack
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Snack_GordurosoComAcucar_ScoreBaixo()
    {
        var strategy = new SnackScoringStrategy();
        var ctx = BuildCtx("biscoito recheado", sugar: 20, fat: 20, baseScore: 50);

        var result = strategy.Adjust(ctx);

        Assert.Equal(-55, result.ScoreDelta);
        Assert.Equal("açúcar e gordura", result.PrincipalOffender);
        Assert.True(50 + result.ScoreDelta <= 0, "Score final deve ser muito baixo");
    }

    [Fact]
    public void Snack_ApenasGordura_Penaliza()
    {
        var strategy = new SnackScoringStrategy();
        var ctx = BuildCtx("salgadinho", sugar: 2, fat: 22, baseScore: 50);

        var result = strategy.Adjust(ctx);

        Assert.Equal(-25, result.ScoreDelta);
        Assert.Equal("gordura", result.PrincipalOffender);
    }

    // ──────────────────────────────────────────────────────────────────
    // Default
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Default_NaoAlteraScore()
    {
        var strategy = new DefaultScoringStrategy();
        var ctx = BuildCtx("arroz integral", baseScore: 70);

        var result = strategy.Adjust(ctx);

        Assert.Equal(0, result.ScoreDelta);
        Assert.Null(result.PrincipalOffender);
    }

    // ──────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────

    private static CategoryScoringContext BuildCtx(
        string category,
        double? protein = null,
        double? sugar = null,
        double? fat = null,
        double? satFat = null,
        double? sodium = null,
        double? calories = null,
        double? fiber = null,
        int baseScore = 50) =>
        new()
        {
            Category     = category,
            Protein      = protein,
            Sugar        = sugar,
            Fat          = fat,
            SaturatedFat = satFat,
            Sodium       = sodium,
            Calories     = calories,
            Fiber        = fiber,
            BaseScore    = baseScore
        };
}
