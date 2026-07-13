using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LabelWise.Tests.Services;

public class IntelligentAnalysisScoreServiceTests
{
    [Fact]
    public void Apply_OpenAiVisionWithMissingCriticalFields_ShouldNotCalculateScore()
    {
        var service = new IntelligentAnalysisScoreService(
            new NutritionScoringServiceV2(NullLogger<NutritionScoringServiceV2>.Instance),
            NullLogger<IntelligentAnalysisScoreService>.Instance);

        var response = new IntelligentAnalysisResponse
        {
            Source = "openai-vision",
            Nutrition = new NutritionTableView
            {
                Unit = "g",
                Per100 = new NutritionValues
                {
                    CaloriesKcal = 213,
                    Carbohydrates = null,
                    Sugars = null,
                    Proteins = 5,
                    TotalFats = 9,
                    SaturatedFats = 1,
                    Fiber = 2.5,
                    SodiumMg = 18
                }
            }
        };

        service.Apply(response, confidence: null);

        Assert.Null(response.Score);
        Assert.Contains(response.Diagnostics.Warnings,
            warning => warning.Contains("Score nutricional não calculado", StringComparison.OrdinalIgnoreCase));
    }
}
