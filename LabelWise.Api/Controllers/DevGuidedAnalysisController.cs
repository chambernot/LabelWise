using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LabelWise.Application.DTOs.Development;
using LabelWise.Application.Interfaces;
using LabelWise.Api.Models;

namespace LabelWise.Api.Controllers
{
    /// <summary>
    /// Controller de desenvolvimento para testar fluxo completo de captura guiada.
    /// </summary>
    /// <remarks>
    /// ⚠️ **APENAS PARA DESENVOLVIMENTO**
    /// 
    /// Este endpoint está disponível apenas em ambiente de Development e permite
    /// testar o fluxo completo de captura guiada em uma única chamada, simulando
    /// o que o app mobile faria em múltiplas etapas.
    /// 
    /// ## Uso
    /// 
    /// Envie um request multipart/form-data com as imagens desejadas:
    /// 
    /// ```bash
    /// curl -X POST "https://localhost:7319/api/dev/full-guided-analysis-test" \
    ///   -H "Authorization: Bearer {seu-token}" \
    ///   -F "frontImage=@front.jpg" \
    ///   -F "ingredientsImage=@ingredients.jpg" \
    ///   -F "nutritionImage=@nutrition.jpg" \
    ///   -F "allergenImage=@allergen.jpg" \
    ///   -F "barcode=7891234567890" \
    ///   -F "languageCode=pt-BR"
    /// ```
    /// 
    /// ## Campos Aceitos
    /// 
    /// | Campo | Tipo | Obrigatório | Descrição |
    /// |-------|------|-------------|-----------|
    /// | frontImage | file | Não | Foto da embalagem frontal |
    /// | ingredientsImage | file | Recomendado | Foto da lista de ingredientes |
    /// | nutritionImage | file | Recomendado | Foto da tabela nutricional |
    /// | allergenImage | file | Não | Foto da declaração de alérgenos |
    /// | barcode | string | Não | Código de barras manual |
    /// | languageCode | string | Não | Código do idioma (padrão: pt-BR) |
    /// | deviceInfo | string | Não | Informações do teste |
    /// 
    /// ## Response
    /// 
    /// Retorna um objeto completo com:
    /// - Identificação do produto consolidada
    /// - Ingredientes detectados
    /// - Alérgenos identificados
    /// - Informações nutricionais
    /// - Análise final com score e classificação
    /// - Detalhes de confiança multidimensional
    /// - Metadados de cada etapa processada
    /// - Warnings e erros detalhados
    /// </remarks>
    [ApiController]
    [Route("api/dev")]
    [Produces("application/json")]
    [ApiExplorerSettings(IgnoreApi = false)] // Mostrar no Swagger mesmo sendo dev
    public class DevGuidedAnalysisController : ControllerBase
    {
        private readonly IDevFullGuidedAnalysisOrchestrator _orchestrator;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<DevGuidedAnalysisController> _logger;

        private static readonly string[] _allowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
        private const long MaxFileSize = 10 * 1024 * 1024; // 10MB

        public DevGuidedAnalysisController(
            IDevFullGuidedAnalysisOrchestrator orchestrator,
            IHostEnvironment environment,
            ILogger<DevGuidedAnalysisController> logger)
        {
            _orchestrator = orchestrator;
            _environment = environment;
            _logger = logger;
        }

