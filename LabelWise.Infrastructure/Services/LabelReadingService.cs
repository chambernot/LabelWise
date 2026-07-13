using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LabelWise.Application.DTOs;
using LabelWise.Application.DTOs.LabelReading;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Parsing;
using LabelWise.Domain.Enums;
using LabelWise.Infrastructure.Services.LabelReading;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services
{
    /// <summary>
    /// Serviço de leitura de rótulos que integra OCR + Parsing especializado.
    /// 
    /// ARQUITETURA:
    /// ┌─────────────────────────────────────────────────────────┐
    /// │           LabelReadingService                            │
    /// │  (Orquestração principal)                                │
    /// └─────────────────────────────────────────────────────────┘
    ///                      ↓
    /// ┌─────────────────────────────────────────────────────────┐
    /// │  IOcrProvider (Tesseract/Azure/Selector)                │
    /// │  - Extração de texto bruto                               │
    /// └─────────────────────────────────────────────────────────┘
    ///                      ↓
    /// ┌─────────────────────────────────────────────────────────┐
    /// │  Capture Strategy (por CaptureType)                     │
    /// │  - NutritionTableStrategy                                │
    /// │  - IngredientsListStrategy                               │
    /// │  - AllergenStatementStrategy                             │
    /// │  - FrontPackagingStrategy                                │
    /// └─────────────────────────────────────────────────────────┘
    ///                      ↓
    /// ┌─────────────────────────────────────────────────────────┐
    /// │  Structured Data (resultado final)                      │
    /// └─────────────────────────────────────────────────────────┘
    /// 
    /// FLUXO:
    /// 1. Recebe múltiplas capturas (nutrition table, ingredients, allergens)
    /// 2. Para cada captura:
    ///    a) Executa OCR (com fallback strategy se configurado)
    ///    b) Aplica estratégia de parsing específica do CaptureType
    ///    c) Valida qualidade da extração
    ///    d) Estrutura os dados
    /// 3. Consolida informações de todas as capturas
    /// 4. Retorna resultado com confiança geral
    /// </summary>
    public class LabelReadingService : ILabelReadingService
    {
        private readonly IOcrProvider _ocrProvider;
        private readonly IIngredientAllergenParser _parser;
        private readonly Dictionary<CaptureType, ICaptureReadingStrategy> _strategies;
        private readonly ILogger<LabelReadingService> _logger;

        public LabelReadingService(
            IOcrProvider ocrProvider,
            IIngredientAllergenParser parser,
            ILogger<LabelReadingService> logger)
        {
            _ocrProvider = ocrProvider ?? throw new ArgumentNullException(nameof(ocrProvider));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Inicializar estratégias de leitura por tipo de captura
            _strategies = new Dictionary<CaptureType, ICaptureReadingStrategy>
            {
                [CaptureType.NutritionTable] = new NutritionTableReadingStrategy(_parser, _logger),
                [CaptureType.IngredientsList] = new IngredientsListReadingStrategy(_parser, _logger),
                [CaptureType.AllergenStatement] = new AllergenStatementReadingStrategy(_parser, _logger),
                [CaptureType.FrontPackaging] = new FrontPackagingReadingStrategy(_parser, _logger),
                [CaptureType.Barcode] = new BarcodeReadingStrategy(_logger)
            };

            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            _logger.LogInformation("📖 LabelReadingService Inicializado");
            _logger.LogInformation("   OCR Provider: {Provider}", _ocrProvider.ProviderName);
            _logger.LogInformation("   Parser: {Parser}", _parser.GetType().Name);
            _logger.LogInformation("   Estratégias: {Count} tipos de captura suportados", _strategies.Count);
            _logger.LogInformation("═══════════════════════════════════════════════════════════");
        }

        public async Task<LabelReadingResult> ReadLabelAsync(LabelReadingRequest request)
        {
            _logger.LogInformation("🎯 Iniciando leitura de rótulo");
            _logger.LogInformation("   UserId: {UserId}", request.UserId);
            _logger.LogInformation("   Capturas: {Count}", request.Captures.Count);
            _logger.LogInformation("   Idioma: {Language}", request.LanguageCode);

            var stopwatch = Stopwatch.StartNew();

            var result = new LabelReadingResult
            {
                Success = true,
                Metadata = new Dictionary<string, string>
                {
                    ["OcrProvider"] = _ocrProvider.ProviderName,
                    ["LanguageCode"] = request.LanguageCode,
                    ["TotalCaptures"] = request.Captures.Count.ToString()
                }
            };

            // Processar cada captura
            foreach (var capture in request.Captures.OrderBy(c => c.Priority))
            {
                try
                {
                    _logger.LogInformation("📸 Processando captura: {CaptureType} (Priority: {Priority})",
                        capture.CaptureType, capture.Priority);

                    var captureResult = await ProcessCaptureAsync(capture, request);
                    result.CaptureResults.Add(captureResult);

                    if (!captureResult.Success)
                    {
                        _logger.LogWarning("⚠️ Falha ao processar {CaptureType}: {Error}",
                            capture.CaptureType, captureResult.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Erro ao processar captura {CaptureType}", capture.CaptureType);

                    result.CaptureResults.Add(new CaptureReadingResult
                    {
                        CaptureType = capture.CaptureType,
                        Success = false,
                        ErrorMessage = $"Exceção: {ex.Message}",
                        Confidence = 0
                    });
                }
            }

            // Consolidar informações de todas as capturas
            ConsolidateResults(result);

            // Calcular confiança geral
            result.OverallConfidence = CalculateOverallConfidence(result.CaptureResults);

            // Se nenhuma captura foi bem-sucedida, marcar como falha
            if (!result.CaptureResults.Any(c => c.Success))
            {
                result.Success = false;
                result.ErrorMessage = "Nenhuma captura foi processada com sucesso";
            }

            stopwatch.Stop();
            result.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;

            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            _logger.LogInformation("📊 RESULTADO DA LEITURA");
            _logger.LogInformation("   Success: {Success}", result.Success);
            _logger.LogInformation("   Confiança Geral: {Confidence:P2}", result.OverallConfidence);
            _logger.LogInformation("   Capturas OK: {Ok}/{Total}",
                result.CaptureResults.Count(c => c.Success),
                result.CaptureResults.Count);
            _logger.LogInformation("   Ingredientes: {Count}", result.Ingredients.Count);
            _logger.LogInformation("   Alérgenos: {Count}", result.Allergens.Count);
            _logger.LogInformation("   Info Nutricional: {HasNutrition}", result.NutritionalInfo != null);
            _logger.LogInformation("   Tempo: {Time:F2}s", result.ProcessingTimeSeconds);
            _logger.LogInformation("═══════════════════════════════════════════════════════════");

            return result;
        }

        public async Task<NutritionalInformationDto?> ReadNutritionTableAsync(byte[] imageData, string languageCode = "pt")
        {
            _logger.LogInformation("📊 Lendo tabela nutricional (método específico)");

            var capture = new LabelCapture
            {
                CaptureType = CaptureType.NutritionTable,
                ImageData = imageData
            };

            var request = new LabelReadingRequest
            {
                UserId = 0,
                LanguageCode = languageCode,
                Captures = new List<LabelCapture> { capture }
            };

            var result = await ReadLabelAsync(request);

            return result.NutritionalInfo;
        }

        public async Task<List<string>> ReadIngredientsAsync(byte[] imageData, string languageCode = "pt")
        {
            _logger.LogInformation("🧪 Lendo lista de ingredientes (método específico)");

            var capture = new LabelCapture
            {
                CaptureType = CaptureType.IngredientsList,
                ImageData = imageData
            };

            var request = new LabelReadingRequest
            {
                UserId = 0,
                LanguageCode = languageCode,
                Captures = new List<LabelCapture> { capture }
            };

            var result = await ReadLabelAsync(request);

            return result.Ingredients;
        }

        public async Task<List<string>> ReadAllergensAsync(byte[] imageData, string languageCode = "pt")
        {
            _logger.LogInformation("⚠️ Lendo declaração de alérgenos (método específico)");

            var capture = new LabelCapture
            {
                CaptureType = CaptureType.AllergenStatement,
                ImageData = imageData
            };

            var request = new LabelReadingRequest
            {
                UserId = 0,
                LanguageCode = languageCode,
                Captures = new List<LabelCapture> { capture }
            };

            var result = await ReadLabelAsync(request);

            return result.Allergens;
        }

        public async Task<Dictionary<string, string>> ValidateReadingQualityAsync(LabelReadingResult result)
        {
            await Task.CompletedTask; // Placeholder

            var quality = new Dictionary<string, string>();

            // Calcular qualidade geral
            var overallQuality = result.OverallConfidence >= 0.85 ? "Excellent" :
                                result.OverallConfidence >= 0.70 ? "Good" :
                                result.OverallConfidence >= 0.50 ? "Fair" : "Poor";

            quality["OverallQuality"] = overallQuality;
            quality["OcrConfidence"] = result.OverallConfidence.ToString("F2");

            // Calcular completude dos dados
            var completeness = 0.0;
            if (result.NutritionalInfo != null) completeness += 0.4;
            if (result.Ingredients.Any()) completeness += 0.3;
            if (result.Allergens.Any()) completeness += 0.2;
            if (result.NutritionalClaims.Any()) completeness += 0.1;

            quality["DataCompleteness"] = completeness.ToString("F2");

            // Identificar problemas
            var issues = new List<string>();
            if (!result.Success) issues.Add("Leitura falhou");
            if (result.OverallConfidence < 0.5) issues.Add("Baixa confiança do OCR");
            if (result.NutritionalInfo == null) issues.Add("Informação nutricional não extraída");
            if (!result.Ingredients.Any()) issues.Add("Ingredientes não extraídos");
            if (result.Warnings.Any()) issues.AddRange(result.Warnings);

            quality["Issues"] = System.Text.Json.JsonSerializer.Serialize(issues);

            _logger.LogDebug("Validação de qualidade: {Quality}, Completude: {Completeness:P0}, Issues: {IssueCount}",
                overallQuality, completeness, issues.Count);

            return quality;
        }

        public async Task<Dictionary<string, string>> GetServiceStatusAsync()
        {
            var status = new Dictionary<string, string>();

            try
            {
                // Verificar disponibilidade do OCR Provider
                var ocrAvailable = await _ocrProvider.IsAvailableAsync();
                var providers = new List<string> { _ocrProvider.ProviderName };

                status["OcrProviders"] = System.Text.Json.JsonSerializer.Serialize(new
                {
                    available = ocrAvailable,
                    providers = providers
                });

                status["ParsingEngine"] = "Available";
                status["QualityValidator"] = "Available";

                _logger.LogDebug("Status do serviço: OCR={OcrAvailable}, Parser=Available", ocrAvailable);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter status do serviço");
                status["Error"] = ex.Message;
            }

            return status;
        }

        public async Task<Dictionary<string, string>> GetUsageStatisticsAsync()
        {
            await Task.CompletedTask; // Placeholder

            // TODO: Implementar rastreamento de métricas
            var stats = new Dictionary<string, string>
            {
                ["TotalReadings"] = "0",
                ["SuccessRate"] = "0.0",
                ["AverageConfidence"] = "0.0",
                ["ProviderUsage"] = "{}"
            };

            _logger.LogDebug("Estatísticas de uso solicitadas (não implementado)");

            return stats;
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // MÉTODOS PRIVADOS
        // ═══════════════════════════════════════════════════════════════════════════════

        private async Task<CaptureReadingResult> ProcessCaptureAsync(
            LabelCapture capture,
            LabelReadingRequest request)
        {
            var captureStopwatch = Stopwatch.StartNew();

            var result = new CaptureReadingResult
            {
                CaptureType = capture.CaptureType,
                Success = false,
                Confidence = 0
            };

            // ETAPA 1: Salvar imagem temporariamente
            string? tempImagePath = null;
            try
            {
                tempImagePath = Path.Combine(Path.GetTempPath(), $"ocr_{Guid.NewGuid()}{GetImageExtension(capture.ContentType)}");
                await File.WriteAllBytesAsync(tempImagePath, capture.ImageData);
                _logger.LogDebug("   → Imagem salva temporariamente: {Path}", tempImagePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao salvar imagem temporária");
                result.ErrorMessage = $"Erro ao salvar imagem: {ex.Message}";
                return result;
            }

            try
            {
                // ETAPA 2: OCR - Extrair texto bruto
                OcrResultDto ocrResult;
                try
                {
                    var ocrRequest = new OcrRequestDto
                    {
                        ImagePath = tempImagePath,
                        FileName = capture.FileName ?? $"{capture.CaptureType}.jpg",
                        ContentType = capture.ContentType ?? "image/jpeg"
                    };

                    _logger.LogDebug("   → Executando OCR para {CaptureType}...", capture.CaptureType);
                    ocrResult = await _ocrProvider.ExtractTextAsync(ocrRequest);

                    result.OcrProvider = ocrResult.ProviderMetadata?.GetValueOrDefault("SelectedProvider")
                        ?? _ocrProvider.ProviderName;
                    result.RawText = ocrResult.RawText;
                    result.Confidence = ocrResult.Confidence;

                    if (!ocrResult.Success)
                    {
                        result.ErrorMessage = $"OCR falhou: {ocrResult.ErrorMessage}";
                        return result;
                    }

                    _logger.LogDebug("   ✅ OCR concluído: {Chars} caracteres, confiança {Confidence:P2}",
                        ocrResult.RawText.Length, ocrResult.Confidence);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Erro no OCR para {CaptureType}", capture.CaptureType);
                    result.ErrorMessage = $"Erro no OCR: {ex.Message}";
                    return result;
                }

                // ETAPA 3: Aplicar estratégia de parsing específica do CaptureType
                if (_strategies.TryGetValue(capture.CaptureType, out var strategy))
                {
                    try
                    {
                        _logger.LogDebug("   → Aplicando estratégia de parsing para {CaptureType}...",
                            capture.CaptureType);

                        var strategyResult = strategy.Parse(ocrResult.RawText, ocrResult.Confidence);

                        result.Success = strategyResult.Success;
                        result.Confidence = strategyResult.Confidence;
                        result.StructuredData = strategyResult.StructuredData;
                        result.Metadata = strategyResult.Metadata;

                        if (!strategyResult.Success)
                        {
                            result.ErrorMessage = strategyResult.ErrorMessage;
                        }

                        _logger.LogDebug("   ✅ Parsing concluído: Success={Success}, Confidence={Confidence:P2}",
                            strategyResult.Success, strategyResult.Confidence);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Erro no parsing para {CaptureType}", capture.CaptureType);
                        result.ErrorMessage = $"Erro no parsing: {ex.Message}";
                        result.Success = false;
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️ Nenhuma estratégia configurada para {CaptureType}", capture.CaptureType);
                    result.Success = true; // Pelo menos temos o texto bruto
                    result.ErrorMessage = "Nenhuma estratégia de parsing disponível";
                }

                captureStopwatch.Stop();
                result.ProcessingTimeSeconds = captureStopwatch.Elapsed.TotalSeconds;

                return result;
            }
            finally
            {
                // Limpar arquivo temporário
                if (!string.IsNullOrEmpty(tempImagePath) && File.Exists(tempImagePath))
                {
                    try
                    {
                        File.Delete(tempImagePath);
                        _logger.LogDebug("   → Arquivo temporário removido: {Path}", tempImagePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Erro ao remover arquivo temporário: {Path}", tempImagePath);
                    }
                }
            }
        }

        private string GetImageExtension(string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
                return ".jpg";

            return contentType.ToLowerInvariant() switch
            {
                "image/jpeg" => ".jpg",
                "image/jpg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
        }

        private void ConsolidateResults(LabelReadingResult result)
        {
            _logger.LogDebug("📦 Consolidando resultados de {Count} capturas...", result.CaptureResults.Count);

            foreach (var captureResult in result.CaptureResults.Where(c => c.Success))
            {
                try
                {
                    switch (captureResult.CaptureType)
                    {
                        case CaptureType.NutritionTable:
                            ConsolidateNutritionTable(captureResult, result);
                            break;

                        case CaptureType.IngredientsList:
                            ConsolidateIngredients(captureResult, result);
                            break;

                        case CaptureType.AllergenStatement:
                            ConsolidateAllergens(captureResult, result);
                            break;

                        case CaptureType.FrontPackaging:
                            ConsolidateFrontPackaging(captureResult, result);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Erro ao consolidar {CaptureType}", captureResult.CaptureType);
                    result.Warnings.Add($"Erro ao consolidar {captureResult.CaptureType}: {ex.Message}");
                }
            }
        }

        private void ConsolidateNutritionTable(CaptureReadingResult captureResult, LabelReadingResult result)
        {
            if (string.IsNullOrWhiteSpace(captureResult.StructuredData))
                return;

            try
            {
                var nutritionInfo = JsonSerializer.Deserialize<NutritionalInformationDto>(
                    captureResult.StructuredData,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (nutritionInfo != null)
                {
                    // Se já existe informação nutricional, mantém a de maior confiança
                    if (result.NutritionalInfo == null ||
                        captureResult.Confidence > result.CaptureResults
                            .FirstOrDefault(c => c.CaptureType == CaptureType.NutritionTable && c != captureResult)?.Confidence)
                    {
                        result.NutritionalInfo = nutritionInfo;
                        _logger.LogDebug("   ✅ Informação nutricional consolidada");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Erro ao deserializar informação nutricional");
            }
        }

        private void ConsolidateIngredients(CaptureReadingResult captureResult, LabelReadingResult result)
        {
            if (string.IsNullOrWhiteSpace(captureResult.StructuredData))
                return;

            try
            {
                var ingredients = JsonSerializer.Deserialize<List<string>>(
                    captureResult.StructuredData,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (ingredients != null && ingredients.Any())
                {
                    // Adicionar ingredientes únicos
                    foreach (var ingredient in ingredients)
                    {
                        if (!string.IsNullOrWhiteSpace(ingredient) &&
                            !result.Ingredients.Contains(ingredient, StringComparer.OrdinalIgnoreCase))
                        {
                            result.Ingredients.Add(ingredient);
                        }
                    }

                    _logger.LogDebug("   ✅ {Count} ingredientes consolidados", ingredients.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Erro ao deserializar ingredientes");
            }
        }

        private void ConsolidateAllergens(CaptureReadingResult captureResult, LabelReadingResult result)
        {
            if (string.IsNullOrWhiteSpace(captureResult.StructuredData))
                return;

            try
            {
                var allergens = JsonSerializer.Deserialize<List<string>>(
                    captureResult.StructuredData,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (allergens != null && allergens.Any())
                {
                    // Adicionar alérgenos únicos
                    foreach (var allergen in allergens)
                    {
                        if (!string.IsNullOrWhiteSpace(allergen) &&
                            !result.Allergens.Contains(allergen, StringComparer.OrdinalIgnoreCase))
                        {
                            result.Allergens.Add(allergen);
                        }
                    }

                    _logger.LogDebug("   ✅ {Count} alérgenos consolidados", allergens.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Erro ao deserializar alérgenos");
            }
        }

        private void ConsolidateFrontPackaging(CaptureReadingResult captureResult, LabelReadingResult result)
        {
            if (string.IsNullOrWhiteSpace(captureResult.StructuredData))
                return;

            try
            {
                var claims = JsonSerializer.Deserialize<List<string>>(
                    captureResult.StructuredData,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (claims != null && claims.Any())
                {
                    // Adicionar claims únicos
                    foreach (var claim in claims)
                    {
                        if (!string.IsNullOrWhiteSpace(claim) &&
                            !result.NutritionalClaims.Contains(claim, StringComparer.OrdinalIgnoreCase))
                        {
                            result.NutritionalClaims.Add(claim);
                        }
                    }

                    _logger.LogDebug("   ✅ {Count} claims nutricionais consolidados", claims.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Erro ao deserializar claims");
            }
        }

        private double CalculateOverallConfidence(List<CaptureReadingResult> captureResults)
        {
            var successfulCaptures = captureResults.Where(c => c.Success).ToList();

            if (!successfulCaptures.Any())
                return 0;

            // Média ponderada: capturas mais importantes têm maior peso
            var weightedSum = 0.0;
            var totalWeight = 0.0;

            foreach (var capture in successfulCaptures)
            {
                var weight = GetCaptureWeight(capture.CaptureType);
                weightedSum += capture.Confidence * weight;
                totalWeight += weight;
            }

            return totalWeight > 0 ? weightedSum / totalWeight : 0;
        }

        private double GetCaptureWeight(CaptureType captureType)
        {
            // Pesos por importância da captura
            return captureType switch
            {
                CaptureType.NutritionTable => 2.0,      // Mais importante
                CaptureType.IngredientsList => 2.0,     // Mais importante
                CaptureType.AllergenStatement => 1.5,   // Importante
                CaptureType.FrontPackaging => 1.0,      // Menos crítico
                CaptureType.Barcode => 0.5,             // Apenas para identificação
                _ => 1.0
            };
        }
    }
}
