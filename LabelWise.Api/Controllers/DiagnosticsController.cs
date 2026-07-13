using Microsoft.AspNetCore.Mvc;
using LabelWise.Application.Interfaces;
using System.Reflection;

namespace LabelWise.Api.Controllers
{
    /// <summary>
    /// Controller para diagnóstico e verificação de configuração da API.
    /// </summary>
    [ApiController]
    [Route("api/diagnostics")]
    public class DiagnosticsController : ControllerBase
    {
        private readonly IOcrProvider _ocrProvider;

        public DiagnosticsController(IOcrProvider ocrProvider)
        {
            _ocrProvider = ocrProvider;
        }

        /// <summary>
        /// Retorna informações sobre o OCR Provider atualmente configurado.
        /// Útil para validar que o provider correto está sendo usado.
        /// </summary>
        [HttpGet("ocr-provider")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public IActionResult GetOcrProviderInfo()
        {
            var providerType = _ocrProvider.GetType();
            var assembly = providerType.Assembly;

            return Ok(new
            {
                provider = new
                {
                    name = _ocrProvider.ProviderName,
                    type = providerType.FullName,
                    typeName = providerType.Name,
                    assembly = assembly.GetName().Name,
                    assemblyVersion = assembly.GetName().Version?.ToString(),
                    isRealOcr = !_ocrProvider.ProviderName.Contains("Mock", StringComparison.OrdinalIgnoreCase),
                    isMock = _ocrProvider.ProviderName.Contains("Mock", StringComparison.OrdinalIgnoreCase)
                },
                configuration = new
                {
                    configuredProvider = Environment.GetEnvironmentVariable("OcrProvider__Provider") 
                                       ?? "Not set via environment (check appsettings.json)",
                    tessdataPath = Environment.GetEnvironmentVariable("OcrProvider__TessdataPath") 
                                 ?? Environment.GetEnvironmentVariable("TESSDATA_PREFIX")
                                 ?? "Not set (auto-detect mode)",
                    language = Environment.GetEnvironmentVariable("OcrProvider__Language") 
                             ?? "Not set via environment (check appsettings.json)"
                },
                diagnostic = new
                {
                    timestamp = DateTime.UtcNow,
                    message = _ocrProvider.ProviderName.Contains("Mock", StringComparison.OrdinalIgnoreCase)
                        ? "⚠️ WARNING: Using Mock OCR Provider. Set OcrProvider:Provider='Tesseract' in appsettings.json for real OCR."
                        : "✅ Using real OCR provider."
                }
            });
        }

        /// <summary>
        /// Retorna informações sobre a API e suas versões.
        /// </summary>
        [HttpGet("info")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public IActionResult GetApiInfo()
        {
            var apiAssembly = Assembly.GetExecutingAssembly();
            var appAssembly = Assembly.Load("LabelWise.Application");
            var infraAssembly = Assembly.Load("LabelWise.Infrastructure");

            return Ok(new
            {
                api = new
                {
                    name = "LabelWise API",
                    version = apiAssembly.GetName().Version?.ToString() ?? "Unknown",
                    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
                },
                assemblies = new
                {
                    api = apiAssembly.GetName().Version?.ToString(),
                    application = appAssembly.GetName().Version?.ToString(),
                    infrastructure = infraAssembly.GetName().Version?.ToString()
                },
                timestamp = DateTime.UtcNow
            });
        }
    }
}
