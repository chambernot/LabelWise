using System.Collections.Generic;
using System.Linq;
using LabelWise.Application.Confidence;
using LabelWise.Application.DTOs;
using LabelWise.Application.Interfaces;
using LabelWise.Application.SummaryGeneration;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Enums;
using ConfLevel = LabelWise.Application.Confidence.ConfidenceLevel;

namespace LabelWise.Application.Rules
{
    public class RulesEngine : IProductAnalysisEngine
    {
        private readonly IEnumerable<IRule> _rules;
        private readonly IAnalysisSummaryGenerator _summaryGenerator;

        public RulesEngine(IEnumerable<IRule> rules, IAnalysisSummaryGenerator summaryGenerator)
        {
            _rules = rules;
            _summaryGenerator = summaryGenerator;
        }

        public ProductAnalysisResultDto Analyze(Product product, NutritionalInfo? nutrition, IEnumerable<ProductIngredient> ingredients, IEnumerable<ProductAllergen> allergens, UserProfile? userProfile = null)
        {
            return Analyze(product, nutrition, ingredients, allergens, userProfile, context: null);
        }

        /// <summary>
        /// Analisa o produto com contexto de confiança.
        /// Esta versão permite análise consciente de completude e confiança.
        /// </summary>
        public ProductAnalysisResultDto Analyze(
            Product product, 
            NutritionalInfo? nutrition, 
            IEnumerable<ProductIngredient> ingredients, 
            IEnumerable<ProductAllergen> allergens, 
            UserProfile? userProfile,
            AnalysisContext? context)
        {
            var ingredientsList = ingredients?.ToList() ?? [];
            var allergensList = allergens?.ToList() ?? [];

            var result = new ProductAnalysisResultDto
            {
                ProductName = product.Name,
                Brand = product.Brand,
                Summary = "",
            };

            var scoring = new ScoringContext();

            // Initialize default scores
            result.GeneralScore = scoring.GeneralScore;
            result.PersonalizedScore = scoring.PersonalizedScore;

            // Pass a mutable result object that rules can update
            foreach (var r in _rules)
            {
                r.Evaluate(product, nutrition, ingredientsList, allergensList, userProfile, result);
            }

            // Clamp scores
            result.GeneralScore = System.Math.Max(0, System.Math.Min(1, result.GeneralScore));
            result.PersonalizedScore = System.Math.Max(0, System.Math.Min(1, result.PersonalizedScore));

            // Cria contexto de análise se não fornecido
            context ??= CreateAnalysisContext(nutrition, ingredientsList, allergensList, product);

            // ═══════════════════════════════════════════════════════════════════
            // NOVA LÓGICA: Classificação e resumo conscientes de confiança
            // ═══════════════════════════════════════════════════════════════════

            if (_summaryGenerator is ConfidenceAwareSummaryGenerator confidenceAwareGenerator)
            {
                // Usa o gerador consciente de confiança
                var summaryResult = confidenceAwareGenerator.GenerateSummaryWithContext(
                    product,
                    nutrition,
                    ingredientsList,
                    allergensList,
                    userProfile,
                    result.GeneralScore,
                    result.PersonalizedScore,
                    result.Alerts,
                    result.Recommendations,
                    context);

                result.Summary = summaryResult.Summary;
                result.ShortSummary = summaryResult.ShortSummary;
                result.Classification = summaryResult.ClassificationString;
                result.ConfidenceLevel = summaryResult.ConfidenceLevel;

                // Adiciona disclaimers aos alertas se análise é parcial
                if (summaryResult.IsPartialAnalysis && summaryResult.Disclaimers.Any())
                {
                    foreach (var disclaimer in summaryResult.Disclaimers)
                    {
                        if (!result.Alerts.Contains(disclaimer))
                        {
                            result.Alerts.Insert(0, disclaimer);
                        }
                    }
                }
            }
            else
            {
                // Fallback: usa lógica tradicional com ajustes de segurança
                var initialClassification = DetermineClassification(
                    result.GeneralScore, result.PersonalizedScore, context);

                // Aplica regras de ajuste de classificação
                var adjustedClassification = SummaryAdjustmentRules.AdjustClassification(
                    initialClassification, context, out var adjustmentReason);

                result.Classification = adjustedClassification.ToString();
                result.ShortSummary = GenerateShortSummary(
                    adjustedClassification, result.GeneralScore, result.PersonalizedScore, context);
                result.ConfidenceLevel = DetermineConfidenceLevel(nutrition, ingredientsList, allergensList);

                // Gera o resumo usando o gerador configurado
                result.Summary = _summaryGenerator.GenerateSummary(
                    product,
                    nutrition,
                    ingredientsList,
                    allergensList,
                    userProfile,
                    result.GeneralScore,
                    result.PersonalizedScore,
                    result.Alerts,
                    result.Recommendations
                );

                // Adiciona disclaimer se classificação foi ajustada
                if (!string.IsNullOrEmpty(adjustmentReason))
                {
                    result.Alerts.Insert(0, $"ℹ️ {adjustmentReason}");
                }

                // Adiciona disclaimers para análises parciais
                var disclaimers = SummaryAdjustmentRules.GetDisclaimers(context);
                foreach (var disclaimer in disclaimers)
                {
                    if (!result.Alerts.Contains(disclaimer))
                    {
                        result.Alerts.Add(disclaimer);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Cria um contexto de análise baseado nos dados disponíveis.
        /// </summary>
        private AnalysisContext CreateAnalysisContext(
            NutritionalInfo? nutrition,
            List<ProductIngredient> ingredients,
            List<ProductAllergen> allergens,
            Product product)
        {
            var hasNutrition = nutrition != null;
            var hasIngredients = ingredients.Count > 0;
            var hasAllergens = allergens.Count > 0;

            // Conta campos nutricionais preenchidos
            var nutritionalFields = CountNutritionalFields(nutrition);

            // Determina se há dados suficientes
            var dataPoints = 0;
            if (hasNutrition && nutritionalFields >= 3) dataPoints++;
            if (hasIngredients && ingredients.Count >= 2) dataPoints++;

            // Determina se produto foi identificado
            var productIdentified = !string.IsNullOrWhiteSpace(product.Name) && 
                                   product.Name.Length > 2 &&
                                   !product.Name.Equals("Unknown", StringComparison.OrdinalIgnoreCase);

            return new AnalysisContext
            {
                ProductIdentified = productIdentified,
                OcrComplete = hasIngredients || nutritionalFields >= 3,
                AnalysisComplete = dataPoints >= 2,
                HasDeclaredAllergens = hasAllergens,
                ValidIngredientsCount = ingredients.Count,
                AllergensCount = allergens.Count,
                NutritionalFieldsCount = nutritionalFields,
                IngredientsCompletenessScore = hasIngredients ? 
                    System.Math.Min(1.0, ingredients.Count / 5.0) : 0.0,
                NutritionalCompletenessScore = hasNutrition ? 
                    System.Math.Min(1.0, nutritionalFields / 7.0) : 0.0,
                OverallConfidenceLevel = dataPoints switch
                {
                    >= 2 => ConfLevel.Medium,
                    1 => ConfLevel.Low,
                    _ => ConfLevel.VeryLow
                },
                QualityGatePassed = dataPoints >= 2 && productIdentified
            };
        }

        private int CountNutritionalFields(NutritionalInfo? nutrition)
        {
            if (nutrition == null) return 0;

            var count = 0;
            if (nutrition.Calories > 0) count++;
            if (nutrition.TotalFatGrams.HasValue && nutrition.TotalFatGrams > 0) count++;
            if (nutrition.TotalCarbohydratesGrams.HasValue && nutrition.TotalCarbohydratesGrams > 0) count++;
            if (nutrition.ProteinGrams.HasValue && nutrition.ProteinGrams > 0) count++;
            if (nutrition.SodiumMg.HasValue && nutrition.SodiumMg > 0) count++;
            if (nutrition.SugarsGrams.HasValue && nutrition.SugarsGrams > 0) count++;
            if (nutrition.DietaryFiberGrams.HasValue && nutrition.DietaryFiberGrams > 0) count++;
            return count;
        }

        private string DetermineConfidenceLevel(
            NutritionalInfo? nutrition, 
            List<ProductIngredient> ingredients,
            List<ProductAllergen> allergens)
        {
            var hasNutrition = nutrition != null;
            var hasIngredients = ingredients.Count > 0;
            var hasAllergens = allergens.Count > 0;

            var dataPoints = 0;
            if (hasNutrition) dataPoints++;
            if (hasIngredients) dataPoints++;
            if (hasAllergens) dataPoints++;

            // Quanto mais dados, maior a confiança
            return dataPoints switch
            {
                3 => "Alto",
                2 => "Médio",
                1 => "Baixo",
                _ => "Muito Baixo"
            };
        }

        private AnalysisClassification DetermineClassification(
            double generalScore, 
            double personalizedScore,
            AnalysisContext context)
        {
            // Usa o menor score entre geral e personalizado (mais conservador)
            var minScore = System.Math.Min(generalScore, personalizedScore);
            var avgScore = (generalScore + personalizedScore) / 2.0;

            // ═══════════════════════════════════════════════════════════════════
            // REGRA: Se análise é parcial, NUNCA retornar classificação otimista
            // ═══════════════════════════════════════════════════════════════════
            if (context.IsPartialAnalysis)
            {
                // Limita classificação máxima a Caution em análises parciais
                if (avgScore >= 0.65)
                    return AnalysisClassification.Caution;
                if (avgScore >= 0.50)
                    return AnalysisClassification.Caution;
                return AnalysisClassification.Avoid;
            }

            // ═══════════════════════════════════════════════════════════════════
            // REGRA: Se há alérgenos, evitar Safe mesmo com score alto
            // ═══════════════════════════════════════════════════════════════════
            if (context.HasDeclaredAllergens)
            {
                if (avgScore >= 0.80 && minScore >= 0.70)
                    return AnalysisClassification.Caution; // Rebaixa de Safe para Caution
                if (avgScore >= 0.65)
                    return AnalysisClassification.Caution;
                return AnalysisClassification.Avoid;
            }

            // Classificação padrão para análises completas sem alérgenos
            if (avgScore >= 0.80 && minScore >= 0.70)
            {
                return AnalysisClassification.Safe;
            }
            else if (avgScore >= 0.65 && minScore >= 0.50)
            {
                return AnalysisClassification.Moderate;
            }
            else if (avgScore >= 0.50)
            {
                return AnalysisClassification.Caution;
            }
            else
            {
                return AnalysisClassification.Avoid;
            }
        }

        private string GenerateShortSummary(
            AnalysisClassification classification, 
            double generalScore, 
            double personalizedScore,
            AnalysisContext context)
        {
            // Converte para escala 0-100
            var minScore = System.Math.Min(generalScore, personalizedScore) * 100.0;
            var scoreDisplay = (int)System.Math.Round(minScore);

            // ═══════════════════════════════════════════════════════════════════
            // REGRA: Análises parciais NUNCA usam frases otimistas
            // ═══════════════════════════════════════════════════════════════════
            if (context.IsPartialAnalysis)
            {
                if (!context.ProductIdentified)
                    return $"Produto não identificado. Envie imagem mais clara. (Score parcial: {scoreDisplay}/100)";

                if (!context.OcrComplete)
                    return $"Leitura incompleta do rótulo ({scoreDisplay}/100). Envie outra imagem para maior precisão.";

                return $"Análise parcial do rótulo ({scoreDisplay}/100). Informações incompletas.";
            }

            // ═══════════════════════════════════════════════════════════════════
            // REGRA: Alérgenos declarados - sempre alertar
            // ═══════════════════════════════════════════════════════════════════
            if (context.HasDeclaredAllergens)
            {
                return $"Contém alérgenos declarados ({scoreDisplay}/100). Verifique compatibilidade antes de consumir.";
            }

            // Mensagens padrão (apenas para análises completas sem alérgenos)
            return classification switch
            {
                AnalysisClassification.Excellent => 
                    $"Excelente escolha ({scoreDisplay}/100). Produto adequado para consumo regular.",
                AnalysisClassification.Safe => 
                    $"Boa escolha ({scoreDisplay}/100). Pode consumir regularmente com moderação.",
                AnalysisClassification.Moderate => 
                    $"Escolha moderada ({scoreDisplay}/100). Atenção às porções.",
                AnalysisClassification.Caution => 
                    $"Atenção necessária ({scoreDisplay}/100). Consumir esporadicamente.",
                AnalysisClassification.Avoid => 
                    $"Não recomendado ({scoreDisplay}/100). Evitar este produto.",
                AnalysisClassification.Unsafe => 
                    $"Evitar ({scoreDisplay}/100). Perfil nutricional preocupante.",
                AnalysisClassification.Incomplete => 
                    $"Análise incompleta ({scoreDisplay}/100). Envie mais dados.",
                _ => 
                    $"Análise incompleta ({scoreDisplay}/100). Dados insuficientes."
            };
        }
    }
}
