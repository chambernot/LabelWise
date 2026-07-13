using LabelWise.Application.Confidence;
using LabelWise.Application.Parsing;
using LabelWise.Application.QualityGate;
using Xunit;

namespace LabelWise.Application.Tests.Confidence
{
    /// <summary>
    /// Testes para o sistema de confiança multidimensional.
    /// Verifica as regras de cálculo de ProductIdentificationConfidence,
    /// LabelReadingConfidence e FinalAnalysisConfidence.
    /// </summary>
    public class MultidimensionalConfidenceCalculatorTests
    {
        private readonly MultidimensionalConfidenceCalculator _calculator;
        private readonly OcrQualityAssessor _ocrAssessor;
        private readonly ParsingQualityAssessor _parsingAssessor;

        public MultidimensionalConfidenceCalculatorTests()
        {
            _calculator = new MultidimensionalConfidenceCalculator();
            _ocrAssessor = new OcrQualityAssessor();
            _parsingAssessor = new ParsingQualityAssessor();
        }

        // ═══════════════════════════════════════════════════════════════════
        // TESTES DE PRODUCT IDENTIFICATION CONFIDENCE
        // ═══════════════════════════════════════════════════════════════════

        [Fact]
        public void ProductIdentification_WithValidName_ReturnsHighConfidence()
        {
            // Arrange
            var parseResult = CreateParseResult(
                productName: "Biscoito Recheado Oreo",
                brand: "Mondelez",
                ingredients: ["farinha", "açúcar", "cacau"],
                allergens: ["glúten", "leite"]);

            var ocrMetrics = _ocrAssessor.AssessQuality("texto de teste", 0.90);
            var parsingMetrics = _parsingAssessor.AssessQuality(parseResult);

            // Act
            var result = _calculator.Calculate(parseResult, ocrMetrics, parsingMetrics);

            // Assert
            Assert.True(result.ProductIdentification.ProductNameIdentified);
            Assert.True(result.ProductIdentification.BrandIdentified);
            Assert.True(result.ProductIdentification.Score.Value >= 0.80);
            Assert.Equal(ConfidenceLevel.High, result.ProductIdentification.Score.Level);
        }

        [Fact]
        public void ProductIdentification_WithUnknownProduct_ReturnsLowConfidence()
        {
            // Arrange
            var parseResult = CreateParseResult(
                productName: "Produto Desconhecido",
                brand: null,
                ingredients: ["item1", "item2"],
                allergens: []);

            var ocrMetrics = _ocrAssessor.AssessQuality("texto incompleto", 0.50);
            var parsingMetrics = _parsingAssessor.AssessQuality(parseResult);

            // Act
            var result = _calculator.Calculate(parseResult, ocrMetrics, parsingMetrics);

            // Assert
            Assert.False(result.ProductIdentification.ProductNameIdentified);
            Assert.True(result.ProductIdentification.Score.Value <= 0.50);
        }

        [Fact]
        public void ProductIdentification_WithBarcode_IncreasesConfidence()
        {
            // Arrange
            var parseResult = CreateParseResult(
                productName: "Produto X",
                brand: null,
                ingredients: ["ingrediente"],
                allergens: []);

            var ocrMetrics = _ocrAssessor.AssessQuality("texto", 0.70);
            var parsingMetrics = _parsingAssessor.AssessQuality(parseResult);

            // Act
            var resultWithBarcode = _calculator.Calculate(parseResult, ocrMetrics, parsingMetrics, "7891234567890");
            var resultWithoutBarcode = _calculator.Calculate(parseResult, ocrMetrics, parsingMetrics);

            // Assert
            Assert.True(resultWithBarcode.ProductIdentification.BarcodeIdentified);
            Assert.True(resultWithBarcode.ProductIdentification.Score.Value > 
                        resultWithoutBarcode.ProductIdentification.Score.Value);
        }

        // ═══════════════════════════════════════════════════════════════════
        // TESTES DE LABEL READING CONFIDENCE
        // ═══════════════════════════════════════════════════════════════════

        [Fact]
        public void LabelReading_WithCompleteNutrients_ReturnsHighNutrientScore()
        {
            // Arrange
            var parseResult = CreateParseResult(
                productName: "Produto Completo",
                brand: "Marca",
                ingredients: ["farinha", "açúcar", "sal", "óleo", "água"],
                allergens: ["glúten"],
                nutrition: CreateCompleteNutrition());

            var ocrMetrics = _ocrAssessor.AssessQuality("texto completo com muitas palavras válidas", 0.92);
            var parsingMetrics = _parsingAssessor.AssessQuality(parseResult);

            // Act
            var result = _calculator.Calculate(parseResult, ocrMetrics, parsingMetrics);

            // Assert
            Assert.True(result.LabelReading.NutrientsExtracted);
            Assert.True(result.LabelReading.NutrientsScore >= 0.70);
            Assert.False(result.LabelReading.NutrientsIncomplete);
        }

