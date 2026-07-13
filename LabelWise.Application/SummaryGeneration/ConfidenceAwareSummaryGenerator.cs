using LabelWise.Application.Confidence;
using LabelWise.Application.DTOs;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Enums;
using ConfLevel = LabelWise.Application.Confidence.ConfidenceLevel;

namespace LabelWise.Application.SummaryGeneration
{
    /// <summary>
    /// Gerador de resumos baseado em regras com consciência de completude e confiança.
    /// 
    /// Implementa as seguintes regras de segurança:
    /// 1. Análise parcial → não usa "Boa Escolha" ou "Pode consumir regularmente"
    /// 2. Alérgenos declarados → evita classificação Safe por padrão
    /// 3. Produto não identificado → classificação Caution ou Incomplete
    /// 4. Confiança alta + análise completa → permite mensagens afirmativas
    /// </summary>
    public class ConfidenceAwareSummaryGenerator : IAnalysisSummaryGenerator
    {
        public string StrategyName => "ConfidenceAware";

        /// <summary>
        /// Gera resumo considerando contexto de análise (confiança e completude).
        /// </summary>
        public SummaryGenerationResult GenerateSummaryWithContext(
            Product product,
            NutritionalInfo? nutrition,
            IEnumerable<ProductIngredient> ingredients,
            IEnumerable<ProductAllergen> allergens,
            UserProfile? userProfile,
            double generalScore,
            double personalizedScore,
            List<string> alerts,
            List<string> recommendations,
            AnalysisContext context)
        {
            var result = new SummaryGenerationResult();

            // Converte para escala 0-100
            var generalScore100 = generalScore * 100.0;
            var personalizedScore100 = personalizedScore * 100.0;
            var minScore = Math.Min(generalScore100, personalizedScore100);

            // ═══════════════════════════════════════════════════════════════════
            // PASSO 1: Determinar classificação inicial (baseada em score)
            // ═══════════════════════════════════════════════════════════════════
            var initialClassification = DetermineInitialClassification(minScore);

            // ═══════════════════════════════════════════════════════════════════
            // PASSO 2: Ajustar classificação baseada no contexto
            // ═══════════════════════════════════════════════════════════════════
            var adjustedClassification = SummaryAdjustmentRules.AdjustClassification(
                initialClassification, context, out var adjustmentReason);

            result.OriginalClassification = initialClassification;
            result.Classification = adjustedClassification;
            result.ClassificationString = adjustedClassification.ToString();
            result.ClassificationAdjusted = initialClassification != adjustedClassification;
            result.AdjustmentReason = adjustmentReason;
            result.IsPartialAnalysis = context.IsPartialAnalysis;
            result.ConfidenceLevel = SummaryAdjustmentRules.GetConfidenceLevelDisplay(context);

            // ═══════════════════════════════════════════════════════════════════
            // PASSO 3: Obter disclaimers apropriados
            // ═══════════════════════════════════════════════════════════════════
            result.Disclaimers = SummaryAdjustmentRules.GetDisclaimers(context);

            // ═══════════════════════════════════════════════════════════════════
            // PASSO 4: Gerar resumo e short summary baseados no contexto
            // ═══════════════════════════════════════════════════════════════════
            result.Summary = BuildSummary(
                product, nutrition, allergens.ToList(), userProfile,
                minScore, alerts, context, adjustedClassification);

            result.ShortSummary = BuildShortSummary(
                minScore, context, adjustedClassification);

            return result;
        }

        /// <summary>
        /// Implementação da interface IAnalysisSummaryGenerator.
        /// Para compatibilidade, cria um contexto padrão conservador.
        /// </summary>
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
            // Cria contexto conservador para chamadas legadas
            var context = CreateConservativeContext(nutrition, ingredients, allergens);

            var result = GenerateSummaryWithContext(
                product, nutrition, ingredients, allergens, userProfile,
                generalScore, personalizedScore, alerts, recommendations, context);

            return result.Summary;
        }

        // ═══════════════════════════════════════════════════════════════════
        // MÉTODOS PRIVADOS
        // ═══════════════════════════════════════════════════════════════════

        private AnalysisClassification DetermineInitialClassification(double minScore)
        {
            return minScore switch
            {
                >= 80.0 => AnalysisClassification.Excellent,
                >= 60.0 => AnalysisClassification.Safe,
                >= 40.0 => AnalysisClassification.Caution,
                >= 20.0 => AnalysisClassification.Avoid,
                _ => AnalysisClassification.Unsafe
            };
        }

