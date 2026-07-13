using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services
{
    public class ScoreInterpretationService : IScoreInterpretationService
    {
        private static readonly string[] UltraProcessedKeywords =
        {
            "barra proteica",
            "barra de proteína",
            "barra de proteina",
            "achocolatado",
            "biscoito recheado",
            "biscoito amanteigado",
            "salgadinho",
            "refrigerante",
            "sobremesa láctea",
            "sobremesa lactea",
            "bebida proteica",
            "bebida proteica industrializada",
            "bebida láctea",
            "bebida lactea",
            "shake proteico",
            "cookie proteico",
            "snack proteico"
        };

        private static readonly string[] ProcessedKeywords =
        {
            "queijo",
            "pão",
            "pao",
            "iogurte",
            "molho",
            "conserva",
            "requeijão",
            "requeijao",
            "presunto",
            "peito de peru",
            "granola"
        };

        private static readonly string[] MinimallyProcessedKeywords =
        {
            "arroz",
            "aveia",
            "milho para pipoca",
            "pipoca em grão",
            "pipoca em grao",
            "iogurte natural",
            "feijão",
            "feijao",
            "leite",
            "queijo minas",
            "queijo fresco",
            "ricota"
        };

        private static readonly string[] InNaturaKeywords =
        {
            "in natura",
            "fruta",
            "verdura",
            "legume",
            "hortaliça",
            "hortalica",
            "castanha",
            "semente"
        };

        /// <summary>
        /// Bebidas industrializadas que podem conter palavras de in natura (ex: "fruta")
        /// mas são produtos processados — não devem ser classificadas como in_natura.
        /// </summary>
        private static readonly string[] IndustrialBeverageKeywords =
        {
            "suco",
            "néctar",
            "nectar",
            "refresco",
            "limonada",
            "bebida à base de",
            "bebida a base de"
        };

        private static readonly string[] IndustrialClaims =
        {
            "alto teor de proteína",
            "alto teor de proteina",
            "fonte de proteína",
            "fonte de proteina",
            "protein",
            "proteico",
            "zero açúcar",
            "zero acucar",
            "sem açúcar",
            "sem acucar",
            "ready to drink",
            "pronto para beber"
        };

        private readonly ILogger<ScoreInterpretationService> _logger;

        public ScoreInterpretationService(ILogger<ScoreInterpretationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ScoreInterpretationResult Interpret(NutritionAnalysisContext context)
        {
            var interpretationContext = new ScoreInterpretationContext
            {
                Score = context.ScoreAdjusted,
                ProductName = context.ProductName,
                Category = context.CategoryNormalized ?? context.CategoryRaw,
                VisibleClaims = context.VisibleClaims,
                PrincipalOffender = context.PrincipalOffender,
                NutritionProfile = context.FinalNutritionProfile,
                Classification = context.HealthProfiles
            };

            var snapshot = InterpretCore(interpretationContext);

            return new ScoreInterpretationResult
            {
                Label = snapshot.Label,
                SafeLabel = snapshot.Label,
                Status = snapshot.Status,
                Color = snapshot.Color,
                RecommendationLevel = snapshot.RecommendationLevel,
                SemanticRecommendation = snapshot.AbsoluteRecommendation,
                AbsoluteRecommendation = snapshot.AbsoluteRecommendation,
                ComparativeRecommendation = string.Empty,
                ScoreInterpretation = snapshot.ScoreInterpretation,
                AbsoluteLabel = snapshot.AbsoluteLabel
            };
        }

        public string DetermineProcessingLevel(string? category, IEnumerable<string>? visibleClaims, string? productName)
        {
            var normalizedCategory = Normalize(category);
            var normalizedProductName = Normalize(productName);
            var normalizedClaims = (visibleClaims ?? [])
                .Where(claim => !string.IsNullOrWhiteSpace(claim))
                .Select(Normalize)
                .ToList();

            var combined = string.Join(" | ", new[] { normalizedCategory, normalizedProductName }
                .Where(value => !string.IsNullOrWhiteSpace(value)));

            if (ContainsAny(combined, InNaturaKeywords)
                && !ContainsAny(combined, UltraProcessedKeywords)
                && !ContainsAny(combined, IndustrialBeverageKeywords))
            {
                return "in_natura";
            }

            // Sucos, néctares e refrescos industrializados são processados, não in_natura
            if (ContainsAny(combined, IndustrialBeverageKeywords))
            {
                return "processado";
            }

            if (ContainsAny(combined, UltraProcessedKeywords))
            {
                return "ultraprocessado";
            }

            if (ContainsAny(normalizedClaims, IndustrialClaims)
                && (combined.Contains("barra", StringComparison.OrdinalIgnoreCase)
                    || combined.Contains("bebida", StringComparison.OrdinalIgnoreCase)
                    || combined.Contains("shake", StringComparison.OrdinalIgnoreCase)
                    || combined.Contains("cookie", StringComparison.OrdinalIgnoreCase)))
            {
                return "ultraprocessado";
            }

            if (ContainsAny(combined, MinimallyProcessedKeywords))
            {
                return "minimamente_processado";
            }

            if (ContainsAny(combined, ProcessedKeywords) || ContainsAny(normalizedClaims, IndustrialClaims))
            {
                return "processado";
            }

            return "processado";
        }

        public NutritionalScore BuildSafeScoreLabel(ScoreInterpretationContext context)
        {
            var snapshot = InterpretCore(context);

            _logger.LogInformation(
                "Semantic score interpreted. Product={ProductName}, Category={Category}, ProcessingLevel={ProcessingLevel}, OriginalScore={OriginalScore}, LabelBefore={LabelBefore}, LabelAfter={LabelAfter}, RequiresModeration={RequiresModeration}, FinalRecommendation={FinalRecommendation}",
                context.ProductName ?? "N/A",
                context.Category ?? "N/A",
                snapshot.ProcessingLevel,
                snapshot.Score,
                snapshot.LabelBefore,
                snapshot.Label,
                snapshot.RequiresModeration,
                snapshot.AbsoluteRecommendation);

            return new NutritionalScore
            {
                Value = snapshot.Score,
                Label = snapshot.Label,
                SafeLabel = snapshot.Label,
                Status = snapshot.Status,
                Color = snapshot.Color,
                Reason = snapshot.Reason,
                RecommendationLevel = snapshot.RecommendationLevel,
                SemanticRecommendation = snapshot.AbsoluteRecommendation,
                AbsoluteRecommendation = snapshot.AbsoluteRecommendation,
                ComparativeRecommendation = string.Empty,
                ScoreInterpretation = snapshot.ScoreInterpretation,
                AbsoluteLabel = snapshot.AbsoluteLabel,
                ComparativeLabel = string.Empty,
                ProcessingLevel = snapshot.ProcessingLevel,
                RequiresModeration = snapshot.RequiresModeration,
                IsUltraProcessed = snapshot.IsUltraProcessed
            };
        }

        public string BuildAbsoluteRecommendation(ScoreInterpretationContext context)
        {
            return InterpretCore(context).AbsoluteRecommendation;
        }

        public string BuildComparativeRecommendation(ScoreInterpretationContext primary, ScoreInterpretationContext secondary, bool isPrimaryWinner, bool isTie)
        {
            var primarySnapshot = InterpretCore(primary);
            var secondarySnapshot = InterpretCore(secondary);

            if (isTie)
            {
                if (primarySnapshot.RequiresModeration && secondarySnapshot.RequiresModeration)
                {
                    return "Perfis semelhantes na comparação, mas ambos ainda pedem moderação.";
                }

                return "Perfis semelhantes na comparação, sem vantagem nutricional clara.";
            }

            if (isPrimaryWinner)
            {
                return primarySnapshot.RequiresModeration
                    ? "Melhor opção entre as analisadas, mas com moderação."
                    : "Melhor opção entre as analisadas.";
            }

            return "Há alternativa mais equilibrada na comparação; este produto pede mais moderação.";
        }

        public bool ShouldCapPositiveLabel(ScoreInterpretationContext context, string processingLevel)
        {
            return InterpretCore(context, processingLevel).ShouldCapPositiveLabel;
        }

        public string BuildScoreReason(ScoreInterpretationContext context)
        {
            return InterpretCore(context).Reason;
        }

        private InterpretationSnapshot InterpretCore(ScoreInterpretationContext context, string? forcedProcessingLevel = null)
        {
            var score = Math.Clamp(context?.Score ?? 0, 0, 100);
            var processingLevel = forcedProcessingLevel ?? DetermineProcessingLevel(context?.Category, context?.VisibleClaims, context?.ProductName);
            var baseBand = GetBaseBand(score);
            var concerns = AnalyzeConcerns(context, processingLevel);

            var label = baseBand.Label;
            var status = baseBand.Status;
            var color = baseBand.Color;
            var shouldCap = ShouldCapPositiveLabel(score, processingLevel, concerns);

            if (shouldCap && score >= 85)
            {
                label = "Boa escolha";
                status = "bom";
                color = "green";
            }

            if (score >= 70 && processingLevel == "ultraprocessado" && concerns.CriticalCount >= 2)
            {
                label = "Consumo moderado";
                status = "consumo_moderado";
                color = "yellow";
            }

            if (score >= 64
                && score < 70
                && (processingLevel == "in_natura" || processingLevel == "minimamente_processado")
                && concerns.CriticalCount == 0
                && concerns.PositiveSignalCount >= 2)
            {
                label = "Boa escolha";
                status = "bom";
                color = "green";
            }

            // Regra 3: score >= 85 → requiresModeration = false (produto genuinamente excelente)
            if (score >= 85)
                concerns.RequiresModeration = false;

            // Regra 4: garantir coerência entre label e moderação
            // "Excelente escolha" não pode vir com requiresModeration = true
            if (label == "Excelente escolha" && concerns.RequiresModeration)
                label = "Boa escolha";

            var absoluteRecommendation = BuildAbsoluteRecommendation(label, processingLevel, concerns);
            var absoluteLabel = BuildAbsoluteLabel(label, concerns.RequiresModeration);
            var recommendationLevel = BuildRecommendationLevel(label, concerns.RequiresModeration);
            var reason = BuildReason(score, label, baseBand.Label, processingLevel, concerns);
            var scoreInterpretation = BuildInterpretationSummary(score, label, baseBand.Label, processingLevel, concerns.RequiresModeration);

            return new InterpretationSnapshot
            {
                Score = score,
                LabelBefore = baseBand.Label,
                Label = label,
                Status = status,
                Color = color,
                ProcessingLevel = processingLevel,
                RequiresModeration = concerns.RequiresModeration,
                IsUltraProcessed = processingLevel == "ultraprocessado" ? (bool?)true : false,
                ShouldCapPositiveLabel = shouldCap,
                Reason = reason,
                AbsoluteRecommendation = absoluteRecommendation,
                AbsoluteLabel = absoluteLabel,
                RecommendationLevel = recommendationLevel,
                ScoreInterpretation = scoreInterpretation
            };
        }

        private static bool ShouldCapPositiveLabel(int score, string processingLevel, ConcernSummary concerns)
        {
            if (score < 85)
            {
                return false;
            }

            if (processingLevel == "ultraprocessado")
            {
                return true;
            }

            if (processingLevel == "processado" && (concerns.CriticalCount > 0 || concerns.HasPrincipalOffender || concerns.ProteinInflationRisk))
            {
                return true;
            }

            return concerns.CriticalCount >= 2 || concerns.ProteinInflationRisk;
        }

        private static bool IsBeverageCategoryString(string? category)
        {
            if (string.IsNullOrWhiteSpace(category)) return false;
            var norm = category.Trim().ToLowerInvariant();
            return norm.Contains("suco") || norm.Contains("néctar") || norm.Contains("nectar")
                || norm.Contains("refresco") || norm.Contains("limonada")
                || norm.Contains("bebida à base") || norm.Contains("bebida a base");
        }

        private static ConcernSummary AnalyzeConcerns(ScoreInterpretationContext context, string processingLevel)
        {
            var nutrition = context.NutritionProfile;
            var sugar = nutrition?.EstimatedSugarPer100g ?? 0;
            var sodium = nutrition?.EstimatedSodiumPer100g ?? 0;
            var fat = nutrition?.EstimatedFatPer100g ?? 0;
            var fiber = nutrition?.EstimatedFiberPer100g ?? 0;
            var protein = nutrition?.EstimatedProteinPer100g ?? 0;

            var criticalCount = 0;
            var positiveSignals = 0;
            var primaryConcern = string.Empty;

            // Para bebidas (sucos, néctares), o limiar de açúcar preocupante é menor:
            // 7g/100ml = 14g numa porção de 200ml — já representa ~56% da recomendação diária da OMS
            var isBeverage = IsBeverageCategoryString(context.Category);
            var sugarConcernThreshold = isBeverage ? 7.0 : 15.0;

            if (sugar > sugarConcernThreshold)
            {
                criticalCount++;
                primaryConcern = "açúcar";
            }

            if (sodium > 600)
            {
                criticalCount++;
                primaryConcern = string.IsNullOrWhiteSpace(primaryConcern) ? "sódio" : primaryConcern;
            }

            if (fat > 17.5)
            {
                criticalCount++;
                primaryConcern = string.IsNullOrWhiteSpace(primaryConcern) ? "gordura" : primaryConcern;
            }

            if (fiber is > 0 and < 2)
            {
                criticalCount++;
                primaryConcern = string.IsNullOrWhiteSpace(primaryConcern) ? "baixa fibra" : primaryConcern;
            }

            var proteinInflationRisk = protein >= 15 && fiber < 2 && (sugar > 10 || sodium > 400 || fat > 10);
            if (proteinInflationRisk)
            {
                criticalCount++;
                primaryConcern = string.IsNullOrWhiteSpace(primaryConcern) ? "proteína compensando um perfil ainda crítico" : primaryConcern;
            }

            if (sugar <= 5) positiveSignals++;
            // Para bebidas, baixo sódio e baixa gordura são esperados e não constituem diferencial positivo
            if (!isBeverage && sodium <= 200) positiveSignals++;
            if (!isBeverage && fat <= 5) positiveSignals++;
            if (fiber >= 3) positiveSignals++;
            if (protein >= 8) positiveSignals++;

            var principalOffender = NormalizeOffender(context.PrincipalOffender);
            if (!string.IsNullOrWhiteSpace(principalOffender))
            {
                primaryConcern = string.IsNullOrWhiteSpace(primaryConcern) ? principalOffender : primaryConcern;
            }

            var requiresModeration = processingLevel is "processado" or "ultraprocessado"
                || criticalCount > 0
                || !string.IsNullOrWhiteSpace(principalOffender)
                || CountSensitiveProfiles(context.Classification) > 0;

            return new ConcernSummary
            {
                CriticalCount = criticalCount,
                PositiveSignalCount = positiveSignals,
                PrimaryConcern = primaryConcern,
                RequiresModeration = requiresModeration,
                ProteinInflationRisk = proteinInflationRisk,
                HasPrincipalOffender = !string.IsNullOrWhiteSpace(principalOffender)
            };
        }

        private static int CountSensitiveProfiles(ProductClassificationDto? classification)
        {
            if (classification == null)
            {
                return 0;
            }

            return new[]
            {
                classification.Diabetic?.Status,
                classification.BloodPressure?.Status,
                classification.WeightLoss?.Status,
                classification.MuscleGain?.Status
            }
            .Count(status => !string.IsNullOrWhiteSpace(status)
                && (status.Contains("nao", StringComparison.OrdinalIgnoreCase)
                    || status.Contains("moderado", StringComparison.OrdinalIgnoreCase)
                    || status.Contains("fraco", StringComparison.OrdinalIgnoreCase)));
        }

        private static string BuildAbsoluteRecommendation(string label, string processingLevel, ConcernSummary concerns)
        {
            var concern = concerns.PrimaryConcern;

            return label switch
            {
                "Excelente escolha" => "Boa opção para o dia a dia.",
                "Boa escolha" when processingLevel == "ultraprocessado" => "Boa escolha dentro da categoria, mas ainda vale moderar.",
                "Boa escolha" when !string.IsNullOrWhiteSpace(concern) => $"Boa escolha, mas vale atenção em {NormalizeConcern(concern)}.",
                "Boa escolha" => "Pode entrar na rotina com equilíbrio.",
                "Consumo moderado" when !string.IsNullOrWhiteSpace(concern) => $"Melhor consumir com moderação por causa de {NormalizeConcern(concern)}.",
                "Consumo moderado" => "Melhor consumir com moderação.",
                "Ruim" when !string.IsNullOrWhiteSpace(concern) => $"Não é uma boa opção por causa de {NormalizeConcern(concern)}.",
                "Ruim" => "Não é uma boa opção para o dia a dia.",
                _ when !string.IsNullOrWhiteSpace(concern) => $"Evite consumo frequente por causa de {NormalizeConcern(concern)}.",
                _ => "Não é uma boa opção para consumo frequente."
            };
        }

        private static string BuildAbsoluteLabel(string label, bool requiresModeration)
        {
            return label switch
            {
                "Excelente escolha" => "excelente_escolha",
                "Boa escolha" when requiresModeration => "consumo_moderado_positivo",
                "Boa escolha" => "boa_escolha",
                "Consumo moderado" => "consumo_moderado",
                "Ruim" => "ruim",
                _ => "nao_recomendado"
            };
        }

        private static string BuildRecommendationLevel(string label, bool requiresModeration)
        {
            return label switch
            {
                "Excelente escolha" => "excelente_escolha",
                "Boa escolha" when requiresModeration => "consumo_moderado_positivo",
                "Boa escolha" => "escolha_segura",
                "Consumo moderado" => "consumo_moderado",
                "Ruim" => "ruim",
                _ => "nao_recomendado"
            };
        }

        private static string BuildReason(int score, string finalLabel, string labelBefore, string processingLevel, ConcernSummary concerns)
        {
            var concern = NormalizeConcern(concerns.PrimaryConcern);

            // Mapear o principal fator para a frase prescrita pelo spec
            if (!string.IsNullOrWhiteSpace(concern))
            {
                return concern switch
                {
                    var c when c.Contains("açúcar")   => "alto teor de açúcar",
                    var c when c.Contains("sódio")    => "alto teor de sódio",
                    var c when c.Contains("gordura")  => "alto teor de gordura saturada",
                    var c when c.Contains("caloria")  => "densidade calórica elevada",
                    _ => $"alto teor de {concern}"
                };
            }

            return "boa qualidade nutricional geral";
        }

        private static string BuildInterpretationSummary(int score, string finalLabel, string labelBefore, string processingLevel, bool requiresModeration)
        {
            if (requiresModeration)
            {
                return $"Score {score}/100. {finalLabel}. Melhor consumir com moderação.";
            }

            return $"Score {score}/100. {finalLabel}. Pode entrar na rotina com equilíbrio.";
        }

        private static (string Label, string Status, string Color) GetBaseBand(int score)
        {
            return score switch
            {
                >= 85 => ("Excelente escolha", "excelente", "green"),
                >= 70 => ("Boa escolha", "bom", "green"),
                >= 50 => ("Consumo moderado", "consumo_moderado", "yellow"),
                >= 30 => ("Ruim", "ruim", "orange"),
                _ => ("Não recomendado", "nao_recomendado", "red")
            };
        }

        private static string Normalize(string? value)
        {
            return value?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        private static bool ContainsAny(string source, IEnumerable<string> terms)
        {
            return !string.IsNullOrWhiteSpace(source)
                && terms.Any(term => source.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        private static bool ContainsAny(IEnumerable<string> source, IEnumerable<string> terms)
        {
            return source.Any(item => ContainsAny(item, terms));
        }

        private static string NormalizeOffender(string? offender)
        {
            if (string.IsNullOrWhiteSpace(offender))
            {
                return string.Empty;
            }

            return offender.Trim().ToLowerInvariant() switch
            {
                "fat" => "gordura",
                "sugar" => "açúcar",
                "sodium" => "sódio",
                "caloriedensity" => "densidade calórica",
                "lowprotein" => "baixa proteína",
                "lowfiber" => "baixa fibra",
                _ => offender.Trim().ToLowerInvariant()
            };
        }

        private static string DescribeProcessing(string processingLevel)
        {
            return processingLevel switch
            {
                "in_natura" => "alimento in natura",
                "minimamente_processado" => "alimento minimamente processado",
                "ultraprocessado" => "produto ultraprocessado",
                _ => "produto processado"
            };
        }

        private static string NormalizeConcern(string concern)
        {
            return concern switch
            {
                "açúcar" => "alto teor de açúcar",
                "sódio" => "alto teor de sódio",
                "gordura" => "alto teor de gordura",
                "baixa fibra" => "baixo teor de fibras",
                "baixa proteína" => "baixo teor de proteína",
                "densidade calórica" => "muitas calorias",
                _ => concern
            };
        }

        private sealed class ConcernSummary
        {
            public int CriticalCount { get; set; }
            public int PositiveSignalCount { get; set; }
            public string PrimaryConcern { get; set; } = string.Empty;
            public bool RequiresModeration { get; set; }
            public bool ProteinInflationRisk { get; set; }
            public bool HasPrincipalOffender { get; set; }
        }

        private sealed class InterpretationSnapshot
        {
            public int Score { get; set; }
            public string LabelBefore { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string Color { get; set; } = string.Empty;
            public string ProcessingLevel { get; set; } = string.Empty;
            public bool RequiresModeration { get; set; }
            public bool? IsUltraProcessed { get; set; }
            public bool ShouldCapPositiveLabel { get; set; }
            public string Reason { get; set; } = string.Empty;
            public string AbsoluteRecommendation { get; set; } = string.Empty;
            public string AbsoluteLabel { get; set; } = string.Empty;
            public string RecommendationLevel { get; set; } = string.Empty;
            public string ScoreInterpretation { get; set; } = string.Empty;
        }
    }
}
