using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LabelWise.Tests.Services;

/// <summary>
/// Testes do Sistema de Scoring Nutricional V2
/// 
/// CASOS DE TESTE:
/// 1. Produto com sódio crítico (> 3000mg) → Score ~5-10
/// 2. Produto com sódio alto (1500-3000mg) → Score ~20-40
/// 3. Produto saudável (baixo sódio, alto proteína) → Score ~80-100
/// 4. Produto ultraprocessado → Penalidade adicional
/// </summary>
public class NutritionScoringServiceV2Tests
{
    private readonly NutritionScoringServiceV2 _scoringService;

    public NutritionScoringServiceV2Tests()
    {
        _scoringService = new NutritionScoringServiceV2(
            NullLogger<NutritionScoringServiceV2>.Instance);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CASO 1: SÓDIO CRÍTICO (3084mg/100g) - SEU CASO REAL
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Calculate_ProductWithCriticalSodium_ShouldReturnVeryLowScore()
    {
        // Arrange: Produto com 3084mg de sódio (seu caso real)
        var enriched = CreateEnrichedData(
            calories: 105,
            carbs: 19,
            sugar: 0,
            addedSugar: 0,
            protein: 6.5,
            fat: 0,
            satFat: 0,
            sodium: 3084,  // ⚠️ CRÍTICO
            fiber: 2);

        // Act
        var result = _scoringService.Calculate(enriched);

        // Assert
        Assert.InRange(result.Value, 0, 15);  // Score MUITO BAIXO
        Assert.Equal("Muito ruim", result.Label);
        Assert.Equal("#dc3545", result.Color);  // Vermelho
        Assert.Equal("sódio", result.PrincipalOffender);
        Assert.Contains(result.Warnings, w => w.Contains("SÓDIO MUITO ALTO"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // CASO 2: SÓDIO ALTO (1500mg) - EVITAR
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Calculate_ProductWithHighSodium_ShouldReturnLowScore()
    {
        // Arrange
        var enriched = CreateEnrichedData(
            calories: 200,
            carbs: 30,
            sugar: 5,
            protein: 10,
            fat: 5,
            satFat: 1,
            sodium: 1500,  // Alto
            fiber: 3);

        // Act
        var result = _scoringService.Calculate(enriched);

        // Assert
        Assert.InRange(result.Value, 20, 40);  // Score BAIXO
        Assert.Equal("Evitar", result.Label);
        Assert.Equal("sódio", result.PrincipalOffender);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CASO 3: SÓDIO MODERADO (600mg) - ATENÇÃO
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Calculate_ProductWithModerateSodium_ShouldReturnCautionScore()
    {
        // Arrange
        var enriched = CreateEnrichedData(
            calories: 180,
            carbs: 25,
            sugar: 3,
            protein: 8,
            fat: 4,
            satFat: 1.5,
            sodium: 600,  // Moderado
            fiber: 4);

        // Act
        var result = _scoringService.Calculate(enriched);

        // Assert
        Assert.InRange(result.Value, 40, 70);  // Score MODERADO
        Assert.Contains(new[] { "Atenção", "Bom" }, label => label == result.Label);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CASO 4: PRODUTO SAUDÁVEL - EXCELENTE
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Calculate_HealthyProduct_ShouldReturnExcellentScore()
    {
        // Arrange: Produto ideal (baixo sódio, alto proteína, alta fibra)
        var enriched = CreateEnrichedData(
            calories: 120,
            carbs: 15,
            sugar: 2,
            addedSugar: 0,
            protein: 20,  // Alto
            fat: 3,
            satFat: 0.5,
            sodium: 100,  // Baixo
            fiber: 6);    // Alto

        // Act
        var result = _scoringService.Calculate(enriched);

        // Assert
        Assert.InRange(result.Value, 80, 100);  // Score ALTO
        Assert.Equal("Excelente", result.Label);
        Assert.Equal("#28a745", result.Color);  // Verde escuro
        Assert.Contains(result.Highlights, h => h.Contains("proteína"));
        Assert.Contains(result.Highlights, h => h.Contains("fibra"));
        Assert.Contains(result.Highlights, h => h.Contains("sódio"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // CASO 5: AÇÚCAR CRÍTICO
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Calculate_ProductWithCriticalSugar_ShouldReturnVeryLowScore()
    {
        // Arrange: Produto com açúcar muito alto
        var enriched = CreateEnrichedData(
            calories: 400,
            carbs: 80,
            sugar: 50,  // ⚠️ CRÍTICO
            addedSugar: 45,
            protein: 2,
            fat: 2,
            satFat: 1,
            sodium: 200,
            fiber: 1);

        // Act
        var result = _scoringService.Calculate(enriched);

        // Assert
        Assert.InRange(result.Value, 0, 20);  // Score MUITO BAIXO
        Assert.Equal("Muito ruim", result.Label);
        Assert.Equal("açúcar", result.PrincipalOffender);
        Assert.Contains(result.Warnings, w => w.Contains("AÇÚCAR MUITO ALTO"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // CASO 6: GORDURA SATURADA CRÍTICA
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Calculate_ProductWithCriticalSatFat_ShouldReturnLowScore()
    {
        // Arrange: Produto com gordura saturada muito alta
        var enriched = CreateEnrichedData(
            calories: 600,
            carbs: 10,
            sugar: 2,
            protein: 5,
            fat: 50,
            satFat: 40,  // ⚠️ CRÍTICO
            sodium: 200,
            fiber: 1);

        // Act
        var result = _scoringService.Calculate(enriched);

        // Assert
        Assert.InRange(result.Value, 0, 30);  // Score BAIXO
        Assert.Contains(new[] { "Muito ruim", "Evitar" }, label => label == result.Label);
        Assert.Equal("gordura saturada", result.PrincipalOffender);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CASO 7: ULTRAPROCESSADO
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Calculate_UltraprocessedProduct_ShouldApplyPenalty()
    {
        // Arrange: Produto moderado MAS ultraprocessado
        var enriched = CreateEnrichedData(
            calories: 200,
            carbs: 30,
            sugar: 8,
            protein: 5,
            fat: 6,
            satFat: 2,
            sodium: 400,
            fiber: 2);
        enriched.ProcessingLevel = "ultraprocessado";

        // Act
        var result = _scoringService.Calculate(enriched);

        // Assert (deve ser -5 pontos vs. não ultraprocessado)
        var enrichedNotUltra = CreateEnrichedData(
            calories: 200,
            carbs: 30,
            sugar: 8,
            protein: 5,
            fat: 6,
            satFat: 2,
            sodium: 400,
            fiber: 2);
        var resultNotUltra = _scoringService.Calculate(enrichedNotUltra);

        Assert.Equal(resultNotUltra.Value - 5, result.Value);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CASO 8: PROTEÍNA BAIXA (GANHO DE MASSA)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Calculate_LowProteinProduct_ShouldNotGetProteinBonus()
    {
        // Arrange: Produto com proteína muito baixa
        var enriched = CreateEnrichedData(
            calories: 150,
            carbs: 25,
            sugar: 3,
            protein: 2,  // Muito baixo
            fat: 3,
            satFat: 0.5,
            sodium: 150,
            fiber: 5);

        // Act
        var result = _scoringService.Calculate(enriched);

        // Assert
        Assert.DoesNotContain(result.Highlights, h => h.Contains("proteína"));
        Assert.Contains(result.Warnings, w => w.Contains("Baixo teor de proteína"));
    }

    [Fact]
    public void Calculate_SugaryBeverage_ShouldNotReturnExcellentScore()
    {
        var enriched = CreateEnrichedData(
            calories: 69,
            carbs: 12,
            sugar: 9.6,
            protein: 3.6,
            fat: 0.3,
            satFat: 0.1,
            sodium: 51,
            fiber: 1.4,
            addedSugar: 4.7,
            unit: "ml");

        var result = _scoringService.Calculate(enriched);

        Assert.InRange(result.Value, 50, 70);
        Assert.NotEqual("Excelente", result.Label);
        Assert.Equal("açúcar", result.PrincipalOffender);
        Assert.Contains(result.Warnings, w => w.Contains("100ml"));
        Assert.Contains(result.Warnings, w => w.Contains("açúcar", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Calculate_EnergyDenseLowProteinProduct_ShouldNotReturnExcellentScore()
    {
        var enriched = CreateEnrichedData(
            calories: 213,
            carbs: 28,
            sugar: 0,
            protein: 5,
            fat: 9,
            satFat: 1,
            sodium: 18,
            fiber: 2.5);

        var result = _scoringService.Calculate(enriched);

        Assert.InRange(result.Value, 70, 89);
        Assert.Equal("Bom", result.Label);
        Assert.Equal("nenhum relevante", result.PrincipalOffender);
    }

    // ═══════════════════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════════════════

    private NutritionEnrichedData CreateEnrichedData(
        double calories, double carbs, double sugar, double protein,
        double fat, double satFat, double sodium, double fiber,
        double addedSugar = 0,
        string unit = "g")
    {
        return new NutritionEnrichedData
        {
            NormalizedProfile = new EstimatedNutritionProfileDto
            {
                CaloriesPer100g = calories,
                EstimatedCarbsPer100g = carbs,
                EstimatedSugarPer100g = sugar,
                EstimatedAddedSugarPer100g = addedSugar,
                EstimatedProteinPer100g = protein,
                EstimatedFatPer100g = fat,
                EstimatedSaturatedFatPer100g = satFat,
                EstimatedSodiumPer100g = sodium,
                EstimatedFiberPer100g = fiber,
                NutritionUnit = unit
            },
            ProcessingLevel = "processado",
            Confidence = "alta",
            FallbackUsed = false
        };
    }
}
