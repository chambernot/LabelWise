using LabelWise.Application.DTOs.ProductIdentification;
using LabelWise.Application.Helpers.ProductIdentification;
using Xunit;

namespace LabelWise.Application.Tests.ProductIdentification
{
    public class TextSimilarityCalculatorTests
    {
        [Theory]
        [InlineData("Coca-Cola", "Coca-Cola", 1.0)]
        [InlineData("Coca-Cola", "coca cola", 0.8)]
        [InlineData("Guaraná Antarctica", "guarana antarctica", 0.9)]
        [InlineData("Biscoito Oreo", "Oreo Cookie", 0.4)]
        public void CalculateSimilarity_ReturnsExpectedSimilarity(
            string source, string target, double expectedMin)
        {
            // Act
            var similarity = TextSimilarityCalculator.CalculateSimilarity(source, target);

            // Assert
            Assert.True(similarity >= expectedMin * 0.8, 
                $"Similarity {similarity} should be at least {expectedMin * 0.8}");
        }

        [Theory]
        [InlineData("coca cola refrigerante", "refrigerante cola coca", 0.7)]
        [InlineData("leite integral", "integral leite", 0.9)]
        public void CalculateTokenSimilarity_HandlesWordOrder(
            string source, string target, double expectedMin)
        {
            // Act
            var similarity = TextSimilarityCalculator.CalculateTokenSimilarity(source, target);

            // Assert
            Assert.True(similarity >= expectedMin,
                $"Token similarity {similarity} should be at least {expectedMin}");
        }

        [Theory]
        [InlineData("Coca-Cola Original", new[] { "Pepsi", "Fanta", "Coca-Cola" }, "Coca-Cola")]
        [InlineData("Guaraná", new[] { "Refrigerante", "Guaraná Antarctica", "Água" }, "Guaraná Antarctica")]
        public void FindBestMatch_ReturnsCorrectMatch(
            string query, string[] candidates, string expectedBest)
        {
            // Act
            var (bestMatch, _) = TextSimilarityCalculator.FindBestMatch(query, candidates);

            // Assert
            Assert.Equal(expectedBest, bestMatch);
        }

        [Fact]
        public void NormalizeText_RemovesDiacriticsAndLowercase()
        {
            // Arrange
            var text = "Açaí com Maracujá";

            // Act
            var normalized = TextSimilarityCalculator.NormalizeText(text);

            // Assert
            Assert.Equal("acai com maracuja", normalized);
        }

        [Fact]
        public void CalculateListSimilarity_CalculatesCorrectly()
        {
            // Arrange
            var source = new[] { "leite", "açúcar", "cacau" };
            var target = new[] { "leite", "açucar", "chocolate" };

            // Act
            var similarity = TextSimilarityCalculator.CalculateListSimilarity(source, target);

            // Assert
            Assert.True(similarity > 0.5, $"List similarity {similarity} should be > 0.5");
        }

        [Theory]
        [InlineData("teste", "teste", true)]
        [InlineData("coca cola", "cocacola", true)]
        [InlineData("água", "fogo", false)]
        public void IsSimilar_ReturnsExpectedResult(string source, string target, bool expected)
        {
            // Act
            var result = TextSimilarityCalculator.IsSimilar(source, target, 0.6);

            // Assert
            Assert.Equal(expected, result);
        }
    }

    public class CategoryInferenceHelperTests
    {
        [Theory]
        [InlineData("Refrigerante de Cola com cafeína", "Bebida")]
        [InlineData("Iogurte grego com proteína", "Laticínio")]
        [InlineData("Salgadinho de milho com queijo", "Snack")]
        [InlineData("Chocolate ao leite com avelã", "Doce e Confeitaria")]
        public void InferCategory_ReturnsExpectedCategory(string text, string expectedCategory)
        {
            // Act
            var category = CategoryInferenceHelper.InferCategory(text);

            // Assert
            Assert.Equal(expectedCategory, category);
        }

