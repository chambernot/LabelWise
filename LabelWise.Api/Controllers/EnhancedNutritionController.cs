using LabelWise.Api.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace LabelWise.Api.Controllers;

/// <summary>
/// Controller para análise nutricional enriquecida com fallback inteligente por categoria.
/// </summary>
[ApiController]
[Route("api/v2/[controller]")]
[Produces("application/json")]
public class EnhancedNutritionController : ControllerBase
{
    private readonly IEnhancedNutritionPipelineOrchestrator _orchestrator;
    private readonly ILogger<EnhancedNutritionController> _logger;

    public EnhancedNutritionController(
        IEnhancedNutritionPipelineOrchestrator orchestrator,
        ILogger<EnhancedNutritionController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Analisa uma imagem de produto alimentício com fallback nutricional inteligente.
    /// </summary>
    /// <remarks>
    /// **Novo Pipeline V2 com Fallback Inteligente:**
    /// 
    /// Este endpoint usa um motor avançado de análise que:
    /// 
    /// 1. **Extrai dados reais** da tabela nutricional quando disponível
    /// 2. **Normaliza categorias** detectadas pela IA para categorias do banco
    /// 3. **Aplica fallback inteligente** para completar dados faltantes usando perfis típicos
    /// 4. **Detecta principal ofensor** nutricional (açúcar, gordura, sódio, etc)
    /// 5. **Calcula score ponderado** considerando confiança dos dados
    /// 6. **Ajusta summary e alerts** para refletir origem dos dados
    /// 
    /// **Tipos de Fonte de Dados (DataSourceType):**
    /// - **Real**: 100% dos dados extraídos da tabela nutricional
    /// - **Mixed**: Parte extraída, parte estimada por categoria
    /// - **EstimatedByCategory**: 100% estimado usando perfil da categoria
    /// - **Unknown**: Categoria não identificada
    /// 
    /// **Exemplo de Uso:**
    /// ```
    /// POST /api/v2/enhancednutrition/analyze
    /// Content-Type: multipart/form-data
    /// 
    /// image: [arquivo de imagem]
    /// additionalContext: "Requeijão cremoso light"
    /// ```
    /// 
    /// **Resposta:**
    /// ```json
    /// {
    ///   "success": true,
    ///   "productName": "Requeijão Cremoso Light",
    ///   "category": "Laticínio",
    ///   "estimatedNutritionProfile": {
    ///     "caloriesPer100g": 140,
    ///     "sugarPer100g": 2,
    ///     "proteinPer100g": 10,
    ///     "fatPer100g": 8,
    ///     "sodiumPer100g": 400
    ///   },
    ///   "dataSourceType": "Mixed",
    ///   "realFieldsCount": 3,
    ///   "estimatedFieldsCount": 2,
    ///   "normalizedCategoryCode": "laticinio_cremoso_light",
    ///   "normalizedCategoryName": "Laticínio Cremoso Light",
    ///   "nutritionalScore": 68.5,
    ///   "principalOffender": {
    ///     "type": "Sodium",
    ///     "value": 400,
    ///     "severity": "Medium",
    ///     "score": 4.0
    ///   },
    ///   "summary": "Leitura parcial (3 campos extraídos) complementada com perfil típico da categoria Laticínio Cremoso Light. Ponto de atenção: Alto teor de sódio."
    /// }
    /// ```
    /// </remarks>
    /// <param name="model">Dados do formulário contendo a imagem e contexto adicional.</param>
    /// <returns>Análise nutricional enriquecida com metadata de origem dos dados</returns>
    /// <response code="200">Análise concluída com sucesso</response>
    /// <response code="400">Imagem inválida ou não fornecida</response>
    /// <response code="500">Erro interno no processamento</response>
    [HttpPost("analyze")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(EnhancedNutritionAnalysisResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Analyze([FromForm] EnhancedNutritionAnalysisFormModel model)
    {
        try
        {
            // Validar imagem
            if (model.Image == null || model.Image.Length == 0)
            {
                _logger.LogWarning("No image provided for analysis");
                return BadRequest(new { error = "Image is required" });
            }

            // Validar tamanho (máx 10MB)
            const long maxSize = 10 * 1024 * 1024;
            if (model.Image.Length > maxSize)
            {
                _logger.LogWarning("Image too large: {Size} bytes", model.Image.Length);
                return BadRequest(new { error = "Image size must be less than 10MB" });
            }

            // Converter para byte array
            byte[] imageData;
            using (var memoryStream = new System.IO.MemoryStream())
            {
                await model.Image.CopyToAsync(memoryStream);
                imageData = memoryStream.ToArray();
            }

            _logger.LogInformation(
                "Starting enhanced nutrition analysis: FileName={FileName}, Size={Size}, Context={Context}",
                model.Image.FileName, model.Image.Length, model.AdditionalContext);

            // Executar análise
            var result = await _orchestrator.AnalyzeAsync(imageData, model.AdditionalContext);

            if (!result.Success)
            {
                _logger.LogError("Analysis failed: {ErrorMessage}", result.ErrorMessage);
                return StatusCode(500, new { error = result.ErrorMessage });
            }

            _logger.LogInformation(
                "Analysis completed successfully: Product={Product}, DataSourceType={DataSourceType}, Score={Score}",
                result.ProductName, result.DataSourceType, result.NutritionalScore);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in enhanced nutrition analysis");
            return StatusCode(500, new { error = "Internal server error during analysis" });
        }
    }

    /// <summary>
    /// Health check do serviço de análise enriquecida.
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            service = "EnhancedNutritionAnalysis",
            version = "2.0",
            features = new[]
            {
                "AI Vision Analysis",
                "Category Normalization",
                "Intelligent Nutritional Fallback",
                "Principal Offender Detection",
                "Weighted Nutritional Scoring",
                "Smart Summary Adjustment"
            },
            timestamp = DateTime.UtcNow
        });
    }
}
