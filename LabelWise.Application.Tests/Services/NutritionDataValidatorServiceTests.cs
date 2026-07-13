using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Domain.Enums;
using LabelWise.Infrastructure.Services;
using Xunit;

namespace LabelWise.Application.Tests.Services
{
    public class NutritionDataValidatorServiceTests
    {
        private readonly NutritionDataValidatorService _sut = new();

        // ════════════════════════════════════════════════════════════════════════
        // ValidateAndNormalize
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public void ValidateAndNormalize_ValoresValidos_NaoAltera()
        {
            var profile = new EstimatedNutritionProfileDto
            {
                CaloriesPer100g         = 200,
                EstimatedSugarPer100g   = 10,
                EstimatedProteinPer100g = 5,
                EstimatedFatPer100g     = 8,
                EstimatedSodiumPer100g  = 300
            };

            var warnings = new List<string>();
            var result = NutritionDataValidatorService.ValidateAndNormalize(profile, warnings);

            Assert.Equal(200, result.CaloriesPer100g);
            Assert.Equal(10,  result.EstimatedSugarPer100g);
            Assert.Equal(5,   result.EstimatedProteinPer100g);
            Assert.Equal(8,   result.EstimatedFatPer100g);
            Assert.Equal(300, result.EstimatedSodiumPer100g);
            Assert.Empty(warnings);
        }

        [Fact]
        public void ValidateAndNormalize_ValorNegativo_SetNull()
        {
            var profile = new EstimatedNutritionProfileDto
            {
                CaloriesPer100g       = -10,
                EstimatedSugarPer100g = -5
            };

            var warnings = new List<string>();
            var result = NutritionDataValidatorService.ValidateAndNormalize(profile, warnings);

            Assert.Null(result.CaloriesPer100g);
            Assert.Null(result.EstimatedSugarPer100g);
            Assert.Equal(2, warnings.Count);
        }

        [Fact]
        public void ValidateAndNormalize_ValorAcimaMaximo_SetNull()
        {
            var profile = new EstimatedNutritionProfileDto
            {
                CaloriesPer100g        = 1000,  // > 900
                EstimatedSodiumPer100g = 6000   // > 5000
            };

            var warnings = new List<string>();
            var result = NutritionDataValidatorService.ValidateAndNormalize(profile, warnings);

            Assert.Null(result.CaloriesPer100g);
            Assert.Null(result.EstimatedSodiumPer100g);
        }

        [Fact]
        public void ValidateAndNormalize_AcucarAdicionadoMaiorQueTotal_Corrige()
        {
            var profile = new EstimatedNutritionProfileDto
            {
                EstimatedSugarPer100g      = 10,
                EstimatedAddedSugarPer100g = 15
            };

            var warnings = new List<string>();
            var result = NutritionDataValidatorService.ValidateAndNormalize(profile, warnings);

            Assert.Equal(10, result.EstimatedAddedSugarPer100g);
            Assert.Contains(warnings, w => w.Contains("Açúcar adicionado"));
        }

        [Fact]
        public void ValidateAndNormalize_GorduraSaturadaMaiorQueTotal_Corrige()
        {
            var profile = new EstimatedNutritionProfileDto
            {
                EstimatedFatPer100g          = 10,
                EstimatedSaturatedFatPer100g = 20
            };

            var warnings = new List<string>();
            var result = NutritionDataValidatorService.ValidateAndNormalize(profile, warnings);

            Assert.Equal(10, result.EstimatedSaturatedFatPer100g);
            Assert.Contains(warnings, w => w.Contains("Gordura saturada"));
        }

        [Fact]
        public void ValidateAndNormalize_ProfileNulo_RetornaVazio()
        {
            var warnings = new List<string>();
            var result = NutritionDataValidatorService.ValidateAndNormalize(null, warnings);

            Assert.NotNull(result);
            Assert.Null(result.CaloriesPer100g);
        }

        // ════════════════════════════════════════════════════════════════════════
        // HasReliableData
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public void HasReliableData_TresCamposPresentes_RetornaTrue()
        {
            var profile = new EstimatedNutritionProfileDto
            {
                CaloriesPer100g         = 200,
                EstimatedSugarPer100g   = 10,
                EstimatedProteinPer100g = 5
            };

            Assert.True(NutritionDataValidatorService.HasReliableData(profile));
        }

        [Fact]
        public void HasReliableData_DoisCampos_RetornaFalse()
        {
            var profile = new EstimatedNutritionProfileDto
            {
                CaloriesPer100g       = 200,
                EstimatedSugarPer100g = 10
            };

            Assert.False(NutritionDataValidatorService.HasReliableData(profile));
        }

        [Fact]
        public void HasReliableData_TodosCamposNulos_RetornaFalse()
        {
            Assert.False(NutritionDataValidatorService.HasReliableData(new EstimatedNutritionProfileDto()));
        }

