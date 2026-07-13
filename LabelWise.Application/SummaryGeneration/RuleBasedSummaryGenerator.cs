using LabelWise.Application.DTOs;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LabelWise.Application.SummaryGeneration
{
    /// <summary>
    /// Implementação tradicional baseada em regras para geração de resumo.
    /// Usa lógica determinística baseada em scores e limiares.
    /// </summary>
    public class RuleBasedSummaryGenerator : IAnalysisSummaryGenerator
    {
        public string StrategyName => "RuleBased";

        public string GenerateSummary(
            Product product,
            NutritionalInfo? nutrition,
            IEnumerable<ProductIngredient> ingredients,
            IEnumerable<ProductAllergen> allergens,
            UserProfile? userProfile,
            double generalScore,
            double personalizedScore,
            List<string> alerts,
            List<string> recommendations)
        {
            // Converte para escala 0-100
            var generalScore100 = generalScore * 100.0;
            var personalizedScore100 = personalizedScore * 100.0;
            var minScore = System.Math.Min(generalScore100, personalizedScore100);

            // Classificação baseada em limiares (nova escala)
            string classification;
            string recommendation;

            if (minScore >= 80.0)
            {
                classification = "Excelente Escolha";
                recommendation = "Produto adequado para consumo regular. Perfil nutricional balanceado.";
            }
            else if (minScore >= 60.0)
            {
                classification = "Boa Escolha";
                recommendation = "Pode consumir regularmente com moderação. Verifique porções.";
            }
            else if (minScore >= 40.0)
            {
                classification = "Atenção Necessária";
                recommendation = "Consumir esporadicamente. Considere alternativas mais saudáveis.";
            }
            else
            {
                classification = "Não Recomendado";
                recommendation = "Evitar este produto. Busque opções nutricionalmente superiores.";
            }

            // Constrói o resumo detalhado
            var summaryParts = new List<string>
            {
                $"**{classification}** (Score: {minScore:F0}/100)",
                recommendation
            };

            // Adiciona contexto nutricional se disponível
            if (nutrition != null)
            {
                var nutritionalHighlights = BuildNutritionalHighlights(nutrition, minScore);
                if (!string.IsNullOrEmpty(nutritionalHighlights))
                {
                    summaryParts.Add(nutritionalHighlights);
                }
            }

            // Adiciona alertas importantes
            if (alerts.Any())
            {
                var criticalAlerts = alerts.Where(a => a.Contains("🚨")).ToList();
                if (criticalAlerts.Any())
                {
                    summaryParts.Add($"🚨 {criticalAlerts.Count} alerta(s) crítico(s) identificado(s)");
                }
                else
                {
                    summaryParts.Add($"⚠️ {alerts.Count} alerta(s) identificado(s)");
                }
            }

            // Contexto personalizado
            if (userProfile != null)
            {
                var personalContext = BuildPersonalContext(userProfile, allergens, minScore);
                if (!string.IsNullOrEmpty(personalContext))
                {
                    summaryParts.Add(personalContext);
                }
            }

            return string.Join(" • ", summaryParts);
        }

        private string BuildNutritionalHighlights(NutritionalInfo nutrition, double score)
        {
            var highlights = new List<string>();

            // Destaca valores nutricionais relevantes com linguagem realista

            // Açúcar
            if (nutrition.SugarsGrams.HasValue && nutrition.SugarsGrams > 0)
            {
                if (nutrition.SugarsGrams >= 20)
                    highlights.Add($"Açúcar muito elevado ({nutrition.SugarsGrams}g)");
                else if (nutrition.SugarsGrams >= 15)
                    highlights.Add($"Alto teor de açúcar ({nutrition.SugarsGrams}g)");
                else if (nutrition.SugarsGrams >= 10)
                    highlights.Add($"Açúcar moderado-alto ({nutrition.SugarsGrams}g)");
                else if (nutrition.SugarsGrams <= 3)
                    highlights.Add($"Baixo teor de açúcar ({nutrition.SugarsGrams}g)");
            }

            // Sódio
            if (nutrition.SodiumMg.HasValue && nutrition.SodiumMg > 0)
            {
                if (nutrition.SodiumMg >= 1000)
                    highlights.Add($"Sódio muito elevado ({nutrition.SodiumMg}mg)");
                else if (nutrition.SodiumMg >= 600)
                    highlights.Add($"Alto teor de sódio ({nutrition.SodiumMg}mg)");
                else if (nutrition.SodiumMg <= 200)
                    highlights.Add($"Baixo teor de sódio ({nutrition.SodiumMg}mg)");
            }

            // Fibra
            if (nutrition.DietaryFiberGrams.HasValue && nutrition.DietaryFiberGrams > 0)
            {
                if (nutrition.DietaryFiberGrams >= 7)
                    highlights.Add($"Excelente fonte de fibras ({nutrition.DietaryFiberGrams}g)");
                else if (nutrition.DietaryFiberGrams >= 5)
                    highlights.Add($"Boa fonte de fibras ({nutrition.DietaryFiberGrams}g)");
            }
            else if (!nutrition.DietaryFiberGrams.HasValue || nutrition.DietaryFiberGrams < 2)
            {
                highlights.Add("Baixo teor de fibras");
            }

            // Proteína
            if (nutrition.ProteinGrams.HasValue && nutrition.ProteinGrams > 0)
            {
                if (nutrition.ProteinGrams >= 15)
                    highlights.Add($"Rica em proteína ({nutrition.ProteinGrams}g)");
                else if (nutrition.ProteinGrams >= 10)
                    highlights.Add($"Boa fonte de proteína ({nutrition.ProteinGrams}g)");
            }

            // Gordura saturada
            if (nutrition.SaturatedFatGrams.HasValue && nutrition.SaturatedFatGrams >= 5)
            {
                highlights.Add($"Alto teor de gordura saturada ({nutrition.SaturatedFatGrams}g)");
            }

            // Gordura trans
            if (nutrition.TransFatGrams.HasValue && nutrition.TransFatGrams > 0)
            {
                highlights.Add("CONTÉM GORDURA TRANS");
            }

            // Calorias
            if (nutrition.Calories > 0)
            {
                if (nutrition.Calories > 400)
                    highlights.Add($"Alto valor calórico ({nutrition.Calories} kcal)");
                else if (nutrition.Calories < 100)
                    highlights.Add($"Baixo valor calórico ({nutrition.Calories} kcal)");
            }

            return highlights.Any() ? string.Join(", ", highlights) : string.Empty;
        }

        private string BuildPersonalContext(UserProfile userProfile, IEnumerable<ProductAllergen> allergens, double score)
        {
            var contexts = new List<string>();

            // Verifica restrições do perfil
            var hasRestrictions = userProfile.LactoseIntolerance || userProfile.GlutenFree || 
                                 userProfile.Diabetes || userProfile.SodiumControl;

            if (hasRestrictions)
            {
                var restrictions = new List<string>();
                if (userProfile.LactoseIntolerance) restrictions.Add("intolerância à lactose");
                if (userProfile.GlutenFree) restrictions.Add("dieta sem glúten");
                if (userProfile.Diabetes) restrictions.Add("diabetes");
                if (userProfile.SodiumControl) restrictions.Add("controle de sódio");

                contexts.Add($"Perfil: {string.Join(", ", restrictions)}");

                // Se score é baixo e há restrições, reforça o alerta
                if (score < 40.0)
                {
                    contexts.Add("⚠️ Este produto pode não ser adequado para seu perfil");
                }
            }

            // Contexto de objetivo
            var goalDescription = userProfile.Goal.ToString();
            contexts.Add($"Objetivo: {goalDescription}");

            return contexts.Any() ? string.Join(" • ", contexts) : string.Empty;
        }
    }
}
