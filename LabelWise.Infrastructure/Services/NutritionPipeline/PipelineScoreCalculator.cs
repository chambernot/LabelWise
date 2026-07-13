using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Domain.Enums;
using LabelWise.Infrastructure.Services.NutritionPipeline.Scoring;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services.NutritionPipeline;

/// <summary>
/// Score nutricional robusto de 0–100 inspirado em Yuka/Foodvisor:
/// penaliza açúcar, sódio, gordura saturada, calorias e ultraprocessamento;
/// bonifica fibra e proteína; aplica travas críticas para categorias de risco.
/// </summary>
public sealed class PipelineScoreCalculator : IScoreCalculator
{
    // ──────────────────────────────────────────────────────────────────
    // Fallback base-scores (used when no numeric data is available)
    // ──────────────────────────────────────────────────────────────────
    private static readonly IReadOnlyDictionary<string, int> CategoryBaseScores =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["refrigerante"]        = 18,
            ["salgadinho"]          = 26,
            ["biscoito recheado"]   = 30,
            ["biscoito amanteigado"]= 36,
            ["chocolate"]           = 30,
            ["achocolatado em pó"]  = 35,
            ["achocolatado"]        = 35,
            ["embutido"]            = 28,
            ["salsicha"]            = 28,
            ["macarrão instantâneo"]= 32,
            ["miojo"]               = 32,
            ["pão de forma"]        = 50,
            ["arroz integral"]      = 68,
            ["arroz"]               = 58,
            ["feijão"]              = 72,
            ["iogurte proteico"]    = 76,
            ["iogurte comum"]       = 58,
            ["queijo minas"]        = 60,
            ["queijo light"]        = 66,
            ["barra proteica"]      = 72,
        };

    private readonly ILogger<PipelineScoreCalculator> _logger;

    public PipelineScoreCalculator(ILogger<PipelineScoreCalculator> logger) => _logger = logger;

    // ──────────────────────────────────────────────────────────────────
    // Entry point
    // ──────────────────────────────────────────────────────────────────

    public ScoreCalculationResult Calculate(NutritionAnalysisContext context)
    {
        var result = new ScoreCalculationResult();

        if (!context.HasReliableNutritionData || context.FinalNutritionProfile == null)
            CalculateFallbackScore(context, result);
        else
            CalculateFromNutritionData(context, result);

        _logger.LogInformation(
            "[PipelineScoreCalculator] Raw={Raw} Adjusted={Adj} Offender={Off} Penalties={P} Bonuses={B}",
            result.ScoreRaw, result.ScoreAdjusted, result.ProbableOffender,
            result.Penalties.Count, result.Bonuses.Count);

        return result;
    }

    // ──────────────────────────────────────────────────────────────────
    // MAIN: score from nutrition data (per 100 g or 100 ml)
    // ──────────────────────────────────────────────────────────────────

    private static void CalculateFromNutritionData(NutritionAnalysisContext context, ScoreCalculationResult result)
    {
        var n = context.FinalNutritionProfile!;

        var calories   = n.CaloriesPer100g              ?? 0;
        var sugar      = n.EstimatedSugarPer100g         ?? 0;
        var addedSugar = n.EstimatedAddedSugarPer100g    ?? 0;
        var satFat     = n.EstimatedSaturatedFatPer100g  ?? 0;
        var sodium     = n.EstimatedSodiumPer100g        ?? 0;
        var protein    = n.EstimatedProteinPer100g       ?? 0;
        var fiber      = n.EstimatedFiberPer100g         ?? 0;

        var isUltra = context.IsUltraProcessed
                      || context.ProcessingLevel.Equals("ultraprocessado", StringComparison.OrdinalIgnoreCase);

        // ── 1. Score base por nível de processamento ────────────────────
        int score = GetProcessingBaseScore(context.ProcessingLevel, isUltra);
        result.AppliedRules.Add($"base:{context.ProcessingLevel}={score}");

        // ── 2. Penalidades ─────────────────────────────────────────────

        // Açúcar total
        var sugarPenalty = sugar switch
        {
            > 15 => 25,
            > 10 => 15,
            > 5  => 8,
            _    => 0
        };
        if (sugarPenalty > 0) AddPenalty(result, "açúcar", sugarPenalty);

        // Açúcar adicionado (prioridade alta)
        var addedSugarPenalty = addedSugar switch
        {
            > 10 => 30,
            > 5  => 20,
            > 0  => 10,
            _    => 0
        };
        if (addedSugarPenalty > 0) AddPenalty(result, "açúcar adicionado", addedSugarPenalty);

        // Sódio
        var sodiumPenalty = sodium switch
        {
            > 600 => 20,
            > 300 => 10,
            _     => 0
        };
        if (sodiumPenalty > 0) AddPenalty(result, "sódio", sodiumPenalty);

        // Gordura saturada
        var satFatPenalty = satFat switch
        {
            > 5 => 15,
            > 2 => 8,
            _   => 0
        };
        if (satFatPenalty > 0) AddPenalty(result, "gordura saturada", satFatPenalty);

        // Calorias (peso reduzido — nunca o principal fator)
        var calPenalty = calories switch
        {
            > 500 => 10,
            > 300 => 5,
            _     => 0
        };
        if (calPenalty > 0) AddPenalty(result, "calorias", calPenalty);

        // ── 3. Bônus nutricionais ──────────────────────────────────────

        var fiberBonus = fiber switch
        {
            > 8 => 10,
            > 4 => 5,
            _   => 0
        };
        if (fiberBonus > 0) result.Bonuses.Add(new ScoreImpact { Reason = "fibras", Points = fiberBonus });

        var proteinBonus = protein switch
        {
            > 10 => 8,
            > 5  => 4,
            _    => 0
        };
        if (proteinBonus > 0) result.Bonuses.Add(new ScoreImpact { Reason = "proteína", Points = proteinBonus });

        // ── 4. Aplicar penalidades e bônus ─────────────────────────────
        var totalPenalty = result.Penalties.Sum(p => p.Points);
        var totalBonus   = Math.Min(18, result.Bonuses.Sum(b => b.Points));

        score = Math.Clamp(score - totalPenalty + totalBonus, 0, 100);

        // ── 5. Regras de coerência ─────────────────────────────────────
        score = ApplyCoherenceRules(score, context.ProcessingLevel, isUltra, addedSugar, result);

        result.ScoreRaw      = score;
        result.ProbableOffender = DetermineOffender(sugar, addedSugar, sodium, satFat, calories, isUltra);

        // ── 6. Ajuste por categoria (Strategy Pattern) ─────────────────
        score = ApplyCategoryStrategy(context, score, n, result);

        result.ScoreAdjusted = score;
        result.AppliedRules  = BuildAppliedRules(result);
    }

    // ── Aplicar Strategy de categoria ─────────────────────────────────

    private static int ApplyCategoryStrategy(NutritionAnalysisContext context, int score, EstimatedNutritionProfileDto n, ScoreCalculationResult result)
    {
        var category = context.CategoryNormalized ?? context.CategoryRaw;
        var strategy = CategoryScoringStrategyResolver.Resolve(category);

        if (strategy is DefaultScoringStrategy) return score;

        var ctx = new CategoryScoringContext
        {
            Category     = category ?? string.Empty,
            Sugar        = n.EstimatedSugarPer100g,
            AddedSugar   = n.EstimatedAddedSugarPer100g,
            Protein      = n.EstimatedProteinPer100g,
            Fat          = n.EstimatedFatPer100g,
            SaturatedFat = n.EstimatedSaturatedFatPer100g,
            Sodium       = n.EstimatedSodiumPer100g,
            Calories     = n.CaloriesPer100g,
            Fiber        = n.EstimatedFiberPer100g,
            BaseScore    = score
        };

        var adjustment = strategy.Adjust(ctx);

        foreach (var rule in adjustment.AppliedRules)
            result.AppliedRules.Add(rule);

        // Sobrescrever offender somente se a estratégia retornou um valor
        if (!string.IsNullOrWhiteSpace(adjustment.PrincipalOffender))
            result.ProbableOffender = adjustment.PrincipalOffender;

        // Sobrescrever processing level somente se a estratégia indicou um
        if (!string.IsNullOrWhiteSpace(adjustment.InferredProcessingLevel)
            && string.Equals(context.ProcessingLevel, "processado", StringComparison.OrdinalIgnoreCase))
        {
            context.ProcessingLevel = adjustment.InferredProcessingLevel;
        }

        var adjusted = Math.Clamp(score + adjustment.ScoreDelta, 0, 100);
        result.AppliedRules.Add($"category_strategy:{strategy.StrategyName}(delta={adjustment.ScoreDelta:+#;-#;0})");
        return adjusted;
    }

    // ── Score base por processamento ───────────────────────────────────

    private static int GetProcessingBaseScore(string processingLevel, bool isUltraProcessed)
    {
        if (isUltraProcessed) return 40;

        return processingLevel.ToLowerInvariant() switch
        {
            "in_natura"              => 95,
            "in natura"              => 95,
            "minimamente_processado" => 85,
            "processado"             => 65,
            "ultraprocessado"        => 40,
            _                        => 50
        };
    }

    // ── Regras de coerência ────────────────────────────────────────────

    private static int ApplyCoherenceRules(int score, string processingLevel, bool isUltra, double addedSugar, ScoreCalculationResult result)
    {
        // Regra 1: minimamente_processado/in_natura nunca abaixo de 70
        var pl = processingLevel.ToLowerInvariant();
        if ((pl is "minimamente_processado" or "in_natura" or "in natura") && !isUltra && score < 70)
        {
            result.AppliedRules.Add("regra1:processamento_leve_floor70");
            score = 70;
        }

        // Regra 2: açúcar adicionado > 15g → score máximo = 40
        if (addedSugar > 15 && score > 40)
        {
            result.AppliedRules.Add("regra2:acucar_adicionado_alto_cap40");
            score = 40;
        }

        return Math.Clamp(score, 0, 100);
    }

    // ── Offender detection ─────────────────────────────────────────────

    private static string DetermineOffender(double sugar, double addedSugar, double sodium, double satFat, double calories, bool isUltra)
    {
        // Açúcar adicionado tem prioridade máxima
        if (addedSugar > 5)  return "açúcar adicionado";
        if (sugar > 10)      return "açúcar";
        if (sodium > 400)    return "sódio";
        if (satFat > 4)      return "gordura saturada";
        if (calories > 400)  return "calorias";
        if (isUltra)         return "ultraprocessado";
        return string.Empty;
    }

    // ── Fallback (no numeric data) ─────────────────────────────────────

    private static void CalculateFallbackScore(NutritionAnalysisContext context, ScoreCalculationResult result)
    {
        var decision  = context.CategoryDecision;
        var baseScore = GetCategoryBaseScore(context.CategoryNormalized ?? context.CategoryRaw);

        // Ajustar base pelo nível de processamento se não há score de categoria específico
        if (baseScore == 50)
            baseScore = GetProcessingBaseScore(context.ProcessingLevel, context.IsUltraProcessed);

        var riskPenalty = context.InferredRisks.Count * 5;
        if (context.IsUltraProcessed) riskPenalty += 10;

        var raw = Math.Clamp(baseScore - riskPenalty, decision.FallbackScoreMin, decision.FallbackScoreMax);

        result.ScoreRaw      = raw;
        result.ScoreAdjusted = raw;
        result.ProbableOffender = decision.PreferredOffender;

        if (riskPenalty > 0)
            result.Penalties.Add(new ScoreImpact { Reason = "riscos inferidos por categoria", Points = riskPenalty });

        result.AppliedRules = ["fallback_qualitativo"];
    }

    // ── RequiresModeration ─────────────────────────────────────────────

    /// <summary>
    /// Determina se o produto requer moderação com base nos limiares definidos.
    /// Chamado pelo pipeline no Stage 8 para complementar a interpretação do score.
    /// </summary>
    public static bool ComputeRequiresModeration(NutritionAnalysisContext context)
    {
        if (context.FinalNutritionProfile == null) return context.IsUltraProcessed;

        var n         = context.FinalNutritionProfile;
        var addedSugar = n.EstimatedAddedSugarPer100g ?? 0;
        var sodium     = n.EstimatedSodiumPer100g      ?? 0;
        var satFat     = n.EstimatedSaturatedFatPer100g ?? 0;
        var isUltra    = context.IsUltraProcessed;

        if (addedSugar > 8)  return true;
        if (sodium > 400)    return true;
        if (satFat > 4)      return true;
        if (isUltra && context.ScoreAdjusted > 70) return true;

        return false;
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static void AddPenalty(ScoreCalculationResult r, string reason, int points) =>
        r.Penalties.Add(new ScoreImpact { Reason = reason, Points = points });

    private static int GetCategoryBaseScore(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return 50;
        var norm = category.Trim().ToLowerInvariant();
        foreach (var key in CategoryBaseScores.Keys.OrderByDescending(k => k.Length))
            if (norm.Contains(key, StringComparison.OrdinalIgnoreCase))
                return CategoryBaseScores[key];
        return 50;
    }

    private static List<string> BuildAppliedRules(ScoreCalculationResult result)
    {
        var rules = result.Penalties.Select(p => $"penalidade:{p.Reason}(-{p.Points})")
            .Concat(result.Bonuses.Select(b => $"bonus:{b.Reason}(+{b.Points})"))
            .ToList();
        return rules;
    }
}
