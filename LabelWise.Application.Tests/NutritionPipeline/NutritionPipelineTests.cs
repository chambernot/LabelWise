using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Domain.Enums;
using LabelWise.Infrastructure.Services.NutritionPipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LabelWise.Application.Tests.NutritionPipeline;

public class CategoryDecisionEngineTests
{
    private readonly ICategoryDecisionEngine _engine = new CategoryDecisionEngine(
        NullLogger<CategoryDecisionEngine>.Instance);

    [Fact]
    public void Decide_UltraProcessedProduct_ReturnsUltraprocessado()
    {
        var context = CreateContext(categoryRaw: "biscoito recheado");
        var result = _engine.Decide(context);

        Assert.Equal("ultraprocessado", result.ProcessingLevel);
        Assert.True(result.IsUltraProcessed);
    }

    [Fact]
    public void Decide_InNaturaProduct_ReturnsInNatura()
    {
        var context = CreateContext(categoryRaw: "fruta", productName: "banana");
        var result = _engine.Decide(context);

        Assert.Equal("in_natura", result.ProcessingLevel);
        Assert.False(result.IsUltraProcessed);
    }

    [Fact]
    public void Decide_ProcessedProduct_ReturnsProcessado()
    {
        var context = CreateContext(categoryRaw: "queijo minas");
        var result = _engine.Decide(context);

        Assert.Equal("minimamente_processado", result.ProcessingLevel);
    }

    [Fact]
    public void Decide_IndustrialBeverage_ReturnsProcessado()
    {
        var context = CreateContext(categoryRaw: "suco de laranja");
        var result = _engine.Decide(context);

        Assert.Equal("processado", result.ProcessingLevel);
    }

    [Fact]
    public void Decide_HighSugarCategory_InfersSugarOffender()
    {
        var context = CreateContext(categoryRaw: "refrigerante");
        var result = _engine.Decide(context);

        Assert.Equal("açúcar", result.PreferredOffender);
    }

    [Fact]
    public void Decide_HighSodiumCategory_InfersSodiumOffender()
    {
        var context = CreateContext(categoryRaw: "salgadinho");
        var result = _engine.Decide(context);

        Assert.Equal("sódio", result.PreferredOffender);
    }

    [Fact]
    public void Decide_UltraProcessed_FallbackScoreRangeIsCapped()
    {
        var context = CreateContext(categoryRaw: "biscoito recheado");
        var result = _engine.Decide(context);

        Assert.True(result.FallbackScoreMax <= 50);
    }

    [Fact]
    public void Decide_InNatura_FallbackScoreRangeIsHigher()
    {
        var context = CreateContext(categoryRaw: "castanha");
        var result = _engine.Decide(context);

        Assert.True(result.FallbackScoreMin >= 60);
    }

    private static NutritionAnalysisContext CreateContext(
        string? categoryRaw = null,
        string? productName = null,
        List<string>? claims = null)
    {
        return new NutritionAnalysisContext
        {
            CategoryRaw = categoryRaw,
            ProductName = productName,
            VisibleClaims = claims ?? []
        };
    }
}

public class PipelineScoreCalculatorTests
{
    private readonly IScoreCalculator _calculator = new PipelineScoreCalculator(
        NullLogger<PipelineScoreCalculator>.Instance);

    [Fact]
    public void Calculate_WithReliableData_HighSugar_PenalizesScore()
    {
        var context = CreateContext(hasReliableData: true, sugar: 30, sodium: 100, fat: 5, protein: 3, fiber: 1);
        var result = _calculator.Calculate(context);

        Assert.True(result.ScoreAdjusted < 60);
        Assert.Contains(result.Penalties, p => p.Reason.Contains("açúcar"));
    }

    [Fact]
    public void Calculate_WithReliableData_HighProtein_GivesBonus()
    {
        var context = CreateContext(hasReliableData: true, sugar: 2, sodium: 100, fat: 3, protein: 25, fiber: 5);
        var result = _calculator.Calculate(context);

        Assert.True(result.ScoreAdjusted >= 70);
        Assert.Contains(result.Bonuses, b => b.Reason.Contains("proteína"));
    }

    [Fact]
    public void Calculate_WithoutReliableData_UsesFallbackScore()
    {
        var context = CreateContext(hasReliableData: false, category: "refrigerante");
        context.CategoryDecision = new CategoryDecisionResult
        {
            FallbackScoreMin = 20,
            FallbackScoreMax = 50,
            PreferredOffender = "açúcar",
            IsUltraProcessed = true
        };
        context.IsUltraProcessed = true;

        var result = _calculator.Calculate(context);

        Assert.True(result.ScoreAdjusted >= 20 && result.ScoreAdjusted <= 50);
        Assert.Equal("açúcar", result.ProbableOffender);
    }

    [Fact]
    public void Calculate_HighSodium_CapsScore()
    {
        var context = CreateContext(hasReliableData: true, sugar: 2, sodium: 1200, fat: 3, protein: 5, fiber: 2);
        var result = _calculator.Calculate(context);

        Assert.True(result.ScoreAdjusted <= 40);
    }

    private static NutritionAnalysisContext CreateContext(
        bool hasReliableData,
        double sugar = 0, double sodium = 0, double fat = 0, double protein = 0, double fiber = 0,
        string? category = null)
    {
        var ctx = new NutritionAnalysisContext
        {
            HasReliableNutritionData = hasReliableData,
            CategoryRaw = category,
            CategoryNormalized = category,
            FinalNutritionProfile = hasReliableData
                ? new EstimatedNutritionProfileDto
                {
                    EstimatedSugarPer100g = sugar,
                    EstimatedSodiumPer100g = sodium,
                    EstimatedFatPer100g = fat,
                    EstimatedProteinPer100g = protein,
                    EstimatedFiberPer100g = fiber
                }
                : null
        };
        return ctx;
    }
}

