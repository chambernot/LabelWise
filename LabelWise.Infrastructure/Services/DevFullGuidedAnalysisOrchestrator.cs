using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LabelWise.Application.Confidence;
using LabelWise.Application.DTOs.Development;
using LabelWise.Application.DTOs.GuidedCapture;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;

namespace LabelWise.Infrastructure.Services
{
    /// <summary>
    /// Orquestrador para endpoint de desenvolvimento de análise guiada completa.
    /// </summary>
    public class DevFullGuidedAnalysisOrchestrator : IDevFullGuidedAnalysisOrchestrator
    {
        private readonly IGuidedCaptureService _guidedCaptureService;
        private readonly ILogger<DevFullGuidedAnalysisOrchestrator> _logger;

        public DevFullGuidedAnalysisOrchestrator(
            IGuidedCaptureService guidedCaptureService,
            ILogger<DevFullGuidedAnalysisOrchestrator> logger)
        {
            _guidedCaptureService = guidedCaptureService;
            _logger = logger;
        }

        public async Task<FullGuidedAnalysisResponse> ProcessFullGuidedAnalysisAsync(
            Dictionary<string, (Stream stream, string fileName)> images,
            string? barcode,
            int userId,
            string languageCode = "pt-BR",
            string deviceInfo = "DevEndpoint-Test",
            CancellationToken cancellationToken = default)
        {
            var totalStopwatch = Stopwatch.StartNew();
            var response = new FullGuidedAnalysisResponse
            {
                ProcessedAt = DateTime.UtcNow,
                Success = false
            };

            try
            {
                // 1. Criar sessão
                // Nota: userId é int mas GuidedCaptureService espera Guid?.
                // Para o endpoint de dev, vamos deixar como null e associar depois se necessário.
                var sessionRequest = new StartGuidedSessionRequest
                {
                    UserId = null,  // Simplificação: deixar null para dev endpoint
                    LanguageCode = languageCode,
                    DeviceInfo = deviceInfo
                };

                var sessionResponse = await _guidedCaptureService.StartSessionAsync(sessionRequest, cancellationToken);
                response.SessionId = sessionResponse.SessionId;

                // 2. Processar imagens
                foreach (var (key, (stream, fileName)) in images)
                {
                    var captureType = MapKeyToCaptureType(key);
                    var stepStopwatch = Stopwatch.StartNew();

                    try
                    {
                        var captureResponse = await _guidedCaptureService.AddCaptureAsync(
                            response.SessionId,
                            captureType,
                            stream,
                            fileName,
                            null,
                            languageCode,
                            true,
                            true,
                            cancellationToken);

                        var metadata = new ProcessedStepMetadata
                        {
                            CaptureType = captureType,
                            StepName = GetStepName(captureType, languageCode),
                            Success = captureResponse.Success,
                            Duration = stepStopwatch.Elapsed,
                            FileSizeBytes = stream.Length
                        };

                        if (captureResponse.Success)
                        {
                            metadata.OcrResult = new OcrStepResult
                            {
                                Success = true,
                                Confidence = (double)captureResponse.Confidence,
                                TextLength = 0,
                                Provider = "Auto"
                            };
                        }
                        else
                        {
                            metadata.StepErrors.Add(captureResponse.ErrorMessage ?? "Erro desconhecido");
                        }

                        response.ProcessedSteps.Add(metadata);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao processar {CaptureType}", captureType);
                        response.Errors.Add($"Erro em {captureType}: {ex.Message}");
                    }
                }

                // 3. Processar barcode se fornecido
                if (!string.IsNullOrEmpty(barcode))
                {
                    try
                    {
                        var barcodeResponse = await _guidedCaptureService.AddCaptureAsync(
                            response.SessionId,
                            CaptureType.Barcode,
                            null,
                            null,
                            barcode,
                            languageCode,
                            false,
                            true,
                            cancellationToken);

                        response.ProcessedSteps.Add(new ProcessedStepMetadata
                        {
                            CaptureType = CaptureType.Barcode,
                            StepName = "Código de Barras",
                            Success = barcodeResponse.Success,
                            Duration = TimeSpan.FromMilliseconds(barcodeResponse.ProcessingTimeMs)
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao processar barcode");
                    }
                }

                // 4. Finalizar análise
                var finalizeRequest = new FinalizeAnalysisRequest
                {
                    SessionId = response.SessionId,
                    ForceAnalysis = false
                };

                var finalizeResponse = await _guidedCaptureService.FinalizeAnalysisAsync(
                    finalizeRequest,
                    cancellationToken);

                response.Success = finalizeResponse.Success;

                if (finalizeResponse.Success && finalizeResponse.Product != null)
                {
                    // Mapear dados consolidados (simplificado)
                    response.ProductIdentification = new ProductIdentificationSummary
                    {
                        ProductName = finalizeResponse.Product.Name,
                        Brand = finalizeResponse.Product.Brand,
                        Confidence = (double)finalizeResponse.OverallConfidence
                    };

                    // Apenas marcar como sucesso - mapeamentos detalhados podem ser adicionados depois
                    if (finalizeResponse.Summary != null)
                    {
                        response.FinalAnalysis = new FinalAnalysisSummary
                        {
                            ProductAnalysisId = null,  // Não temos acesso ao ID int aqui
                            Classification = AnalysisClassification.Moderate,  // Valor padrão
                            OverallScore = (double)finalizeResponse.OverallConfidence * 5.0,  // Escala 0-1 para 0-5
                            Alerts = finalizeResponse.Alerts.Select(a => a.ToString()).ToList(),
                            Recommendations = finalizeResponse.Recommendations.Select(r => r.ToString()).ToList()
                        };
                    }
                }

                totalStopwatch.Stop();
                response.TotalDuration = totalStopwatch.Elapsed;

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no processamento completo");
                response.Success = false;
                response.Errors.Add($"Erro inesperado: {ex.Message}");
                response.TotalDuration = totalStopwatch.Elapsed;
                return response;
            }
        }

        private CaptureType MapKeyToCaptureType(string key)
        {
            return key.ToLowerInvariant() switch
            {
                "front" => CaptureType.FrontPackaging,
                "ingredients" => CaptureType.IngredientsList,
                "nutrition" => CaptureType.NutritionTable,
                "allergen" => CaptureType.AllergenStatement,
                _ => CaptureType.FrontPackaging
            };
        }

        private string GetStepName(CaptureType captureType, string languageCode)
        {
            var isPt = languageCode.StartsWith("pt", StringComparison.OrdinalIgnoreCase);
            return captureType switch
            {
                CaptureType.FrontPackaging => isPt ? "Embalagem Frontal" : "Front Packaging",
                CaptureType.IngredientsList => isPt ? "Lista de Ingredientes" : "Ingredients List",
                CaptureType.NutritionTable => isPt ? "Tabela Nutricional" : "Nutrition Table",
                CaptureType.AllergenStatement => isPt ? "Declaração de Alérgenos" : "Allergen Statement",
                CaptureType.Barcode => isPt ? "Código de Barras" : "Barcode",
                _ => captureType.ToString()
            };
        }
    }
}
