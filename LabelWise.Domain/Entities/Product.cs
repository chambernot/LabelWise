using System;
using System.Collections.Generic;
using LabelWise.Domain.Common;

namespace LabelWise.Domain.Entities
{
    public class Product : AuditableEntity
    {
        public string Name { get; private set; } = default!;
        public string? Brand { get; private set; }
        public string? Barcode { get; private set; }

        // Raw OCR content
        public string? NutritionTableRaw { get; private set; }
        public string? IngredientsRaw { get; private set; }

        /// <summary>
        /// Indica se este produto já foi consolidado/validado.
        /// </summary>
        public bool IsConsolidated { get; private set; }

        /// <summary>
        /// Número total de capturas associadas a este produto.
        /// </summary>
        public int TotalCaptureCount { get; private set; }

        // Relations
        public virtual NutritionalInfo? NutritionalInfo { get; private set; }
        public virtual ProductLabel? Label { get; private set; }
        public virtual ValidatedProduct? ValidatedData { get; private set; }
        public virtual ICollection<ProductIngredient> Ingredients { get; private set; } = new List<ProductIngredient>();
        public virtual ICollection<ProductAllergen> Allergens { get; private set; } = new List<ProductAllergen>();
        public virtual ICollection<ProductAnalysis> Analyses { get; private set; } = new List<ProductAnalysis>();
        public virtual ICollection<ProductCapture> Captures { get; private set; } = new List<ProductCapture>();
        public virtual ICollection<ProductAnalysisSession> Sessions { get; private set; } = new List<ProductAnalysisSession>();

        protected Product() { }

        public Product(string name, string? brand = null, string? barcode = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Brand = brand;
            Barcode = barcode;
        }

        public void UpdateName(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            SetUpdated();
        }

        public void UpdateBrand(string? brand)
        {
            Brand = brand;
            SetUpdated();
        }

        public void UpdateBarcode(string? barcode)
        {
            Barcode = barcode;
            SetUpdated();
        }

        public void SetRawNutrition(string? raw)
        {
            NutritionTableRaw = raw;
            SetUpdated();
        }

        public void SetRawIngredients(string? raw)
        {
            IngredientsRaw = raw;
            SetUpdated();
        }

        public void SetNutritionalInfo(NutritionalInfo info)
        {
            NutritionalInfo = info ?? throw new ArgumentNullException(nameof(info));
            SetUpdated();
        }

        public void SetLabel(ProductLabel label)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            SetUpdated();
        }

        public void AddIngredient(ProductIngredient ingredient)
        {
            Ingredients.Add(ingredient ?? throw new ArgumentNullException(nameof(ingredient)));
            SetUpdated();
        }

        public void AddAllergen(ProductAllergen allergen)
        {
            Allergens.Add(allergen ?? throw new ArgumentNullException(nameof(allergen)));
            SetUpdated();
        }

        public void MarkAsConsolidated()
        {
            IsConsolidated = true;
            SetUpdated();
        }

        public void IncrementCaptureCount()
        {
            TotalCaptureCount++;
            SetUpdated();
        }

        public void AddCapture(ProductCapture capture)
        {
            Captures.Add(capture ?? throw new ArgumentNullException(nameof(capture)));
            TotalCaptureCount++;
            SetUpdated();
        }
    }
}
