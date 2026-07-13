using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;

namespace LabelWise.Infrastructure.Services
{
    public class CapturePersistenceService : ICapturePersistenceService
    {
        private readonly IProductCaptureRepository _captureRepository;
        private readonly IProductAnalysisSessionRepository _sessionRepository;
        private readonly IValidatedProductRepository _validatedProductRepository;
        private readonly IProductRepository _productRepository;

        public CapturePersistenceService(
            IProductCaptureRepository captureRepository,
            IProductAnalysisSessionRepository sessionRepository,
            IValidatedProductRepository validatedProductRepository,
            IProductRepository productRepository)
        {
            _captureRepository = captureRepository;
            _sessionRepository = sessionRepository;
            _validatedProductRepository = validatedProductRepository;
            _productRepository = productRepository;
        }

        public async Task<ProductAnalysisSession> StartSessionAsync(
            Guid? userId = null, 
            CancellationToken cancellationToken = default)
        {
            var session = new ProductAnalysisSession(userId);
            await _sessionRepository.AddAsync(session, cancellationToken);
            return session;
        }

        public async Task<ProductCapture> SaveCaptureAsync(
            SaveCaptureRequest request, 
            CancellationToken cancellationToken = default)
        {
            var capture = new ProductCapture(
                request.SessionId,
                request.CaptureType,
                request.ImagePath,
                request.OcrProvider,
                request.ExtractedText,
                request.Confidence,
                request.ProcessingTimeMs,
                request.ProductId);

            if (!string.IsNullOrEmpty(request.ParsedDataJson))
            {
                capture.SetParsedData(request.ParsedDataJson);
            }

            await _captureRepository.AddAsync(capture, cancellationToken);

            // Update session status
            var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
            if (session is not null && session.Status == SessionStatus.Started)
            {
                session.StartCapturing();
                await _sessionRepository.UpdateAsync(session, cancellationToken);
            }

            return capture;
        }

        public async Task AssociateCapturesToProductAsync(
            Guid sessionId, 
            Guid productId, 
            CancellationToken cancellationToken = default)
        {
            var captures = await _captureRepository.GetBySessionIdAsync(sessionId, cancellationToken);
            var product = await _productRepository.GetByIdAsync(productId, cancellationToken);

            foreach (var capture in captures)
            {
                capture.AssociateWithProduct(productId);
                await _captureRepository.UpdateAsync(capture, cancellationToken);
            }

            if (product is not null)
            {
                foreach (var _ in captures)
                {
                    product.IncrementCaptureCount();
                }
                await _productRepository.UpdateAsync(product, cancellationToken);
            }
        }

        public async Task CompleteSessionAsync(
            Guid sessionId, 
            Guid productId, 
            Guid analysisId, 
            decimal overallConfidence, 
            CancellationToken cancellationToken = default)
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId, cancellationToken);
            if (session is not null)
            {
                session.Complete(productId, analysisId, overallConfidence);
                await _sessionRepository.UpdateAsync(session, cancellationToken);
            }
        }

        public async Task FailSessionAsync(
            Guid sessionId, 
            string errorMessage, 
            CancellationToken cancellationToken = default)
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId, cancellationToken);
            if (session is not null)
            {
                session.Fail(errorMessage);
                await _sessionRepository.UpdateAsync(session, cancellationToken);
            }
        }

        public async Task<ValidatedProduct> ConsolidateProductAsync(
            ConsolidateProductRequest request, 
            CancellationToken cancellationToken = default)
        {
            var existingValidated = await _validatedProductRepository
                .GetByProductIdAsync(request.ProductId, cancellationToken);

            if (existingValidated is not null)
            {
                existingValidated.UpdateValidatedData(
                    request.ValidatedName,
                    request.ValidatedBrand,
                    request.ValidatedBarcode,
                    request.ValidationLevel,
                    request.ValidationConfidence);

                existingValidated.SetValidatedIngredients(request.ValidatedIngredientsJson);
                existingValidated.SetValidatedAllergens(request.ValidatedAllergensJson);
                existingValidated.SetValidatedNutritional(request.ValidatedNutritionalJson);

                if (!string.IsNullOrEmpty(request.ExternalSourceId))
                {
                    existingValidated.SetExternalSource(
                        request.ExternalSourceId, 
                        request.ExternalSourceName ?? "Unknown");
                }

                existingValidated.IncrementCaptureCount();
                await _validatedProductRepository.UpdateAsync(existingValidated, cancellationToken);

                return existingValidated;
            }

            var validatedProduct = new ValidatedProduct(
                request.ProductId,
                request.ValidatedName,
                request.ValidatedBrand,
                request.ValidatedBarcode,
                request.ValidationLevel,
                request.ValidationConfidence);

            validatedProduct.SetValidatedIngredients(request.ValidatedIngredientsJson);
            validatedProduct.SetValidatedAllergens(request.ValidatedAllergensJson);
            validatedProduct.SetValidatedNutritional(request.ValidatedNutritionalJson);

            if (!string.IsNullOrEmpty(request.ExternalSourceId))
            {
                validatedProduct.SetExternalSource(
                    request.ExternalSourceId, 
                    request.ExternalSourceName ?? "Unknown");
            }

            await _validatedProductRepository.AddAsync(validatedProduct, cancellationToken);

            // Mark product as consolidated
            var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
            if (product is not null)
            {
                product.MarkAsConsolidated();
                await _productRepository.UpdateAsync(product, cancellationToken);
            }

            return validatedProduct;
        }

        public async Task<IReadOnlyList<ProductCapture>> GetSessionCapturesAsync(
            Guid sessionId, 
            CancellationToken cancellationToken = default)
        {
            return await _captureRepository.GetBySessionIdAsync(sessionId, cancellationToken);
        }

        public async Task<IReadOnlyList<ProductCapture>> GetProductCaptureHistoryAsync(
            Guid productId, 
            CancellationToken cancellationToken = default)
        {
            return await _captureRepository.GetByProductIdAsync(productId, cancellationToken);
        }
    }
}
