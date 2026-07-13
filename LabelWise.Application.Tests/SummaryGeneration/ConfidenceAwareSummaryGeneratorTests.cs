using System;
using System.Collections.Generic;
using Xunit;
using LabelWise.Application.Confidence;
using LabelWise.Application.SummaryGeneration;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Enums;
using ConfLevel = LabelWise.Application.Confidence.ConfidenceLevel;

namespace LabelWise.Application.Tests.SummaryGeneration
{
    /// <summary>
    /// Testes unitários para ConfidenceAwareSummaryGenerator
    /// Verifica que resumos parciais NUNCA usam frases otimistas.
    /// </summary>
    public class ConfidenceAwareSummaryGeneratorTests
    {
        private readonly ConfidenceAwareSummaryGenerator _generator;

        public ConfidenceAwareSummaryGeneratorTests()
        {
            _generator = new ConfidenceAwareSummaryGenerator();
        }

        // ═══════════════════════════════════════════════════════════════════
        // TESTES: ANÁLISE PARCIAL - NUNCA USAR FRASES OTIMISTAS
        // ═══════════════════════════════════════════════════════════════════

        [Fact]
        public void PartialAnalysis_ShouldNeverUse_BoaEscolha()
        {
            // Arrange
            var product = CreateTestProduct("Produto Teste");
            var context = CreatePartialAnalysisContext();

            // Score alto que normalmente seria "Boa Escolha"
            var generalScore = 0.85;
            var personalizedScore = 0.80;

            // Act
            var result = _generator.GenerateSummaryWithContext(
                product, null, [], [], null,
                generalScore, personalizedScore, [], [], context);

            // Assert
            Assert.DoesNotContain("Boa Escolha", result.Summary);
            Assert.DoesNotContain("Boa Escolha", result.ShortSummary);
            Assert.DoesNotContain("Excelente Escolha", result.Summary);
            Assert.DoesNotContain("Excelente Escolha", result.ShortSummary);
        }

        [Fact]
        public void PartialAnalysis_ShouldNeverUse_PodeConsumirRegularmente()
        {
            // Arrange
            var product = CreateTestProduct("Produto Teste");
            var context = CreatePartialAnalysisContext();
            var generalScore = 0.85;
            var personalizedScore = 0.80;

            // Act
            var result = _generator.GenerateSummaryWithContext(
                product, null, [], [], null,
                generalScore, personalizedScore, [], [], context);

            // Assert
            Assert.DoesNotContain("Pode consumir regularmente", result.Summary);
            Assert.DoesNotContain("Pode consumir regularmente", result.ShortSummary);
            Assert.DoesNotContain("consumo regular", result.ShortSummary);
        }

        [Fact]
        public void PartialAnalysis_ShouldUse_AnaliseParcialDoRotulo()
        {
            // Arrange
            var product = CreateTestProduct("Produto Teste");
            var context = new AnalysisContext
            {
                ProductIdentified = true,
                OcrComplete = true,
                AnalysisComplete = false, // Análise incompleta
                OverallConfidenceLevel = ConfLevel.Low,
                QualityGatePassed = false
            };

            // Act
            var result = _generator.GenerateSummaryWithContext(
                product, null, [], [], null,
                0.75, 0.70, [], [], context);

            // Assert - Deve usar frases de análise parcial
            Assert.True(
                result.Summary.Contains("Análise parcial") ||
                result.Summary.Contains("parcial") ||
                result.ShortSummary.Contains("parcial"),
                "Deve mencionar análise parcial");
        }

        [Fact]
        public void PartialAnalysis_WithIncompleteOcr_ShouldSuggest_EnvieOutraImagem()
        {
            // Arrange
            var product = CreateTestProduct("Produto Teste");
            var context = new AnalysisContext
            {
                ProductIdentified = true,
                OcrComplete = false, // OCR incompleto
                AnalysisComplete = false,
                OverallConfidenceLevel = ConfLevel.Low,
                QualityGatePassed = false
            };

            // Act
            var result = _generator.GenerateSummaryWithContext(
                product, null, [], [], null,
                0.75, 0.70, [], [], context);

            // Assert
            Assert.True(
                result.Summary.Contains("Envie outra imagem") ||
                result.Summary.Contains("Leitura incompleta") ||
                result.ShortSummary.Contains("Envie") ||
                result.ShortSummary.Contains("incompleta"),
                "Deve sugerir enviar outra imagem ou mencionar leitura incompleta");
        }