        private string BuildSummary(
            Product product,
            NutritionalInfo? nutrition,
            List<ProductAllergen> allergens,
            UserProfile? userProfile,
            double minScore,
            List<string> alerts,
            AnalysisContext context,
            AnalysisClassification classification)
        {
            var summaryParts = new List<string>();

            // ═══════════════════════════════════════════════════════════════════
            // CABEÇALHO: Depende do contexto de análise
            // ═══════════════════════════════════════════════════════════════════
            if (context.IsPartialAnalysis)
            {
                // NUNCA usar frases otimistas em análises parciais
                summaryParts.Add(GetPartialAnalysisHeader(context, minScore));
            }
            else if (context.CanUseAffirmativeMessages)
            {
                // Apenas se confiança é alta e análise completa
                summaryParts.Add(GetAffirmativeHeader(classification, minScore));
            }
            else
            {
                // Caso conservador padrão
                summaryParts.Add(GetConservativeHeader(classification, minScore));
            }

            // ═══════════════════════════════════════════════════════════════════
            // SEÇÃO NUTRICIONAL
            // ═══════════════════════════════════════════════════════════════════
            if (nutrition != null)
            {
                var nutritionalHighlights = BuildNutritionalHighlights(nutrition, context);
                if (!string.IsNullOrEmpty(nutritionalHighlights))
                {
                    summaryParts.Add(nutritionalHighlights);
                }
            }
            else if (context.IsPartialAnalysis)
            {
                summaryParts.Add("⚠️ Informações nutricionais não disponíveis");
            }

            // ═══════════════════════════════════════════════════════════════════
            // ALERTAS
            // ═══════════════════════════════════════════════════════════════════
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

            // ═══════════════════════════════════════════════════════════════════
            // ALÉRGENOS - Sempre destacar quando presentes
            // ═══════════════════════════════════════════════════════════════════
            if (context.HasDeclaredAllergens)
            {
                var allergenNames = allergens.Select(a => a.AllergenName).Take(5);
                summaryParts.Add($"🔴 Alérgenos: {string.Join(", ", allergenNames)}");
            }

            // ═══════════════════════════════════════════════════════════════════
            // CONTEXTO PERSONALIZADO
            // ═══════════════════════════════════════════════════════════════════
            if (userProfile != null)
            {
                var personalContext = BuildPersonalContext(userProfile, allergens, minScore, context);
                if (!string.IsNullOrEmpty(personalContext))
                {
                    summaryParts.Add(personalContext);
                }
            }

            // ═══════════════════════════════════════════════════════════════════
            // DISCLAIMER PARA ANÁLISES PARCIAIS
            // ═══════════════════════════════════════════════════════════════════
            if (context.IsPartialAnalysis)
            {
                summaryParts.Add(SummaryAdjustmentRules.IncompleteOcrPhrases[2]); // "Envie outra imagem..."
            }

            return string.Join(" • ", summaryParts);
        }

        private string GetPartialAnalysisHeader(AnalysisContext context, double minScore)
        {
            var scoreDisplay = (int)Math.Round(minScore);

            if (!context.ProductIdentified)
            {
                return $"**⚠️ Produto não identificado** (Score parcial: {scoreDisplay}/100)";
            }

            if (!context.OcrComplete)
            {
                return $"**📷 Leitura incompleta do rótulo** (Score parcial: {scoreDisplay}/100)";
            }

            return $"**📋 Análise parcial** (Score: {scoreDisplay}/100)";
        }

        private string GetAffirmativeHeader(AnalysisClassification classification, double minScore)
        {
            var scoreDisplay = (int)Math.Round(minScore);

            return classification switch
            {
                AnalysisClassification.Excellent =>
                    $"**✅ Excelente Escolha** (Score: {scoreDisplay}/100) - Produto adequado para consumo regular.",
                AnalysisClassification.Safe =>
                    $"**✅ Boa Escolha** (Score: {scoreDisplay}/100) - Pode consumir regularmente com moderação.",
                AnalysisClassification.Moderate =>
                    $"**⚡ Escolha Moderada** (Score: {scoreDisplay}/100) - Consumir com atenção às porções.",
                AnalysisClassification.Caution =>
                    $"**⚠️ Atenção Necessária** (Score: {scoreDisplay}/100) - Consumir esporadicamente.",
                AnalysisClassification.Avoid =>
                    $"**🛑 Não Recomendado** (Score: {scoreDisplay}/100) - Evitar consumo frequente.",
                AnalysisClassification.Unsafe =>
                    $"**🚫 Evitar** (Score: {scoreDisplay}/100) - Produto com perfil nutricional preocupante.",
                _ =>
                    $"**❓ Análise Incompleta** (Score: {scoreDisplay}/100) - Dados insuficientes."
            };
        }

