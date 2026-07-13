using System;
using System.Collections.Generic;
using System.Linq;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Presentation
{
    /// <summary>
    /// Motor de apresentação final para resultados nutricionais.
    /// Transforma dados técnicos em informações claras e acionáveis para o usuário final.
    /// </summary>
    public class NutritionPresentationEngine
    {
        /// <summary>
        /// Processa o resultado da análise nutricional e gera uma apresentação refinada.
        /// </summary>
        public static NutritionPresentationResult ProcessForPresentation(NutritionAnalysisResponseDto analysis)
        {
            var mainOffender = IdentifyMainOffender(analysis.EstimatedNutritionProfile);
            var refinedScore = CalculateRefinedScore(analysis, mainOffender);
            var refinedSummary = BuildRefinedSummary(analysis, mainOffender, refinedScore);
            var alerts = BuildContextualAlerts(analysis, mainOffender);

            return new NutritionPresentationResult
            {
                Score = refinedScore,
                Summary = refinedSummary,
                Alerts = alerts,
                MainOffender = mainOffender,
                OriginalAnalysis = analysis
            };
        }

        #region Main Offender Detection

        private static NutrientOffender? IdentifyMainOffender(EstimatedNutritionProfileDto? profile)
        {
            if (profile == null) return null;

            var offenders = new List<NutrientOffenderCandidate>();

            // Açúcar - limite crítico: >15g/100g
            if (profile.EstimatedSugarPer100g > 15)
            {
                var severity = CalculateSugarSeverity(profile.EstimatedSugarPer100g ?? 0);
                offenders.Add(new NutrientOffenderCandidate
                {
                    Nutrient = "Açúcar",
                    Value = profile.EstimatedSugarPer100g ?? 0,
                    Unit = "g",
                    Severity = severity,
                    ImpactMessage = BuildSugarImpactMessage(profile.EstimatedSugarPer100g ?? 0)
                });
            }

            // Sódio - limite crítico: >600mg/100g
            if (profile.EstimatedSodiumPer100g > 600)
            {
                var severity = CalculateSodiumSeverity(profile.EstimatedSodiumPer100g ?? 0);
                offenders.Add(new NutrientOffenderCandidate
                {
                    Nutrient = "Sódio",
                    Value = profile.EstimatedSodiumPer100g ?? 0,
                    Unit = "mg",
                    Severity = severity,
                    ImpactMessage = BuildSodiumImpactMessage(profile.EstimatedSodiumPer100g ?? 0)
                });
            }

            // Gordura - limite crítico: >20g/100g
            if (profile.EstimatedFatPer100g > 20)
            {
                var severity = CalculateFatSeverity(profile.EstimatedFatPer100g ?? 0);
                offenders.Add(new NutrientOffenderCandidate
                {
                    Nutrient = "Gordura",
                    Value = profile.EstimatedFatPer100g ?? 0,
                    Unit = "g",
                    Severity = severity,
                    ImpactMessage = BuildFatImpactMessage(profile.EstimatedFatPer100g ?? 0)
                });
            }

            // Retornar o ofensor mais severo
            var mainOffender = offenders.OrderByDescending(o => o.Severity).FirstOrDefault();
            
            return mainOffender != null ? new NutrientOffender
            {
                Nutrient = mainOffender.Nutrient,
                Value = mainOffender.Value,
                Unit = mainOffender.Unit,
                Severity = mainOffender.Severity,
                ImpactMessage = mainOffender.ImpactMessage
            } : null;
        }

        private static int CalculateSugarSeverity(double sugar)
        {
            return sugar switch
            {
                > 50 => 95,  // Açúcar extremamente alto (>50g)
                > 30 => 85,  // Açúcar muito alto (30-50g)
                > 20 => 70,  // Açúcar alto (20-30g)
                > 15 => 55,  // Açúcar moderadamente alto (15-20g)
                _ => 40
            };
        }

        private static int CalculateSodiumSeverity(double sodium)
        {
            return sodium switch
            {
                > 1200 => 90,  // Sódio extremamente alto
                > 900 => 75,   // Sódio muito alto
                > 600 => 60,   // Sódio alto
                _ => 45
            };
        }

        private static int CalculateFatSeverity(double fat)
        {
            return fat switch
            {
                > 35 => 85,  // Gordura extremamente alta
                > 25 => 70,  // Gordura muito alta
                > 20 => 55,  // Gordura alta
                _ => 40
            };
        }

        private static string BuildSugarImpactMessage(double sugar)
        {
            return sugar switch
            {
                > 50 => "Nível extremamente elevado de açúcar. Pode comprometer seriamente o controle glicêmico.",
                > 30 => "Alto teor de açúcar. Impacto significativo na glicemia e ganho de peso.",
                > 20 => "Teor elevado de açúcar. Pode afetar o controle de peso e saúde metabólica.",
                > 15 => "Açúcar acima do recomendado. Consumo deve ser moderado.",
                _ => "Teor de açúcar dentro dos limites aceitáveis."
            };
        }

        private static string BuildSodiumImpactMessage(double sodium)
        {
            return sodium switch
            {
                > 1200 => "Sódio muito elevado. Não recomendado para hipertensos.",
                > 900 => "Alto teor de sódio. Risco para pressão arterial.",
                > 600 => "Sódio acima do ideal. Consumo ocasional apenas.",
                _ => "Teor de sódio aceitável."
            };
        }

        private static string BuildFatImpactMessage(double fat)
        {
            return fat switch
            {
                > 35 => "Gordura extremamente alta. Alta densidade calórica.",
                > 25 => "Teor elevado de gordura. Impacto no controle de peso.",
                > 20 => "Gordura acima do ideal. Consumo controlado recomendado.",
                _ => "Teor de gordura aceitável."
            };
        }

        #endregion

        #region Refined Score Calculation

        private static RefinedNutritionalScore CalculateRefinedScore(
            NutritionAnalysisResponseDto analysis, 
            NutrientOffender? mainOffender)
        {
            int baseScore = 100;

            // Penalidades por classificação
            baseScore += CalculateClassificationImpact(analysis.Classification);

            // Penalidades por ofensor principal (com peso maior)
            if (mainOffender != null)
            {
                baseScore += CalculateOffenderImpact(mainOffender);
            }

            // Penalidades nutricionais adicionais
            baseScore += CalculateNutritionImpact(analysis.EstimatedNutritionProfile);

            // Ajustes por categoria
            baseScore += CalculateCategoryAdjustment(analysis.Category, mainOffender);

            // Bonificações por aspectos positivos
            baseScore += CalculatePositiveAspects(analysis.EstimatedNutritionProfile);

            // Normalizar entre 0 e 100
            var finalScore = Math.Max(0, Math.Min(100, baseScore));

            // Aplicar cap por categoria/offender para produtos problemáticos
            finalScore = ApplyCategoryAndOffenderCaps(finalScore, analysis.Category, mainOffender);

            return BuildRefinedScoreDto(finalScore, mainOffender, analysis);
        }

        private static int CalculateClassificationImpact(ProductClassificationDto? classification)
        {
            if (classification == null) return -10;

            var profiles = new List<HealthProfileResult?>
            {
                classification.Diabetic,
                classification.BloodPressure,
                classification.WeightLoss,
                classification.MuscleGain
            }.Where(p => p != null).ToList();

            int impact = 0;

            var naoRecomendadoCount = profiles.Count(p => 
                p!.Status.Equals("nao_recomendado", StringComparison.OrdinalIgnoreCase));
            var consumoModeradoCount = profiles.Count(p => 
                p!.Status.Equals("consumo_moderado", StringComparison.OrdinalIgnoreCase));
            var fracoCount = profiles.Count(p => 
                p!.Status.Equals("fraco", StringComparison.OrdinalIgnoreCase));

            // Penalidades progressivas
            if (naoRecomendadoCount >= 3) impact -= 40;
            else if (naoRecomendadoCount >= 2) impact -= 30;
            else if (naoRecomendadoCount >= 1) impact -= 20;

            if (consumoModeradoCount >= 3) impact -= 20;
            else if (consumoModeradoCount >= 2) impact -= 12;
            else if (consumoModeradoCount == 1) impact -= 6;

            if (fracoCount >= 2) impact -= 15;
            else if (fracoCount == 1) impact -= 8;

            return impact;
        }

        private static int CalculateOffenderImpact(NutrientOffender offender)
        {
            // Impacto baseado na severidade do ofensor principal
            return offender.Severity switch
            {
                >= 90 => -45,  // Extremamente severo
                >= 80 => -35,  // Muito severo
                >= 70 => -28,  // Severo
                >= 60 => -20,  // Moderadamente severo
                >= 50 => -15,  // Levemente severo
                _ => -10
            };
        }

        private static int CalculateNutritionImpact(EstimatedNutritionProfileDto? profile)
        {
            if (profile == null) return -5;

            int impact = 0;

            // Penalidades por densidade calórica excessiva
            if (profile.CaloriesPer100g > 500) impact -= 10;
            else if (profile.CaloriesPer100g > 400) impact -= 6;
            else if (profile.CaloriesPer100g > 300) impact -= 3;

            return impact;
        }

        private static int CalculateCategoryAdjustment(string? category, NutrientOffender? offender)
        {
            if (string.IsNullOrWhiteSpace(category)) return 0;

            var categoryLower = category.ToLowerInvariant();
            int adjustment = 0;

            // Penalidades adicionais para categorias problemáticas
            if (categoryLower.Contains("sobremesa") || categoryLower.Contains("doce"))
                adjustment -= 8;
            if (categoryLower.Contains("ultraprocessado"))
                adjustment -= 12;
            if (categoryLower.Contains("achocolatado") && offender?.Nutrient == "Açúcar")
                adjustment -= 10; // Penalidade extra para achocolatado com alto açúcar

            return adjustment;
        }

        private static int CalculatePositiveAspects(EstimatedNutritionProfileDto? profile)
        {
            if (profile == null) return 0;

            int bonus = 0;

            // Bonificações por aspectos positivos
            if (profile.EstimatedProteinPer100g > 20) bonus += 8;
            else if (profile.EstimatedProteinPer100g > 15) bonus += 5;
            else if (profile.EstimatedProteinPer100g > 10) bonus += 3;

            if (profile.EstimatedFiberPer100g > 8) bonus += 6;
            else if (profile.EstimatedFiberPer100g > 5) bonus += 4;
            else if (profile.EstimatedFiberPer100g > 3) bonus += 2;

            // Baixo açúcar é um ponto positivo
            if (profile.EstimatedSugarPer100g < 5) bonus += 5;
            else if (profile.EstimatedSugarPer100g < 10) bonus += 3;

            // Baixo sódio é um ponto positivo
            if (profile.EstimatedSodiumPer100g < 150) bonus += 5;
            else if (profile.EstimatedSodiumPer100g < 300) bonus += 3;

            return bonus;
        }

        private static int ApplyCategoryAndOffenderCaps(int score, string? category, NutrientOffender? offender)
        {
            if (string.IsNullOrWhiteSpace(category)) return score;

            var categoryLower = category.ToLowerInvariant();

            // Caps específicos para categorias problemáticas
            var caps = new Dictionary<string, int>
            {
                ["achocolatado"] = 48,
                ["sobremesa láctea"] = 42,
                ["biscoito recheado"] = 38,
                ["refrigerante"] = 30,
                ["salgadinho"] = 35,
                ["chocolate"] = 45
            };

            foreach (var cap in caps)
            {
                if (categoryLower.Contains(cap.Key))
                {
                    return Math.Min(score, cap.Value);
                }
            }

            // Cap adicional para produtos com ofensores severos
            if (offender != null && offender.Severity >= 85)
            {
                return Math.Min(score, 45);
            }

            return score;
        }

        private static RefinedNutritionalScore BuildRefinedScoreDto(
            int score, 
            NutrientOffender? offender,
            NutritionAnalysisResponseDto analysis)
        {
            var dto = new RefinedNutritionalScore { Value = score };

            // Definir status, cor e label baseado no score refinado
            if (score >= 80)
            {
                dto.Status = "excelente";
                dto.Color = "#22c55e"; // green-500
                dto.Label = "Excelente escolha";
                dto.Recommendation = "Produto nutricionalmente adequado para consumo regular.";
            }
            else if (score >= 65)
            {
                dto.Status = "bom";
                dto.Color = "#84cc16"; // lime-500
                dto.Label = "Boa escolha";
                dto.Recommendation = "Produto com perfil nutricional satisfatório.";
            }
            else if (score >= 50)
            {
                dto.Status = "moderado";
                dto.Color = "#f59e0b"; // amber-500
                dto.Label = "Consumo com atenção";
                dto.Recommendation = offender != null 
                    ? $"Atenção ao {offender.Nutrient.ToLowerInvariant()}. Consumo ocasional recomendado."
                    : "Consumo deve ser moderado devido ao perfil nutricional.";
            }
            else if (score >= 35)
            {
                dto.Status = "ruim";
                dto.Color = "#f97316"; // orange-500
                dto.Label = "Evitar consumo frequente";
                dto.Recommendation = offender != null 
                    ? $"Alto teor de {offender.Nutrient.ToLowerInvariant()}. Reservar para ocasiões especiais."
                    : "Perfil nutricional não recomendado para consumo regular.";
            }
            else
            {
                dto.Status = "muito_ruim";
                dto.Color = "#ef4444"; // red-500
                dto.Label = "Evitar";
                dto.Recommendation = offender != null 
                    ? $"Teor crítico de {offender.Nutrient.ToLowerInvariant()}. Não recomendado."
                    : "Perfil nutricional inadequado. Buscar alternativas mais saudáveis.";
            }

            return dto;
        }

        #endregion

        #region Refined Summary Generation

        private static string BuildRefinedSummary(
            NutritionAnalysisResponseDto analysis,
            NutrientOffender? mainOffender,
            RefinedNutritionalScore score)
        {
            var parts = new List<string>();

            // Parte 1: Identificação do produto
            var productName = analysis.ProductName ?? "Produto";
            parts.Add(productName);

            // Parte 2: Principal característica nutricional
            if (mainOffender != null)
            {
                var offenderDescription = BuildOffenderDescription(mainOffender);
                parts.Add(offenderDescription);
            }
            else if (analysis.EstimatedNutritionProfile != null)
            {
                var generalDescription = BuildGeneralNutritionalDescription(analysis.EstimatedNutritionProfile);
                if (!string.IsNullOrWhiteSpace(generalDescription))
                {
                    parts.Add(generalDescription);
                }
            }

            // Parte 3: Contexto de análise
            var analysisContext = BuildAnalysisContext(analysis.AnalysisMode, analysis.EstimatedNutritionProfile);
            if (!string.IsNullOrWhiteSpace(analysisContext))
            {
                parts.Add(analysisContext);
            }

            // Parte 4: Recomendação final
            if (score.Value < 50)
            {
                parts.Add(BuildCriticalRecommendation(mainOffender));
            }

            return string.Join(". ", parts) + ".";
        }

        private static string BuildOffenderDescription(NutrientOffender offender)
        {
            var valueText = $"{offender.Value:0.#}{offender.Unit}/100g";
            
            return offender.Nutrient switch
            {
                "Açúcar" when offender.Value > 50 => $"contém teor extremamente elevado de açúcar ({valueText})",
                "Açúcar" when offender.Value > 30 => $"apresenta alto teor de açúcar ({valueText})",
                "Açúcar" when offender.Value > 20 => $"possui quantidade elevada de açúcar ({valueText})",
                "Açúcar" => $"contém açúcar acima do recomendado ({valueText})",
                
                "Sódio" when offender.Value > 1200 => $"possui teor muito elevado de sódio ({valueText})",
                "Sódio" when offender.Value > 900 => $"apresenta alto teor de sódio ({valueText})",
                "Sódio" => $"contém sódio acima do ideal ({valueText})",
                
                "Gordura" when offender.Value > 35 => $"possui teor extremamente alto de gordura ({valueText})",
                "Gordura" when offender.Value > 25 => $"apresenta alto teor de gordura ({valueText})",
                "Gordura" => $"contém gordura acima do ideal ({valueText})",
                
                _ => $"apresenta {offender.Nutrient.ToLowerInvariant()} elevado"
            };
        }

        private static string BuildGeneralNutritionalDescription(EstimatedNutritionProfileDto profile)
        {
            // Se não há ofensores críticos, destacar aspectos positivos ou neutros
            if (profile.EstimatedProteinPer100g > 15)
            {
                return $"oferece bom aporte proteico ({profile.EstimatedProteinPer100g:0.#}g/100g)";
            }

            if (profile.EstimatedFiberPer100g > 6)
            {
                return $"fonte de fibras ({profile.EstimatedFiberPer100g:0.#}g/100g)";
            }

            if (profile.CaloriesPer100g < 150)
            {
                return "apresenta baixa densidade calórica";
            }

            if (profile.EstimatedSugarPer100g < 5 && profile.EstimatedSodiumPer100g < 300)
            {
                return "possui perfil nutricional equilibrado";
            }

            return "perfil nutricional dentro da faixa esperada para a categoria";
        }

        private static string BuildAnalysisContext(AnalysisMode mode, EstimatedNutritionProfileDto? profile)
        {
            if (mode == AnalysisMode.FrontOfPackageOnly || 
                profile?.Basis?.Contains("categoria", StringComparison.OrdinalIgnoreCase) == true)
            {
                return "Análise baseada na categoria do produto devido à ausência de tabela nutricional legível";
            }

            // Verificar se há leitura real ou estimativa
            var hasRealData = profile?.CaloriesPer100g > 0 || 
                              profile?.EstimatedSugarPer100g >= 0 ||
                              profile?.EstimatedProteinPer100g >= 0;

            if (hasRealData)
            {
                return "Dados extraídos da tabela nutricional presente no rótulo";
            }

            return string.Empty;
        }

        private static string BuildCriticalRecommendation(NutrientOffender? offender)
        {
            if (offender == null)
            {
                return "Produto não recomendado para consumo regular";
            }

            return offender.Nutrient switch
            {
                "Açúcar" => "Não recomendado para diabéticos ou quem busca controle de peso",
                "Sódio" => "Não adequado para hipertensos ou dietas com restrição de sal",
                "Gordura" => "Evitar em dietas de emagrecimento devido à alta densidade calórica",
                _ => "Consumo deve ser restrito a ocasiões especiais"
            };
        }

        #endregion

        #region Contextual Alerts

        private static List<string> BuildContextualAlerts(
            NutritionAnalysisResponseDto analysis,
            NutrientOffender? mainOffender)
        {
            var alerts = new List<string>();

            // Alerta principal sobre o ofensor
            if (mainOffender != null)
            {
                alerts.Add($"⚠️ {mainOffender.ImpactMessage}");
            }

            // Alertas secundários por perfil de saúde
            if (analysis.Classification != null)
            {
                AddHealthProfileAlerts(alerts, analysis.Classification, mainOffender);
            }

            // Alerta sobre qualidade da análise
            if (analysis.AnalysisMode == AnalysisMode.FrontOfPackageOnly)
            {
                alerts.Add("ℹ️ Valores nutricionais estimados por categoria. Para análise precisa, capture a tabela nutricional.");
            }

            return alerts;
        }

        private static void AddHealthProfileAlerts(
            List<string> alerts,
            ProductClassificationDto classification,
            NutrientOffender? offender)
        {
            // Diabéticos
            if (classification.Diabetic?.Status == "nao_recomendado" && offender?.Nutrient == "Açúcar")
            {
                alerts.Add("🚫 Não recomendado para diabéticos devido ao alto teor de açúcar.");
            }
            else if (classification.Diabetic?.Status == "consumo_moderado")
            {
                alerts.Add("⚠️ Diabéticos devem consumir com moderação e monitorar glicemia.");
            }

            // Hipertensos
            if (classification.BloodPressure?.Status == "nao_recomendado" && offender?.Nutrient == "Sódio")
            {
                alerts.Add("🚫 Não adequado para hipertensos devido ao alto teor de sódio.");
            }
            else if (classification.BloodPressure?.Status == "consumo_moderado")
            {
                alerts.Add("⚠️ Hipertensos devem limitar o consumo devido ao sódio.");
            }

            // Emagrecimento
            if (classification.WeightLoss?.Status == "nao_recomendado")
            {
                if (offender?.Nutrient == "Açúcar")
                {
                    alerts.Add("🚫 Não recomendado para emagrecimento: alto teor de açúcar e calorias vazias.");
                }
                else if (offender?.Nutrient == "Gordura")
                {
                    alerts.Add("🚫 Não recomendado para emagrecimento: alta densidade calórica.");
                }
                else
                {
                    alerts.Add("🚫 Não adequado para dietas de emagrecimento.");
                }
            }
        }

        #endregion

        #region Helper Classes

        private class NutrientOffenderCandidate
        {
            public string Nutrient { get; set; } = string.Empty;
            public double Value { get; set; }
            public string Unit { get; set; } = string.Empty;
            public int Severity { get; set; }
            public string ImpactMessage { get; set; } = string.Empty;
        }

        #endregion
    }

    #region Result DTOs

    public class NutritionPresentationResult
    {
        public RefinedNutritionalScore Score { get; set; } = new();
        public string Summary { get; set; } = string.Empty;
        public List<string> Alerts { get; set; } = new();
        public NutrientOffender? MainOffender { get; set; }
        public NutritionAnalysisResponseDto OriginalAnalysis { get; set; } = new();
    }

    public class RefinedNutritionalScore
    {
        public int Value { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
    }

    public class NutrientOffender
    {
        public string Nutrient { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public int Severity { get; set; }
        public string ImpactMessage { get; set; } = string.Empty;
    }

    #endregion
}
