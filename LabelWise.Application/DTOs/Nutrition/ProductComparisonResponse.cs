namespace LabelWise.Application.DTOs.Nutrition
{
    public class ProductComparisonResponse
    {
        public ProductComparisonItem ProductA { get; set; } = new();
        public ProductComparisonItem ProductB { get; set; } = new();
        public string Winner { get; set; } = "tie";
        public string WinnerReason { get; set; } = string.Empty;
        public string ComparisonLevel { get; set; } = string.Empty;
        public ScoreComparisonDto ScoreComparison { get; set; } = new();
        public Dictionary<string, HealthProfileComparisonDto> HealthProfileComparison { get; set; } = new();
        public List<string> KeyDifferences { get; set; } = new();
        public string Recommendation { get; set; } = string.Empty;
        public string ComparativeRecommendation { get; set; } = string.Empty;
        public string AbsoluteRecommendation { get; set; } = string.Empty;
        public double Confidence { get; set; }
    }

    public class ProductComparisonItem
    {
        public string ProductName { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public string? Category { get; set; }
        public int Score { get; set; }
        public string ScoreLabel { get; set; } = string.Empty;
        public string? PrincipalOffender { get; set; }
        public NutritionalScore ScoreDetails { get; set; } = new();
    }

    public class ScoreComparisonDto
    {
        public int ProductA { get; set; }
        public int ProductB { get; set; }
        public int Difference { get; set; }
    }

    public class HealthProfileComparisonDto
    {
        public string Winner { get; set; } = "tie";
        public string Reason { get; set; } = string.Empty;
    }
}
