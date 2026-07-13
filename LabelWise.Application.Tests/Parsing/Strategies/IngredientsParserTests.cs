using Xunit;
using LabelWise.Application.Parsing.Strategies;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Tests.Parsing.Strategies
{
    public class IngredientsParserTests
    {
        private readonly IngredientsParser _parser;

        public IngredientsParserTests()
        {
            _parser = new IngredientsParser();
        }

        [Fact]
        public void Parse_ValidIngredientsList_ExtractsAll()
        {
            // Arrange
            var ocrText = @"
                INGREDIENTES: Farinha de trigo, açúcar, gordura vegetal, cacau em pó,
                leite em pó, sal, fermento químico e aromatizante.
            ";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HasIngredients);
            Assert.Equal(ConfidenceLevel.High, result.Confidence);
            Assert.Contains("farinha de trigo", result.Ingredients);
            Assert.Contains("açúcar", result.Ingredients);
            Assert.Contains("gordura vegetal", result.Ingredients);
            Assert.Contains("cacau em pó", result.Ingredients);
            Assert.Contains("leite em pó", result.Ingredients);
            Assert.Contains("sal", result.Ingredients);
            Assert.Contains("fermento químico", result.Ingredients);
            Assert.Contains("aromatizante", result.Ingredients);
        }

        [Fact]
        public void Parse_IngredientsSeparatedBySemicolon_ExtractsCorrectly()
        {
            // Arrange
            var ocrText = "INGREDIENTES: água; açúcar; suco de laranja; ácido cítrico";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HasIngredients);
            Assert.Contains("água", result.Ingredients);
            Assert.Contains("açúcar", result.Ingredients);
            Assert.Contains("suco de laranja", result.Ingredients);
            Assert.Contains("ácido cítrico", result.Ingredients);
        }

        [Fact]
        public void Parse_IngredientsSeparatedByAnd_ExtractsCorrectly()
        {
            // Arrange
            var ocrText = "INGREDIENTES: farinha e açúcar e sal";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HasIngredients);
            Assert.Contains("farinha", result.Ingredients);
            Assert.Contains("açúcar", result.Ingredients);
            Assert.Contains("sal", result.Ingredients);
        }

        [Fact]
        public void Parse_EmptyText_ReturnsLowConfidence()
        {
            // Arrange
            var ocrText = "";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ConfidenceLevel.Low, result.Confidence);
            Assert.False(result.HasIngredients);
            Assert.Contains("Texto OCR vazio ou nulo", result.ValidationWarnings);
        }

        [Fact]
        public void Parse_NoIngredientsSection_ReturnsLowConfidence()
        {
            // Arrange
            var ocrText = "Este é um texto qualquer sem seção de ingredientes";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ConfidenceLevel.Low, result.Confidence);
            Assert.False(result.HasIngredients);
            Assert.Contains("Seção de ingredientes não encontrada", result.ValidationWarnings);
        }

        [Fact]
        public void Parse_OnlyOneIngredient_ReturnsLowConfidence()
        {
            // Arrange
            var ocrText = "INGREDIENTES: água";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ConfidenceLevel.Low, result.Confidence);
            Assert.Single(result.Ingredients);
            Assert.Contains(result.ValidationWarnings, w => w.Contains("Apenas 1 ingrediente"));
        }

        [Fact]
        public void Parse_RemovesInvalidCharacters()
        {
            // Arrange
            var ocrText = "INGREDIENTES: |farinha|, [açúcar], {sal}";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("farinha", result.Ingredients);
            Assert.Contains("açúcar", result.Ingredients);
            Assert.Contains("sal", result.Ingredients);
        }

        [Fact]
        public void Parse_FiltersOutNumericalValues()
        {
            // Arrange
            var ocrText = "INGREDIENTES: farinha, 123, açúcar, 456g, sal";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("farinha", result.Ingredients);
            Assert.Contains("açúcar", result.Ingredients);
            Assert.Contains("sal", result.Ingredients);
            Assert.DoesNotContain("123", result.Ingredients);
        }

        [Fact]
        public void Parse_FiltersOutTableNutritionalKeywords()
        {
            // Arrange
            var ocrText = "INGREDIENTES: farinha, açúcar, sal. INFORMAÇÃO NUTRICIONAL: 100 kcal";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("farinha", result.Ingredients);
            Assert.Contains("açúcar", result.Ingredients);
            Assert.Contains("sal", result.Ingredients);
            Assert.DoesNotContain("informação nutricional", result.Ingredients);
        }

        [Fact]
        public void Parse_RemovesDuplicates()
        {
            // Arrange
            var ocrText = "INGREDIENTES: farinha, açúcar, farinha, sal, açúcar";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Ingredients.Count);
            Assert.Contains("farinha", result.Ingredients);
            Assert.Contains("açúcar", result.Ingredients);
            Assert.Contains("sal", result.Ingredients);
        }
    }
}
