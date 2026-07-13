using System;
using System.Collections.Generic;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Presentation;
using LabelWise.Domain.Enums;

namespace LabelWise.Examples
{
    /// <summary>
    /// Exemplos práticos de uso do NutritionSummaryRefiner.
    /// </summary>
    public class NutritionSummaryRefinerExamples
    {
        /// <summary>
        /// Exemplo 1: Produto com alto teor de açúcar (Achocolatado).
        /// </summary>
        public static void Example1_HighSugarProduct()
        {
            Console.WriteLine("=== EXEMPLO 1: PRODUTO COM ALTO AÇÚCAR ===\n");

            // Simulando um achocolatado com açúcar elevado
            var productName = "Achocolatado em Pó Fortificado";
            var category = "Achocolatado";
            var analysisMode = AnalysisMode.FrontOfPackageOnly;

            var nutrition = new EstimatedNutritionProfileDto
            {
                CaloriesPer100g = 380,
                EstimatedSugarPer100g = 75,
                EstimatedProteinPer100g = 4,
                EstimatedSodiumPer100g = 150,
                EstimatedFiberPer100g = 3,
                EstimatedFatPer100g = 2.5,
                Basis = "Estimativa baseada na categoria"
            };

            var classification = new ProductClassificationDto
            {
                Diabetic = new HealthProfileResult
                {
                    Status = "nao_recomendado",
                    Reason = "Alto teor de açúcar"
                },
                WeightLoss = new HealthProfileResult
                {
                    Status = "nao_recomendado",
                    Reason = "Alta densidade calórica e açúcar"
                },
                BloodPressure = new HealthProfileResult
                {
                    Status = "consumo_moderado",
                    Reason = "Sódio moderado"
                },
                MuscleGain = new HealthProfileResult
                {
                    Status = "fraco",
                    Reason = "Baixo teor proteico"
                }
            };

            // Refinando o summary
            var summary = NutritionSummaryRefiner.RefineSummary(
                productName, category, analysisMode, nutrition, classification
            );

            Console.WriteLine($"Summary Refinado:\n{summary}\n");

            // Refinando o score (simulando score original de 55)
            var response = new NutritionAnalysisResponseDto
            {
                ProductName = productName,
                Category = category,
                EstimatedNutritionProfile = nutrition,
                Classification = classification
            };

            var refinedScore = NutritionSummaryRefiner.RefineScore(response, originalScore: 55);

            Console.WriteLine($"Score Refinado:");
            Console.WriteLine($"  Valor: {refinedScore.Value} (original: 55)");
            Console.WriteLine($"  Label: {refinedScore.Label}");
            Console.WriteLine($"  Status: {refinedScore.Status}");
            Console.WriteLine($"  Cor: {refinedScore.Color}");
            Console.WriteLine($"  Recomendação: {refinedScore.Recommendation}");
            Console.WriteLine();
        }

        /// <summary>
        /// Exemplo 2: Produto equilibrado (Arroz Integral).
        /// </summary>
        public static void Example2_BalancedProduct()
        {
            Console.WriteLine("=== EXEMPLO 2: PRODUTO EQUILIBRADO ===\n");

            var productName = "Arroz Integral Tipo 1";
            var category = "Arroz Integral";
            var analysisMode = AnalysisMode.FullNutritionLabel;

            var nutrition = new EstimatedNutritionProfileDto
            {
                CaloriesPer100g = 360,
                EstimatedSugarPer100g = 0.5,
                EstimatedProteinPer100g = 7.5,
                EstimatedSodiumPer100g = 5,
                EstimatedFiberPer100g = 4,
                EstimatedFatPer100g = 2.5,
                Basis = "Dados extraídos da tabela nutricional"
            };

            var classification = new ProductClassificationDto
            {
                Diabetic = new HealthProfileResult
                {
                    Status = "adequado",
                    Reason = "Baixo teor de açúcar"
                },
                WeightLoss = new HealthProfileResult
                {
                    Status = "adequado",
                    Reason = "Boa fonte de fibras"
                },
                BloodPressure = new HealthProfileResult
                {
                    Status = "adequado",
                    Reason = "Baixo teor de sódio"
                },
                MuscleGain = new HealthProfileResult
                {
                    Status = "consumo_moderado",
                    Reason = "Fonte de carboidratos para energia"
                }
            };

            var summary = NutritionSummaryRefiner.RefineSummary(
                productName, category, analysisMode, nutrition, classification
            );

            Console.WriteLine($"Summary Refinado:\n{summary}\n");

            var response = new NutritionAnalysisResponseDto
            {
                ProductName = productName,
                Category = category,
                EstimatedNutritionProfile = nutrition,
                Classification = classification
            };

            var refinedScore = NutritionSummaryRefiner.RefineScore(response, originalScore: 72);

            Console.WriteLine($"Score Refinado:");
            Console.WriteLine($"  Valor: {refinedScore.Value} (original: 72)");
            Console.WriteLine($"  Label: {refinedScore.Label}");
            Console.WriteLine($"  Status: {refinedScore.Status}");
            Console.WriteLine($"  Cor: {refinedScore.Color}");
            Console.WriteLine();
        }

