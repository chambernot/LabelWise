using System.Collections.Generic;
using LabelWise.Application.DTOs;
using LabelWise.Application.Scoring;
using LabelWise.Domain.Entities;

namespace LabelWise.Application.Rules
{
    /// <summary>
    /// Regra de scoring nutricional usando o NutritionalScoringEngine (0-100).
    /// Substitui o sistema antigo de scores 0-1 por scores baseados em pesos reais.
    /// </summary>
    public class NutrientScoringRule : IRule
    {
        private readonly NutritionalScoringEngine _scoringEngine;

        public NutrientScoringRule()
        {
            _scoringEngine = new NutritionalScoringEngine();
        }

        public void Evaluate(Product product, NutritionalInfo? nutrition, IEnumerable<ProductIngredient> ingredients, IEnumerable<ProductAllergen> allergens, UserProfile? profile, ProductAnalysisResultDto result)
        {
            // Calcula scores usando o novo motor (0-100)
            double generalScore = _scoringEngine.CalculateGeneralScore(nutrition, ingredients);
            double personalizedScore = _scoringEngine.CalculatePersonalizedScore(nutrition, ingredients, allergens, profile);

            // Converte para escala 0-1 para compatibilidade com o sistema existente
            result.GeneralScore = generalScore / 100.0;
            result.PersonalizedScore = personalizedScore / 100.0;

            // Armazena scores originais (0-100) em propriedades customizadas se necessário
            // Isso permite debugging e visualização dos scores reais

            // Log do breakdown para debugging (pode ser removido em produção)
            #if DEBUG
            var breakdown = _scoringEngine.GenerateScoreBreakdown(nutrition, ingredients, profile);
            System.Diagnostics.Debug.WriteLine($"=== Score Breakdown para {product.Name} ===");
            System.Diagnostics.Debug.WriteLine(breakdown);
            System.Diagnostics.Debug.WriteLine($"Score Geral: {generalScore:F1}/100 ({result.GeneralScore:P0})");
            System.Diagnostics.Debug.WriteLine($"Score Personalizado: {personalizedScore:F1}/100 ({result.PersonalizedScore:P0})");
            System.Diagnostics.Debug.WriteLine($"Classificação: {_scoringEngine.DetermineClassification(Math.Min(generalScore, personalizedScore))}");
            System.Diagnostics.Debug.WriteLine("===========================================");
            #endif
        }
    }
}
