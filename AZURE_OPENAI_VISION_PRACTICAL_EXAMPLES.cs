// ==============================================================================
// AZURE OPENAI VISION INTERPRETER - PRACTICAL EXAMPLES
// ==============================================================================
// This file demonstrates real-world usage of the AzureOpenAiVisionInterpreter
// service with various scenarios and expected responses.
// ==============================================================================

using LabelWise.Application.DTOs.AI;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelWise.Examples.AI;

// ==============================================================================
// EXAMPLE 1: Basic Usage - Front Packaging Analysis
// ==============================================================================

public class Example1_FrontPackagingAnalysis
{
    private readonly IVisualInterpreter _visualInterpreter;
    private readonly ILogger<Example1_FrontPackagingAnalysis> _logger;

    public Example1_FrontPackagingAnalysis(
        IVisualInterpreter visualInterpreter,
        ILogger<Example1_FrontPackagingAnalysis> logger)
    {
        _visualInterpreter = visualInterpreter;
        _logger = logger;
    }

    public async Task AnalyzeFrontPackagingAsync()
    {
        // Input: Front of a cookie package
        var request = new VisualInterpretationRequest
        {
            ImagePath = "/uploads/products/cookie-front.jpg"
        };

        var result = await _visualInterpreter.InterpretImageAsync(request);

        // Expected Output:
        // {
        //   ProbableProductName: "Biscoito Recheado Chocolate"
        //   ProbableBrand: "Nestlé"
        //   ProbableCategory: "biscoito recheado"
        //   ProbableCaptureType: CaptureType.FrontPackaging
        //   InterpretationConfidence: ConfidenceLevel.High
        //   InterpretationSummary: "Product: Biscoito Recheado Chocolate | Brand: Nestlé | Category: biscoito recheado | Weight: 140 g | Visible text elements: 8"
        // }

        _logger.LogInformation("Analysis complete: {Product} by {Brand}",
            result.ProbableProductName,
            result.ProbableBrand);
    }
}

// ==============================================================================
// EXAMPLE 2: Nutrition Table Analysis
// ==============================================================================

public class Example2_NutritionTableAnalysis
{
    private readonly IVisualInterpreter _visualInterpreter;

    public Example2_NutritionTableAnalysis(IVisualInterpreter visualInterpreter)
    {
        _visualInterpreter = visualInterpreter;
    }

    public async Task<VisualInterpretationResult> AnalyzeNutritionTableAsync(string imagePath)
    {
        var request = new VisualInterpretationRequest
        {
            ImagePath = imagePath
        };

        var result = await _visualInterpreter.InterpretImageAsync(request);

        // Expected JSON Response from Model:
        // {
        //   "productName": null,
        //   "brand": null,
        //   "category": null,
        //   "packageWeight": null,
        //   "captureType": "NutritionTable",
        //   "confidence": 0.95,
        //   "visibleText": [
        //     "Informação Nutricional",
        //     "Porção 30g",
        //     "Valor Energético",
        //     "150 kcal",
        //     "Carboidratos 20g",
        //     "Proteínas 3g"
        //   ]
        // }

        // Mapped Result:
        // {
        //   ProbableProductName: null
        //   ProbableBrand: null
        //   ProbableCategory: null
        //   ProbableCaptureType: CaptureType.NutritionTable
        //   InterpretationConfidence: ConfidenceLevel.High  (0.95 → High)
        //   InterpretationSummary: "Visible text elements: 6"
        // }

        return result;
    }
}

// ==============================================================================
// EXAMPLE 3: Protein Bar Analysis (High Confidence)
// ==============================================================================

public class Example3_ProteinBarHighConfidence
{
    public async Task<VisualInterpretationResult> AnalyzeProteinBarAsync(
        IVisualInterpreter visualInterpreter)
    {
        var request = new VisualInterpretationRequest
        {
            ImagePath = "/uploads/protein-bar-front.jpg"
        };

        var result = await visualInterpreter.InterpretImageAsync(request);

        // Model JSON Response:
        // {
        //   "productName": "Whey Bar",
        //   "brand": "Integral Médica",
        //   "category": "barra proteica",
        //   "packageWeight": "40 g",
        //   "captureType": "FrontPackaging",
        //   "confidence": 0.88,
        //   "visibleText": [
        //     "Integral Médica",
        //     "Whey Bar",
        //     "20g de Proteína",
        //     "40g",
        //     "Sabor Amendoim"
        //   ]
        // }

        // Mapped Result:
        // ProbableProductName: "Whey Bar"
        // ProbableBrand: "Integral Médica"
        // ProbableCategory: "barra proteica"
        // ProbableCaptureType: CaptureType.FrontPackaging
        // InterpretationConfidence: ConfidenceLevel.High (0.88 → High)

        Console.WriteLine($"Product: {result.ProbableProductName}");
        Console.WriteLine($"Brand: {result.ProbableBrand}");
        Console.WriteLine($"Category: {result.ProbableCategory}");
        Console.WriteLine($"Confidence: {result.InterpretationConfidence}");

        return result;
    }
}

