using System;
using System.Collections.Generic;
using System.Linq;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Scoring
{
    /// <summary>
    /// Calcula um score nutricional determinÃ­stico de 0 a 100 baseado em nÃ­vel de processamento,
    /// penalidades nutricionais e bÃ´nus. O score NÃƒO Ã© dominado por calorias.
    /// </summary>
    public static class NutritionScoreCalculator
    {
        private static readonly string[] InNaturaKeywords =
        {
            "fruta", "legume", "verdura", "folha", "carne", "peixe",
            "frango", "ovo", "in natura", "grÃ£o", "grao", "semente"
        };

        private static readonly string[] MinimallyProcessedKeywords =
        {
            "iogurte natural", "queijo", "aveia", "granola", "arroz", "feijÃ£o",
            "feijao", "lentilha", "ervilha", "leite", "amendoim", "castanha"
        };

        private static readonly string[] ProcessedKeywords =
        {
            "conserva", "defumado", "pÃ£o integral", "pao integral",
            "macarrÃ£o", "macarrao", "azeite", "vinagre", "mel"
        };

        private static readonly string[] UltraProcessedKeywords =
        {
            "salgadinho", "snack", "refrigerante", "achocolatado", "ultraprocessado",
            "biscoito recheado", "nugget", "hamburguer", "hambÃºrguer",
            "mortadela", "salsicha", "refresco", "nÃ©ctar", "nectar"
        };

        private static readonly string[] UltraProcessedIngredientSignals =
        {
            "maltodextrina", "aromatizante", "corante",
            "glutamato monossÃ³dico", "glutamato monosÃ³dico", "xarope de glicose"
        };

        public static NutritionalScore Calculate(NutritionAnalysisResponseDto response)
        {
            if (response == null)
            {
                return BuildUnavailableScore();
            }

            var nutrition = response.EstimatedNutritionProfile;
            if (nutrition == null || !response.HasReliableNutritionData)
            {
                return BuildFallbackScore(response);
            }

            var processingLevel = InferProcessingLevel(response);

            // 1. Score base pelo nÃ­vel de processamento
            var score = processingLevel switch
            {
                "in_natura" => 95,
                "minimamente_processado" => 85,
                "processado" => 65,
                "ultraprocessado" => 40,
                _ => 50
            };

            var sugar = nutrition.EstimatedSugarPer100g ?? 0;
            var addedSugar = nutrition.EstimatedAddedSugarPer100g;
            var saturatedFat = nutrition.EstimatedSaturatedFatPer100g ?? 0;
            var sodium = nutrition.EstimatedSodiumPer100g ?? 0;
            var calories = nutrition.CaloriesPer100g ?? 0;
            var protein = nutrition.EstimatedProteinPer100g ?? 0;
            var fiber = nutrition.EstimatedFiberPer100g ?? 0;

            // 2. Penalidades
            var sugarPenalty = sugar >= 15 ? 25 : sugar >= 10 ? 15 : sugar >= 5 ? 8 : 0;
            var addedSugarPenalty = addedSugar.HasValue
                ? (addedSugar.Value >= 10 ? 30 : addedSugar.Value >= 5 ? 20 : addedSugar.Value > 0 ? 10 : 0)
                : 0;
            var sodiumPenalty = sodium >= 600 ? 20 : sodium >= 300 ? 10 : 0;
            var saturatedFatPenalty = saturatedFat >= 5 ? 15 : saturatedFat >= 2 ? 8 : 0;

            // Calorias com peso reduzido â€” nunca o principal fator
            var caloriesPenalty = calories >= 500 ? 10 : calories >= 300 ? 5 : 0;

            var totalPenalty = sugarPenalty + addedSugarPenalty + sodiumPenalty + saturatedFatPenalty + caloriesPenalty;

            // 3. BÃ´nus nutricionais
            var fiberBonus = fiber >= 8 ? 10 : fiber >= 4 ? 5 : 0;
            var proteinBonus = protein >= 10 ? 8 : protein >= 5 ? 4 : 0;
            var totalBonus = fiberBonus + proteinBonus;

            score = score - totalPenalty + totalBonus;

            // 4. Regras de coerÃªncia obrigatÃ³rias
            // Regra 1: minimamente_processado e score < 70 â†’ ajustar para 70
            if (processingLevel == "minimamente_processado" && score < 70)
            {
                score = 70;
            }

            // Regra 2: aÃ§Ãºcar adicionado > 15g â†’ score mÃ¡ximo = 40
            if (addedSugar.HasValue && addedSugar.Value > 15)
            {
                score = Math.Min(score, 40);
            }

            // 5. NormalizaÃ§Ã£o
            score = Math.Clamp(score, 0, 100);

            // Regra 3: score >= 85 â†’ requiresModeration = false
            var requiresModeration = score < 85;

            var principalOffender = DeterminePrincipalOffender(sugar, addedSugar, sodium, saturatedFat, calories, processingLevel);
            var confidence = DetermineConfidence(response, nutrition);
            var reason = BuildReason(principalOffender, score);
            var recommendation = BuildRecommendation(score, principalOffender);

            return BuildScoreDto(score, processingLevel, confidence, reason, recommendation, requiresModeration,
                processingLevel == "ultraprocessado", principalOffender);
        }

        private static NutritionalScore BuildFallbackScore(NutritionAnalysisResponseDto response)
        {
            var processingLevel = InferProcessingLevel(response);

            var score = processingLevel switch
            {
                "in_natura" => 80,
                "minimamente_processado" => 72,
                "processado" => 55,
                "ultraprocessado" => 38,
                _ => 45
            };

            if (response.InferredRisks.Any(risk => risk.Contains("alto_acucar", StringComparison.OrdinalIgnoreCase)))
            {
                score -= 8;
            }

            if (response.InferredRisks.Any(risk => risk.Contains("alto_sodio", StringComparison.OrdinalIgnoreCase)))
            {
                score -= 8;
            }

            if (response.InferredRisks.Any(risk => risk.Contains("alta_gordura", StringComparison.OrdinalIgnoreCase)))
            {
                score -= 5;
            }

            if (processingLevel == "minimamente_processado" && score < 70)
            {
                score = 70;
            }

            score = Math.Clamp(score, 0, 100);

            var principalOffender = DetermineFallbackPrincipalOffender(response.InferredRisks, processingLevel);
            var reason = BuildReason(principalOffender, score);
            var requiresModeration = score < 85;

            return BuildScoreDto(score, processingLevel, "baixa", reason,
                "Score estimado por categoria. Se puder, envie a tabela nutricional para uma leitura mais precisa.",
                requiresModeration, processingLevel == "ultraprocessado", principalOffender);
        }

        private static NutritionalScore BuildUnavailableScore()
        {
            return new NutritionalScore
            {
                Value = 40,
                Label = "AtenÃ§Ã£o",
                SafeLabel = "AtenÃ§Ã£o",
                Status = "atencao",
                Color = "orange",
                Confidence = "baixa",
                PrincipalOffender = "dados insuficientes",
                Reason = "Dados insuficientes para uma leitura nutricional confiÃ¡vel.",
                AbsoluteRecommendation = "Tente enviar uma imagem com a tabela nutricional visÃ­vel.",
                SemanticRecommendation = "Tente enviar uma imagem com a tabela nutricional visÃ­vel.",
                ScoreInterpretation = "Score estimado com baixa confianÃ§a.",
                AbsoluteLabel = "atencao",
                RecommendationLevel = "atencao",
                RequiresModeration = true
            };
        }

        private static string InferProcessingLevel(NutritionAnalysisResponseDto response)
        {
            var combined = string.Join(" | ", new[]
            {
                response.Category,
                response.ProductName,
                string.Join(" | ", response.VisibleClaims ?? []),
                string.Join(" | ", response.InferredRisks ?? [])
            }
            .Where(v => !string.IsNullOrWhiteSpace(v)))
            .ToLowerInvariant();

            if (ContainsAny(combined, UltraProcessedKeywords))
            {
                return "ultraprocessado";
            }

            var ingredientSignalCount = UltraProcessedIngredientSignals
                .Count(s => combined.Contains(s, StringComparison.OrdinalIgnoreCase));
            if (ingredientSignalCount >= 2)
            {
                return "ultraprocessado";
            }

            if (response.InferredRisks.Any(r => r.Contains("ultra", StringComparison.OrdinalIgnoreCase)))
            {
                return "ultraprocessado";
            }

            if (ContainsAny(combined, InNaturaKeywords))
            {
                return "in_natura";
            }

            if (ContainsAny(combined, MinimallyProcessedKeywords))
            {
                return "minimamente_processado";
            }

            if (ContainsAny(combined, ProcessedKeywords))
            {
                return "processado";
            }

            return "desconhecido";
        }

        private static string DetermineConfidence(NutritionAnalysisResponseDto response, EstimatedNutritionProfileDto nutrition)
        {
            if (!response.HasReliableNutritionData)
            {
                return "baixa";
            }

            var presentCoreFields = new[]
            {
                nutrition.EstimatedSugarPer100g,
                nutrition.EstimatedFatPer100g,
                nutrition.EstimatedSodiumPer100g,
                nutrition.EstimatedProteinPer100g,
                nutrition.EstimatedFiberPer100g
            }.Count(v => v.HasValue);

            return presentCoreFields switch
            {
                >= 5 when response.AnalysisMode == AnalysisMode.FullNutritionLabel => "alta",
                >= 3 => "media",
                _ => "baixa"
            };
        }

        private static string DeterminePrincipalOffender(
            double sugar, double? addedSugar, double sodium, double saturatedFat,
            double calories, string processingLevel)
        {
            // Prioridade 1: acucar adicionado (qualquer valor positivo)
            if (addedSugar.HasValue && addedSugar.Value > 0)
            {
                return "acucar_adicionado";
            }

            // Prioridade 2: acucar total
            if (sugar >= 5)
            {
                return "acucar";
            }

            // Prioridade 3: sodio
            if (sodium >= 300)
            {
                return "sodio";
            }

            // Prioridade 4: gordura saturada
            if (saturatedFat >= 2)
            {
                return "gordura saturada";
            }

            // Prioridade 5: calorias — somente se combinado com outro fator negativo
            var hasOtherNegativeFactor = sodium >= 150 || saturatedFat >= 1 || sugar >= 3;
            if (calories >= 500 && hasOtherNegativeFactor)
            {
                return "calorias";
            }

            if (processingLevel == "ultraprocessado")
            {
                return "ultraprocessado";
            }

            return string.Empty;
        }
        private static string DetermineFallbackPrincipalOffender(IEnumerable<string> inferredRisks, string processingLevel)
        {
            if (inferredRisks.Any(r => r.Contains("acucar", StringComparison.OrdinalIgnoreCase)))
            {
                return "acucar";
            }

            if (inferredRisks.Any(r => r.Contains("sodio", StringComparison.OrdinalIgnoreCase)))
            {
                return "sodio";
            }

            if (inferredRisks.Any(r => r.Contains("gordura", StringComparison.OrdinalIgnoreCase)))
            {
                return "gordura saturada";
            }

            if (processingLevel == "ultraprocessado")
            {
                return "ultraprocessado";
            }

            return string.Empty;
        }
        // 7. Explicacao: reason reflete o principal fator que impactou o score
        private static string BuildReason(string principalOffender, int score)
        {
            if (string.IsNullOrWhiteSpace(principalOffender))
            {
                return "boa qualidade nutricional geral";
            }

            return principalOffender switch
            {
                "acucar_adicionado" => "alto teor de acucar adicionado",
                "acucar"            => "alto teor de acucar",
                "sodio"             => "alto teor de sodio",
                "gordura saturada"  => "alto teor de gordura saturada",
                "calorias"          => "alta densidade calorica",
                "ultraprocessado"   => "produto ultraprocessado",
                _                   => "boa qualidade nutricional geral"
            };
        }
        // 8. RecomendaÃ§Ã£o coerente com o score â€” sem contradiÃ§Ãµes semÃ¢nticas (Regra 4)
        private static string BuildRecommendation(int score, string principalOffender)
        {
            if (score >= 85)
            {
                return "Boa opÃ§Ã£o para o dia a dia.";
            }

            if (score >= 70)
            {
                return string.IsNullOrWhiteSpace(principalOffender)
                    ? "Pode entrar na rotina com tranquilidade."
                    : $"Boa opÃ§Ã£o, mas vale atenÃ§Ã£o ao {principalOffender}.";
            }

            if (score >= 50)
            {
                return string.IsNullOrWhiteSpace(principalOffender)
                    ? "Consuma com moderaÃ§Ã£o."
                    : $"Consuma com moderaÃ§Ã£o, principalmente por causa de {principalOffender}.";
            }

            if (score >= 30)
            {
                return string.IsNullOrWhiteSpace(principalOffender)
                    ? "NÃ£o Ã© uma boa opÃ§Ã£o para consumo frequente."
                    : $"NÃ£o Ã© uma boa opÃ§Ã£o para consumo frequente por causa de {principalOffender}.";
            }

            return string.IsNullOrWhiteSpace(principalOffender)
                ? "Melhor evitar consumo frequente."
                : $"Melhor evitar consumo frequente por causa de {principalOffender}.";
        }

        private static NutritionalScore BuildScoreDto(
            int score,
            string processingLevel,
            string confidence,
            string reason,
            string recommendation,
            bool requiresModeration,
            bool isUltraProcessed,
            string principalOffender)
        {
            // 6. ClassificaÃ§Ã£o final
            string label, status, color;

            if (score >= 85)
            {
                label = "Excelente escolha";
                status = "excelente";
                color = "green";
            }
            else if (score >= 70)
            {
                label = "Boa escolha";
                status = "boa_escolha";
                color = "green";
            }
            else if (score >= 50)
            {
                label = "Consumo moderado";
                status = "consumo_moderado";
                color = "yellow";
            }
            else if (score >= 30)
            {
                label = "Ruim";
                status = "ruim";
                color = "orange";
            }
            else
            {
                label = "NÃ£o recomendado";
                status = "nao_recomendado";
                color = "red";
            }

            var reasonCapitalized = string.IsNullOrEmpty(reason)
                ? string.Empty
                : char.ToUpperInvariant(reason[0]) + reason[1..];

            return new NutritionalScore
            {
                Value = score,
                Label = label,
                SafeLabel = label,
                Status = status,
                Color = color,
                Reason = reason,
                AbsoluteRecommendation = recommendation,
                SemanticRecommendation = recommendation,
                ComparativeRecommendation = string.Empty,
                ScoreInterpretation = $"Score {score}/100. {reasonCapitalized}.",
                AbsoluteLabel = status,
                RecommendationLevel = status,
                ProcessingLevel = processingLevel,
                RequiresModeration = requiresModeration,
                IsUltraProcessed = isUltraProcessed,
                Confidence = confidence,
                PrincipalOffender = principalOffender
            };
        }

        private static bool ContainsAny(string source, IEnumerable<string> keywords)
            => keywords.Any(k => source.Contains(k, StringComparison.OrdinalIgnoreCase));

        private readonly record struct ScoreComponent(string Description, int Points);

        private readonly record struct CategoryProfile(
            bool IgnoreProteinBonus,
            double SugarMultiplier,
            bool PrioritizeSodium,
            int ExtraCategoryPenalty,
            bool IsSweetenedBeverage,
            bool IsUltraProcessed,
            bool IsLight,
            string EvidenceText);
    }
}
