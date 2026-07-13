// EXEMPLOS DE VALIDAÇÃO DO NOVO MOTOR DE SCORING
// Arquivo: SCORING_VALIDATION_EXAMPLES.cs

using System;
using System.Collections.Generic;
using LabelWise.Application.Scoring;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Examples
{
    /// <summary>
    /// Exemplos práticos de validação do motor de scoring nutricional.
    /// Use estes exemplos para validar que produtos ultraprocessados recebem scores baixos.
    /// </summary>
    public class ScoringValidationExamples
    {
        private readonly NutritionalScoringEngine _engine = new();

        /// <summary>
        /// EXEMPLO 1: Biscoito Recheado Ultraprocessado
        /// Esperado: Score < 40 (Avoid)
        /// </summary>
        public void ValidateUltraProcessedCookie()
        {
            Console.WriteLine("=== EXEMPLO 1: Biscoito Recheado Ultraprocessado ===\n");

            // Dados nutricionais típicos de biscoito recheado (por 100g)
            var nutrition = CreateNutrition(
                calories: 480m,
                totalFat: 22m,
                saturatedFat: 9m,
                transFat: 0.5m,
                sodium: 420m,
                carbs: 66m,
                fiber: 1m,
                sugars: 28m,
                protein: 4m
            );

            // Lista extensa de ingredientes com aditivos
            var ingredients = new List<ProductIngredient>
            {
                CreateIngredient("Farinha de trigo enriquecida"),
                CreateIngredient("Açúcar"),
                CreateIngredient("Gordura vegetal hidrogenada"),
                CreateIngredient("Xarope de glicose"),
                CreateIngredient("Cacau em pó"),
                CreateIngredient("Amido de milho"),
                CreateIngredient("Sal"),
                CreateIngredient("Emulsificante lecitina de soja"),
                CreateIngredient("Aromatizante"),
                CreateIngredient("Corante caramelo"),
                CreateIngredient("Estabilizante carbonato de cálcio"),
                CreateIngredient("Fermento químico bicarbonato de sódio"),
                CreateIngredient("Acidulante ácido cítrico"),
                CreateIngredient("Conservante sorbato de potássio"),
                CreateIngredient("Realçador de sabor glutamato monossódico"),
                CreateIngredient("Antioxidante BHT"),
                CreateIngredient("Corante artificial"),
                CreateIngredient("Maltodextrina"),
                CreateIngredient("Soro de leite"),
                CreateIngredient("Gordura de palma"),
                CreateIngredient("Glucose"),
                CreateIngredient("Espessante goma xantana")
            };

            // Calcula scores
            double generalScore = _engine.CalculateGeneralScore(nutrition, ingredients);
            string classification = _engine.DetermineClassification(generalScore);
            string breakdown = _engine.GenerateScoreBreakdown(nutrition, ingredients);

            // Resultados
            Console.WriteLine(breakdown);
            Console.WriteLine($"\n✅ Score Final: {generalScore:F1}/100");
            Console.WriteLine($"✅ Classificação: {classification}");
            Console.WriteLine($"✅ Validação: {(generalScore < 40 ? "PASSOU" : "FALHOU")} (esperado < 40)");
            
            // Análise
            Console.WriteLine("\n📊 Análise:");
            Console.WriteLine($"- Açúcar muito alto (28g)");
            Console.WriteLine($"- Gordura trans presente (0.5g) → CRÍTICO");
            Console.WriteLine($"- Gordura hidrogenada → CRÍTICO");
            Console.WriteLine($"- 22 ingredientes (ultraprocessado)");
            Console.WriteLine($"- 6+ aditivos químicos");
            Console.WriteLine($"- Baixa fibra (1g)");
            Console.WriteLine($"- Baixa proteína (4g)");
            Console.WriteLine("\n🚨 RESULTADO: Produto EVITÁVEL\n");
        }

        /// <summary>
        /// EXEMPLO 2: Iogurte Natural (Produto Saudável)
        /// Esperado: Score > 70 (Good ou Excellent)
        /// </summary>
        public void ValidateHealthyYogurt()
        {
            Console.WriteLine("=== EXEMPLO 2: Iogurte Natural ===\n");

            var nutrition = CreateNutrition(
                calories: 60m,
                totalFat: 3m,
                saturatedFat: 2m,
                transFat: 0m,
                sodium: 50m,
                carbs: 4.5m,
                fiber: 0m,
                sugars: 4m,
                protein: 6m
            );

            var ingredients = new List<ProductIngredient>
            {
                CreateIngredient("Leite integral"),
                CreateIngredient("Fermento lácteo")
            };

            double generalScore = _engine.CalculateGeneralScore(nutrition, ingredients);
            string classification = _engine.DetermineClassification(generalScore);
            string breakdown = _engine.GenerateScoreBreakdown(nutrition, ingredients);

            Console.WriteLine(breakdown);
            Console.WriteLine($"\n✅ Score Final: {generalScore:F1}/100");
            Console.WriteLine($"✅ Classificação: {classification}");
            Console.WriteLine($"✅ Validação: {(generalScore >= 70 ? "PASSOU" : "FALHOU")} (esperado ≥ 70)");
            
            Console.WriteLine("\n📊 Análise:");
            Console.WriteLine($"- Baixo açúcar (4g)");
            Console.WriteLine($"- Sem gordura trans");
            Console.WriteLine($"- Sem gordura hidrogenada");
            Console.WriteLine($"- Mínimo processamento (2 ingredientes)");
            Console.WriteLine($"- Sem aditivos");
            Console.WriteLine($"- Boa fonte de proteína (6g)");
            Console.WriteLine($"- Baixo sódio (50mg)");
            Console.WriteLine("\n✅ RESULTADO: Produto ADEQUADO\n");
        }

        /// <summary>
        /// EXEMPLO 3: Refrigerante (Alto Açúcar)
        /// Esperado: Score < 30 (Avoid)
        /// </summary>
        public void ValidateSoda()
        {
            Console.WriteLine("=== EXEMPLO 3: Refrigerante ===\n");

            var nutrition = CreateNutrition(
                calories: 180m,
                totalFat: 0m,
                saturatedFat: 0m,
                transFat: 0m,
                sodium: 45m,
                carbs: 46m,
                fiber: 0m,
                sugars: 46m,
                protein: 0m
            );

            var ingredients = new List<ProductIngredient>
            {
                CreateIngredient("Água gaseificada"),
                CreateIngredient("Açúcar"),
                CreateIngredient("Extrato de cola"),
                CreateIngredient("Corante caramelo"),
                CreateIngredient("Acidulante ácido fosfórico"),
                CreateIngredient("Aromatizante"),
                CreateIngredient("Conservante benzoato de sódio"),
                CreateIngredient("Cafeína")
            };

            double generalScore = _engine.CalculateGeneralScore(nutrition, ingredients);
            string classification = _engine.DetermineClassification(generalScore);
            string breakdown = _engine.GenerateScoreBreakdown(nutrition, ingredients);

            Console.WriteLine(breakdown);
            Console.WriteLine($"\n✅ Score Final: {generalScore:F1}/100");
            Console.WriteLine($"✅ Classificação: {classification}");
            Console.WriteLine($"✅ Validação: {(generalScore < 30 ? "PASSOU" : "FALHOU")} (esperado < 30)");
            
            Console.WriteLine("\n📊 Análise:");
            Console.WriteLine($"- Açúcar EXTREMAMENTE ALTO (46g)");
            Console.WriteLine($"- Zero fibra");
            Console.WriteLine($"- Zero proteína");
            Console.WriteLine($"- Múltiplos aditivos");
            Console.WriteLine($"- Apenas calorias vazias");
            Console.WriteLine("\n🚨 RESULTADO: Produto EVITÁVEL\n");
        }

        /// <summary>
        /// EXEMPLO 4: Score Personalizado para Diabético
        /// Produto com açúcar moderado - Geral vs Personalizado
        /// </summary>
        public void ValidateDiabeticPersonalization()
        {
            Console.WriteLine("=== EXEMPLO 4: Personalização para Diabético ===\n");

            var nutrition = CreateNutrition(
                calories: 250m,
                totalFat: 8m,
                saturatedFat: 3m,
                transFat: 0m,
                sodium: 300m,
                carbs: 38m,
                fiber: 2m,
                sugars: 12m,
                protein: 5m
            );

            var ingredients = new List<ProductIngredient>
            {
                CreateIngredient("Farinha de trigo"),
                CreateIngredient("Açúcar"),
                CreateIngredient("Óleo vegetal"),
                CreateIngredient("Maltodextrina"),
                CreateIngredient("Sal"),
                CreateIngredient("Fermento"),
                CreateIngredient("Aromatizante")
            };

            // Perfil diabético
            var profile = CreateDiabeticProfile();

            // Calcula ambos scores
            double generalScore = _engine.CalculateGeneralScore(nutrition, ingredients);
            double personalizedScore = _engine.CalculatePersonalizedScore(nutrition, ingredients, new List<ProductAllergen>(), profile);

            Console.WriteLine($"Score Geral: {generalScore:F1}/100");
            Console.WriteLine($"Score Personalizado (Diabético): {personalizedScore:F1}/100");
            Console.WriteLine($"Diferença: {(generalScore - personalizedScore):F1} pontos");
            
            Console.WriteLine($"\n✅ Classificação Geral: {_engine.DetermineClassification(generalScore)}");
            Console.WriteLine($"✅ Classificação Personalizada: {_engine.DetermineClassification(personalizedScore)}");
            
            Console.WriteLine("\n📊 Análise:");
            Console.WriteLine($"- Açúcar moderado (12g) → Penalização FORTE para diabéticos (-25 pts)");
            Console.WriteLine($"- Maltodextrina presente → Penalização adicional (-20 pts)");
            Console.WriteLine($"- Total de penalização: ~45 pontos");
            Console.WriteLine("\n⚠️ RESULTADO: Inadequado para DIABÉTICOS\n");
        }

        /// <summary>
        /// EXEMPLO 5: Produto com Alergênico Crítico
        /// Esperado: Score personalizado = 0
        /// </summary>
        public void ValidateCriticalAllergen()
        {
            Console.WriteLine("=== EXEMPLO 5: Alergênico Crítico (Lactose) ===\n");

            var nutrition = CreateNutrition(
                calories: 150m,
                totalFat: 7m,
                saturatedFat: 4m,
                transFat: 0m,
                sodium: 180m,
                carbs: 18m,
                fiber: 1m,
                sugars: 10m,
                protein: 4m
            );

            var ingredients = new List<ProductIngredient>
            {
                CreateIngredient("Leite integral"),
                CreateIngredient("Açúcar"),
                CreateIngredient("Cacau"),
                CreateIngredient("Emulsificante lecitina de soja")
            };

            var allergens = new List<ProductAllergen>
            {
                CreateAllergen("Leite"),
                CreateAllergen("Lactose")
            };

            // Perfil com intolerância à lactose
            var profile = CreateLactoseIntolerantProfile();

            double generalScore = _engine.CalculateGeneralScore(nutrition, ingredients);
            double personalizedScore = _engine.CalculatePersonalizedScore(nutrition, ingredients, allergens, profile);

            Console.WriteLine($"Score Geral: {generalScore:F1}/100");
            Console.WriteLine($"Score Personalizado (Intolerante Lactose): {personalizedScore:F1}/100");
            
            Console.WriteLine($"\n✅ Classificação Geral: {_engine.DetermineClassification(generalScore)}");
            Console.WriteLine($"✅ Classificação Personalizada: {_engine.DetermineClassification(personalizedScore)}");
            Console.WriteLine($"✅ Validação: {(personalizedScore == 0 ? "PASSOU" : "FALHOU")} (esperado = 0)");
            
            Console.WriteLine("\n📊 Análise:");
            Console.WriteLine($"- Contém lactose → VIOLAÇÃO CRÍTICA");
            Console.WriteLine($"- Score personalizado forçado a 0");
            Console.WriteLine("\n🚨 RESULTADO: INADEQUADO para perfil\n");
        }

        /// <summary>
        /// EXEMPLO 6: Cereal Matinal "Saudável" (Marketing vs Realidade)
        /// Esperado: Score < 50 (Attention ou Avoid)
        /// </summary>
        public void ValidateMisleadingCereal()
        {
            Console.WriteLine("=== EXEMPLO 6: Cereal Matinal 'Integral' ===\n");

            var nutrition = CreateNutrition(
                calories: 380m,
                totalFat: 3m,
                saturatedFat: 0.5m,
                transFat: 0m,
                sodium: 450m,
                carbs: 82m,
                fiber: 4m,
                sugars: 22m,
                protein: 8m
            );

            var ingredients = new List<ProductIngredient>
            {
                CreateIngredient("Farinha de trigo integral"),
                CreateIngredient("Açúcar"),
                CreateIngredient("Xarope de glicose"),
                CreateIngredient("Maltodextrina"),
                CreateIngredient("Sal"),
                CreateIngredient("Extrato de malte"),
                CreateIngredient("Aromatizante"),
                CreateIngredient("Vitaminas e minerais"),
                CreateIngredient("Corante caramelo"),
                CreateIngredient("Antioxidante BHT")
            };

            double generalScore = _engine.CalculateGeneralScore(nutrition, ingredients);
            string classification = _engine.DetermineClassification(generalScore);
            string breakdown = _engine.GenerateScoreBreakdown(nutrition, ingredients);

            Console.WriteLine(breakdown);
            Console.WriteLine($"\n✅ Score Final: {generalScore:F1}/100");
            Console.WriteLine($"✅ Classificação: {classification}");
            Console.WriteLine($"✅ Validação: {(generalScore < 50 ? "PASSOU" : "FALHOU")} (esperado < 50)");
            
            Console.WriteLine("\n📊 Análise:");
            Console.WriteLine($"- Marketing: 'Integral e Nutritivo'");
            Console.WriteLine($"- Realidade: Açúcar muito alto (22g)");
            Console.WriteLine($"- 3 tipos de açúcar (açúcar, xarope, maltodextrina)");
            Console.WriteLine($"- Alto sódio (450mg)");
            Console.WriteLine($"- Múltiplos aditivos");
            Console.WriteLine($"- Fibra insuficiente para compensar açúcar");
            Console.WriteLine("\n⚠️ RESULTADO: Marketing ENGANOSO\n");
        }

        /// <summary>
        /// EXEMPLO 7: Comparação Antes x Depois (Sistema Antigo vs Novo)
        /// </summary>
        public void ValidateBeforeAfterComparison()
        {
            Console.WriteLine("=== EXEMPLO 7: ANTES x DEPOIS ===\n");
            Console.WriteLine("Comparando biscoito recheado no sistema antigo vs novo\n");

            var nutrition = CreateNutrition(
                calories: 480m,
                saturatedFat: 9m,
                transFat: 0.5m,
                sodium: 420m,
                fiber: 1m,
                sugars: 28m,
                protein: 4m
            );

            var ingredients = CreateUltraProcessedIngredients(22);

            double newScore = _engine.CalculateGeneralScore(nutrition, ingredients);

            Console.WriteLine("🔴 SISTEMA ANTIGO:");
            Console.WriteLine("   Score: ~60/100 (0.6/1.0)");
            Console.WriteLine("   Classificação: Moderate ou Safe");
            Console.WriteLine("   Problema: Produto ruim classificado como OK\n");

            Console.WriteLine("🟢 SISTEMA NOVO:");
            Console.WriteLine($"   Score: {newScore:F1}/100");
            Console.WriteLine($"   Classificação: {_engine.DetermineClassification(newScore)}");
            Console.WriteLine("   Correção: Produto ruim classificado como Avoid\n");

            Console.WriteLine("✅ VALIDAÇÃO:");
            Console.WriteLine($"   Sistema corrigiu o problema: {(newScore < 40 ? "SIM" : "NÃO")}");
            Console.WriteLine($"   Diferença de score: ~{(60 - newScore):F0} pontos\n");
        }

        #region Helper Methods

        private NutritionalInfo CreateNutrition(
            decimal calories = 0,
            decimal totalFat = 0,
            decimal saturatedFat = 0,
            decimal transFat = 0,
            decimal sodium = 0,
            decimal carbs = 0,
            decimal fiber = 0,
            decimal sugars = 0,
            decimal protein = 0)
        {
            var nutrition = new NutritionalInfo(Guid.NewGuid());
            nutrition.UpdateMacros(
                calories: calories,
                totalFat: totalFat,
                satFat: saturatedFat,
                transFat: transFat,
                sodium: sodium,
                carbs: carbs,
                fiber: fiber,
                sugars: sugars,
                protein: protein
            );
            return nutrition;
        }

        private ProductIngredient CreateIngredient(string name)
        {
            return new ProductIngredient(Guid.NewGuid(), name, 0);
        }

        private ProductAllergen CreateAllergen(string name)
        {
            return new ProductAllergen(Guid.NewGuid(), name);
        }

        private UserProfile CreateDiabeticProfile()
        {
            return new UserProfile(
                userId: Guid.NewGuid(),
                goal: GoalType.DiabeticFriendly,
                diabetes: true
            );
        }

        private UserProfile CreateLactoseIntolerantProfile()
        {
            return new UserProfile(
                userId: Guid.NewGuid(),
                goal: GoalType.MaintainWeight,
                lactoseIntolerance: true
            );
        }

        private List<ProductIngredient> CreateUltraProcessedIngredients(int count)
        {
            var ingredients = new List<ProductIngredient>();
            var names = new[]
            {
                "Farinha", "Açúcar", "Gordura hidrogenada", "Xarope", "Sal",
                "Aromatizante", "Corante", "Emulsificante", "Conservante",
                "Acidulante", "Estabilizante", "Realçador de sabor", "Espessante",
                "Maltodextrina", "Glucose", "Antioxidante", "Fermento",
                "Soro de leite", "Cacau", "Amido", "Lecitina", "BHT"
            };

            for (int i = 0; i < Math.Min(count, names.Length); i++)
            {
                ingredients.Add(CreateIngredient(names[i]));
            }

            return ingredients;
        }

        #endregion

        /// <summary>
        /// Executa todos os exemplos de validação
        /// </summary>
        public void RunAllValidations()
        {
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║   VALIDAÇÃO DO MOTOR DE SCORE NUTRICIONAL                     ║");
            Console.WriteLine("║   Sistema baseado em pesos reais (0-100)                      ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");

            ValidateUltraProcessedCookie();
            ValidateHealthyYogurt();
            ValidateSoda();
            ValidateDiabeticPersonalization();
            ValidateCriticalAllergen();
            ValidateMisleadingCereal();
            ValidateBeforeAfterComparison();

            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║   VALIDAÇÃO COMPLETA                                          ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        }
    }

    /// <summary>
    /// Programa de exemplo para executar validações
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            var validator = new ScoringValidationExamples();
            validator.RunAllValidations();

            Console.WriteLine("\nPressione qualquer tecla para sair...");
            Console.ReadKey();
        }
    }
}