// ==============================================================================
// EXAMPLE 4: Low Confidence / Unclear Image
// ==============================================================================

public class Example4_LowConfidenceScenario
{
    public async Task<VisualInterpretationResult> AnalyzeBlurryImageAsync(
        IVisualInterpreter visualInterpreter)
    {
        var request = new VisualInterpretationRequest
        {
            ImagePath = "/uploads/blurry-image.jpg"
        };

        var result = await visualInterpreter.InterpretImageAsync(request);

        // Model JSON Response (low confidence):
        // {
        //   "productName": null,
        //   "brand": null,
        //   "category": null,
        //   "packageWeight": null,
        //   "captureType": "Unknown",
        //   "confidence": 0.25,
        //   "visibleText": []
        // }

        // Mapped Result:
        // ProbableProductName: null
        // ProbableBrand: null
        // ProbableCategory: null
        // ProbableCaptureType: CaptureType.FrontPackaging (default)
        // InterpretationConfidence: ConfidenceLevel.Low (0.25 → Low)
        // InterpretationSummary: "Visual interpretation completed"

        if (result.InterpretationConfidence == ConfidenceLevel.Low)
        {
            Console.WriteLine("⚠️ Low confidence result. Consider re-capturing image.");
        }

        return result;
    }
}

// ==============================================================================
// EXAMPLE 5: Ingredients List Analysis
// ==============================================================================

public class Example5_IngredientsListAnalysis
{
    public async Task<VisualInterpretationResult> AnalyzeIngredientsAsync(
        IVisualInterpreter visualInterpreter)
    {
        var request = new VisualInterpretationRequest
        {
            ImagePath = "/uploads/ingredients-list.jpg"
        };

        var result = await visualInterpreter.InterpretImageAsync(request);

        // Model JSON Response:
        // {
        //   "productName": null,
        //   "brand": null,
        //   "category": null,
        //   "packageWeight": null,
        //   "captureType": "IngredientsList",
        //   "confidence": 0.82,
        //   "visibleText": [
        //     "Ingredientes:",
        //     "Farinha de trigo enriquecida",
        //     "Açúcar",
        //     "Gordura vegetal",
        //     "Cacau em pó",
        //     "Sal",
        //     "Emulsificante lecitina de soja"
        //   ]
        // }

        // Mapped Result:
        // ProbableCaptureType: CaptureType.IngredientsList
        // InterpretationConfidence: ConfidenceLevel.High (0.82 → High)
        // InterpretationSummary: "Visible text elements: 7"

        return result;
    }
}

// ==============================================================================
// EXAMPLE 6: Error Handling - Invalid Image Path
// ==============================================================================

public class Example6_ErrorHandling
{
    public async Task<VisualInterpretationResult> HandleInvalidImageAsync(
        IVisualInterpreter visualInterpreter)
    {
        var request = new VisualInterpretationRequest
        {
            ImagePath = "/uploads/non-existent.jpg"  // File doesn't exist
        };

        var result = await visualInterpreter.InterpretImageAsync(request);

        // Result:
        // ProbableCaptureType: CaptureType.FrontPackaging (default)
        // InterpretationConfidence: ConfidenceLevel.Low
        // InterpretationSummary: "Image file not found or invalid path."

        if (result.InterpretationConfidence == ConfidenceLevel.Low &&
            result.InterpretationSummary.Contains("not found"))
        {
            Console.WriteLine("❌ Image file not found. Check the file path.");
        }

        return result;
    }
}

// ==============================================================================
// EXAMPLE 7: Batch Processing Multiple Images
// ==============================================================================

public class Example7_BatchProcessing
{
    private readonly IVisualInterpreter _visualInterpreter;
    private readonly ILogger<Example7_BatchProcessing> _logger;

    public Example7_BatchProcessing(
        IVisualInterpreter visualInterpreter,
        ILogger<Example7_BatchProcessing> logger)
    {
        _visualInterpreter = visualInterpreter;
        _logger = logger;
    }

