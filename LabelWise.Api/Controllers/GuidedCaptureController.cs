using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using LabelWise.Application.DTOs.GuidedCapture;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;

namespace LabelWise.Api.Controllers
{
    /// <summary>
    /// Controller para fluxo de captura guiada em apps mobile.
    /// Permite análise passo-a-passo de produtos alimentícios.
    /// </summary>
    /// <remarks>
    /// ## Fluxo de Uso
    /// 
    /// 1. **POST /api/guided-capture/sessions** - Inicia uma nova sessão
    /// 2. **POST /api/guided-capture/sessions/{sessionId}/captures** - Adiciona capturas (repetir para cada etapa)
    /// 3. **GET /api/guided-capture/sessions/{sessionId}** - Verifica progresso
    /// 4. **POST /api/guided-capture/sessions/{sessionId}/finalize** - Finaliza e gera análise
    ///
    /// ## Etapas de Captura
    /// 
    /// | Ordem | Tipo | Obrigatório | Descrição |
    /// |-------|------|-------------|-----------|
    /// | 1 | FrontPackaging | Não | Embalagem frontal |
    /// | 2 | IngredientsList | Sim | Lista de ingredientes |
    /// | 3 | NutritionTable | Sim | Tabela nutricional |
    /// | 4 | AllergenStatement | Não | Declaração de alérgenos |
    /// | 5 | Barcode | Não | Código de barras |
    /// </remarks>
    [ApiController]
    [Route("api/guided-capture")]
    [Produces("application/json")]
    public class GuidedCaptureController : ControllerBase
    {
        private readonly IGuidedCaptureService _guidedCaptureService;
        private static readonly string[] _allowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
        private const long MaxFileSize = 10 * 1024 * 1024; // 10MB

        public GuidedCaptureController(IGuidedCaptureService guidedCaptureService)
        {
            _guidedCaptureService = guidedCaptureService;
        }

