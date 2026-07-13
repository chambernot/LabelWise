using System;
using System.Collections.Generic;
using LabelWise.Domain.Common;

namespace LabelWise.Domain.Entities
{
    /// <summary>
    /// Nível de validação de um produto.
    /// </summary>
    public enum ValidationLevel
    {
        /// <summary>
        /// Não validado - dados brutos do OCR.
        /// </summary>
        None = 0,

        /// <summary>
        /// Validação automática por IA.
        /// </summary>
        AutoValidated = 1,

        /// <summary>
        /// Validação por múltiplas capturas consistentes.
        /// </summary>
        MultiCaptureValidated = 2,

        /// <summary>
        /// Validação manual por usuário.
        /// </summary>
        ManuallyValidated = 3,

        /// <summary>
        /// Validação por fonte externa confiável (ex: Open Food Facts).
        /// </summary>
        ExternalSourceValidated = 4
    }

    /// <summary>
    /// Representa um produto consolidado e validado.
    /// Usado para cache e reutilização de produtos conhecidos.
    /// </summary>
    public class ValidatedProduct : AuditableEntity
    {
        public Guid ProductId { get; private set; }
        public virtual Product Product { get; private set; } = null!;

        /// <summary>
        /// ID do usuário que validou/criou este produto.
        /// </summary>
        public int? ValidatedByUserId { get; private set; }

        /// <summary>
        /// Categoria validada do produto.
        /// </summary>
        public string? ValidatedCategory { get; private set; }

        public string? ValidatedName { get; private set; }
        public string? ValidatedBrand { get; private set; }
        public string? ValidatedBarcode { get; private set; }

        /// <summary>
        /// JSON com ingredientes validados.
        /// </summary>
        public string? ValidatedIngredientsJson { get; private set; }

        /// <summary>
        /// JSON com alérgenos validados.
        /// </summary>
        public string? ValidatedAllergensJson { get; private set; }

        /// <summary>
        /// JSON com informações nutricionais validadas.
        /// </summary>
        public string? ValidatedNutritionalJson { get; private set; }

        public ValidationLevel ValidationLevel { get; private set; }

        /// <summary>
        /// Confiança geral dos dados validados (0-1).
        /// </summary>
        public decimal ValidationConfidence { get; private set; }

        /// <summary>
        /// Número de capturas usadas para consolidação.
        /// </summary>
        public int CaptureCount { get; private set; }

        /// <summary>
        /// Data da última validação/consolidação.
        /// </summary>
        public DateTimeOffset LastValidatedAt { get; private set; }

        /// <summary>
        /// Número de vezes que este produto validado foi reutilizado.
        /// </summary>
        public int ReuseCount { get; private set; }

        /// <summary>
        /// ID da fonte externa (ex: Open Food Facts).
        /// </summary>
        public string? ExternalSourceId { get; private set; }

        /// <summary>
        /// Nome da fonte externa.
        /// </summary>
        public string? ExternalSourceName { get; private set; }

        /// <summary>
        /// Hash dos dados validados para detecção de mudanças.
        /// </summary>
        public string? DataHash { get; private set; }

        protected ValidatedProduct() { }

        public ValidatedProduct(
            Guid productId,
            string? validatedName,
            string? validatedBrand,
            string? validatedBarcode,
            ValidationLevel validationLevel,
            decimal validationConfidence)
        {
            ProductId = productId;
            ValidatedName = validatedName;
            ValidatedBrand = validatedBrand;
            ValidatedBarcode = validatedBarcode;
            ValidationLevel = validationLevel;
            ValidationConfidence = validationConfidence;
            CaptureCount = 1;
            LastValidatedAt = DateTimeOffset.UtcNow;
        }

        public void UpdateValidatedData(
            string? name,
            string? brand,
            string? barcode,
            ValidationLevel level,
            decimal confidence)
        {
            ValidatedName = name;
            ValidatedBrand = brand;
            ValidatedBarcode = barcode;
            ValidationLevel = level;
            ValidationConfidence = confidence;
            LastValidatedAt = DateTimeOffset.UtcNow;
            SetUpdated();
        }

        public void SetValidatedIngredients(string? json)
        {
            ValidatedIngredientsJson = json;
            LastValidatedAt = DateTimeOffset.UtcNow;
            SetUpdated();
        }

        public void SetValidatedAllergens(string? json)
        {
            ValidatedAllergensJson = json;
            LastValidatedAt = DateTimeOffset.UtcNow;
            SetUpdated();
        }

        public void SetValidatedNutritional(string? json)
        {
            ValidatedNutritionalJson = json;
            LastValidatedAt = DateTimeOffset.UtcNow;
            SetUpdated();
        }

        public void SetExternalSource(string sourceId, string sourceName)
        {
            ExternalSourceId = sourceId;
            ExternalSourceName = sourceName;
            ValidationLevel = ValidationLevel.ExternalSourceValidated;
            SetUpdated();
        }

        public void IncrementCaptureCount()
        {
            CaptureCount++;
            SetUpdated();
        }

        public void IncrementReuseCount()
        {
            ReuseCount++;
            SetUpdated();
        }

        public void UpdateDataHash(string hash)
        {
            DataHash = hash;
            SetUpdated();
        }

        public void AttachProduct(Product product)
        {
            Product = product ?? throw new ArgumentNullException(nameof(product));
            ProductId = product.Id;
        }
    }
}
