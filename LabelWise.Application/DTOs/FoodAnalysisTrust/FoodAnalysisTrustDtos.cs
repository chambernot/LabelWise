namespace LabelWise.Application.DTOs.FoodAnalysisTrust;

public sealed class FoodAnalysisTrustInput
{
    public string Source { get; set; } = "unknown";
    public string OcrConfidence { get; set; } = "low";
    public bool BlurDetected { get; set; }
    public bool ReflectionDetected { get; set; }
    public bool CroppedTable { get; set; }
    public bool PartialRead { get; set; } = true;
    public bool TablePartiallyObstructed { get; set; }
    public bool IngredientRegionDetected { get; set; }
    public bool NutritionRegionDetected { get; set; }
    public bool RegulatoryClaimRegionDetected { get; set; }
    public bool IngredientCompletenessLow { get; set; }
    public bool TableCompletenessLow { get; set; }
    public bool ParsingBroken { get; set; }
    public bool BoundaryLeakDetected { get; set; }
    public bool DuplicatedOcr { get; set; }
    public int TextConsistencyConflicts { get; set; }
    public int IngredientConsistencyConflicts { get; set; }
    public int SemanticConflictCount { get; set; }
    public int InferredDataCount { get; set; }
    public int ExplicitClaimCount { get; set; }
    public int OcrCorrectionCount { get; set; }
    public int IngredientCount { get; set; }
    public int LowConfidenceIngredientCount { get; set; }
    public int NutritionFieldCount { get; set; }
    public Dictionary<string, double?> NutritionValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Warnings { get; set; } = new();
}

public sealed class FoodAnalysisTrustReport
{
    public int AnalysisTrustScore { get; set; }
    public string TrustLevel { get; set; } = "low";
    public bool SafeToConclude { get; set; }
    public bool SafeToRecommend { get; set; }
    public bool SafeToScore { get; set; }
    public string AnalysisMode { get; set; } = "partial";
    public bool SafeModeRequired { get; set; } = true;
    public List<string> Reasons { get; set; } = new();
    public List<string> QualityGateFailures { get; set; } = new();
    public List<FoodAnalysisTrustSignalDto> Signals { get; set; } = new();
}

public sealed class FoodAnalysisTrustSignalDto
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Source { get; set; } = "backend";
    public string DetectionType { get; set; } = "confirmed";
}
