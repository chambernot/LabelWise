using System.Collections.Generic;
using LabelWise.Application.DTOs;
using LabelWise.Domain.Entities;

namespace LabelWise.Application.Rules
{
    public interface IRule
    {
        void Evaluate(Product product, NutritionalInfo? nutrition, IEnumerable<ProductIngredient> ingredients, IEnumerable<ProductAllergen> allergens, UserProfile? profile, ProductAnalysisResultDto result);
    }
}
