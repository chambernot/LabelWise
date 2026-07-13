using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using LabelWise.Application.DTOs;
using LabelWise.Application.DTOs.Analysis;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;

namespace LabelWise.Api.Controllers
{
    /// <summary>
    /// Controller para análise de imagens de rótulos de produtos alimentícios.
    /// Suporta diferentes tipos de captura: código de barras, tabela nutricional, ingredientes, alérgenos e embalagem frontal.
    /// </summary>
    [ApiController]
    [Route("api/pipeline")]
    [Produces("application/json")]
    public class ProductAnalysisPipelineController : ControllerBase
    {
        private readonly IProductAnalysisPipelineOrchestrator _orchestrator;
        private static readonly string[] _allowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
        private const long MaxFileSize = 5 * 1024 * 1024; // 5MB

        public ProductAnalysisPipelineController(IProductAnalysisPipelineOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        /// <summary>
        /// Analisa uma imagem capturada de rótulo de produto com suporte a diferentes tipos de captura.
        /// </summary>
        /// <remarks>
        /// **Tipos de Captura Suportados:**
        /// 
        /// | CaptureType | Descrição | Arquivo Obrigatório | Barcode Obrigatório |
        /// |-------------|-----------|---------------------|---------------------|
        /// | 1 - Barcode | Código de barras (EAN, UPC) | Não | Sim |
        /// | 2 - FrontPackaging | Embalagem frontal | Sim | Não |
        /// | 3 - NutritionTable | Tabela nutricional | Sim | Não |
        /// | 4 - IngredientsList | Lista de ingredientes | Sim | Não |
        /// | 5 - AllergenStatement | Declaração de alérgenos | Sim | Não |
        /// 
        /// **Fluxo de Processamento:**
        /// 1. Validação dos parâmetros de entrada
        /// 2. Identificação do produto (via barcode ou OCR)
        /// 3. Leitura do rótulo (OCR + parsing específico por tipo)
        /// 4. Análise nutricional (scores, alertas, recomendações)
        /// 5. Consolidação dos resultados
        /// 
        /// **Exemplo de Uso com cURL:**
        /// ```bash
        /// curl -X POST "https://api.labelwise.com/api/pipeline/analyze" \
        ///   -H "Content-Type: multipart/form-data" \
        ///   -F "file=@rotulo.jpg" \
        ///   -F "captureType=3" \
        ///   -F "barcode=7891234567890"
        /// ```
        /// </remarks>
        /// <param name="file">Arquivo de imagem do rótulo. Obrigatório para todos os tipos exceto Barcode.</param>
        /// <param name="captureType">Tipo de captura da imagem. Valores: 1=Barcode, 2=FrontPackaging, 3=NutritionTable, 4=IngredientsList, 5=AllergenStatement</param>
        /// <param name="barcode">Código de barras do produto. Obrigatório para CaptureType=Barcode. Formatos: EAN-8, EAN-13, UPC-A.</param>
        /// <param name="languageCode">Idioma para OCR (ISO 639-1). Padrão: pt</param>
        /// <param name="enableExternalDatabaseLookup">Buscar em bases externas (Open Food Facts). Padrão: true</param>
        /// <param name="enableMultiProviderOcr">Usar múltiplos providers de OCR (fallback). Padrão: true</param>
        /// <param name="executeNutritionalAnalysis">Executar análise nutricional completa. Padrão: true</param>
        /// <returns>Resultado completo da análise incluindo identificação, leitura e análise nutricional.</returns>
        /// <response code="200">Análise executada com sucesso.</response>
        /// <response code="400">Parâmetros inválidos (arquivo faltando, barcode inválido, etc).</response>
        /// <response code="401">Token JWT inválido ou expirado.</response>
        /// <response code="500">Erro interno durante o processamento.</response>
        [HttpPost("analyze")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(CapturedImageAnalysisResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AnalyzeCapturedImage(
            IFormFile? file,
            [Required(ErrorMessage = "CaptureType é obrigatório.")]
            [FromForm] CaptureType captureType,
            [FromForm] string? barcode = null,
            [FromForm] string languageCode = "pt",
            [FromForm] bool enableExternalDatabaseLookup = true,
            [FromForm] bool enableMultiProviderOcr = true,
            [FromForm] bool executeNutritionalAnalysis = true)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = new CapturedImageAnalysisResponse
            {
                CaptureType = captureType,
                Metadata = new AnalysisMetadataDto
                {
                    StartTime = DateTime.UtcNow
                }
            };

            try
            {
                // ═══════════════════════════════════════════════════════════════════
                // VALIDAÇÕES
                // ═══════════════════════════════════════════════════════════════════

                var validationErrors = ValidateRequest(file, captureType, barcode);
                if (validationErrors.Count > 0)
                {
                    return BadRequest(new ValidationProblemDetails
                    {
                        Title = "Erro de validação",
                        Status = StatusCodes.Status400BadRequest,
                        Errors = validationErrors
                    });
                }

                // ═══════════════════════════════════════════════════════════════════
                // EXTRAIR USER ID DO TOKEN JWT
                // ═══════════════════════════════════════════════════════════════════

                Guid? userId = ExtractUserId();

                // ═══════════════════════════════════════════════════════════════════
                // PREENCHER METADATA
                // ═══════════════════════════════════════════════════════════════════

                if (file != null)
                {
                    response.Metadata.FileName = file.FileName;
                    response.Metadata.FileSizeBytes = file.Length;
                    response.Metadata.ContentType = file.ContentType;
                }

                // ═══════════════════════════════════════════════════════════════════
                // EXECUTAR PIPELINE
                // ═══════════════════════════════════════════════════════════════════

                // Para CaptureType.Barcode sem arquivo, cria um request simplificado
                if (captureType == CaptureType.Barcode && file == null)
                {
                    var barcodeResult = await ProcessBarcodeOnlyAsync(barcode!, userId, response);
                    return Ok(barcodeResult);
                }

                // Para outros tipos, executa pipeline completo com OCR
                using var stream = file!.OpenReadStream();
                var pipelineResult = await _orchestrator.ExecutePipelineWithCaptureTypeAsync(
                    stream,
                    file.FileName,
                    captureType,
                    new CapturedImageAnalysisRequest
                    {
                        CaptureType = captureType,
                        Barcode = barcode,
                        LanguageCode = languageCode,
                        EnableExternalDatabaseLookup = enableExternalDatabaseLookup,
                        EnableMultiProviderOcr = enableMultiProviderOcr,
                        ExecuteNutritionalAnalysis = executeNutritionalAnalysis
                    },
                    userId);

                // Mapear resultado do pipeline para resposta
                response = MapPipelineResultToResponse(pipelineResult, captureType, stopwatch);

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Erro de validação",
                    Detail = ex.Message,
                    Status = StatusCodes.Status400BadRequest
                });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Erro de configuração",
                    Detail = ex.Message,
                    Status = StatusCodes.Status500InternalServerError
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] AnalyzeCapturedImage: {ex.Message}");
                Console.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");

                return StatusCode(500, new ProblemDetails
                {
                    Title = "Erro interno",
                    Detail = "Um erro inesperado ocorreu durante o processamento.",
                    Status = StatusCodes.Status500InternalServerError
                });
            }
        }

