using System.Linq;
using LabelWise.Application.Parsing.Strategies;
using LabelWise.Domain.Enums;
using Xunit;

namespace LabelWise.Application.Tests.Parsing.Strategies
{
    /// <summary>
    /// Testes do parser refinado de tabela nutricional.
    /// Usa exemplos REAIS de OCR de produtos brasileiros.
    /// </summary>
    public class RefinedNutritionTableParserTests
    {
        private readonly NutritionTableParser _parser;

        public RefinedNutritionTableParserTests()
        {
            _parser = new NutritionTableParser();
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // TESTE 1: OREO (Biscoito recheado)
        // ═══════════════════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Parse_OreoNutritionTable_ShouldExtractAllMainFields()
        {
            // Arrange - Texto real de OCR de embalagem Oreo
            var ocrText = @"
INFORMAÇÃO NUTRICIONAL
Porção de 30g (3 biscoitos)
Quantidade por porção
Valor energético 140 kcal
Carboidratos 21 g
Açúcares totais 12 g
Açúcares adicionados 12 g
Proteínas 1,5 g
Gorduras totais 5,5 g
Gorduras saturadas 2,5 g
Gorduras trans 0 g
Fibra alimentar 0,6 g
Sódio 95 mg
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert - Verificar campos principais
            Assert.True(result.HasNutritionData, "Deve ter dados nutricionais");
            Assert.Equal("30g (3 biscoitos)", result.ServingSize?.Trim().Replace("  ", " "));
            
            // Macronutrientes
            Assert.NotNull(result.Calories);
            Assert.Equal(140, result.Calories.Value);
            
            Assert.NotNull(result.TotalCarbohydrate);
            Assert.Equal(21, result.TotalCarbohydrate.Value);
            
            Assert.NotNull(result.Sugars);
            Assert.Equal(12, result.Sugars.Value);
            
            Assert.NotNull(result.AddedSugars);
            Assert.Equal(12, result.AddedSugars.Value);
            
            Assert.NotNull(result.Protein);
            Assert.Equal(1.5, result.Protein.Value);
            
            Assert.NotNull(result.TotalFat);
            Assert.Equal(5.5, result.TotalFat.Value);
            
            Assert.NotNull(result.SaturatedFat);
            Assert.Equal(2.5, result.SaturatedFat.Value);
            
            Assert.NotNull(result.TransFat);
            Assert.Equal(0, result.TransFat.Value);
            
            Assert.NotNull(result.DietaryFiber);
            Assert.Equal(0.6, result.DietaryFiber.Value);
            
            Assert.NotNull(result.Sodium);
            Assert.Equal(95, result.Sodium.Value);

            // Confiança deve ser alta (10+ campos extraídos)
            Assert.Equal(ConfidenceLevel.High, result.Confidence);
            Assert.True(result.ExtractedFieldsCount >= 10, 
                $"Deveria extrair 10+ campos, mas extraiu {result.ExtractedFieldsCount}");
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // TESTE 2: CREATINA (Suplemento)
        // ═══════════════════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Parse_CreatineNutritionTable_ShouldExtractCreatineInMg()
        {
            // Arrange - Texto real de OCR de embalagem de Creatina
            var ocrText = @"
INFORMAÇÕES NUTRICIONAIS
Porção: 3g (1 colher medida)
Aproximadamente 100 porções por embalagem

Quantidade por porção
Valor energético 0 kcal
Carboidratos 0 g
Proteínas 0 g
Gorduras totais 0 g
Creatina 3 g
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.True(result.HasNutritionData);
            
            Assert.NotNull(result.ServingSize);
            Assert.Contains("3g", result.ServingSize);
            
            Assert.NotNull(result.ServingsPerContainer);
            Assert.Equal(100, result.ServingsPerContainer.Value);
            
            // Creatina deve ser convertida para mg
            Assert.NotNull(result.Creatine);
            Assert.Equal(3000, result.Creatine.Value); // 3g = 3000mg
            
            // Outros campos devem ser 0
            Assert.NotNull(result.Calories);
            Assert.Equal(0, result.Calories.Value);
            
            Assert.NotNull(result.TotalCarbohydrate);
            Assert.Equal(0, result.TotalCarbohydrate.Value);

            Assert.Equal(ConfidenceLevel.Medium, result.Confidence);
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // TESTE 3: IOGURTE (Laticínio)
        // ═══════════════════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Parse_YogurtNutritionTable_ShouldExtractCalciumAndLactose()
        {
            // Arrange - Texto real de OCR de embalagem de iogurte
            var ocrText = @"
INFORMAÇÃO NUTRICIONAL
Porção: 200ml (1 copo)

Valor energético 120 kcal
Carboidratos 18 g
Açúcares totais 16 g
Lactose 9,5 g
Proteínas 6,5 g
Gorduras totais 2,8 g
Gorduras saturadas 1,8 g
Gorduras trans 0 g
Fibra alimentar 0 g
Sódio 85 mg
Cálcio 240 mg
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.True(result.HasNutritionData);
            
            Assert.NotNull(result.ServingSize);
            Assert.Contains("200ml", result.ServingSize);
            
            Assert.NotNull(result.Calories);
            Assert.Equal(120, result.Calories.Value);
            
            Assert.NotNull(result.TotalCarbohydrate);
            Assert.Equal(18, result.TotalCarbohydrate.Value);
            
            Assert.NotNull(result.Sugars);
            Assert.Equal(16, result.Sugars.Value);
            
            // Lactose - importante para iogurtes
            Assert.NotNull(result.Lactose);
            Assert.Equal(9.5, result.Lactose.Value);
            
            Assert.NotNull(result.Protein);
            Assert.Equal(6.5, result.Protein.Value);
            
            Assert.NotNull(result.TotalFat);
            Assert.Equal(2.8, result.TotalFat.Value);
            
            Assert.NotNull(result.SaturatedFat);
            Assert.Equal(1.8, result.SaturatedFat.Value);
            
            Assert.NotNull(result.Sodium);
            Assert.Equal(85, result.Sodium.Value);
            
            // Cálcio - importante para laticínios
            Assert.NotNull(result.Calcium);
            Assert.Equal(240, result.Calcium.Value);

            Assert.Equal(ConfidenceLevel.High, result.Confidence);
            Assert.True(result.ExtractedFieldsCount >= 10);
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // TESTE 4: OCR QUEBRADO EM MÚLTIPLAS LINHAS
        // ═══════════════════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Parse_BrokenOcrWithMultipleLines_ShouldStillExtractValues()
        {
            // Arrange - Texto com OCR quebrado (comum em fotos ruins)
            var ocrText = @"
INFORMAÇÃO
NUTRICIONAL
Porção de
30g
Valor
energético
150
kcal
Carboidratos
22,5
g
Proteínas
2
g
Gorduras
totais
6
g
Sódio
100
mg
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert - Parser deve lidar com quebras
            Assert.True(result.HasNutritionData);
            
            Assert.NotNull(result.ServingSize);
            Assert.Contains("30g", result.ServingSize);
            
            Assert.NotNull(result.Calories);
            Assert.Equal(150, result.Calories.Value);
            
            Assert.NotNull(result.TotalCarbohydrate);
            Assert.Equal(22.5, result.TotalCarbohydrate.Value);
            
            Assert.NotNull(result.Protein);
            Assert.Equal(2, result.Protein.Value);
            
            Assert.NotNull(result.TotalFat);
            Assert.Equal(6, result.TotalFat.Value);
            
            Assert.NotNull(result.Sodium);
            Assert.Equal(100, result.Sodium.Value);

            // Confiança média (menos campos devido ao OCR ruim)
            Assert.True(result.Confidence >= ConfidenceLevel.Medium);
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // TESTE 5: SUPORTE A VÍRGULA E PONTO
        // ═══════════════════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Parse_MixedDecimalSeparators_ShouldHandleBothCommaAndDot()
        {
            // Arrange - Texto com vírgulas e pontos misturados
            var ocrText = @"
Porção: 50g
Valor energético 180 kcal
Carboidratos 25,5 g
Proteínas 3.2 g
Gorduras totais 7,8 g
Gorduras saturadas 2.1 g
Fibra alimentar 1,5 g
Sódio 120 mg
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert - Ambos os formatos devem funcionar
            Assert.NotNull(result.TotalCarbohydrate);
            Assert.Equal(25.5, result.TotalCarbohydrate.Value);
            
            Assert.NotNull(result.Protein);
            Assert.Equal(3.2, result.Protein.Value);
            
            Assert.NotNull(result.TotalFat);
            Assert.Equal(7.8, result.TotalFat.Value);
            
            Assert.NotNull(result.SaturatedFat);
            Assert.Equal(2.1, result.SaturatedFat.Value);
            
            Assert.NotNull(result.DietaryFiber);
            Assert.Equal(1.5, result.DietaryFiber.Value);
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // TESTE 6: VALIDAÇÃO DE CONSISTÊNCIA
        // ═══════════════════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Parse_InconsistentData_ShouldAddValidationWarnings()
        {
            // Arrange - Dados inconsistentes (açúcar adicionado > açúcar total)
            var ocrText = @"
Porção: 30g
Valor energético 150 kcal
Carboidratos 20 g
Açúcares totais 10 g
Açúcares adicionados 15 g
Proteínas 2 g
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert - Deve extrair mas adicionar warning
            Assert.True(result.HasNutritionData);
            Assert.NotEmpty(result.ValidationWarnings);
            Assert.Contains(result.ValidationWarnings, 
                w => w.Contains("Açúcar adicionado") && w.Contains("maior que açúcar total"));
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // TESTE 7: IGNORAR %VD
        // ═══════════════════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Parse_WithPercentageDailyValue_ShouldIgnorePercentages()
        {
            // Arrange - Texto com %VD misturado
            var ocrText = @"
Porção: 30g
Valor energético 150 kcal 8% VD
Carboidratos 21 g 7%
Proteínas 2,5 g 3%
Gorduras totais 6 g 11%
Sódio 100 mg 4%
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert - Deve extrair valores, não porcentagens
            Assert.NotNull(result.Calories);
            Assert.Equal(150, result.Calories.Value);
            Assert.NotEqual(8, result.Calories.Value); // Não deve pegar o 8%
            
            Assert.NotNull(result.TotalCarbohydrate);
            Assert.Equal(21, result.TotalCarbohydrate.Value);
            Assert.NotEqual(7, result.TotalCarbohydrate.Value); // Não deve pegar o 7%
            
            Assert.NotNull(result.Sodium);
            Assert.Equal(100, result.Sodium.Value);
            Assert.NotEqual(4, result.Sodium.Value); // Não deve pegar o 4%
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // TESTE 8: TEXTO VAZIO OU INVÁLIDO
        // ═══════════════════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Parse_EmptyText_ShouldReturnLowConfidence()
        {
            // Act
            var result = _parser.Parse("");

            // Assert
            Assert.False(result.HasNutritionData);
            Assert.Equal(ConfidenceLevel.Low, result.Confidence);
            Assert.NotEmpty(result.ValidationWarnings);
        }

        [Fact]
        public void Parse_NoNutritionalData_ShouldReturnLowConfidence()
        {
            // Arrange
            var ocrText = "Este é um texto aleatório sem dados nutricionais.";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.False(result.HasNutritionData);
            Assert.Equal(ConfidenceLevel.Low, result.Confidence);
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // TESTE 9: CONTAGEM DE CAMPOS EXTRAÍDOS
        // ═══════════════════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Parse_CompleteTable_ShouldCountAllExtractedFields()
        {
            // Arrange
            var ocrText = @"
Porção: 30g
Valor energético 150 kcal
Carboidratos 20 g
Açúcares totais 12 g
Açúcares adicionados 10 g
Proteínas 2,5 g
Gorduras totais 6 g
Gorduras saturadas 3 g
Gorduras trans 0 g
Fibra alimentar 1,5 g
Sódio 95 mg
Cálcio 100 mg
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.True(result.ExtractedFieldsCount >= 11, 
                $"Esperado 11+ campos, mas extraiu {result.ExtractedFieldsCount}");
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // TESTE 10: SERVINGS PER CONTAINER
        // ═══════════════════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Parse_WithServingsPerContainer_ShouldExtractServings()
        {
            // Arrange
            var ocrText = @"
INFORMAÇÃO NUTRICIONAL
Porção: 30g
Aproximadamente 10 porções por embalagem
Valor energético 140 kcal
Carboidratos 21 g
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result.ServingsPerContainer);
            Assert.Equal(10, result.ServingsPerContainer.Value);
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // TESTE 11: MÚLTIPLOS FORMATOS DE PORÇÃO
        // ═══════════════════════════════════════════════════════════════════════════════════════

        [Theory]
        [InlineData("Porção: 30g", "30g")]
        [InlineData("Porção de 200ml", "200ml")]
        [InlineData("Porção: 3 biscoitos", "3 biscoitos")]
        [InlineData("Porção de 2 colheres", "2 colheres")]
        [InlineData("Porção: 1 unidade", "1 unidade")]
        [InlineData("Porção de 1 scoop", "1 scoop")]
        public void Parse_DifferentServingSizeFormats_ShouldExtractCorrectly(string servingText, string expected)
        {
            // Arrange
            var ocrText = $@"
{servingText}
Valor energético 100 kcal
Carboidratos 15 g
";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result.ServingSize);
            Assert.Contains(expected.Split(' ')[0], result.ServingSize);
        }
    }
}