public class AnalysisConsistencyValidatorTests
{
    private readonly IAnalysisConsistencyValidator _validator = new AnalysisConsistencyValidator(
        NullLogger<AnalysisConsistencyValidator>.Instance);

    [Fact]
    public void ValidateAndCorrect_LowScoreWithoutModeration_ForcesModeration()
    {
        var context = new NutritionAnalysisContext
        {
            ScoreAdjusted = 45,
            RequiresModeration = false,
            ScoreLabel = "Consumo moderado"
        };

        _validator.ValidateAndCorrect(context);

        Assert.True(context.RequiresModeration);
    }

    [Fact]
    public void ValidateAndCorrect_UltraProcessedExcellent_RebaixaLabel()
    {
        var context = new NutritionAnalysisContext
        {
            ScoreAdjusted = 90,
            IsUltraProcessed = true,
            ScoreLabel = "Excelente escolha",
            SafeLabel = "Excelente escolha"
        };

        _validator.ValidateAndCorrect(context);

        Assert.Equal("Boa escolha", context.ScoreLabel);
    }

    [Fact]
    public void ValidateAndCorrect_NoReliableData_CapsScoreAt55()
    {
        var context = new NutritionAnalysisContext
        {
            ScoreAdjusted = 75,
            HasReliableNutritionData = false,
            ScoreLabel = "Boa escolha"
        };

        _validator.ValidateAndCorrect(context);

        Assert.True(context.ScoreAdjusted <= 55);
    }

    [Fact]
    public void ValidateAndCorrect_NoReliableData_ForcesConservativeClassification()
    {
        var context = new NutritionAnalysisContext
        {
            HasReliableNutritionData = false,
            ScoreAdjusted = 50,
            ScoreLabel = "Consumo moderado",
        };
        context.HealthProfiles.Diabetic = new HealthProfileResult { Status = "adequado", Reason = "Baixo teor de açúcar" };

        _validator.ValidateAndCorrect(context);

        Assert.Equal("indeterminado", context.HealthProfiles.Diabetic.Status);
    }

    [Fact]
    public void ValidateAndCorrect_PrincipalOffenderEmpty_PropagatesFromScoreCalculation()
    {
        var context = new NutritionAnalysisContext
        {
            ScoreAdjusted = 50,
            PrincipalOffender = "",
            ScoreCalculation = new ScoreCalculationResult { ProbableOffender = "sódio" },
            ScoreLabel = "Consumo moderado"
        };

        _validator.ValidateAndCorrect(context);

        Assert.Equal("sódio", context.PrincipalOffender);
    }
}

public class NutritionResponseMapperTests
{
    private readonly INutritionResponseMapper _mapper = new NutritionResponseMapper(
        NullLogger<NutritionResponseMapper>.Instance);

    [Fact]
    public void Map_SuccessContext_ProducesValidResponse()
    {
        var context = new NutritionAnalysisContext
        {
            Success = true,
            ProductName = "Arroz Integral",
            CategoryRaw = "arroz",
            CategoryNormalized = "arroz integral",
            ScoreAdjusted = 68,
            ScoreLabel = "Boa escolha",
            SafeLabel = "Boa escolha",
            ProcessingLevel = "minimamente_processado",
            PrincipalOffender = "",
            HasReliableNutritionData = true,
            FallbackType = "real",
            FinalNutritionProfile = new EstimatedNutritionProfileDto
            {
                CaloriesPer100g = 360,
                EstimatedSugarPer100g = 0.5,
                EstimatedProteinPer100g = 7,
                EstimatedSodiumPer100g = 5,
                EstimatedFiberPer100g = 3,
                EstimatedFatPer100g = 2.5
            },
            PublicAnalysisMode = AnalysisMode.FullNutritionLabel,
            ScoreInterpretation = new ScoreInterpretationResult
            {
                Label = "Boa escolha",
                SafeLabel = "Boa escolha",
                Status = "bom",
                Color = "green",
                AbsoluteRecommendation = "Pode entrar na rotina com equilíbrio.",
                RecommendationLevel = "escolha_segura"
            }
        };

        var response = _mapper.Map(context);

        Assert.True(response.Success);
        Assert.Equal("Arroz Integral", response.ProductName);
        Assert.Equal("arroz integral", response.Category);
        Assert.NotNull(response.Score);
        Assert.Equal(68, response.Score!.Value);
        Assert.Equal("Boa escolha", response.Score.Label);
        Assert.Equal("green", response.Score.Color);
        Assert.True(response.HasReliableNutritionData);
        Assert.Equal("real", response.FallbackType);
    }

    [Fact]
    public void Map_ErrorContext_ProducesErrorResponse()
    {
        var context = new NutritionAnalysisContext
        {
            Success = false,
            ErrorMessage = "Erro ao interpretar imagem"
        };

        var response = _mapper.Map(context);

        Assert.False(response.Success);
        Assert.Equal("Erro ao interpretar imagem", response.ErrorMessage);
    }

    [Fact]
    public void Map_NoReliableData_ScoreHasLowConfidence()
    {
        var context = new NutritionAnalysisContext
        {
            Success = true,
            HasReliableNutritionData = false,
            ScoreAdjusted = 45,
            ScoreLabel = "Consumo moderado",
            FallbackType = "category_based"
        };

        var response = _mapper.Map(context);

        Assert.Equal("baixa", response.Score!.Confidence);
    }
}