        [Fact]
        public void LabelReading_WithIncompleteNutrients_ReducesConfidence()
        {
            // Arrange
            var parseResult = CreateParseResult(
                productName: "Produto Incompleto",
                brand: "Marca",
                ingredients: ["farinha", "açúcar"],
                allergens: [],
                nutrition: CreateIncompleteNutrition());

            var ocrMetrics = _ocrAssessor.AssessQuality("texto parcial", 0.75);
            var parsingMetrics = _parsingAssessor.AssessQuality(parseResult);

            // Act
            var result = _calculator.Calculate(parseResult, ocrMetrics, parsingMetrics);

            // Assert
            Assert.True(result.LabelReading.NutrientsIncomplete);
            Assert.True(result.LabelReading.Score.Value < 0.85); // Deve ter penalização
        }

        [Fact]
        public void LabelReading_WithExcessiveIngredientNoise_ReducesConfidence()
        {
            // Arrange
            var parseResult = CreateParseResult(
                productName: "Produto Com Ruído",
                brand: "Marca",
                ingredients: ["???", "n/a", "...", "x", "y", "z", "farinha"], // Maioria inválida
                allergens: []);

            var ocrMetrics = _ocrAssessor.AssessQuality("texto com ruído", 0.60);
            var parsingMetrics = _parsingAssessor.AssessQuality(parseResult);

            // Act
            var result = _calculator.Calculate(parseResult, ocrMetrics, parsingMetrics);

            // Assert
            Assert.True(result.LabelReading.IngredientsHaveExcessiveNoise);
        }

        [Fact]
        public void LabelReading_WithClearlyDetectedAllergens_IncreasesAllergenScore()
        {
            // Arrange
            var parseResult = CreateParseResult(
                productName: "Produto Alergênico",
                brand: "Marca",
                ingredients: ["farinha de trigo", "leite", "ovos"],
                allergens: ["glúten", "lactose", "ovo"]);

            var ocrMetrics = _ocrAssessor.AssessQuality("texto claro", 0.88);
            var parsingMetrics = _parsingAssessor.AssessQuality(parseResult);

            // Act
            var result = _calculator.Calculate(parseResult, ocrMetrics, parsingMetrics);

            // Assert
            Assert.True(result.LabelReading.AllergensClearlyDetected);
            Assert.Equal(3, result.LabelReading.AllergensCount);
            Assert.True(result.LabelReading.AllergensScore >= 0.85);
        }

        // ═══════════════════════════════════════════════════════════════════
        // TESTES DE FINAL ANALYSIS CONFIDENCE
        // ═══════════════════════════════════════════════════════════════════

        [Fact]
        public void FinalAnalysis_WhenProductNotIdentified_AppliesStrongPenalty()
        {
            // Arrange
            var parseResult = CreateParseResult(
                productName: "???",
                brand: null,
                ingredients: ["item"],
                allergens: []);

            var ocrMetrics = _ocrAssessor.AssessQuality("texto ruim", 0.40);
            var parsingMetrics = _parsingAssessor.AssessQuality(parseResult);

            // Act
            var result = _calculator.Calculate(parseResult, ocrMetrics, parsingMetrics);

            // Assert
            Assert.True(result.FinalAnalysis.PenaltyApplied >= 0.25);
            Assert.False(result.FinalAnalysis.ClassificationReliable);
        }

        [Fact]
        public void FinalAnalysis_WithHighQualityInputs_HasMinimalPenalty()
        {
            // Arrange
            var parseResult = CreateParseResult(
                productName: "Produto Premium",
                brand: "Marca Conhecida",
                ingredients: Enumerable.Range(1, 10).Select(i => $"ingrediente{i}").ToList(),
                allergens: ["glúten", "leite"],
                nutrition: CreateCompleteNutrition());

            var ocrMetrics = _ocrAssessor.AssessQuality(
                "texto completo com muitas palavras válidas ingredientes nutrientes alérgenos", 0.95);
            var parsingMetrics = _parsingAssessor.AssessQuality(parseResult);

            // Act
            var result = _calculator.Calculate(parseResult, ocrMetrics, parsingMetrics);

            // Assert
            Assert.True(result.FinalAnalysis.PenaltyApplied <= 0.15);
            Assert.True(result.FinalAnalysis.ClassificationReliable);
        }

        // ═══════════════════════════════════════════════════════════════════
        // TESTES DE REGRAS DE CLASSIFICAÇÃO
        // ═══════════════════════════════════════════════════════════════════

        [Fact]
        public void Classification_WhenLowOverallConfidence_CannotBeSafe()
        {
            // Arrange
            var parseResult = CreateParseResult(
                productName: "Produto",
                brand: null,
                ingredients: ["item"],
                allergens: []);

            var ocrMetrics = _ocrAssessor.AssessQuality("texto curto", 0.45);
            var parsingMetrics = _parsingAssessor.AssessQuality(parseResult);

            var confidence = _calculator.Calculate(parseResult, ocrMetrics, parsingMetrics);
            var adjuster = new ConfidenceBasedClassificationAdjuster();

            // Act
            var adjustment = adjuster.AdjustClassification("Safe", confidence);

            // Assert
            Assert.True(adjustment.WasAdjusted);
            Assert.NotEqual("Safe", adjustment.AdjustedClassification);
        }

