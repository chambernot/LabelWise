using System;
using System.Collections.Generic;
using System.Linq;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Presentation
{
    public static class NutritionTextPresentationBuilder
    {
        public static void Apply(NutritionAnalysisResponseDto response)
        {
            if (response == null || !response.Success)
            {
                return;
            }

            var isFrontOnly = response.AnalysisMode == AnalysisMode.FrontOfPackageOnly;

            var reference = NutritionReferenceRanges.Resolve(response.Category, response.ProductName, response.VisibleClaims);

            ApplyBasis(response.EstimatedNutritionProfile, response.AnalysisMode, null);
            HumanizeClassification(response.Classification, response.EstimatedNutritionProfile, response.AnalysisMode, reference);

            var text = BuildCore(
                response.EstimatedNutritionProfile,
                response.Score?.Value,
                response.AnalysisMode,
                response.PrincipalOffender,
                null,
                reference);

            ApplyText(response, text);

            if (response.Score != null)
            {
                response.Score.Reason = text.ExplicacaoScore;
                response.Score.ScoreInterpretation = BuildScoreInterpretation(response.Score.Value, text, text.SourceNote);
                response.Score.AbsoluteRecommendation = text.Guidance;
                response.Score.SemanticRecommendation = text.Guidance;

                if (isFrontOnly)
                {
                    response.Score.IsUltraProcessed = null;
                }
            }

            response.Warnings = BuildWarningList(text);
            response.Alerts = text.ResumoRapido.ToList();
        }

        public static void Apply(RefactoredNutritionAnalysisResponse response)
        {
            if (response == null || !response.Success)
            {
                return;
            }

            ApplyBasis(response.EstimatedNutritionProfile, response.AnalysisMode, null);
            var reference = NutritionReferenceRanges.Resolve(response.Category, response.ProductName);
            HumanizeClassification(response.Classification, response.EstimatedNutritionProfile, response.AnalysisMode, reference);

            var text = BuildCore(
                response.EstimatedNutritionProfile,
                null,
                response.AnalysisMode,
                null,
                null,
                reference);

            ApplyText(response, text);
            response.Warnings = BuildWarningList(text);
        }

        public static void Apply(EnhancedNutritionAnalysisResult response)
        {
            if (response == null || !response.Success)
            {
                return;
            }

            ApplyBasis(response.EstimatedNutritionProfile, response.AnalysisMode, response.DataSourceType);
            var reference = NutritionReferenceRanges.Resolve(response.Category, response.ProductName, response.VisibleClaims);
            HumanizeClassification(response.Classification, response.EstimatedNutritionProfile, response.AnalysisMode, reference);

            var text = BuildCore(
                response.EstimatedNutritionProfile,
                (int?)Math.Round(response.NutritionalScore, MidpointRounding.AwayFromZero),
                response.AnalysisMode,
                response.PrincipalOffender?.Type,
                response.DataSourceType,
                reference);

            ApplyText(response, text);
            response.Alerts = text.ResumoRapido
                .Concat(BuildWarningList(text))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static void ApplyText(NutritionAnalysisResponseDto response, PresentationText text)
        {
            response.ResumoRapido = text.ResumoRapido.ToList();
            response.ExplicacaoScore = text.ExplicacaoScore;
            response.PontoPrincipal = text.PontoPrincipal;
            response.Tom = "simples e direto";
            response.Summary = BuildSummary(text);
        }

        private static void ApplyText(RefactoredNutritionAnalysisResponse response, PresentationText text)
        {
            response.ResumoRapido = text.ResumoRapido.ToList();
            response.ExplicacaoScore = text.ExplicacaoScore;
            response.PontoPrincipal = text.PontoPrincipal;
            response.Tom = "simples e direto";
            response.Summary = BuildSummary(text);
        }

        private static void ApplyText(EnhancedNutritionAnalysisResult response, PresentationText text)
        {
            response.ResumoRapido = text.ResumoRapido.ToList();
            response.ExplicacaoScore = text.ExplicacaoScore;
            response.PontoPrincipal = text.PontoPrincipal;
            response.Tom = "simples e direto";
            response.Summary = BuildSummary(text);
        }

        private static PresentationText BuildCore(
            EstimatedNutritionProfileDto? nutrition,
            int? score,
            AnalysisMode analysisMode,
            string? principalOffender,
            DataSourceType? dataSourceType,
            NutritionReferenceRange? reference = null)
        {
            if (analysisMode == AnalysisMode.FrontOfPackageOnly)
            {
                return BuildLimitedAnalysisText();
            }

            reference ??= NutritionReferenceRanges.Resolve(null, null);

            var mainConcern = DetermineMainConcern(nutrition, principalOffender, reference);
            var positiveSignals = DeterminePositiveSignals(nutrition, reference);
            var mainPositive = positiveSignals.FirstOrDefault();
            var scoreGuidance = BuildGuidance(score, mainConcern != null);
            var sourceNote = BuildSourceNote(analysisMode, dataSourceType);
            var scoreExplanation = BuildScoreExplanation(mainConcern, positiveSignals, score);
            var mainPoint = mainConcern != null
                ? $"Principal ponto de atenção: {mainConcern.Name}."
                : mainPositive != null
                    ? $"Principal destaque: {mainPositive.Highlight}."
                    : "Principal ponto: melhor consumir com equilíbrio.";

            var resumoRapido = new List<string>();

            if (mainConcern != null)
            {
                resumoRapido.Add(mainConcern.Bullet);
            }
            else if (mainPositive != null)
            {
                resumoRapido.Add(mainPositive.Bullet);
            }
            else
            {
                resumoRapido.Add("Não é a melhor opção para o dia a dia.");
            }

            resumoRapido.Add(scoreGuidance);

            var suggestion = mainConcern != null
                ? mainConcern.Suggestion
                : mainPositive != null
                    ? "Mesmo sendo uma boa opção, vale variar as escolhas."
                    : null;

            if (!string.IsNullOrWhiteSpace(sourceNote))
            {
                resumoRapido.Add(sourceNote!);
            }
            else if (!string.IsNullOrWhiteSpace(suggestion))
            {
                resumoRapido.Add(suggestion!);
            }

            return new PresentationText
            {
                ResumoRapido = resumoRapido
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(3)
                    .ToList(),
                ExplicacaoScore = scoreExplanation,
                PontoPrincipal = mainPoint,
                Guidance = scoreGuidance,
                SourceNote = sourceNote
            };
        }

        private static ConcernSignal? DetermineMainConcern(
            EstimatedNutritionProfileDto? nutrition,
            string? principalOffender,
            NutritionReferenceRange reference)
        {
            var offender = NormalizeOffender(principalOffender);
            var sugar = nutrition?.EstimatedSugarPer100g ?? 0;
            var sodium = nutrition?.EstimatedSodiumPer100g ?? 0;
            var fat = nutrition?.EstimatedFatPer100g ?? 0;
            var saturatedFat = nutrition?.EstimatedSaturatedFatPer100g ?? 0;
            var calories = nutrition?.CaloriesPer100g ?? 0;
            var fiber = nutrition?.EstimatedFiberPer100g ?? 0;
            var protein = nutrition?.EstimatedProteinPer100g ?? 0;

            var concerns = new List<ConcernSignal>();

            // Thresholds derivados do catálogo para a categoria do produto
            // "moderado": 30% acima da média da categoria
            // "elevado": ponto médio entre média e máximo
            // "bastante": acima do máximo da categoria
            var sugarModThreshold  = reference.SugarPer100g.Average * 1.3;
            var sugarHighThreshold = reference.SugarPer100g.Average + (reference.SugarPer100g.Maximum - reference.SugarPer100g.Average) * 0.5;
            var sugarMaxThreshold  = reference.SugarPer100g.Maximum;

            var isDairy = reference.Key == "dairy-solid" || reference.Key == "dairy-liquid";

            if (offender == "açúcar" || sugar > sugarModThreshold)
            {
                var sugarBullet = sugar > sugarMaxThreshold
                    ? "Contém bastante açúcar."
                    : sugar > sugarHighThreshold
                        ? "Tem açúcar elevado."
                        : "Contém açúcar relevante por porção.";
                var sugarPriority = sugar > sugarMaxThreshold ? 100 : sugar > sugarHighThreshold ? 80 : 65;
                if (sugar > protein)
                {
                    sugarPriority += 10;
                }

                // Para laticínios o açúcar é majoritariamente lactose natural — sugestão contextual
                var sugarSuggestion = isDairy
                    ? "Prefira consumir na quantidade recomendada e evite adicionar açúcar ao preparo."
                    : "Prefira versões sem açúcar.";
                concerns.Add(new ConcernSignal("açúcar", sugarBullet, "alto teor de açúcar", sugarPriority, sugarSuggestion));
            }

            var sodiumHighThreshold = reference.SodiumPer100g.Maximum * 0.75;
            if (offender == "sódio" || sodium > sodiumHighThreshold)
            {
                var sodiumBullet = sodium > reference.SodiumPer100g.Maximum ? "Tem muito sódio." : "Tem sódio elevado para a categoria.";
                var sodiumPriority = sodium >= 400 ? 120 : sodium > reference.SodiumPer100g.Maximum ? 95 : 75;
                concerns.Add(new ConcernSignal("sódio", sodiumBullet, "alto teor de sódio", sodiumPriority, "Busque opções com menos sódio."));
            }

            if (offender == "gordura saturada" || saturatedFat > 2)
            {
                var saturatedFatBullet = saturatedFat > 10
                    ? "Tem muita gordura saturada."
                    : saturatedFat > 5
                        ? "Tem gordura saturada elevada."
                        : "Tem gordura saturada relevante.";
                var saturatedFatPriority = saturatedFat > 10 ? 90 : saturatedFat > 5 ? 78 : 68;
                concerns.Add(new ConcernSignal("gordura saturada", saturatedFatBullet, "alto teor de gordura saturada", saturatedFatPriority, "Prefira opções com menos gordura saturada."));
            }

            var fatHighThreshold = reference.FatPer100g.Maximum * 0.75;
            if (offender == "gordura" || fat > fatHighThreshold)
            {
                var fatBullet = fat > reference.FatPer100g.Maximum ? "Tem bastante gordura." : "Tem gordura elevada para a categoria.";
                var fatPriority = fat > reference.FatPer100g.Maximum ? 85 : 65;
                concerns.Add(new ConcernSignal("gordura", fatBullet, "alto teor de gordura", fatPriority, "Prefira opções com menos gordura."));
            }

            var caloriesHighThreshold = reference.CaloriesPer100g.Maximum * 0.75;
            if (offender == "densidade calórica" || calories > caloriesHighThreshold)
            {
                var calPriority = calories > reference.CaloriesPer100g.Maximum ? 70 : 55;
                concerns.Add(new ConcernSignal("calorias", "É um produto bem calórico.", "muitas calorias", calPriority, "Evite consumo frequente."));
            }

            // Fibra e proteína usam thresholds absolutos (independem da categoria)
            if (offender == "baixa fibra" || fiber is > 0 and < 2)
            {
                concerns.Add(new ConcernSignal("fibras", "Tem pouca fibra.", "baixo teor de fibras", 45, "Busque opções mais naturais."));
            }

            if (offender == "baixa proteína" || (protein > 0 && protein < 5 && sugar > sugarModThreshold))
            {
                concerns.Add(new ConcernSignal("proteína", "Tem pouca proteína para compensar o resto.", "baixo teor de proteína", 40, "Se a ideia for saciedade, procure opções com mais proteína."));
            }

            return concerns
                .OrderByDescending(item => item.Priority)
                .FirstOrDefault();
        }

        private static List<PositiveSignal> DeterminePositiveSignals(
            EstimatedNutritionProfileDto? nutrition,
            NutritionReferenceRange reference)
        {
            var signals = new List<PositiveSignal>();
            if (nutrition == null)
            {
                return signals;
            }

            var protein = nutrition.EstimatedProteinPer100g ?? 0;
            var fiber = nutrition.EstimatedFiberPer100g ?? 0;
            var sugar = nutrition.EstimatedSugarPer100g ?? 0;
            var sodium = nutrition.EstimatedSodiumPer100g ?? 0;
            var fat = nutrition.EstimatedFatPer100g ?? 0;

            if (protein >= 20)
            {
                signals.Add(new PositiveSignal("alta proteína", "Tem alta proteína.", "alto teor de proteína", 100));
            }
            else if (protein >= 12)
            {
                signals.Add(new PositiveSignal("boa proteína", "Tem boa quantidade de proteína.", "bom teor de proteína", 85));
            }

            if (fiber >= 5)
            {
                signals.Add(new PositiveSignal("boas fibras", "Tem boa quantidade de fibras.", "bom teor de fibras", 90));
            }

            // Sinal positivo de açúcar: apenas se claramente abaixo da média da categoria (não trivialmente verdadeiro)
            if (sugar <= reference.SugarPer100g.Average * 0.5 && reference.SugarPer100g.Average > 2)
            {
                signals.Add(new PositiveSignal("baixo açúcar", "Baixo teor de açúcar.", "baixo teor de açúcar", 70));
            }

            // Sinal positivo de sódio: apenas se significativamente abaixo do esperado para a categoria
            if (sodium <= reference.SodiumPer100g.Average * 0.4 && reference.SodiumPer100g.Average > 50)
            {
                signals.Add(new PositiveSignal("baixo sódio", "Baixo teor de sódio.", "baixo teor de sódio", 65));
            }

            // Sinal positivo de gordura: apenas se claramente abaixo do esperado para a categoria
            if (fat <= reference.FatPer100g.Average * 0.4 && reference.FatPer100g.Average > 3)
            {
                signals.Add(new PositiveSignal("baixa gordura", "Baixo teor de gordura.", "baixo teor de gordura", 60));
            }

            return signals
                .OrderByDescending(item => item.Priority)
                .ToList();
        }

        private static string BuildGuidance(int? score, bool hasConcern)
        {
            if (score.HasValue)
            {
                return score.Value switch
                {
                    >= 81 => "Boa opção para o dia a dia.",
                    >= 61 when hasConcern => "Melhor consumir com moderação.",
                    >= 61 => "Pode entrar na rotina com equilíbrio.",
                    >= 41 => "Consuma com atenção.",
                    >= 21 => "Não é uma boa opção para consumo frequente.",
                    _ => "Melhor evitar consumo frequente."
                };
            }

            return hasConcern
                ? "Melhor consumir com moderação."
                : "Pode entrar na rotina com equilíbrio.";
        }

        private static string BuildScoreExplanation(ConcernSignal? mainConcern, IReadOnlyCollection<PositiveSignal> positiveSignals, int? score)
        {
            if (mainConcern != null)
            {
                return $"Pontuação reduzida por {mainConcern.ScoreReason}.";
            }

            var topPositives = positiveSignals
                .Take(2)
                .Select(item => item.ScoreReason)
                .ToList();

            if (topPositives.Count >= 2)
            {
                return $"Boa pontuação por {topPositives[0]} e {topPositives[1]}.";
            }

            if (topPositives.Count == 1)
            {
                return $"Boa pontuação por {topPositives[0]}.";
            }

            if (score.HasValue && score.Value < 40)
            {
                return "Pontuação baixa por pouco equilíbrio entre açúcar, gordura, sódio e calorias.";
            }

            return "Pontuação baseada em açúcar, gordura, sódio e calorias.";
        }

        private static List<string> BuildWarningList(PresentationText text)
        {
            return new[]
            {
                text.SourceNote,
                text.Guidance,
                text.ResumoRapido.LastOrDefault()
            }
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()!;
        }

        private static string BuildSummary(PresentationText text)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(text.ExplicacaoScore))
            {
                parts.Add(text.ExplicacaoScore);
            }

            // Inclui PontoPrincipal apenas se não repete o mesmo nutriente já citado em ExplicacaoScore
            if (!string.IsNullOrWhiteSpace(text.PontoPrincipal)
                && !SharesKeyNutrientMention(text.ExplicacaoScore, text.PontoPrincipal))
            {
                parts.Add(text.PontoPrincipal);
            }

            if (!string.IsNullOrWhiteSpace(text.SourceNote))
            {
                parts.Add(text.SourceNote!);
            }

            return string.Join(" ", parts);
        }

        private static bool SharesKeyNutrientMention(string? a, string? b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;

            var keywords = new[] { "açúcar", "acucar", "sódio", "sodio", "gordura", "gordura saturada", "calorias", "proteína", "proteina", "fibra" };
            return keywords.Any(k =>
                a.Contains(k, StringComparison.OrdinalIgnoreCase) &&
                b.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        private static string BuildScoreInterpretation(int score, PresentationText text, string? sourceNote)
        {
            var parts = new List<string>
            {
                $"Score {score}/100.",
                text.ExplicacaoScore
            };

            if (!string.IsNullOrWhiteSpace(sourceNote))
            {
                parts.Add(sourceNote!);
            }

            return string.Join(" ", parts.Where(item => !string.IsNullOrWhiteSpace(item)));
        }

        private static string? BuildSourceNote(AnalysisMode analysisMode, DataSourceType? dataSourceType)
        {
            if (dataSourceType == DataSourceType.EstimatedByCategory || analysisMode == AnalysisMode.FrontOfPackageOnly)
            {
                return "Análise baseada na categoria do produto.";
            }

            if (dataSourceType == DataSourceType.Mixed)
            {
                return "Parte dos valores veio da tabela e parte foi estimada.";
            }

            return null;
        }

        private static void ApplyBasis(
    EstimatedNutritionProfileDto? nutrition,
    AnalysisMode analysisMode,
    DataSourceType? dataSourceType)
        {
            if (nutrition == null)
                return;

            // 🔥 CORREÇÃO CRÍTICA
            if (!string.IsNullOrWhiteSpace(nutrition.Basis))
                return;

            if (dataSourceType == DataSourceType.Mixed)
            {
                nutrition.Basis = "Parte dos valores veio da tabela e parte foi estimada.";
                return;
            }

            nutrition.Basis = analysisMode == AnalysisMode.FrontOfPackageOnly || dataSourceType == DataSourceType.EstimatedByCategory
                ? "Análise baseada na categoria do produto."
                : "Análise baseada na tabela nutricional.";
        }
        private static PresentationText BuildLimitedAnalysisText()
        {
            return new PresentationText
            {
                ResumoRapido = new List<string>
                {
                    "Sem tabela nutricional visível, a análise é limitada.",
                    "Pontuação estimada com base apenas na categoria do produto."
                },
                ExplicacaoScore = "Pontuação qualitativa e conservadora, baseada apenas na categoria do produto.",
                PontoPrincipal = "Nenhum marcador nutricional pôde ser confirmado.",
                Guidance = "Para uma análise mais precisa, prefira produtos com tabela nutricional visível.",
                SourceNote = "Análise baseada na categoria do produto."
            };
        }

        private static void HumanizeClassification(ProductClassificationDto? classification, EstimatedNutritionProfileDto? nutrition, AnalysisMode analysisMode = AnalysisMode.FullNutritionLabel, NutritionReferenceRange? reference = null)
        {
            if (classification == null)
            {
                return;
            }

            reference ??= NutritionReferenceRanges.Resolve(null, null);

            HumanizeProfile(classification.Diabetic, "diabetic", nutrition, analysisMode, reference);
            HumanizeProfile(classification.BloodPressure, "bloodPressure", nutrition, analysisMode, reference);
            HumanizeProfile(classification.WeightLoss, "weightLoss", nutrition, analysisMode, reference);
            HumanizeProfile(classification.MuscleGain, "muscleGain", nutrition, analysisMode, reference);
        }

        private static void HumanizeClassification(ProfileClassificationDto? classification, EstimatedNutritionProfileDto? nutrition, AnalysisMode analysisMode = AnalysisMode.FullNutritionLabel, NutritionReferenceRange? reference = null)
        {
            if (classification == null)
            {
                return;
            }

            reference ??= NutritionReferenceRanges.Resolve(null, null);

            HumanizeProfile(classification.Diabetic, "diabetic", nutrition, analysisMode, reference);
            HumanizeProfile(classification.BloodPressure, "bloodPressure", nutrition, analysisMode, reference);
            HumanizeProfile(classification.WeightLoss, "weightLoss", nutrition, analysisMode, reference);
            HumanizeProfile(classification.MuscleGain, "muscleGain", nutrition, analysisMode, reference);
        }

        private static void HumanizeProfile(HealthProfileResult? profile, string profileType, EstimatedNutritionProfileDto? nutrition, AnalysisMode analysisMode = AnalysisMode.FullNutritionLabel, NutritionReferenceRange? reference = null)
        {
            if (profile == null)
            {
                return;
            }

            profile.Reason = BuildProfileReason(profile.Status, profileType, nutrition, analysisMode, reference);
        }

        private static void HumanizeProfile(ProfileStatusDto? profile, string profileType, EstimatedNutritionProfileDto? nutrition, AnalysisMode analysisMode = AnalysisMode.FullNutritionLabel, NutritionReferenceRange? reference = null)
        {
            if (profile == null)
            {
                return;
            }

            profile.Reason = BuildProfileReason(profile.Status, profileType, nutrition, analysisMode, reference);
        }

        private static string BuildProfileReason(string? status, string profileType, EstimatedNutritionProfileDto? nutrition, AnalysisMode analysisMode = AnalysisMode.FullNutritionLabel, NutritionReferenceRange? reference = null)
        {
            if (analysisMode == AnalysisMode.FrontOfPackageOnly)
            {
                return profileType switch
                {
                    "diabetic" => "Sem tabela nutricional visível, não foi possível confirmar o teor de açúcar.",
                    "bloodPressure" => "Sem tabela nutricional visível, não foi possível confirmar o teor de sódio.",
                    "weightLoss" => "Sem tabela nutricional visível, não foi possível avaliar calorias e perfil nutricional com segurança.",
                    "muscleGain" => "Sem tabela nutricional visível, não foi possível confirmar o teor de proteína.",
                    _ => "Sem tabela nutricional visível, a análise é limitada."
                };
            }

            reference ??= NutritionReferenceRanges.Resolve(null, null);

            var normalizedStatus = (status ?? string.Empty).Trim().ToLowerInvariant();
            var sugar = nutrition?.EstimatedSugarPer100g ?? 0;
            var sodium = nutrition?.EstimatedSodiumPer100g ?? 0;
            var protein = nutrition?.EstimatedProteinPer100g ?? 0;
            var calories = nutrition?.CaloriesPer100g ?? 0;
            var fat = nutrition?.EstimatedFatPer100g ?? 0;

            // Thresholds derivados do catálogo — calibrados por categoria do produto
            var sugarModThreshold      = reference.SugarPer100g.Average * 1.3;
            var sugarHighThreshold     = reference.SugarPer100g.Maximum * 0.75;
            var sodiumModThreshold     = reference.SodiumPer100g.Average * 1.5;
            var sodiumHighThreshold    = reference.SodiumPer100g.Maximum * 0.75;
            var caloriesModThreshold   = reference.CaloriesPer100g.Average * 1.2;
            var caloriesHighThreshold  = reference.CaloriesPer100g.Maximum * 0.75;
            var fatHighThreshold       = reference.FatPer100g.Maximum * 0.75;

            var isBeverage = reference.Key == "beverage" || reference.Key == "dairy-liquid";

            return profileType switch
            {
                "diabetic" when normalizedStatus.Contains("nao") || sugar > sugarHighThreshold
                    => isBeverage
                        ? "Bebida com alto teor de açúcar. Não recomendada para quem controla a glicemia."
                        : "Pode impactar o controle do açúcar no sangue.",
                "diabetic" when normalizedStatus.Contains("moderado") || sugar > sugarModThreshold
                    => isBeverage
                        ? "Bebida com açúcar relevante por porção. Atenção ao consumo para quem controla a glicemia."
                        : "Melhor consumir com moderação se você controla o açúcar.",
                "diabetic"
                    => "É uma opção equilibrada para quem controla o açúcar.",

                "bloodPressure" when normalizedStatus.Contains("nao") || sodium > sodiumHighThreshold
                    => "Pode não ser ideal para quem controla o sódio.",
                "bloodPressure" when normalizedStatus.Contains("moderado") || sodium > sodiumModThreshold
                    => "Melhor moderar se você precisa cuidar do sódio.",
                "bloodPressure"
                    => "Nível de sódio dentro do esperado para a categoria.",

                "weightLoss" when normalizedStatus.Contains("nao") || calories > caloriesHighThreshold || fat > fatHighThreshold || sugar > sugarHighThreshold
                    => isBeverage
                        ? "Bebida com açúcar elevado. Não indicada para quem busca emagrecimento."
                        : "Não é a melhor opção para dietas de emagrecimento.",
                "weightLoss" when normalizedStatus.Contains("moderado") || calories > caloriesModThreshold || sugar > sugarModThreshold
                    => isBeverage
                        ? "Bebida com açúcar relevante por porção. A porção faz diferença para quem controla o peso."
                        : "Pode entrar na dieta, mas a porção faz diferença.",
                "weightLoss"
                    => "Pode ser compatível com dietas de emagrecimento.",

                "muscleGain" when normalizedStatus.Contains("fraco") || protein < 8
                    => "Não é das melhores opções para ganho de massa.",
                "muscleGain" when normalizedStatus.Contains("moderado") || protein < 20
                    => "Pode ajudar, mas não é das opções mais ricas em proteína.",
                _   => "Pode ajudar no ganho de massa se consumido corretamente."
            };
        }

        private static string NormalizeOffender(string? principalOffender)
        {
            if (string.IsNullOrWhiteSpace(principalOffender))
            {
                return string.Empty;
            }

            return principalOffender.Trim().ToLowerInvariant() switch
            {
                // Inglês (setado por ApplyPrincipalOffenderIfMissing)
                "sugar"            => "açúcar",
                "sodium"           => "sódio",
                "fat"              => "gordura",
                "saturatedfat"     => "gordura saturada",
                "caloriedensity"   => "densidade calórica",
                "lowfiber"         => "baixa fibra",
                "lowprotein"       => "baixa proteína",
                // Português (setado diretamente pela IA vision)
                "açúcar"           => "açúcar",
                "acucar"           => "açúcar",
                "sódio"            => "sódio",
                "sodio"            => "sódio",
                "gordura"          => "gordura",
                "gordura saturada" => "gordura saturada",
                "calorias"         => "densidade calórica",
                "fibras"           => "baixa fibra",
                "proteína"         => "baixa proteína",
                "proteina"         => "baixa proteína",
                _                  => principalOffender.Trim().ToLowerInvariant()
            };
        }

        private sealed record ConcernSignal(string Name, string Bullet, string ScoreReason, int Priority, string Suggestion);
        private sealed record PositiveSignal(string Highlight, string Bullet, string ScoreReason, int Priority);

        private sealed class PresentationText
        {
            public List<string> ResumoRapido { get; set; } = new();
            public string ExplicacaoScore { get; set; } = string.Empty;
            public string PontoPrincipal { get; set; } = string.Empty;
            public string Guidance { get; set; } = string.Empty;
            public string? SourceNote { get; set; }
        }
    }
}
