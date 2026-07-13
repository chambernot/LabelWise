using System;
using LabelWise.Domain.Common;
using LabelWise.Domain.Enums;

namespace LabelWise.Domain.Entities
{
    public class AnalysisRecommendation : AuditableEntity
    {
        public Guid ProductAnalysisId { get; private set; }
        public virtual ProductAnalysis ProductAnalysis { get; private set; } = null!;

        public string Recommendation { get; private set; } = string.Empty;
        public string? Reason { get; private set; }
        public ExplanationLevel ExplanationLevel { get; private set; }

        protected AnalysisRecommendation() { }

        public AnalysisRecommendation(Guid productAnalysisId, string recommendation, string? reason = null,
            ExplanationLevel explanationLevel = ExplanationLevel.Brief)
        {
            ProductAnalysisId = productAnalysisId;
            Recommendation = recommendation ?? string.Empty;
            Reason = reason;
            ExplanationLevel = explanationLevel;
        }

        public void UpdateRecommendation(string recommendation, string? reason = null)
        {
            Recommendation = recommendation ?? string.Empty;
            Reason = reason;
            SetUpdated();
        }

        public void SetExplanationLevel(ExplanationLevel level)
        {
            ExplanationLevel = level;
            SetUpdated();
        }
    }
}