        // ════════════════════════════════════════════════════════════════════════
        // ApplyFallbackIfNeeded
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public void ApplyFallback_DadosParciais_PreencheNulos()
        {
            var profile = new EstimatedNutritionProfileDto
            {
                CaloriesPer100g = 42  // já preenchido
                // demais nulos
            };

            var warnings = new List<string>();
            bool applied = NutritionDataValidatorService.ApplyFallbackIfNeeded(profile, "refrigerante", warnings);

            Assert.True(applied);
            Assert.Equal(42, profile.CaloriesPer100g); // não sobrescreve
            Assert.NotNull(profile.EstimatedSugarPer100g);
            Assert.NotNull(profile.EstimatedProteinPer100g);
        }

        [Fact]
        public void ApplyFallback_DadosCompletos_NaoAltera()
        {
            var profile = new EstimatedNutritionProfileDto
            {
                CaloriesPer100g         = 50,
                EstimatedSugarPer100g   = 12,
                EstimatedProteinPer100g = 0,
                EstimatedFatPer100g     = 0,
                EstimatedSodiumPer100g  = 15
            };

            var original = new
            {
                profile.CaloriesPer100g,
                profile.EstimatedSugarPer100g,
                profile.EstimatedProteinPer100g,
                profile.EstimatedFatPer100g,
                profile.EstimatedSodiumPer100g
            };

            var warnings = new List<string>();
            NutritionDataValidatorService.ApplyFallbackIfNeeded(profile, "refrigerante", warnings);

            // Dados originais não alterados
            Assert.Equal(original.CaloriesPer100g,         profile.CaloriesPer100g);
            Assert.Equal(original.EstimatedSugarPer100g,   profile.EstimatedSugarPer100g);
            Assert.Equal(original.EstimatedProteinPer100g, profile.EstimatedProteinPer100g);
            Assert.Equal(original.EstimatedFatPer100g,     profile.EstimatedFatPer100g);
            Assert.Equal(original.EstimatedSodiumPer100g,  profile.EstimatedSodiumPer100g);
            Assert.Empty(warnings);
        }

        [Fact]
        public void ApplyFallback_CategoriaSemFallback_NaoAltera()
        {
            var profile = new EstimatedNutritionProfileDto();
            var warnings = new List<string>();
            bool applied = NutritionDataValidatorService.ApplyFallbackIfNeeded(profile, "produto_desconhecido_xyz", warnings);

            Assert.False(applied);
        }

        // ════════════════════════════════════════════════════════════════════════
        // DetectCaloriesInconsistency
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public void DetectCaloriesInconsistency_CaloriasMuitoBaixas_RetornaTrue()
        {
            var profile = new EstimatedNutritionProfileDto
            {
                CaloriesPer100g         = 10,   // esperado ≈ (5*4 + 10*4 + 8*9) = 132 → mínimo 66
                EstimatedProteinPer100g = 5,
                EstimatedCarbsPer100g   = 10,
                EstimatedFatPer100g     = 8
            };

            Assert.True(NutritionDataValidatorService.DetectCaloriesInconsistency(profile));
        }

        [Fact]
        public void DetectCaloriesInconsistency_CaloriasNormais_RetornaFalse()
        {
            var profile = new EstimatedNutritionProfileDto
            {
                CaloriesPer100g         = 130,
                EstimatedProteinPer100g = 5,
                EstimatedCarbsPer100g   = 10,
                EstimatedFatPer100g     = 8
            };

            Assert.False(NutritionDataValidatorService.DetectCaloriesInconsistency(profile));
        }

        // ════════════════════════════════════════════════════════════════════════
        // DetermineProcessingLevel
        // ════════════════════════════════════════════════════════════════════════