        /// <summary>
        /// Exemplo 3: Sobremesa com açúcar extremo.
        /// </summary>
        public static void Example3_ExtremeSugarProduct()
        {
            Console.WriteLine("=== EXEMPLO 3: SOBREMESA COM AÇÚCAR EXTREMO ===\n");

            var productName = "Sobremesa Láctea";
            var category = "Sobremesa Láctea";
            var analysisMode = AnalysisMode.FullNutritionLabel;

            var nutrition = new EstimatedNutritionProfileDto
            {
                CaloriesPer100g = 150,
                EstimatedSugarPer100g = 50,
                EstimatedProteinPer100g = 3,
                EstimatedSodiumPer100g = 120,
                EstimatedFiberPer100g = 0.5,
                EstimatedFatPer100g = 4,
                Basis = "Dados extraídos da tabela nutricional"
            };

            var classification = new ProductClassificationDto
            {
                Diabetic = new HealthProfileResult
                {
                    Status = "nao_recomendado",
                    Reason = "Açúcar extremamente elevado"
                },
                WeightLoss = new HealthProfileResult
                {
                    Status = "nao_recomendado",
                    Reason = "Alta densidade de açúcar e calorias vazias"
                },
                BloodPressure = new HealthProfileResult
                {
                    Status = "adequado",
                    Reason = "Sódio dentro dos limites"
                },
                MuscleGain = new HealthProfileResult
                {
                    Status = "fraco",
                    Reason = "Baixíssimo teor proteico"
                }
            };

            var summary = NutritionSummaryRefiner.RefineSummary(
                productName, category, analysisMode, nutrition, classification
            );

            Console.WriteLine($"Summary Refinado:\n{summary}\n");

            var response = new NutritionAnalysisResponseDto
            {
                ProductName = productName,
                Category = category,
                EstimatedNutritionProfile = nutrition,
                Classification = classification
            };

            var refinedScore = NutritionSummaryRefiner.RefineScore(response, originalScore: 48);

            Console.WriteLine($"Score Refinado:");
            Console.WriteLine($"  Valor: {refinedScore.Value} (original: 48)");
            Console.WriteLine($"  Label: {refinedScore.Label}");
            Console.WriteLine($"  Status: {refinedScore.Status}");
            Console.WriteLine($"  Cor: {refinedScore.Color}");
            Console.WriteLine($"  Recomendação: {refinedScore.Recommendation}");
            Console.WriteLine();
        }

        /// <summary>
        /// Exemplo 4: Correção de textos técnicos.
        /// </summary>
        public static void Example4_TechnicalTextFixes()
        {
            Console.WriteLine("=== EXEMPLO 4: CORREÇÃO DE TEXTOS TÉCNICOS ===\n");

            var textsToFix = new List<string>
            {
                "fibras não legível na imagem",
                "tabela nutricional não visível",
                "Estimated sugar Per100g: 50",
                "Valores estimated baseados em categoria"
            };

            Console.WriteLine("Textos Originais → Corrigidos:\n");

            foreach (var text in textsToFix)
            {
                var fixed = NutritionSummaryRefiner.FixTechnicalText(text);
                Console.WriteLine($"❌ {text}");
                Console.WriteLine($"✅ {fixed}\n");
            }
        }

        /// <summary>
        /// Exemplo 5: Comparação de labels em diferentes faixas de score.
        /// </summary>
        public static void Example5_LabelComparison()
        {
            Console.WriteLine("=== EXEMPLO 5: COMPARAÇÃO DE LABELS ===\n");

            var testCases = new List<(int score, double sugar, double sodium, string expectedLabel)>
            {
                (85, 5, 100, "Excelente escolha"),
                (70, 8, 250, "Boa escolha"),
                (55, 18, 400, "Consumo com atenção"),     // Alto açúcar
                (55, 5, 700, "Consumo com atenção"),      // Alto sódio
                (55, 5, 200, "Consumo moderado"),         // Sem problemas específicos
                (40, 25, 500, "Evitar consumo frequente"),
                (25, 40, 800, "Não recomendado")
            };

            Console.WriteLine("Score | Açúcar | Sódio  | Label Gerada");
            Console.WriteLine("------|--------|--------|------------------------------------");

            foreach (var (score, sugar, sodium, expectedLabel) in testCases)
            {
                var response = new NutritionAnalysisResponseDto
                {
                    EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                    {
                        EstimatedSugarPer100g = sugar,
                        EstimatedSodiumPer100g = sodium
                    }
                };

                var refinedScore = NutritionSummaryRefiner.RefineScore(response, score);
                var match = refinedScore.Label == expectedLabel ? "✅" : "⚠️";

                Console.WriteLine($"{match} {score,-4} | {sugar,-6}g | {sodium,-6}mg | {refinedScore.Label}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Executa todos os exemplos.
        /// </summary>
        public static void RunAllExamples()
        {
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  EXEMPLOS DE USO: NUTRITION SUMMARY REFINER                   ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            Example1_HighSugarProduct();
            Console.WriteLine(new string('─', 65));
            Console.WriteLine();

            Example2_BalancedProduct();
            Console.WriteLine(new string('─', 65));
            Console.WriteLine();

            Example3_ExtremeSugarProduct();
            Console.WriteLine(new string('─', 65));
            Console.WriteLine();

            Example4_TechnicalTextFixes();
            Console.WriteLine(new string('─', 65));
            Console.WriteLine();

            Example5_LabelComparison();
            Console.WriteLine(new string('─', 65));
            Console.WriteLine();

            Console.WriteLine("✅ Todos os exemplos executados com sucesso!");
        }
    }

    /// <summary>
    /// Programa principal para executar os exemplos.
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                NutritionSummaryRefinerExamples.RunAllExamples();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao executar exemplos: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine();
            Console.WriteLine("Pressione qualquer tecla para sair...");
            Console.ReadKey();
        }
    }
}
