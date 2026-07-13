using System.Collections.Generic;
using LabelWise.Application.DTOs;
using LabelWise.Domain.Entities;

namespace LabelWise.Application.Interfaces
{
    public interface IProductAnalysisEngine
    {
        ProductAnalysisResultDto Analyze(Product product, NutritionalInfo? nutrition,
            IEnumerable<ProductIngredient> ingredients,
            IEnumerable<ProductAllergen> allergens,
            UserProfile? userProfile = null);
    }
}
