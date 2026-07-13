using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Application.Configuration;
using LabelWise.Application.DTOs.AI;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Domain.Enums;
using LabelWise.Infrastructure.AI.DTOs;
using LabelWise.Infrastructure.AI.Prompts;
using LabelWise.Infrastructure.Helpers;
using LabelWise.Application.Presentation;
using LabelWise.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace LabelWise.Infrastructure.AI;

/// <summary>
/// Visual interpreter especializado para análise nutricional usando Azure OpenAI Vision.
/// Utiliza prompts específicos para extrair informações nutricionais detalhadas de embalagens.
/// </summary>
public class NutritionVisionInterpreter : IVisualInterpreter
{
    private readonly AzureOpenAiVisionOptions _options;
    private readonly ILogger<NutritionVisionInterpreter> _logger;
    private readonly INutritionSanitizer _nutritionSanitizer;
    private readonly INutritionConsistencyValidator _nutritionConsistencyValidator;
    private readonly INutritionAutoCorrector _nutritionAutoCorrector;
    private readonly INutritionOcrCrossValidator _nutritionOcrCrossValidator;
    private readonly IHybridOcrValidator _hybridOcrValidator;

    public NutritionVisionInterpreter(
        IOptions<AzureOpenAiVisionOptions> options, 
        ILogger<NutritionVisionInterpreter> logger,
        INutritionSanitizer nutritionSanitizer,
        INutritionConsistencyValidator nutritionConsistencyValidator,
        INutritionAutoCorrector nutritionAutoCorrector,
        INutritionOcrCrossValidator nutritionOcrCrossValidator,
        IHybridOcrValidator hybridOcrValidator)
    {
        _options = options.Value;
        _logger = logger;
        _nutritionSanitizer = nutritionSanitizer;
        _nutritionConsistencyValidator = nutritionConsistencyValidator;
        _nutritionAutoCorrector = nutritionAutoCorrector;
        _nutritionOcrCrossValidator = nutritionOcrCrossValidator;
        _hybridOcrValidator = hybridOcrValidator;
    }