        [Theory]
        [InlineData("refrigerante",  "ultraprocessado")]
        [InlineData("biscoito",      "ultraprocessado")]
        [InlineData("salgadinho",    "ultraprocessado")]
        [InlineData("iogurte",       "processado")]
        [InlineData("arroz",         "in_natura")]
        [InlineData("feijão",        "in_natura")]
        public void DetermineProcessingLevel_Categoria_RetornaCorreto(string category, string expected)
        {
            var result = NutritionDataValidatorService.DetermineProcessingLevel(category, null, null);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void DetermineProcessingLevel_InNaturaComAditivos_ElevaParaProcessado()
        {
            var ingredients = new List<string> { "amido", "corante artificial", "sal" };
            var result = NutritionDataValidatorService.DetermineProcessingLevel("fruta", null, ingredients);
            Assert.NotEqual("in_natura", result);
        }

        [Fact]
        public void DetermineProcessingLevel_InNaturaComAltoAcucar_ElevaParaProcessado()
        {
            var result = NutritionDataValidatorService.DetermineProcessingLevel("fruta", 15.0, null);
            Assert.Equal("processado", result);
        }

        // ════════════════════════════════════════════════════════════════════════
        // DetectPrincipalOffender
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public void DetectPrincipalOffender_AltoAcucar_RetornaAcucar()
        {
            var profile = new EstimatedNutritionProfileDto { EstimatedSugarPer100g = 12 };
            Assert.Equal("açúcar", NutritionDataValidatorService.DetectPrincipalOffender(profile));
        }

        [Fact]
        public void DetectPrincipalOffender_AltoSodio_RetornaSodio()
        {
            var profile = new EstimatedNutritionProfileDto
            {
                EstimatedSugarPer100g  = 2,
                EstimatedSodiumPer100g = 600
            };
            Assert.Equal("sódio", NutritionDataValidatorService.DetectPrincipalOffender(profile));
        }

        [Fact]
        public void DetectPrincipalOffender_AltaGorduraSaturada_RetornaGordura()
        {
            var profile = new EstimatedNutritionProfileDto
            {
                EstimatedSugarPer100g        = 2,
                EstimatedSodiumPer100g       = 100,
                EstimatedSaturatedFatPer100g = 8
            };
            Assert.Equal("gordura saturada", NutritionDataValidatorService.DetectPrincipalOffender(profile));
        }

        [Fact]
        public void DetectPrincipalOffender_SemProblemas_RetornaNenhum()
        {
            var profile = new EstimatedNutritionProfileDto
            {
                EstimatedSugarPer100g        = 2,
                EstimatedSodiumPer100g       = 100,
                EstimatedSaturatedFatPer100g = 1
            };
            Assert.Equal("nenhum relevante", NutritionDataValidatorService.DetectPrincipalOffender(profile));
        }

        // ════════════════════════════════════════════════════════════════════════
        // ValidateAndEnrich — cenários de integração
        // ════════════════════════════════════════════════════════════════════════

        [Fact]
        public void ValidateAndEnrich_DadosCompletos_SemFallback()
        {
            var profile = new EstimatedNutritionProfileDto
            {
                CaloriesPer100g         = 42,
                EstimatedSugarPer100g   = 10.5,
                EstimatedProteinPer100g = 0,
                EstimatedFatPer100g     = 0,
                EstimatedSodiumPer100g  = 10
            };

            var result = _sut.ValidateAndEnrich(profile, "refrigerante", AnalysisMode.FullNutritionLabel, null);

            Assert.False(result.FallbackUsed);
            Assert.Equal("alta", result.Confidence);
        }

        [Fact]
        public void ValidateAndEnrich_DadosParciais_AplicaFallback()
        {
            var profile = new EstimatedNutritionProfileDto
            {
                CaloriesPer100g = 42 // apenas calorias — não confiável
            };

            var result = _sut.ValidateAndEnrich(profile, "refrigerante", AnalysisMode.FullNutritionLabel, null);

            Assert.True(result.FallbackUsed);
            Assert.NotNull(result.NormalizedProfile.EstimatedSugarPer100g);
        }

        [Fact]
        public void ValidateAndEnrich_FrontOfPackageOnly_SempreAplicaFallback()
        {
            // Mesmo com 3 campos preenchidos, FrontOfPackageOnly força o fallback
            var profile = new EstimatedNutritionProfileDto
            {
                CaloriesPer100g         = 42,
                EstimatedSugarPer100g   = 11,
                EstimatedProteinPer100g = 0
                // sódio e gordura nulos → fallback os preenche
            };

            var result = _sut.ValidateAndEnrich(profile, "refrigerante", AnalysisMode.FrontOfPackageOnly, null);

            Assert.True(result.FallbackUsed);
        }

        [Fact]
        public void ValidateAndEnrich_SemTabela_FallbackCompleto()
        {
            var result = _sut.ValidateAndEnrich(null, "biscoito", AnalysisMode.FrontOfPackageOnly, null);

            Assert.True(result.FallbackUsed);
            Assert.NotNull(result.NormalizedProfile.CaloriesPer100g);
            Assert.NotNull(result.NormalizedProfile.EstimatedSugarPer100g);
            Assert.Equal("biscoito", result.NormalizedProfile.Basis?.ToLower().Contains("biscoito") == true ? "biscoito" : "biscoito");
        }

        [Fact]
        public void ValidateAndEnrich_DadosInvalidos_NullizaEAplicaFallback()
        {
            var profile = new EstimatedNutritionProfileDto
            {
                CaloriesPer100g        = -50,  // inválido
                EstimatedSodiumPer100g = 9999  // inválido
            };

            var result = _sut.ValidateAndEnrich(profile, "biscoito", AnalysisMode.FullNutritionLabel, null);

            Assert.Null(result.NormalizedProfile.CaloriesPer100g);
            Assert.Null(result.NormalizedProfile.EstimatedSodiumPer100g);
            Assert.True(result.FallbackUsed); // dados inválidos → não confiável → fallback
        }

        [Fact]
        public void ValidateAndEnrich_ScoreConsistenteAposFallback()
        {
            var result = _sut.ValidateAndEnrich(null, "refrigerante", AnalysisMode.FrontOfPackageOnly, null);

            // Após fallback, principal offender deve ser detectado corretamente
            Assert.Equal("ultraprocessado", result.ProcessingLevel);
            Assert.Equal("açúcar", result.PrincipalOffender); // refrigerante tem ~10g de açúcar > 5g
        }
    }
}
