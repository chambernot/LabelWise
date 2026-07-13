using System;
using LabelWise.Domain.Common;
using LabelWise.Domain.Enums;

namespace LabelWise.Domain.Entities
{
    public class ProductLabel : AuditableEntity
    {
        public Guid ProductId { get; private set; }
        public virtual Product Product { get; private set; } = null!;

        // Raw OCR text of the label image
        public string OcrText { get; private set; } = string.Empty;

        // Extracted data summary (could be JSON or structured text)
        public string? ExtractedData { get; private set; }

        public DateTimeOffset CapturedAt { get; private set; }

        protected ProductLabel() { }

        public ProductLabel(Guid productId, string ocrText, string? extractedData = null, DateTimeOffset? capturedAt = null)
        {
            ProductId = productId;
            OcrText = ocrText ?? string.Empty;
            ExtractedData = extractedData;
            CapturedAt = capturedAt ?? DateTimeOffset.UtcNow;
        }

        public void UpdateOcrText(string text)
        {
            OcrText = text ?? string.Empty;
            SetUpdated();
        }

        public void UpdateExtractedData(string? data)
        {
            ExtractedData = data;
            SetUpdated();
        }
    }
}
