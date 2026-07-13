using Xunit;
using LabelWise.Application.Parsing.Strategies;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Tests.Parsing.Strategies
{
    public class NutritionTableParserTests
    {
        private readonly NutritionTableParser _parser;

        public NutritionTableParserTests()
        {
            _parser = new NutritionTableParser();
        }

        [Fact]
        public void Parse_ValidNutritionTable_ExtractsAllValues()
        {
            // Arrange
            var ocrText = @"
                INFORMAÇÃO NUTRICIONAL
                Porção: 30g
                Valor energético: 120 kcal
                Carboidratos: 25g
                Açúcares: 10g
                Proteínas: 3g
                Gorduras totais: 2g
                Gorduras saturadas: 1g
                Gorduras trans: 0g
                Fibra alimentar: 2g
                Sódio: 150mg
            ";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HasNutritionData);
            Assert.Equal(ConfidenceLevel.High, result.Confidence);
            Assert.Equal("30g", result.ServingSize);
            Assert.Equal(120, result.Calories);
            Assert.Equal(25, result.TotalCarbohydrate);
            Assert.Equal(10, result.Sugars);
            Assert.Equal(3, result.Protein);
            Assert.Equal(2, result.TotalFat);
            Assert.Equal(1, result.SaturatedFat);
            Assert.Equal(0, result.TransFat);
            Assert.Equal(2, result.DietaryFiber);
            Assert.Equal(150, result.Sodium);
        }

        [Fact]
        public void Parse_PartialNutritionTable_ExtractsAvailableValues()
        {
            // Arrange
            var ocrText = @"
                Valor energético: 200 kcal
                Carboidratos: 30g
                Proteínas: 5g
            ";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HasNutritionData);
            Assert.Equal(200, result.Calories);
            Assert.Equal(30, result.TotalCarbohydrate);
            Assert.Equal(5, result.Protein);
            Assert.Null(result.TotalFat);
            Assert.Null(result.Sodium);
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
            Assert.False(result.HasNutritionData);
            Assert.Contains("Texto OCR vazio ou nulo", result.ValidationWarnings);
        }

        [Fact]
        public void Parse_NoNutritionalData_ReturnsLowConfidence()
        {
            // Arrange
            var ocrText = "Este é um texto qualquer sem dados nutricionais";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ConfidenceLevel.Low, result.Confidence);
            Assert.False(result.HasNutritionData);
            Assert.Contains("Nenhum valor nutricional encontrado", result.ValidationWarnings);
        }

        [Fact]
        public void Parse_FewValues_ReturnsMediumConfidence()
        {
            // Arrange
            var ocrText = @"
                Calorias: 150 kcal
                Carboidratos: 20g
            ";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ConfidenceLevel.Low, result.Confidence);
            Assert.True(result.HasNutritionData);
        }

        [Fact]
        public void Parse_SuspiciousValues_AddsWarning()
        {
            // Arrange
            var ocrText = @"
                Calorias: 15000 kcal
                Sódio: 100000mg
            ";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Contains(result.ValidationWarnings, w => w.Contains("suspeito"));
        }

        [Fact]
        public void Parse_DifferentFormats_ExtractsCorrectly()
        {
            // Arrange
            var ocrText = @"
                Energia 180kcal
                Proteínas: 4,5g
                Gorduras 3.2 g
            ";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(180, result.Calories);
            Assert.Equal(4.5, result.Protein);
            Assert.Equal(3.2, result.TotalFat);
        }
    }
}
