using System;

namespace LabelWise.Application.DTOs
{
    public class AnalysisHistorySummaryDto
    {
        public Guid Id { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public DateTimeOffset AnalyzedAt { get; set; }
        public string Classification { get; set; } = string.Empty;
        public string ConfidenceLevel { get; set; } = string.Empty;
        public int AlertsCount { get; set; }
        public int RecommendationsCount { get; set; }
    }
}
