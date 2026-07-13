using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using LabelWise.Application.Interfaces;

namespace LabelWise.Api.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductAnalysisController : ControllerBase
    {
        private readonly IProductAnalysisService _service;
        private static readonly string[] _allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxFileSize = 5 * 1024 * 1024; // 5MB

        public ProductAnalysisController(IProductAnalysisService service)
        {
            _service = service;
        }

        /// <summary>
        /// Endpoint principal para análise de imagem de produto.
        /// Recebe uma imagem, processa através da pipeline completa e retorna a análise.
        /// </summary>
        /// <param name="file">Arquivo de imagem do rótulo do produto</param>
        /// <returns>Análise completa do produto</returns>
        [HttpPost("analyze-image")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AnalyzeImage(IFormFile file)
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
                Guid? userId = null;
                if (User?.Identity?.IsAuthenticated == true)
                {
                    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                                   ?? User.FindFirst("sub")?.Value
                                   ?? User.FindFirst("userId")?.Value;

                    if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var parsedUserId))
                    {
                        userId = parsedUserId;
                    }
                }

                // Executar análise através da pipeline real
                using var stream = file.OpenReadStream();
                var result = await _service.AnalyzeImageAsync(stream, file.FileName, userId);

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
                // Log em produção
                Console.WriteLine($"[ERROR] ProductAnalysisController.AnalyzeImage: {ex.Message}");
                Console.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");

                return StatusCode(500, new { error = "Um erro inesperado ocorreu durante a análise." });
            }
        }
    }
}