        private string GetConservativeHeader(AnalysisClassification classification, double minScore)
        {
            var scoreDisplay = (int)Math.Round(minScore);

            // Versões conservadoras - evita termos excessivamente positivos
            return classification switch
            {
                AnalysisClassification.Excellent =>
                    $"**Perfil Nutricional Positivo** (Score: {scoreDisplay}/100) - Verifique os detalhes.",
                AnalysisClassification.Safe =>
                    $"**Perfil Nutricional Aceitável** (Score: {scoreDisplay}/100) - Considere as porções.",
                AnalysisClassification.Moderate =>
                    $"**Requer Atenção** (Score: {scoreDisplay}/100) - Avalie consumo moderado.",
                AnalysisClassification.Caution =>
                    $"**Atenção Necessária** (Score: {scoreDisplay}/100) - Consumir esporadicamente.",
                AnalysisClassification.Avoid =>
                    $"**Não Recomendado** (Score: {scoreDisplay}/100) - Evitar consumo frequente.",
                AnalysisClassification.Unsafe =>
                    $"**Evitar** (Score: {scoreDisplay}/100) - Perfil nutricional preocupante.",
                AnalysisClassification.Incomplete =>
                    $"**Análise Incompleta** (Score: {scoreDisplay}/100) - Dados insuficientes para avaliação.",
                _ =>
                    $"**Status Indefinido** (Score: {scoreDisplay}/100) - Necessária análise adicional."
            };
        }

        private string BuildShortSummary(
            double minScore,
            AnalysisContext context,
            AnalysisClassification classification)
        {
            var scoreDisplay = (int)Math.Round(minScore);

            // ═══════════════════════════════════════════════════════════════════
            // REGRA: Em análises parciais, NUNCA usar mensagens otimistas
            // ═══════════════════════════════════════════════════════════════════
            if (context.IsPartialAnalysis)
            {
                if (!context.ProductIdentified)
                {
                    return $"Produto não identificado. Envie imagem mais clara. (Score parcial: {scoreDisplay}/100)";
                }

                if (!context.OcrComplete)
                {
                    return $"Leitura incompleta do rótulo. Envie outra imagem. (Score parcial: {scoreDisplay}/100)";
                }

                return $"Análise parcial do rótulo ({scoreDisplay}/100). Dados incompletos.";
            }

            // ═══════════════════════════════════════════════════════════════════
            // REGRA: Alérgenos declarados - sempre alertar
            // ═══════════════════════════════════════════════════════════════════
            if (context.HasDeclaredAllergens)
            {
                return classification switch
                {
                    AnalysisClassification.Excellent or AnalysisClassification.Safe =>
                        $"Perfil nutricional positivo, mas CONTÉM ALÉRGENOS ({scoreDisplay}/100). Verifique.",
                    _ =>
                        $"Atenção: contém alérgenos declarados ({scoreDisplay}/100). Verifique compatibilidade."
                };
            }

            // ═══════════════════════════════════════════════════════════════════
            // Análise completa com alta confiança - mensagens afirmativas permitidas
            // ═══════════════════════════════════════════════════════════════════
            if (context.CanUseAffirmativeMessages)
            {
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
                    _ =>
                        $"Análise incompleta ({scoreDisplay}/100). Dados insuficientes."
                };
            }

