using LabelWise.Application.DTOs.IngredientAnalysis;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Models.NutritionConversation;
using MongoDB.Bson;
using Xunit;

namespace LabelWise.Tests.Services;

public sealed class NutritionConversationContextTests
{
    [Fact]
    public void FromAnalysis_ShouldCreateMongoSerializableContext_WhenIngredientPublicResponseContainsObjectPayload()
    {
        var nutritionAnalysis = new UnifiedNutritionAnalysisResponse
        {
            AnalysisId = Guid.NewGuid(),
            Score = new UnifiedNutritionScore { Value = 70, Label = "Resultado preliminar" }
        };
        var ingredientAnalysis = new IngredientAnalysisResponse
        {
            Success = true,
            IngredientsDetected = ["água", "aveia"],
            Claims = ["CONTÉM GLÚTEN"],
            AllergenRisks = [new AllergenRiskDto { Name = "glúten", RiskType = "contains" }],
            PublicResponse = new FoodPublicResponseDto
            {
                Analysis = new UnifiedFoodAnalysisResponse()
            }
        };

        var context = NutritionConversationContext.FromAnalysis(nutritionAnalysis, ingredientAnalysis);
        var session = new NutritionConversationSession
        {
            Id = "conversation-1",
            ConversationId = "conversation-1",
            AnalysisId = nutritionAnalysis.AnalysisId.Value.ToString("N"),
            Context = context
        };

        var bson = session.ToBsonDocument();

        Assert.Contains("água", context.IngredientAnalysis!.IngredientsDetected);
        Assert.Null(context.IngredientAnalysis.PublicResponse.Analysis);
        Assert.True(bson.Contains("Context"));
    }
}