        /// <summary>
        /// Executa o pipeline completo de análise com metadados detalhados de cada etapa.
        /// Este endpoint retorna informações técnicas sobre o processamento (tempos, OCR, parsing, etc).
        /// </summary>
        /// <remarks>
        /// **Nota:** Este endpoint é mantido para compatibilidade com versões anteriores.
        /// Para novos desenvolvimentos, utilize o endpoint `/api/pipeline/analyze` que suporta CaptureType.
        /// </remarks>
        /// <param name="file">Arquivo de imagem do rótulo do produto</param>
        /// <returns>Resultado completo do pipeline com metadados técnicos</returns>
        [HttpPost("analyze-image")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ProductAnalysisPipelineResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Obsolete("Use /api/pipeline/analyze com CaptureType para novos desenvolvimentos.")]
        public async Task<IActionResult> AnalyzeImageWithMetadata(IFormFile file)
        {
            // Validação: arquivo obrigatório
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "Arquivo é obrigatório." });
            }

            // Validação: tamanho do arquivo
            if (file.Length > MaxFileSize)
            {
                return BadRequest(new { error = $"Arquivo muito grande. Tamanho máximo permitido: {MaxFileSize / 1024 / 1024}MB." });
            }

            // Validação: extensão do arquivo
            var extension = System.IO.Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !_allowedExtensions.Contains(extension))
            {
                return BadRequest(new 
                { 
                    error = $"Formato de arquivo não suportado. Formatos aceitos: {string.Join(", ", _allowedExtensions)}" 
                });
            }

            try
            {
                // Extrair userId do token JWT se autenticado
                Guid? userId = ExtractUserId();

                // Executar pipeline completo com metadados
                using var stream = file.OpenReadStream();
                var result = await _orchestrator.ExecutePipelineAsync(stream, file.FileName, userId);

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(500, new { error = $"Erro de configuração: {ex.Message}" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] AnalyzeImageWithMetadata: {ex.Message}");
                Console.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");

                return StatusCode(500, new { error = "Um erro inesperado ocorreu durante o processamento do pipeline." });
            }
        }

        /// <summary>
        /// Retorna os tipos de captura suportados pela API.
        /// </summary>
        /// <returns>Lista de tipos de captura com descrições.</returns>
        [HttpGet("capture-types")]
        [ProducesResponseType(typeof(IEnumerable<CaptureTypeInfoDto>), StatusCodes.Status200OK)]
        public IActionResult GetCaptureTypes()
        {
            var captureTypes = Enum.GetValues<CaptureType>()
                .Select(ct => new CaptureTypeInfoDto
                {
                    Value = (int)ct,
                    Name = ct.ToString(),
                    Description = GetCaptureTypeDescription(ct),
                    RequiresFile = ct != CaptureType.Barcode,
                    RequiresBarcode = ct == CaptureType.Barcode
                })
                .ToList();

            return Ok(captureTypes);
        }

        #region Private Methods

        private Dictionary<string, string[]> ValidateRequest(IFormFile? file, CaptureType captureType, string? barcode)
        {
            var errors = new Dictionary<string, string[]>();

            // Validar CaptureType
            if (!Enum.IsDefined(typeof(CaptureType), captureType))
            {
                errors["captureType"] = [$"CaptureType inválido. Valores aceitos: {string.Join(", ", Enum.GetNames(typeof(CaptureType)))}"];
            }

            // Validar barcode quando CaptureType = Barcode
            if (captureType == CaptureType.Barcode)
            {
                if (string.IsNullOrWhiteSpace(barcode))
                {
                    errors["barcode"] = ["Barcode é obrigatório quando CaptureType = Barcode."];
                }
                else if (!IsValidBarcode(barcode))
                {
                    errors["barcode"] = ["Barcode inválido. Formatos aceitos: EAN-8 (8 dígitos), EAN-13 (13 dígitos), UPC-A (12 dígitos)."];
                }
            }
            else
            {
                // Arquivo obrigatório para outros tipos
                if (file == null || file.Length == 0)
                {
                    errors["file"] = ["Arquivo é obrigatório para este tipo de captura."];
                }
            }

            // Validar arquivo se presente
            if (file != null)
            {
                if (file.Length > MaxFileSize)
                {
                    errors["file"] = [$"Arquivo muito grande. Tamanho máximo permitido: {MaxFileSize / 1024 / 1024}MB."];
                }

                var extension = System.IO.Path.GetExtension(file.FileName)?.ToLowerInvariant();
                if (string.IsNullOrEmpty(extension) || !_allowedExtensions.Contains(extension))
                {
                    var existingErrors = errors.GetValueOrDefault("file", []);
                    errors["file"] = [.. existingErrors, $"Formato de arquivo não suportado. Formatos aceitos: {string.Join(", ", _allowedExtensions)}"];
                }
            }

            return errors;
        }

        private static bool IsValidBarcode(string barcode)
        {
            var cleanBarcode = barcode.Trim().Replace(" ", "").Replace("-", "");
            return System.Text.RegularExpressions.Regex.IsMatch(cleanBarcode, @"^\d{8}$|^\d{12}$|^\d{13}$");
        }

        private Guid? ExtractUserId()
        {
            if (User?.Identity?.IsAuthenticated != true)
                return null;

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value
                           ?? User.FindFirst("userId")?.Value;

            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var parsedUserId))
            {
                return parsedUserId;
            }

            return null;
        }

        private async Task<CapturedImageAnalysisResponse> ProcessBarcodeOnlyAsync(
            string barcode,
            Guid? userId,
            CapturedImageAnalysisResponse response)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Processar apenas com barcode (sem imagem)
                var result = await _orchestrator.ProcessBarcodeAsync(barcode, userId);

                stopwatch.Stop();

                response.Success = result.AnalysisResult != null;
                response.OverallConfidence = response.Success ? 0.95 : 0.0;
                response.IdentificationResult = new ProductIdentificationResultDto
                {
                    Success = true,
                    Method = "BarcodeInput",
                    Confidence = 1.0,
                    Barcode = barcode,
                    ProductName = result.AnalysisResult?.ProductName,
                    Brand = result.AnalysisResult?.Brand,
                    DataSource = "Manual Entry",
                    IsFromExternalDatabase = false
                };
                response.FinalAnalysis = result.AnalysisResult;
                response.Metadata.EndTime = DateTime.UtcNow;
                response.Metadata.TotalProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                response.Metadata.Steps.Add(new PipelineStepMetadataDto
                {
                    StepName = "BarcodeProcessing",
                    Success = true,
                    DurationMs = stopwatch.Elapsed.TotalMilliseconds
                });

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                response.Success = false;
                response.ErrorMessage = $"Erro ao processar barcode: {ex.Message}";
                response.Metadata.EndTime = DateTime.UtcNow;
                response.Metadata.TotalProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                return response;
            }
        }

        private static CapturedImageAnalysisResponse MapPipelineResultToResponse(
            ProductAnalysisPipelineResultDto pipelineResult,
            CaptureType captureType,
            Stopwatch stopwatch)
        {
            stopwatch.Stop();

            return new CapturedImageAnalysisResponse
            {
                Success = pipelineResult.AnalysisResult != null,
                CaptureType = captureType,
                OverallConfidence = pipelineResult.Metadata.OcrStep.Success ? 0.85 : 0.0,
                IdentificationResult = new ProductIdentificationResultDto
                {
                    Success = !string.IsNullOrEmpty(pipelineResult.AnalysisResult?.ProductName),
                    Method = "OCR",
                    Confidence = pipelineResult.Metadata.OcrStep.Success ? 0.85 : 0.0,
                    ProductName = pipelineResult.AnalysisResult?.ProductName,
                    Brand = pipelineResult.AnalysisResult?.Brand,
                    DataSource = pipelineResult.Metadata.OcrProviderName ?? "Unknown"
                },
                LabelReadingResult = new LabelReadingResultDto
                {
                    Success = pipelineResult.Metadata.OcrStep.Success,
                    Confidence = pipelineResult.Metadata.OcrStep.Success ? 0.85 : 0.0,
                    RawText = pipelineResult.AnalysisResult?.ExtractedText,
                    Ingredients = pipelineResult.AnalysisResult?.ExtractedIngredients ?? [],
                    Allergens = pipelineResult.AnalysisResult?.ExtractedAllergens ?? [],
                    OcrProvider = pipelineResult.Metadata.OcrProviderName,
                    OcrProcessingTimeMs = pipelineResult.Metadata.OcrStep.DurationMs
                },
                FinalAnalysis = pipelineResult.AnalysisResult,
                Metadata = new AnalysisMetadataDto
                {
                    ProcessingId = pipelineResult.Metadata.PipelineId,
                    StartTime = pipelineResult.Metadata.StartTime,
                    EndTime = pipelineResult.Metadata.EndTime,
                    TotalProcessingTimeMs = pipelineResult.Metadata.TotalDurationMs,
                    OcrProvider = pipelineResult.Metadata.OcrProviderName,
                    OcrProviderVersion = pipelineResult.Metadata.OcrProviderVersion,
                    Steps =
                    [
                        new PipelineStepMetadataDto
                        {
                            StepName = "Upload",
                            Success = pipelineResult.Metadata.UploadStep.Success,
                            DurationMs = pipelineResult.Metadata.UploadStep.DurationMs,
                            ErrorMessage = pipelineResult.Metadata.UploadStep.ErrorMessage
                        },
                        new PipelineStepMetadataDto
                        {
                            StepName = "OCR",
                            Success = pipelineResult.Metadata.OcrStep.Success,
                            DurationMs = pipelineResult.Metadata.OcrStep.DurationMs,
                            ErrorMessage = pipelineResult.Metadata.OcrStep.ErrorMessage
                        },
                        new PipelineStepMetadataDto
                        {
                            StepName = "Parsing",
                            Success = pipelineResult.Metadata.ParsingStep.Success,
                            DurationMs = pipelineResult.Metadata.ParsingStep.DurationMs,
                            ErrorMessage = pipelineResult.Metadata.ParsingStep.ErrorMessage
                        },
                        new PipelineStepMetadataDto
                        {
                            StepName = "Analysis",
                            Success = pipelineResult.Metadata.AnalysisStep.Success,
                            DurationMs = pipelineResult.Metadata.AnalysisStep.DurationMs,
                            ErrorMessage = pipelineResult.Metadata.AnalysisStep.ErrorMessage
                        }
                    ]
                },
                Warnings = [],
                Recommendations = pipelineResult.AnalysisResult?.Recommendations ?? []
            };
        }

        private static string GetCaptureTypeDescription(CaptureType captureType)
        {
            return captureType switch
            {
                CaptureType.Barcode => "Código de barras (EAN, UPC) para identificação do produto em bases externas.",
                CaptureType.FrontPackaging => "Embalagem frontal do produto com marca, nome e claims nutricionais.",
                CaptureType.NutritionTable => "Tabela nutricional com valores energéticos, macro e micronutrientes.",
                CaptureType.IngredientsList => "Lista de ingredientes em ordem decrescente de quantidade.",
                CaptureType.AllergenStatement => "Declaração de alérgenos com alertas sobre presença ou traços.",
                _ => "Tipo de captura não especificado."
            };
        }

        #endregion
    }

    /// <summary>
    /// Informações sobre um tipo de captura suportado.
    /// </summary>
    public class CaptureTypeInfoDto
    {
        /// <summary>
        /// Valor numérico do tipo de captura.
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// Nome do tipo de captura.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Descrição do tipo de captura.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Indica se este tipo requer upload de arquivo.
        /// </summary>
        public bool RequiresFile { get; set; }

        /// <summary>
        /// Indica se este tipo requer código de barras.
        /// </summary>
        public bool RequiresBarcode { get; set; }
    }
}
