using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using LabelWise.Application.Configuration;
using LabelWise.Application.DTOs.AI;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;
using LabelWise.Infrastructure.AI.Prompts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace LabelWise.Infrastructure.AI
{
    /// <summary>
    /// Implements the IVisualInterpreter using Azure OpenAI's vision capabilities.
    /// </summary>
    public class AzureOpenAiVisionInterpreter : IVisualInterpreter
    {
        private readonly AzureOpenAiVisionOptions _options;
        private readonly ILogger<AzureOpenAiVisionInterpreter> _logger;

        public AzureOpenAiVisionInterpreter(IOptions<AzureOpenAiVisionOptions> options, ILogger<AzureOpenAiVisionInterpreter> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public async Task<VisualInterpretationResult> InterpretImageAsync(VisualInterpretationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ImagePath) || !File.Exists(request.ImagePath))
            {
                _logger.LogWarning("Image path is null, empty, or file does not exist: {ImagePath}", request.ImagePath);
                return new VisualInterpretationResult 
                { 
                    InterpretationConfidence = ConfidenceLevel.Low,
                    InterpretationSummary = "Image file not found or invalid path."
                };
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Starting visual interpretation for image: {ImagePath}", request.ImagePath);

                // Validate configuration
                if (string.IsNullOrWhiteSpace(_options.Endpoint) || 
                    string.IsNullOrWhiteSpace(_options.ApiKey) || 
                    string.IsNullOrWhiteSpace(_options.VisionDeployment))
                {
                    _logger.LogError("Azure OpenAI Vision configuration is incomplete. Endpoint, ApiKey, and VisionDeployment are required.");
                    return new VisualInterpretationResult 
                    { 
                        InterpretationConfidence = ConfidenceLevel.Low,
                        InterpretationSummary = "Azure OpenAI Vision service is not properly configured."
                    };
                }

                // Read image file
                byte[] imageBytes = await File.ReadAllBytesAsync(request.ImagePath);
                _logger.LogInformation("Read {ByteCount} bytes from image file", imageBytes.Length);

                // Get MIME type
                string mimeType = GetMimeType(request.ImagePath);

                // Build data URL
                string dataUrl = BuildDataUrl(imageBytes, mimeType);

                // Create OpenAI ChatClient for Azure OpenAI
                // For Azure OpenAI, the endpoint should be: https://your-resource.openai.azure.com/
                string normalizedEndpoint = _options.Endpoint.TrimEnd('/');

                _logger.LogInformation("========================================");
                _logger.LogInformation("AZURE OPENAI VISION - CONNECTION DETAILS");
                _logger.LogInformation("========================================");
                _logger.LogInformation("Endpoint: {Endpoint}", normalizedEndpoint);
                _logger.LogInformation("Deployment: {Deployment}", _options.VisionDeployment);
                _logger.LogInformation("API Key Length: {KeyLength}", _options.ApiKey?.Length ?? 0);
                _logger.LogInformation("Expected URL: {ExpectedUrl}", 
                    $"{normalizedEndpoint}/openai/deployments/{_options.VisionDeployment}/chat/completions");
                _logger.LogInformation("========================================");

                // Create AzureOpenAIClient (CORRIGIDO!)
                AzureOpenAIClient azureClient = new(
                    new Uri(normalizedEndpoint),
                    new AzureKeyCredential(_options.ApiKey)
                );

                // Get ChatClient from the AzureOpenAIClient
                ChatClient chatClient = azureClient.GetChatClient(_options.VisionDeployment);

                _logger.LogInformation("✅ ChatClient created successfully for deployment: {Deployment}", _options.VisionDeployment);

                // Build chat messages with nutrition analysis prompt
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
                    MaxOutputTokenCount = 2000, // Aumentado para suportar respostas mais detalhadas
                    Temperature = 0.1f // Reduzida para maior consistência
                };

                // Call the model
                _logger.LogInformation("Calling Azure OpenAI Vision model...");
                var completion = await chatClient.CompleteChatAsync(messages, chatOptions);

                stopwatch.Stop();
                _logger.LogInformation("Azure OpenAI Vision call completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

                // Extract response text
                string responseText = completion.Value.Content[0].Text;
                _logger.LogInformation("Received response: {Response}", responseText);

                // Try to parse structured JSON response
                var result = TryParseStructuredResponse(responseText);

                if (result == null)
                {
                    _logger.LogWarning("Failed to parse structured JSON response, attempting fallback extraction");
                    result = TryFallbackFromRawText(responseText);
                }

                if (result == null)
                {
                    _logger.LogError("Could not extract any meaningful data from response");
                    return new VisualInterpretationResult 
                    { 
                        InterpretationConfidence = ConfidenceLevel.Low,
                        InterpretationSummary = "Could not interpret the image content."
                    };
                }

                _logger.LogInformation(
                    "Visual interpretation completed: CaptureType={CaptureType}, Confidence={Confidence}, Product={ProductName}",
                    result.ProbableCaptureType,
                    result.InterpretationConfidence,
                    result.ProbableProductName ?? "unknown"
                );

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error during Azure OpenAI Vision interpretation for image: {ImagePath}", request.ImagePath);
                return new VisualInterpretationResult 
                { 
                    InterpretationConfidence = ConfidenceLevel.Low,
                    InterpretationSummary = $"Error during visual interpretation: {ex.Message}"
                };
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

        /// <summary>
        /// Tries to parse the structured JSON response from the model.
        /// </summary>
        private VisualInterpretationResult? TryParseStructuredResponse(string responseText)
        {
            try
            {
                // Clean up response text - remove markdown code blocks if present
                string jsonText = ExtractJson(responseText);

                var visionResponse = JsonSerializer.Deserialize<VisionModelResponse>(jsonText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                });

                if (visionResponse == null)
                {
                    return null;
                }

                return new VisualInterpretationResult
                {
                    ProbableProductName = visionResponse.ProductName,
                    ProbableBrand = visionResponse.Brand,
                    ProbableCategory = visionResponse.Category,
                    ProbablePackageWeight = visionResponse.PackageWeight,
                    VisibleClaims = visionResponse.VisibleClaims ?? new List<string>(),
                    RawExtractedText = visionResponse.RawExtractedText,
                    ProbableCaptureType = MapCaptureType(visionResponse.CaptureType),
                    InterpretationConfidence = MapConfidence(visionResponse.Confidence),
                    InterpretationSummary = BuildSummary(visionResponse)
                };
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize JSON response");
                return null;
            }
        }

        /// <summary>
        /// Extracts JSON from response text that may contain markdown code blocks.
        /// </summary>
        private string ExtractJson(string responseText)
        {
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

        /// <summary>
        /// Attempts to extract information from raw text when JSON parsing fails.
        /// </summary>
        private VisualInterpretationResult? TryFallbackFromRawText(string responseText)
        {
            try
            {
                var result = new VisualInterpretationResult
                {
                    InterpretationConfidence = ConfidenceLevel.Low,
                    InterpretationSummary = "Extracted information from unstructured response"
                };

                // Try to extract weight
                var weightMatch = Regex.Match(responseText, @"\b(\d+\s?(?:g|kg|ml|l))\b", RegexOptions.IgnoreCase);
                if (weightMatch.Success)
                {
                    result.InterpretationSummary += $" - Found weight: {weightMatch.Value}";
                }

                // Try to detect capture type from keywords
                if (Regex.IsMatch(responseText, @"nutrition|nutri[çc][ãa]o|tabela|facts", RegexOptions.IgnoreCase))
                {
                    result.ProbableCaptureType = CaptureType.NutritionTable;
                }
                else if (Regex.IsMatch(responseText, @"ingredient|ingred", RegexOptions.IgnoreCase))
                {
                    result.ProbableCaptureType = CaptureType.IngredientsList;
                }
                else if (Regex.IsMatch(responseText, @"al[ée]rgen|allergen", RegexOptions.IgnoreCase))
                {
                    result.ProbableCaptureType = CaptureType.AllergenStatement;
                }
                else if (Regex.IsMatch(responseText, @"barcode|c[óo]digo de barras|ean", RegexOptions.IgnoreCase))
                {
                    result.ProbableCaptureType = CaptureType.Barcode;
                }
                else
                {
                    result.ProbableCaptureType = CaptureType.FrontPackaging;
                }

                return result;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Maps the string capture type from the model to the CaptureType enum.
        /// </summary>
        private CaptureType MapCaptureType(string? captureType)
        {
            if (string.IsNullOrWhiteSpace(captureType))
            {
                return CaptureType.FrontPackaging;
            }

            return captureType.ToLowerInvariant() switch
            {
                "frontpackaging" or "front" or "packaging" => CaptureType.FrontPackaging,
                "nutritiontable" or "nutrition" or "table" => CaptureType.NutritionTable,
                "ingredientslist" or "ingredients" or "list" => CaptureType.IngredientsList,
                "allergenstatement" or "allergen" or "allergens" => CaptureType.AllergenStatement,
                "barcode" or "ean" or "upc" => CaptureType.Barcode,
                _ => CaptureType.FrontPackaging
            };
        }

        /// <summary>
        /// Maps the confidence value to ConfidenceLevel enum.
        /// </summary>
        private ConfidenceLevel MapConfidence(double confidence)
        {
            return confidence switch
            {
                >= 0.7 => ConfidenceLevel.High,
                >= 0.4 => ConfidenceLevel.Medium,
                _ => ConfidenceLevel.Low
            };
        }

        /// <summary>
        /// Builds a human-readable summary from the vision response.
        /// </summary>
        private string BuildSummary(VisionModelResponse response)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(response.ProductName))
            {
                parts.Add($"Product: {response.ProductName}");
            }

            if (!string.IsNullOrWhiteSpace(response.Brand))
            {
                parts.Add($"Brand: {response.Brand}");
            }

            if (!string.IsNullOrWhiteSpace(response.Category))
            {
                parts.Add($"Category: {response.Category}");
            }

            if (!string.IsNullOrWhiteSpace(response.PackageWeight))
            {
                parts.Add($"Weight: {response.PackageWeight}");
            }

            if (response.VisibleText != null && response.VisibleText.Any())
            {
                parts.Add($"Visible text elements: {response.VisibleText.Count}");
            }

            return parts.Any() 
                ? string.Join(" | ", parts) 
                : "Visual interpretation completed";
        }

        #endregion

        #region Internal DTOs

        /// <summary>
        /// Internal DTO for deserializing the JSON response from the vision model.
        /// </summary>
        private class VisionModelResponse
        {
            [JsonPropertyName("productName")]
            public string? ProductName { get; set; }

            [JsonPropertyName("brand")]
            public string? Brand { get; set; }

            [JsonPropertyName("category")]
            public string? Category { get; set; }

            [JsonPropertyName("packageWeight")]
            public string? PackageWeight { get; set; }

            [JsonPropertyName("captureType")]
            public string? CaptureType { get; set; }

            [JsonPropertyName("confidence")]
            public double Confidence { get; set; }

            [JsonPropertyName("visibleText")]
            public List<string>? VisibleText { get; set; }

            [JsonPropertyName("visibleClaims")]
            public List<string>? VisibleClaims { get; set; }

            [JsonPropertyName("rawExtractedText")]
            public List<string>? RawExtractedText { get; set; }
        }

        #endregion
    }
}
