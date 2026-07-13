using System;
using System.Collections.Generic;

namespace LabelWise.Application.DTOs
{
    public class AnalysisHistoryDetailDto
    {
        public Guid Id { get; set; }
        public DateTimeOffset AnalyzedAt { get; set; }
        public string Classification { get; set; } = string.Empty;
        public string ConfidenceLevel { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        
        public ProductDetailsDto Product { get; set; } = new();
        public List<AlertDetailsDto> Alerts { get; set; } = new();
        public List<RecommendationDetailsDto> Recommendations { get; set; } = new();
    }

    public class ProductDetailsDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public string? Barcode { get; set; }
    }

    public class AlertDetailsDto
    {
        public Guid Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Confidence { get; set; } = string.Empty;
    }

    public class RecommendationDetailsDto
    {
        public Guid Id { get; set; }
        public string Recommendation { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public string ExplanationLevel { get; set; } = string.Empty;
    }
}
