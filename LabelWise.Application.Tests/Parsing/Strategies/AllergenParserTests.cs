using Xunit;
using LabelWise.Application.Parsing.Strategies;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Tests.Parsing.Strategies
{
    public class AllergenParserTests
    {
        private readonly AllergenParser _parser;

        public AllergenParserTests()
        {
            _parser = new AllergenParser();
        }

        [Fact]
        public void Parse_ConfirmedAllergens_ExtractsCorrectly()
        {
            // Arrange
            var ocrText = "CONTÉM: GLÚTEN, LEITE, OVOS E SOJA.";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HasConfirmedAllergens);
            Assert.Equal(ConfidenceLevel.High, result.Confidence);
            Assert.Contains("glúten", result.ConfirmedAllergens);
            Assert.Contains("leite", result.ConfirmedAllergens);
            Assert.Contains("ovos", result.ConfirmedAllergens);
            Assert.Contains("soja", result.ConfirmedAllergens);
        }

        [Fact]
        public void Parse_MayContainAllergens_ExtractsCorrectly()
        {
            // Arrange
            var ocrText = "PODE CONTER: AMENDOIM, CASTANHAS E NOZES.";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HasPotentialAllergens);
            Assert.Equal(ConfidenceLevel.High, result.Confidence);
            Assert.Contains("amendoim", result.MayContainAllergens);
            Assert.Contains("castanha", result.MayContainAllergens);
            Assert.Contains("nozes", result.MayContainAllergens);
        }

        [Fact]
        public void Parse_DoesNotContainAllergens_ExtractsCorrectly()
        {
            // Arrange
            var ocrText = "NÃO CONTÉM: GLÚTEN. ISENTO DE LACTOSE.";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HasAllergenInfo);
            Assert.Equal(ConfidenceLevel.High, result.Confidence);
            Assert.Contains("glúten", result.DoesNotContainAllergens);
            Assert.Contains("lactose", result.DoesNotContainAllergens);
        }

        [Fact]
        public void Parse_MixedDeclarations_SeparatesCorrectly()
        {
            // Arrange
            var ocrText = @"
                CONTÉM: GLÚTEN E LEITE.
                PODE CONTER: AMENDOIM.
                NÃO CONTÉM: SOJA.
            ";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HasAllergenInfo);
            Assert.Contains("glúten", result.ConfirmedAllergens);
            Assert.Contains("leite", result.ConfirmedAllergens);
            Assert.Contains("amendoim", result.MayContainAllergens);
            Assert.Contains("soja", result.DoesNotContainAllergens);
        }

        [Fact]
        public void Parse_ContainsDerivatives_ExtractsAsConfirmed()
        {
            // Arrange
            var ocrText = "CONTÉM DERIVADOS DE LEITE E TRIGO.";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HasConfirmedAllergens);
            Assert.Contains("leite", result.ConfirmedAllergens);
            Assert.Contains("trigo", result.ConfirmedAllergens);
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
            Assert.False(result.HasAllergenInfo);
            Assert.Contains("Texto OCR vazio ou nulo", result.ValidationWarnings);
        }

        [Fact]
        public void Parse_NoAllergenInfo_ReturnsLowConfidence()
        {
            // Arrange
            var ocrText = "Este é um texto qualquer sem informações de alérgenos";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ConfidenceLevel.Low, result.Confidence);
            Assert.False(result.HasAllergenInfo);
            Assert.Contains("Nenhuma informação de alérgeno encontrada", result.ValidationWarnings);
        }

        [Fact]
        public void Parse_NormalizesAllergenNames()
        {
            // Arrange - usando forma sem acento
            var ocrText = "CONTÉM: GLUTEN E CAMARAO.";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HasConfirmedAllergens);
            // Deve normalizar para a forma com acento
            Assert.Contains("glúten", result.ConfirmedAllergens);
            Assert.Contains("camarão", result.ConfirmedAllergens);
        }

        [Fact]
        public void Parse_ExtractsPhrasesCorrectly()
        {
            // Arrange
            var ocrText = "CONTÉM: GLÚTEN E LEITE. PODE CONTER TRAÇOS DE AMENDOIM.";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ExtractedPhrases.Count > 0);
        }

        [Fact]
        public void Parse_RemovesDuplicates()
        {
            // Arrange
            var ocrText = @"
                CONTÉM: LEITE.
                CONTÉM DERIVADOS DE LEITE.
            ";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            // Deve ter apenas uma ocorrência de "leite"
            Assert.Single(result.ConfirmedAllergens.Where(a => a == "leite"));
        }

        [Fact]
        public void Parse_MultipleAllergensInSamePhrase_ExtractsAll()
        {
            // Arrange
            var ocrText = "CONTÉM: GLÚTEN (TRIGO, CEVADA, CENTEIO), LEITE, OVOS E SOJA.";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HasConfirmedAllergens);
            Assert.Contains("glúten", result.ConfirmedAllergens);
            Assert.Contains("trigo", result.ConfirmedAllergens);
            Assert.Contains("cevada", result.ConfirmedAllergens);
            Assert.Contains("centeio", result.ConfirmedAllergens);
            Assert.Contains("leite", result.ConfirmedAllergens);
            Assert.Contains("ovos", result.ConfirmedAllergens);
            Assert.Contains("soja", result.ConfirmedAllergens);
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // NOVOS TESTES: Cenários de "NÃO CONTÉM" que não devem gerar alérgenos positivos
        // ═══════════════════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Parse_NaoContemGluten_DoesNotGeneratePositiveAllergen()
        {
            // Arrange - Cenário do suplemento Creatina
            var ocrText = @"
                Creapure
                INFORMAÇÃO NUTRICIONAL
                Porção: 3 g
                INGREDIENTES: Creatina monohidratada
                NÃO CONTÉM GLÚTEN
            ";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            // Glúten NÃO deve aparecer em ConfirmedAllergens
            Assert.DoesNotContain("glúten", result.ConfirmedAllergens);
            Assert.DoesNotContain("gluten", result.ConfirmedAllergens);
            // Glúten NÃO deve aparecer em MayContainAllergens
            Assert.DoesNotContain("glúten", result.MayContainAllergens);
            Assert.DoesNotContain("gluten", result.MayContainAllergens);
            // Glúten DEVE aparecer em DoesNotContainAllergens
            Assert.Contains("glúten", result.DoesNotContainAllergens);
        }

        [Fact]
        public void Parse_MixedNaoContemAndContem_SeparatesCorrectly()
        {
            // Arrange - Produto com "contém" e "não contém" ao mesmo tempo
            var ocrText = @"
                ALÉRGICOS:
                CONTÉM GLÚTEN E LEITE
                NÃO CONTÉM LACTOSE
                PODE CONTER AMENDOIM
            ";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            // Glúten e leite devem ser confirmados
            Assert.Contains("glúten", result.ConfirmedAllergens);
            Assert.Contains("leite", result.ConfirmedAllergens);
            // Lactose NÃO deve ser confirmado (está em "não contém")
            Assert.DoesNotContain("lactose", result.ConfirmedAllergens);
            Assert.Contains("lactose", result.DoesNotContainAllergens);
            // Amendoim deve estar em "pode conter"
            Assert.Contains("amendoim", result.MayContainAllergens);
        }

        [Fact]
        public void Parse_SemGluten_TreatsAsNaoContem()
        {
            // Arrange - Formato alternativo "SEM GLÚTEN"
            var ocrText = "Produto SEM GLÚTEN e SEM LACTOSE";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.DoesNotContain("glúten", result.ConfirmedAllergens);
            Assert.DoesNotContain("lactose", result.ConfirmedAllergens);
            Assert.Contains("glúten", result.DoesNotContainAllergens);
            Assert.Contains("lactose", result.DoesNotContainAllergens);
        }

        [Fact]
        public void Parse_IsentoDeGluten_TreatsAsNaoContem()
        {
            // Arrange - Formato "ISENTO DE"
            var ocrText = "Produto ISENTO DE GLÚTEN";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.DoesNotContain("glúten", result.ConfirmedAllergens);
            Assert.Contains("glúten", result.DoesNotContainAllergens);
        }

        [Fact]
        public void Parse_LivreDeGluten_TreatsAsNaoContem()
        {
            // Arrange - Formato "LIVRE DE"
            var ocrText = "LIVRE DE GLÚTEN E LACTOSE";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.DoesNotContain("glúten", result.ConfirmedAllergens);
            Assert.DoesNotContain("lactose", result.ConfirmedAllergens);
            Assert.Contains("glúten", result.DoesNotContainAllergens);
            Assert.Contains("lactose", result.DoesNotContainAllergens);
        }

        [Fact]
        public void Parse_SameAllergenInContemAndNaoContem_PrefersNaoContem()
        {
            // Arrange - Cenário edge case: mesmo alérgeno em ambas as frases
            // Isso pode acontecer em OCR com ruído ou texto mal formatado
            var ocrText = @"
                NÃO CONTÉM GLÚTEN
                Produzido em linha que processa glúten
            ";

            // Act
            var result = _parser.Parse(ocrText);

            // Assert
            Assert.NotNull(result);
            // A frase negativa deve ter prioridade - glúten deve estar em DoesNotContain
            Assert.Contains("glúten", result.DoesNotContainAllergens);
            // E NÃO deve estar em Confirmed (pois foi explicitamente negado antes)
            Assert.DoesNotContain("glúten", result.ConfirmedAllergens);
        }
    }
}