            // ═══════════════════════════════════════════════════════════════════
            // Caso conservador padrão (confiança média ou baixa)
            // ═══════════════════════════════════════════════════════════════════
            return classification switch
            {
                AnalysisClassification.Excellent =>
                    $"Perfil nutricional positivo ({scoreDisplay}/100). Verifique detalhes.",
                AnalysisClassification.Safe =>
                    $"Perfil aceitável ({scoreDisplay}/100). Considere as porções.",
                AnalysisClassification.Moderate =>
                    $"Requer atenção ({scoreDisplay}/100). Avalie consumo.",
                AnalysisClassification.Caution =>
                    $"Atenção necessária ({scoreDisplay}/100). Consumo moderado.",
                AnalysisClassification.Avoid =>
                    $"Não recomendado ({scoreDisplay}/100). Considere alternativas.",
                AnalysisClassification.Unsafe =>
                    $"Evitar ({scoreDisplay}/100). Busque alternativas.",
                AnalysisClassification.Incomplete =>
                    $"Análise incompleta ({scoreDisplay}/100). Envie mais dados.",
                _ =>
                    $"Status indefinido ({scoreDisplay}/100). Análise adicional necessária."
            };
        }

        private string BuildNutritionalHighlights(NutritionalInfo nutrition, AnalysisContext context)
        {
            var highlights = new List<string>();

            // Se análise é parcial, adiciona disclaimer
            if (context.IsPartialAnalysis && context.NutritionalFieldsCount < 5)
            {
                highlights.Add("ℹ️ Dados nutricionais parciais");
            }

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

            // Gordura trans - sempre alertar
            if (nutrition.TransFatGrams.HasValue && nutrition.TransFatGrams > 0)
            {
                highlights.Add("⚠️ CONTÉM GORDURA TRANS");
            }

            return highlights.Any() ? string.Join(", ", highlights) : string.Empty;
        }

        private string BuildPersonalContext(
            UserProfile userProfile,
            List<ProductAllergen> allergens,
            double score,
            AnalysisContext context)
        {
            var contexts = new List<string>();

            // Verifica alérgenos que coincidem com restrições do perfil
            if (context.HasMatchingUserAllergens)
            {
                contexts.Add("🚨 CONFLITO com suas restrições alimentares!");
            }

            // Restrições do perfil
            var restrictions = new List<string>();
            if (userProfile.LactoseIntolerance) restrictions.Add("lactose");
            if (userProfile.GlutenFree) restrictions.Add("glúten");
            if (userProfile.Diabetes) restrictions.Add("diabetes");
            if (userProfile.SodiumControl) restrictions.Add("sódio");

            if (restrictions.Any())
            {
                contexts.Add($"Seu perfil: {string.Join(", ", restrictions)}");

                // Alerta mais forte se score baixo e há restrições
                if (score < 40.0)
                {
                    contexts.Add("⚠️ Este produto pode não ser adequado para seu perfil");
                }
            }

            // Objetivo
            var goal = GetGoalDescription(userProfile.Goal);
            if (!string.IsNullOrEmpty(goal))
            {
                contexts.Add($"Objetivo: {goal}");
            }

            return contexts.Any() ? string.Join(" • ", contexts) : string.Empty;
        }

        private string GetGoalDescription(GoalType goal)
        {
            return goal switch
            {
                GoalType.WeightLoss => "Perda de peso",
                GoalType.WeightGain => "Ganho de massa",
                GoalType.MaintainWeight => "Manutenção de peso",
                GoalType.DiabeticFriendly => "Controle de diabetes",
                GoalType.LowSodium => "Baixo sódio",
                GoalType.LowSugar => "Baixo açúcar",
                GoalType.HighProtein => "Alto proteína",
                _ => goal.ToString()
            };
        }

        private AnalysisContext CreateConservativeContext(
            NutritionalInfo? nutrition,
            IEnumerable<ProductIngredient> ingredients,
            IEnumerable<ProductAllergen> allergens)
        {
            var ingredientsList = ingredients?.ToList() ?? [];
            var allergensList = allergens?.ToList() ?? [];

            var hasNutrition = nutrition != null;
            var hasIngredients = ingredientsList.Count > 0;
            var hasAllergens = allergensList.Count > 0;

            // Determina completude baseada em dados disponíveis
            var dataPoints = 0;
            if (hasNutrition) dataPoints++;
            if (hasIngredients) dataPoints++;

            return new AnalysisContext
            {
                // Conservador: assume que produto foi identificado se há ingredientes
                ProductIdentified = hasIngredients || hasNutrition,

                // Conservador: assume OCR completo se há dados
                OcrComplete = hasIngredients || hasNutrition,

                // Análise completa requer múltiplos dados
                AnalysisComplete = dataPoints >= 2,

                // Alérgenos declarados
                HasDeclaredAllergens = hasAllergens,

                // Métricas de completude
                ValidIngredientsCount = ingredientsList.Count,
                AllergensCount = allergensList.Count,
                NutritionalFieldsCount = CountNutritionalFields(nutrition),

                // Confiança conservadora
                OverallConfidenceLevel = dataPoints switch
                {
                    >= 2 => ConfLevel.Medium,
                    1 => ConfLevel.Low,
                    _ => ConfLevel.VeryLow
                },

                // Quality gate passa apenas com dados completos
                QualityGatePassed = dataPoints >= 2
            };
        }

        private int CountNutritionalFields(NutritionalInfo? nutrition)
        {
            if (nutrition == null) return 0;

            var count = 0;
            if (nutrition.Calories > 0) count++;
            if (nutrition.TotalFatGrams.HasValue) count++;
            if (nutrition.TotalCarbohydratesGrams.HasValue) count++;
            if (nutrition.ProteinGrams.HasValue) count++;
            if (nutrition.SodiumMg.HasValue) count++;
            if (nutrition.SugarsGrams.HasValue) count++;
            if (nutrition.DietaryFiberGrams.HasValue) count++;
            return count;
        }
    }
}
