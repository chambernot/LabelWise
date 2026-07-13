using System;
using System.Threading;
using System.Threading.Tasks;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;

namespace LabelWise.Infrastructure.Services
{
    public class ProductCacheService : IProductCacheService
    {
        private readonly IValidatedProductRepository _validatedProductRepository;

        // Período de validade do cache em dias
        private const int CacheValidityDays = 30;

        public ProductCacheService(IValidatedProductRepository validatedProductRepository)
        {
            _validatedProductRepository = validatedProductRepository;
        }

        public async Task<CachedProductResult?> GetByBarcodeAsync(
            string barcode, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return null;

            var validatedProduct = await _validatedProductRepository
                .GetByBarcodeAsync(barcode, cancellationToken);

            if (validatedProduct is null)
                return null;

            var requiresRevalidation = RequiresRevalidation(validatedProduct);

            return new CachedProductResult(
                validatedProduct.Product,
                validatedProduct,
                requiresRevalidation);
        }

        public async Task<bool> IsCachedAndValidAsync(
            string barcode, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return false;

            var validatedProduct = await _validatedProductRepository
                .GetByBarcodeAsync(barcode, cancellationToken);

            if (validatedProduct is null)
                return false;

            return !RequiresRevalidation(validatedProduct);
        }

        public async Task CacheProductAsync(
            Product product, 
            ValidatedProduct validatedData, 
            CancellationToken cancellationToken = default)
        {
            var existing = await _validatedProductRepository
                .GetByProductIdAsync(product.Id, cancellationToken);

            if (existing is not null)
            {
                existing.AttachProduct(product);
                existing.UpdateValidatedData(
                    validatedData.ValidatedName,
                    validatedData.ValidatedBrand,
                    validatedData.ValidatedBarcode,
                    validatedData.ValidationLevel,
                    validatedData.ValidationConfidence);

                existing.SetValidatedIngredients(validatedData.ValidatedIngredientsJson);
                existing.SetValidatedAllergens(validatedData.ValidatedAllergensJson);
                existing.SetValidatedNutritional(validatedData.ValidatedNutritionalJson);

                await _validatedProductRepository.UpdateAsync(existing, cancellationToken);
            }
            else
            {
                validatedData.AttachProduct(product);
                await _validatedProductRepository.AddAsync(validatedData, cancellationToken);
            }
        }

        public async Task IncrementReuseCountAsync(
            Guid productId, 
            CancellationToken cancellationToken = default)
        {
            var validatedProduct = await _validatedProductRepository
                .GetByProductIdAsync(productId, cancellationToken);

            if (validatedProduct is not null)
            {
                validatedProduct.IncrementReuseCount();
                await _validatedProductRepository.UpdateAsync(validatedProduct, cancellationToken);
            }
        }

        public async Task InvalidateCacheAsync(
            Guid productId, 
            CancellationToken cancellationToken = default)
        {
            var validatedProduct = await _validatedProductRepository
                .GetByProductIdAsync(productId, cancellationToken);

            if (validatedProduct is not null)
            {
                await _validatedProductRepository.DeleteAsync(validatedProduct.Id, cancellationToken);
            }
        }

        private static bool RequiresRevalidation(ValidatedProduct validatedProduct)
        {
            // Check if cache is expired
            var cacheAge = DateTimeOffset.UtcNow - validatedProduct.LastValidatedAt;
            if (cacheAge.TotalDays > CacheValidityDays)
                return true;

            // Check validation level - lower levels may need revalidation
            if (validatedProduct.ValidationLevel == ValidationLevel.None)
                return true;

            // Check confidence threshold
            if (validatedProduct.ValidationConfidence < 0.7m)
                return true;

            return false;
        }
    }
}
