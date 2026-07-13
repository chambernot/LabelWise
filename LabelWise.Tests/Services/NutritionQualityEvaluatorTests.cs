using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Infrastructure.Services;
using Xunit;

namespace LabelWise.Tests.Services;

public sealed class NutritionQualityEvaluatorTests
{
    [Fact]
    public void ServingColumnDiscarded_WithCompletePer100Data_ShouldRemainUsable()
    {
        var profile = new EstimatedNutritionProfileDto
        {
            NutritionUnit = "g",
            IsFromOpenAI = true,
            ParserConfidence = "medium",
            NutritionConfidence = new() { GlobalScore = 0.55 },
            CaloriesPer100g = 93,
            EstimatedCarbsPer100g = 14,
            EstimatedSugarPer100g = 12,
            EstimatedAddedSugarPer100g = 8.4,
            EstimatedProteinPer100g = 3,
            EstimatedFatPer100g = 3,
            EstimatedSaturatedFatPer100g = 1.7,
            EstimatedTransFatPer100g = 0,
            EstimatedFiberPer100g = 0.4,
            EstimatedSodiumPer100g = 58
        };
        var warnings = new[]
        {
            "Coluna por porção descartada: múltiplas inconsistências entre coluna por porção e coluna 100g indicam provável desalinhamento de linhas, mas a coluna 100g será preservada como evidência visível independente.",
            "Valor por porção de calorias inconsistente com a coluna 100g (porção=53, esperado≈13). Campo por porção ignorado para evitar desalinhamento de linha."
        };

        var imageQuality = NutritionQualityEvaluator.EvaluateImageQuality(null, profile, warnings);
        var analysisQuality = NutritionQualityEvaluator.EvaluateAnalysisQuality(profile, imageQuality, warnings);
        var reliability = NutritionQualityEvaluator.CalculateReliabilityScore(profile, warnings, imageQuality.SafeForPreciseNutritionAnalysis ? 0 : 10);

        Assert.False(imageQuality.RetryRequested);
        Assert.True(imageQuality.TableVisible);
        Assert.NotEqual("unsafe", analysisQuality.Mode);
        Assert.InRange(reliability, 40, 100);
    }
}
