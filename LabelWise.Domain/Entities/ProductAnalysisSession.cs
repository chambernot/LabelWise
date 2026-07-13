using System;
using System.Collections.Generic;
using LabelWise.Domain.Common;
using LabelWise.Domain.Enums;

namespace LabelWise.Domain.Entities
{
    /// <summary>
    /// Status de uma sessão de análise.
    /// </summary>
    public enum SessionStatus
    {
        /// <summary>
        /// Sessão iniciada, aguardando capturas.
        /// </summary>
        Started = 1,

        /// <summary>
        /// Capturas em andamento.
        /// </summary>
        Capturing = 2,

        /// <summary>
        /// Processamento OCR em andamento.
        /// </summary>
        Processing = 3,

        /// <summary>
        /// Análise concluída com sucesso.
        /// </summary>
        Completed = 4,

        /// <summary>
        /// Sessão falhou.
        /// </summary>
        Failed = 5,

        /// <summary>
        /// Sessão cancelada pelo usuário.
        /// </summary>
        Cancelled = 6
    }

    /// <summary>
    /// Representa uma sessão de análise de produto.
    /// Uma sessão agrupa múltiplas capturas de um mesmo produto.
    /// </summary>
    public class ProductAnalysisSession : AuditableEntity
    {
        public Guid? UserId { get; private set; }
        public virtual User? User { get; private set; }

        public Guid? ProductId { get; private set; }
        public virtual Product? Product { get; private set; }

        public Guid? AnalysisId { get; private set; }
        public virtual ProductAnalysis? Analysis { get; private set; }

        public SessionStatus Status { get; private set; }

        public DateTimeOffset StartedAt { get; private set; }

        public DateTimeOffset? CompletedAt { get; private set; }

        /// <summary>
        /// Código de barras identificado durante a sessão.
        /// </summary>
        public string? DetectedBarcode { get; private set; }

        /// <summary>
        /// Indica se o produto foi identificado a partir de cache.
        /// </summary>
        public bool ProductFromCache { get; private set; }

        /// <summary>
        /// Mensagem de erro caso a sessão tenha falhado.
        /// </summary>
        public string? ErrorMessage { get; private set; }

        /// <summary>
        /// Confiança geral da sessão (média das capturas).
        /// </summary>
        public decimal OverallConfidence { get; private set; }

        public virtual ICollection<ProductCapture> Captures { get; private set; } = new List<ProductCapture>();

        protected ProductAnalysisSession() { }

        public ProductAnalysisSession(Guid? userId = null)
        {
            UserId = userId;
            Status = SessionStatus.Started;
            StartedAt = DateTimeOffset.UtcNow;
        }

        public void StartCapturing()
        {
            Status = SessionStatus.Capturing;
            SetUpdated();
        }

        public void StartProcessing()
        {
            Status = SessionStatus.Processing;
            SetUpdated();
        }

        public void Complete(Guid productId, Guid analysisId, decimal overallConfidence)
        {
            ProductId = productId;
            AnalysisId = analysisId;
            OverallConfidence = overallConfidence;
            Status = SessionStatus.Completed;
            CompletedAt = DateTimeOffset.UtcNow;
            SetUpdated();
        }

        public void Fail(string errorMessage)
        {
            ErrorMessage = errorMessage;
            Status = SessionStatus.Failed;
            CompletedAt = DateTimeOffset.UtcNow;
            SetUpdated();
        }

        public void Cancel()
        {
            Status = SessionStatus.Cancelled;
            CompletedAt = DateTimeOffset.UtcNow;
            SetUpdated();
        }

        public void SetDetectedBarcode(string barcode)
        {
            DetectedBarcode = barcode;
            SetUpdated();
        }

        public void MarkProductFromCache()
        {
            ProductFromCache = true;
            SetUpdated();
        }

        public void AssociateProduct(Guid productId)
        {
            ProductId = productId;
            SetUpdated();
        }

        public void AddCapture(ProductCapture capture)
        {
            Captures.Add(capture ?? throw new ArgumentNullException(nameof(capture)));
            SetUpdated();
        }

        public void LoadCaptures(IEnumerable<ProductCapture> captures)
        {
            Captures = captures?.ToList() ?? new List<ProductCapture>();
        }
    }
}
