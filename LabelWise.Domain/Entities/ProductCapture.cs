using System;
using LabelWise.Domain.Common;
using LabelWise.Domain.Enums;

namespace LabelWise.Domain.Entities
{
    /// <summary>
    /// Representa uma captura individual de imagem de um produto.
    /// Cada captura contém o resultado do OCR e metadados da extração.
    /// </summary>
    public class ProductCapture : AuditableEntity
    {
        public Guid SessionId { get; private set; }
        public virtual ProductAnalysisSession Session { get; private set; } = null!;

        public Guid? ProductId { get; private set; }
        public virtual Product? Product { get; private set; }

        public CaptureType CaptureType { get; private set; }

        public string ImagePath { get; private set; } = string.Empty;

        public string OcrProvider { get; private set; } = string.Empty;

        public string ExtractedText { get; private set; } = string.Empty;

        public decimal Confidence { get; private set; }

        public DateTimeOffset CapturedAt { get; private set; }

        /// <summary>
        /// Duração do processamento OCR em milissegundos.
        /// </summary>
        public int ProcessingTimeMs { get; private set; }

        /// <summary>
        /// JSON com dados estruturados extraídos (ingredientes, nutrientes, etc.).
        /// </summary>
        public string? ParsedDataJson { get; private set; }

        /// <summary>
        /// Indica se esta captura foi validada manualmente ou por IA.
        /// </summary>
        public bool IsValidated { get; private set; }

        /// <summary>
        /// Indica se esta captura foi usada para consolidar o produto.
        /// </summary>
        public bool UsedForConsolidation { get; private set; }

        protected ProductCapture() { }

        public ProductCapture(
            Guid sessionId,
            CaptureType captureType,
            string imagePath,
            string ocrProvider,
            string extractedText,
            decimal confidence,
            int processingTimeMs = 0,
            Guid? productId = null)
        {
            SessionId = sessionId;
            CaptureType = captureType;
            ImagePath = imagePath ?? throw new ArgumentNullException(nameof(imagePath));
            OcrProvider = ocrProvider ?? throw new ArgumentNullException(nameof(ocrProvider));
            ExtractedText = extractedText ?? string.Empty;
            Confidence = confidence;
            ProcessingTimeMs = processingTimeMs;
            ProductId = productId;
            CapturedAt = DateTimeOffset.UtcNow;
        }

        public void AssociateWithProduct(Guid productId)
        {
            ProductId = productId;
            SetUpdated();
        }

        public void SetParsedData(string? json)
        {
            ParsedDataJson = json;
            SetUpdated();
        }

        public void MarkAsValidated()
        {
            IsValidated = true;
            SetUpdated();
        }

        public void MarkAsUsedForConsolidation()
        {
            UsedForConsolidation = true;
            SetUpdated();
        }

        public void UpdateConfidence(decimal confidence)
        {
            Confidence = confidence;
            SetUpdated();
        }
    }
}
