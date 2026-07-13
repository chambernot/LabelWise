using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services
{
    public class ProductComparisonService : IProductComparisonService
    {
        private static readonly IReadOnlyDictionary<string, int> ProfileRank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["nao_recomendado"] = 0,
            ["nao_indicado"] = 0,
            ["fraco"] = 1,
            ["consumo_moderado"] = 2,
            ["moderado"] = 2,
            ["indeterminado"] = 3,
            ["adequado"] = 4,
            ["favoravel"] = 4,
            ["mais_adequado"] = 5,
            ["bom"] = 5
        };

        private readonly IAnalysisRepository _analysisRepository;
        private readonly IAnalysisHistoryRepository _analysisHistoryRepository;
        private readonly ILogger<ProductComparisonService> _logger;

        public ProductComparisonService(
            IAnalysisRepository analysisRepository,
            IAnalysisHistoryRepository analysisHistoryRepository,
            ILogger<ProductComparisonService> logger)
        {
            _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
            _analysisHistoryRepository = analysisHistoryRepository ?? throw new ArgumentNullException(nameof(analysisHistoryRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ProductComparisonResponse> CompareAsync(string analysisIdA, string analysisIdB, Guid? userId = null, string? deviceId = null)
        {
            if (string.IsNullOrWhiteSpace(analysisIdA))
            {
                throw new ArgumentException("analysisIdA é obrigatório.", nameof(analysisIdA));
            }

            if (string.IsNullOrWhiteSpace(analysisIdB))
            {
                throw new ArgumentException("analysisIdB é obrigatório.", nameof(analysisIdB));
            }

            if (!Guid.TryParse(analysisIdA, out var parsedAnalysisIdA))
            {
                throw new ArgumentException("analysisIdA inválido.", nameof(analysisIdA));
            }

            if (!Guid.TryParse(analysisIdB, out var parsedAnalysisIdB))
            {
                throw new ArgumentException("analysisIdB inválido.", nameof(analysisIdB));
            }

            var rawAnalysisA = await _analysisRepository.GetByIdAsync(parsedAnalysisIdA);
            if (rawAnalysisA == null)
            {
                throw new KeyNotFoundException("Análise A não encontrada.");
            }

            var rawAnalysisB = await _analysisRepository.GetByIdAsync(parsedAnalysisIdB);
            if (rawAnalysisB == null)
            {
                throw new KeyNotFoundException("Análise B não encontrada.");
            }

            ValidateOwnership(rawAnalysisA, rawAnalysisB, userId, deviceId);

            var analysisA = await _analysisHistoryRepository.GetByIdAsync(parsedAnalysisIdA);
            if (analysisA == null)
            {
                throw new KeyNotFoundException("Análise A não encontrada.");
            }

            var analysisB = await _analysisHistoryRepository.GetByIdAsync(parsedAnalysisIdB);
            if (analysisB == null)
            {
                throw new KeyNotFoundException("Análise B não encontrada.");
            }

            return CompareCore(analysisA, analysisB, analysisIdA, analysisIdB);
        }

        private static void ValidateOwnership(Domain.Entities.ProductAnalysis analysisA, Domain.Entities.ProductAnalysis analysisB, Guid? userId, string? deviceId)
        {
            if (userId.HasValue)
            {
                if (analysisA.UserId != userId.Value || analysisB.UserId != userId.Value)
                {
                    throw new KeyNotFoundException("Uma ou mais análises não pertencem ao usuário informado.");
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return;
            }

            var normalizedDeviceId = deviceId.Trim();
            var sameOwner = string.Equals(analysisA.DeviceId, normalizedDeviceId, StringComparison.Ordinal)
                && string.Equals(analysisB.DeviceId, normalizedDeviceId, StringComparison.Ordinal)
                && string.Equals(analysisA.DeviceId, analysisB.DeviceId, StringComparison.Ordinal);

            if (!sameOwner)
            {
                throw new UnauthorizedAccessException("As análises informadas não pertencem ao mesmo deviceId.");
            }
        }

        public Task<ProductComparisonResponse> CompareAsync(ProductComparisonAnalysisInputDto productA, ProductComparisonAnalysisInputDto productB)
        {
            return Task.FromResult(CompareCore(productA, productB, productA?.AnalysisId, productB?.AnalysisId));
        }

        private ProductComparisonResponse CompareCore(
            ProductComparisonAnalysisInputDto productA,
            ProductComparisonAnalysisInputDto productB,
            string? analysisIdA,
            string? analysisIdB)
        {
            var normalizedA = Normalize(productA, "A");
            var normalizedB = Normalize(productB, "B");

            var difference = Math.Abs(normalizedA.Score - normalizedB.Score);
            var winner = DetermineWinner(normalizedA.Score, normalizedB.Score);
            var comparisonLevel = DetermineComparisonLevel(normalizedA.Score, normalizedB.Score, difference);
            var healthProfileComparison = BuildHealthProfileComparison(normalizedA, normalizedB);
            var keyDifferences = BuildKeyDifferences(normalizedA, normalizedB, winner, difference, healthProfileComparison);
            var winnerReason = BuildWinnerReason(normalizedA, normalizedB, winner, difference, healthProfileComparison);
            var confidence = CalculateConfidence(normalizedA, normalizedB);
            var recommendation = BuildRecommendation(normalizedA.Score, normalizedB.Score, winner, comparisonLevel);

            _logger.LogInformation(
                "Product comparison computed. AnalysisIdA={AnalysisIdA}, AnalysisIdB={AnalysisIdB}, A={ScoreA}, B={ScoreB}, Winner={Winner}, Level={Level}, Confidence={Confidence}",
                analysisIdA ?? "inline-A",
                analysisIdB ?? "inline-B",
                normalizedA.Score,
                normalizedB.Score,
                winner,
                comparisonLevel,
                confidence);

            return new ProductComparisonResponse
            {
                ProductA = new ProductComparisonItem
                {
                    ProductName = normalizedA.ProductName,
                    Brand = normalizedA.Brand,
                    Category = normalizedA.Category,
                    Score = normalizedA.Score,
                    ScoreLabel = normalizedA.ScoreLabel,
                    PrincipalOffender = normalizedA.PrincipalOffender
                },
                ProductB = new ProductComparisonItem
                {
                    ProductName = normalizedB.ProductName,
                    Brand = normalizedB.Brand,
                    Category = normalizedB.Category,
                    Score = normalizedB.Score,
                    ScoreLabel = normalizedB.ScoreLabel,
                    PrincipalOffender = normalizedB.PrincipalOffender
                },
                Winner = winner,
                WinnerReason = winnerReason,
                ComparisonLevel = comparisonLevel,
                ScoreComparison = new ScoreComparisonDto
                {
                    ProductA = normalizedA.Score,
                    ProductB = normalizedB.Score,
                    Difference = difference
                },
                HealthProfileComparison = healthProfileComparison,
                KeyDifferences = keyDifferences,
                Recommendation = recommendation,
                Confidence = confidence
            };
        }

        private static ComparableAnalysis Normalize(ProductComparisonAnalysisInputDto input, string fallbackLabel)
        {
            if (input == null)
            {
                throw new ArgumentException("Dados da análise são obrigatórios.");
            }

            var classification = MergeClassification(input.Classification, input.EstimatedNutritionProfile);
            var hasUsefulClassification = HasUsefulClassification(classification);
            var hasUsefulNutrition = HasUsefulNutrition(input.EstimatedNutritionProfile);
            var score = input.Score ?? ComputeScore(input.EstimatedNutritionProfile, classification);

            if (!score.HasValue || (!hasUsefulClassification && !hasUsefulNutrition))
            {
                throw new InvalidOperationException("Comparação impossível por dados insuficientes.");
            }

            return new ComparableAnalysis
            {
                ProductName = string.IsNullOrWhiteSpace(input.ProductName) ? $"Produto {fallbackLabel}" : input.ProductName.Trim(),
                Brand = NormalizeOptional(input.Brand),
                Category = NormalizeOptional(input.Category),
                AnalysisMode = input.AnalysisMode ?? InferAnalysisMode(input.EstimatedNutritionProfile),
                Score = Math.Clamp(score.Value, 0, 100),
                ScoreLabel = string.IsNullOrWhiteSpace(input.ScoreLabel) ? BuildScoreLabel(score.Value) : input.ScoreLabel.Trim(),
                PrincipalOffender = string.IsNullOrWhiteSpace(input.PrincipalOffender)
                    ? DetectPrincipalOffender(input.EstimatedNutritionProfile)
                    : input.PrincipalOffender.Trim(),
                Classification = classification,
                EstimatedNutritionProfile = input.EstimatedNutritionProfile
            };
        }

        private static string? NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static AnalysisMode InferAnalysisMode(EstimatedNutritionProfileDto? nutritionProfile)
        {
            return HasUsefulNutrition(nutritionProfile)
                ? AnalysisMode.FullNutritionLabel
                : AnalysisMode.FrontOfPackageOnly;
        }

        private static bool HasUsefulNutrition(EstimatedNutritionProfileDto? nutritionProfile)
        {
            return nutritionProfile != null
                && (nutritionProfile.CaloriesPer100g.HasValue
                    || nutritionProfile.EstimatedSugarPer100g.HasValue
                    || nutritionProfile.EstimatedProteinPer100g.HasValue
                    || nutritionProfile.EstimatedSodiumPer100g.HasValue
                    || nutritionProfile.EstimatedFiberPer100g.HasValue
                    || nutritionProfile.EstimatedFatPer100g.HasValue);
        }

        private static bool HasUsefulClassification(ProductClassificationDto? classification)
        {
            if (classification == null)
            {
                return false;
            }

            return GetProfiles(classification)
                .Any(profile => !string.IsNullOrWhiteSpace(profile.Value.Status)
                    && !profile.Value.Status.Equals("indeterminado", StringComparison.OrdinalIgnoreCase));
        }

        private static ProductClassificationDto MergeClassification(
            ProductClassificationDto? classification,
            EstimatedNutritionProfileDto? nutritionProfile)
        {
            var derived = DeriveClassification(nutritionProfile);

            return new ProductClassificationDto
            {
                Diabetic = CoalesceProfile(classification?.Diabetic, derived.Diabetic),
                BloodPressure = CoalesceProfile(classification?.BloodPressure, derived.BloodPressure),
                WeightLoss = CoalesceProfile(classification?.WeightLoss, derived.WeightLoss),
                MuscleGain = CoalesceProfile(classification?.MuscleGain, derived.MuscleGain)
            };
        }

        private static HealthProfileResult CoalesceProfile(HealthProfileResult? primary, HealthProfileResult? fallback)
        {
            if (!string.IsNullOrWhiteSpace(primary?.Status))
            {
                return new HealthProfileResult
                {
                    Status = primary.Status.Trim(),
                    Reason = primary.Reason?.Trim() ?? string.Empty
                };
            }

            if (fallback != null)
            {
                return fallback;
            }

            return new HealthProfileResult
            {
                Status = "indeterminado",
                Reason = "Dados insuficientes para este perfil."
            };
        }

        private static ProductClassificationDto DeriveClassification(EstimatedNutritionProfileDto? nutritionProfile)
        {
            if (!HasUsefulNutrition(nutritionProfile))
            {
                return CreateIndeterminateClassification();
            }

            var sugar = nutritionProfile?.EstimatedSugarPer100g ?? 0;
            var sodium = nutritionProfile?.EstimatedSodiumPer100g ?? 0;
            var calories = nutritionProfile?.CaloriesPer100g ?? 0;
            var fat = nutritionProfile?.EstimatedFatPer100g ?? 0;
            var protein = nutritionProfile?.EstimatedProteinPer100g ?? 0;
            var fiber = nutritionProfile?.EstimatedFiberPer100g ?? 0;

            return new ProductClassificationDto
            {
                Diabetic = sugar switch
                {
                    > 15 => new HealthProfileResult { Status = "nao_recomendado", Reason = "Alto impacto glicêmico estimado." },
                    > 7 => new HealthProfileResult { Status = "consumo_moderado", Reason = "Açúcar moderado pede atenção ao consumo." },
                    _ => new HealthProfileResult { Status = fiber >= 3 ? "mais_adequado" : "adequado", Reason = "Melhor adequação para controle glicêmico." }
                },
                BloodPressure = sodium switch
                {
                    > 600 => new HealthProfileResult { Status = "nao_recomendado", Reason = "Teor elevado de sódio." },
                    > 300 => new HealthProfileResult { Status = "consumo_moderado", Reason = "Sódio moderado exige controle de porção." },
                    _ => new HealthProfileResult { Status = "mais_adequado", Reason = "Melhor adequação para pressão arterial." }
                },
                WeightLoss = calories > 350 || sugar > 15 || fat > 17.5
                    ? new HealthProfileResult { Status = "nao_recomendado", Reason = "Alta densidade energética ou excesso de açúcar/gordura." }
                    : calories > 220 || sugar > 8 || fat > 10
                        ? new HealthProfileResult { Status = "consumo_moderado", Reason = "Perfil intermediário para emagrecimento." }
                        : new HealthProfileResult { Status = fiber >= 3 ? "mais_adequado" : "adequado", Reason = "Melhor adequação para controle energético." },
                MuscleGain = protein switch
                {
                    >= 20 => new HealthProfileResult { Status = "bom", Reason = "Boa densidade proteica." },
                    >= 10 => new HealthProfileResult { Status = "consumo_moderado", Reason = "Proteína moderada." },
                    _ => new HealthProfileResult { Status = "fraco", Reason = "Baixo teor proteico." }
                }
            };
        }

        private static ProductClassificationDto CreateIndeterminateClassification()
        {
            var profile = new HealthProfileResult
            {
                Status = "indeterminado",
                Reason = "Dados insuficientes para este perfil."
            };

            return new ProductClassificationDto
            {
                Diabetic = profile,
                BloodPressure = new HealthProfileResult { Status = profile.Status, Reason = profile.Reason },
                WeightLoss = new HealthProfileResult { Status = profile.Status, Reason = profile.Reason },
                MuscleGain = new HealthProfileResult { Status = profile.Status, Reason = profile.Reason }
            };
        }

        private static int? ComputeScore(EstimatedNutritionProfileDto? nutritionProfile, ProductClassificationDto classification)
        {
            if (HasUsefulNutrition(nutritionProfile))
            {
                var score = 82.0;
                score -= GetSugarPenalty(nutritionProfile?.EstimatedSugarPer100g);
                score -= GetFatPenalty(nutritionProfile?.EstimatedFatPer100g);
                score -= GetSodiumPenalty(nutritionProfile?.EstimatedSodiumPer100g);
                score -= GetCaloriesPenalty(nutritionProfile?.CaloriesPer100g);
                score += GetProteinBonus(nutritionProfile?.EstimatedProteinPer100g);
                score += GetFiberBonus(nutritionProfile?.EstimatedFiberPer100g);

                return (int)Math.Round(Math.Clamp(score, 0, 100), MidpointRounding.AwayFromZero);
            }

            var ranks = GetProfiles(classification)
                .Select(profile => ResolveProfileRank(profile.Value.Status))
                .ToArray();

            if (ranks.Length == 0)
            {
                return null;
            }

            var averageRank = ranks.Average();
            var derivedScore = 20 + (averageRank * 15);
            return (int)Math.Round(Math.Clamp(derivedScore, 0, 100), MidpointRounding.AwayFromZero);
        }

        private static int GetSugarPenalty(double? sugarPer100g)
        {
            return sugarPer100g switch
            {
                > 25 => 26,
                > 15 => 18,
                > 8 => 10,
                > 4 => 4,
                _ => 0
            };
        }

        private static int GetFatPenalty(double? fatPer100g)
        {
            return fatPer100g switch
            {
                > 25 => 18,
                > 17.5 => 12,
                > 10 => 6,
                _ => 0
            };
        }

        private static int GetSodiumPenalty(double? sodiumPer100g)
        {
            return sodiumPer100g switch
            {
                > 900 => 22,
                > 600 => 15,
                > 300 => 8,
                > 150 => 3,
                _ => 0
            };
        }

        private static int GetCaloriesPenalty(double? caloriesPer100g)
        {
            return caloriesPer100g switch
            {
                > 450 => 14,
                > 320 => 9,
                > 220 => 4,
                _ => 0
            };
        }

        private static int GetProteinBonus(double? proteinPer100g)
        {
            return proteinPer100g switch
            {
                >= 20 => 8,
                >= 12 => 5,
                >= 8 => 3,
                _ => 0
            };
        }

        private static int GetFiberBonus(double? fiberPer100g)
        {
            return fiberPer100g switch
            {
                >= 10 => 8,
                >= 6 => 6,
                >= 3 => 3,
                _ => 0
            };
        }

        private static string BuildScoreLabel(int score)
        {
            return score switch
            {
                >= 82 => "Muito saudável",
                >= 68 => "Boa escolha",
                >= 45 => "Escolha razoável",
                >= 28 => "Consumo ocasional",
                _ => "Não recomendado"
            };
        }

        private static string? DetectPrincipalOffender(EstimatedNutritionProfileDto? nutritionProfile)
        {
            if (!HasUsefulNutrition(nutritionProfile))
            {
                return null;
            }

            var candidates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["açúcar"] = GetSugarPenalty(nutritionProfile?.EstimatedSugarPer100g),
                ["gordura"] = GetFatPenalty(nutritionProfile?.EstimatedFatPer100g),
                ["sódio"] = GetSodiumPenalty(nutritionProfile?.EstimatedSodiumPer100g),
                ["densidade calórica"] = GetCaloriesPenalty(nutritionProfile?.CaloriesPer100g)
            };

            var best = candidates.OrderByDescending(candidate => candidate.Value).FirstOrDefault();
            if (best.Value > 0)
            {
                return best.Key;
            }

            if ((nutritionProfile?.EstimatedProteinPer100g ?? 0) < 8)
            {
                return "proteína";
            }

            if ((nutritionProfile?.EstimatedFiberPer100g ?? 0) < 3)
            {
                return "fibras";
            }

            return null;
        }

        private static string DetermineWinner(int scoreA, int scoreB)
        {
            var difference = Math.Abs(scoreA - scoreB);
            if (difference < 5)
            {
                return "tie";
            }

            return scoreA >= scoreB ? "A" : "B";
        }

        private static string DetermineComparisonLevel(int scoreA, int scoreB, int difference)
        {
            if (scoreA < 40 && scoreB < 40)
            {
                return "ambos_ruins";
            }

            if (scoreA > 70 && scoreB > 70)
            {
                return "ambos_bons";
            }

            if (difference < 5)
            {
                return "empate_tecnico";
            }

            return difference > 10
                ? "melhor_escolha_clara"
                : "melhor_escolha_moderada";
        }

        private static Dictionary<string, HealthProfileComparisonDto> BuildHealthProfileComparison(
            ComparableAnalysis analysisA,
            ComparableAnalysis analysisB)
        {
            var result = new Dictionary<string, HealthProfileComparisonDto>(StringComparer.OrdinalIgnoreCase);

            AddProfileComparison(result, "diabetic", analysisA.Classification.Diabetic, analysisB.Classification.Diabetic, "controle glicêmico");
            AddProfileComparison(result, "bloodPressure", analysisA.Classification.BloodPressure, analysisB.Classification.BloodPressure, "pressão arterial");
            AddProfileComparison(result, "weightLoss", analysisA.Classification.WeightLoss, analysisB.Classification.WeightLoss, "emagrecimento");
            AddProfileComparison(result, "muscleGain", analysisA.Classification.MuscleGain, analysisB.Classification.MuscleGain, "ganho muscular");

            return result;
        }

        private static void AddProfileComparison(
            IDictionary<string, HealthProfileComparisonDto> target,
            string profileKey,
            HealthProfileResult? profileA,
            HealthProfileResult? profileB,
            string profileDescription)
        {
            var rankA = ResolveProfileRank(profileA?.Status);
            var rankB = ResolveProfileRank(profileB?.Status);
            var winner = rankA == rankB ? "tie" : rankA > rankB ? "A" : "B";

            var reason = winner == "tie"
                ? profileA?.Status?.Equals("indeterminado", StringComparison.OrdinalIgnoreCase) == true
                    && profileB?.Status?.Equals("indeterminado", StringComparison.OrdinalIgnoreCase) == true
                        ? $"Dados insuficientes para diferenciar adequação em {profileDescription}."
                        : $"Adequação semelhante para {profileDescription}."
                : $"Melhor adequação para {profileDescription}.";

            target[profileKey] = new HealthProfileComparisonDto
            {
                Winner = winner,
                Reason = reason
            };
        }

        private static int ResolveProfileRank(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return ProfileRank["indeterminado"];
            }

            return ProfileRank.TryGetValue(status.Trim(), out var rank)
                ? rank
                : ProfileRank["indeterminado"];
        }

        private static List<string> BuildKeyDifferences(
            ComparableAnalysis analysisA,
            ComparableAnalysis analysisB,
            string winner,
            int scoreDifference,
            IReadOnlyDictionary<string, HealthProfileComparisonDto> healthProfileComparison)
        {
            var differences = new List<string>();

            if (winner != "tie")
            {
                if (scoreDifference > 10)
                {
                    differences.Add($"Produto {winner} apresenta equilíbrio nutricional claramente melhor.");
                }
                else if (scoreDifference >= 5)
                {
                    differences.Add($"Produto {winner} apresenta vantagem moderada no score nutricional.");
                }
            }

            if (HasSugarDifference(analysisA, analysisB, out var sugarDifferenceMessage))
            {
                differences.Add(sugarDifferenceMessage);
            }

            if (HasLabelContrast(analysisA.Score, analysisB.Score, out var labelContrastMessage))
            {
                differences.Add(labelContrastMessage);
            }

            if (differences.Count < 3 && healthProfileComparison["bloodPressure"].Winner is "A" or "B")
            {
                differences.Add($"Produto {healthProfileComparison["bloodPressure"].Winner} é mais favorável para pressão arterial.");
            }

            if (differences.Count < 3 && healthProfileComparison["weightLoss"].Winner is "A" or "B")
            {
                differences.Add($"Produto {healthProfileComparison["weightLoss"].Winner} é mais alinhado com objetivos de emagrecimento.");
            }

            if (differences.Count < 3 && healthProfileComparison["diabetic"].Winner is "A" or "B")
            {
                differences.Add($"Produto {healthProfileComparison["diabetic"].Winner} oferece melhor adequação para controle glicêmico.");
            }

            if (differences.Count == 0)
            {
                differences.Add("Os produtos apresentam perfis muito semelhantes com os dados disponíveis.");
            }

            return differences
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();
        }

        private static bool HasSugarDifference(
            ComparableAnalysis analysisA,
            ComparableAnalysis analysisB,
            out string message)
        {
            var offenderAIsSugar = string.Equals(analysisA.PrincipalOffender, "açúcar", StringComparison.OrdinalIgnoreCase);
            var offenderBIsSugar = string.Equals(analysisB.PrincipalOffender, "açúcar", StringComparison.OrdinalIgnoreCase);

            if (offenderAIsSugar == offenderBIsSugar)
            {
                message = string.Empty;
                return false;
            }

            message = offenderAIsSugar
                ? "Produto A concentra maior atenção em açúcar do que o Produto B."
                : "Produto B concentra maior atenção em açúcar do que o Produto A.";

            return true;
        }

        private static bool HasLabelContrast(int scoreA, int scoreB, out string message)
        {
            if (scoreA >= 68 && scoreB < 40)
            {
                message = "Produto B é menos adequado para consumo frequente.";
                return true;
            }

            if (scoreB >= 68 && scoreA < 40)
            {
                message = "Produto A é menos adequado para consumo frequente.";
                return true;
            }

            message = string.Empty;
            return false;
        }

        private static string BuildWinnerReason(
            ComparableAnalysis analysisA,
            ComparableAnalysis analysisB,
            string winner,
            int difference,
            IReadOnlyDictionary<string, HealthProfileComparisonDto> healthProfileComparison)
        {
            if (winner == "tie")
            {
                if (analysisA.Score < 40 && analysisB.Score < 40)
                {
                    return "Ambos têm pontuação baixa e diferenças pequenas.";
                }

                if (analysisA.Score > 70 && analysisB.Score > 70)
                {
                    return "Ambos têm boa pontuação e perfis bastante próximos.";
                }

                return "Apesar de ambos serem semelhantes, não há vantagem nutricional relevante.";
            }

            var reasonParts = new List<string>
            {
                difference > 10 ? "Melhor pontuação nutricional" : "Leve vantagem na pontuação nutricional"
            };

            if (healthProfileComparison["diabetic"].Winner == winner)
            {
                reasonParts.Add("melhor adequação para controle glicêmico");
            }
            else if (healthProfileComparison["bloodPressure"].Winner == winner)
            {
                reasonParts.Add("melhor adequação para pressão arterial");
            }
            else if (healthProfileComparison["weightLoss"].Winner == winner)
            {
                reasonParts.Add("melhor adequação para emagrecimento");
            }
            else if (HasLowerSugarImpact(winner, analysisA, analysisB))
            {
                reasonParts.Add("menor impacto relacionado ao açúcar");
            }
            else
            {
                reasonParts.Add("melhor equilíbrio geral");
            }

            return string.Join(" e ", reasonParts);
        }

        private static bool HasLowerSugarImpact(string winner, ComparableAnalysis analysisA, ComparableAnalysis analysisB)
        {
            var sugarA = string.Equals(analysisA.PrincipalOffender, "açúcar", StringComparison.OrdinalIgnoreCase);
            var sugarB = string.Equals(analysisB.PrincipalOffender, "açúcar", StringComparison.OrdinalIgnoreCase);

            return winner switch
            {
                "A" => !sugarA && sugarB,
                "B" => !sugarB && sugarA,
                _ => false
            };
        }

        private static string BuildRecommendation(int scoreA, int scoreB, string winner, string comparisonLevel)
        {
            if (comparisonLevel == "ambos_ruins")
            {
                return "Evite ambos e busque alternativas melhores";
            }

            if (winner == "tie")
            {
                return scoreA > 70 && scoreB > 70
                    ? "Ambos são boas opções, com perfis muito próximos"
                    : "Ambos podem ser consumidos com moderação";
            }

            return winner == "A"
                ? "Prefira o Produto A"
                : "Prefira o Produto B";
        }

        private static double CalculateConfidence(ComparableAnalysis analysisA, ComparableAnalysis analysisB)
        {
            var bothFull = analysisA.AnalysisMode == AnalysisMode.FullNutritionLabel
                && analysisB.AnalysisMode == AnalysisMode.FullNutritionLabel;
            var oneFull = analysisA.AnalysisMode == AnalysisMode.FullNutritionLabel
                || analysisB.AnalysisMode == AnalysisMode.FullNutritionLabel;

            var (min, max, baseline) = bothFull
                ? (0.85d, 0.95d, 0.92d)
                : oneFull
                    ? (0.65d, 0.80d, 0.74d)
                    : (0.50d, 0.70d, 0.60d);

            var indeterminateCount = CountIndeterminateProfiles(analysisA.Classification)
                + CountIndeterminateProfiles(analysisB.Classification);

            var adjusted = baseline - Math.Min(0.12d, indeterminateCount * 0.03d);
            return Math.Round(Math.Clamp(adjusted, min, max), 2);
        }

        private static int CountIndeterminateProfiles(ProductClassificationDto classification)
        {
            return GetProfiles(classification)
                .Count(profile => string.IsNullOrWhiteSpace(profile.Value.Status)
                    || profile.Value.Status.Equals("indeterminado", StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<KeyValuePair<string, HealthProfileResult>> GetProfiles(ProductClassificationDto classification)
        {
            return new Dictionary<string, HealthProfileResult>
            {
                ["diabetic"] = classification.Diabetic ?? new HealthProfileResult(),
                ["bloodPressure"] = classification.BloodPressure ?? new HealthProfileResult(),
                ["weightLoss"] = classification.WeightLoss ?? new HealthProfileResult(),
                ["muscleGain"] = classification.MuscleGain ?? new HealthProfileResult()
            };
        }

        private sealed class ComparableAnalysis
        {
            public string ProductName { get; set; } = string.Empty;
            public string? Brand { get; set; }
            public string? Category { get; set; }
            public AnalysisMode AnalysisMode { get; set; }
            public int Score { get; set; }
            public string ScoreLabel { get; set; } = string.Empty;
            public string? PrincipalOffender { get; set; }
            public ProductClassificationDto Classification { get; set; } = new();
            public EstimatedNutritionProfileDto? EstimatedNutritionProfile { get; set; }
        }
    }
}
