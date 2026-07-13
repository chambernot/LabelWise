using LabelWise.Domain.Enums;

namespace LabelWise.Application.DTOs.Nutrition
{
    public class CompareProductsRequest
    {
        public string? DeviceId { get; set; }
        public string? AnalysisIdA { get; set; }
        public string? AnalysisIdB { get; set; }
        public ProductComparisonAnalysisInputDto? ProductAAnalysis { get; set; }
        public ProductComparisonAnalysisInputDto? ProductBAnalysis { get; set; }
    }

    public class ProductComparisonAnalysisInputDto
    {
        public string? AnalysisId { get; set; }
        public string? ProductName { get; set; }
        public string? Brand { get; set; }
        public string? Category { get; set; }
        public AnalysisMode? AnalysisMode { get; set; }
        public List<string> VisibleClaims { get; set; } = new();
        public int? Score { get; set; }
        public string? ScoreLabel { get; set; }
        public string? PrincipalOffender { get; set; }
        public ProductClassificationDto? Classification { get; set; }
        public EstimatedNutritionProfileDto? EstimatedNutritionProfile { get; set; }
        public ConfidenceDetailsDto? ConfidenceDetails { get; set; }
        public string? Summary { get; set; }
    }
}