    public async Task<List<VisualInterpretationResult>> ProcessMultipleImagesAsync(
        List<string> imagePaths)
    {
        var results = new List<VisualInterpretationResult>();

        foreach (var imagePath in imagePaths)
        {
            try
            {
                var request = new VisualInterpretationRequest
                {
                    ImagePath = imagePath
                };

                var result = await _visualInterpreter.InterpretImageAsync(request);
                results.Add(result);

                _logger.LogInformation(
                    "Processed {ImagePath}: Product={Product}, Confidence={Confidence}",
                    imagePath,
                    result.ProbableProductName ?? "unknown",
                    result.InterpretationConfidence);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process image: {ImagePath}", imagePath);
            }
        }

        return results;
    }
}

// ==============================================================================
// EXAMPLE 8: Integration with Guided Capture Flow
// ==============================================================================

public class Example8_GuidedCaptureIntegration
{
    private readonly IVisualInterpreter _visualInterpreter;

    public Example8_GuidedCaptureIntegration(IVisualInterpreter visualInterpreter)
    {
        _visualInterpreter = visualInterpreter;
    }

    public async Task<CaptureValidationResult> ValidateCaptureTypeAsync(
        string imagePath,
        CaptureType expectedType)
    {
        var request = new VisualInterpretationRequest
        {
            ImagePath = imagePath
        };

        var result = await _visualInterpreter.InterpretImageAsync(request);

        // Check if the capture type matches expectations
        bool isCorrectType = result.ProbableCaptureType == expectedType;
        bool isHighConfidence = result.InterpretationConfidence == ConfidenceLevel.High;

        return new CaptureValidationResult
        {
            IsValid = isCorrectType && isHighConfidence,
            DetectedType = result.ProbableCaptureType,
            ExpectedType = expectedType,
            Confidence = result.InterpretationConfidence,
            Message = isCorrectType
                ? "✅ Correct capture type detected"
                : $"⚠️ Expected {expectedType}, but detected {result.ProbableCaptureType}"
        };
    }

    public class CaptureValidationResult
    {
        public bool IsValid { get; set; }
        public CaptureType DetectedType { get; set; }
        public CaptureType ExpectedType { get; set; }
        public ConfidenceLevel Confidence { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}

// ==============================================================================
// EXAMPLE 9: Fallback Mechanism Example
// ==============================================================================

public class Example9_FallbackMechanism
{
    public async Task<VisualInterpretationResult> SimulateFallbackAsync(
        IVisualInterpreter visualInterpreter)
    {
        // Simulate a scenario where the model returns non-JSON text
        var request = new VisualInterpretationRequest
        {
            ImagePath = "/uploads/unusual-format.jpg"
        };

        var result = await visualInterpreter.InterpretImageAsync(request);

        // If model returns:
        // "This image shows a nutrition table with values for energy, carbohydrates, 
        // proteins, and fats. I can see text indicating 150 kcal and 30g serving size."

        // Fallback mechanism kicks in:
        // - Detects keywords: "nutrition", "table"
        // - Maps to CaptureType.NutritionTable
        // - Sets InterpretationConfidence = ConfidenceLevel.Low
        // - InterpretationSummary = "Extracted information from unstructured response"

        return result;
    }
}

// ==============================================================================
// EXAMPLE 10: Using Results for Product Identification
// ==============================================================================

public class Example10_ProductIdentification
{
    private readonly IVisualInterpreter _visualInterpreter;

    public Example10_ProductIdentification(IVisualInterpreter visualInterpreter)
    {
        _visualInterpreter = visualInterpreter;
    }

    public async Task<ProductIdentificationCandidate> ExtractProductInfoAsync(string imagePath)
    {
        var request = new VisualInterpretationRequest
        {
            ImagePath = imagePath
        };

        var result = await _visualInterpreter.InterpretImageAsync(request);

        // Use the results to create a product identification candidate
        var candidate = new ProductIdentificationCandidate
        {
            ProductName = result.ProbableProductName,
            Brand = result.ProbableBrand,
            Category = result.ProbableCategory,
            Source = "AzureOpenAIVision",
            Confidence = MapConfidenceToScore(result.InterpretationConfidence)
        };

        return candidate;
    }

    private double MapConfidenceToScore(ConfidenceLevel level)
    {
        return level switch
        {
            ConfidenceLevel.High => 0.85,
            ConfidenceLevel.Medium => 0.55,
            ConfidenceLevel.Low => 0.25,
            _ => 0.0
        };
    }

