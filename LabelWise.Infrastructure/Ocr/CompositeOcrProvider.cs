using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LabelWise.Application.DTOs;
using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Ocr
{
    /// <summary>
    /// Provider composto que usa múltiplos providers de OCR com lógica de fallback inteligente.
    /// 
    /// ESTRATÉGIA:
    /// 1. Tenta o provider primário (geralmente Azure)
    /// 2. Se confiança < threshold (default 0.85), tenta o fallback (geralmente Tesseract)
    /// 3. Retorna o resultado com maior confiança
    /// 
    /// BENEFÍCIOS:
    /// - Maximiza precisão combinando múltiplos providers
    /// - Custo otimizado: usa Azure apenas quando necessário
    /// - Resiliente: se um provider falhar, usa outro
    /// - Transparente: informa qual provider foi usado via metadata
    /// 
    /// CONFIGURAÇÃO TÍPICA:
    /// - Primary: Azure Computer Vision (alta precisão, custo por uso)
    /// - Fallback: Tesseract (grátis, local, boa precisão)
    /// </summary>
    public class CompositeOcrProvider : IOcrProvider
    {
        private readonly IOcrProvider _primaryProvider;
        private readonly IOcrProvider _fallbackProvider;
        private readonly double _confidenceThreshold;
        private readonly ILogger<CompositeOcrProvider>? _logger;

        public string ProviderName => "Composite OCR (Primary + Fallback)";

        /// <summary>
        /// Inicializa o provider composto com estratégia de fallback.
        /// </summary>
        /// <param name="primaryProvider">Provider principal (ex: Azure)</param>
        /// <param name="fallbackProvider">Provider de fallback (ex: Tesseract)</param>
        /// <param name="confidenceThreshold">Threshold de confiança para usar fallback (0.0-1.0, default 0.85)</param>
        /// <param name="logger">Logger opcional</param>
        public CompositeOcrProvider(
            IOcrProvider primaryProvider,
            IOcrProvider fallbackProvider,
            double confidenceThreshold = 0.85,
            ILogger<CompositeOcrProvider>? logger = null)
        {
            _primaryProvider = primaryProvider ?? throw new ArgumentNullException(nameof(primaryProvider));
            _fallbackProvider = fallbackProvider ?? throw new ArgumentNullException(nameof(fallbackProvider));
            _confidenceThreshold = Math.Clamp(confidenceThreshold, 0.0, 1.0);
            _logger = logger;

            _logger?.LogInformation("═══════════════════════════════════════════════════════════");
            _logger?.LogInformation("🔀 CompositeOcrProvider Inicializado");
            _logger?.LogInformation("   Primary Provider: {Primary}", _primaryProvider.ProviderName);
            _logger?.LogInformation("   Fallback Provider: {Fallback}", _fallbackProvider.ProviderName);
            _logger?.LogInformation("   Confidence Threshold: {Threshold:F2}%", _confidenceThreshold * 100);
            _logger?.LogInformation("═══════════════════════════════════════════════════════════");
        }

        public async Task<OcrResultDto> ExtractTextAsync(OcrRequestDto request)
        {
            _logger?.LogInformation("🚀 Iniciando OCR Composto");
            _logger?.LogInformation("   Arquivo: {FileName}", request.FileName);

            // Variáveis para armazenar resultados
            OcrResultDto? primaryResult = null;
            OcrResultDto? fallbackResult = null;
            bool primaryFailed = false;
            bool fallbackExecuted = false;

            // ========================================================================
            // ETAPA 1: Tentar Provider Primário
            // ========================================================================
            try
            {
                _logger?.LogInformation("📍 ETAPA 1: Tentando provider primário ({Provider})...", 
                    _primaryProvider.ProviderName);

                primaryResult = await _primaryProvider.ExtractTextAsync(request);

                if (primaryResult.Success)
                {
                    _logger?.LogInformation("✅ Provider primário concluído");
                    _logger?.LogInformation("   Confiança: {Confidence:F2}%", primaryResult.Confidence * 100);
                    _logger?.LogInformation("   Caracteres: {Chars}", primaryResult.RawText.Length);
                }
                else
                {
                    _logger?.LogWarning("⚠️ Provider primário retornou erro: {Error}", 
                        primaryResult.ErrorMessage);
                    primaryFailed = true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Falha no provider primário ({Provider})", 
                    _primaryProvider.ProviderName);
                primaryFailed = true;
            }

            // ========================================================================
            // ETAPA 2: Decidir se usa Fallback
            // ========================================================================
            bool shouldUseFallback = primaryFailed || 
                                    (primaryResult != null && 
                                     primaryResult.Success && 
                                     primaryResult.Confidence < _confidenceThreshold);

            if (shouldUseFallback)
            {
                if (primaryFailed)
                {
                    _logger?.LogWarning("⚠️ Provider primário falhou. Usando fallback...");
                }
                else if (primaryResult != null)
                {
                    _logger?.LogInformation("📊 Confiança ({Confidence:F2}%) abaixo do threshold ({Threshold:F2}%). Usando fallback para comparação...",
                        primaryResult.Confidence * 100,
                        _confidenceThreshold * 100);
                }

                // ========================================================================
                // ETAPA 3: Executar Provider de Fallback
                // ========================================================================
                try
                {
                    _logger?.LogInformation("📍 ETAPA 2: Tentando provider de fallback ({Provider})...",
                        _fallbackProvider.ProviderName);

                    fallbackResult = await _fallbackProvider.ExtractTextAsync(request);
                    fallbackExecuted = true;

                    if (fallbackResult.Success)
                    {
                        _logger?.LogInformation("✅ Provider de fallback concluído");
                        _logger?.LogInformation("   Confiança: {Confidence:F2}%", fallbackResult.Confidence * 100);
                        _logger?.LogInformation("   Caracteres: {Chars}", fallbackResult.RawText.Length);
                    }
                    else
                    {
                        _logger?.LogWarning("⚠️ Provider de fallback retornou erro: {Error}",
                            fallbackResult.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "❌ Falha no provider de fallback ({Provider})",
                        _fallbackProvider.ProviderName);
                }
            }
            else
            {
                _logger?.LogInformation("✅ Confiança do provider primário suficiente. Fallback não necessário.");
            }

            // ========================================================================
            // ETAPA 4: Selecionar Melhor Resultado
            // ========================================================================
            var finalResult = SelectBestResult(primaryResult, fallbackResult, fallbackExecuted);

            // ========================================================================
            // ETAPA 5: Adicionar Metadata do Provider Composto
            // ========================================================================
            if (finalResult.ProviderMetadata == null)
            {
                finalResult.ProviderMetadata = new Dictionary<string, string>();
            }

            finalResult.ProviderMetadata["CompositeProvider"] = "true";
            finalResult.ProviderMetadata["PrimaryProvider"] = _primaryProvider.ProviderName;
            finalResult.ProviderMetadata["FallbackProvider"] = _fallbackProvider.ProviderName;
            finalResult.ProviderMetadata["ConfidenceThreshold"] = $"{_confidenceThreshold:F2}";
            finalResult.ProviderMetadata["FallbackExecuted"] = fallbackExecuted.ToString();
            
            if (primaryResult != null)
            {
                finalResult.ProviderMetadata["PrimaryConfidence"] = $"{primaryResult.Confidence:F2}";
            }
            
            if (fallbackResult != null)
            {
                finalResult.ProviderMetadata["FallbackConfidence"] = $"{fallbackResult.Confidence:F2}";
            }

            // Log final
            _logger?.LogInformation("═══════════════════════════════════════════════════════════");
            _logger?.LogInformation("🏁 OCR Composto Finalizado");
            _logger?.LogInformation("   Provider Usado: {Provider}", 
                finalResult.ProviderMetadata["UsedProvider"]);
            _logger?.LogInformation("   Confiança Final: {Confidence:F2}%", finalResult.Confidence * 100);
            _logger?.LogInformation("   Caracteres: {Chars}", finalResult.RawText.Length);
            _logger?.LogInformation("   Fallback Executado: {Executed}", fallbackExecuted);
            _logger?.LogInformation("═══════════════════════════════════════════════════════════");

            return finalResult;
        }

        /// <summary>
        /// Seleciona o melhor resultado entre os providers.
        /// Prioriza resultado com maior confiança e maior quantidade de texto extraído.
        /// </summary>
        private OcrResultDto SelectBestResult(
            OcrResultDto? primaryResult,
            OcrResultDto? fallbackResult,
            bool fallbackExecuted)
        {
            // Se nenhum funcionou, retorna erro
            if (primaryResult == null && fallbackResult == null)
            {
                return CreateErrorResult("Ambos os providers falharam");
            }

            // Se apenas um funcionou, retorna ele
            if (primaryResult == null || !primaryResult.Success)
            {
                if (fallbackResult != null)
                {
                    fallbackResult.ProviderMetadata ??= new Dictionary<string, string>();
                    fallbackResult.ProviderMetadata["UsedProvider"] = _fallbackProvider.ProviderName;
                    fallbackResult.ProviderMetadata["Reason"] = "Primary failed";
                    return fallbackResult;
                }
                return CreateErrorResult("Provider primário falhou e fallback não disponível");
            }

            if (fallbackResult == null || !fallbackResult.Success || !fallbackExecuted)
            {
                primaryResult.ProviderMetadata ??= new Dictionary<string, string>();
                primaryResult.ProviderMetadata["UsedProvider"] = _primaryProvider.ProviderName;
                primaryResult.ProviderMetadata["Reason"] = "Primary successful, fallback not needed";
                return primaryResult;
            }

            // Ambos funcionaram: seleciona o melhor
            // Critério: confiança e quantidade de texto
            var primaryScore = CalculateResultScore(primaryResult);
            var fallbackScore = CalculateResultScore(fallbackResult);

            _logger?.LogDebug("📊 Comparando resultados:");
            _logger?.LogDebug("   Primary Score: {Score:F2} (Confidence: {Conf:F2}%, Chars: {Chars})",
                primaryScore, primaryResult.Confidence * 100, primaryResult.RawText.Length);
            _logger?.LogDebug("   Fallback Score: {Score:F2} (Confidence: {Conf:F2}%, Chars: {Chars})",
                fallbackScore, fallbackResult.Confidence * 100, fallbackResult.RawText.Length);

            OcrResultDto selectedResult;
            string selectedProvider;
            string reason;

            if (primaryScore >= fallbackScore)
            {
                selectedResult = primaryResult;
                selectedProvider = _primaryProvider.ProviderName;
                reason = $"Primary score ({primaryScore:F2}) >= Fallback score ({fallbackScore:F2})";
            }
            else
            {
                selectedResult = fallbackResult;
                selectedProvider = _fallbackProvider.ProviderName;
                reason = $"Fallback score ({fallbackScore:F2}) > Primary score ({primaryScore:F2})";
            }

            selectedResult.ProviderMetadata ??= new Dictionary<string, string>();
            selectedResult.ProviderMetadata["UsedProvider"] = selectedProvider;
            selectedResult.ProviderMetadata["Reason"] = reason;

            _logger?.LogInformation("🎯 Resultado selecionado: {Provider} ({Reason})", 
                selectedProvider, reason);

            return selectedResult;
        }

        /// <summary>
        /// Calcula score do resultado baseado em confiança e quantidade de texto.
        /// Score = (Confiança * 0.7) + (Normalização de texto * 0.3)
        /// </summary>
        private double CalculateResultScore(OcrResultDto result)
        {
            if (!result.Success || string.IsNullOrWhiteSpace(result.RawText))
            {
                return 0;
            }

            // Peso da confiança: 70%
            var confidenceScore = result.Confidence * 0.7;

            // Peso do texto: 30% (normalizado, máximo 500 caracteres = 1.0)
            var textScore = Math.Min(result.RawText.Length / 500.0, 1.0) * 0.3;

            return confidenceScore + textScore;
        }

        public async Task<bool> IsAvailableAsync()
        {
            var primaryAvailable = await _primaryProvider.IsAvailableAsync();
            var fallbackAvailable = await _fallbackProvider.IsAvailableAsync();

            // Considera disponível se pelo menos um provider está disponível
            var isAvailable = primaryAvailable || fallbackAvailable;

            _logger?.LogDebug("IsAvailableAsync: {IsAvailable} (Primary: {Primary}, Fallback: {Fallback})",
                isAvailable, primaryAvailable, fallbackAvailable);

            return isAvailable;
        }

        public Dictionary<string, string> GetMetadata()
        {
            var metadata = new Dictionary<string, string>
            {
                ["ProviderName"] = ProviderName,
                ["ProviderType"] = GetType().FullName ?? "CompositeOcrProvider",
                ["PrimaryProvider"] = _primaryProvider.ProviderName,
                ["FallbackProvider"] = _fallbackProvider.ProviderName,
                ["ConfidenceThreshold"] = $"{_confidenceThreshold:F2}",
                ["IsMock"] = "false",
                ["Strategy"] = "Fallback on low confidence or failure"
            };

            // Adiciona metadata dos providers individuais
            var primaryMeta = _primaryProvider.GetMetadata();
            var fallbackMeta = _fallbackProvider.GetMetadata();

            foreach (var kvp in primaryMeta)
            {
                metadata[$"Primary_{kvp.Key}"] = kvp.Value;
            }

            foreach (var kvp in fallbackMeta)
            {
                metadata[$"Fallback_{kvp.Key}"] = kvp.Value;
            }

            return metadata;
        }

        private OcrResultDto CreateErrorResult(string errorMessage)
        {
            return new OcrResultDto
            {
                Success = false,
                ErrorMessage = errorMessage,
                RawText = string.Empty,
                Confidence = 0,
                TextBlocks = new List<OcrTextBlock>(),
                ProviderMetadata = GetMetadata()
            };
        }
    }
}
