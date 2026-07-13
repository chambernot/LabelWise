using LabelWise.Application.DTOs.AI;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Enums;
using LabelWise.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LabelWise.Application.Tests.Services;

public class NutritionAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeProductImageAsync_BiscoitoRecheado_ReturnsMobileFriendlyPostProcessing()
    {
        var service = CreateService(
            new StubVisualInterpreter(new VisualInterpretationResult
            {
                ProductName = "Biscoito",
                Category = "biscoito recheado",
                VisibleClaims = new List<string>(),
                ProbableCaptureType = CaptureType.FrontPackaging
            }));

        var response = await service.AnalyzeProductImageAsync(new byte[] { 1, 2, 3 }, "biscoito.jpg");

        Assert.True(response.Success);
        Assert.Equal("Biscoito Recheado", response.ProductName);
        Assert.NotNull(response.Score);
        Assert.NotNull(response.Summary);
        Assert.Contains("Biscoito Recheado", response.Summary!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("consumo frequente", response.Summary!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HealthScore representa", response.Score!.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("O campo Score", response.Score.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(response.Warnings);
        Assert.Equal("Análise estimada com base na frente da embalagem e na categoria.", response.Warnings[0]);
        Assert.Contains("açúcar", response.Warnings[1], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("calórica", response.Warnings[2], StringComparison.OrdinalIgnoreCase);
        Assert.All(
            response.Warnings.Where(w => w.Contains("proteína", StringComparison.OrdinalIgnoreCase)),
            warning => Assert.Contains("massa", warning, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnalyzeProductImageAsync_WhiteRice_ReturnsHumanFriendlySummaryRelevantWarningsAndSafePackageCalories()
    {
        var service = CreateService(
            new StubVisualInterpreter(new VisualInterpretationResult
            {
                ProductName = "Arroz",
                Category = "arroz branco",
                VisibleClaims = new List<string> { "Tipo 1" },
                ProbableCaptureType = CaptureType.NutritionTable,
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g = 360,
                    EstimatedPackageCalories = 1800,
                    EstimatedSugarPer100g = 0.5,
                    EstimatedProteinPer100g = 7,
                    EstimatedSodiumPer100g = 5,
                    EstimatedFiberPer100g = 1,
                    EstimatedFatPer100g = 0.5,
                    Basis = "Tabela nutricional identificada na embalagem"
                },
                Classification = new ProductClassificationDto
                {
                    Diabetic = new HealthProfileResult { Status = "consumo_moderado", Reason = "Fonte de carboidratos." },
                    WeightLoss = new HealthProfileResult { Status = "consumo_moderado", Reason = "A porção influencia a refeição." },
                    BloodPressure = new HealthProfileResult { Status = "adequado", Reason = "Baixo sódio." },
                    MuscleGain = new HealthProfileResult { Status = "consumo_moderado", Reason = "Fonte energética." }
                }
            }));

        var response = await service.AnalyzeProductImageAsync(new byte[] { 1, 2, 3 }, "arroz.jpg");

        Assert.True(response.Success);
        Assert.Equal("Arroz Branco Tipo 1", response.ProductName);
        Assert.NotNull(response.Score);
        Assert.NotNull(response.Summary);
        Assert.Contains("perfil nutricional intermediário", response.Summary!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("baixo teor de sódio", response.Summary!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("controle glicêmico", response.Summary!, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(response.EstimatedNutritionProfile);
        Assert.Null(response.EstimatedNutritionProfile!.EstimatedPackageCalories);
        Assert.Contains("cru, cozido ou porção", response.EstimatedNutritionProfile.Basis, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(response.Warnings);
        Assert.Contains("fibr", response.Warnings[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("glic", response.Warnings[1], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(response.Warnings, warning => warning.Contains("ganho de massa", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(response.Warnings, warning => warning.Contains("proteína", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnalyzeProductImageAsync_GenericIntermediateProfile_PreservesSpecificSummaryAndAvoidsOverpenalization()
    {
        var azureSummary = "Produto com baixo teor de gordura e sódio, açúcar moderado, proteína moderada e perfis entre adequado e consumo moderado.";

        var service = CreateService(
            new StubVisualInterpreter(new VisualInterpretationResult
            {
                ProductName = "Bebida Láctea",
                Category = "bebida láctea fermentada",
                Summary = azureSummary,
                ProbableCaptureType = CaptureType.NutritionTable,
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g = 92,
                    EstimatedSugarPer100g = 12,
                    EstimatedProteinPer100g = 5.2,
                    EstimatedSodiumPer100g = 65,
                    EstimatedFiberPer100g = 0,
                    EstimatedFatPer100g = 2.1,
                    Basis = "Tabela nutricional identificada na embalagem"
                },
                Classification = new ProductClassificationDto
                {
                    Diabetic = new HealthProfileResult { Status = "consumo_moderado", Reason = "Açúcar moderado para a categoria." },
                    BloodPressure = new HealthProfileResult { Status = "adequado", Reason = "Baixo teor de sódio." },
                    WeightLoss = new HealthProfileResult { Status = "adequado", Reason = "Baixa densidade calórica e gordura controlada." },
                    MuscleGain = new HealthProfileResult { Status = "consumo_moderado", Reason = "Proteína moderada para a categoria." }
                }
            }));

        var response = await service.AnalyzeProductImageAsync(new byte[] { 1, 2, 3 }, "produto.jpg");

        Assert.True(response.Success);
        Assert.NotNull(response.Score);
        Assert.Equal("atencao", response.Score!.Status);
        Assert.Equal("Escolha razoável", response.Score.Label);
        Assert.Equal(azureSummary, response.Summary);
        Assert.NotEmpty(response.Warnings);
        Assert.Contains(response.Warnings, warning => warning.Contains("açúcar", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(response.Warnings, warning => warning.Contains("proteína", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(response.Warnings, warning => warning.Contains("massa", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnalyzeProductImageAsync_FrontOfPackageCheese_PreservesSanitizedProfileAfterHybridInference()
    {
        var service = CreateService(
            new StubVisualInterpreter(new VisualInterpretationResult
            {
                ProductName = "Queijo Parmesão Ralado Grosso",
                Brand = "Produto Public",
                Category = "queijo parmesão ralado",
                PackageWeight = "100 g",
                ProbableCaptureType = CaptureType.FrontPackaging,
                VisibleClaims = new List<string> { "Nova Receita", "Grosso" },
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g = 250,
                    EstimatedPackageCalories = 250,
                    EstimatedSugarPer100g = 10,
                    EstimatedProteinPer100g = 5,
                    EstimatedSodiumPer100g = 300,
                    EstimatedFiberPer100g = 2,
                    EstimatedFatPer100g = 10,
                    Basis = "Estimativa padronizada por 100g para a categoria (tabela nutricional não visível)"
                },
                ConfidenceDetails = new ConfidenceDetailsDto
                {
                    ProductIdentification = 0.9,
                    VisibleClaimsExtraction = 0.6,
                    EstimatedNutritionProfile = 0.4,
                    Classification = 0.7
                },
                Summary = "Queijo Parmesão Ralado Grosso tem um perfil nutricional intermediário, principalmente por açúcar moderado. Resumo estimado a partir da categoria e da frente da embalagem."
            }));

        var response = await service.AnalyzeProductImageAsync(new byte[] { 1, 2, 3 }, "parmesao.jpg");

        Assert.True(response.Success);
        Assert.NotNull(response.EstimatedNutritionProfile);
        Assert.Equal(1, response.EstimatedNutritionProfile!.EstimatedSugarPer100g);
        Assert.Equal(24, response.EstimatedNutritionProfile.EstimatedProteinPer100g);
        Assert.Equal(24, response.EstimatedNutritionProfile.EstimatedFatPer100g);
        Assert.Equal(650, response.EstimatedNutritionProfile.EstimatedSodiumPer100g);
        Assert.Equal(320, response.EstimatedNutritionProfile.CaloriesPer100g);
        Assert.Equal(0.25, response.ConfidenceDetails!.EstimatedNutritionProfile);
        Assert.Contains(response.Warnings, warning => warning.Contains("faixa esperada", StringComparison.OrdinalIgnoreCase));
        Assert.StartsWith("Nota: Estas são estimativas baseadas na categoria do produto, pois as informações nutricionais não estão totalmente visíveis na foto.", response.Summary);
        Assert.DoesNotContain("açúcar moderado", response.Summary ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static NutritionAnalysisService CreateService(IVisualInterpreter visualInterpreter)
    {
        return new NutritionAnalysisService(
            visualInterpreter,
            new NutritionSanitizer(new TestLogger<NutritionSanitizer>()),
            new StubCategoryNormalizationService(),
            new StubDatabaseNutritionFallbackService(),
            new StubScoreInterpretationService(),
            new StubProductRepository(),
            new StubAnalysisWriteRepository(),
            new TestLogger<NutritionAnalysisService>());
    }

    private sealed class StubCategoryNormalizationService : ICategoryNormalizationService
    {
        public Task<CategoryNormalizationResult> NormalizeAsync(string? detectedCategory, string? productName, IEnumerable<string>? visibleClaims = null, string? brand = null)
        {
            return Task.FromResult(new CategoryNormalizationResult
            {
                IsNormalized = !string.IsNullOrWhiteSpace(detectedCategory),
                NormalizedCategoryCode = detectedCategory,
                NormalizedCategoryName = detectedCategory,
                RawInput = detectedCategory,
                Confidence = 0
            });
        }
    }

    private sealed class StubDatabaseNutritionFallbackService : IDatabaseNutritionFallbackService
    {
        public Task<DatabaseFallbackResult> ApplyFallbackAsync(EstimatedNutritionProfileDto? partialNutrition, string? normalizedCategoryCode, string analysisMode)
        {
            return Task.FromResult(new DatabaseFallbackResult
            {
                Profile = partialNutrition ?? new EstimatedNutritionProfileDto(),
                RequestedCategoryCode = normalizedCategoryCode,
                NormalizedCategoryCode = normalizedCategoryCode,
                NormalizedCategoryName = normalizedCategoryCode
            });
        }
    }

    private sealed class StubScoreInterpretationService : IScoreInterpretationService
    {
        public string DetermineProcessingLevel(string? category, IEnumerable<string>? visibleClaims, string? productName) => "processado";

        public ScoreInterpretationResult Interpret(NutritionAnalysisContext context)
        {
            return new ScoreInterpretationResult
            {
                Label = "Consumo moderado",
                SafeLabel = "Consumo moderado",
                Status = "consumo_moderado",
                Color = "yellow",
                RecommendationLevel = "consumo_moderado",
                AbsoluteRecommendation = "Melhor consumir com moderação."
            };
        }

        public NutritionalScore BuildSafeScoreLabel(ScoreInterpretationContext context)
        {
            return new NutritionalScore { Value = context.Score, Label = "moderado", Status = "atencao" };
        }

        public string BuildAbsoluteRecommendation(ScoreInterpretationContext context) => string.Empty;

        public string BuildComparativeRecommendation(ScoreInterpretationContext primary, ScoreInterpretationContext secondary, bool isPrimaryWinner, bool isTie) => string.Empty;

        public bool ShouldCapPositiveLabel(ScoreInterpretationContext context, string processingLevel) => false;

        public string BuildScoreReason(ScoreInterpretationContext context) => string.Empty;
    }

    private sealed class StubProductRepository : IProductRepository
    {
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);

        public Task AddAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubAnalysisWriteRepository : IAnalysisWriteRepository
    {
        public Task AddAsync(ProductAnalysis analysis, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(ProductAnalysis analysis, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubVisualInterpreter(VisualInterpretationResult result) : IVisualInterpreter
    {
        public Task<VisualInterpretationResult> InterpretImageAsync(VisualInterpretationRequest request)
        {
            return Task.FromResult(result);
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