        // ═══════════════════════════════════════════════════════════════════
        // TESTES: ALÉRGENOS DECLARADOS - EVITAR SAFE
        // ═══════════════════════════════════════════════════════════════════

        [Fact]
        public void DeclaredAllergens_ShouldNeverUse_SafeClassification()
        {
            // Arrange
            var product = CreateTestProduct("Produto com Alérgenos");
            var context = new AnalysisContext
            {
                ProductIdentified = true,
                OcrComplete = true,
                AnalysisComplete = true,
                HasDeclaredAllergens = true, // Alérgenos presentes
                OverallConfidenceLevel = ConfLevel.High,
                QualityGatePassed = true
            };

            // Score alto
            var generalScore = 0.85;
            var personalizedScore = 0.85;

            // Act
            var result = _generator.GenerateSummaryWithContext(
                product, null, [], [], null,
                generalScore, personalizedScore, [], [], context);

            // Assert - Classificação NÃO deve ser Safe
            Assert.NotEqual(AnalysisClassification.Safe, result.Classification);
            Assert.NotEqual(AnalysisClassification.Excellent, result.Classification);
        }

        [Fact]
        public void DeclaredAllergens_ShouldAlertUser()
        {
            // Arrange
            var product = CreateTestProduct("Produto com Alérgenos");
            var allergens = new List<ProductAllergen>
            {
                new(Guid.NewGuid(), "Leite", true),
                new(Guid.NewGuid(), "Glúten", true)
            };
            var context = new AnalysisContext
            {
                ProductIdentified = true,
                OcrComplete = true,
                AnalysisComplete = true,
                HasDeclaredAllergens = true,
                AllergensCount = 2,
                OverallConfidenceLevel = ConfLevel.High,
                QualityGatePassed = true
            };

            // Act
            var result = _generator.GenerateSummaryWithContext(
                product, null, [], allergens, null,
                0.85, 0.85, [], [], context);

            // Assert - Deve alertar sobre alérgenos
            Assert.True(
                result.Summary.Contains("Alérgeno") ||
                result.Summary.Contains("alérgeno") ||
                result.ShortSummary.Contains("alérgeno") ||
                result.ShortSummary.Contains("ALÉRGENO"),
                "Deve alertar sobre alérgenos");
        }

        // ═══════════════════════════════════════════════════════════════════
        // TESTES: PRODUTO NÃO IDENTIFICADO
        // ═══════════════════════════════════════════════════════════════════

        [Fact]
        public void UnidentifiedProduct_ShouldUse_CautionOrIncomplete()
        {
            // Arrange
            var product = CreateTestProduct("Unknown");
            var context = new AnalysisContext
            {
                ProductIdentified = false, // Produto não identificado
                OcrComplete = true,
                AnalysisComplete = false,
                OverallConfidenceLevel = ConfLevel.Low,
                QualityGatePassed = false
            };

            // Act
            var result = _generator.GenerateSummaryWithContext(
                product, null, [], [], null,
                0.85, 0.85, [], [], context);

            // Assert - Classificação deve ser Caution ou Incomplete
            Assert.True(
                result.Classification == AnalysisClassification.Caution ||
                result.Classification == AnalysisClassification.Incomplete,
                $"Classificação deveria ser Caution ou Incomplete, mas foi {result.Classification}");
        }

        [Fact]
        public void UnidentifiedProduct_ShouldNotUse_BoaEscolha_EvenWithHighScore()
        {
            // Arrange
            var product = CreateTestProduct("Unknown");
            var context = new AnalysisContext
            {
                ProductIdentified = false,
                OcrComplete = true,
                AnalysisComplete = false,
                OverallConfidenceLevel = ConfLevel.Low,
                QualityGatePassed = false
            };

            // Act
            var result = _generator.GenerateSummaryWithContext(
                product, null, [], [], null,
                0.95, 0.95, [], [], context); // Score muito alto

            // Assert
            Assert.DoesNotContain("Boa Escolha", result.Summary);
            Assert.DoesNotContain("Excelente", result.Summary);
            Assert.True(
                result.Summary.Contains("não identificado") ||
                result.ShortSummary.Contains("não identificado"),
                "Deve mencionar que o produto não foi identificado");
        }

        // ═══════════════════════════════════════════════════════════════════
        // TESTES: CONFIANÇA ALTA + ANÁLISE COMPLETA = PERMITIR AFIRMATIVAS
        // ═══════════════════════════════════════════════════════════════════

