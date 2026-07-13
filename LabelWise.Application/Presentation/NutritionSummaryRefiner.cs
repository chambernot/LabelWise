using System;
using System.Collections.Generic;
using System.Linq;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Presentation
{
    /// <summary>
    /// Refinador de apresentação nutricional focado em clareza e usabilidade para app mobile.
    /// Transforma dados técnicos em mensagens diretas e acionáveis para o usuário final.
    /// </summary>
    public static class NutritionSummaryRefiner
    {
        /// <summary>
        /// Refina o summary para ser mais direto e destacar o principal problema.
        /// Linguagem coerente com classificação e score.
        /// </summary>
        public static string RefineSummary(
            string? productName,
            string? category,
            AnalysisMode analysisMode,
            EstimatedNutritionProfileDto? nutrition,
            ProductClassificationDto? classification)
        {
            var mainIssue = IdentifyMainIssue(nutrition, classification, category);
            var dataSource = GetDataSourceDescription(analysisMode, nutrition);
            var coherenceNote = BuildCoherenceNote(nutrition, classification);

            var parts = new List<string>();

            // Parte 1: Nome do produto
            var product = !string.IsNullOrWhiteSpace(productName) ? productName : "Produto";
            parts.Add(product);

            // Parte 2: Principal problema nutricional (se houver)
            if (!string.IsNullOrWhiteSpace(mainIssue))
            {
                parts.Add(mainIssue);
            }
            else
            {
                // Se não há problema, destacar ponto positivo
                var positiveAspect = IdentifyPositiveAspect(nutrition);
                if (!string.IsNullOrWhiteSpace(positiveAspect))
                {
                    parts.Add(positiveAspect);
                }
            }

            // Parte 2.5: Nota de coerência com classificação (se aplicável)
            if (!string.IsNullOrWhiteSpace(coherenceNote))
            {
                parts.Add(coherenceNote);
            }

            // Parte 3: Fonte dos dados (apenas se relevante)
            if (!string.IsNullOrWhiteSpace(dataSource))
            {
                parts.Add(dataSource);
            }

            return string.Join(". ", parts) + ".";
        }

        /// <summary>
        /// Constrói nota de coerência quando há classificação "não recomendado".
        /// Torna o summary mais direto e alinhado com score baixo.
        /// </summary>
        private static string? BuildCoherenceNote(
            EstimatedNutritionProfileDto? nutrition,
            ProductClassificationDto? classification)
        {
            if (nutrition == null || classification == null)
            {
                return null;
            }

            var sugar = nutrition.EstimatedSugarPer100g ?? 0;
            var sodium = nutrition.EstimatedSodiumPer100g ?? 0;

            // Se há alto açúcar e classificação "não recomendado" para diabéticos
            if (sugar > 20 && classification.Diabetic?.Status == "nao_recomendado")
            {
                return "Principal ponto de atenção: açúcar elevado, não adequado para diabéticos";
            }

            // Se há alto sódio e classificação "não recomendado" para hipertensos
            if (sodium > 800 && classification.BloodPressure?.Status == "nao_recomendado")
            {
                return "Principal ponto de atenção: sódio elevado, não adequado para hipertensos";
            }

            // Se há múltiplas classificações "não recomendado"
            var naoRecomendadoCount = 0;
            if (classification.Diabetic?.Status == "nao_recomendado") naoRecomendadoCount++;
            if (classification.BloodPressure?.Status == "nao_recomendado") naoRecomendadoCount++;
            if (classification.WeightLoss?.Status == "nao_recomendado") naoRecomendadoCount++;

            if (naoRecomendadoCount >= 2)
            {
                return "Perfil nutricional inadequado para consumo regular";
            }

            return null;
        }

        /// <summary>
        /// Identifica o principal problema nutricional de forma direta.
        /// </summary>
        private static string? IdentifyMainIssue(
            EstimatedNutritionProfileDto? nutrition,
            ProductClassificationDto? classification,
            string? category)
        {
            if (nutrition == null) return null;

            var issues = new List<(string issue, int severity)>();

            // Açúcar: principal vilão em produtos doces
            var sugar = nutrition.EstimatedSugarPer100g ?? 0;
            if (sugar > 50)
            {
                issues.Add(($"Contém açúcar extremamente elevado ({sugar:0.#}g/100g)", 95));
            }
            else if (sugar > 30)
            {
                issues.Add(($"Alto teor de açúcar ({sugar:0.#}g/100g)", 85));
            }
            else if (sugar > 20)
            {
                issues.Add(($"Açúcar elevado ({sugar:0.#}g/100g)", 70));
            }
            else if (sugar > 15)
            {
                issues.Add(($"Açúcar acima do recomendado ({sugar:0.#}g/100g)", 60));
            }

            // Sódio: importante para hipertensos
            var sodium = nutrition.EstimatedSodiumPer100g ?? 0;
            if (sodium > 1200)
            {
                issues.Add(($"Teor muito elevado de sódio ({sodium:0}mg/100g)", 90));
            }
            else if (sodium > 900)
            {
                issues.Add(($"Alto teor de sódio ({sodium:0}mg/100g)", 75));
            }
            else if (sodium > 600)
            {
                issues.Add(($"Sódio acima do ideal ({sodium:0}mg/100g)", 60));
            }

            // Gordura: relevante para densidade calórica
            var fat = nutrition.EstimatedFatPer100g ?? 0;
            if (fat > 35)
            {
                issues.Add(($"Gordura extremamente elevada ({fat:0.#}g/100g)", 85));
            }
            else if (fat > 25)
            {
                issues.Add(($"Alto teor de gordura ({fat:0.#}g/100g)", 70));
            }
            else if (fat > 20)
            {
                issues.Add(($"Gordura acima do ideal ({fat:0.#}g/100g)", 55));
            }

            // Retornar o problema mais severo
            var mainIssue = issues.OrderByDescending(i => i.severity).FirstOrDefault();
            return mainIssue.issue;
        }

        /// <summary>
        /// Identifica aspectos positivos quando não há problemas críticos.
        /// </summary>
        private static string? IdentifyPositiveAspect(EstimatedNutritionProfileDto? nutrition)
        {
            if (nutrition == null) return null;

            var protein = nutrition.EstimatedProteinPer100g ?? 0;
            if (protein > 20)
            {
                return $"Excelente fonte de proteínas ({protein:0.#}g/100g)";
            }
            else if (protein > 15)
            {
                return $"Boa fonte de proteínas ({protein:0.#}g/100g)";
            }

            var fiber = nutrition.EstimatedFiberPer100g ?? 0;
            if (fiber > 8)
            {
                return $"Rico em fibras ({fiber:0.#}g/100g)";
            }
            else if (fiber > 5)
            {
                return $"Fonte de fibras ({fiber:0.#}g/100g)";
            }

            var sugar = nutrition.EstimatedSugarPer100g ?? 0;
            var sodium = nutrition.EstimatedSodiumPer100g ?? 0;
            if (sugar < 5 && sodium < 300)
            {
                return "Perfil nutricional equilibrado";
            }

            return "Perfil dentro da faixa esperada para a categoria";
        }

        /// <summary>
        /// Descreve a fonte dos dados de forma clara.
        /// </summary>
        private static string? GetDataSourceDescription(AnalysisMode mode, EstimatedNutritionProfileDto? nutrition)
        {
            if (mode == AnalysisMode.FrontOfPackageOnly)
            {
                return "Valores estimados por categoria (tabela nutricional não legível)";
            }

            // Se tem dados reais, mencionar
            var hasRealData = nutrition?.CaloriesPer100g > 0 ||
                              nutrition?.EstimatedSugarPer100g >= 0 ||
                              nutrition?.EstimatedProteinPer100g >= 0;

            if (hasRealData)
            {
                // Contar campos extraídos
                var fieldsCount = 0;
                if (nutrition?.CaloriesPer100g > 0) fieldsCount++;
                if (nutrition?.EstimatedSugarPer100g >= 0) fieldsCount++;
                if (nutrition?.EstimatedProteinPer100g >= 0) fieldsCount++;
                if (nutrition?.EstimatedSodiumPer100g >= 0) fieldsCount++;
                if (nutrition?.EstimatedFatPer100g >= 0) fieldsCount++;
                if (nutrition?.EstimatedFiberPer100g >= 0) fieldsCount++;

                if (fieldsCount >= 5)
                {
                    return "Dados extraídos da tabela nutricional";
                }
                else if (fieldsCount >= 2)
                {
                    return "Dados parciais da tabela nutricional";
                }
            }

            return null;
        }

        /// <summary>
        /// Refina o score nutricional com recalibração para produtos doces.
        /// </summary>
        public static RefinedScore RefineScore(
            NutritionAnalysisResponseDto analysis,
            int originalScore)
        {
            var sugar = analysis.EstimatedNutritionProfile?.EstimatedSugarPer100g ?? 0;
            var sodium = analysis.EstimatedNutritionProfile?.EstimatedSodiumPer100g ?? 0;
            var fat = analysis.EstimatedNutritionProfile?.EstimatedFatPer100g ?? 0;
            var calories = analysis.EstimatedNutritionProfile?.CaloriesPer100g ?? 0;

            var categoryLower = (analysis.Category ?? "").ToLowerInvariant();
            var finalScore = originalScore;

            // Recalibração para produtos doces com açúcar elevado
            if (sugar > 30)
            {
                // Produtos com muito açúcar não devem passar de 42
                finalScore = Math.Min(finalScore, 42);
            }
            else if (sugar > 20)
            {
                // Açúcar elevado: cap em 48
                finalScore = Math.Min(finalScore, 48);
            }
            else if (sugar > 15)
            {
                // Açúcar moderadamente alto: cap em 52
                finalScore = Math.Min(finalScore, 52);
            }

            // Caps específicos por categoria
            if (categoryLower.Contains("achocolatado"))
            {
                finalScore = Math.Min(finalScore, 45);
            }
            else if (categoryLower.Contains("sobremesa"))
            {
                finalScore = Math.Min(finalScore, 40);
            }
            else if (categoryLower.Contains("biscoito recheado"))
            {
                finalScore = Math.Min(finalScore, 38);
            }
            else if (categoryLower.Contains("refrigerante"))
            {
                finalScore = Math.Min(finalScore, 28);
            }

            // Penalidade adicional por múltiplos problemas
            var problemCount = 0;
            if (sugar > 15) problemCount++;
            if (sodium > 600) problemCount++;
            if (fat > 20) problemCount++;
            if (calories > 400) problemCount++;

            if (problemCount >= 3)
            {
                finalScore -= 8; // Penalidade extra para produtos com múltiplos problemas
            }

            // Normalizar
            finalScore = Math.Max(0, Math.Min(100, finalScore));

            return new RefinedScore
            {
                Value = finalScore,
                Status = GetStatus(finalScore),
                Color = GetColor(finalScore),
                Label = GetFriendlyLabel(finalScore, sugar, sodium),
                Recommendation = GetRecommendation(finalScore, sugar, sodium, fat)
            };
        }

        private static string GetStatus(int score)
        {
            return score switch
            {
                >= 80 => "excelente",
                >= 65 => "bom",
                >= 50 => "moderado",
                >= 35 => "ruim",
                _ => "muito_ruim"
            };
        }

        private static string GetColor(int score)
        {
            return score switch
            {
                >= 80 => "#22c55e", // green-500
                >= 65 => "#84cc16", // lime-500
                >= 50 => "#f59e0b", // amber-500
                >= 35 => "#f97316", // orange-500
                _ => "#ef4444"      // red-500
            };
        }

        /// <summary>
        /// Retorna label amigável para o app mobile.
        /// </summary>
        private static string GetFriendlyLabel(int score, double sugar, double sodium)
        {
            if (score >= 80)
            {
                return "Excelente escolha";
            }
            else if (score >= 65)
            {
                return "Boa escolha";
            }
            else if (score >= 50)
            {
                // Faixa 50-64: "Consumo com atenção" ou "Consumo moderado"
                if (sugar > 15 || sodium > 600)
                {
                    return "Consumo com atenção";
                }
                return "Consumo moderado";
            }
            else if (score >= 35)
            {
                return "Evitar consumo frequente";
            }
            else
            {
                return "Não recomendado";
            }
        }

        /// <summary>
        /// Gera recomendação contextual baseada no score e perfil nutricional.
        /// </summary>
        private static string GetRecommendation(int score, double sugar, double sodium, double fat)
        {
            if (score >= 80)
            {
                return "Produto adequado para consumo regular.";
            }
            else if (score >= 65)
            {
                return "Produto com perfil nutricional satisfatório.";
            }
            else if (score >= 50)
            {
                if (sugar > 15)
                {
                    return "Atenção ao açúcar. Consumo ocasional recomendado.";
                }
                else if (sodium > 600)
                {
                    return "Atenção ao sódio. Consumo ocasional recomendado.";
                }
                return "Consumo deve ser moderado.";
            }
            else if (score >= 35)
            {
                if (sugar > 20)
                {
                    return "Alto teor de açúcar. Reservar para ocasiões especiais.";
                }
                else if (sodium > 900)
                {
                    return "Alto teor de sódio. Evitar se houver hipertensão.";
                }
                return "Perfil nutricional não adequado para consumo regular.";
            }
            else
            {
                if (sugar > 30)
                {
                    return "Açúcar muito elevado. Buscar alternativas mais saudáveis.";
                }
                else if (sodium > 1200)
                {
                    return "Sódio crítico. Não recomendado.";
                }
                return "Perfil nutricional inadequado. Evitar.";
            }
        }

        /// <summary>
        /// Corrige textos técnicos para linguagem natural.
        /// </summary>
        public static string FixTechnicalText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            // Corrigir "não legível" para algo mais amigável
            text = text.Replace("fibras não legível", "fibras não identificadas");
            text = text.Replace("não legível", "não identificado");
            text = text.Replace("não visível", "não identificado");

            // Corrigir termos técnicos
            text = text.Replace("Per100g", " por 100g");
            text = text.Replace("per 100g", " por 100g");

            // Remover "Estimated" que pode aparecer
            text = text.Replace("Estimated", "");
            text = text.Replace("estimated", "");

            return text.Trim();
        }
    }

    /// <summary>
    /// Score refinado com metadados adicionais.
    /// </summary>
    public class RefinedScore
    {
        public int Value { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
    }
}
