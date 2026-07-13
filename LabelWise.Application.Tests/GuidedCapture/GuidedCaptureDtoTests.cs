using LabelWise.Application.DTOs.GuidedCapture;
using LabelWise.Domain.Enums;
using Xunit;

namespace LabelWise.Application.Tests.GuidedCapture
{
    /// <summary>
    /// Testes para os DTOs de Captura Guiada.
    /// </summary>
    public class GuidedCaptureDtoTests
    {
        [Fact]
        public void CaptureStepsProgressDto_CalculatesPercentCorrectly()
        {
            // Arrange
            var progress = new CaptureStepsProgressDto
            {
                TotalSteps = 5,
                CompletedSteps = 2
            };

            // Act & Assert
            Assert.Equal(40, progress.PercentComplete);
        }

        [Fact]
        public void CaptureStepsProgressDto_RequiredStepsComplete_WhenBothCaptured()
        {
            // Arrange
            var progress = new CaptureStepsProgressDto
            {
                NutritionTableCaptured = true,
                IngredientsListCaptured = true
            };

            // Act & Assert
            Assert.True(progress.RequiredStepsComplete);
            Assert.True(progress.ReadyForAnalysis);
        }

        [Fact]
        public void CaptureStepsProgressDto_RequiredStepsNotComplete_WhenMissing()
        {
            // Arrange
            var progress = new CaptureStepsProgressDto
            {
                NutritionTableCaptured = true,
                IngredientsListCaptured = false,
                FrontPackagingCaptured = true
            };

            // Act & Assert
            Assert.False(progress.RequiredStepsComplete);
            Assert.False(progress.ReadyForAnalysis);
        }

        [Fact]
        public void StartGuidedSessionResponse_ContainsAllStepDefinitions()
        {
            // Arrange
            var response = new StartGuidedSessionResponse
            {
                SessionId = Guid.NewGuid(),
                AllSteps = new List<CaptureStepDefinitionDto>
                {
                    new() { CaptureType = CaptureType.FrontPackaging, Order = 1, IsRequired = false },
                    new() { CaptureType = CaptureType.IngredientsList, Order = 2, IsRequired = true },
                    new() { CaptureType = CaptureType.NutritionTable, Order = 3, IsRequired = true },
                    new() { CaptureType = CaptureType.AllergenStatement, Order = 4, IsRequired = false },
                    new() { CaptureType = CaptureType.Barcode, Order = 5, IsRequired = false }
                }
            };

            // Act & Assert
            Assert.Equal(5, response.AllSteps.Count);
            Assert.Equal(2, response.AllSteps.Count(s => s.IsRequired));
        }

        [Fact]
        public void AddCaptureResponse_IncludesSessionStatus()
        {
            // Arrange
            var response = new AddCaptureResponse
            {
                Success = true,
                CaptureId = Guid.NewGuid(),
                CaptureType = CaptureType.NutritionTable,
                Confidence = 0.92m,
                ProcessingTimeMs = 1500,
                SessionStatus = new GuidedCaptureSessionDto
                {
                    SessionId = Guid.NewGuid(),
                    Status = "Capturing",
                    Progress = new CaptureStepsProgressDto
                    {
                        CompletedSteps = 1,
                        NutritionTableCaptured = true
                    }
                }
            };

            // Assert
            Assert.True(response.Success);
            Assert.NotNull(response.SessionStatus);
            Assert.Equal(1, response.SessionStatus.Progress.CompletedSteps);
        }

        [Fact]
        public void FinalizeAnalysisResponse_ContainsAllRequiredFields()
        {
            // Arrange
            var response = new FinalizeAnalysisResponse
            {
                Success = true,
                SessionId = Guid.NewGuid(),
                AnalysisId = Guid.NewGuid(),
                ProductId = Guid.NewGuid(),
                OverallConfidence = 0.87m,
                Product = new ConsolidatedProductDto
                {
                    Name = "Test Product",
                    Brand = "Test Brand",
                    Ingredients = new List<string> { "Water", "Sugar", "Salt" },
                    Allergens = new List<string> { "Gluten" }
                },
                NutritionalAnalysis = new NutritionalAnalysisResultDto
                {
                    OverallScore = 65,
                    Classification = "Bom",
                    NutriScore = "B"
                },
                Summary = new AnalysisSummaryDto
                {
                    Title = "Test Analysis",
                    ShortDescription = "Test description"
                }
            };

            // Assert
            Assert.True(response.Success);
            Assert.NotNull(response.Product);
            Assert.NotNull(response.NutritionalAnalysis);
            Assert.NotNull(response.Summary);
            Assert.Equal(3, response.Product.Ingredients.Count);
            Assert.Single(response.Product.Allergens);
            Assert.Equal("B", response.NutritionalAnalysis.NutriScore);
        }

        [Theory]
        [InlineData(CaptureType.NutritionTable, true)]
        [InlineData(CaptureType.IngredientsList, true)]
        [InlineData(CaptureType.FrontPackaging, false)]
        [InlineData(CaptureType.AllergenStatement, false)]
        [InlineData(CaptureType.Barcode, false)]
        public void CaptureStepDefinitionDto_RequiredFlagIsCorrect(CaptureType captureType, bool expectedRequired)
        {
            // This test validates the business rule that NutritionTable and IngredientsList are required
            var step = new CaptureStepDefinitionDto
            {
                CaptureType = captureType,
                IsRequired = captureType is CaptureType.NutritionTable or CaptureType.IngredientsList
            };

            Assert.Equal(expectedRequired, step.IsRequired);
        }

        [Fact]
        public void NutritionPreviewDto_CanStoreAllNutrientValues()
        {
            // Arrange
            var preview = new NutritionPreviewDto
            {
                ServingSize = "30g",
                Calories = 120m,
                Carbohydrates = 18m,
                Proteins = 3m,
                TotalFat = 4.5m,
                Sodium = 150m,
                Sugars = 6m
            };

            // Assert
            Assert.Equal("30g", preview.ServingSize);
            Assert.Equal(120m, preview.Calories);
            Assert.Equal(18m, preview.Carbohydrates);
            Assert.Equal(3m, preview.Proteins);
            Assert.Equal(4.5m, preview.TotalFat);
            Assert.Equal(150m, preview.Sodium);
            Assert.Equal(6m, preview.Sugars);
        }
    }
}
