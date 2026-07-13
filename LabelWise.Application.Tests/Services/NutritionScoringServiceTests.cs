using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Domain.Enums;
using LabelWise.Infrastructure.Services;
using Xunit;

namespace LabelWise.Application.Tests.Services
{
    public class NutritionScoringServiceTests
    {
        private readonly NutritionScoringService _sut = new();

        // ════════════════════════════════════════════════════════════════════════
        // Cenário 1: Produto com tabela completa — sem fallback, score calculado
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Calculate_TabelaCompleta_SemFallback_ScoreCoerente()
        {
            var enriched = BuildEnriched(
                calories: 42, sugar: 10.5, protein: 0, fat: 0, sodium: 10,
                processingLevel: "ultraprocessado",
                fallbackUsed: false,
                confidence: "alta");

            var score = _sut.Calculate(enriched);

            Assert.InRange(score.Value, 0, 100);
            Assert.Equal("Alto teor de açúcar", score.Warnings.First(w => w.Contains("açúcar")));
            Assert.Equal("Produto ultraprocessado", score.Warnings.First(w => w.Contains("ultraprocessado")));
            Assert.False(string.IsNullOrWhiteSpace(score.Label));
            Assert.False(string.IsNullOrWhiteSpace(score.Color));
        }

        // ════════════════════════════════════════════════════════════════════════
        // Cenário 2: Produto sem tabela — fallback aplicado, aviso no score
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Calculate_SemTabela_FallbackUsed_WarningPresente()
        {
            var enriched = BuildEnriched(
                calories: null, sugar: null, protein: null, fat: null, sodium: null,
                processingLevel: "desconhecido",
                fallbackUsed: true,
                confidence: "baixa");

            var score = _sut.Calculate(enriched);

            Assert.Contains(score.Warnings, w => w.Contains("estimada por categoria"));
            Assert.Contains(score.Warnings, w => w.Contains("baixa confiabilidade"));
        }

        // ════════════════════════════════════════════════════════════════════════
        // Cenário 3: Alto açúcar → score baixo
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Calculate_AltoAcucar_ScoreBaixo()
        {
            var enriched = BuildEnriched(
                sugar: 25, sodium: 50, protein: 5, fat: 3, satFat: 1,
                processingLevel: "processado",
                fallbackUsed: false,
                confidence: "alta");

            var score = _sut.Calculate(enriched);

            Assert.True(score.Value < 50, $"Score deveria ser < 50, mas foi {score.Value}");
            Assert.Equal("açúcar", score.PrincipalOffender);
            Assert.Contains(score.Warnings, w => w.Contains("açúcar"));
        }

