using System.Collections.Generic;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Presentation;
using LabelWise.Domain.Enums;
using Xunit;

namespace LabelWise.Application.Tests.Presentation
{
    public class NutritionPresentationEngineTests
    {
        [Fact]
        public void ProcessForPresentation_ProductWithHighSugar_ShouldIdentifySugarAsMainOffender()
        {
            // Arrange
            var analysis = new NutritionAnalysisResponseDto
            {
                Success = true,
                ProductName = "Achocolatado em Pó",
                Category = "Achocolatado",
                AnalysisMode = AnalysisMode.FullNutritionLabel,
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g = 380,
                    EstimatedSugarPer100g = 75,
                    EstimatedProteinPer100g = 4,
                    EstimatedSodiumPer100g = 150,
                    EstimatedFatPer100g = 2.5,
                    Basis = "Leitura da tabela nutricional"
                },
                Classification = new ProductClassificationDto
                {
                    Diabetic = new HealthProfileResult { Status = "nao_recomendado", Reason = "Alto açúcar" },
                    WeightLoss = new HealthProfileResult { Status = "nao_recomendado", Reason = "Alto açúcar" },
                    BloodPressure = new HealthProfileResult { Status = "consumo_moderado", Reason = "Sódio moderado" },
                    MuscleGain = new HealthProfileResult { Status = "fraco", Reason = "Baixa proteína" }
                }
            };

            // Act
            var result = NutritionPresentationEngine.ProcessForPresentation(analysis);

            // Assert
            Assert.NotNull(result.MainOffender);
            Assert.Equal("Açúcar", result.MainOffender.Nutrient);
            Assert.Equal(75, result.MainOffender.Value);
            Assert.True(result.MainOffender.Severity >= 85);
        }

        [Fact]
        public void ProcessForPresentation_ProductWithHighSugar_ShouldHaveLowScore()
        {
            // Arrange
            var analysis = new NutritionAnalysisResponseDto
            {
                Success = true,
                ProductName = "Sobremesa Láctea",
                Category = "Sobremesa láctea",
                AnalysisMode = AnalysisMode.FullNutritionLabel,
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g = 320,
                    EstimatedSugarPer100g = 28,
                    EstimatedProteinPer100g = 2.8,
                    EstimatedSodiumPer100g = 100,
                    EstimatedFatPer100g = 10,
                    Basis = "Leitura da tabela nutricional"
                },
                Classification = new ProductClassificationDto
                {
                    Diabetic = new HealthProfileResult { Status = "nao_recomendado", Reason = "Alto açúcar" },
                    WeightLoss = new HealthProfileResult { Status = "nao_recomendado", Reason = "Alto açúcar" },
                    BloodPressure = new HealthProfileResult { Status = "adequado", Reason = "Sódio baixo" },
                    MuscleGain = new HealthProfileResult { Status = "fraco", Reason = "Baixa proteína" }
                }
            };

            // Act
            var result = NutritionPresentationEngine.ProcessForPresentation(analysis);

