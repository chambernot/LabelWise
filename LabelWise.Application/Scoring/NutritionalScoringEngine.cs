using System;
using System.Collections.Generic;
using System.Linq;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Scoring
{
    /// <summary>
    /// Motor de score nutricional baseado em pesos reais (0 a 100).
    /// Implementa regras nutricionais baseadas em evidências científicas.
    /// </summary>
    public class NutritionalScoringEngine
    {
        // Pesos das categorias (soma = 100)
        private const double WEIGHT_SUGAR = 25.0;
        private const double WEIGHT_BAD_FAT = 20.0;
        private const double WEIGHT_FIBER = 15.0;
        private const double WEIGHT_PROTEIN = 10.0;
        private const double WEIGHT_SODIUM = 10.0;
        private const double WEIGHT_ULTRA_PROCESSED = 10.0;
        private const double WEIGHT_ADDITIVES = 10.0;

        /// <summary>
        /// Calcula o score nutricional geral (0-100) baseado em dados nutricionais e ingredientes.
        /// </summary>
        public double CalculateGeneralScore(
            NutritionalInfo? nutrition,
            IEnumerable<ProductIngredient> ingredients)
        {
            if (nutrition == null)
                return 50.0; // Score neutro se não há dados

            double totalScore = 0.0;

            // 1. Score de Açúcar (0-25 pontos)
            totalScore += CalculateSugarScore(nutrition) * WEIGHT_SUGAR / 100.0;

            // 2. Score de Gordura Ruim (0-20 pontos)
            totalScore += CalculateBadFatScore(nutrition, ingredients) * WEIGHT_BAD_FAT / 100.0;

            // 3. Score de Fibra (0-15 pontos)
            totalScore += CalculateFiberScore(nutrition) * WEIGHT_FIBER / 100.0;

            // 4. Score de Proteína (0-10 pontos)
            totalScore += CalculateProteinScore(nutrition) * WEIGHT_PROTEIN / 100.0;

            // 5. Score de Sódio (0-10 pontos)
            totalScore += CalculateSodiumScore(nutrition) * WEIGHT_SODIUM / 100.0;

            // 6. Score de Ultraprocessamento (0-10 pontos)
            totalScore += CalculateUltraProcessedScore(nutrition, ingredients) * WEIGHT_ULTRA_PROCESSED / 100.0;

            // 7. Score de Aditivos (0-10 pontos)
            totalScore += CalculateAdditiveScore(ingredients) * WEIGHT_ADDITIVES / 100.0;

            return Math.Max(0, Math.Min(100, totalScore));
        }

        /// <summary>
        /// Calcula o score personalizado ajustado pelo perfil do usuário.
        /// </summary>
        public double CalculatePersonalizedScore(
            NutritionalInfo? nutrition,
            IEnumerable<ProductIngredient> ingredients,
            IEnumerable<ProductAllergen> allergens,
            UserProfile? profile)
        {
            if (profile == null || nutrition == null)
                return CalculateGeneralScore(nutrition, ingredients);

            // Verifica restrições críticas primeiro
            var criticalViolation = CheckCriticalViolations(ingredients, allergens, profile);
            if (criticalViolation.HasValue)
                return criticalViolation.Value; // 0 para alergênicos críticos

            double baseScore = CalculateGeneralScore(nutrition, ingredients);

            // Ajusta pesos baseado no perfil
            double adjustedScore = baseScore;

            // Ajustes específicos por objetivo/restrição
            adjustedScore = AdjustForWeightLoss(adjustedScore, nutrition, profile);
            adjustedScore = AdjustForDiabetes(adjustedScore, nutrition, ingredients, profile);
            adjustedScore = AdjustForHypertension(adjustedScore, nutrition, profile);
            adjustedScore = AdjustForVegan(adjustedScore, ingredients, profile);
            adjustedScore = AdjustForLowSugar(adjustedScore, nutrition, profile);
            adjustedScore = AdjustForHighProtein(adjustedScore, nutrition, profile);
            adjustedScore = AdjustForKetogenic(adjustedScore, nutrition, profile);

            return Math.Max(0, Math.Min(100, adjustedScore));
        }

        #region Score por Categoria (0-100 cada)

        /// <summary>
        /// Score de açúcar: menos açúcar = maior score
        /// </summary>
        private double CalculateSugarScore(NutritionalInfo nutrition)
        {
            if (!nutrition.SugarsGrams.HasValue)
                return 60.0; // Neutro se não informado

            var sugar = (double)nutrition.SugarsGrams.Value;

            // Escala de penalização
            if (sugar <= 1.0) return 100.0;  // Excelente
            if (sugar <= 3.0) return 90.0;   // Muito bom
            if (sugar <= 5.0) return 80.0;   // Bom
            if (sugar <= 8.0) return 65.0;   // Aceitável
            if (sugar <= 12.0) return 50.0;  // Moderado
            if (sugar <= 15.0) return 35.0;  // Alto
            if (sugar <= 20.0) return 20.0;  // Muito alto
            return 5.0;                      // Extremamente alto
        }

        /// <summary>
        /// Score de gordura ruim: trans, saturada, hidrogenada
        /// </summary>
        private double CalculateBadFatScore(NutritionalInfo nutrition, IEnumerable<ProductIngredient> ingredients)
        {
            double score = 100.0;

            // Gordura trans: ZERO TOLERÂNCIA
            if (nutrition.TransFatGrams.HasValue && nutrition.TransFatGrams.Value > 0)
            {
                return 0.0; // Produto com trans = 0 pontos em gordura
            }

            // Gordura saturada
            if (nutrition.SaturatedFatGrams.HasValue)
            {
                var satFat = (double)nutrition.SaturatedFatGrams.Value;
                if (satFat >= 10.0) score -= 70.0;      // Muito alta
                else if (satFat >= 7.0) score -= 50.0;  // Alta
                else if (satFat >= 5.0) score -= 35.0;  // Moderada-alta
                else if (satFat >= 3.0) score -= 20.0;  // Moderada
                else if (satFat >= 1.5) score -= 10.0;  // Baixa-moderada
            }

            // Gordura hidrogenada (ingredientes)
            var ingredientList = ingredients?.Select(i => i.Name.ToLowerInvariant()).ToList() ?? new List<string>();
            bool hasHydrogenatedFat = ingredientList.Any(ing =>
                ing.Contains("gordura hidrogenada") ||
                ing.Contains("gordura vegetal hidrogenada") ||
                ing.Contains("óleo hidrogenado") ||
                ing.Contains("parcialmente hidrogenada"));

            if (hasHydrogenatedFat)
                score -= 50.0; // Penalidade severa

            return Math.Max(0, score);
        }

        /// <summary>
        /// Score de fibra: mais fibra = maior score
        /// </summary>
        private double CalculateFiberScore(NutritionalInfo nutrition)
        {
            if (!nutrition.DietaryFiberGrams.HasValue)
                return 40.0; // Penaliza levemente falta de informação

            var fiber = (double)nutrition.DietaryFiberGrams.Value;

            if (fiber >= 10.0) return 100.0;  // Excelente
            if (fiber >= 7.0) return 90.0;    // Muito bom
            if (fiber >= 5.0) return 75.0;    // Bom
            if (fiber >= 3.0) return 55.0;    // Aceitável
            if (fiber >= 1.5) return 35.0;    // Baixo
            if (fiber >= 0.5) return 20.0;    // Muito baixo
            return 5.0;                       // Praticamente sem fibra
        }

        /// <summary>
        /// Score de proteína: mais proteína = maior score (com limites razoáveis)
        /// </summary>
        private double CalculateProteinScore(NutritionalInfo nutrition)
        {
            if (!nutrition.ProteinGrams.HasValue)
                return 50.0; // Neutro

            var protein = (double)nutrition.ProteinGrams.Value;

            if (protein >= 20.0) return 100.0;  // Excelente
            if (protein >= 15.0) return 90.0;   // Muito bom
            if (protein >= 10.0) return 75.0;   // Bom
            if (protein >= 5.0) return 55.0;    // Aceitável
            if (protein >= 3.0) return 40.0;    // Baixo
            if (protein >= 1.0) return 25.0;    // Muito baixo
            return 10.0;                        // Praticamente sem proteína
        }

        /// <summary>
        /// Score de sódio: menos sódio = maior score
        /// </summary>
        private double CalculateSodiumScore(NutritionalInfo nutrition)
        {
            if (!nutrition.SodiumMg.HasValue)
                return 60.0; // Neutro

            var sodium = (double)nutrition.SodiumMg.Value;

            if (sodium <= 100.0) return 100.0;    // Excelente
            if (sodium <= 200.0) return 85.0;     // Muito bom
            if (sodium <= 300.0) return 70.0;     // Bom
            if (sodium <= 400.0) return 55.0;     // Aceitável
            if (sodium <= 600.0) return 40.0;     // Moderado
            if (sodium <= 800.0) return 25.0;     // Alto
            if (sodium <= 1000.0) return 15.0;    // Muito alto
            return 5.0;                           // Extremamente alto
        }

        /// <summary>
        /// Score de ultraprocessamento: baseado em indicadores NOVA
        /// </summary>
        private double CalculateUltraProcessedScore(NutritionalInfo nutrition, IEnumerable<ProductIngredient> ingredients)
        {
            double score = 100.0;

            var ingredientList = ingredients?.Select(i => i.Name.ToLowerInvariant()).ToList() ?? new List<string>();
            int ingredientCount = ingredientList.Count;

            // Muitos ingredientes (>15 = ultraprocessado)
            if (ingredientCount > 20) score -= 50.0;
            else if (ingredientCount > 15) score -= 35.0;
            else if (ingredientCount > 10) score -= 20.0;
            else if (ingredientCount > 7) score -= 10.0;

            // Açúcares de alto índice glicêmico
            var highGlycemicCount = ingredientList.Count(ing =>
                ing.Contains("xarope de glicose") ||
                ing.Contains("xarope de milho") ||
                ing.Contains("maltodextrina") ||
                ing.Contains("dextrose") ||
                ing.Contains("açúcar invertido"));

            if (highGlycemicCount > 0)
                score -= (highGlycemicCount * 15.0); // -15 por ingrediente

            // Combinação ruim: alto açúcar + baixa fibra
            if (nutrition.SugarsGrams.HasValue && nutrition.DietaryFiberGrams.HasValue)
            {
                bool highSugar = nutrition.SugarsGrams.Value >= 10.0m;
                bool lowFiber = nutrition.DietaryFiberGrams.Value < 2.0m;

                if (highSugar && lowFiber)
                    score -= 25.0;
            }

            return Math.Max(0, score);
        }

        /// <summary>
        /// Score de aditivos: menos aditivos = maior score
        /// </summary>
        private double CalculateAdditiveScore(IEnumerable<ProductIngredient> ingredients)
        {
            var ingredientList = ingredients?.Select(i => i.Name.ToLowerInvariant()).ToList() ?? new List<string>();
            
            var additiveKeywords = new[]
            {
                "aromatizante", "corante", "emulsificante", "espessante", "estabilizante",
                "realçador de sabor", "conservante", "acidulante", "umectante",
                "antioxidante artificial", "aroma artificial"
            };

            int additiveCount = additiveKeywords.Count(kw => ingredientList.Any(ing => ing.Contains(kw)));

            if (additiveCount == 0) return 100.0;  // Sem aditivos
            if (additiveCount == 1) return 70.0;   // 1 aditivo
            if (additiveCount == 2) return 50.0;   // 2 aditivos
            if (additiveCount == 3) return 30.0;   // 3 aditivos
            if (additiveCount >= 4) return 10.0;   // 4+ aditivos
            return 5.0;
        }

        #endregion

        #region Violações Críticas

        /// <summary>
        /// Verifica violações críticas (alergênicos) que resultam em score 0.
        /// </summary>
        private double? CheckCriticalViolations(
            IEnumerable<ProductIngredient> ingredients,
            IEnumerable<ProductAllergen> allergens,
            UserProfile profile)
        {
            var ingredientList = ingredients?.Select(i => i.Name.ToLowerInvariant()).ToList() ?? new List<string>();
            var allergenList = allergens?.Select(a => a.AllergenName.ToLowerInvariant()).ToList() ?? new List<string>();

            // Lactose
            if (profile.LactoseIntolerance)
            {
                bool hasLactose = allergenList.Any(a => a.Contains("lactose") || a.Contains("leite")) ||
                                  ingredientList.Any(i => i.Contains("lactose") || i.Contains("leite") || i.Contains("soro de leite"));
                if (hasLactose)
                    return 0.0;
            }

            // Glúten
            if (profile.GlutenFree)
            {
                bool hasGluten = allergenList.Any(a => a.Contains("glúten") || a.Contains("trigo")) ||
                                 ingredientList.Any(i => i.Contains("glúten") || i.Contains("trigo") || 
                                                        i.Contains("cevada") || i.Contains("centeio") || i.Contains("aveia"));
                if (hasGluten)
                    return 0.0;
            }

            // Vegano
            if (profile.Goal == GoalType.Vegan)
            {
                bool hasAnimalProduct = ingredientList.Any(i =>
                    i.Contains("leite") || i.Contains("ovo") || i.Contains("mel") ||
                    i.Contains("gelatina") || i.Contains("soro") || i.Contains("lactose") ||
                    i.Contains("caseína") || i.Contains("albumina"));
                if (hasAnimalProduct)
                    return 0.0;
            }

            return null; // Sem violações críticas
        }

        #endregion

        #region Ajustes por Perfil

        private double AdjustForWeightLoss(double baseScore, NutritionalInfo nutrition, UserProfile profile)
        {
            if (profile.Goal != GoalType.WeightLoss)
                return baseScore;

            // Aumenta peso de açúcar, calorias e fibra
            double penalty = 0.0;

            if (nutrition.SugarsGrams.HasValue)
            {
                var sugar = (double)nutrition.SugarsGrams.Value;
                if (sugar >= 20.0) penalty += 25.0;
                else if (sugar >= 15.0) penalty += 18.0;
                else if (sugar >= 10.0) penalty += 12.0;
                else if (sugar >= 5.0) penalty += 6.0;
            }

            if (nutrition.Calories > 400)
                penalty += 10.0;
            else if (nutrition.Calories > 300)
                penalty += 5.0;

            if (!nutrition.DietaryFiberGrams.HasValue || nutrition.DietaryFiberGrams.Value < 3.0m)
                penalty += 8.0;

            return baseScore - penalty;
        }

        private double AdjustForDiabetes(double baseScore, NutritionalInfo nutrition, IEnumerable<ProductIngredient> ingredients, UserProfile profile)
        {
            if (!profile.Diabetes && profile.Goal != GoalType.DiabeticFriendly)
                return baseScore;

            double penalty = 0.0;

            // Penalização fortíssima para açúcar
            if (nutrition.SugarsGrams.HasValue)
            {
                var sugar = (double)nutrition.SugarsGrams.Value;
                if (sugar >= 15.0) penalty += 35.0;  // Crítico
                else if (sugar >= 10.0) penalty += 25.0;
                else if (sugar >= 5.0) penalty += 15.0;
                else if (sugar >= 3.0) penalty += 8.0;
            }

            // Maltodextrina é crítica para diabéticos
            var ingredientList = ingredients?.Select(i => i.Name.ToLowerInvariant()).ToList() ?? new List<string>();
            if (ingredientList.Any(i => i.Contains("maltodextrina")))
                penalty += 20.0;

            return baseScore - penalty;
        }

        private double AdjustForHypertension(double baseScore, NutritionalInfo nutrition, UserProfile profile)
        {
            if (!profile.SodiumControl && profile.Goal != GoalType.LowSodium)
                return baseScore;

            double penalty = 0.0;

            if (nutrition.SodiumMg.HasValue)
            {
                var sodium = (double)nutrition.SodiumMg.Value;
                if (sodium >= 1000.0) penalty += 30.0;
                else if (sodium >= 800.0) penalty += 22.0;
                else if (sodium >= 600.0) penalty += 15.0;
                else if (sodium >= 400.0) penalty += 8.0;
            }

            return baseScore - penalty;
        }

        private double AdjustForVegan(double baseScore, IEnumerable<ProductIngredient> ingredients, UserProfile profile)
        {
            if (profile.Goal != GoalType.Vegan)
                return baseScore;

            // Já checado em violações críticas, retorna 0 se houver produto animal
            return baseScore;
        }

        private double AdjustForLowSugar(double baseScore, NutritionalInfo nutrition, UserProfile profile)
        {
            if (profile.Goal != GoalType.LowSugar)
                return baseScore;

            double penalty = 0.0;

            if (nutrition.SugarsGrams.HasValue)
            {
                var sugar = (double)nutrition.SugarsGrams.Value;
                if (sugar >= 15.0) penalty += 30.0;
                else if (sugar >= 10.0) penalty += 20.0;
                else if (sugar >= 5.0) penalty += 10.0;
            }

            return baseScore - penalty;
        }

        private double AdjustForHighProtein(double baseScore, NutritionalInfo nutrition, UserProfile profile)
        {
            if (profile.Goal != GoalType.HighProtein)
                return baseScore;

            double bonus = 0.0;

            if (nutrition.ProteinGrams.HasValue)
            {
                var protein = (double)nutrition.ProteinGrams.Value;
                if (protein >= 20.0) bonus += 15.0;
                else if (protein >= 15.0) bonus += 10.0;
                else if (protein >= 10.0) bonus += 5.0;
                else if (protein < 5.0) bonus -= 10.0; // Penaliza baixa proteína
            }

            return baseScore + bonus;
        }

        private double AdjustForKetogenic(double baseScore, NutritionalInfo nutrition, UserProfile profile)
        {
            if (profile.Goal != GoalType.Ketogenic)
                return baseScore;

            double adjustment = 0.0;

            // Keto: baixo carb, alto gordura, moderada proteína
            if (nutrition.TotalCarbohydratesGrams.HasValue)
            {
                var carbs = (double)nutrition.TotalCarbohydratesGrams.Value;
                if (carbs <= 5.0) adjustment += 15.0;      // Excelente
                else if (carbs <= 10.0) adjustment += 8.0;  // Bom
                else if (carbs <= 15.0) adjustment -= 5.0;  // Aceitável
                else adjustment -= 20.0;                    // Muito alto
            }

            // Proteína moderada (não muito alta)
            if (nutrition.ProteinGrams.HasValue)
            {
                var protein = (double)nutrition.ProteinGrams.Value;
                if (protein >= 10.0 && protein <= 20.0) adjustment += 5.0;
            }

            return baseScore + adjustment;
        }

        #endregion

        /// <summary>
        /// Determina a classificação baseada no score (0-100).
        /// </summary>
        public string DetermineClassification(double score)
        {
            if (score >= 80.0) return "Excellent";
            if (score >= 60.0) return "Good";
            if (score >= 40.0) return "Attention";
            return "Avoid";
        }

        /// <summary>
        /// Gera resumo detalhado do cálculo para debugging.
        /// </summary>
        public string GenerateScoreBreakdown(
            NutritionalInfo? nutrition,
            IEnumerable<ProductIngredient> ingredients,
            UserProfile? profile = null)
        {
            if (nutrition == null)
                return "Sem dados nutricionais disponíveis.";

            var breakdown = new List<string>
            {
                $"Açúcar: {CalculateSugarScore(nutrition):F1}/100 (peso 25%)",
                $"Gordura Ruim: {CalculateBadFatScore(nutrition, ingredients):F1}/100 (peso 20%)",
                $"Fibra: {CalculateFiberScore(nutrition):F1}/100 (peso 15%)",
                $"Proteína: {CalculateProteinScore(nutrition):F1}/100 (peso 10%)",
                $"Sódio: {CalculateSodiumScore(nutrition):F1}/100 (peso 10%)",
                $"Ultraprocessamento: {CalculateUltraProcessedScore(nutrition, ingredients):F1}/100 (peso 10%)",
                $"Aditivos: {CalculateAdditiveScore(ingredients):F1}/100 (peso 10%)"
            };

            return string.Join("\n", breakdown);
        }
    }
}