        /// <summary>
        /// Processa múltiplas capturas de imagem em um único request para teste completo do fluxo guiado.
        /// </summary>
        /// <remarks>
        /// ⚠️ **Disponível apenas em Development**
        /// 
        /// Este endpoint simula o fluxo completo de captura guiada que o app mobile
        /// faz em múltiplas etapas, consolidando tudo em uma única chamada.
        /// 
        /// ## Exemplo de Uso com PowerShell
        /// 
        /// ```powershell
        /// # Variáveis
        /// $apiUrl = "https://localhost:7319/api/dev/full-guided-analysis-test"
        /// $token = "seu-token-jwt"
        /// 
        /// # Criar multipart form data
        /// $form = @{
        ///     frontImage = Get-Item -Path "C:\images\front.jpg"
        ///     ingredientsImage = Get-Item -Path "C:\images\ingredients.jpg"
        ///     nutritionImage = Get-Item -Path "C:\images\nutrition.jpg"
        ///     allergenImage = Get-Item -Path "C:\images\allergen.jpg"
        ///     barcode = "7891234567890"
        ///     languageCode = "pt-BR"
        ///     deviceInfo = "PowerShell Test Script"
        /// }
        /// 
        /// # Enviar request
        /// $response = Invoke-RestMethod -Uri $apiUrl `
        ///     -Method Post `
        ///     -Headers @{ Authorization = "Bearer $token" } `
        ///     -Form $form
        /// 
        /// # Exibir resultado
        /// $response | ConvertTo-Json -Depth 10
        /// ```
        /// 
        /// ## Exemplo de Uso com cURL
        /// 
        /// ```bash
        /// curl -X POST "https://localhost:7319/api/dev/full-guided-analysis-test" \
        ///   -H "Authorization: Bearer {token}" \
        ///   -F "frontImage=@/path/to/front.jpg" \
        ///   -F "ingredientsImage=@/path/to/ingredients.jpg" \
        ///   -F "nutritionImage=@/path/to/nutrition.jpg" \
        ///   -F "allergenImage=@/path/to/allergen.jpg" \
        ///   -F "barcode=7891234567890" \
        ///   -F "languageCode=pt-BR"
        /// ```
        /// 
        /// ## Response de Sucesso
        /// 
        /// ```json
        /// {
        ///   "sessionId": "550e8400-e29b-41d4-a716-446655440000",
        ///   "processedAt": "2024-01-15T10:30:00Z",
        ///   "totalDuration": "00:00:05.234",
        ///   "success": true,
        ///   "productIdentification": {
        ///     "productName": "Biscoito Recheado",
        ///     "brand": "Marca XYZ",
        ///     "barcode": "7891234567890",
        ///     "confidence": 0.92
        ///   },
        ///   "ingredients": {
        ///     "detectedIngredients": ["farinha de trigo", "açúcar", "gordura vegetal"],
        ///     "totalCount": 15,
        ///     "parseConfidence": 0.88
        ///   },
        ///   "allergens": {
        ///     "declaredAllergens": ["trigo", "leite", "soja"],
        ///     "mayContainAllergens": ["amendoim"],
        ///     "detectionConfidence": 0.85
        ///   },
        ///   "nutritionalFacts": {
        ///     "calories": 450,
        ///     "servingSize": "30g",
        ///     "nutrientsDetected": 6
        ///   },
        ///   "finalAnalysis": {
        ///     "classification": "NeedsModeration",
        ///     "overallScore": 3.2,
        ///     "alerts": ["Alto teor de açúcar", "Alto teor de gordura saturada"],
        ///     "recommendations": ["Consumir com moderação"]
        ///   },
        ///   "processedSteps": [
        ///     {
        ///       "captureType": "FrontPackaging",
        ///       "stepName": "Embalagem Frontal",
        ///       "success": true,
        ///       "duration": "00:00:01.234"
        ///     }
        ///   ],
        ///   "confidenceDetails": {
        ///     "overall": 0.87,
        ///     "dimensions": {
        ///       "OCR": 0.90,
        ///       "Parsing": 0.85,
        ///       "Identification": 0.92,
        ///       "Completeness": 0.80
        ///     }
        ///   }
        /// }
        /// ```
        /// </remarks>
        /// <param name="model">Modelo de form data com imagens e parâmetros.</param>
        /// <param name="cancellationToken">Token de cancelamento.</param>
        /// <returns>Resultado completo da análise guiada.</returns>
        /// <response code="200">Análise processada com sucesso (pode conter warnings).</response>
        /// <response code="400">Requisição inválida ou nenhuma imagem fornecida.</response>
        /// <response code="401">Não autenticado.</response>
        /// <response code="403">Endpoint disponível apenas em Development.</response>
        /// <response code="500">Erro interno ao processar análise.</response>
        [HttpPost("full-guided-analysis-test")]
        [Authorize]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(FullGuidedAnalysisResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<FullGuidedAnalysisResponse>> ProcessFullGuidedAnalysisTest(
            [FromForm] FullGuidedAnalysisFormModel model,
            CancellationToken cancellationToken = default)
        {
            // 1. Verificar se está em Development
            if (!_environment.IsDevelopment())
            {
                _logger.LogWarning(
                    "[DevGuidedAnalysis] Attempt to access dev endpoint in {Environment} environment",
                    _environment.EnvironmentName);

                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Title = "Endpoint disponível apenas em Development",
                    Detail = "Este endpoint é exclusivo para testes em ambiente de desenvolvimento.",
                    Status = StatusCodes.Status403Forbidden
                });
            }

