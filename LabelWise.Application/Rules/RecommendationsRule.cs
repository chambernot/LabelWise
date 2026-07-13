using System.Collections.Generic;
using LabelWise.Application.DTOs;
using LabelWise.Domain.Entities;

namespace LabelWise.Application.Rules
{
    // Simple recommendations based on alerts and scores
    public class RecommendationsRule : IRule
    {
        public void Evaluate(Product product, NutritionalInfo? nutrition, IEnumerable<ProductIngredient> ingredients, IEnumerable<ProductAllergen> allergens, UserProfile? profile, ProductAnalysisResultDto result)
        {
            if (result.Alerts.Count > 0)
            {
                result.Recommendations.Add("⚠️ Leia atentamente a lista de ingredientes e alertas identificados");
            }

            var avgScore = (result.GeneralScore + result.PersonalizedScore) / 2.0;

            if (avgScore < 0.35)
            {
                result.Recommendations.Add("🚫 Evite este produto - não recomendado para seu perfil");
            }
            else if (avgScore < 0.5)
            {
                result.Recommendations.Add("⚠️ Atenção: Este produto deve ser evitado ou consumido raramente");
            }
            else if (avgScore < 0.65)
            {
                result.Recommendations.Add("⚠️ Consumo esporádico: Não recomendado para consumo frequente");
            }
            else if (avgScore < 0.8)
            {
                result.Recommendations.Add("✓ Aceitável com moderação: Monitore porções e frequência");
            }
            else
            {
                result.Recommendations.Add("✓ Produto adequado: Compatível com perfil saudável");
            }

            // Recomendações adicionais baseadas em nutrição
            if (nutrition != null)
            {
                if (nutrition.SugarsGrams.HasValue && nutrition.SugarsGrams.Value >= 15)
                {
                    result.Recommendations.Add("🍬 Alto teor de açúcar - limite o consumo");
                }

                if (nutrition.SodiumMg.HasValue && nutrition.SodiumMg.Value >= 600)
                {
                    result.Recommendations.Add("🧂 Alto teor de sódio - evite se possível");
                }

                if (nutrition.DietaryFiberGrams.HasValue && nutrition.DietaryFiberGrams.Value < 2)
                {
                    result.Recommendations.Add("⚠️ Baixo teor de fibras - complemente com outros alimentos");
                }
            }

            // Recomendações baseadas em alergênicos
            if (allergens?.Any() == true)
            {
                result.Recommendations.Add("⚠️ Produto contém alergênicos declarados - verifique compatibilidade");
            }
        }
    }
}
