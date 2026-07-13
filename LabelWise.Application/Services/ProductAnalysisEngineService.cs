using System.Collections.Generic;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Rules;
using LabelWise.Domain.Entities;
using LabelWise.Application.DTOs;

namespace LabelWise.Application.Services
{
    public class ProductAnalysisEngineService : IProductAnalysisEngine
    {
        private readonly RulesEngine _engine;

        public ProductAnalysisEngineService(IEnumerable<IRule> rules, IAnalysisSummaryGenerator summaryGenerator)
        {
            _engine = new RulesEngine(rules, summaryGenerator);
        }

        public ProductAnalysisResultDto Analyze(Product product, NutritionalInfo? nutrition, IEnumerable<ProductIngredient> ingredients, IEnumerable<ProductAllergen> allergens, UserProfile? userProfile = null)
        {
            return _engine.Analyze(product, nutrition, ingredients, allergens, userProfile);
        }
    }
}
