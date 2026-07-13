using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services.NutritionPipeline;

public sealed class CategoryDecisionEngine : ICategoryDecisionEngine
{
    private static readonly string[] UltraProcessedKeywords =
    {
        "barra proteica", "barra de proteína", "barra de proteina",
        "achocolatado", "biscoito recheado", "biscoito amanteigado",
        "salgadinho", "refrigerante", "sobremesa láctea", "sobremesa lactea",
        "bebida proteica", "bebida láctea", "bebida lactea",
        "shake proteico", "cookie proteico", "snack proteico"
    };

    private static readonly string[] ProcessedKeywords =
    {
        "queijo", "pão", "pao", "iogurte", "molho", "conserva",
        "requeijão", "requeijao", "presunto", "peito de peru", "granola"
    };

    private static readonly string[] MinimallyProcessedKeywords =
    {
        "arroz", "aveia", "milho para pipoca", "iogurte natural",
        "feijão", "feijao", "leite", "queijo minas", "queijo fresco", "ricota"
    };

    private static readonly string[] InNaturaKeywords =
    {
        "in natura", "fruta", "verdura", "legume", "hortaliça", "hortalica", "castanha", "semente"
    };

    private static readonly string[] IndustrialBeverageKeywords =
    {
        "suco", "néctar", "nectar", "refresco", "limonada", "bebida à base de", "bebida a base de"
    };

    private static readonly string[] HighSugarCategories =
    {
        "refrigerante", "achocolatado", "biscoito recheado", "chocolate", "sobremesa"
    };

    private static readonly string[] HighSodiumCategories =
    {
        "salgadinho", "tempero", "caldo", "molho", "embutido", "presunto"
    };

    private readonly ILogger<CategoryDecisionEngine> _logger;

    public CategoryDecisionEngine(ILogger<CategoryDecisionEngine> logger)
    {
        _logger = logger;
    }

    public CategoryDecisionResult Decide(NutritionAnalysisContext context)
    {
        var result = new CategoryDecisionResult
        {
            CategoryCode = context.CategoryNormalizedCode,
            CategoryName = context.CategoryNormalized ?? context.CategoryRaw
        };

        var combined = BuildCombinedText(context);

        result.ProcessingLevel = DetermineProcessingLevel(combined, context.VisibleClaims);
        result.IsUltraProcessed = result.ProcessingLevel == "ultraprocessado";
        result.PreferredOffender = InferPreferredOffender(combined, context.VisibleClaims);

        InferNutritionalCapabilities(result, combined);
        BuildQualitativeSignals(result, combined, context.VisibleClaims);

        var (min, max) = InferFallbackScoreRange(result);
        result.FallbackScoreMin = min;
        result.FallbackScoreMax = max;

        _logger.LogInformation(
            "[CategoryDecision] Category={Category}, ProcessingLevel={Level}, Offender={Offender}, ScoreRange={Min}-{Max}",
            result.CategoryName, result.ProcessingLevel, result.PreferredOffender, min, max);

        return result;
    }

    private static string BuildCombinedText(NutritionAnalysisContext context)
    {
        return string.Join(" | ", new[]
            {
                context.CategoryRaw,
                context.CategoryNormalized,
                context.ProductName
            }
            .Where(v => !string.IsNullOrWhiteSpace(v)))
            .ToLowerInvariant();
    }

    private static string DetermineProcessingLevel(string combined, List<string> claims)
    {
        var normalizedClaims = claims
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.ToLowerInvariant())
            .ToList();

        if (ContainsAny(combined, InNaturaKeywords)
            && !ContainsAny(combined, UltraProcessedKeywords)
            && !ContainsAny(combined, IndustrialBeverageKeywords))
            return "in_natura";

        if (ContainsAny(combined, IndustrialBeverageKeywords))
            return "processado";

        if (ContainsAny(combined, UltraProcessedKeywords))
            return "ultraprocessado";

        if (normalizedClaims.Any(c =>
                c.Contains("proteic") || c.Contains("protein") || c.Contains("zero açúcar") || c.Contains("zero acucar"))
            && (combined.Contains("barra") || combined.Contains("bebida") || combined.Contains("shake") || combined.Contains("cookie")))
            return "ultraprocessado";

        if (ContainsAny(combined, MinimallyProcessedKeywords))
            return "minimamente_processado";

        if (ContainsAny(combined, ProcessedKeywords))
            return "processado";

        return "processado";
    }

    private static string InferPreferredOffender(string combined, List<string> claims)
    {
        if (ContainsAny(combined, HighSugarCategories))
            return "açúcar";

        if (ContainsAny(combined, HighSodiumCategories))
            return "sódio";

        if (combined.Contains("gordura") || combined.Contains("fritura") || combined.Contains("frito"))
            return "gordura";

        return "dados insuficientes";
    }

    private static void InferNutritionalCapabilities(CategoryDecisionResult result, string combined)
    {
        result.CanInferProteinPositive = combined.Contains("proteic") || combined.Contains("protein")
            || combined.Contains("whey") || combined.Contains("iogurte");
        result.CanInferFiberPositive = combined.Contains("integral") || combined.Contains("aveia")
            || combined.Contains("granola") || combined.Contains("fibra");
        result.CanInferLowSodiumPositive = combined.Contains("in natura") || combined.Contains("fruta")
            || combined.Contains("castanha");
    }

    private static void BuildQualitativeSignals(CategoryDecisionResult result, string combined, List<string> claims)
    {
        if (result.IsUltraProcessed)
            result.QualitativeSignals.Add("produto ultraprocessado");

        if (ContainsAny(combined, HighSugarCategories))
            result.QualitativeSignals.Add("categoria tipicamente rica em açúcar");

        if (ContainsAny(combined, HighSodiumCategories))
            result.QualitativeSignals.Add("categoria tipicamente rica em sódio");

        if (claims.Any(c => c.Contains("zero", StringComparison.OrdinalIgnoreCase)))
            result.QualitativeSignals.Add("alega zero/redução de algum nutriente");
    }

    private static (int Min, int Max) InferFallbackScoreRange(CategoryDecisionResult result)
    {
        return result.ProcessingLevel switch
        {
            "in_natura" => (60, 82),
            "minimamente_processado" => (55, 75),
            "processado" => (35, 60),
            "ultraprocessado" => (20, 50),
            _ => (35, 60)
        };
    }

    private static bool ContainsAny(string source, IEnumerable<string> terms)
    {
        return !string.IsNullOrWhiteSpace(source)
            && terms.Any(t => source.Contains(t, StringComparison.OrdinalIgnoreCase));
    }
}
