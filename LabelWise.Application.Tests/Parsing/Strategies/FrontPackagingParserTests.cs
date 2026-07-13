using Xunit;
using LabelWise.Application.Parsing.Strategies;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Tests.Parsing.Strategies
{
    public class FrontPackagingParserTests
    {
        private readonly FrontPackagingParser _parser;

        public FrontPackagingParserTests()
        {
            _parser = new FrontPackagingParser();
        }

        [Fact]
        public void Parse_ValidFrontPackaging_ExtractsProductNameAndBrand()
        {
            // Arrange
            var ocrText = @"
                NESTLÉ
                Nescau
                Cereal Matinal
            ";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HasProductInfo);
            Assert.Equal(ConfidenceLevel.High, result.Confidence);
            Assert.Equal("NESTLÉ", result.ProductName);
            Assert.True(result.IsProductNameValidated);
        }

        [Fact]
        public void Parse_ProductWithFlavor_ExtractsFlavorCorrectly()
        {
            // Arrange
            var ocrText = @"
                Coca-Cola
                Sabor Original
            ";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HasProductInfo);
            Assert.Equal("Coca-Cola", result.ProductName);
            Assert.Equal("Sabor Original", result.Flavor);
        }

        [Fact]
        public void Parse_IgnoresNutritionalTableLines()
        {
            // Arrange
            var ocrText = @"
                PRODUTO TESTE
                INFORMAÇÃO NUTRICIONAL
                Valor energético: 100 kcal
                Carboidratos: 20g
            ";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("PRODUTO TESTE", result.ProductName);
            // Não deve incluir linhas de tabela nutricional
        }

        [Fact]
        public void Parse_IgnoresInvalidKeywords()
        {
            // Arrange
            var ocrText = @"
                PRODUTO TESTE
                INGREDIENTES: farinha, açúcar
                CONTÉM GLÚTEN
                Peso líquido: 200g
            ";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("PRODUTO TESTE", result.ProductName);
            // Não deve incluir linhas com keywords inválidas
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
            Assert.False(result.HasProductInfo);
            Assert.Contains("Texto OCR vazio ou nulo", result.ValidationWarnings);
        }

        [Fact]
        public void Parse_NoValidLines_ReturnsLowConfidence()
        {
            // Arrange
            var ocrText = @"
                INGREDIENTES: farinha
                CONTÉM GLÚTEN
                Peso líquido: 200g
            ";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ConfidenceLevel.Low, result.Confidence);
            Assert.False(result.HasProductInfo);
            Assert.Contains("Nenhuma linha válida encontrada", result.ValidationWarnings);
        }

        [Fact]
        public void Parse_OnlyProductName_ReturnsMediumConfidence()
        {
            // Arrange
            var ocrText = "Biscoito de Chocolate";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ConfidenceLevel.Medium, result.Confidence);
            Assert.Equal("Biscoito de Chocolate", result.ProductName);
            Assert.True(result.IsProductNameValidated);
            Assert.Contains("Marca não identificada", result.ValidationWarnings);
        }

        [Fact]
        public void Parse_FiltersLinesWithManyNumbers()
        {
            // Arrange
            var ocrText = @"
                PRODUTO TESTE
                1234567890
                MARCA TESTE
            ";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("PRODUTO TESTE", result.ProductName);
            // Linha com muitos números deve ser filtrada
        }

        [Fact]
        public void Parse_FiltersLinesWithManySpecialChars()
        {
            // Arrange
            var ocrText = @"
                PRODUTO TESTE
                |||***+++===
                MARCA TESTE
            ";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("PRODUTO TESTE", result.ProductName);
            // Linha com muitos caracteres especiais deve ser filtrada
        }

        [Fact]
        public void Parse_VeryShortProductName_AddsWarning()
        {
            // Arrange
            var ocrText = "AB";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Contains(result.ValidationWarnings, w => w.Contains("muito curto"));
        }

        [Fact]
        public void Parse_MultipleValidLines_ExtractsFirstAsProductName()
        {
            // Arrange
            var ocrText = @"
                Biscoito Recheado
                Chocolate
                Nestlé
            ";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Biscoito Recheado", result.ProductName);
        }

        [Fact]
        public void Parse_IdentifiesBrandCorrectly()
        {
            // Arrange
            var ocrText = @"
                Biscoito Recheado
                Nestlé
                Chocolate
            ";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Biscoito Recheado", result.ProductName);
            Assert.Equal("Nestlé", result.Brand);
            Assert.True(result.IsBrandValidated);
        }
    }
}
