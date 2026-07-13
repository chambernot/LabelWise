using System;
using System.Collections.Generic;
using LabelWise.Domain.Common;
using LabelWise.Domain.Enums;

namespace LabelWise.Domain.Entities
{
    public class ProductAnalysis : AuditableEntity
    {
        public Guid ProductId { get; private set; }
        public virtual Product Product { get; private set; } = null!;

        public Guid? UserId { get; private set; }
        public virtual User? User { get; private set; }
        public string? DeviceId { get; private set; }

        public DateTimeOffset AnalyzedAt { get; private set; }

        public AnalysisClassification Classification { get; private set; }
        public ConfidenceLevel Confidence { get; private set; }

        // Free text summary
        public string Summary { get; private set; } = string.Empty;

        public virtual ICollection<AnalysisAlert> Alerts { get; private set; } = new List<AnalysisAlert>();
        public virtual ICollection<AnalysisRecommendation> Recommendations { get; private set; } = new List<AnalysisRecommendation>();

        protected ProductAnalysis() { }

        public ProductAnalysis(Guid productId, Guid? userId, AnalysisClassification classification,
            ConfidenceLevel confidence, string summary, string? deviceId = null)
        {
            ProductId = productId;
            UserId = userId;
            DeviceId = NormalizeDeviceId(deviceId);
            Classification = classification;
            Confidence = confidence;
            Summary = summary ?? string.Empty;
            AnalyzedAt = DateTimeOffset.UtcNow;
        }

        public void AssignDevice(string? deviceId)
        {
            DeviceId = NormalizeDeviceId(deviceId);
            SetUpdated();
        }

        public void AttachProduct(Product product)
        {
            Product = product ?? throw new ArgumentNullException(nameof(product));
            ProductId = product.Id;
        }

        public void AddAlert(AnalysisAlert alert)
        {
            Alerts.Add(alert ?? throw new ArgumentNullException(nameof(alert)));
            SetUpdated();
        }

        public void AddRecommendation(AnalysisRecommendation rec)
        {
            Recommendations.Add(rec ?? throw new ArgumentNullException(nameof(rec)));
            SetUpdated();
        }

        public void UpdateClassification(AnalysisClassification cls, ConfidenceLevel confidence)
        {
            Classification = cls;
            Confidence = confidence;
            SetUpdated();
        }

        public void UpdateSummary(string summary)
        {
            Summary = summary ?? string.Empty;
            SetUpdated();
        }

        private static string? NormalizeDeviceId(string? deviceId)
        {
            return string.IsNullOrWhiteSpace(deviceId)
                ? null
                : deviceId.Trim();
        }
    }
}
