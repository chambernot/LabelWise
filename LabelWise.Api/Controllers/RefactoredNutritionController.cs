using LabelWise.Api.Models;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LabelWise.Api.Controllers
{
    /// <summary>
    /// Controller refatorado para análise nutricional com separação clara entre
    /// dados extraídos visualmente e dados inferidos/estimados.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RefactoredNutritionController : ControllerBase
    {
        private readonly RefactoredNutritionAnalysisService _nutritionService;
        private readonly ILogger<RefactoredNutritionController> _logger;

        public RefactoredNutritionController(
            RefactoredNutritionAnalysisService nutritionService,
            ILogger<RefactoredNutritionController> logger)
        {
            _nutritionService = nutritionService;
            _logger = logger;
        }

        /// <summary>
        /// Analisa uma imagem de produto alimentício e retorna informações nutricionais
        /// com separação clara entre dados extraídos e estimados.
        /// </summary>
        /// <param name="model">Modelo com imagem e idioma</param>
        /// <returns>Análise nutricional refatorada</returns>
        [HttpPost("analyze")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(RefactoredNutritionAnalysisResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<RefactoredNutritionAnalysisResponse>> AnalyzeProductImage(
            [FromForm] RefactoredNutritionAnalysisFormModel model)
        {
            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            _logger.LogInformation("📸 Refactored Nutrition Analysis - Request Received");
            _logger.LogInformation("   FileName: {FileName}", model.Image?.FileName);
            _logger.LogInformation("   FileSize: {Size} bytes", model.Image?.Length);
            _logger.LogInformation("   ContentType: {ContentType}", model.Image?.ContentType);
            _logger.LogInformation("   Language: {Language}", model.LanguageCode);
            _logger.LogInformation("═══════════════════════════════════════════════════════════");

            if (model.Image == null || model.Image.Length == 0)
            {
                _logger.LogWarning("❌ Imagem não fornecida ou vazia");
                return BadRequest(new { error = "Imagem não fornecida" });
            }

            try
            {
                byte[] imageData;
                using (var memoryStream = new MemoryStream())
                {
                    await model.Image.CopyToAsync(memoryStream);
                    imageData = memoryStream.ToArray();
                }

                var result = await _nutritionService.AnalyzeProductImageRefactoredAsync(
                    imageData,
                    model.Image.FileName,
                    model.LanguageCode ?? "pt");

                if (!result.Success)
                {
                    _logger.LogWarning("⚠️ Análise retornou sem sucesso: {ErrorMessage}", result.ErrorMessage);
                    return Ok(result);
                }

                _logger.LogInformation("✅ Análise concluída com sucesso");
                _logger.LogInformation("   Product: {ProductName}", result.ProductName);
                _logger.LogInformation("   Brand: {Brand}", result.Brand);
                _logger.LogInformation("   Category: {Category}", result.Category);
                _logger.LogInformation("   AnalysisMode: {Mode}", result.AnalysisMode);
                _logger.LogInformation("   VisibleClaims: {ClaimCount}", result.VisibleClaims.Count);
                _logger.LogInformation("   Warnings: {WarningCount}", result.Warnings.Count);
                _logger.LogInformation("═══════════════════════════════════════════════════════════");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao processar análise nutricional refatorada");
                return StatusCode(500, new
                {
                    success = false,
                    errorMessage = $"Erro interno ao processar análise: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Endpoint de exemplo mostrando o formato de resposta esperado.
        /// </summary>
        [HttpGet("example")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(NutritionAnalysisResponseDto), StatusCodes.Status200OK)]
        public ActionResult<NutritionAnalysisResponseDto> GetExampleResponse()
        {
            return Ok(new NutritionAnalysisResponseDto
            {
                Success = true,
                ProductName = "Chocolatto",
                Brand = "3 Corações",
                Category = "alimento achocolatado em pó instantâneo",
                PackageWeight = "560 g",
                AnalysisMode = Domain.Enums.AnalysisMode.FrontOfPackageOnly,
                VisibleClaims = new List<string>
                {
                    "Não contém glúten",
                    "Fonte de vitaminas e minerais",
                    "Vitaminas D, B1, B2, B6 e B12",
                    "Ferro e zinco",
                    "Nova fórmula"
                },
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g = 380,
                    EstimatedPackageCalories = 2128,
                    EstimatedSugarPer100g = 75,
                    EstimatedProteinPer100g = 4,
                    EstimatedSodiumPer100g = 150,
                    EstimatedFiberPer100g = 3,
                    EstimatedFatPer100g = 3,
                    Basis = "Estimativa por categoria visual, sem leitura da tabela nutricional oficial"
                },
                Classification = new ProductClassificationDto
                {
                    Diabetic = new HealthProfileResult
                    {
                        Status = "consumo_moderado",
                        Reason = "Produto achocolatado tende a conter açúcar relevante e baixa fibra"
                    },
                    BloodPressure = new HealthProfileResult
                    {
                        Status = "nao_recomendado",
                        Reason = "Produto ultraprocessado pode apresentar teor relevante de sódio"
                    },
                    WeightLoss = new HealthProfileResult
                    {
                        Status = "consumo_moderado",
                        Reason = "Produto com densidade calórica moderada e provável adição de açúcar"
                    },
                    MuscleGain = new HealthProfileResult
                    {
                        Status = "fraco",
                        Reason = "Não é uma fonte relevante de proteína"
                    }
                },
                Summary = "Achocolatado em pó ultraprocessado, com fortificação de vitaminas e minerais, sem indicação de alto teor proteico, com provável presença relevante de açúcar, baseado em análise visual da embalagem.",
                ConfidenceDetails = new ConfidenceDetailsDto
                {
                    ProductIdentification = 0.90,
                    VisibleClaimsExtraction = 0.85,
                    EstimatedNutritionProfile = 0.55,
                    Classification = 0.70
                },
                Warnings = new List<string>
                {
                    "Análise estimada com base na imagem frontal do produto",
                    "Valores nutricionais não foram extraídos da tabela nutricional oficial",
                    "Para análise precisa, envie a parte traseira com tabela nutricional e ingredientes"
                },
                ProcessingTimeSeconds = 6.58
            });
        }
    }
}
