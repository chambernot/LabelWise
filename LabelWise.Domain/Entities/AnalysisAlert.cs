using System;
using LabelWise.Domain.Common;
using LabelWise.Domain.Enums;

namespace LabelWise.Domain.Entities
{
    public class AnalysisAlert : AuditableEntity
    {
        public Guid ProductAnalysisId { get; private set; }
        public virtual ProductAnalysis ProductAnalysis { get; private set; } = null!;

        public string Message { get; private set; } = string.Empty;
        public AnalysisClassification Severity { get; private set; }
        public ConfidenceLevel Confidence { get; private set; }

        protected AnalysisAlert() { }

        public AnalysisAlert(Guid productAnalysisId, string message, AnalysisClassification severity, ConfidenceLevel confidence)
        {
            ProductAnalysisId = productAnalysisId;
            Message = message ?? string.Empty;
            Severity = severity;
            Confidence = confidence;
        }

        public void UpdateMessage(string message)
        {
            Message = message ?? string.Empty;
            SetUpdated();
        }

        public void UpdateSeverity(AnalysisClassification severity, ConfidenceLevel confidence)
        {
            Severity = severity;
            Confidence = confidence;
            SetUpdated();
        }
    }
}