        /// <summary>
        /// Inicia uma nova sessão de captura guiada.
        /// </summary>
        /// <remarks>
        /// Cria uma nova sessão e retorna o SessionId que deve ser usado em todas as chamadas subsequentes.
        /// 
        /// **Exemplo de Request:**
        /// ```json
        /// {
        ///   "languageCode": "pt-BR",
        ///   "deviceInfo": "LabelWise iOS 2.1.0"
        /// }
        /// ```
        /// 
        /// **Exemplo de Response:**
        /// ```json
        /// {
        ///   "sessionId": "550e8400-e29b-41d4-a716-446655440000",
        ///   "status": "Started",
        ///   "startedAt": "2024-01-15T10:30:00Z",
        ///   "firstStep": {
        ///     "captureType": 2,
        ///     "stepName": "Lista de Ingredientes",
        ///     "description": "Fotografe a lista de ingredientes completa",
        ///     "isRequired": true
        ///   }
        /// }
        /// ```
        /// </remarks>
        /// <param name="request">Dados para iniciar a sessão.</param>
        /// <returns>Sessão criada com instruções iniciais.</returns>
        /// <response code="201">Sessão criada com sucesso.</response>
        /// <response code="400">Dados de entrada inválidos.</response>
        [HttpPost("sessions")]
        [ProducesResponseType(typeof(StartGuidedSessionResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> StartSession([FromBody] StartGuidedSessionRequest? request)
        {
            request ??= new StartGuidedSessionRequest();

            // Try to get user ID from claims if authenticated
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(userIdClaim, out var userId))
                {
                    request.UserId = userId;
                }
            }

            var result = await _guidedCaptureService.StartSessionAsync(request);

            return CreatedAtAction(
                nameof(GetSessionStatus),
                new { sessionId = result.SessionId },
                result);
        }

        /// <summary>
        /// Obtém o status atual de uma sessão.
        /// </summary>
        /// <remarks>
        /// Retorna o progresso completo da sessão, incluindo quais etapas foram completadas
        /// e qual é a próxima etapa recomendada.
        /// 
        /// **Campos de Progresso:**
        /// - `progress.completedSteps` - Número de etapas completadas
        /// - `progress.percentComplete` - Percentual de conclusão (0-100)
        /// - `progress.readyForAnalysis` - Se pode finalizar a análise
        /// - `nextStep` - Próxima etapa recomendada
        /// </remarks>
        /// <param name="sessionId">ID da sessão.</param>
        /// <returns>Status atual da sessão.</returns>
        /// <response code="200">Status retornado com sucesso.</response>
        /// <response code="404">Sessão não encontrada.</response>
        [HttpGet("sessions/{sessionId:guid}")]
        [ProducesResponseType(typeof(GuidedCaptureSessionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSessionStatus(Guid sessionId)
        {
            var session = await _guidedCaptureService.GetSessionStatusAsync(sessionId);

            if (session is null)
            {
                return NotFound(new { message = "Sessão não encontrada" });
            }

            return Ok(session);
        }

        /// <summary>
        /// Adiciona uma captura a uma sessão existente.
        /// </summary>
        /// <remarks>
        /// Processa uma imagem ou código de barras e adiciona à sessão.
        /// O OCR é executado automaticamente para extrair as informações.
        /// 
        /// **Tipos de Captura:**
        /// | Valor | Nome | Requer Imagem | Requer Barcode |
        /// |-------|------|---------------|----------------|
        /// | 1 | Barcode | Não | Sim |
        /// | 2 | FrontPackaging | Sim | Não |
        /// | 3 | NutritionTable | Sim | Não |
        /// | 4 | IngredientsList | Sim | Não |
        /// | 5 | AllergenStatement | Sim | Não |
        /// 
        /// **Exemplo com cURL:**
        /// ```bash
        /// curl -X POST "https://api.example.com/api/guided-capture/sessions/{sessionId}/captures" \
        ///   -H "Content-Type: multipart/form-data" \
        ///   -F "file=@ingredientes.jpg" \
        ///   -F "captureType=4"
        /// ```
        /// </remarks>
        /// <param name="sessionId">ID da sessão.</param>
        /// <param name="file">Arquivo de imagem (obrigatório exceto para Barcode).</param>
        /// <param name="captureType">Tipo de captura (1=Barcode, 2=FrontPackaging, 3=NutritionTable, 4=IngredientsList, 5=AllergenStatement).</param>
        /// <param name="barcode">Código de barras (obrigatório para captureType=1).</param>
        /// <param name="languageCode">Idioma para OCR (padrão: pt).</param>
        /// <returns>Resultado da captura com dados extraídos.</returns>
        /// <response code="200">Captura adicionada com sucesso.</response>
        /// <response code="400">Dados de entrada inválidos.</response>
        /// <response code="404">Sessão não encontrada.</response>
        [HttpPost("sessions/{sessionId:guid}/captures")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(AddCaptureResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10MB
        public async Task<IActionResult> AddCapture(
            Guid sessionId,
            [FromForm] Models.AddCaptureFormModel model)
        {
            // Validate based on capture type
            if (model.CaptureType == CaptureType.Barcode)
            {
                if (string.IsNullOrWhiteSpace(model.Barcode))
                {
                    return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
                    {
                        ["barcode"] = ["Código de barras é obrigatório para este tipo de captura"]
                    }));
                }
            }
            else
            {
                if (model.File is null || model.File.Length == 0)
                {
                    return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
                    {
                        ["file"] = ["Arquivo de imagem é obrigatório"]
                    }));
                }

                // Validate file
                var validationError = ValidateImageFile(model.File);
                if (validationError is not null)
                {
                    return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
                    {
                        ["file"] = [validationError]
                    }));
                }
            }

            // Create stream from file if present
            Stream? imageStream = null;
            string? fileName = null;
            if (model.File is not null)
            {
                imageStream = model.File.OpenReadStream();
                fileName = model.File.FileName;
            }

            try
            {
                var result = await _guidedCaptureService.AddCaptureAsync(
                    sessionId,
                    model.CaptureType,
                    imageStream,
                    fileName,
                    model.Barcode,
                    model.LanguageCode);

                if (!result.Success && result.ErrorMessage?.Contains("não encontrada") == true)
                {
                    return NotFound(new { message = result.ErrorMessage });
                }

                if (!result.Success)
                {
                    return BadRequest(new { message = result.ErrorMessage, warnings = result.Warnings });
                }

                return Ok(result);
            }
            finally
            {
                imageStream?.Dispose();
            }
        }