        [Fact]
        public void HighConfidence_CompleteAnalysis_NoAllergens_ShouldAllow_AffirmativeMessages()
        {
            // Arrange
            var product = CreateTestProduct("Produto Saudável");
            var nutrition = CreateHealthyNutrition();
            var ingredients = CreateValidIngredients();
            var context = new AnalysisContext
            {
                ProductIdentified = true,
                OcrComplete = true,
                AnalysisComplete = true,
                HasDeclaredAllergens = false, // Sem alérgenos
                OverallConfidenceLevel = ConfLevel.High,
                QualityGatePassed = true,
                ValidIngredientsCount = 5,
                NutritionalFieldsCount = 7,
                OcrQualityScore = 0.95
            };

            // Act
            var result = _generator.GenerateSummaryWithContext(
                product, nutrition, ingredients, [], null,
                0.85, 0.85, [], [], context);

            // Assert - Agora SIM pode usar mensagens afirmativas
            Assert.True(
                result.Summary.Contains("Excelente Escolha") ||
                result.Summary.Contains("Boa Escolha") ||
                result.Summary.Contains("Perfil Nutricional Positivo"),
                "Com confiança alta e análise completa, deve permitir mensagens afirmativas");
        }

        [Fact]
        public void HighConfidence_CompleteAnalysis_ShouldReturn_SafeOrExcellent()
        {
            // Arrange
            var product = CreateTestProduct("Produto Excelente");
            var context = new AnalysisContext
            {
                ProductIdentified = true,
                OcrComplete = true,
                AnalysisComplete = true,
                HasDeclaredAllergens = false,
                OverallConfidenceLevel = ConfLevel.High,
                QualityGatePassed = true
            };

            // Act
            var result = _generator.GenerateSummaryWithContext(
                product, null, [], [], null,
                0.90, 0.90, [], [], context);

            // Assert
            Assert.True(
                result.Classification == AnalysisClassification.Safe ||
                result.Classification == AnalysisClassification.Excellent ||
                result.Classification == AnalysisClassification.Moderate,
                $"Classificação deveria ser Safe, Excellent ou Moderate, mas foi {result.Classification}");
        }

        // ═══════════════════════════════════════════════════════════════════
        // TESTES: DISCLAIMERS E AJUSTES
        // ═══════════════════════════════════════════════════════════════════

        [Fact]
        public void PartialAnalysis_ShouldReturn_Disclaimers()
        {
            // Arrange
            var product = CreateTestProduct("Produto Teste");
            var context = CreatePartialAnalysisContext();

            // Act
            var result = _generator.GenerateSummaryWithContext(
                product, null, [], [], null,
                0.75, 0.75, [], [], context);

            // Assert
            Assert.NotEmpty(result.Disclaimers);
            Assert.True(result.IsPartialAnalysis);
        }

        [Fact]
        public void ClassificationAdjustment_ShouldRecord_Reason()
        {
            // Arrange
            var product = CreateTestProduct("Produto Teste");
            var context = new AnalysisContext
            {
                ProductIdentified = false, // Vai forçar ajuste
                OcrComplete = true,
                AnalysisComplete = false,
                OverallConfidenceLevel = ConfLevel.Low,
                QualityGatePassed = false
            };

            // Act
            var result = _generator.GenerateSummaryWithContext(
                product, null, [], [], null,
                0.85, 0.85, [], [], context);

            // Assert
            Assert.True(result.ClassificationAdjusted);
            Assert.NotEmpty(result.AdjustmentReason);
        }

        // ═══════════════════════════════════════════════════════════════════
        // TESTES: COMPATIBILIDADE COM INTERFACE LEGADA
        // ═══════════════════════════════════════════════════════════════════

        [Fact]
        public void LegacyInterface_ShouldWork_WithConservativeContext()
        {
            // Arrange
            var product = CreateTestProduct("Produto Teste");

            // Act - Usando a interface IAnalysisSummaryGenerator (legado)
            var summary = _generator.GenerateSummary(
                product, null, [], [], null,
                0.75, 0.75, [], []);

            // Assert
            Assert.NotNull(summary);
            Assert.NotEmpty(summary);
        }

        // ═══════════════════════════════════════════════════════════════════
        // HELPER METHODS
        // ═══════════════════════════════════════════════════════════════════

        private static Product CreateTestProduct(string name)
        {
            return new Product(name, "Test Brand", Guid.NewGuid());
        }

        private static AnalysisContext CreatePartialAnalysisContext()
        {
            return new AnalysisContext
            {
                ProductIdentified = true,
                OcrComplete = false, // OCR incompleto
                AnalysisComplete = false,
                HasDeclaredAllergens = false,
                OverallConfidenceLevel = ConfLevel.Low,
                QualityGatePassed = false,
                ValidIngredientsCount = 0,
                NutritionalFieldsCount = 2
            };
        }