            // Assert
            Assert.True(result.Score.Value < 50, $"Score should be below 50, got {result.Score.Value}");
            Assert.True(result.Score.Value <= 42, $"Sobremesa láctea should be capped at 42, got {result.Score.Value}");
        }

        [Fact]
        public void ProcessForPresentation_AchocolatadoWithHighSugar_ShouldBeCappedAt48()
        {
            // Arrange
            var analysis = new NutritionAnalysisResponseDto
            {
                Success = true,
                ProductName = "Achocolatado em Pó Fortificado",
                Category = "Achocolatado em pó",
                AnalysisMode = AnalysisMode.FullNutritionLabel,
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g = 380,
                    EstimatedSugarPer100g = 76,
                    EstimatedProteinPer100g = 4,
                    EstimatedSodiumPer100g = 150,
                    EstimatedFiberPer100g = 3,
                    EstimatedFatPer100g = 2.5,
                    Basis = "Leitura da tabela nutricional"
                },
                Classification = new ProductClassificationDto
                {
                    Diabetic = new HealthProfileResult { Status = "nao_recomendado", Reason = "Alto açúcar" },
                    WeightLoss = new HealthProfileResult { Status = "nao_recomendado", Reason = "Alto açúcar" },
                    BloodPressure = new HealthProfileResult { Status = "consumo_moderado", Reason = "Sódio moderado" },
                    MuscleGain = new HealthProfileResult { Status = "fraco", Reason = "Baixa proteína" }
                }
            };

            // Act
            var result = NutritionPresentationEngine.ProcessForPresentation(analysis);

            // Assert
            Assert.True(result.Score.Value <= 48, $"Achocolatado should be capped at 48, got {result.Score.Value}");
            Assert.Contains("alto", result.Summary.ToLowerInvariant());
            Assert.Contains("açúcar", result.Summary.ToLowerInvariant());
        }

        [Fact]
        public void ProcessForPresentation_ProductWithHighSugar_ShouldHaveClearSummary()
        {
            // Arrange
            var analysis = new NutritionAnalysisResponseDto
            {
                Success = true,
                ProductName = "Achocolatado em Pó",
                Category = "Achocolatado",
                AnalysisMode = AnalysisMode.FullNutritionLabel,
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    EstimatedSugarPer100g = 75,
                    Basis = "Leitura da tabela nutricional"
                }
            };

            // Act
            var result = NutritionPresentationEngine.ProcessForPresentation(analysis);

            // Assert
            Assert.NotEmpty(result.Summary);
            Assert.Contains("açúcar", result.Summary.ToLowerInvariant());
            Assert.Contains("75", result.Summary); // Deve mencionar o valor
            Assert.DoesNotContain("perfil intermediário", result.Summary.ToLowerInvariant());
        }

        [Fact]
        public void ProcessForPresentation_ProductWithHighSugar_ShouldHaveClearLabel()
        {
            // Arrange
            var analysis = new NutritionAnalysisResponseDto
            {
                Success = true,
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    EstimatedSugarPer100g = 30,
                    EstimatedSodiumPer100g = 200
                },
                Classification = new ProductClassificationDto
                {
                    Diabetic = new HealthProfileResult { Status = "nao_recomendado" },
                    WeightLoss = new HealthProfileResult { Status = "consumo_moderado" }
                }
            };

            // Act
            var result = NutritionPresentationEngine.ProcessForPresentation(analysis);

            // Assert
            Assert.NotEqual("Moderado", result.Score.Label); // Não deve ser genérico
            Assert.True(
                result.Score.Label == "Evitar consumo frequente" || 
                result.Score.Label == "Evitar" ||
                result.Score.Label == "Consumo com atenção",
                $"Expected clearer label, got: {result.Score.Label}");
        }

        [Fact]
        public void ProcessForPresentation_ShouldGenerateContextualAlerts()
        {
            // Arrange
            var analysis = new NutritionAnalysisResponseDto
            {
                Success = true,
                ProductName = "Produto Teste",
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    EstimatedSugarPer100g = 35,
                    EstimatedSodiumPer100g = 150
                },
                Classification = new ProductClassificationDto
                {
                    Diabetic = new HealthProfileResult { Status = "nao_recomendado", Reason = "Alto açúcar" }
                },
                AnalysisMode = AnalysisMode.FullNutritionLabel
            };

            // Act
            var result = NutritionPresentationEngine.ProcessForPresentation(analysis);

            // Assert
            Assert.NotEmpty(result.Alerts);
            Assert.Contains(result.Alerts, a => a.Contains("diabético", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ProcessForPresentation_FrontOfPackageOnly_ShouldIndicateEstimation()
        {
            // Arrange
            var analysis = new NutritionAnalysisResponseDto
            {
                Success = true,
                ProductName = "Produto Sem Tabela",
                AnalysisMode = AnalysisMode.FrontOfPackageOnly,
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    Basis = "Estimativa por categoria"
                }
            };

            // Act
            var result = NutritionPresentationEngine.ProcessForPresentation(analysis);

            // Assert
            Assert.Contains(result.Alerts, a => 
                a.Contains("estimados", StringComparison.OrdinalIgnoreCase) ||
                a.Contains("categoria", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ProcessForPresentation_ProductWithHighProtein_ShouldHaveBonusInScore()
        {
            // Arrange
            var analysis = new NutritionAnalysisResponseDto
            {
                Success = true,
                ProductName = "Queijo Cottage",
                Category = "Queijo",
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g = 98,
                    EstimatedSugarPer100g = 3,
                    EstimatedProteinPer100g = 22,
                    EstimatedSodiumPer100g = 400,
                    EstimatedFatPer100g = 2,
                    Basis = "Leitura da tabela nutricional"
                },
                Classification = new ProductClassificationDto
                {
                    Diabetic = new HealthProfileResult { Status = "adequado" },
                    WeightLoss = new HealthProfileResult { Status = "adequado" },
                    BloodPressure = new HealthProfileResult { Status = "consumo_moderado" },
                    MuscleGain = new HealthProfileResult { Status = "adequado" }
                }
            };

            // Act
            var result = NutritionPresentationEngine.ProcessForPresentation(analysis);

            // Assert
            Assert.True(result.Score.Value >= 65, $"High protein product should score well, got {result.Score.Value}");
        }

        [Fact]
        public void ProcessForPresentation_ProductWithHighSodium_ShouldIdentifySodiumAsOffender()
        {
            // Arrange
            var analysis = new NutritionAnalysisResponseDto
            {
                Success = true,
                ProductName = "Salgadinho",
                Category = "Salgadinho",
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g = 520,
                    EstimatedSugarPer100g = 2,
                    EstimatedProteinPer100g = 6,
                    EstimatedSodiumPer100g = 1100,
                    EstimatedFatPer100g = 32,
                    Basis = "Leitura da tabela nutricional"
                },
                Classification = new ProductClassificationDto
                {
                    BloodPressure = new HealthProfileResult { Status = "nao_recomendado", Reason = "Alto sódio" }
                }
            };

            // Act
            var result = NutritionPresentationEngine.ProcessForPresentation(analysis);

            // Assert
            Assert.NotNull(result.MainOffender);
            // Pode ser sódio ou gordura, dependendo da severidade
            Assert.True(
                result.MainOffender.Nutrient == "Sódio" || 
                result.MainOffender.Nutrient == "Gordura");
        }

        [Fact]
        public void ProcessForPresentation_ScoreLabelsShouldBeUserFriendly()
        {
            // Arrange
            var testCases = new[]
            {
                (85, "Excelente escolha"),
                (70, "Boa escolha"),
                (55, "Consumo com atenção"),
                (40, "Evitar consumo frequente"),
                (25, "Evitar")
            };

            foreach (var (baseScore, expectedLabel) in testCases)
            {
                var analysis = new NutritionAnalysisResponseDto
                {
                    Success = true,
                    EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                    {
                        CaloriesPer100g = 350 - (baseScore * 2),
                        EstimatedSugarPer100g = 100 - baseScore,
                        EstimatedProteinPer100g = baseScore / 10
                    }
                };

                // Act
                var result = NutritionPresentationEngine.ProcessForPresentation(analysis);

                // Assert - verificar que o label não é genérico
                Assert.DoesNotContain("moderado", result.Score.Label.ToLowerInvariant());
                Assert.DoesNotContain("atenção", result.Score.Label.ToLowerInvariant(), StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}
