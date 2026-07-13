using Xunit;
using LabelWise.Application.Parsing;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Tests.Parsing
{
    /// <summary>
    /// Testes para o parser melhorado de rótulos alimentares.
    /// Valida a correção do problema de identificação incorreta de nomes de produtos a partir de tabelas nutricionais.
    /// </summary>
    public class ImprovedIngredientAllergenParserTests
    {
        private readonly IngredientAllergenParser _parser;

        public ImprovedIngredientAllergenParserTests()
        {
            _parser = new IngredientAllergenParser();
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // TESTE 1: Rótulo Limpo (Sem Tabela Nutricional)
        // ═══════════════════════════════════════════════════════════════════════════════
        [Fact]
        public void Parse_CleanLabel_ExtractsCorrectProductNameAndBrand()
        {
            // Arrange
            var ocrText = @"
Chocolate em Pó
NESTLÉ
INGREDIENTES: cacau, açúcar, leite
CONTÉM: leite, soja
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.Equal("Chocolate em Pó", result.ProductName);
            Assert.Equal("NESTLÉ", result.Brand);
            Assert.True(result.Ingredients.Count > 0);
            Assert.Contains("cacau", result.Ingredients);
            Assert.Contains("açúcar", result.Ingredients);
            Assert.Contains("leite", result.Ingredients);
            Assert.Contains("leite", result.ConfirmedAllergens);
            Assert.Contains("soja", result.ConfirmedAllergens);
            Assert.Equal(ConfidenceLevel.High, result.ParsingConfidence);
            Assert.True(result.IsProductNameValidated);
            Assert.True(result.IsBrandValidated);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // TESTE 2: Rótulo com Tabela Nutricional (Problema Original) ⭐
        // ═══════════════════════════════════════════════════════════════════════════════
        [Fact]
        public void Parse_LabelWithNutritionalTable_IgnoresTableAndExtractsCorrectProductName()
        {
            // Arrange
            var ocrText = @"
Biscoito Recheado
BAUDUCCO
INFORMAÇÃO NUTRICIONAL
Porção 30g (3 unidades)
Valor Energético 150 kcal
Carboidratos 20g
Proteínas 3g
Gorduras 5g
%VD 10%
INGREDIENTES: farinha de trigo, açúcar, gordura vegetal
CONTÉM: glúten, leite
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            // ✅ Nome do produto deve ser "Biscoito Recheado", NÃO "Porção 30g (3 unidades)"
            Assert.Equal("Biscoito Recheado", result.ProductName);
            Assert.Equal("BAUDUCCO", result.Brand);

            // ✅ Tabela nutricional deve ser completamente ignorada
            Assert.DoesNotContain("Porção", result.ProductName ?? "");
            Assert.DoesNotContain("kcal", result.ProductName ?? "");
            Assert.DoesNotContain("30g", result.ProductName ?? "");

            Assert.True(result.Ingredients.Count > 0);
            Assert.Contains("farinha de trigo", result.Ingredients);
            Assert.Contains("glúten", result.ConfirmedAllergens);
            Assert.Contains("leite", result.ConfirmedAllergens);
            Assert.Equal(ConfidenceLevel.High, result.ParsingConfidence);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // TESTE 3: Rótulo Sem Nome Válido (Apenas Tabela)
        // ═══════════════════════════════════════════════════════════════════════════════
        [Fact]
        public void Parse_LabelWithoutValidName_ReturnsNullProductName()
        {
            // Arrange
            var ocrText = @"
INFORMAÇÃO NUTRICIONAL
Porção 30g
Valor Energético 150 kcal
INGREDIENTES: farinha, açúcar
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.Null(result.ProductName);
            Assert.Null(result.Brand);
            Assert.True(result.Ingredients.Count > 0);
            Assert.NotEqual(ConfidenceLevel.High, result.ParsingConfidence);
            Assert.True(result.ValidationWarnings.Count > 0);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // TESTE 4: Validação de Nome Inválido (Apenas Números)
        // ═══════════════════════════════════════════════════════════════════════════════
        [Fact]
        public void Parse_LabelWithOnlyNumbers_RejectsAsInvalidProductName()
        {
            // Arrange
            var ocrText = @"
12345
6789
INGREDIENTES: açúcar, farinha
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.Null(result.ProductName);
            Assert.Null(result.Brand);
            Assert.False(result.IsProductNameValidated);
            Assert.True(result.ValidationWarnings.Count > 0);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // TESTE 5: Múltiplos Alergênicos com Classificação
        // ═══════════════════════════════════════════════════════════════════════════════
        [Fact]
        public void Parse_MultipleAllergens_ClassifiesCorrectly()
        {
            // Arrange
            var ocrText = @"
Barra de Cereal
NATURE VALLEY
INGREDIENTES: aveia, mel, amendoim, castanhas
CONTÉM: glúten, amendoim, castanhas
PODE CONTER: leite, soja
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.Equal("Barra de Cereal", result.ProductName);
            Assert.Equal("NATURE VALLEY", result.Brand);

            // Alergênicos confirmados
            Assert.Contains("glúten", result.ConfirmedAllergens);
            Assert.Contains("amendoim", result.ConfirmedAllergens);
            Assert.True(result.ConfirmedAllergens.Contains("castanhas") || result.ConfirmedAllergens.Contains("castanha"));

            // Alergênicos potenciais
            Assert.Contains("leite", result.MayContainAllergens);
            Assert.Contains("soja", result.MayContainAllergens);

            Assert.Equal(ConfidenceLevel.High, result.ParsingConfidence);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // TESTE 6: Limpeza de Ingredientes com Ruído de OCR
        // ═══════════════════════════════════════════════════════════════════════════════
        [Fact]
        public void Parse_IngredientsWithOcrNoise_CleansInvalidCharacters()
        {
            // Arrange
            var ocrText = @"
Produto Teste
MARCA TESTE
INGREDIENTES: |cacau|, açúcar, [leite], {farinha}
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.True(result.Ingredients.Count > 0);

            // Verificar que caracteres inválidos foram removidos
            foreach (var ingredient in result.Ingredients)
            {
                Assert.DoesNotContain("|", ingredient);
                Assert.DoesNotContain("[", ingredient);
                Assert.DoesNotContain("]", ingredient);
                Assert.DoesNotContain("{", ingredient);
                Assert.DoesNotContain("}", ingredient);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // TESTE 7: Texto Vazio Retorna Baixa Confiança
        // ═══════════════════════════════════════════════════════════════════════════════
        [Fact]
        public void Parse_EmptyText_ReturnsLowConfidence()
        {
            // Arrange
            var ocrText = "";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.Null(result.ProductName);
            Assert.Equal(ConfidenceLevel.Low, result.ParsingConfidence);
            Assert.Contains("Texto OCR vazio ou nulo", result.ValidationWarnings);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // TESTE 8: Nome Muito Curto (< 3 caracteres)
        // ═══════════════════════════════════════════════════════════════════════════════
        [Fact]
        public void Parse_ProductNameTooShort_RejectsAsInvalid()
        {
            // Arrange
            var ocrText = @"
AB
CD
INGREDIENTES: açúcar, farinha
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.Null(result.ProductName);
            Assert.False(result.IsProductNameValidated);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // TESTE 9: Nome com Excesso de Símbolos Especiais
        // ═══════════════════════════════════════════════════════════════════════════════
        [Fact]
        public void Parse_ProductNameWithExcessiveSymbols_RejectsAsInvalid()
        {
            // Arrange
            var ocrText = @"
###|||@@@***
Marca Válida
INGREDIENTES: açúcar, farinha
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            // Nome com muitos símbolos deve ser rejeitado
            Assert.DoesNotContain("###", result.ProductName ?? "");
            Assert.DoesNotContain("|||", result.ProductName ?? "");

            // Se houver segundo nome válido, deve ser usado
            if (result.ProductName != null)
            {
                Assert.Equal("Marca Válida", result.ProductName);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // TESTE 10: Palavras-Chave Inválidas para Nome de Produto
        // ═══════════════════════════════════════════════════════════════════════════════
        [Fact]
        public void Parse_LinesWithInvalidKeywords_RejectsAsProductName()
        {
            // Arrange
            var ocrText = @"
INGREDIENTES: teste
CONTÉM: teste
VALIDADE: 01/01/2025
Produto Real
INGREDIENTES: açúcar, farinha
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.DoesNotContain("INGREDIENTES", result.ProductName ?? "");
            Assert.DoesNotContain("CONTÉM", result.ProductName ?? "");
            Assert.DoesNotContain("VALIDADE", result.ProductName ?? "");

            if (result.ProductName != null)
            {
                Assert.Equal("Produto Real", result.ProductName);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // TESTE 11: Confiança Reduzida com Ingredientes Ausentes
        // ═══════════════════════════════════════════════════════════════════════════════
        [Fact]
        public void Parse_NoIngredientsFound_ReducesConfidence()
        {
            // Arrange
            var ocrText = @"
Produto Teste
Marca Teste
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.Empty(result.Ingredients);
            Assert.NotEqual(ConfidenceLevel.High, result.ParsingConfidence);
            Assert.Contains("Nenhum ingrediente identificado", result.ValidationWarnings);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // TESTE 12: Distinção entre ProductName e Brand
        // ═══════════════════════════════════════════════════════════════════════════════
        [Fact]
        public void Parse_TwoValidLines_AssignsFirstToProductNameSecondToBrand()
        {
            // Arrange
            var ocrText = @"
Primeira Linha Válida
Segunda Linha Válida
INGREDIENTES: teste
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.Equal("Primeira Linha Válida", result.ProductName);
            Assert.Equal("Segunda Linha Válida", result.Brand);
            Assert.True(result.IsProductNameValidated);
            Assert.True(result.IsBrandValidated);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // TESTE 13: Suplemento com NÃO CONTÉM GLÚTEN
        // ═══════════════════════════════════════════════════════════════════════════════
        [Fact]
        public void Parse_SupplementWithNaoContemGluten_DoesNotExtractGlutenAsPositive()
        {
            // Arrange - Cenário do suplemento Creatina
            var ocrText = @"
Creapure
INFORMAÇÃO NUTRICIONAL
Porção: 3 g (1 dosador)
Creatina (mg) 3.000
INGREDIENTES: Creatina monohidratada
NÃO CONTÉM GLÚTEN
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.Equal("Creapure", result.ProductName);
            // Glúten NÃO deve aparecer em Allergens ou ConfirmedAllergens
            Assert.DoesNotContain("glúten", result.Allergens);
            Assert.DoesNotContain("gluten", result.Allergens);
            Assert.DoesNotContain("glúten", result.ConfirmedAllergens);
            Assert.DoesNotContain("gluten", result.ConfirmedAllergens);
            // Creatina deve ser extraída
            Assert.NotNull(result.Nutrition);
            Assert.True(result.Nutrition.Creatine.HasValue);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // TESTE 14: Limpeza de Ruído OCR no Nome
        // ═══════════════════════════════════════════════════════════════════════════════
        [Fact]
        public void Parse_ProductNameWithOcrQuotes_CleansNoise()
        {
            // Arrange
            var ocrText = @"
\"" Creapure'
INGREDIENTES: Creatina monohidratada
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            // Nome deve ser limpo de aspas
            Assert.DoesNotContain("\\", result.ProductName ?? "");
            Assert.DoesNotContain("\"", result.ProductName ?? "");
            Assert.DoesNotContain("'", result.ProductName ?? "");
        }
    }
}
