using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LabelWise.Application.DTOs;
using LabelWise.Domain.Entities;

namespace LabelWise.Application.Rules
{
    /// <summary>
    /// Regra específica para adicionar alertas sobre produtos ultraprocessados.
    /// O scoring já é feito pelo NutritionalScoringEngine, esta regra foca em alertas contextuais.
    /// </summary>
    public class UltraProcessedProductRule : IRule
    {
        private static readonly string[] HydrogenatedFatKeywords = new[]
        {
            "gordura vegetal hidrogenada", "gordura hidrogenada", "óleo hidrogenado",
            "parcialmente hidrogenada", "gordura vegetal parcialmente"
        };

        private static readonly string[] ArtificialAdditivesKeywords = new[]
        {
            "aromatizante", "corante", "emulsificante", "espessante", "estabilizante",
            "realçador de sabor", "conservante", "acidulante", "umectante",
            "antioxidante artificial", "corante artificial", "aroma artificial"
        };

        private static readonly string[] HighGlycemicKeywords = new[]
        {
            "xarope de glicose", "xarope de milho", "maltodextrina", "dextrose",
            "açúcar invertido", "glucose", "frutose", "sacarose"
        };

        public void Evaluate(Product product, NutritionalInfo? nutrition, IEnumerable<ProductIngredient> ingredients, IEnumerable<ProductAllergen> allergens, UserProfile? profile, ProductAnalysisResultDto result)
        {
            var ingredientList = ingredients?.Select(i => i.Name.ToLowerInvariant()).ToList() ?? new List<string>();
            int ultraProcessedScore = 0; // Contador de características ultraprocessadas

            // 1. Verificar presença de gordura hidrogenada (CRÍTICO)
            bool hasHydrogenatedFat = HasAnyKeyword(ingredientList, HydrogenatedFatKeywords);
            if (hasHydrogenatedFat)
            {
                ultraProcessedScore += 3; // Peso alto
                result.Alerts.Add("🚨 Contém gordura hidrogenada - associada a riscos cardiovasculares");
            }

            // 2. Verificar múltiplos aditivos químicos
            int additiveCount = CountKeywords(ingredientList, ArtificialAdditivesKeywords);
            if (additiveCount >= 5)
            {
                ultraProcessedScore += 3;
                result.Alerts.Add($"🚨 Produto altamente processado: {additiveCount} tipos de aditivos químicos");
            }
            else if (additiveCount >= 3)
            {
                ultraProcessedScore += 2;
                result.Alerts.Add($"⚠️ Contém {additiveCount} tipos de aditivos químicos");
            }
            else if (additiveCount > 0)
            {
                ultraProcessedScore += 1;
                result.Alerts.Add($"ℹ️ Contém {additiveCount} aditivo(s) químico(s)");
            }

            // 3. Verificar açúcares de alto índice glicêmico
            var highGlycemicIngredients = ingredientList.Where(i => HighGlycemicKeywords.Any(kw => i.Contains(kw))).ToList();
            if (highGlycemicIngredients.Any())
            {
                ultraProcessedScore += 2;
                var examples = string.Join(", ", highGlycemicIngredients.Take(3));
                result.Alerts.Add($"⚠️ Contém açúcares de alto índice glicêmico: {examples}");

                if (profile != null && (profile.Diabetes || profile.Goal == Domain.Enums.GoalType.DiabeticFriendly))
                {
                    result.Alerts.Add("🚨 ATENÇÃO DIABÉTICOS: Este produto contém ingredientes de alto índice glicêmico");
                }
            }

            // 4. Nutrição: Alto açúcar + Baixa fibra (combo ruim)
            if (nutrition != null)
            {
                bool highSugar = nutrition.SugarsGrams.HasValue && nutrition.SugarsGrams.Value >= 10;
                bool lowFiber = !nutrition.DietaryFiberGrams.HasValue || nutrition.DietaryFiberGrams.Value < 2;

                if (highSugar && lowFiber)
                {
                    ultraProcessedScore += 2;
                    result.Alerts.Add("⚠️ Alto teor de açúcar combinado com baixa fibra (perfil ultraprocessado)");
                }

                // Açúcar muito alto (>20g)
                if (nutrition.SugarsGrams.HasValue && nutrition.SugarsGrams.Value >= 20)
                {
                    ultraProcessedScore += 1;
                    result.Alerts.Add($"🚨 Teor de açúcar muito elevado: {nutrition.SugarsGrams.Value}g por porção");
                }

                // Gordura trans detectada
                if (nutrition.TransFatGrams.HasValue && nutrition.TransFatGrams.Value > 0)
                {
                    ultraProcessedScore += 3;
                    result.Alerts.Add("🚨 CONTÉM GORDURA TRANS - Evite este produto!");
                }

                // Sódio muito alto
                if (nutrition.SodiumMg.HasValue && nutrition.SodiumMg.Value >= 1000)
                {
                    result.Alerts.Add($"🚨 Sódio muito elevado: {nutrition.SodiumMg.Value}mg por porção");
                    if (profile != null && profile.SodiumControl)
                    {
                        result.Alerts.Add("🚨 ATENÇÃO HIPERTENSOS: Este produto é inadequado para controle de pressão arterial");
                    }
                }
            }

            // 5. Muitos ingredientes (>15 ingredientes = ultraprocessado)
            if (ingredientList.Count > 20)
            {
                ultraProcessedScore += 2;
                result.Alerts.Add($"🚨 Lista muito extensa de ingredientes ({ingredientList.Count} itens) - característico de ultraprocessados");
            }
            else if (ingredientList.Count > 15)
            {
                ultraProcessedScore += 1;
                result.Alerts.Add($"⚠️ Lista extensa de ingredientes ({ingredientList.Count} itens) - indicador de processamento");
            }

            // 6. Classificação final baseada no score de ultraprocessamento
            if (ultraProcessedScore >= 8)
            {
                result.Alerts.Add("🚨 PRODUTO ULTRAPROCESSADO (Grau 4 - NOVA) - Evitar consumo regular");
                result.Recommendations.Add("Prefira alimentos in natura ou minimamente processados");
                result.Recommendations.Add("Consulte as informações nutricionais e lista de ingredientes");
            }
            else if (ultraProcessedScore >= 5)
            {
                result.Alerts.Add("⚠️ Produto ultraprocessado - Consumir esporadicamente");
                result.Recommendations.Add("Limite o consumo a ocasiões especiais");
            }
            else if (ultraProcessedScore >= 3)
            {
                result.Alerts.Add("⚠️ Produto processado - Consumo moderado recomendado");
                result.Recommendations.Add("Prefira versões com menos aditivos");
            }

            // Adiciona contexto educativo se houver muitos problemas
            if (ultraProcessedScore >= 5)
            {
                result.Recommendations.Add("Ultraprocessados podem contribuir para obesidade, diabetes e doenças cardiovasculares");
            }
        }

        private bool HasAnyKeyword(List<string> ingredients, string[] keywords)
        {
            return keywords.Any(kw => ingredients.Any(ing => ing.Contains(kw)));
        }

        private int CountKeywords(List<string> ingredients, string[] keywords)
        {
            return keywords.Count(kw => ingredients.Any(ing => ing.Contains(kw)));
        }
    }
}