        /// <summary>
        /// Remove uma captura específica de uma sessão.
        /// </summary>
        /// <remarks>
        /// Permite ao usuário refazer uma captura específica.
        /// O arquivo de imagem associado também é removido.
        /// </remarks>
        /// <param name="sessionId">ID da sessão.</param>
        /// <param name="captureId">ID da captura a remover.</param>
        /// <returns>Status atualizado da sessão.</returns>
        /// <response code="200">Captura removida com sucesso.</response>
        /// <response code="404">Sessão ou captura não encontrada.</response>
        [HttpDelete("sessions/{sessionId:guid}/captures/{captureId:guid}")]
        [ProducesResponseType(typeof(GuidedCaptureSessionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RemoveCapture(Guid sessionId, Guid captureId)
        {
            var result = await _guidedCaptureService.RemoveCaptureAsync(sessionId, captureId);

            if (result is null)
            {
                return NotFound(new { message = "Sessão ou captura não encontrada" });
            }

            return Ok(result);
        }

        /// <summary>
        /// Obtém a próxima etapa recomendada para uma sessão.
        /// </summary>
        /// <remarks>
        /// Retorna a próxima etapa que o usuário deve completar,
        /// com dicas para uma boa captura.
        /// </remarks>
        /// <param name="sessionId">ID da sessão.</param>
        /// <returns>Recomendação da próxima etapa.</returns>
        /// <response code="200">Recomendação retornada com sucesso.</response>
        /// <response code="404">Sessão não encontrada.</response>
        [HttpGet("sessions/{sessionId:guid}/next-step")]
        [ProducesResponseType(typeof(NextStepRecommendationDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetNextStep(Guid sessionId)
        {
            var nextStep = await _guidedCaptureService.GetNextStepRecommendationAsync(sessionId);

            if (nextStep is null)
            {
                return NotFound(new { message = "Sessão não encontrada" });
            }

            return Ok(nextStep);
        }

        /// <summary>
        /// Finaliza a sessão e gera a análise nutricional completa.
        /// </summary>
        /// <remarks>
        /// Consolida todas as capturas e gera:
        /// - Informações consolidadas do produto
        /// - Análise nutricional com scores
        /// - Alertas (alérgenos, alto teor de açúcar/sódio, etc.)
        /// - Recomendações personalizadas
        /// 
        /// **Requisitos:**
        /// - Pelo menos a Tabela Nutricional E Lista de Ingredientes devem ter sido capturadas
        /// - Use `forceAnalysis=true` para analisar com dados incompletos
        /// 
        /// **Exemplo de Request:**
        /// ```json
        /// {
        ///   "sessionId": "550e8400-e29b-41d4-a716-446655440000",
        ///   "forceAnalysis": false,
        ///   "includePersonalizedRecommendations": true,
        ///   "explanationLevel": "Standard"
        /// }
        /// ```
        /// </remarks>
        /// <param name="sessionId">ID da sessão.</param>
        /// <param name="request">Parâmetros para finalização.</param>
        /// <returns>Resultado completo da análise.</returns>
        /// <response code="200">Análise gerada com sucesso.</response>
        /// <response code="400">Etapas obrigatórias não completadas.</response>
        /// <response code="404">Sessão não encontrada.</response>
        [HttpPost("sessions/{sessionId:guid}/finalize")]
        [ProducesResponseType(typeof(FinalizeAnalysisResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> FinalizeAnalysis(
            Guid sessionId,
            [FromBody] FinalizeAnalysisRequest? request)
        {
            request ??= new FinalizeAnalysisRequest();
            request.SessionId = sessionId;

            // Try to get user ID from claims
            if (User.Identity?.IsAuthenticated == true && !request.UserId.HasValue)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(userIdClaim, out var userId))
                {
                    request.UserId = userId;
                }
            }

            var result = await _guidedCaptureService.FinalizeAnalysisAsync(request);

            if (!result.Success)
            {
                if (result.ErrorMessage?.Contains("não encontrada") == true)
                {
                    return NotFound(new { message = result.ErrorMessage });
                }

                return BadRequest(new
                {
                    message = result.ErrorMessage,
                    warnings = result.Warnings
                });
            }

            return Ok(result);
        }

        /// <summary>
        /// Cancela uma sessão em andamento.
        /// </summary>
        /// <remarks>
        /// Remove todas as imagens capturadas e marca a sessão como cancelada.
        /// Esta ação não pode ser desfeita.
        /// </remarks>
        /// <param name="sessionId">ID da sessão.</param>
        /// <returns>Confirmação do cancelamento.</returns>
        /// <response code="200">Sessão cancelada com sucesso.</response>
        /// <response code="404">Sessão não encontrada.</response>
        [HttpPost("sessions/{sessionId:guid}/cancel")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelSession(Guid sessionId)
        {
            var success = await _guidedCaptureService.CancelSessionAsync(sessionId);

            if (!success)
            {
                return NotFound(new { message = "Sessão não encontrada" });
            }

            return Ok(new { message = "Sessão cancelada com sucesso", sessionId });
        }

        /// <summary>
        /// Lista todas as definições de etapas de captura.
        /// </summary>
        /// <remarks>
        /// Retorna as definições de todas as etapas disponíveis,
        /// útil para construir a UI do app mobile.
        /// 
        /// Cada etapa inclui:
        /// - Nome e descrição
        /// - Se é obrigatória
        /// - Dicas para uma boa captura
        /// - Ícone sugerido
        /// </remarks>
        /// <param name="languageCode">Idioma para as descrições (pt ou en).</param>
        /// <returns>Lista de etapas de captura.</returns>
        /// <response code="200">Lista de etapas retornada com sucesso.</response>
        [HttpGet("steps")]
        [ProducesResponseType(typeof(List<CaptureStepDefinitionDto>), StatusCodes.Status200OK)]
        public IActionResult GetCaptureSteps([FromQuery] string languageCode = "pt")
        {
            var steps = _guidedCaptureService.GetCaptureStepDefinitions(languageCode);
            return Ok(steps);
        }

        #region Private Methods

        private static string? ValidateImageFile(IFormFile file)
        {
            if (file.Length > MaxFileSize)
            {
                return $"Arquivo muito grande. Tamanho máximo: {MaxFileSize / (1024 * 1024)}MB";
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension))
            {
                return $"Formato de arquivo não suportado. Use: {string.Join(", ", _allowedExtensions)}";
            }

            return null;
        }

        #endregion
    }
}