        [Fact]
        public void InferCategoryFromIngredients_InfersCorrectly()
        {
            // Arrange
            var ingredients = new[] { "leite", "açúcar", "cacau", "manteiga de cacau" };

            // Act
            var category = CategoryInferenceHelper.InferCategoryFromIngredients(ingredients);

            // Assert - can be Laticínio (leite) or Doce e Confeitaria (cacau)
            Assert.NotNull(category);
            Assert.Contains(category, new[] { "Laticínio", "Doce e Confeitaria" });
        }

        [Fact]
        public void GetCategoryProbabilities_ReturnsOrderedList()
        {
            // Arrange
            var text = "Refrigerante de cola com café";

            // Act
            var probabilities = CategoryInferenceHelper.GetCategoryProbabilities(text);

            // Assert
            Assert.NotEmpty(probabilities);
            Assert.True(probabilities[0].Probability >= probabilities[^1].Probability);
        }

        [Fact]
        public void GetAllCategories_ReturnsAllCategories()
        {
            // Act
            var categories = CategoryInferenceHelper.GetAllCategories();

            // Assert
            Assert.True(categories.Count >= 10);
            Assert.Contains("Bebida", categories);
            Assert.Contains("Laticínio", categories);
            Assert.Contains("Snack", categories);
        }

        [Theory]
        [InlineData("refrigerante de laranja", "Bebida", true)]
        [InlineData("biscoito recheado", "Bebida", false)]
        public void BelongsToCategory_ReturnsExpectedResult(
            string text, string category, bool expected)
        {
            // Act
            var result = CategoryInferenceHelper.BelongsToCategory(text, category);

            // Assert
            Assert.Equal(expected, result);
        }
    }

    public class CandidateSuggestionResultTests
    {
        [Fact]
        public void CreateUnknown_SetsCorrectProperties()
        {
            // Act
            var result = CandidateSuggestionResult.CreateUnknown("Test reason");

            // Assert
            Assert.True(result.IsProductUnknown);
            Assert.Equal("Test reason", result.FallbackReason);
            Assert.False(result.HasCandidates);
            Assert.NotNull(result.UserMessage);
        }

        [Fact]
        public void CreateWithCandidates_OrdersByConfidence()
        {
            // Arrange
            var candidates = new List<SuggestedCandidate>
            {
                new() { CandidateName = "Product A", CandidateConfidence = 0.5 },
                new() { CandidateName = "Product B", CandidateConfidence = 0.9 },
                new() { CandidateName = "Product C", CandidateConfidence = 0.7 }
            };

            // Act
            var result = CandidateSuggestionResult.CreateWithCandidates(
                candidates, "fallback", ["TextSimilarity"]);

            // Assert
            Assert.True(result.HasCandidates);
            Assert.Equal("Product B", result.TopCandidates[0].CandidateName);
            Assert.Equal("Product C", result.TopCandidates[1].CandidateName);
            Assert.Equal("Product A", result.TopCandidates[2].CandidateName);
        }

        [Fact]
        public void CreateWithCandidates_SetsCorrectUserMessage()
        {
            // Arrange
            var candidates = new List<SuggestedCandidate>
            {
                new() { CandidateName = "Product A", CandidateConfidence = 0.8 },
                new() { CandidateName = "Product B", CandidateConfidence = 0.6 }
            };

            // Act
            var result = CandidateSuggestionResult.CreateWithCandidates(
                candidates, "fallback", ["TextSimilarity"]);

            // Assert
            Assert.Contains("2 produto(s) similares", result.UserMessage);
        }

        [Fact]
        public void CreateWithCandidates_EmptyList_SetsUnknown()
        {
            // Act
            var result = CandidateSuggestionResult.CreateWithCandidates(
                [], "fallback", []);

            // Assert
            Assert.True(result.IsProductUnknown);
            Assert.False(result.HasCandidates);
        }
    }
}