    public async Task<VisualInterpretationResult> InterpretImageAsync(VisualInterpretationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ImagePath) || !File.Exists(request.ImagePath))
        {
            _logger.LogWarning("Image path is null, empty, or file does not exist: {ImagePath}", request.ImagePath);
            return CreateErrorResult("Image file not found or invalid path.");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting nutrition-focused visual interpretation for image: {ImagePath}", request.ImagePath);

            // Validate configuration
            if (string.IsNullOrWhiteSpace(_options.Endpoint) || 
                string.IsNullOrWhiteSpace(_options.ApiKey) || 
                string.IsNullOrWhiteSpace(_options.VisionDeployment))
            {
                _logger.LogError("Azure OpenAI Vision configuration is incomplete");
                return CreateErrorResult("Azure OpenAI Vision service is not properly configured.");
            }

            // Read and process image
            byte[] imageBytes = await File.ReadAllBytesAsync(request.ImagePath);
            _logger.LogInformation("Read {ByteCount} bytes from image file", imageBytes.Length);

            string mimeType = GetMimeType(request.ImagePath);
            _logger.LogInformation("[DIAGNOSTIC] Original image: size={Size}KB, type={MimeType}", 
                imageBytes.Length / 1024, mimeType);

            var optimizedImage = await OptimizeImageForInferenceAsync(imageBytes, request.ImagePath, mimeType);

            _logger.LogInformation("[DIAGNOSTIC] After optimization: size={Size}KB, dimensions={Width}x{Height}, wasOptimized={WasOptimized}",
                optimizedImage.Bytes.Length / 1024, optimizedImage.Width, optimizedImage.Height, optimizedImage.WasOptimized);

            string dataUrl = BuildDataUrl(optimizedImage.Bytes, optimizedImage.MimeType);
            dataUrl = ImageFormatHelper.NormalizeBase64Image(dataUrl);

            // Create Azure OpenAI client
            string normalizedEndpoint = _options.Endpoint.TrimEnd('/');
            AzureOpenAIClient azureClient = new(
                new Uri(normalizedEndpoint),
                new AzureKeyCredential(_options.ApiKey)
            );

            ChatClient chatClient = azureClient.GetChatClient(_options.VisionDeployment);

            _logger.LogInformation("✅ ChatClient created successfully for nutrition analysis");

            // Build messages with nutrition-specific prompt
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(NutritionVisionPrompts.ProductNutritionAnalysisSystemPrompt),
                new UserChatMessage(
                    ChatMessageContentPart.CreateTextPart(NutritionVisionPrompts.AnalyzeNutritionUserMessage),
                    ChatMessageContentPart.CreateImagePart(new Uri(dataUrl))
                )
            };

            var chatOptions = new ChatCompletionOptions
            {
                MaxOutputTokenCount = 2500,
                Temperature = 0.1f
            };

            // Call the model
            _logger.LogInformation("Calling Azure OpenAI Vision model for nutrition analysis...");
            var completionTask = chatClient.CompleteChatAsync(messages, chatOptions);
            var referenceCatalogTask = Task.Run(NutritionReferenceRanges.GetSnapshot);

            await Task.WhenAll(completionTask, referenceCatalogTask);

            var completion = await completionTask;
            var referenceCatalog = await referenceCatalogTask;

            stopwatch.Stop();    
            _logger.LogInformation("Azure OpenAI Vision call completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            // Extract and parse response
            string responseText = completion.Value.Content.FirstOrDefault()?.Text ?? string.Empty;
            _logger.LogInformation("Received nutrition analysis response: {ResponseLength} characters", responseText.Length);
            _logger.LogInformation("[DIAGNOSTIC] Model usage - PromptTokens={PromptTokens}, CompletionTokens={CompletionTokens}, TotalTokens={TotalTokens}",
                completion.Value.Usage?.InputTokenCount ?? 0,
                completion.Value.Usage?.OutputTokenCount ?? 0,
                completion.Value.Usage?.TotalTokenCount ?? 0);

            var result = await ParseNutritionAnalysisResponseAsync(responseText, referenceCatalog, request.ImagePath);
            if (result == null)
            {
                _logger.LogWarning("Failed to parse nutrition analysis response");
                await SaveFailedImageForDiagnosticAsync(request.ImagePath, responseText, "parse_failed");
                return CreateErrorResult("Could not interpret the nutrition analysis from the image.");
            }

            if (!result.Success || result.EstimatedNutritionProfile == null || 
                (!result.EstimatedNutritionProfile.CaloriesPer100g.HasValue && 
                 !result.EstimatedNutritionProfile.CaloriesPer100ml.HasValue &&
                 result.AnalysisMode == AnalysisMode.FullNutritionLabel))
            {
                _logger.LogWarning("[DIAGNOSTIC] Incomplete nutrition extraction - Mode={Mode}, HasCalories={HasCalories}",
                    result.AnalysisMode, 
                    result.EstimatedNutritionProfile?.CaloriesPer100g.HasValue ?? false);
                await SaveFailedImageForDiagnosticAsync(request.ImagePath, responseText, "incomplete_extraction");
            }

            _logger.LogInformation(
                "Nutrition analysis completed successfully: Product={ProductName}, Mode={AnalysisMode}",
                result.ProductName ?? "unknown",
                result.AnalysisMode
            );

            return MapToVisualInterpretationResult(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during nutrition-focused visual interpretation: {ImagePath}", request.ImagePath);
            return CreateErrorResult($"Error during nutrition analysis: {ex.Message}");
        }
    }

    private async Task<NutritionAnalysisResponseDto?> ParseNutritionAnalysisResponseAsync(
    string responseText,
    NutritionReferenceCatalogSnapshot? referenceCatalog,
    string imagePath)
    {
        try
        {
            string jsonText = ExtractJson(responseText);

            if (string.IsNullOrWhiteSpace(jsonText) || !LooksLikeJsonObject(jsonText))
            {
                _logger.LogWarning("[RAW] Resposta da IA não contém JSON válido");
                _logger.LogWarning("[DIAGNOSTIC] Raw response preview: {Preview}", 
                    responseText.Length > 500 ? responseText[..500] + "..." : responseText);
                return null;
            }

            _logger.LogInformation("[DIAGNOSTIC] JSON extracted successfully, length={Length}", jsonText.Length);

            var modelResponse = JsonSerializer.Deserialize<NutritionVisionModelResponse>(jsonText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            });

            if (modelResponse == null)
            {
                _logger.LogWarning("[RAW] Deserialização retornou null");
                return null;
            }

            var result = new NutritionAnalysisResponseDto
            {
                Success = modelResponse.Success,
                ProductName = modelResponse.ProductName,
                Brand = modelResponse.Brand,
                Category = modelResponse.Category,
                PackageWeight = modelResponse.PackageWeight,
                AnalysisMode = MapAnalysisMode(modelResponse.AnalysisMode),
                VisibleClaims = modelResponse.VisibleClaims ?? new List<string>(),

                // 🔥 AQUI É ONDE ENTRA SUA CORREÇÃO COM OCR
                EstimatedNutritionProfile = MapNutritionProfile(
                    modelResponse.EstimatedNutritionProfile,
                    modelResponse.RawExtractedText),

                Classification = MapClassification(modelResponse.Classification),
                ConfidenceDetails = MapConfidenceDetails(modelResponse.ConfidenceDetails),
                Summary = modelResponse.Summary,
                Warnings = modelResponse.Warnings ?? new List<string>(),
                RawExtractedText = modelResponse.RawExtractedText,
                ErrorMessage = modelResponse.ErrorMessage,
                ProcessingTimeSeconds = 0
            };

            if (result.EstimatedNutritionProfile != null && modelResponse.RawExtractedText is { Count: > 0 })
            {
                NutritionTableFixer.Fix(
                    result.EstimatedNutritionProfile,
                    modelResponse.RawExtractedText,
                    msg => _logger.LogInformation("[DIAGNOSTIC] {Message}", msg));

                result.EstimatedNutritionProfile = _nutritionAutoCorrector.AutoCorrect(
                    result.EstimatedNutritionProfile,
                    modelResponse.RawExtractedText);

                result.EstimatedNutritionProfile = _nutritionOcrCrossValidator.ValidateAndCorrect(
                    result.EstimatedNutritionProfile,
                    modelResponse.RawExtractedText);

                // 🔥 VALIDAÇÃO HÍBRIDA: Azure Computer Vision valida valores críticos
                try
                {
                    var corrected = await _hybridOcrValidator.ValidateAndCorrectAsync(
                        result.EstimatedNutritionProfile,
                        imagePath,
                        result.Warnings);

                    if (corrected)
                    {
                        _logger.LogInformation("[HYBRID_OCR] ✅ Values corrected using Azure Computer Vision validation");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[HYBRID_OCR] Validation failed, keeping current values");
                }
            }

            if (result.EstimatedNutritionProfile != null)
            {
                var validation = _nutritionConsistencyValidator.Validate(result.EstimatedNutritionProfile, result.Category);

                foreach (var warning in validation.Warnings)
                {
                    if (!result.Warnings.Contains(warning, StringComparer.OrdinalIgnoreCase))
                        result.Warnings.Add(warning);
                }

                foreach (var error in validation.Errors)
                {
                    var message = $"Erro de consistência: {error}";
                    if (!result.Warnings.Contains(message, StringComparer.OrdinalIgnoreCase))
                        result.Warnings.Add(message);
                }

                if (validation.HasCriticalError())
                {
                    result.IsInconsistent = true;
                    _logger.LogWarning("Inconsistência nutricional detectada");
                }
            }

            _logger.LogInformation(
                "[RAW] IA → Product={Product}, Calories={Calories}",
                result.ProductName ?? "null",
                result.EstimatedNutritionProfile?.CaloriesPer100g?.ToString() ?? "null");

            if (result.EstimatedNutritionProfile != null)
            {
                _logger.LogInformation("[DIAGNOSTIC] Nutrition extracted - Calories={Cal}, Protein={Prot}, Fat={Fat}, Sugar={Sug}, Sodium={Sod}, Unit={Unit}",
                    result.EstimatedNutritionProfile.CaloriesPer100g ?? result.EstimatedNutritionProfile.CaloriesPer100ml,
                    result.EstimatedNutritionProfile.EstimatedProteinPer100g,
                    result.EstimatedNutritionProfile.EstimatedFatPer100g,
                    result.EstimatedNutritionProfile.EstimatedSugarPer100g,
                    result.EstimatedNutritionProfile.EstimatedSodiumPer100g,
                    result.EstimatedNutritionProfile.NutritionUnit ?? "not specified");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RAW] Erro ao processar resposta da IA");
            return null;
        }
    }

    #region Private Helpers

    /// <summary>
    /// Gets the MIME type based on file extension.
    /// </summary>
    private string GetMimeType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };
    }

    /// <summary>
    /// Builds a data URL from image bytes.
    /// </summary>
    private string BuildDataUrl(byte[] imageBytes, string mimeType)
    {
        string base64 = Convert.ToBase64String(imageBytes);
        return $"data:{mimeType};base64,{base64}";
    }

    private async Task<OptimizedImagePayload> OptimizeImageForInferenceAsync(byte[] imageBytes, string imagePath, string fallbackMimeType)
    {
        try
        {
            var optimizedImage = await ImageOptimizationHelper.OptimizeForVisionAsync(imageBytes, imagePath);

            if (optimizedImage.WasOptimized)
            {
                _logger.LogInformation(
                    "Image optimized for Azure OpenAI Vision: {OriginalBytes} -> {OptimizedBytes} bytes, dimensions {Width}x{Height}",
                    imageBytes.Length,
                    optimizedImage.Bytes.Length,
                    optimizedImage.Width,
                    optimizedImage.Height);
            }

            return optimizedImage;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Image optimization failed. Using original payload for Azure OpenAI Vision.");
            return new OptimizedImagePayload(imageBytes, fallbackMimeType, 0, 0, false);
        }
    }

    /// <summary>
    /// Mapeia o JSON bruto da IA diretamente para o DTO — sem nenhum pós-processamento.
    /// Todo o tratamento anterior foi comentado para isolar gaps no retorno da IA.
    /// </summary>
   
    /*
    ═══════════════════════════════════════════════════════════════════════════
    BLOCO DE PÓS-PROCESSAMENTO — DESATIVADO PARA DEBUG
    Reativar quando o retorno bruto da IA estiver validado.
    ═══════════════════════════════════════════════════════════════════════════

    Etapas comentadas:
    1. NormalizeVisibleClaims()           — filtrava claims inválidas
    2. NormalizeProductName()             — removia marca do nome, inferia por categoria
    3. AdjustAnalysisModeBasedOnRealData()— promovia FrontOfPackage → FullNutritionLabel
    4. AdjustMuscleGainClassification()   — corrigia classification para produtos pobres em proteína
    5. EnsureClassificationIsComplete()   — gerava classification local quando IA não retornava
    6. AdjustConfidenceDetails()          — recalculava confidence de claims
    7. NutritionExtractionPostProcessor   — corrigia column mixing, math coherence, row displacement
    8. PackageWeightValidator             — bloqueava packageWeight vindo de nutrientes
    9. NutritionSummaryRefiner            — refinava ou gerava o summary
    10. _nutritionSanitizer.Sanitize()    — sanitizava valores fora de faixa por categoria

    ═══════════════════════════════════════════════════════════════════════════
    */

    /// <summary>
    /// Extracts JSON from response text that may contain markdown code blocks.
    /// </summary>
    private string ExtractJson(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return string.Empty;
        }

        // Remove markdown code blocks
        var match = Regex.Match(responseText, @"```(?:json)?\s*(\{.*?\})\s*```", RegexOptions.Singleline);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        // Try to find JSON object
        match = Regex.Match(responseText, @"\{.*\}", RegexOptions.Singleline);
        if (match.Success)
        {
            return match.Value;
        }

        return responseText;
    }

    private static bool LooksLikeJsonObject(string value)
    {
        var v = value.Trim();
        return v.Length >= 2 && v[0] == '{' && v[^1] == '}';
    }

    /// <summary>
    /// Maps model analysis mode to enum.
    /// </summary>
    private AnalysisMode MapAnalysisMode(string? analysisMode)
    {
        var normalized = (analysisMode ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty);

        return normalized switch
        {
            "fullnutritionlabel" => AnalysisMode.FullNutritionLabel,
            "frontofpackageonly" => AnalysisMode.FrontOfPackageOnly,
            _ => AnalysisMode.FrontOfPackageOnly
        };
    }

    /// <summary>
    /// Maps model nutrition profile to DTO — includes all ANVISA fields.
    /// Every field is tagged with its extraction source (GPT or OCR) and a
    /// per-field confidence score stored in <c>result.FieldValues</c>.
    /// </summary>
    private EstimatedNutritionProfileDto? MapNutritionProfile(
        NutritionProfileResponse? profile,
        List<string>? rawText)
    {
        if (profile == null)
        {
            return new EstimatedNutritionProfileDto
            {
                Basis = "Não foi possível estimar perfil nutricional"
            };
        }

        // ── Step 1: seed from GPT JSON (confidence = 0.75) ────────────
        var result = new EstimatedNutritionProfileDto
        {
            CaloriesPer100g              = profile.CaloriesPer100g,
            CaloriesPer100ml             = profile.CaloriesPer100ml,
            EstimatedPackageCalories     = profile.EstimatedPackageCalories,
            EstimatedCarbsPer100g        = profile.EstimatedCarbsPer100g,
            EstimatedSugarPer100g        = profile.EstimatedSugarPer100g,
            EstimatedAddedSugarPer100g   = profile.EstimatedAddedSugarPer100g,
            EstimatedSaturatedFatPer100g = profile.EstimatedSaturatedFatPer100g,
            EstimatedProteinPer100g      = profile.EstimatedProteinPer100g,
            EstimatedSodiumPer100g       = profile.EstimatedSodiumPer100g,
            EstimatedFiberPer100g        = profile.EstimatedFiberPer100g,
            EstimatedFatPer100g          = profile.EstimatedFatPer100g,
            NutritionUnit                = profile.NutritionUnit,
            Basis                        = profile.Basis ?? "Estimativa baseada em análise visual"
        };

        // Tag all initial GPT values
        TagFieldValue(result.FieldValues, NutritionFieldMergeEngine.Calories,
            profile.CaloriesPer100g ?? profile.CaloriesPer100ml, "GPT", 0.75);
        TagFieldValue(result.FieldValues, NutritionFieldMergeEngine.Carbs,        profile.EstimatedCarbsPer100g,        "GPT", 0.75);
        TagFieldValue(result.FieldValues, NutritionFieldMergeEngine.Sugar,        profile.EstimatedSugarPer100g,        "GPT", 0.75);
        TagFieldValue(result.FieldValues, NutritionFieldMergeEngine.AddedSugar,   profile.EstimatedAddedSugarPer100g,   "GPT", 0.75);
        TagFieldValue(result.FieldValues, NutritionFieldMergeEngine.Protein,      profile.EstimatedProteinPer100g,      "GPT", 0.75);
        TagFieldValue(result.FieldValues, NutritionFieldMergeEngine.Fat,          profile.EstimatedFatPer100g,          "GPT", 0.75);
        TagFieldValue(result.FieldValues, NutritionFieldMergeEngine.SaturatedFat, profile.EstimatedSaturatedFatPer100g, "GPT", 0.75);
        TagFieldValue(result.FieldValues, NutritionFieldMergeEngine.Fiber,        profile.EstimatedFiberPer100g,        "GPT", 0.75);
        TagFieldValue(result.FieldValues, NutritionFieldMergeEngine.Sodium,       profile.EstimatedSodiumPer100g,       "GPT", 0.75);

        // ── Step 2: text-parser override (confidence = 0.85 base) ─────
        if (rawText != null && rawText.Any())
        {
            var columns = NutritionColumnParser.DetectColumns(rawText);
            _logger.LogInformation(
                "Detected columns: ml={ml}, g={g}, portion={portion}",
                columns.Per100mlIndex?.ToString() ?? "null",
                columns.Per100gIndex?.ToString() ?? "null",
                columns.PortionIndex?.ToString() ?? "null");

            var parser = new NutritionTableParser();
            var parsed = parser.Parse(rawText);

            if (parsed.HasAnyValue)
            {
                // Log per-nutrient debug evidence
                foreach (var (nutrient, evidence) in parsed.RowMatches)
                {
                    _logger.LogDebug(
                        "[MapNutritionProfile] 🔍 {Nutrient}: value={Value}, candidates=[{Candidates}], col={Col}, row=\"{Row}\"",
                        nutrient,
                        evidence.ExtractedValue,
                        string.Join(", ", evidence.Candidates),
                        evidence.ColumnIndex?.ToString() ?? "?",
                        evidence.RowText.Length > 80 ? evidence.RowText[..80] + "…" : evidence.RowText);
                }

                if (parsed.Warnings.Count > 0)
                {
                    _logger.LogWarning("[MapNutritionProfile] ⚠️ Parser warnings: {Warnings}",
                        string.Join(" | ", parsed.Warnings));
                }

                // Per-field OCR confidence: 0.85 base, reduced by 0.10 when a parser
                // warning exists that mentions the nutrient name.
                double OcrConf(string nutrientKey)
                {
                    bool hasWarning = parsed.Warnings.Any(w =>
                        w.Contains(nutrientKey, StringComparison.OrdinalIgnoreCase));
                    return hasWarning ? 0.75 : 0.85;
                }

                // Confidence upgrade: structured parse + ≥5 fields + no warnings → high
                int extractedCount = new[]
                {
                    parsed.Calories, parsed.Carbs, parsed.Protein, parsed.Fat,
                    parsed.SaturatedFat, parsed.Sugar, parsed.Fiber, parsed.Sodium
                }.Count(v => v.HasValue);

                if (extractedCount >= 5 && parsed.Warnings.Count == 0)
                {
                    _logger.LogInformation(
                        "[MapNutritionProfile] ✅ Confiança promovida para ALTA " +
                        "({Count} campos extraídos sem warnings)", extractedCount);
                    result.Basis ??= "100 g";
                }

                // Override fields from parser — OCR wins over GPT JSON
                OverrideIfPresent(result, result.FieldValues,
                    NutritionFieldMergeEngine.Carbs,        parsed.Carbs,
                    v => result.EstimatedCarbsPer100g        = v, "OCR", OcrConf("carbs"));

                OverrideIfPresent(result, result.FieldValues,
                    NutritionFieldMergeEngine.AddedSugar,   parsed.AddedSugar,
                    v => result.EstimatedAddedSugarPer100g   = v, "OCR", OcrConf("added_sugar"));

                OverrideIfPresent(result, result.FieldValues,
                    NutritionFieldMergeEngine.Protein,      parsed.Protein,
                    v => result.EstimatedProteinPer100g      = v, "OCR", OcrConf("protein"));

                OverrideIfPresent(result, result.FieldValues,
                    NutritionFieldMergeEngine.Fat,          parsed.Fat,
                    v => result.EstimatedFatPer100g          = v, "OCR", OcrConf("fat"));

                OverrideIfPresent(result, result.FieldValues,
                    NutritionFieldMergeEngine.SaturatedFat, parsed.SaturatedFat,
                    v => result.EstimatedSaturatedFatPer100g = v, "OCR", OcrConf("saturated_fat"));

                OverrideIfPresent(result, result.FieldValues,
                    NutritionFieldMergeEngine.Fiber,        parsed.Fiber,
                    v => result.EstimatedFiberPer100g        = v, "OCR", OcrConf("fiber"));

                // Sodium: parser prone to column errors — only override when plausible (≥ 5 mg)
                if (parsed.Sodium.HasValue && parsed.Sodium.Value >= 5)
                {
                    OverrideIfPresent(result, result.FieldValues,
                        NutritionFieldMergeEngine.Sodium, parsed.Sodium,
                        v => result.EstimatedSodiumPer100g = v, "OCR", OcrConf("sodium"));
                }
                else if (!profile.EstimatedSodiumPer100g.HasValue || profile.EstimatedSodiumPer100g.Value < 5)
                {
                    // Both sources suspect — take parser value (will be nulled downstream)
                    OverrideIfPresent(result, result.FieldValues,
                        NutritionFieldMergeEngine.Sodium, parsed.Sodium,
                        v => result.EstimatedSodiumPer100g = v, "OCR", 0.30);
                }

                // Sugar: prefer the higher value (more likely to be 100g column)
                if (parsed.Sugar.HasValue)
                {
                    double sugarConf = OcrConf("sugar");
                    if (!profile.EstimatedSugarPer100g.HasValue)
                    {
                        OverrideIfPresent(result, result.FieldValues,
                            NutritionFieldMergeEngine.Sugar, parsed.Sugar,
                            v => result.EstimatedSugarPer100g = v, "OCR", sugarConf);
                    }
                    else if (parsed.Sugar.Value >= profile.EstimatedSugarPer100g.Value)
                    {
                        OverrideIfPresent(result, result.FieldValues,
                            NutritionFieldMergeEngine.Sugar, parsed.Sugar,
                            v => result.EstimatedSugarPer100g = v, "OCR", sugarConf);
                    }
                    // else: GPT value is larger — keep existing GPT tag
                }

                // Calories + unit
                result.NutritionUnit = parsed.Unit;
                if (parsed.Unit == "ml")
                {
                    result.CaloriesPer100ml = parsed.Calories;
                    result.CaloriesPer100g  = null;
                    result.Basis = "100 ml (produto preparado)";
                }
                else
                {
                    result.CaloriesPer100g  = parsed.Calories;
                    result.CaloriesPer100ml = null;
                    result.Basis = "100 g";
                }

                TagFieldValue(result.FieldValues, NutritionFieldMergeEngine.Calories,
                    parsed.Calories, "OCR", OcrConf("calories"));

                _logger.LogInformation("Parsed nutrient: Protein={value}", parsed.Protein?.ToString() ?? "null");
            }
        }

        // ── Step 3: unit / basis normalisation ────────────────────────
        if (result.NutritionUnit == "ml")
        {
            if (!result.CaloriesPer100ml.HasValue && result.CaloriesPer100g.HasValue)
            {
                result.CaloriesPer100ml = result.CaloriesPer100g;
                result.CaloriesPer100g  = null;
            }
            result.Basis = string.IsNullOrWhiteSpace(result.Basis)
                ? "100 ml (produto preparado)" : result.Basis;
        }
        else if (result.NutritionUnit == "g")
        {
            result.CaloriesPer100ml = null;
            result.Basis = string.IsNullOrWhiteSpace(result.Basis) ? "100 g" : result.Basis;
        }

        if (string.IsNullOrWhiteSpace(result.NutritionUnit))
        {
            var basis = result.Basis?.ToLowerInvariant() ?? string.Empty;
            if (basis.Contains("100 ml"))      result.NutritionUnit = "ml";
            else if (basis.Contains("100 g"))  result.NutritionUnit = "g";
        }

        // ── Step 4: cross-field validation on FieldValues ─────────────
        result.FieldValues = NutritionFieldMergeEngine.ValidateAndAdjust(result.FieldValues);

        // ── Step 5: aggregate confidence → ParserConfidence ───────────
        bool hasValidationIssues = result.FieldValues.Values.Any(f => f.Confidence < 0.75);
        double agg = NutritionFieldMergeEngine.ComputeAggregateConfidence(
            result.FieldValues, hasValidationIssues);
        result.ParserConfidence = agg >= 0.88 ? "high" : agg >= 0.65 ? "medium" : "low";

        _logger.LogInformation(
            "[MapNutritionProfile] Unit={Unit}, AggConfidence={Agg:F2}, ParserConfidence={PC}",
            result.NutritionUnit ?? "null", agg, result.ParserConfidence);

        return result;
    }

    // ── FieldValue helpers ────────────────────────────────────────────

    private static void TagFieldValue(
        Dictionary<string, FieldValue> dict,
        string key, double? value, string source, double confidence)
    {
        if (!value.HasValue) return;
        dict[key] = source switch
        {
            "OCR"      => FieldValue.FromOcr(value.Value, confidence),
            "GPT"      => FieldValue.FromGpt(value.Value, confidence),
            "Fallback" => FieldValue.FromFallback(value.Value, confidence),
            _          => new FieldValue { Value = value.Value, Source = source, Confidence = confidence }
        };
    }

    /// <summary>
    /// Overrides a flat profile field and updates its FieldValue entry.
    /// OCR always wins: even if a GPT value is already present, the parser
    /// result takes priority (it is reading the image directly).
    /// </summary>
    private static void OverrideIfPresent(
        EstimatedNutritionProfileDto _,
        Dictionary<string, FieldValue> dict,
        string key, double? value,
        Action<double> apply, string source, double confidence)
    {
        if (!value.HasValue) return;
        apply(value.Value);
        TagFieldValue(dict, key, value, source, confidence);
    }
    private void ForceLiquidBasisIfNeeded(EstimatedNutritionProfileDto profile)
    {
        if (profile == null) return;

        var basis = profile.Basis?.ToLowerInvariant() ?? "";

        // Detecta se veio 100 ml da IA
        if (basis.Contains("100 ml") || basis.Contains("100ml"))
        {
            profile.Basis = "Valores por 100 ml (produto preparado)";
        }
    }

    /// <summary>
    /// Maps model classification to DTO.
    /// </summary>
    private ProductClassificationDto? MapClassification(ClassificationResponse? classification)
    {
        if (classification == null)
        {
            return null;
        }

        return new ProductClassificationDto
        {
            Diabetic = MapProfileClassification(classification.Diabetic),
            BloodPressure = MapProfileClassification(classification.BloodPressure),
            WeightLoss = MapProfileClassification(classification.WeightLoss),
            MuscleGain = MapProfileClassification(classification.MuscleGain)
        };
    }

    /// <summary>
    /// Maps individual profile classification.
    /// </summary>
    private HealthProfileResult? MapProfileClassification(ProfileClassificationResponse? profileClass)
    {
        if (profileClass == null)
        {
            return null;
        }

        return new HealthProfileResult
        {
            Status = profileClass.Status ?? "indeterminado",
            Reason = profileClass.Reason ?? "Classificação não disponível"
        };
    }

    /// <summary>
    /// Maps confidence details.
    /// </summary>
    private ConfidenceDetailsDto? MapConfidenceDetails(ConfidenceDetailsResponse? confidence)
    {
        if (confidence == null)
        {
            return new ConfidenceDetailsDto
            {
                ProductIdentification = 0.0,
                VisibleClaimsExtraction = 0.0,
                EstimatedNutritionProfile = 0.0,
                Classification = 0.0
            };
        }

        return new ConfidenceDetailsDto
        {
            ProductIdentification = confidence.ProductIdentification,
            VisibleClaimsExtraction = confidence.VisibleClaimsExtraction,
            EstimatedNutritionProfile = confidence.EstimatedNutritionProfile,
            Classification = confidence.Classification
        };
    }

    /// <summary>
    /// Maps nutrition analysis result to the existing VisualInterpretationResult for compatibility.
    /// </summary>
    private VisualInterpretationResult MapToVisualInterpretationResult(NutritionAnalysisResponseDto nutritionResult)
    {
        return new VisualInterpretationResult
        {
            ProbableProductName = nutritionResult.ProductName,
            ProductName = nutritionResult.ProductName,
            ProbableBrand = nutritionResult.Brand,
            Brand = nutritionResult.Brand,
            ProbableCategory = nutritionResult.Category,
            Category = nutritionResult.Category,
            ProbablePackageWeight = nutritionResult.PackageWeight,
            PackageWeight = nutritionResult.PackageWeight,
            VisibleClaims = nutritionResult.VisibleClaims,
            ProbableCaptureType = nutritionResult.AnalysisMode == AnalysisMode.FullNutritionLabel 
                ? CaptureType.NutritionTable 
                : CaptureType.FrontPackaging,
            InterpretationConfidence = MapOverallConfidence(nutritionResult.ConfidenceDetails),
            Summary = nutritionResult.Summary,
            InterpretationSummary = nutritionResult.Summary ?? "Análise nutricional visual realizada",
            EstimatedNutritionProfile = nutritionResult.EstimatedNutritionProfile,
            Classification = nutritionResult.Classification,
            ConfidenceDetails = nutritionResult.ConfidenceDetails,
            Warnings = nutritionResult.Warnings,
            RawExtractedText = nutritionResult.RawExtractedText,
            ErrorMessage = nutritionResult.ErrorMessage
        };
    }

    /// <summary>
    /// Maps confidence details to overall confidence level.
    /// </summary>
    private ConfidenceLevel MapOverallConfidence(ConfidenceDetailsDto? confidenceDetails)
    {
        if (confidenceDetails == null)
        {
            return ConfidenceLevel.Low;
        }

        double average = (confidenceDetails.ProductIdentification + 
                         confidenceDetails.VisibleClaimsExtraction + 
                         confidenceDetails.EstimatedNutritionProfile + 
                         confidenceDetails.Classification) / 4.0;

        return average switch
        {
            >= 0.7 => ConfidenceLevel.High,
            >= 0.4 => ConfidenceLevel.Medium,
            _ => ConfidenceLevel.Low
        };
    }

    /// <summary>
    /// Creates an error result.
    /// </summary>
    private VisualInterpretationResult CreateErrorResult(string errorMessage)
    {
        return new VisualInterpretationResult
        {
            InterpretationConfidence = ConfidenceLevel.Low,
            InterpretationSummary = errorMessage,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Salva imagem que falhou para diagnóstico posterior.
    /// </summary>
    private async Task SaveFailedImageForDiagnosticAsync(string originalPath, string aiResponse, string reason)
    {
        try
        {
            var diagnosticFolder = Path.Combine(Path.GetTempPath(), "LabelWise_FailedImages");
            Directory.CreateDirectory(diagnosticFolder);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{timestamp}_{reason}_{Path.GetFileName(originalPath)}";
            var diagnosticPath = Path.Combine(diagnosticFolder, fileName);
            var logPath = Path.ChangeExtension(diagnosticPath, ".log.txt");

            File.Copy(originalPath, diagnosticPath, true);

            var logContent = $"""
                Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}
                Reason: {reason}
                Original Path: {originalPath}

                ═══════════════════════════════════════
                AI RESPONSE
                ═══════════════════════════════════════
                {aiResponse}
                """;

            await File.WriteAllTextAsync(logPath, logContent);

            _logger.LogInformation("[DIAGNOSTIC] Failed image saved to: {DiagnosticPath}", diagnosticPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DIAGNOSTIC] Could not save failed image for diagnostic");
        }
    }

    #endregion

    #region Normalization and Business Logic Helpers

    /// <summary>
    /// Normalizes and filters visible claims to remove noise and standardize format.
    /// Mantém apenas alegações nutricionais, funcionais ou promocionais.
    /// Remove nomes de produtos, marcas e descrições genéricas.
    /// </summary>
    private static List<string> NormalizeVisibleClaims(List<string>? rawClaims)
    {
        if (rawClaims?.Any() != true)
            return new List<string>();

        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var claim in rawClaims)
        {
            if (string.IsNullOrWhiteSpace(claim))
                continue;

            var cleaned = CleanClaim(claim);
            if (IsValidNutritionalClaim(cleaned))
            {
                normalized.Add(cleaned);
            }
        }

        return normalized.ToList();
    }

    /// <summary>
    /// Cleans individual claim text.
    /// </summary>
    private static string CleanClaim(string claim)
    {
        var cleaned = claim.Trim();

        // Remove quotes and other noise
        cleaned = cleaned.Trim('"', '\'', '•', '-');

        // Capitalize properly
        if (cleaned.Length > 0)
        {
            cleaned = char.ToUpperInvariant(cleaned[0]) + 
                     (cleaned.Length > 1 ? cleaned[1..] : "");
        }

        return cleaned;
    }

    /// <summary>
    /// Valida se uma claim é uma alegação nutricional, funcional ou promocional real.
    /// Filtra nomes de produtos, marcas e descrições genéricas.
    /// </summary>
    private static bool IsValidNutritionalClaim(string claim)
    {
        if (string.IsNullOrWhiteSpace(claim) || claim.Length < 3)
            return false;

        var claimLower = claim.ToLowerInvariant();

        // === FILTRAR NOMES DE PRODUTOS E MARCAS ===

        // Nomes de marcas conhecidas (adicionar conforme necessário)
        var knownBrands = new[]
        {
            "nestle", "nescau", "danone", "vigor", "parmalat", "italac",
            "tio joão", "camil", "urbano", "uncle ben's", "ades",
            "marilan", "bauducco", "panco", "pullman", "visconti",
            "toddy", "nescal", "ovomaltine", "italac"
        };

        if (knownBrands.Any(brand => claimLower.Contains(brand)))
        {
            return false;
        }

        // Padrões de nomes de produtos (não são claims nutricionais)
        var productNamePatterns = new[]
        {
            "arroz", "feijão", "macarrão", "biscoito", "bolacha",
            "queijo", "requeijão", "cream cheese", "iogurte",
            "leite", "achocolatado", "cereal", "pão", "farinha",
            "óleo", "azeite", "margarina", "manteiga",
            "tipo 1", "tipo 2", "tipo 3", // Classificações de arroz
            "sabor", "tradicional", "original", "clássico"
        };

        // Se a claim contém apenas padrões de nome de produto, não é uma claim nutricional
        var onlyProductName = productNamePatterns.Any(pattern => 
            claimLower == pattern || claimLower.StartsWith(pattern + " ") || claimLower.EndsWith(" " + pattern));

        if (onlyProductName)
        {
            return false;
        }

        // === FILTRAR DESCRIÇÕES GENÉRICAS ===

        var genericDescriptions = new[]
        {
            "queijo minas frescal",
            "processado cremoso",
            "produto alimentício",
            "alimento",
            "comida",
            "produto",
            "novo",
            "lançamento",
            "embalagem"
        };

        if (genericDescriptions.Any(desc => claimLower.Contains(desc)))
        {
            return false;
        }

        // === ACEITAR APENAS CLAIMS NUTRICIONAIS, FUNCIONAIS OU PROMOCIONAIS ===

        // Palavras-chave que indicam alegações nutricionais válidas
        var nutritionalKeywords = new[]
        {
            // Nutrientes
            "vitamina", "mineral", "cálcio", "ferro", "zinco", "magnésio",
            "proteína", "fibra", "ômega", "ácido fólico", "colágeno",

            // Fortificação
            "fortificado", "enriquecido", "fonte de", "rico em", "alto teor",
            "contém", "adicionado",

            // Ausências/Reduções (importantes para consumidores)
            "sem", "zero", "não contém", "livre de", "isento",
            "light", "diet", "reduzido", "baixo teor",

            // Glúten e lactose (muito importantes)
            "glúten", "lactose", "sem glúten", "sem lactose",

            // Características naturais
            "natural", "orgânico", "integral", "inteiro",

            // Processamento
            "não transgênico", "sem conservantes", "sem corantes",
            "sem aromatizantes", "sem aditivos", "artesanal",

            // Selos e certificações
            "halal", "kosher", "vegano", "vegetariano"
        };

        // A claim é válida se contém pelo menos uma palavra-chave nutricional
        var isNutritionalClaim = nutritionalKeywords.Any(keyword => 
            claimLower.Contains(keyword));

        return isNutritionalClaim;
    }

    /// <summary>
    /// Normalizes product name by creating a more descriptive and accurate product denomination.
    /// Avoids brand repetition and builds proper product names based on category and visible claims.
    /// </summary>
    private static string NormalizeProductName(string? productName, string? brand, string? category, List<string>? visibleClaims)
    {
        var claims = visibleClaims ?? new List<string>();

        // Clean original name first
        var cleanedName = CleanProductNameFromBrand(productName, brand);

        // If we have a good cleaned name, use it
        if (!string.IsNullOrWhiteSpace(cleanedName) && 
            !IsNameTooGenericOrSimilarToBrand(cleanedName, brand, category))
        {
            return ApplyCategoryEnhancements(cleanedName, category, claims);
        }

        // Build name from category and claims
        return BuildProductNameFromCategory(category, claims, brand);
    }

    /// <summary>
    /// Cleans product name from brand repetition and noise.
    /// </summary>
    private static string? CleanProductNameFromBrand(string? productName, string? brand)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return null;

        if (string.IsNullOrWhiteSpace(brand))
            return productName.Trim();

        var cleaned = productName.Replace(brand, "", StringComparison.OrdinalIgnoreCase).Trim();

        // Remove common separators left behind
        cleaned = cleaned.TrimStart('-', '•', '|', ':').Trim();

        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    /// <summary>
    /// Checks if the product name is too generic or too similar to brand name.
    /// </summary>
    private static bool IsNameTooGenericOrSimilarToBrand(string productName, string? brand, string? category)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return true;

        var nameLower = productName.ToLowerInvariant().Trim();

        // Check if name is just the brand
        if (!string.IsNullOrWhiteSpace(brand) && 
            string.Equals(nameLower, brand.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check if name is just a single word that matches category
        if (!string.IsNullOrWhiteSpace(category))
        {
            var categoryWords = category.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var nameWords = nameLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (nameWords.Length == 1 && categoryWords.Contains(nameWords[0]))
            {
                return true;
            }
        }

        // Check for brand similarity
        if (!string.IsNullOrWhiteSpace(brand))
        {
            var brandLower = brand.ToLowerInvariant();
            if (nameLower.Contains(brandLower) || brandLower.Contains(nameLower))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Applies category-specific enhancements to product name.
    /// </summary>
    private static string ApplyCategoryEnhancements(string baseName, string? category, List<string> claims)
    {
        if (string.IsNullOrWhiteSpace(category))
            return baseName;

        var categoryLower = category.ToLowerInvariant();
        var enhanced = baseName;

        // Rice enhancements
        if (categoryLower.Contains("arroz"))
        {
            if (!enhanced.Contains("Tipo") && claims.Any(c => c.Contains("tipo 1", StringComparison.OrdinalIgnoreCase)))
            {
                enhanced += " Tipo 1";
            }
            if (!enhanced.Contains("Integral") && claims.Any(c => c.Contains("integral", StringComparison.OrdinalIgnoreCase)))
            {
                enhanced = enhanced.Replace("Branco", "Integral");
            }
        }

        return enhanced;
    }

    /// <summary>
    /// Builds product name from category and claims.
    /// </summary>
    private static string BuildProductNameFromCategory(string? category, List<string> claims, string? brand)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return "Produto alimentício";
        }

        var categoryLower = category.ToLowerInvariant();

        // Rice products
        if (categoryLower.Contains("arroz"))
        {
            var riceType = "Arroz Branco";
            var riceGrade = "";

            // Check for rice type in visible claims
            if (claims.Any(c => c.Contains("integral", StringComparison.OrdinalIgnoreCase)))
            {
                riceType = "Arroz Integral";
            }
            else if (claims.Any(c => c.Contains("parboilizado", StringComparison.OrdinalIgnoreCase)))
            {
                riceType = "Arroz Parboilizado";
            }

            // Check for rice grade
            if (claims.Any(c => c.Contains("tipo 1", StringComparison.OrdinalIgnoreCase)))
            {
                riceGrade = " Tipo 1";
            }
            else if (claims.Any(c => c.Contains("tipo 2", StringComparison.OrdinalIgnoreCase)))
            {
                riceGrade = " Tipo 2";
            }

            return riceType + riceGrade;
        }

        // Chocolate powder products
        if (categoryLower.Contains("achocolatado"))
        {
            var hasVitamins = claims.Any(c => c.Contains("vitamina", StringComparison.OrdinalIgnoreCase) || 
                                             c.Contains("fortificado", StringComparison.OrdinalIgnoreCase));
            return hasVitamins ? "Achocolatado em Pó Fortificado" : "Achocolatado em Pó";
        }

        // Dairy products
        if (categoryLower.Contains("queijo"))
        {
            if (categoryLower.Contains("requeijão"))
                return "Requeijão Cremoso";
            if (categoryLower.Contains("cream cheese") || categoryLower.Contains("cream-cheese"))
                return "Cream Cheese";
            if (categoryLower.Contains("minas"))
                return "Queijo Minas";
            return "Queijo";
        }

        // Cookies/crackers
        if (categoryLower.Contains("biscoito") || categoryLower.Contains("bolacha"))
        {
            if (claims.Any(c => c.Contains("recheado", StringComparison.OrdinalIgnoreCase)))
                return "Biscoito Recheado";
            if (claims.Any(c => c.Contains("cream cracker", StringComparison.OrdinalIgnoreCase)))
                return "Biscoito Cream Cracker";
            return "Biscoito";
        }

        // Cereals
        if (categoryLower.Contains("cereal"))
        {
            var hasVitamins = claims.Any(c => c.Contains("vitamina", StringComparison.OrdinalIgnoreCase) || 
                                             c.Contains("fortificado", StringComparison.OrdinalIgnoreCase));
            return hasVitamins ? "Cereal Matinal Fortificado" : "Cereal Matinal";
        }

        // Pasta
        if (categoryLower.Contains("macarrão") || categoryLower.Contains("massa"))
        {
            if (claims.Any(c => c.Contains("espaguete", StringComparison.OrdinalIgnoreCase)))
                return "Macarrão Espaguete";
            if (claims.Any(c => c.Contains("penne", StringComparison.OrdinalIgnoreCase)))
                return "Macarrão Penne";
            return "Macarrão";
        }

        // Bread
        if (categoryLower.Contains("pão"))
        {
            if (claims.Any(c => c.Contains("forma", StringComparison.OrdinalIgnoreCase)))
                return "Pão de Forma";
            if (claims.Any(c => c.Contains("integral", StringComparison.OrdinalIgnoreCase)))
                return "Pão Integral";
            return "Pão";
        }

        // Fallback: capitalize category properly
        return CapitalizeCategory(category);
    }

    /// <summary>
    /// Capitalizes category properly.
    /// </summary>
    private static string CapitalizeCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return "Produto alimentício";

        var words = category.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpperInvariant(words[i][0]) + 
                          (words[i].Length > 1 ? words[i][1..].ToLowerInvariant() : "");
            }
        }
        return string.Join(" ", words);
    }

    /// <summary>
    /// Builds an improved summary that is more technical and informative.
    /// </summary>
    private static string BuildImprovedSummary(string? productName, string? category, AnalysisMode analysisMode, 
        List<string> visibleClaims, NutritionProfileResponse? nutritionProfile)
    {
        var productDescription = !string.IsNullOrWhiteSpace(productName) ? productName : 
                                 (!string.IsNullOrWhiteSpace(category) ? category : "produto");

        // CORREÇÃO: Melhorar descrição do método de análise baseado em dados reais
        var analysisMethod = BuildAnalysisMethodDescription(analysisMode, nutritionProfile);

        // Build category-specific insights
        var categoryInsight = BuildCategorySpecificInsight(category, visibleClaims, analysisMode);

        // Build nutritional limitation notice if applicable
        var nutritionalLimitation = BuildNutritionalLimitationNotice(analysisMode, nutritionProfile);

        // Combine all parts
        var parts = new List<string> { $"{productDescription} analisado {analysisMethod}" };

        if (!string.IsNullOrWhiteSpace(categoryInsight))
            parts.Add(categoryInsight);

        if (!string.IsNullOrWhiteSpace(nutritionalLimitation))
            parts.Add(nutritionalLimitation);

        return string.Join(", ", parts) + ".";
    }

    /// <summary>
    /// Constrói descrição do método de análise baseado nos dados disponíveis.
    /// </summary>
    private static string BuildAnalysisMethodDescription(AnalysisMode analysisMode, NutritionProfileResponse? nutritionProfile)
    {
        if (analysisMode == AnalysisMode.FrontOfPackageOnly)
        {
            return "baseada na análise da categoria, pois a tabela nutricional não está legível na imagem";
        }

        // Para FullNutritionLabel, verificar se temos dados reais
        if (nutritionProfile == null)
        {
            return "com informações nutricionais identificadas";
        }

        // Contar quantos campos foram realmente extraídos
        var extractedFields = new List<string>();

        if (nutritionProfile.CaloriesPer100g.HasValue && nutritionProfile.CaloriesPer100g.Value > 0)
            extractedFields.Add("calorias");

        if (nutritionProfile.EstimatedProteinPer100g.HasValue && nutritionProfile.EstimatedProteinPer100g.Value >= 0)
            extractedFields.Add("proteínas");

        if (nutritionProfile.EstimatedFatPer100g.HasValue && nutritionProfile.EstimatedFatPer100g.Value >= 0)
            extractedFields.Add("gorduras");

        if (nutritionProfile.EstimatedSugarPer100g.HasValue && nutritionProfile.EstimatedSugarPer100g.Value >= 0)
            extractedFields.Add("açúcares");

        if (nutritionProfile.EstimatedSodiumPer100g.HasValue && nutritionProfile.EstimatedSodiumPer100g.Value >= 0)
            extractedFields.Add("sódio");

        if (nutritionProfile.EstimatedFiberPer100g.HasValue && nutritionProfile.EstimatedFiberPer100g.Value >= 0)
            extractedFields.Add("fibras");

        if (extractedFields.Count == 0)
        {
            return "com tabela nutricional visível, porém valores específicos não puderam ser extraídos";
        }

        if (extractedFields.Count >= 5)
        {
            return "com leitura completa da tabela nutricional";
        }

        // Leitura parcial - indicar quais campos foram extraídos
        var fieldsText = extractedFields.Count == 1 
            ? extractedFields[0]
            : string.Join(", ", extractedFields.Take(extractedFields.Count - 1)) + " e " + extractedFields.Last();

        return $"com leitura parcial da tabela nutricional ({fieldsText} extraídos)";
    }

    /// <summary>
    /// Builds category-specific nutritional insights.
    /// </summary>
    private static string BuildCategorySpecificInsight(string? category, List<string> visibleClaims, AnalysisMode analysisMode)
    {
        if (string.IsNullOrWhiteSpace(category))
            return string.Empty;

        var categoryLower = category.ToLowerInvariant();
        var hasVitaminFortification = visibleClaims.Any(c => 
            c.Contains("fortificado", StringComparison.OrdinalIgnoreCase) || 
            c.Contains("vitamina", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("ferro", StringComparison.OrdinalIgnoreCase));

        // Rice products
        if (categoryLower.Contains("arroz"))
        {
            var insight = "caracterizado como fonte primária de carboidratos com perfil proteico limitado";
            if (hasVitaminFortification)
                insight += " e enriquecimento com micronutrientes";
            return insight;
        }

        // Chocolate powder
        if (categoryLower.Contains("achocolatado"))
        {
            var insight = "produto com provável alto teor de açúcar adicionado e baixa densidade proteica";
            if (hasVitaminFortification)
                insight += ", contudo fortificado com vitaminas";
            return insight;
        }

        // Cookies/crackers
        if (categoryLower.Contains("biscoito") || categoryLower.Contains("bolacha"))
        {
            return "produto ultraprocessado com expectativa de elevado teor de açúcar, gordura e aditivos";
        }

        // Dairy products
        if (categoryLower.Contains("queijo") || categoryLower.Contains("requeijão") || categoryLower.Contains("cream"))
        {
            return "produto lácteo com provável perfil rico em proteínas e gorduras, com possível teor elevado de sódio";
        }

        // Pasta
        if (categoryLower.Contains("macarrão") || categoryLower.Contains("massa"))
        {
            return "fonte de carboidratos complexos com perfil proteico moderado";
        }

        // Bread
        if (categoryLower.Contains("pão"))
        {
            var integral = visibleClaims.Any(c => c.Contains("integral", StringComparison.OrdinalIgnoreCase));
            return integral ? "produto panificado integral com maior teor de fibras" : 
                             "produto panificado com base em carboidratos refinados";
        }

        // Cereals
        if (categoryLower.Contains("cereal"))
        {
            var insight = "cereal com base em carboidratos";
            if (hasVitaminFortification)
                insight += " e fortificação com vitaminas e minerais";
            return insight;
        }

        return string.Empty;
    }

    /// <summary>
    /// Builds nutritional limitation notice when applicable.
    /// </summary>
    private static string BuildNutritionalLimitationNotice(AnalysisMode analysisMode, NutritionProfileResponse? nutritionProfile)
    {
        if (analysisMode == AnalysisMode.FrontOfPackageOnly)
        {
            return "valores nutricionais estimados por categoria devido à ausência de tabela nutricional legível";
        }

        // Check if nutrition profile has actual values or just estimates
        if (nutritionProfile != null)
        {
            bool hasActualValues = nutritionProfile.CaloriesPer100g.HasValue || 
                                   nutritionProfile.EstimatedSugarPer100g.HasValue ||
                                   nutritionProfile.EstimatedProteinPer100g.HasValue;

            if (!hasActualValues)
            {
                return "valores nutricionais específicos não identificados na tabela presente";
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Adjusts confidence details, especially for visible claims extraction.
    /// </summary>
    private static ConfidenceDetailsDto AdjustConfidenceDetails(ConfidenceDetailsResponse? originalConfidence, List<string> visibleClaims)
    {
        var productIdentificationConfidence = originalConfidence?.ProductIdentification ?? 0.5;
        var visibleClaimsConfidence = BuildVisibleClaimsConfidence(visibleClaims);
        var nutritionProfileConfidence = originalConfidence?.EstimatedNutritionProfile ?? 0.3;
        var classificationConfidence = originalConfidence?.Classification ?? 0.4;

        return new ConfidenceDetailsDto
        {
            ProductIdentification = Math.Max(0.1, Math.Min(0.9, productIdentificationConfidence)),
            VisibleClaimsExtraction = Math.Max(0.1, Math.Min(0.9, visibleClaimsConfidence)),
            EstimatedNutritionProfile = Math.Max(0.1, Math.Min(0.9, nutritionProfileConfidence)),
            Classification = Math.Max(0.1, Math.Min(0.9, classificationConfidence))
        };
    }

    /// <summary>
    /// Builds visible claims confidence based on actual claims quantity and quality.
    /// </summary>
    private static double BuildVisibleClaimsConfidence(List<string> visibleClaims)
    {
        if (visibleClaims?.Any() != true)
            return 0.2; // Very low confidence for empty claims

        var claimCount = visibleClaims.Count;
        var hasDetailedClaims = visibleClaims.Any(c => c.Length > 10); // More detailed claims indicate better reading
        var hasSpecificClaims = visibleClaims.Any(c => 
            c.Contains("vitamina", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("tipo", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("glúten", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("integral", StringComparison.OrdinalIgnoreCase));

        var baseConfidence = claimCount switch
        {
            1 => 0.4,
            2 => 0.5,
            3 => 0.6,
            >= 4 => 0.7,
            _ => 0.2
        };

        // Boost for detailed claims
        if (hasDetailedClaims)
            baseConfidence += 0.1;

        // Boost for specific/technical claims
        if (hasSpecificClaims)
            baseConfidence += 0.1;

        return Math.Min(baseConfidence, 0.9); // Cap at 0.9
    }

    /// <summary>
    /// Adjusts muscle gain classification to be less optimistic for carb-heavy, protein-poor products.
    /// </summary>
    private static ProductClassificationDto? AdjustMuscleGainClassification(ProductClassificationDto? originalClassification, 
        string? category, NutritionProfileResponse? nutritionProfile)
    {
        if (originalClassification?.MuscleGain == null)
        {
            return originalClassification;
        }

        // Preservar classificações da Azure AI que já são mais restritivas
        var currentStatus = originalClassification.MuscleGain.Status?.ToLowerInvariant();
        if (currentStatus is "fraco" or "nao_recomendado")
        {
            return originalClassification;
        }

        if (IsCarbHeavyProductLowProtein(category, nutritionProfile))
        {
            var adjustedMuscleGain = new HealthProfileResult
            {
                Status = "consumo_moderado",
                Reason = "Contribui como fonte de energia para treinos, mas não é relevante como fonte proteica"
            };

            return new ProductClassificationDto
            {
                Diabetic = originalClassification.Diabetic,
                BloodPressure = originalClassification.BloodPressure,
                WeightLoss = originalClassification.WeightLoss,
                MuscleGain = adjustedMuscleGain
            };
        }

        return originalClassification;
    }

    /// <summary>
    /// Determines if a product is carb-heavy and protein-poor.
    /// </summary>
    private static bool IsCarbHeavyProductLowProtein(string? category, NutritionProfileResponse? nutritionProfile)
    {
        // Check by category first
        if (!string.IsNullOrWhiteSpace(category))
        {
            var categoryLower = category.ToLowerInvariant();
            if (categoryLower.Contains("arroz") || categoryLower.Contains("achocolatado") || 
                categoryLower.Contains("biscoito") || categoryLower.Contains("pão") ||
                categoryLower.Contains("macarrão") || categoryLower.Contains("massa") ||
                categoryLower.Contains("cereal"))
            {
                return true;
            }
        }

        // Check by nutrition profile if available
        if (nutritionProfile?.EstimatedProteinPer100g.HasValue == true)
        {
            var protein = nutritionProfile.EstimatedProteinPer100g.Value;
            return protein < 8; // Less than 8g protein per 100g indicates low protein content
        }

        return false;
    }

    /// <summary>
    /// Ensures the classification object and its properties are not null.
    /// CORREÇÃO: Gera classificações básicas quando há dados nutricionais disponíveis.
    /// </summary>
    private static ProductClassificationDto EnsureClassificationIsComplete(
        ProductClassificationDto? classification, 
        string? category, 
        NutritionProfileResponse? nutritionProfile)
    {
        classification ??= new ProductClassificationDto();

        // Tentar gerar classificações básicas se há dados nutricionais
        var hasNutritionData = nutritionProfile != null && HasRealNutritionData(nutritionProfile);

        if (hasNutritionData)
        {
            classification.Diabetic ??= GenerateBasicDiabeticClassification(nutritionProfile!);
            classification.BloodPressure ??= GenerateBasicBloodPressureClassification(nutritionProfile!);
            classification.WeightLoss ??= GenerateBasicWeightLossClassification(nutritionProfile!);
            classification.MuscleGain ??= GenerateBasicMuscleGainClassification(nutritionProfile!);
        }
        else
        {
            var defaultReason = "Classificação não pôde ser determinada a partir da imagem.";
            classification.Diabetic ??= new HealthProfileResult { Status = "indeterminado", Reason = defaultReason };
            classification.BloodPressure ??= new HealthProfileResult { Status = "indeterminado", Reason = defaultReason };
            classification.WeightLoss ??= new HealthProfileResult { Status = "indeterminado", Reason = defaultReason };
            classification.MuscleGain ??= new HealthProfileResult { Status = "indeterminado", Reason = defaultReason };
        }

        return classification;
    }

    /// <summary>
    /// Gera classificação básica para diabéticos baseado em açúcar e carboidratos.
    /// </summary>
    private static HealthProfileResult GenerateBasicDiabeticClassification(NutritionProfileResponse profile)
    {
        var sugar = profile.EstimatedSugarPer100g ?? 0;
        var calories = profile.CaloriesPer100g ?? 0;

        if (sugar > 15 || calories > 400)
        {
            return new HealthProfileResult
            {
                Status = "nao_recomendado",
                Reason = $"Alto teor de açúcar ({sugar:0.#}g/100g) ou densidade calórica elevada ({calories:0}kcal/100g)"
            };
        }

        if (sugar > 5 || calories > 250)
        {
            return new HealthProfileResult
            {
                Status = "consumo_moderado",
                Reason = $"Teor moderado de açúcar ({sugar:0.#}g/100g); consumo controlado é recomendado"
            };
        }

        return new HealthProfileResult
        {
            Status = "adequado",
            Reason = $"Baixo teor de açúcar ({sugar:0.#}g/100g) e densidade calórica moderada"
        };
    }

    /// <summary>
    /// Gera classificação básica para hipertensos baseado em sódio.
    /// </summary>
    private static HealthProfileResult GenerateBasicBloodPressureClassification(NutritionProfileResponse profile)
    {
        var sodium = profile.EstimatedSodiumPer100g ?? 0;

        if (sodium > 600)
        {
            return new HealthProfileResult
            {
                Status = "nao_recomendado",
                Reason = $"Alto teor de sódio ({sodium:0}mg/100g); não adequado para hipertensos"
            };
        }

        if (sodium > 300)
        {
            return new HealthProfileResult
            {
                Status = "consumo_moderado",
                Reason = $"Teor moderado de sódio ({sodium:0}mg/100g); consumo ocasional e controlado"
            };
        }

        return new HealthProfileResult
        {
            Status = "adequado",
            Reason = $"Baixo teor de sódio ({sodium:0}mg/100g)"
        };
    }

    /// <summary>
    /// Gera classificação básica para emagrecimento baseado em calorias e gorduras.
    /// </summary>
    private static HealthProfileResult GenerateBasicWeightLossClassification(NutritionProfileResponse profile)
    {
        var calories = profile.CaloriesPer100g ?? 0;
        var fat = profile.EstimatedFatPer100g ?? 0;
        var sugar = profile.EstimatedSugarPer100g ?? 0;

        if (calories > 400 || fat > 20 || sugar > 15)
        {
            return new HealthProfileResult
            {
                Status = "nao_recomendado",
                Reason = $"Alta densidade calórica ({calories:0}kcal/100g) ou elevado teor de gorduras/açúcares"
            };
        }

        if (calories > 250 || fat > 10 || sugar > 8)
        {
            return new HealthProfileResult
            {
                Status = "consumo_moderado",
                Reason = $"Densidade calórica moderada ({calories:0}kcal/100g); consumo controlado nas porções"
            };
        }

        return new HealthProfileResult
        {
            Status = "adequado",
            Reason = $"Baixa densidade calórica ({calories:0}kcal/100g) e perfil nutricional favorável"
        };
    }

    /// <summary>
    /// Gera classificação básica para ganho muscular baseado em proteínas.
    /// </summary>
    private static HealthProfileResult GenerateBasicMuscleGainClassification(NutritionProfileResponse profile)
    {
        var protein = profile.EstimatedProteinPer100g ?? 0;
        var calories = profile.CaloriesPer100g ?? 0;

        if (protein > 15)
        {
            return new HealthProfileResult
            {
                Status = "adequado",
                Reason = $"Alto teor proteico ({protein:0.#}g/100g); adequado para suporte ao ganho muscular"
            };
        }

        if (protein > 8)
        {
            return new HealthProfileResult
            {
                Status = "consumo_moderado",
                Reason = $"Teor proteico moderado ({protein:0.#}g/100g); pode complementar a dieta hiperproteica"
            };
        }

        // Produtos com baixa proteína mas alta caloria podem servir como energia
        if (calories > 300)
        {
            return new HealthProfileResult
            {
                Status = "consumo_moderado",
                Reason = $"Baixo teor proteico ({protein:0.#}g/100g), mas serve como fonte de energia para treinos"
            };
        }

        return new HealthProfileResult
        {
            Status = "fraco",
            Reason = $"Baixo teor proteico ({protein:0.#}g/100g); não é relevante para ganho muscular"
        };
    }

    /// <summary>
    /// Ensures the confidence details object is not null.
    /// </summary>
    private static ConfidenceDetailsDto EnsureConfidenceDetailsIsComplete(ConfidenceDetailsDto? confidence)
    {
        return confidence ?? new ConfidenceDetailsDto
        {
            ProductIdentification = 0.2,
            VisibleClaimsExtraction = 0.2,
            EstimatedNutritionProfile = 0.2,
            Classification = 0.2
        };
    }

    #endregion

    #region Nutrition Profile Detection

    /// <summary>
    /// Detecta se há leitura nutricional real (parcial ou completa) — requer ≥ 2 campos.
    /// </summary>
    private static bool HasRealNutritionData(NutritionProfileResponse? profile)
    {
        if (profile == null) return false;
        int n = 0;
        if (profile.CaloriesPer100g.HasValue && profile.CaloriesPer100g.Value > 0) n++;
        if (profile.EstimatedSugarPer100g.HasValue && profile.EstimatedSugarPer100g.Value >= 0) n++;
        if (profile.EstimatedProteinPer100g.HasValue && profile.EstimatedProteinPer100g.Value >= 0) n++;
        if (profile.EstimatedSodiumPer100g.HasValue && profile.EstimatedSodiumPer100g.Value >= 0) n++;
        if (profile.EstimatedFatPer100g.HasValue && profile.EstimatedFatPer100g.Value >= 0) n++;
        if (profile.EstimatedFiberPer100g.HasValue && profile.EstimatedFiberPer100g.Value >= 0) n++;
        return n >= 2;
    }

    /// <summary>
    /// Retorna true se QUALQUER campo de macro foi extraído — evidência mínima de tabela.
    /// </summary>
    private static bool HasAnyRealNutritionField(NutritionProfileResponse? profile)
    {
        if (profile == null) return false;
        return (profile.CaloriesPer100g.HasValue           && profile.CaloriesPer100g.Value > 0)
            || (profile.EstimatedProteinPer100g.HasValue   && profile.EstimatedProteinPer100g.Value >= 0)
            || (profile.EstimatedFatPer100g.HasValue       && profile.EstimatedFatPer100g.Value >= 0)
            || (profile.EstimatedSugarPer100g.HasValue     && profile.EstimatedSugarPer100g.Value >= 0)
            || (profile.EstimatedSodiumPer100g.HasValue    && profile.EstimatedSodiumPer100g.Value >= 0)
            || (profile.EstimatedFiberPer100g.HasValue     && profile.EstimatedFiberPer100g.Value >= 0)
            || (profile.EstimatedCarbsPer100g.HasValue     && profile.EstimatedCarbsPer100g.Value >= 0)
            || (profile.EstimatedSaturatedFatPer100g.HasValue && profile.EstimatedSaturatedFatPer100g.Value >= 0);
    }

    /// <summary>
    /// Detecta sinais textuais de tabela nutricional no campo basis retornado pela IA.
    /// Tolera variações de OCR e ausência de acentuação.
    /// </summary>
    private static bool BasisIndicatesTable(string? basis)
    {
        if (string.IsNullOrWhiteSpace(basis)) return false;
        var b = basis.ToLowerInvariant();
        var tableSignals = new[]
        {
            "tabela nutricional", "informação nutricional", "informacao nutricional",
            "leitura da tabela", "leitura parcial", "tabela lida", "extraído da tabela",
            "extraido da tabela", "valores reais", "valores lidos", "dados lidos",
            "tabela visível", "tabela visivel", "tabela detectada"
        };
        return tableSignals.Any(s => b.Contains(s, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Ajusta o analysisMode baseado em dados reais extraídos ou sinais textuais de tabela.
    /// </summary>
    private static AnalysisMode AdjustAnalysisModeBasedOnRealData(
        AnalysisMode originalMode,
        NutritionProfileResponse? profile,
        string? basis)
    {
        if (originalMode == AnalysisMode.FullNutritionLabel)
        {
            return AnalysisMode.FullNutritionLabel;
        }

        // Promover para FullNutritionLabel quando:
        // - Há QUALQUER dado quantitativo real extraído (1 campo já é evidência de tabela)
        // - OU o basis/texto indica que uma tabela foi lida
        if (HasAnyRealNutritionField(profile) || BasisIndicatesTable(basis))
        {
            return AnalysisMode.FullNutritionLabel;
        }

        return originalMode;
    }

    /// <summary>
    /// Identifica categorias ultraprocessadas para o post-processor de extração.
    /// </summary>
    private static bool IsUltraProcessedCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return false;
        var c = category.ToLowerInvariant();
        return c.Contains("refrigerante") || c.Contains("salgadinho") || c.Contains("biscoito recheado")
            || c.Contains("embutido") || c.Contains("salsicha") || c.Contains("linguiça")
            || c.Contains("macarrão instantâneo") || c.Contains("miojo")
            || c.Contains("achocolatado em pó") || c.Contains("biscoito amanteigado");
    }

    #endregion
}
