using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Request para salvar uma captura.
    /// </summary>
    public record SaveCaptureRequest(
        Guid SessionId,
        CaptureType CaptureType,
        string ImagePath,
        string OcrProvider,
        string ExtractedText,
        decimal Confidence,
        int ProcessingTimeMs = 0,
        string? ParsedDataJson = null,
        Guid? ProductId = null);

    /// <summary>
    /// Request para consolidar um produto.
    /// </summary>
    public record ConsolidateProductRequest(
        Guid ProductId,
        string? ValidatedName,
        string? ValidatedBrand,
        string? ValidatedBarcode,
        string? ValidatedIngredientsJson,
        string? ValidatedAllergensJson,
        string? ValidatedNutritionalJson,
        ValidationLevel ValidationLevel,
        decimal ValidationConfidence,
        string? ExternalSourceId = null,
        string? ExternalSourceName = null);

    /// <summary>
    /// Serviço para gerenciar capturas e consolidação de produtos.
    /// </summary>
    public interface ICapturePersistenceService
    {
        /// <summary>
        /// Inicia uma nova sessão de análise.
        /// </summary>
        Task<ProductAnalysisSession> StartSessionAsync(
            Guid? userId = null, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Salva uma captura na sessão.
        /// </summary>
        Task<ProductCapture> SaveCaptureAsync(
            SaveCaptureRequest request, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Associa todas as capturas de uma sessão a um produto.
        /// </summary>
        Task AssociateCapturesToProductAsync(
            Guid sessionId, 
            Guid productId, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Completa uma sessão com sucesso.
        /// </summary>
        Task CompleteSessionAsync(
            Guid sessionId, 
            Guid productId, 
            Guid analysisId, 
            decimal overallConfidence, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Marca uma sessão como falha.
        /// </summary>
        Task FailSessionAsync(
            Guid sessionId, 
            string errorMessage, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Consolida um produto com dados validados.
        /// </summary>
        Task<ValidatedProduct> ConsolidateProductAsync(
            ConsolidateProductRequest request, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Obtém todas as capturas de uma sessão.
        /// </summary>
        Task<IReadOnlyList<ProductCapture>> GetSessionCapturesAsync(
            Guid sessionId, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Obtém o histórico de capturas de um produto.
        /// </summary>
        Task<IReadOnlyList<ProductCapture>> GetProductCaptureHistoryAsync(
            Guid productId, 
            CancellationToken cancellationToken = default);
    }
}
