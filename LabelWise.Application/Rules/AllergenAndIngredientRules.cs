using System;
using System.Collections.Generic;
using System.Linq;
using LabelWise.Application.DTOs;
using LabelWise.Domain.Entities;

namespace LabelWise.Application.Rules
{
    public class AllergenAndIngredientRules : IRule
    {
        private static readonly string[] LactoseKeywords = new[] { "lactose", "milk", "whey", "casein", "caseína", "soro de leite", "soro", "leite" };
        private static readonly string[] GlutenKeywords = new[] { "trigo", "cevada", "centeio", "malte", "gluten", "contém glúten" };
        private static readonly string[] AnimalKeywords = new[] { "milk", "egg", "honey", "cheese", "gelatin", "whey", "casein", "meat", "fish", "chicken", "egg", "butter", "yogurt" };

        public void Evaluate(Product product, NutritionalInfo? nutrition, IEnumerable<ProductIngredient> ingredients, IEnumerable<ProductAllergen> allergens, UserProfile? profile, ProductAnalysisResultDto result)
        {
            var ing = ingredients?.Select(i => i.Name.ToLowerInvariant()).ToList() ?? new List<string>();
            var all = allergens?.Select(a => a.AllergenName.ToLowerInvariant()).ToList() ?? new List<string>();

            // Lactose intolerance
            if (profile != null && profile.LactoseIntolerance)
            {
                foreach (var kw in LactoseKeywords)
                {
                    if (ing.Any(i => i.Contains(kw)) || all.Any(a => a.Contains(kw)))
                    {
                        result.Alerts.Add("Contains lactose or milk-derived ingredients");
                        result.PersonalizedScore = System.Math.Max(0, result.PersonalizedScore - 0.3);
                        break;
                    }
                }
            }

            // Gluten
            if (profile != null && profile.GlutenFree)
            {
                foreach (var kw in GlutenKeywords)
                {
                    if (ing.Any(i => i.Contains(kw)) || all.Any(a => a.Contains(kw)))
                    {
                        result.Alerts.Add("Contains gluten or gluten-derived ingredients");
                        result.PersonalizedScore = System.Math.Max(0, result.PersonalizedScore - 0.35);
                        break;
                    }
                }
            }

            // Diabetes: maltodextrin
            if (profile != null && profile.Diabetes)
            {
                if (ing.Any(i => i.Contains("maltodextrin") || i.Contains("maltodextrina")))
                {
                    result.Alerts.Add("Contains maltodextrin — fast carbohydrate relevant for diabetes control");
                    result.PersonalizedScore = System.Math.Max(0, result.PersonalizedScore - 0.25);
                }
            }

            // Vegan check
            if ((profile != null && profile.Goal == Domain.Enums.GoalType.Vegan) || (profile != null && profile.OtherRestrictions != null && profile.OtherRestrictions.ToLowerInvariant().Contains("vegan")))
            {
                foreach (var kw in AnimalKeywords)
                {
                    if (ing.Any(i => i.Contains(kw)) || all.Any(a => a.Contains(kw)))
                    {
                        result.Alerts.Add("Contains animal-derived ingredients — conflicts with vegan profile");
                        result.PersonalizedScore = System.Math.Max(0, result.PersonalizedScore - 0.4);
                        break;
                    }
                }
            }

            // General allergen declared
            if (all.Any())
            {
                foreach (var a in all)
                {
                    result.Alerts.Add($"Declared allergen: {a}");
                }
            }
        }
    }
}
