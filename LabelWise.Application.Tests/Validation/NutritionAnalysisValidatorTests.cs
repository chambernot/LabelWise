using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Validation;
using LabelWise.Domain.Enums;
using Xunit;

namespace LabelWise.Application.Tests.Validation
{
    public class NutritionAnalysisValidatorTests
    {
        [Fact]
        public void Apply_GenericCookieProductName_EnrichesNameFromCategoryAndClaims()
        {
            var response = new NutritionAnalysisResponseDto
            {
                ProductName = "Biscoito",
                Brand = "Marca X",
                Category = "biscoito recheado",
                AnalysisMode = AnalysisMode.FrontOfPackageOnly,
                VisibleClaims = new List<string> { "Recheado" }
            };

            NutritionAnalysisValidator.Apply(response);

            Assert.Equal("Biscoito Recheado", response.ProductName);
        }

        [Fact]
        public void Apply_GenericCookieProductName_EnrichesNameFromCategoryEvenWithoutClaims()
        {
            var response = new NutritionAnalysisResponseDto
            {
                ProductName = "Biscoito",
                Brand = "Marca X",
                Category = "biscoito recheado",
                AnalysisMode = AnalysisMode.FrontOfPackageOnly,
                VisibleClaims = new List<string>()
            };

            NutritionAnalysisValidator.Apply(response);

            Assert.Equal("Biscoito Recheado", response.ProductName);
        }

        [Fact]
        public void Apply_ProductNameRepeatedFromBrand_UsesCategorySpecificFallback()
        {
            var response = new NutritionAnalysisResponseDto
            {
                ProductName = "Marca X",
                Brand = "Marca X",
                Category = "iogurte proteico",
                AnalysisMode = AnalysisMode.FrontOfPackageOnly,
                VisibleClaims = new List<string> { "15g protein" }
            };

            NutritionAnalysisValidator.Apply(response);

            Assert.Equal("Iogurte Proteico", response.ProductName);
        }

        [Fact]
        public void Apply_MissingSummary_GeneratesMoreInformativeSummary()
        {
            var response = new NutritionAnalysisResponseDto
            {
                ProductName = "Achocolatado",
                Brand = "Marca X",
                Category = "achocolatado em pó",
                AnalysisMode = AnalysisMode.FrontOfPackageOnly,
                VisibleClaims = new List<string> { "Vitaminas A, C e D" }
            };

            NutritionAnalysisValidator.Apply(response);

            Assert.Equal("Achocolatado em Pó Fortificado", response.ProductName);
            Assert.False(string.IsNullOrWhiteSpace(response.Summary));
            Assert.Contains("Achocolatado em Pó Fortificado", response.Summary);
            Assert.Contains("perfil", response.Summary, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
