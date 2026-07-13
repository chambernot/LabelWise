namespace LabelWise.Application.Models.IngredientAnalysis;

public sealed class IngredientExtractionResult
{
    public string? ProductName { get; set; }
    public string? Brand { get; set; }
    public List<string> IngredientsDetected { get; set; } = new();
    public List<string> Allergens { get; set; } = new();
    public List<string> Claims { get; set; } = new();
    public List<string> RawExtractedText { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed class IngredientAnalysisContext
{
    public IngredientExtractionResult VisionExtraction { get; set; } = new();
    public string? OcrText { get; set; }
    public string? DocumentIntelligenceText { get; set; }
}

public sealed class IngredientDictionaryEntry
{
    public string CanonicalName { get; set; } = string.Empty;
    public List<string> Synonyms { get; set; } = new();
    public string Category { get; set; } = "unknown";
}