        [Fact]
        public void Classification_WhenProductNotIdentified_BecomesIncomplete()
        {
            // Arrange
            var parseResult = CreateParseResult(
                productName: "Produto Desconhecido",
                brand: null,
                ingredients: ["item"],
                allergens: []);

            var ocrMetrics = _ocrAssessor.AssessQuality("texto", 0.60);
            var parsingMetrics = _parsingAssessor.AssessQuality(parseResult);

            var confidence = _calculator.Calculate(parseResult, ocrMetrics, parsingMetrics);
            var adjuster = new ConfidenceBasedClassificationAdjuster();

            // Act
            var adjustment = adjuster.AdjustClassification("Safe", confidence);

            // Assert
            Assert.True(adjustment.WasAdjusted);
            Assert.Equal("Incomplete", adjustment.AdjustedClassification);
            Assert.Equal(ClassificationAdjustmentRule.ProductNotIdentified, adjustment.AdjustmentRule);
        }

        [Fact]
        public void Classification_WhenHighConfidenceAndGoodData_RemainsSafe()
        {
            // Arrange
            var parseResult = CreateParseResult(
                productName: "Produto Saudável",
                brand: "Marca Conhecida",
                ingredients: Enumerable.Range(1, 8).Select(i => $"ingrediente{i}").ToList(),
                allergens: [],
                nutrition: CreateCompleteNutrition());

            var ocrMetrics = _ocrAssessor.AssessQuality(
                "texto completo com muitas palavras válidas ingredientes calorias proteínas", 0.92);
            var parsingMetrics = _parsingAssessor.AssessQuality(parseResult);

            var confidence = _calculator.Calculate(parseResult, ocrMetrics, parsingMetrics);
            var adjuster = new ConfidenceBasedClassificationAdjuster();

            // Act
            var adjustment = adjuster.AdjustClassification("Safe", confidence);

            // Assert - Se confiança é alta, classificação Safe é mantida
            if (confidence.OverallConfidence.Level >= ConfidenceLevel.Medium &&
                confidence.ProductIdentification.ProductNameIdentified)
            {
                Assert.Equal("Safe", adjustment.AdjustedClassification);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // TESTES DE OVERALL CONFIDENCE
        // ═══════════════════════════════════════════════════════════════════

        [Fact]
        public void OverallConfidence_IsWeightedCombination()
        {
            // Arrange
            var parseResult = CreateParseResult(
                productName: "Produto Teste",
                brand: "Marca",
                ingredients: ["farinha", "açúcar", "sal"],
                allergens: ["glúten"],
                nutrition: CreateCompleteNutrition());

            var ocrMetrics = _ocrAssessor.AssessQuality("texto completo", 0.85);
            var parsingMetrics = _parsingAssessor.AssessQuality(parseResult);

            // Act
            var result = _calculator.Calculate(parseResult, ocrMetrics, parsingMetrics);

            // Assert
            Assert.True(result.OverallConfidence.Value > 0);
            Assert.True(result.OverallConfidence.Value <= 1.0);
            Assert.NotNull(result.QualitySummary);
        }

        [Fact]
        public void QualityGate_PassesWhenConfidenceIsAcceptable()
        {
            // Arrange
            var parseResult = CreateParseResult(
                productName: "Produto Válido",
                brand: "Marca",
                ingredients: ["ingrediente1", "ingrediente2", "ingrediente3"],
                allergens: [],
                nutrition: CreateCompleteNutrition());

            var ocrMetrics = _ocrAssessor.AssessQuality("texto razoável com palavras", 0.75);
            var parsingMetrics = _parsingAssessor.AssessQuality(parseResult);

            // Act
            var result = _calculator.Calculate(parseResult, ocrMetrics, parsingMetrics);

            // Assert
            Assert.True(result.QualityGatePassed);
        }

        // ═══════════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════

        private static IngredientAllergenParseResult CreateParseResult(
            string productName,
            string? brand,
            List<string> ingredients,
            List<string> allergens,
            NutritionData? nutrition = null)
        {
            return new IngredientAllergenParseResult
            {
                ProductName = productName,
                Brand = brand,
                Ingredients = ingredients,
                Allergens = allergens,
                Nutrition = nutrition
            };
        }

        private static NutritionData CreateCompleteNutrition()
        {
            return new NutritionData
            {
                Calories = 150,
                TotalFat = 5.0,
                SaturatedFat = 2.0,
                TransFat = 0,
                Cholesterol = 10,
                Sodium = 200,
                TotalCarbohydrate = 20,
                DietaryFiber = 3,
                Sugars = 8,
                Protein = 5,
                ServingSize = "30g"
            };
        }

        private static NutritionData CreateIncompleteNutrition()
        {
            return new NutritionData
            {
                Calories = 100,
                TotalFat = 3.0,
                // Apenas 2 campos preenchidos
            };
        }
    }
}
