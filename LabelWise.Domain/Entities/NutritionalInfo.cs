using System;
using LabelWise.Domain.Common;

namespace LabelWise.Domain.Entities
{
    public class NutritionalInfo : AuditableEntity
    {
        public Guid ProductId { get; private set; }
        public virtual Product Product { get; private set; } = null!;

        // Main nutrients per serving
        public decimal? Calories { get; private set; }
        public decimal? TotalFatGrams { get; private set; }
        public decimal? SaturatedFatGrams { get; private set; }
        public decimal? TransFatGrams { get; private set; }
        public decimal? CholesterolMg { get; private set; }
        public decimal? SodiumMg { get; private set; }
        public decimal? TotalCarbohydratesGrams { get; private set; }
        public decimal? DietaryFiberGrams { get; private set; }
        public decimal? SugarsGrams { get; private set; }
        public decimal? ProteinGrams { get; private set; }

        // Serving info
        public string? ServingSize { get; private set; }
        public decimal? ServingsPerContainer { get; private set; }

        protected NutritionalInfo() { }

        public NutritionalInfo(Guid productId)
        {
            ProductId = productId;
        }

        public void UpdateMacros(decimal? calories = null, decimal? totalFat = null, decimal? satFat = null,
            decimal? transFat = null, decimal? cholesterol = null, decimal? sodium = null,
            decimal? carbs = null, decimal? fiber = null, decimal? sugars = null, decimal? protein = null)
        {
            Calories = calories ?? Calories;
            TotalFatGrams = totalFat ?? TotalFatGrams;
            SaturatedFatGrams = satFat ?? SaturatedFatGrams;
            TransFatGrams = transFat ?? TransFatGrams;
            CholesterolMg = cholesterol ?? CholesterolMg;
            SodiumMg = sodium ?? SodiumMg;
            TotalCarbohydratesGrams = carbs ?? TotalCarbohydratesGrams;
            DietaryFiberGrams = fiber ?? DietaryFiberGrams;
            SugarsGrams = sugars ?? SugarsGrams;
            ProteinGrams = protein ?? ProteinGrams;
            SetUpdated();
        }

        public void UpdateServing(string? size, decimal? perContainer)
        {
            ServingSize = size;
            ServingsPerContainer = perContainer;
            SetUpdated();
        }
    }
}
