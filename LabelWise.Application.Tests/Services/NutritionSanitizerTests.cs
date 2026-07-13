using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LabelWise.Application.Tests.Services
{
    public class NutritionSanitizerTests
    {
        private readonly NutritionSanitizer _sut = new(NullLogger<NutritionSanitizer>.Instance);

        [Fact]
        public void Sanitize_HallucinatedCheeseSugar_ReplacesValueAndLowersNutritionConfidence()
        {
            var response = new NutritionAnalysisResponseDto
            {
                ProductName = "Queijo Parmesão Ralado",
                Category = "Queijo parmesão",
                PackageWeight = "50g",
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g = 390,
                    EstimatedSugarPer100g = 12,
                    EstimatedProteinPer100g = 28,
                    EstimatedFatPer100g = 27,
                    EstimatedSodiumPer100g = 860,
                    EstimatedPackageCalories = 195,
                    Basis = "Estimativa baseada em análise visual"
                },
                ConfidenceDetails = new ConfidenceDetailsDto
                {
                    EstimatedNutritionProfile = 0.82
                },
                Warnings = new List<string>()
            };

            var result = _sut.Sanitize(response);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Equal(1, result.Value!.EstimatedNutritionProfile!.EstimatedSugarPer100g);
            Assert.Equal(0.25, result.Value.ConfidenceDetails!.EstimatedNutritionProfile);
            Assert.Contains(result.Value.Warnings, warning => warning.Contains("açúcar", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("higienizados", result.Value.EstimatedNutritionProfile.Basis, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Sanitize_DefaultWeight_ReplacesWithDetectedMarketWeightAndRecalculatesCalories()
        {
            var response = new NutritionAnalysisResponseDto
            {
                ProductName = "Biscoito Crocante",
                Category = "Snack salgado",
                PackageWeight = "100g",
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g = 500,
                    EstimatedPackageCalories = 500,
                    EstimatedSugarPer100g = 4,
                    EstimatedProteinPer100g = 6,
                    EstimatedFatPer100g = 24,
                    EstimatedSodiumPer100g = 420
                },
                ConfidenceDetails = new ConfidenceDetailsDto
                {
                    EstimatedNutritionProfile = 0.74
                },
                Warnings = new List<string>(),
                Summary = "Pacote individual"
            };

            var context = new NutritionSanitizationContext
            {
                RawModelResponseText = "{\"packageWeight\":\"100g\",\"summary\":\"Peso líquido detectado na frente da embalagem: 40g\"}"
            };

            var result = _sut.Sanitize(response, context);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Equal("40 g", result.Value!.PackageWeight);
            Assert.Equal(200, result.Value.EstimatedNutritionProfile!.EstimatedPackageCalories);
            Assert.Contains(result.Value.Warnings, warning => warning.Contains("unidade explicitamente detectada", StringComparison.OrdinalIgnoreCase));
        }
    }
}
