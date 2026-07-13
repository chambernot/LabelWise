using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Scoring;
using LabelWise.Domain.Enums;
using Xunit;

namespace LabelWise.Application.Tests.Scoring;

public class NutritionScoreCalculatorTests
{
    [Fact]
    public void Calculate_HighSugarSnack_ReturnsLowScoreAndSugarOffender()
    {
        var response = new NutritionAnalysisResponseDto
        {
            Success = true,
            HasReliableNutritionData = true,
            AnalysisMode = AnalysisMode.FullNutritionLabel,
            Category = "biscoito recheado",
            EstimatedNutritionProfile = new EstimatedNutritionProfileDto
            {
                EstimatedSugarPer100g = 28,
                EstimatedSaturatedFatPer100g = 6,
                EstimatedFatPer100g = 22,
                EstimatedSodiumPer100g = 280,
                EstimatedProteinPer100g = 3,
                EstimatedFiberPer100g = 1
            }
        };

        var score = NutritionScoreCalculator.Calculate(response);

        Assert.Equal("açúcar", score.PrincipalOffender);
        Assert.Equal("nao_recomendado", score.Status);
        Assert.True(score.Value <= 20);
        Assert.Equal("alta", score.Confidence);
    }

    [Fact]
    public void Calculate_SavoryCondiment_PrioritizesSodiumAndIgnoresProteinBonus()
    {
        var response = new NutritionAnalysisResponseDto
        {
            Success = true,
            HasReliableNutritionData = true,
            AnalysisMode = AnalysisMode.FullNutritionLabel,
            Category = "tempero pronto",
            EstimatedNutritionProfile = new EstimatedNutritionProfileDto
            {
                EstimatedSugarPer100g = 2,
                EstimatedSaturatedFatPer100g = 0,
                EstimatedFatPer100g = 4,
                EstimatedSodiumPer100g = 1200,
                EstimatedProteinPer100g = 18,
                EstimatedFiberPer100g = 0
            }
        };

        var score = NutritionScoreCalculator.Calculate(response);

        Assert.Equal("sódio", score.PrincipalOffender);
        Assert.True(score.Value <= 40);
        Assert.DoesNotContain("proteína", score.Reason, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Calculate_WithoutReliableData_UsesEstimatedCategoryRange()
    {
        var response = new NutritionAnalysisResponseDto
        {
            Success = true,
            HasReliableNutritionData = false,
            AnalysisMode = AnalysisMode.FrontOfPackageOnly,
            Category = "suco",
            InferredRisks = new() { "alto_acucar" }
        };

        var score = NutritionScoreCalculator.Calculate(response);

        Assert.InRange(score.Value, 40, 70);
        Assert.Equal("baixa", score.Confidence);
        Assert.Contains("estimado por categoria", score.Reason, System.StringComparison.OrdinalIgnoreCase);
    }
}
