using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using LabelWise.Application.DTOs;
using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Ocr
{
    /// <summary>
    /// Seletor inteligente de OCR que implementa estratégia de fallback baseada em confiança.
    /// 
    /// ESTRATÉGIA:
    /// 1. Executa Tesseract primeiro (grátis, local, rápido)
    /// 2. Se confiança < threshold configurado, usa Azure Vision (pago, cloud, alta precisão)
    /// 3. Retorna o melhor resultado (maior confiança)
    /// 
    /// BENEFÍCIOS:
    /// ✅ Custo otimizado: usa Azure apenas quando necessário
    /// ✅ Performance: Tesseract é mais rápido (local)
    /// ✅ Qualidade: Azure complementa quando Tesseract falha
    /// ✅ Resiliente: fallback automático se um provider falhar
    /// ✅ Transparente: metadata indica qual provider foi usado
    /// 
    /// CONFIGURAÇÃO TÍPICA:
    /// - Threshold: 0.85 (85%)
    /// - Se Tesseract retornar confiança >= 85%, usa resultado do Tesseract
    /// - Se Tesseract retornar confiança < 85%, tenta Azure Vision
    /// - Retorna o resultado com maior confiança
    /// 
    /// EXEMPLO DE USO:
    /// Imagem de boa qualidade (scanner):
    ///   → Tesseract: 92% de confiança → USA TESSERACT (grátis)
    /// 
    /// Imagem de celular (baixa qualidade, ângulo):
    ///   → Tesseract: 65% de confiança → USA AZURE (pago, alta qualidade)
    /// </summary>
    public class OcrProviderSelector : IOcrProvider
    {
        private readonly IOcrProvider _tesseractProvider;
        private readonly IOcrProvider _azureProvider;
        private readonly double _confidenceThreshold;
        private readonly ILogger<OcrProviderSelector>? _logger;

        public string ProviderName => "Smart OCR Selector (Tesseract → Azure)";

        /// <summary>
        /// Inicializa o seletor de OCR com estratégia de fallback.
        /// </summary>
        /// <param name="tesseractProvider">Provider Tesseract (primeiro a ser executado)</param>
        /// <param name="azureProvider">Provider Azure Vision (fallback se confiança baixa)</param>
        /// <param name="confidenceThreshold">Threshold de confiança (0.0-1.0, default: 0.85)</param>
        /// <param name="logger">Logger opcional</param>
        public OcrProviderSelector(
            IOcrProvider tesseractProvider,
            IOcrProvider azureProvider,
            double confidenceThreshold = 0.85,
            ILogger<OcrProviderSelector>? logger = null)
        {
            _tesseractProvider = tesseractProvider ?? throw new ArgumentNullException(nameof(tesseractProvider));
            _azureProvider = azureProvider ?? throw new ArgumentNullException(nameof(azureProvider));
            _confidenceThreshold = Math.Clamp(confidenceThreshold, 0.0, 1.0);
            _logger = logger;

            _logger?.LogInformation("═══════════════════════════════════════════════════════════");
            _logger?.LogInformation("🎯 {ProviderName} Inicializado", ProviderName);
            _logger?.LogInformation("   Primary: {Primary}", _tesseractProvider.ProviderName);
            _logger?.LogInformation("   Fallback: {Fallback}", _azureProvider.ProviderName);
            _logger?.LogInformation("   Threshold: {Threshold:P0}", _confidenceThreshold);
            _logger?.LogInformation("═══════════════════════════════════════════════════════════");
        }

        public async Task<OcrResultDto> ExtractTextAsync(OcrRequestDto request)
        {
            _logger?.LogInformation("🎯 Iniciando Smart OCR Selection");
            _logger?.LogInformation("   Arquivo: {FileName}", request.FileName);

            var stopwatch = Stopwatch.StartNew();

            // ═══════════════════════════════════════════════════════════════════════
            // ETAPA 1: Executar Tesseract (primário, grátis, local)
            // ═══════════════════════════════════════════════════════════════════════
            OcrResultDto? tesseractResult = null;
            bool tesseractFailed = false;

            try
            {
                _logger?.LogInformation("📍 ETAPA 1: Executando Tesseract OCR (grátis, local)...");
                var tesseractStopwatch = Stopwatch.StartNew();

                tesseractResult = await _tesseractProvider.ExtractTextAsync(request);

                tesseractStopwatch.Stop();

                if (tesseractResult.Success)
                {
                    _logger?.LogInformation("✅ Tesseract concluído em {Elapsed:F2}s", tesseractStopwatch.Elapsed.TotalSeconds);
                    _logger?.LogInformation("   Confiança: {Confidence:P2}", tesseractResult.Confidence);
                    _logger?.LogInformation("   Caracteres: {Chars:N0}", tesseractResult.RawText.Length);
                    _logger?.LogInformation("   Custo: $0.00 (local)");
                }
                else
                {
                    _logger?.LogWarning("⚠️ Tesseract retornou erro: {Error}", tesseractResult.ErrorMessage);
                    tesseractFailed = true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Falha ao executar Tesseract");
                tesseractFailed = true;
            }

            // ═══════════════════════════════════════════════════════════════════════
            // ETAPA 2: Decidir se precisa executar Azure Vision
            // ═══════════════════════════════════════════════════════════════════════
            bool shouldUseAzure = tesseractFailed ||
                                 (tesseractResult != null &&
                                  tesseractResult.Success &&
                                  tesseractResult.Confidence < _confidenceThreshold);

            OcrResultDto? azureResult = null;

            if (shouldUseAzure)
            {
                if (tesseractFailed)
                {
                    _logger?.LogWarning("⚠️ Tesseract falhou. Tentando Azure Vision como fallback...");
                }
                else if (tesseractResult != null)
                {
                    _logger?.LogInformation("📊 Confiança Tesseract ({Confidence:P2}) < Threshold ({Threshold:P2})",
                        tesseractResult.Confidence, _confidenceThreshold);
                    _logger?.LogInformation("   → Executando Azure Vision para melhor qualidade...");
                }

                // ═══════════════════════════════════════════════════════════════════
                // ETAPA 3: Executar Azure Vision (fallback, pago, alta precisão)
                // ═══════════════════════════════════════════════════════════════════
                try
                {
                    _logger?.LogInformation("📍 ETAPA 2: Executando Azure Vision OCR (pago, cloud)...");
                    var azureStopwatch = Stopwatch.StartNew();

                    azureResult = await _azureProvider.ExtractTextAsync(request);

                    azureStopwatch.Stop();

                    if (azureResult.Success)
                    {
                        _logger?.LogInformation("✅ Azure Vision concluído em {Elapsed:F2}s", azureStopwatch.Elapsed.TotalSeconds);
                        _logger?.LogInformation("   Confiança: {Confidence:P2}", azureResult.Confidence);
                        _logger?.LogInformation("   Caracteres: {Chars:N0}", azureResult.RawText.Length);
                        _logger?.LogInformation("   Custo: ~$0.001 (1 transação)");
                    }
                    else
                    {
                        _logger?.LogWarning("⚠️ Azure Vision retornou erro: {Error}", azureResult.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "❌ Falha ao executar Azure Vision");
                }
            }
            else
            {
                _logger?.LogInformation("✅ Tesseract confiança suficiente. Não é necessário usar Azure.");
            }

            // ═══════════════════════════════════════════════════════════════════════
            // ETAPA 4: Selecionar o melhor resultado
            // ═══════════════════════════════════════════════════════════════════════
            stopwatch.Stop();

            var finalResult = SelectBestResult(tesseractResult, azureResult);

            // Adicionar metadata do selector
            if (finalResult.ProviderMetadata == null)
            {
                finalResult.ProviderMetadata = new Dictionary<string, string>();
            }

            finalResult.ProviderMetadata["SelectorUsed"] = "true";
            finalResult.ProviderMetadata["SelectorName"] = ProviderName;
            finalResult.ProviderMetadata["ConfidenceThreshold"] = _confidenceThreshold.ToString("F2");
            finalResult.ProviderMetadata["TotalExecutionTime"] = $"{stopwatch.Elapsed.TotalSeconds:F2}s";
            finalResult.ProviderMetadata["TesseractExecuted"] = (tesseractResult != null).ToString();
            finalResult.ProviderMetadata["AzureExecuted"] = (azureResult != null).ToString();

            _logger?.LogInformation("═══════════════════════════════════════════════════════════");
            _logger?.LogInformation("📊 RESULTADO FINAL DO SMART SELECTOR");
            _logger?.LogInformation("   Provider usado: {Provider}", finalResult.ProviderMetadata.GetValueOrDefault("SelectedProvider", "Unknown"));
            _logger?.LogInformation("   Confiança: {Confidence:P2}", finalResult.Confidence);
            _logger?.LogInformation("   Caracteres: {Chars:N0}", finalResult.RawText.Length);
            _logger?.LogInformation("   Tempo total: {Time:F2}s", stopwatch.Elapsed.TotalSeconds);
            _logger?.LogInformation("═══════════════════════════════════════════════════════════");

            return finalResult;
        }

        private OcrResultDto SelectBestResult(OcrResultDto? tesseractResult, OcrResultDto? azureResult)
        {
            // Se ambos falharam, retorna erro
            if ((tesseractResult == null || !tesseractResult.Success) &&
                (azureResult == null || !azureResult.Success))
            {
                _logger?.LogError("❌ Ambos os providers falharam");

                return new OcrResultDto
                {
                    Success = false,
                    ErrorMessage = "Todos os providers de OCR falharam. " +
                                  $"Tesseract: {tesseractResult?.ErrorMessage ?? "não executado"}. " +
                                  $"Azure: {azureResult?.ErrorMessage ?? "não executado"}.",
                    RawText = string.Empty,
                    Confidence = 0,
                    TextBlocks = new List<OcrTextBlock>(),
                    ProviderMetadata = new Dictionary<string, string>
                    {
                        ["SelectedProvider"] = "NONE (all failed)",
                        ["TesseractError"] = tesseractResult?.ErrorMessage ?? "N/A",
                        ["AzureError"] = azureResult?.ErrorMessage ?? "N/A"
                    }
                };
            }

            // Se apenas Azure funcionou
            if (azureResult != null && azureResult.Success &&
                (tesseractResult == null || !tesseractResult.Success))
            {
                _logger?.LogInformation("✅ Selecionado: Azure Vision (único disponível)");
                if (azureResult.ProviderMetadata == null)
                    azureResult.ProviderMetadata = new Dictionary<string, string>();

                azureResult.ProviderMetadata["SelectedProvider"] = "Azure Vision";
                azureResult.ProviderMetadata["SelectionReason"] = "Tesseract falhou";
                return azureResult;
            }

            // Se apenas Tesseract funcionou
            if (tesseractResult != null && tesseractResult.Success &&
                (azureResult == null || !azureResult.Success))
            {
                _logger?.LogInformation("✅ Selecionado: Tesseract (único disponível ou Azure não executado)");
                if (tesseractResult.ProviderMetadata == null)
                    tesseractResult.ProviderMetadata = new Dictionary<string, string>();

                tesseractResult.ProviderMetadata["SelectedProvider"] = "Tesseract";
                tesseractResult.ProviderMetadata["SelectionReason"] = azureResult == null
                    ? "Confiança acima do threshold (Azure não necessário)"
                    : "Azure falhou";
                return tesseractResult;
            }

            // Se ambos funcionaram, escolhe o de maior confiança
            if (tesseractResult != null && tesseractResult.Success &&
                azureResult != null && azureResult.Success)
            {
                var selectedResult = tesseractResult.Confidence >= azureResult.Confidence
                    ? tesseractResult
                    : azureResult;

                var selectedName = selectedResult == tesseractResult ? "Tesseract" : "Azure Vision";

                _logger?.LogInformation("✅ Selecionado: {Provider} (maior confiança: {Confidence:P2})",
                    selectedName,
                    selectedResult.Confidence);

                if (selectedResult.ProviderMetadata == null)
                    selectedResult.ProviderMetadata = new Dictionary<string, string>();

                selectedResult.ProviderMetadata["SelectedProvider"] = selectedName;
                selectedResult.ProviderMetadata["SelectionReason"] = "Maior confiança";
                selectedResult.ProviderMetadata["TesseractConfidence"] = tesseractResult.Confidence.ToString("F4");
                selectedResult.ProviderMetadata["AzureConfidence"] = azureResult.Confidence.ToString("F4");

                return selectedResult;
            }

            // Fallback: retorna Tesseract (não deveria chegar aqui)
            _logger?.LogWarning("⚠️ Lógica de seleção inesperada, retornando Tesseract");
            return tesseractResult ?? azureResult ?? new OcrResultDto
            {
                Success = false,
                ErrorMessage = "Erro interno na seleção de provider",
                RawText = string.Empty,
                Confidence = 0,
                TextBlocks = new List<OcrTextBlock>(),
                ProviderMetadata = new Dictionary<string, string>
                {
                    ["SelectedProvider"] = "ERROR",
                    ["SelectionReason"] = "Unexpected logic path"
                }
            };
        }

        public async Task<bool> IsAvailableAsync()
        {
            // O selector está disponível se pelo menos um provider está disponível
            var tesseractAvailable = await _tesseractProvider.IsAvailableAsync();
            var azureAvailable = await _azureProvider.IsAvailableAsync();

            var isAvailable = tesseractAvailable || azureAvailable;

            _logger?.LogDebug("IsAvailableAsync: {IsAvailable} (Tesseract: {Tesseract}, Azure: {Azure})",
                isAvailable, tesseractAvailable, azureAvailable);

            return isAvailable;
        }

        public Dictionary<string, string> GetMetadata()
        {
            var metadata = new Dictionary<string, string>
            {
                ["ProviderName"] = ProviderName,
                ["ProviderType"] = GetType().FullName ?? "OcrProviderSelector",
                ["IsSelector"] = "true",
                ["ConfidenceThreshold"] = _confidenceThreshold.ToString("F2"),
                ["PrimaryProvider"] = _tesseractProvider.ProviderName,
                ["FallbackProvider"] = _azureProvider.ProviderName
            };

            // Adicionar metadata dos sub-providers
            var tesseractMetadata = _tesseractProvider.GetMetadata();
            var azureMetadata = _azureProvider.GetMetadata();

            foreach (var kvp in tesseractMetadata)
            {
                metadata[$"Tesseract_{kvp.Key}"] = kvp.Value;
            }

            foreach (var kvp in azureMetadata)
            {
                metadata[$"Azure_{kvp.Key}"] = kvp.Value;
            }

            return metadata;
        }
    }
}
