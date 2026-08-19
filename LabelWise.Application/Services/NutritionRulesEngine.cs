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
            "soja", "leite", "trigo", "amendoim", "farinha de trigo", "castanha", "nozes", "ovo", "avelã", "cevada", "centeio", "aveia"
        };

        private readonly HashSet<string> _dairyKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "leite", "queijo", "manteiga", "requeijão", "soro de leite", "lactose", "creme de leite", "caseína"
        };

        public ClientAppResponse ProcessAndSimplify(GeminiRawExtraction extractedData)
        {
            var response = new ClientAppResponse();
            var facts = extractedData.NutritionFacts;

            // 0. Preencher informações do Produto
            response.Product.Name = extractedData.ProductName;
            response.Product.Brand = extractedData.Brand;

            // 1. Processar Ingredientes e Alergênicos
            bool hasDairy = false;
            if (extractedData.Ingredients != null)
            {
                foreach (var ingRaw in extractedData.Ingredients)
                {
                    var name = ingRaw.Trim();
                    bool isAllergen = _knownAllergens.Any(a => name.Contains(a, StringComparison.OrdinalIgnoreCase));

                    if (_dairyKeywords.Any(d => name.Contains(d, StringComparison.OrdinalIgnoreCase)))
                    {
                        hasDairy = true;
                    }

                    if (isAllergen && !response.CriticalAlerts.Contains($"Contém alergênico: {name}", StringComparer.OrdinalIgnoreCase))
                    {
                        response.CriticalAlerts.Add($"Contém alergênico: {name}");
                    }

                    response.IngredientsList.Add(new IngredientItem
                    {
                        Name = name,
                        IsAllergen = isAllergen
                    });
                }
            }

            // 1b. Processar Avisos Explícitos de Alergênicos / Selos
            var allergyReasons = new List<string>();
            bool hasExplicitAllergenRisk = false;
            bool isExplicitGlutenFree = false;

            if (extractedData.AllergenWarnings != null)
            {
                foreach (var warning in extractedData.AllergenWarnings)
                {
                    var cleanWarning = warning.Trim();
                    if (string.IsNullOrWhiteSpace(cleanWarning)) continue;

                    if (cleanWarning.Contains("NÃO CONTÉM GLÚTEN", StringComparison.OrdinalIgnoreCase) ||
                        cleanWarning.Contains("ISENTO DE GLÚTEN", StringComparison.OrdinalIgnoreCase))
                    {
                        isExplicitGlutenFree = true;
                        allergyReasons.Add("Não contém glúten");
                        AddUnique(response.Strengths, "NÃO CONTÉM GLÚTEN");
                    }
                    else if (cleanWarning.Contains("NÃO CONTÉM", StringComparison.OrdinalIgnoreCase) ||
                             cleanWarning.Contains("ISENTO", StringComparison.OrdinalIgnoreCase))
                    {
                        allergyReasons.Add(cleanWarning);
                        AddUnique(response.Strengths, cleanWarning);
                    }
                    else
                    {
                        hasExplicitAllergenRisk = true;
                        AddUnique(response.CriticalAlerts, cleanWarning);
                        allergyReasons.Add(cleanWarning);
                    }
                }
            }

            // 2. Criar Selos / Badges (DietaryBadges)
            if (isExplicitGlutenFree)
            {
                response.DietaryBadges.Add(new DietaryBadge { Type = "gluten_free", IsCompatible = true, Label = "Sem Glúten" });
            }
            if (!hasDairy)
            {
                response.DietaryBadges.Add(new DietaryBadge { Type = "lactose_free", IsCompatible = true, Label = "Sem Lactose" });
            }

            // 3. Extrair Tabela Nutricional
            var valuesPer100 = facts?.Per100g;
            var valuesPerServing = facts?.PerServing;
            var refValues = valuesPer100 ?? valuesPerServing;

            if (facts != null)
            {
                if (valuesPer100 != null) response.Nutrition.Per100 = MapNutritionalValues(valuesPer100);
                if (valuesPerServing != null) response.Nutrition.AsLabel = MapNutritionalValues(valuesPerServing);
                else if (valuesPer100 != null) response.Nutrition.AsLabel = MapNutritionalValues(valuesPer100);
            }

            if (refValues == null)
            {
                response.Score.Global = 50;
                response.Score.GlobalLabel = "Atenção";
                response.Score.ExplicacaoScore = "Tabela nutricional incompleta para cálculo detalhado.";
                return response;
            }

            double calValue = refValues.CaloriesKcal ?? 0;
            double sugarValue = refValues.Sugars ?? 0;
            double addedSugarsValue = refValues.AddedSugars ?? 0;
            double proteinValue = refValues.Proteins ?? 0;
            double fatValue = refValues.TotalFats ?? 0;
            double satFatValue = refValues.SaturatedFats ?? 0;
            double fiberValue = refValues.Fiber ?? 0;
            double sodiumValue = refValues.SodiumMg ?? 0;

            // Selo de Baixo Sódio em Badges
            if (sodiumValue <= 120)
            {
                response.DietaryBadges.Add(new DietaryBadge { Type = "low_sodium", IsCompatible = true, Label = "Baixo Sódio" });
            }

            // Alertas Críticos da ANVISA (RDC 429 - Lupa de Alerta Líquidos: >= 7.5g por 100ml)
            if (addedSugarsValue >= 7.5 || sugarValue >= 15.0)
            {
                AddUnique(response.CriticalAlerts, "Alto em Açúcar Adicionado");
            }
            if (satFatValue >= 3.0)
            {
                AddUnique(response.CriticalAlerts, "Alto em Gordura Saturada");
            }
            if (sodiumValue >= 300)
            {
                AddUnique(response.CriticalAlerts, "Alto em Sódio");
            }

            // 4. Perfis Nutricionais
            int allergyScore = (response.IngredientsList.Any(i => i.IsAllergen) || hasExplicitAllergenRisk) ? 20 : 100;
            response.Profiles["alergias"] = new()
            {
                Score = allergyScore,
                Label = allergyScore == 100 ? "Liberado" : "Evitar",
                Reasons = allergyReasons.Any() ? allergyReasons : new List<string> { "Nenhum alergênico identificado." }
            };

            int diabeticScore = sugarValue <= 5 ? 85 : (sugarValue <= 10 ? 60 : 30);
            response.Profiles["diabetico"] = new()
            {
                Score = diabeticScore,
                Label = diabeticScore >= 75 ? "Liberado" : (diabeticScore >= 50 ? "Atenção" : "Evitar"),
                Reasons = new List<string> { sugarValue <= 5 ? "Teor de açúcar favorável" : $"Açúcar: {sugarValue}g — atenção à glicemia" }
            };

            int hypertensionScore = sodiumValue <= 200 ? 85 : (sodiumValue <= 400 ? 60 : 30);
            response.Profiles["hipertensao"] = new()
            {
                Score = hypertensionScore,
                Label = hypertensionScore >= 85 ? "Liberado" : (hypertensionScore >= 60 ? "Atenção" : "Evitar"),
                Reasons = new List<string> { sodiumValue <= 200 ? "Baixo teor de sódio" : $"Sódio: {sodiumValue}mg" }
            };

            int weightLossScore = calValue <= 100 ? 85 : (calValue <= 250 ? 50 : 30);
            response.Profiles["emagrecimento"] = new()
            {
                Score = weightLossScore,
                Label = weightLossScore >= 85 ? "Liberado" : (weightLossScore >= 50 ? "Atenção" : "Evitar"),
                Reasons = new List<string> { calValue <= 100 ? "Baixas calorias" : $"Valor calórico: {calValue} kcal" }
            };

            // Ajuste para Bebidas/Sucos: Não penalizar pesadamente bebidas de baixas calorias e gordura zero por falta de proteína
            bool isBeverageProfile = calValue <= 100 && fatValue == 0 && proteinValue == 0;
            int muscleGainScore = proteinValue >= 10 ? 85 : (proteinValue >= 5 ? 65 : (isBeverageProfile ? 60 : 40));

            response.Profiles["ganhoDeMassa"] = new()
            {
                Score = muscleGainScore,
                Label = muscleGainScore >= 65 ? "Liberado" : "Atenção",
                Reasons = new List<string> {
                    proteinValue >= 5
                        ? $"Aporte proteico ({proteinValue}g)"
                        : (isBeverageProfile ? "Bebida sem proteínas (esperado para a categoria)" : $"Baixo teor de proteína ({proteinValue}g)")
                }
            };

            // 5. Pontos Fortes e Fracos
            if (sodiumValue <= 200) AddUnique(response.Strengths, "Baixo teor de sódio");
            if (sugarValue <= 5) AddUnique(response.Strengths, "Baixo teor de açúcar");
            if (fiberValue > 2) AddUnique(response.Strengths, "Boa fonte de fibra");
            if (proteinValue >= 5) AddUnique(response.Strengths, "Boa fonte de proteína");

            if (sugarValue > 5) AddUnique(response.Weaknesses, $"Açúcar elevado ({sugarValue}g). Consumir com moderação.");
            if (proteinValue < 5 && !isBeverageProfile) AddUnique(response.Weaknesses, $"Baixo teor de proteína ({proteinValue}g).");
            if (satFatValue > 4) AddUnique(response.Weaknesses, $"Gordura saturada alta ({satFatValue}g).");

            // 6. Score Global Ajustado
            int globalScore = (int)Math.Round((diabeticScore + hypertensionScore + weightLossScore + muscleGainScore + allergyScore) / 5.0);

            string globalLabel = globalScore switch
            {
                >= 85 => "Excelente",
                >= 70 => "Bom",
                >= 50 => "Atenção",
                _ => "Muito ruim"
            };

            response.Score.Global = globalScore;
            response.Score.GlobalLabel = globalLabel;
            response.Score.ExplicacaoScore = $"A nota {globalScore} ({globalLabel}) reflete o equilíbrio geral da composição nutricional do produto.";

            return response;
        }

        private static void AddUnique(List<string> list, string value)
        {
            if (!list.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(value);
            }
        }

        private static NutritionValues MapNutritionalValues(GeminiNutritionalValuesDto src)
        {
            return new NutritionValues
            {
                CaloriesKcal = (decimal)(src.CaloriesKcal ?? 0),
                Carbohydrates = (decimal)(src.Carbohydrates ?? 0),
                Sugars = (decimal)(src.Sugars ?? 0),
                AddedSugars = (decimal)(src.AddedSugars ?? 0),
                Proteins = (decimal)(src.Proteins ?? 0),
                TotalFats = (decimal)(src.TotalFats ?? 0),
                SaturatedFats = (decimal)(src.SaturatedFats ?? 0),
                TransFats = (decimal)(src.TransFats ?? 0),
                Fiber = (decimal)(src.Fiber ?? 0),
                SodiumMg = (decimal)(src.SodiumMg ?? 0)
            };
        }
    }
}