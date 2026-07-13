using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Enums;
using LabelWise.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LabelWise.Application.Tests.Services
{
    public class ProductComparisonServiceTests
    {
        [Fact]
        public async Task CompareAsync_WithPayloadsAndClearScoreGap_ReturnsProductAAsWinner()
        {
            var service = CreateService();

            var productA = new ProductComparisonAnalysisInputDto
            {
                ProductName = "Iogurte Natural",
                Brand = "Marca X",
                Category = "iogurte",
                AnalysisMode = AnalysisMode.FullNutritionLabel,
                Score = 71,
                ScoreLabel = "Boa escolha",
                PrincipalOffender = "açúcar",
                Classification = new ProductClassificationDto
                {
                    Diabetic = new HealthProfileResult { Status = "adequado", Reason = "Baixo impacto glicêmico." },
                    BloodPressure = new HealthProfileResult { Status = "adequado", Reason = "Sódio controlado." },
                    WeightLoss = new HealthProfileResult { Status = "adequado", Reason = "Boa densidade nutricional." },
                    MuscleGain = new HealthProfileResult { Status = "consumo_moderado", Reason = "Proteína moderada." }
                }
            };

            var productB = new ProductComparisonAnalysisInputDto
            {
                ProductName = "Achocolatado",
                Brand = "Marca Y",
                Category = "achocolatado",
                AnalysisMode = AnalysisMode.FrontOfPackageOnly,
                Score = 22,
                ScoreLabel = "Não recomendado",
                PrincipalOffender = "açúcar",
                Classification = new ProductClassificationDto
                {
                    Diabetic = new HealthProfileResult { Status = "nao_recomendado", Reason = "Muito açúcar." },
                    BloodPressure = new HealthProfileResult { Status = "consumo_moderado", Reason = "Sódio intermediário." },
                    WeightLoss = new HealthProfileResult { Status = "nao_recomendado", Reason = "Alta carga calórica." },
                    MuscleGain = new HealthProfileResult { Status = "fraco", Reason = "Baixa proteína." }
                }
            };

            var result = await service.CompareAsync(productA, productB);

            Assert.Equal("A", result.Winner);
            Assert.Equal("melhor_escolha_clara", result.ComparisonLevel);
            Assert.Equal(49, result.ScoreComparison.Difference);
            Assert.Equal("Prefira o Produto A", result.Recommendation);
            Assert.Equal("A", result.HealthProfileComparison["diabetic"].Winner);
            Assert.True(result.Confidence >= 0.65);
        }

        [Fact]
        public async Task CompareAsync_WithCloseScores_ReturnsTechnicalTie()
        {
            var service = CreateService();

            var productA = new ProductComparisonAnalysisInputDto
            {
                ProductName = "Produto A",
                AnalysisMode = AnalysisMode.FullNutritionLabel,
                Score = 66,
                Classification = new ProductClassificationDto
                {
                    Diabetic = new HealthProfileResult { Status = "consumo_moderado", Reason = "Atenção ao açúcar." },
                    BloodPressure = new HealthProfileResult { Status = "adequado", Reason = "Bom teor de sódio." },
                    WeightLoss = new HealthProfileResult { Status = "consumo_moderado", Reason = "Perfil intermediário." },
                    MuscleGain = new HealthProfileResult { Status = "moderado", Reason = "Proteína moderada." }
                }
            };

            var productB = new ProductComparisonAnalysisInputDto
            {
                ProductName = "Produto B",
                AnalysisMode = AnalysisMode.FullNutritionLabel,
                Score = 63,
                Classification = new ProductClassificationDto
                {
                    Diabetic = new HealthProfileResult { Status = "consumo_moderado", Reason = "Atenção ao açúcar." },
                    BloodPressure = new HealthProfileResult { Status = "adequado", Reason = "Bom teor de sódio." },
                    WeightLoss = new HealthProfileResult { Status = "consumo_moderado", Reason = "Perfil intermediário." },
                    MuscleGain = new HealthProfileResult { Status = "moderado", Reason = "Proteína moderada." }
                }
            };

            var result = await service.CompareAsync(productA, productB);

            Assert.Equal("tie", result.Winner);
            Assert.Equal("empate_tecnico", result.ComparisonLevel);
            Assert.Equal("Ambos podem ser consumidos com moderação", result.Recommendation);
            Assert.All(result.HealthProfileComparison.Values, profile => Assert.Equal("tie", profile.Winner));
        }

        private static ProductComparisonService CreateService()
        {
            return new ProductComparisonService(
                new StubAnalysisRepository(),
                new StubAnalysisHistoryRepository(),
                new TestLogger<ProductComparisonService>());
        }

        private sealed class StubAnalysisRepository : IAnalysisRepository
        {
            public Task<ProductAnalysis?> GetByIdAsync(Guid analysisId)
            {
                return Task.FromResult<ProductAnalysis?>(null);
            }

            public Task<IReadOnlyCollection<ProductAnalysis>> GetByDeviceIdAsync(string deviceId)
            {
                return Task.FromResult<IReadOnlyCollection<ProductAnalysis>>(Array.Empty<ProductAnalysis>());
            }

            public Task<IReadOnlyCollection<ProductAnalysis>> GetByUserIdAsync(Guid userId)
            {
                return Task.FromResult<IReadOnlyCollection<ProductAnalysis>>(Array.Empty<ProductAnalysis>());
            }
        }

        private sealed class StubAnalysisHistoryRepository : IAnalysisHistoryRepository
        {
            public Task<ProductComparisonAnalysisInputDto?> GetByIdAsync(Guid analysisId, Guid? userId = null)
            {
                return Task.FromResult<ProductComparisonAnalysisInputDto?>(null);
            }
        }

        private sealed class TestLogger<T> : ILogger<T>
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull
            {
                return NoopDisposable.Instance;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return false;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
            }
        }

        private sealed class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