        // ════════════════════════════════════════════════════════════════════════
        // Cenário 4: Produto saudável → score alto
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Calculate_ProdutoSaudavel_ScoreAlto()
        {
            var enriched = BuildEnriched(
                calories: 120, sugar: 1, protein: 18, fat: 3, satFat: 0.5,
                sodium: 60, fiber: 6,
                processingLevel: "in_natura",
                fallbackUsed: false,
                confidence: "alta");

            var score = _sut.Calculate(enriched);

            Assert.True(score.Value >= 70, $"Score deveria ser >= 70, mas foi {score.Value}");
            Assert.Equal("nenhum relevante", score.PrincipalOffender);
            Assert.NotEmpty(score.Highlights);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Cenário 5: Sem duplicidade — score é o único campo de pontuação
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Calculate_ResponseBuilder_SemDuplicidade()
        {
            var enriched = BuildEnriched(
                sugar: 8, sodium: 300, protein: 4, fat: 5,
                processingLevel: "processado",
                fallbackUsed: false,
                confidence: "media");

            var score = _sut.Calculate(enriched);

            var builder  = new NutritionResponseBuilder();
            var pipeline = BuildFakePipelineResult();
            var response = builder.Build(pipeline, enriched, score);

            // A resposta tem exatamente uma fonte de score
            Assert.NotNull(response.Score);
            Assert.InRange(response.Score.Value, 0, 100);

            // Não há nutritionalScore nem advancedScore na resposta unificada
            var type = response.GetType();
            Assert.Null(type.GetProperty("NutritionalScore"));
            Assert.Null(type.GetProperty("AdvancedScore"));
            Assert.Null(type.GetProperty("ResumoRapido"));
            Assert.Null(type.GetProperty("ExplicacaoScore"));
            Assert.Null(type.GetProperty("Classification"));
        }

        // ════════════════════════════════════════════════════════════════════════
        // Cenário 6: Labels e cores corretas por faixa
        // ════════════════════════════════════════════════════════════════════════

        [Theory]
        [InlineData(95, "Excelente", "green")]
        [InlineData(75, "Bom",       "light_green")]
        [InlineData(60, "Atenção",   "yellow")]
        [InlineData(30, "Ruim",      "red")]
        public void Calculate_ScoreFaixas_LabelsCorretas(int expectedScore, string expectedLabel, string expectedColor)
        {
            var enriched = BuildEnrichedForScore(expectedScore);
            var score    = _sut.Calculate(enriched);

            Assert.Equal(expectedLabel, score.Label);
            Assert.Equal(expectedColor, score.Color);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Cenário 7: Principal offender — prioridade correta
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Calculate_AcucarDominante_PrincipalOffenderAcucar()
        {
            var enriched = BuildEnriched(sugar: 12, sodium: 500, satFat: 6,
                processingLevel: "processado", fallbackUsed: false, confidence: "alta");

            var score = _sut.Calculate(enriched);

            // Açúcar tem prioridade sobre sódio e gordura saturada
            Assert.Equal("açúcar", score.PrincipalOffender);
        }

        [Fact]
        public void Calculate_SodioSemAcucar_PrincipalOffenderSodio()
        {
            var enriched = BuildEnriched(sugar: 2, sodium: 600, satFat: 2,
                processingLevel: "processado", fallbackUsed: false, confidence: "alta");

            var score = _sut.Calculate(enriched);

            Assert.Equal("sódio", score.PrincipalOffender);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Cenário 8: Score clampado entre 0 e 100
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Calculate_PiorCenario_ScoreNaoNegativo()
        {
            var enriched = BuildEnriched(
                sugar: 50, sodium: 1200, fat: 40, satFat: 20,
                processingLevel: "ultraprocessado",
                fallbackUsed: true,
                confidence: "baixa");

            var score = _sut.Calculate(enriched);

            Assert.True(score.Value >= 0, "Score não pode ser negativo");
        }

        [Fact]
        public void Calculate_MelhorCenario_ScoreNaoUltrassa100()
        {
            var enriched = BuildEnriched(
                sugar: 0, sodium: 10, protein: 30, fat: 1, satFat: 0, fiber: 10,
                processingLevel: "in_natura",
                fallbackUsed: false,
                confidence: "alta");

            var score = _sut.Calculate(enriched);

            Assert.True(score.Value <= 100, "Score não pode ultrapassar 100");
        }

        // ════════════════════════════════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════════════════════════════════

        private static NutritionEnrichedData BuildEnriched(
            double? calories = null, double? sugar = null, double? protein = null,
            double? fat = null, double? satFat = null, double? sodium = null,
            double? fiber = null,
            string processingLevel = "desconhecido",
            bool fallbackUsed = false,
            string confidence = "media")
        {
            return new NutritionEnrichedData
            {
                NormalizedProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g              = calories,
                    EstimatedSugarPer100g        = sugar,
                    EstimatedProteinPer100g      = protein,
                    EstimatedFatPer100g          = fat,
                    EstimatedSaturatedFatPer100g = satFat,
                    EstimatedSodiumPer100g       = sodium,
                    EstimatedFiberPer100g        = fiber
                },
                ProcessingLevel  = processingLevel,
                FallbackUsed     = fallbackUsed,
                Confidence       = confidence,
                PrincipalOffender = "nenhum relevante",
                ValidationWarnings = new List<string>()
            };
        }

        private static NutritionEnrichedData BuildEnrichedForScore(int targetScore) => targetScore switch
        {
            >= 90 => BuildEnriched(sugar: 1, sodium: 50, protein: 20, fat: 2, fiber: 8,
                processingLevel: "in_natura", confidence: "alta"),
            >= 70 => BuildEnriched(sugar: 4, sodium: 200, protein: 12, fat: 5, fiber: 3,
                processingLevel: "processado", confidence: "alta"),
            >= 50 => BuildEnriched(sugar: 8, sodium: 300, protein: 5, fat: 10,
                processingLevel: "processado", confidence: "media"),
            _ => BuildEnriched(sugar: 15, sodium: 700, protein: 1, fat: 20,
                processingLevel: "ultraprocessado", fallbackUsed: true, confidence: "baixa")
        };

        private static NutritionAnalysisResponseDto BuildFakePipelineResult() =>
            new()
            {
                AnalysisId            = Guid.NewGuid(),
                Success               = true,
                ProductName           = "Produto Teste",
                Category              = "biscoito",
                AnalysisMode          = AnalysisMode.FrontOfPackageOnly,
                ProcessingTimeSeconds = 1.5,
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g       = 480,
                    EstimatedSugarPer100g = 22
                },
                VisibleClaims = new List<string>(),
                Ingredients   = new List<string>(),
                Warnings      = new List<string>()
            };
    }
}
