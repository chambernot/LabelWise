using LabelWise.Application.Interfaces;

namespace LabelWise.Infrastructure.Services.NutritionPipeline.Scoring;

/// <summary>
/// Resolve a estratégia de score adequada para uma determinada categoria de produto.
/// Adicionar novas categorias aqui sem tocar no calculador de score.
/// </summary>
public static class CategoryScoringStrategyResolver
{
    private static readonly ICategoryScoringStrategy _proteinBar  = new ProteinBarScoringStrategy();
    private static readonly ICategoryScoringStrategy _yogurt      = new YogurtScoringStrategy();
    private static readonly ICategoryScoringStrategy _beverage    = new BeverageScoringStrategy();
    private static readonly ICategoryScoringStrategy _snack       = new SnackScoringStrategy();
    private static readonly ICategoryScoringStrategy _default     = new DefaultScoringStrategy();

    /// <summary>Retorna a estratégia mais adequada com base no nome da categoria.</summary>
    public static ICategoryScoringStrategy Resolve(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return _default;

        var norm = category.Trim().ToLowerInvariant();

        if (norm.Contains("barra") || norm.Contains("protein"))
            return _proteinBar;

        if (norm.Contains("iogurte") || norm.Contains("yogurt") || norm.Contains("iogurt"))
            return _yogurt;

        if (norm.Contains("bebida") || norm.Contains("refrigerante") || norm.Contains("suco")
            || norm.Contains("néctar") || norm.Contains("nectar") || norm.Contains("achocolatado"))
            return _beverage;

        if (norm.Contains("biscoito") || norm.Contains("bolacha") || norm.Contains("snack")
            || norm.Contains("salgadinho") || norm.Contains("chip"))
            return _snack;

        return _default;
    }
}