        private static NutritionalInfo CreateHealthyNutrition()
        {
            var nutrition = new NutritionalInfo(Guid.NewGuid());
            nutrition.UpdateMacros(
                calories: 150,
                totalFat: 3,
                carbs: 20,
                sugars: 5,
                protein: 12,
                fiber: 8,
                sodium: 200
            );
            return nutrition;
        }

        private static List<ProductIngredient> CreateValidIngredients()
        {
            var productId = Guid.NewGuid();
            return
            [
                new ProductIngredient(productId, "Aveia integral", 1),
                new ProductIngredient(productId, "Mel", 2),
                new ProductIngredient(productId, "Castanha de caju", 3),
                new ProductIngredient(productId, "Óleo de coco", 4),
                new ProductIngredient(productId, "Sal", 5)
            ];
        }
    }

    /// <summary>
    /// Testes para SummaryAdjustmentRules
    /// </summary>
    public class SummaryAdjustmentRulesTests
    {
        [Theory]
        [InlineData("Boa Escolha")]
        [InlineData("Excelente Escolha")]
        [InlineData("Pode consumir regularmente")]
        [InlineData("Produto adequado para consumo regular")]
        public void ContainsProhibitedPhrases_ShouldDetect_ProhibitedPhrases(string phrase)
        {
            // Arrange
            var summary = $"Este é um teste com a frase '{phrase}' no meio.";

            // Act
            var result = SummaryAdjustmentRules.ContainsProhibitedPhrases(summary);

            // Assert
            Assert.True(result, $"Deveria detectar frase proibida: {phrase}");
        }

        [Fact]
        public void ContainsProhibitedPhrases_ShouldReturn_False_ForSafePhrases()
        {
            // Arrange
            var summary = "Análise parcial do rótulo. Envie outra imagem para maior precisão.";

            // Act
            var result = SummaryAdjustmentRules.ContainsProhibitedPhrases(summary);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AdjustClassification_UnidentifiedProduct_ShouldReturn_Incomplete()
        {
            // Arrange
            var context = new AnalysisContext
            {
                ProductIdentified = false,
                OcrComplete = true,
                AnalysisComplete = false,
                OverallConfidenceLevel = ConfLevel.Low
            };

            // Act
            var result = SummaryAdjustmentRules.AdjustClassification(
                AnalysisClassification.Safe, context, out var reason);

            // Assert
            Assert.Equal(AnalysisClassification.Incomplete, result);
            Assert.NotEmpty(reason);
        }

        [Fact]
        public void AdjustClassification_PartialAnalysis_SafeInput_ShouldReturn_Caution()
        {
            // Arrange
            var context = new AnalysisContext
            {
                ProductIdentified = true,
                OcrComplete = false, // Parcial
                AnalysisComplete = false,
                OverallConfidenceLevel = ConfLevel.Medium
            };

            // Act
            var result = SummaryAdjustmentRules.AdjustClassification(
                AnalysisClassification.Safe, context, out var reason);

            // Assert
            Assert.Equal(AnalysisClassification.Caution, result);
        }

        [Fact]
        public void AdjustClassification_WithAllergens_SafeInput_ShouldReturn_Caution()
        {
            // Arrange
            var context = new AnalysisContext
            {
                ProductIdentified = true,
                OcrComplete = true,
                AnalysisComplete = true,
                HasDeclaredAllergens = true, // Tem alérgenos
                OverallConfidenceLevel = ConfLevel.High
            };

            // Act
            var result = SummaryAdjustmentRules.AdjustClassification(
                AnalysisClassification.Safe, context, out var reason);

            // Assert
            Assert.Equal(AnalysisClassification.Caution, result);
            Assert.Contains("alérgeno", reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetDisclaimers_PartialAnalysis_ShouldReturn_Disclaimers()
        {
            // Arrange
            var context = new AnalysisContext
            {
                ProductIdentified = false,
                OcrComplete = false,
                AnalysisComplete = false,
                HasDeclaredAllergens = true,
                OverallConfidenceLevel = ConfLevel.Low
            };

            // Act
            var disclaimers = SummaryAdjustmentRules.GetDisclaimers(context);

            // Assert
            Assert.NotEmpty(disclaimers);
            Assert.True(disclaimers.Count >= 3); // Pelo menos 3 disclaimers
        }
    }
}