            // 2. Obter userId do token (ou usar valor placeholder para dev)
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int userId = 0; // Placeholder para dev endpoint - o orquestrador ignora este valor

            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userGuid))
            {
                // Usuário autenticado - poderíamos usar AbsoluteHash para converter Guid -> int
                // Mas para dev endpoint, userId não é crítico pois a sessão não fica vinculada
                userId = Math.Abs(userGuid.GetHashCode());

                _logger.LogDebug(
                    "[DevGuidedAnalysis] Authenticated user: {UserGuid}, using placeholder userId: {UserId}",
                    userGuid, userId);
            }
            else
            {
                _logger.LogWarning(
                    "[DevGuidedAnalysis] Token sem userId válido. UserIdClaim: {Claim}",
                    userIdClaim ?? "null");

                // Para dev endpoint, permitir mesmo sem userId válido (usar placeholder)
                userId = -1; // Indica usuário não identificado
            }

            // 3. Validar que pelo menos uma imagem foi fornecida
            var hasImages = model.FrontImage != null ||
                           model.IngredientsImage != null ||
                           model.NutritionImage != null ||
                           model.AllergenImage != null;

            if (!hasImages && string.IsNullOrEmpty(model.Barcode))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Nenhuma entrada fornecida",
                    Detail = "Forneça pelo menos uma imagem ou um código de barras.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // 4. Validar imagens
            var validationErrors = ValidateImages(model.FrontImage, model.IngredientsImage, model.NutritionImage, model.AllergenImage);
            if (validationErrors.Any())
            {
                return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["images"] = validationErrors.ToArray()
                })
                {
                    Title = "Validação de imagens falhou",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation(
                "[DevGuidedAnalysis] Processing full guided analysis for user {UserId}. " +
                "Front: {HasFront}, Ingredients: {HasIngredients}, Nutrition: {HasNutrition}, " +
                "Allergen: {HasAllergen}, Barcode: {Barcode}",
                userId,
                model.FrontImage != null,
                model.IngredientsImage != null,
                model.NutritionImage != null,
                model.AllergenImage != null,
                model.Barcode ?? "none");

            try
            {
                // 5. Montar dictionary de imagens
                var images = new Dictionary<string, (Stream stream, string fileName)>();

                if (model.FrontImage != null)
                {
                    images["front"] = (model.FrontImage.OpenReadStream(), model.FrontImage.FileName);
                }

                if (model.IngredientsImage != null)
                {
                    images["ingredients"] = (model.IngredientsImage.OpenReadStream(), model.IngredientsImage.FileName);
                }

                if (model.NutritionImage != null)
                {
                    images["nutrition"] = (model.NutritionImage.OpenReadStream(), model.NutritionImage.FileName);
                }

                if (model.AllergenImage != null)
                {
                    images["allergen"] = (model.AllergenImage.OpenReadStream(), model.AllergenImage.FileName);
                }

                // 6. Processar através do orquestrador
                var response = await _orchestrator.ProcessFullGuidedAnalysisAsync(
                    images,
                    model.Barcode,
                    userId,
                    model.LanguageCode,
                    model.DeviceInfo,
                    cancellationToken);

                // 7. Log do resultado
                _logger.LogInformation(
                    "[DevGuidedAnalysis] Completed. SessionId: {SessionId}, Success: {Success}, " +
                    "Duration: {Duration}ms, Steps: {Steps}, Errors: {Errors}",
                    response.SessionId,
                    response.Success,
                    response.TotalDuration.TotalMilliseconds,
                    response.ProcessedSteps.Count,
                    response.Errors.Count);

                // 8. Retornar resultado (sempre 200 OK, mesmo com warnings/erros parciais)
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DevGuidedAnalysis] Unexpected error for user {UserId}", userId);

                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Erro ao processar análise",
                    Detail = $"Erro inesperado: {ex.Message}",
                    Status = StatusCodes.Status500InternalServerError
                });
            }
        }

        /// <summary>
        /// Health check do endpoint de desenvolvimento.
        /// </summary>
        /// <returns>Status do endpoint.</returns>
        [HttpGet("full-guided-analysis-test/health")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public IActionResult HealthCheck()
        {
            if (!_environment.IsDevelopment())
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    status = "unavailable",
                    message = "Endpoint disponível apenas em Development"
                });
            }

            return Ok(new
            {
                status = "healthy",
                endpoint = "/api/dev/full-guided-analysis-test",
                environment = _environment.EnvironmentName,
                timestamp = DateTime.UtcNow,
                acceptedImageTypes = _allowedExtensions,
                maxFileSizeMB = MaxFileSize / (1024 * 1024)
            });
        }

        private System.Collections.Generic.List<string> ValidateImages(params IFormFile?[] images)
        {
            var errors = new System.Collections.Generic.List<string>();

            foreach (var image in images)
            {
                if (image == null) continue;

                // Validar tamanho
                if (image.Length > MaxFileSize)
                {
                    errors.Add($"Arquivo '{image.FileName}' excede o tamanho máximo de {MaxFileSize / (1024 * 1024)}MB");
                }

                // Validar extensão
                var extension = Path.GetExtension(image.FileName)?.ToLowerInvariant();
                if (string.IsNullOrEmpty(extension) || !_allowedExtensions.Contains(extension))
                {
                    errors.Add($"Arquivo '{image.FileName}' possui extensão inválida. Extensões permitidas: {string.Join(", ", _allowedExtensions)}");
                }
            }

            return errors;
        }
    }
}
