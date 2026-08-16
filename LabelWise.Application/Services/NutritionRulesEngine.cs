using LabelWise.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LabelWise.Application.Services
{
    public interface INutritionRulesEngine
    {
        ClientAppResponse ProcessAndSimplify(GeminiRawExtraction extractedData);
    }

    public class NutritionRulesEngine : INutritionRulesEngine
    {
        private readonly HashSet<string> _knownAllergens = new(StringComparer.OrdinalIgnoreCase)
        {
            "soja", "leite", "trigo", "amendoim", "farinha de trigo enriquecida"
        };

        public ClientAppResponse ProcessAndSimplify(GeminiRawExtraction extractedData)
        {
            var response = new ClientAppResponse();
            var facts = extractedData.NutritionFacts;

            // 1. Processar Ingredientes e Alergênicos
            foreach (var ingRaw in extractedData.Ingredients)
            {
                var name = ingRaw.Trim();
                bool isAllergen = _knownAllergens.Any(a => name.Contains(a, StringComparison.OrdinalIgnoreCase));

                if (isAllergen)
                    response.CriticalAlerts.Add($"Contém alergênico: {name}");

                response.IngredientsList.Add(new IngredientItem { Name = name, IsAllergen = isAllergen });
            }

            // 2. Popular Tabela Nutricional
            if (facts != null)
            {
                response.Nutrition.AsLabel = new NutritionValues
                {
                    CaloriesKcal = facts.CaloriesKcal,
                    Carbohydrates = facts.Carbohydrates,
                    Sugars = facts.Sugars,
                    Proteins = facts.Proteins,
                    TotalFats = facts.TotalFats,
                    SaturatedFats = facts.SaturatedFats,
                    TransFats = facts.TransFats,
                    Fiber = facts.Fiber,
                    SodiumMg = facts.SodiumMg
                };

                response.Nutrition.Per100 = response.Nutrition.AsLabel;
            }

            // 3. Avaliação por Perfis (Diabético, Pressão Alta, Emagrecimento, Ganho de Massa)
            var sugarValue = facts?.Sugars ?? 0;
            int diabeticScore = sugarValue > 10 ? 10 : (sugarValue > 5 ? 35 : 80);
            response.Profiles["diabetico"] = new ProfileEvaluation
            {
                Score = diabeticScore,
                Label = diabeticScore < 40 ? "Evitar" : (diabeticScore < 70 ? "Atenção" : "Liberado"),
                Reasons = new List<string> { sugarValue > 5 ? $"Açúcar alto ({sugarValue}g) — evite picos de glicemia" : "Teor de açúcar favorável" }
            };

            var sodiumValue = facts?.SodiumMg ?? 0;
            int hipertensaoScore = sodiumValue > 400 ? 30 : (sodiumValue > 200 ? 50 : 85);
            response.Profiles["hipertensao"] = new ProfileEvaluation
            {
                Score = hipertensaoScore,
                Label = hipertensaoScore < 40 ? "Evitar" : (hipertensaoScore < 70 ? "Atenção" : "Liberado"),
                Reasons = new List<string> { sodiumValue > 200 ? $"Sódio moderado/alto ({sodiumValue}mg)" : "Baixo teor de sódio" }
            };

            var calories = facts?.CaloriesKcal ?? 0;
            int emagrecimentoScore = calories > 250 ? 25 : (calories > 150 ? 50 : 80);
            response.Profiles["emagrecimento"] = new ProfileEvaluation
            {
                Score = emagrecimentoScore,
                Label = emagrecimentoScore < 40 ? "Evitar" : "Atenção",
                Reasons = new List<string> { calories > 150 ? $"Produto calórico ({calories} kcal) — atenção à quantidade" : "Baixas calorias" }
            };

            // --- Perfil: Ganho de Massa (Proteína) ---
            var proteinValue = facts?.Proteins ?? 0;
            int ganhoMassaScore = proteinValue > 10 ? 80 : (proteinValue > 5 ? 60 : 40);
            response.Profiles["ganhoDeMassa"] = new ProfileEvaluation
            {
                Score = ganhoMassaScore,
                Label = ganhoMassaScore < 50 ? "Atenção" : "Liberado",
                Reasons = new List<string> { proteinValue < 5 ? $"Baixo teor de proteína ({proteinValue}g). Complementar com outras fontes." : $"Bom aporte proteico ({proteinValue}g)" }
            };

            // 4. Cálculo Dinâmico do Score Global e Resumo (Média dos 4 perfis)
            int globalScore = (diabeticScore + hipertensaoScore + emagrecimentoScore + ganhoMassaScore) / 4;

            if (facts?.SaturatedFats > 4)
            {
                globalScore -= 10;
            }
            globalScore = Math.Clamp(globalScore, 0, 100);

            string globalLabel;
            string explicacaoScore;

            if (globalScore >= 70)
            {
                globalLabel = "Bom";
                explicacaoScore = $"A nota {globalScore} reflete um perfil equilibrado, com bom suporte nutricional.";
            }
            else if (globalScore >= 40)
            {
                globalLabel = "Atenção";
                explicacaoScore = $"A nota {globalScore} indica um produto intermediário, com pontos de atenção nos macronutrientes.";
            }
            else
            {
                globalLabel = "Muito ruim";
                explicacaoScore = $"A nota {globalScore} reflete restrições importantes na composição nutricional.";
            }

            response.Score.Global = globalScore;
            response.Score.GlobalLabel = globalLabel;
            response.Score.ExplicacaoScore = explicacaoScore;

            // 5. Pontos Positivos e de Atenção Dinâmicos
            if (sodiumValue <= 200) response.Strengths.Add("Baixo teor de sódio");
            if (sugarValue <= 5) response.Strengths.Add("Baixo teor de açúcar");
            if (facts?.Fiber > 2) response.Strengths.Add("Boa fonte de fibra");
            if (proteinValue >= 5) response.Strengths.Add("Boa fonte de proteína");

            if (sugarValue > 5)
            {
                response.Weaknesses.Add($"Açúcar alto ({sugarValue}g). Consumir esporadicamente.");
            }
            if (proteinValue < 5)
            {
                response.Weaknesses.Add($"Baixo teor de proteína ({proteinValue}g). Complementar com outras fontes.");
            }
            if (facts?.SaturatedFats > 4)
            {
                response.Weaknesses.Add($"Gordura saturada alta ({facts.SaturatedFats}g). Consumir com moderação.");
            }

            return response;
        }
    }
}