    public class ProductIdentificationCandidate
    {
        public string? ProductName { get; set; }
        public string? Brand { get; set; }
        public string? Category { get; set; }
        public string Source { get; set; } = string.Empty;
        public double Confidence { get; set; }
    }
}

// ==============================================================================
// EXAMPLE 11: Complete Real-World Scenario
// ==============================================================================

public class Example11_CompleteScenario
{
    private readonly IVisualInterpreter _visualInterpreter;
    private readonly ILogger<Example11_CompleteScenario> _logger;

    public Example11_CompleteScenario(
        IVisualInterpreter visualInterpreter,
        ILogger<Example11_CompleteScenario> logger)
    {
        _visualInterpreter = visualInterpreter;
        _logger = logger;
    }

    public async Task<ProductAnalysisReport> AnalyzeProductImageAsync(string imagePath)
    {
        _logger.LogInformation("Starting product image analysis: {ImagePath}", imagePath);

        var request = new VisualInterpretationRequest
        {
            ImagePath = imagePath
        };

        var result = await _visualInterpreter.InterpretImageAsync(request);

        // Build comprehensive report
        var report = new ProductAnalysisReport
        {
            ImagePath = imagePath,
            AnalysisTimestamp = DateTime.UtcNow,
            ProductName = result.ProbableProductName,
            Brand = result.ProbableBrand,
            Category = result.ProbableCategory,
            CaptureType = result.ProbableCaptureType.ToString(),
            Confidence = result.InterpretationConfidence.ToString(),
            Summary = result.InterpretationSummary,
            IsReliable = result.InterpretationConfidence == ConfidenceLevel.High,
            RecommendedAction = GetRecommendedAction(result)
        };

        _logger.LogInformation(
            "Analysis complete: Product={Product}, Confidence={Confidence}, Action={Action}",
            report.ProductName ?? "unknown",
            report.Confidence,
            report.RecommendedAction);

        return report;
    }

    private string GetRecommendedAction(VisualInterpretationResult result)
    {
        return result.InterpretationConfidence switch
        {
            ConfidenceLevel.High => "✅ Proceed with automatic processing",
            ConfidenceLevel.Medium => "⚠️ Review recommended before proceeding",
            ConfidenceLevel.Low => "❌ Manual verification required",
            _ => "Unknown"
        };
    }

    public class ProductAnalysisReport
    {
        public string ImagePath { get; set; } = string.Empty;
        public DateTime AnalysisTimestamp { get; set; }
        public string? ProductName { get; set; }
        public string? Brand { get; set; }
        public string? Category { get; set; }
        public string CaptureType { get; set; } = string.Empty;
        public string Confidence { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public bool IsReliable { get; set; }
        public string RecommendedAction { get; set; } = string.Empty;
    }
}

// ==============================================================================
// EXPECTED MODEL RESPONSES - QUICK REFERENCE
// ==============================================================================

/*

SCENARIO: Front Packaging (High Confidence)
═══════════════════════════════════════════
Model Response:
{
  "productName": "Biscoito Recheado Chocolate",
  "brand": "Nestlé",
  "category": "biscoito recheado",
  "packageWeight": "140 g",
  "captureType": "FrontPackaging",
  "confidence": 0.92,
  "visibleText": ["Nestlé", "Biscoito Recheado", "Chocolate", "140g"]
}

Mapped Result:
- ProbableProductName: "Biscoito Recheado Chocolate"
- ProbableBrand: "Nestlé"
- ProbableCategory: "biscoito recheado"
- ProbableCaptureType: CaptureType.FrontPackaging
- InterpretationConfidence: ConfidenceLevel.High


SCENARIO: Nutrition Table
═══════════════════════════════════════════
Model Response:
{
  "productName": null,
  "brand": null,
  "category": null,
  "packageWeight": null,
  "captureType": "NutritionTable",
  "confidence": 0.88,
  "visibleText": ["Informação Nutricional", "Porção 30g", "150 kcal"]
}

Mapped Result:
- ProbableProductName: null
- ProbableBrand: null
- ProbableCategory: null
- ProbableCaptureType: CaptureType.NutritionTable
- InterpretationConfidence: ConfidenceLevel.High


SCENARIO: Low Confidence
═══════════════════════════════════════════
Model Response:
{
  "productName": null,
  "brand": null,
  "category": null,
  "packageWeight": null,
  "captureType": "Unknown",
  "confidence": 0.30,
  "visibleText": []
}

Mapped Result:
- ProbableProductName: null
- ProbableBrand: null
- ProbableCategory: null
- ProbableCaptureType: CaptureType.FrontPackaging (default)
- InterpretationConfidence: ConfidenceLevel.Low

*/
