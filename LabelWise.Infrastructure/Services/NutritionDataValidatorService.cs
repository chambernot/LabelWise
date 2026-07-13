using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Domain.Enums;

namespace LabelWise.Infrastructure.Services
{
    /// <summary>
    /// Implementação do validador e enriquecedor de dados nutricionais.
    ///
    /// Pipeline aplicada:
    ///   1. ValidateAndNormalize  — remove valores impossíveis
    ///   2. HasReliableData       — avalia confiabilidade dos dados
    ///   3. ApplyFallbackIfNeeded — preenche nulos com médias por categoria
    ///   4. DetectInconsistency   — sinaliza inconsistências calóricas
    ///   5. DetermineProcessingLevel
    ///   6. DetectPrincipalOffender
    /// </summary>
    public class NutritionDataValidatorService : INutritionDataValidatorService, INutritionValidator, INutritionEnricher
    {
        // ────────────────────────────────────────────────────────────────────────
        // Faixas plausíveis por 100 g
        // ────────────────────────────────────────────────────────────────────────
        private const double MaxCalories    = 900;
        private const double MaxSugar       = 100;
        private const double MaxProtein     = 100;
        private const double MaxFat         = 100;
        private const double MaxSodiumMg    = 5000;
        private const double MaxCarbs       = 100;

        // ────────────────────────────────────────────────────────────────────────
        // Perfis de fallback por categoria (médias típicas por 100 g)
        // ────────────────────────────────────────────────────────────────────────
        private static readonly Dictionary<string, CategoryFallbackProfile> FallbackProfiles =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["refrigerante"]       = new(Calories: 42,  Sugar: 10.5, Protein: 0,   Fat: 0,    Sodium: 10),
                ["suco"]               = new(Calories: 45,  Sugar: 10,   Protein: 0.3, Fat: 0.1,  Sodium: 5),
                ["biscoito"]           = new(Calories: 480, Sugar: 22,   Protein: 6,   Fat: 20,   Sodium: 400),
                ["salgadinho"]         = new(Calories: 520, Sugar: 2,    Protein: 6,   Fat: 28,   Sodium: 650),
                ["iogurte"]            = new(Calories: 80,  Sugar: 11,   Protein: 3.5, Fat: 2.5,  Sodium: 50),
                ["iogurte adoçado"]    = new(Calories: 95,  Sugar: 13,   Protein: 3,   Fat: 2.5,  Sodium: 55),
                ["leite"]              = new(Calories: 61,  Sugar: 4.8,  Protein: 3.2, Fat: 3.3,  Sodium: 44),
                ["queijo"]             = new(Calories: 350, Sugar: 0.5,  Protein: 22,  Fat: 28,   Sodium: 600),
                ["pão"]                = new(Calories: 265, Sugar: 5,    Protein: 9,   Fat: 3,    Sodium: 500),
                ["cereal"]             = new(Calories: 380, Sugar: 25,   Protein: 8,   Fat: 4,    Sodium: 300),
                ["chocolate"]          = new(Calories: 530, Sugar: 50,   Protein: 6,   Fat: 30,   Sodium: 80),
                ["arroz"]              = new(Calories: 360, Sugar: 0,    Protein: 7,   Fat: 0.5,  Sodium: 5),
                ["feijão"]             = new(Calories: 330, Sugar: 2,    Protein: 22,  Fat: 1.5,  Sodium: 10),
                ["frango"]             = new(Calories: 165, Sugar: 0,    Protein: 31,  Fat: 3.6,  Sodium: 74),
                ["carne bovina"]       = new(Calories: 250, Sugar: 0,    Protein: 26,  Fat: 15,   Sodium: 65),
                ["fruta"]              = new(Calories: 55,  Sugar: 12,   Protein: 0.7, Fat: 0.3,  Sodium: 2),
                ["verdura"]            = new(Calories: 25,  Sugar: 2,    Protein: 2,   Fat: 0.3,  Sodium: 30),
                ["molho"]              = new(Calories: 100, Sugar: 8,    Protein: 2,   Fat: 5,    Sodium: 700),
                ["macarrão"]           = new(Calories: 360, Sugar: 2,    Protein: 12,  Fat: 1.5,  Sodium: 5),
                ["sorvete"]            = new(Calories: 200, Sugar: 22,   Protein: 3.5, Fat: 10,   Sodium: 70),
                ["barra de cereal"]    = new(Calories: 400, Sugar: 28,   Protein: 6,   Fat: 10,   Sodium: 150),
                ["proteína em pó"]     = new(Calories: 380, Sugar: 5,    Protein: 70,  Fat: 5,    Sodium: 200),
                ["azeite"]             = new(Calories: 884, Sugar: 0,    Protein: 0,   Fat: 100,  Sodium: 0),
                ["óleo"]               = new(Calories: 884, Sugar: 0,    Protein: 0,   Fat: 100,  Sodium: 0),
            };

        // ────────────────────────────────────────────────────────────────────────
        // Categorias por nível de processamento
        // ────────────────────────────────────────────────────────────────────────
        private static readonly HashSet<string> UltraProcessedCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            "refrigerante", "biscoito", "salgadinho", "chocolate", "sorvete",
            "barra de cereal", "cereal", "molho", "embutido", "salsicha",
            "hamburguer", "nugget", "macarrão instantâneo", "sopa instantânea"
        };

        private static readonly HashSet<string> ProcessedCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            "iogurte", "iogurte adoçado", "queijo", "pão", "macarrão",
            "leite uht", "proteína em pó", "suco", "conserva"
        };

        private static readonly HashSet<string> NaturalCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            "arroz", "feijão", "frango", "carne bovina", "fruta", "verdura",
            "legume", "leite", "ovo", "azeite", "óleo"
        };

        private static readonly HashSet<string> AdditiveKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "corante", "aromatizante", "conservante", "antioxidante", "estabilizante",
            "emulsificante", "espessante", "acidulante", "umectante", "realçador",
            "maltodextrina", "xarope de milho", "glutamato", "benzoato", "sorbato",
            "nitrito", "nitrato", "fosfato", "carragena", "polissorbato",
            "parcialmente hidrogenada", "hidrogenada"
        };

        // ════════════════════════════════════════════════════════════════════════
        // INutritionValidator — validação pura (sem fallback, sem enriquecimento)
        // ════════════════════════════════════════════════════════════════════════

        public NutritionSanitizationResult Validate(EstimatedNutritionProfileDto? profile)
        {
            var warnings = new List<string>();
            var normalized = ValidateAndNormalize(profile, warnings);
            bool inconsistency = DetectCaloriesInconsistency(normalized);

            if (inconsistency)
                warnings.Add("Inconsistência calórica detectada: calorias declaradas são menores que o esperado pelos macronutrientes.");

            if (normalized.EstimatedSugarPer100g.HasValue
                && normalized.EstimatedCarbsPer100g.HasValue
                && normalized.EstimatedSugarPer100g > 0
                && Math.Abs(normalized.EstimatedSugarPer100g.Value - normalized.EstimatedCarbsPer100g.Value) < 0.5)
            {
                warnings.Add("Todos os carboidratos são açúcares.");
            }

            return new NutritionSanitizationResult
            {
                Profile                 = normalized,
                Warnings                = warnings,
                HasCaloriesInconsistency = inconsistency
            };
        }

        // ════════════════════════════════════════════════════════════════════════
        // INutritionEnricher — fallback + processamento + confiança
        // ════════════════════════════════════════════════════════════════════════

        public NutritionEnrichedData Enrich(
            NutritionSanitizationResult validated,
            string? category,
            AnalysisMode analysisMode,
            IReadOnlyList<string>? ingredients)
        {
            var profile  = validated.Profile;
            var warnings = new List<string>(validated.Warnings);

            bool reliable     = HasReliableData(profile);
            bool fallbackUsed = false;

            // ✅ NÃO aplicar fallback se dados vieram da OpenAI E são confiáveis
            bool skipFallback = profile.IsFromOpenAI && reliable;

            if (!skipFallback && (!reliable || analysisMode == AnalysisMode.FrontOfPackageOnly))
                fallbackUsed = ApplyFallbackIfNeeded(profile, category, warnings);

            string processingLevel = DetermineProcessingLevel(category, profile.EstimatedSugarPer100g, ingredients);
            string confidence      = DetermineConfidence(profile, reliable, fallbackUsed, validated.HasCaloriesInconsistency);

            return new NutritionEnrichedData
            {
                NormalizedProfile        = profile,
                FallbackUsed             = fallbackUsed,
                Confidence               = confidence,
                HasCaloriesInconsistency = validated.HasCaloriesInconsistency,
                ProcessingLevel          = processingLevel,
                PrincipalOffender        = string.Empty,
                ValidationWarnings       = warnings
            };
        }

        // ════════════════════════════════════════════════════════════════════════
        // INutritionDataValidatorService — backward compat: delega para Validate + Enrich
        // ════════════════════════════════════════════════════════════════════════

        public NutritionEnrichedData ValidateAndEnrich(
            EstimatedNutritionProfileDto? profile,
            string? category,
            AnalysisMode analysisMode,
            IReadOnlyList<string>? ingredients)
        {
            var validated = ((INutritionValidator)this).Validate(profile);
            return Enrich(validated, category, analysisMode, ingredients);
        }

        // ════════════════════════════════════════════════════════════════════════
        // STEP 1 – ValidateAndNormalize
        // ════════════════════════════════════════════════════════════════════════

        public static EstimatedNutritionProfileDto ValidateAndNormalize(
            EstimatedNutritionProfileDto? input,
            List<string> warnings)
        {
            // Começamos com um clone shallow; dados da IA são prioridade
            var result = input == null
                ? new EstimatedNutritionProfileDto()
                : new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g            = input.CaloriesPer100g,
                    CaloriesPer100ml           = input.CaloriesPer100ml,
                    EstimatedPackageCalories   = input.EstimatedPackageCalories,
                    EstimatedCarbsPer100g      = input.EstimatedCarbsPer100g,
                    EstimatedSugarPer100g      = input.EstimatedSugarPer100g,
                    EstimatedAddedSugarPer100g = input.EstimatedAddedSugarPer100g,
                    EstimatedSaturatedFatPer100g = input.EstimatedSaturatedFatPer100g,
                    EstimatedProteinPer100g    = input.EstimatedProteinPer100g,
                    EstimatedSodiumPer100g     = input.EstimatedSodiumPer100g,
                    EstimatedFiberPer100g      = input.EstimatedFiberPer100g,
                    EstimatedFatPer100g        = input.EstimatedFatPer100g,
                    Basis                      = input.Basis,
                    ParserConfidence           = input.ParserConfidence,
                    NutritionUnit              = input.NutritionUnit,
                    IsCorrectedByOcr           = input.IsCorrectedByOcr,
                    IsFromOpenAI               = input.IsFromOpenAI,  // ✅ Preservar flag
                    NutritionConfidence        = input.NutritionConfidence,
                    DataSource                 = input.DataSource != null
                        ? new Dictionary<string, string>(input.DataSource, StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                };

            // Validar cada campo com limites plausíveis
            result.CaloriesPer100g = Sanitize(result.CaloriesPer100g, 0, MaxCalories,
                "Calorias fora da faixa plausível (0–900 kcal/100g)", warnings);

            result.CaloriesPer100ml = Sanitize(result.CaloriesPer100ml, 0, MaxCalories,
                "Calorias fora da faixa plausível (0–900 kcal/100ml)", warnings);

            result.EstimatedSugarPer100g = Sanitize(result.EstimatedSugarPer100g, 0, MaxSugar,
                "Açúcar fora da faixa plausível (0–100 g/100g)", warnings);

            result.EstimatedAddedSugarPer100g = Sanitize(result.EstimatedAddedSugarPer100g, 0, MaxSugar,
                "Açúcar adicionado fora da faixa plausível", warnings);

            result.EstimatedProteinPer100g = Sanitize(result.EstimatedProteinPer100g, 0, MaxProtein,
                "Proteína fora da faixa plausível (0–100 g/100g)", warnings);

            result.EstimatedFatPer100g = Sanitize(result.EstimatedFatPer100g, 0, MaxFat,
                "Gordura total fora da faixa plausível (0–100 g/100g)", warnings);

            result.EstimatedSaturatedFatPer100g = Sanitize(result.EstimatedSaturatedFatPer100g, 0, MaxFat,
                "Gordura saturada fora da faixa plausível", warnings);

            result.EstimatedSodiumPer100g = Sanitize(result.EstimatedSodiumPer100g, 0, MaxSodiumMg,
                "Sódio fora da faixa plausível (0–5000 mg/100g)", warnings);

            result.EstimatedCarbsPer100g = Sanitize(result.EstimatedCarbsPer100g, 0, MaxCarbs,
                "Carboidratos fora da faixa plausível (0–100 g/100g)", warnings);

            result.EstimatedFiberPer100g = Sanitize(result.EstimatedFiberPer100g, 0, MaxCarbs,
                "Fibra fora da faixa plausível (0–100 g/100g)", warnings);

            // Açúcar adicionado não pode ser maior que açúcar total
            if (result.EstimatedAddedSugarPer100g.HasValue
                && result.EstimatedSugarPer100g.HasValue
                && result.EstimatedAddedSugarPer100g > result.EstimatedSugarPer100g)
            {
                warnings.Add("Açúcar adicionado maior que açúcar total — valor corrigido.");
                result.EstimatedAddedSugarPer100g = result.EstimatedSugarPer100g;
            }

            // Gordura saturada não pode ser maior que gordura total
            if (result.EstimatedSaturatedFatPer100g.HasValue
                && result.EstimatedFatPer100g.HasValue
                && result.EstimatedSaturatedFatPer100g > result.EstimatedFatPer100g)
            {
                warnings.Add("Gordura saturada maior que gordura total — valor corrigido.");
                result.EstimatedSaturatedFatPer100g = result.EstimatedFatPer100g;
            }

            EnsureUnitConsistency(result, warnings);

            return result;
        }

        // ════════════════════════════════════════════════════════════════════════
        // STEP 2 – HasReliableData
        // ════════════════════════════════════════════════════════════════════════

        public static bool HasReliableData(EstimatedNutritionProfileDto profile)
        {
            int count = 0;
            if (profile.CaloriesPer100g.HasValue || profile.CaloriesPer100ml.HasValue) count++;
            if (profile.EstimatedSugarPer100g.HasValue)  count++;
            if (profile.EstimatedProteinPer100g.HasValue) count++;
            if (profile.EstimatedFatPer100g.HasValue)    count++;
            if (profile.EstimatedSodiumPer100g.HasValue) count++;
            return count >= 3;
        }

        // ════════════════════════════════════════════════════════════════════════
        // STEP 3 – ApplyFallbackIfNeeded
        // ════════════════════════════════════════════════════════════════════════

        public static bool ApplyFallbackIfNeeded(
            EstimatedNutritionProfileDto profile,
            string? category,
            List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return false;
            }

            var fallback = FindFallback(category);
            if (fallback == null)
            {
                return false;
            }

            bool applied = false;

            if (!profile.CaloriesPer100g.HasValue && fallback.Calories.HasValue)
            {
                profile.CaloriesPer100g = fallback.Calories;
                warnings.Add($"Calorias estimadas por fallback de categoria '{category}'.");
                applied = true;
            }

            if (!profile.EstimatedSugarPer100g.HasValue && fallback.Sugar.HasValue)
            {
                profile.EstimatedSugarPer100g = fallback.Sugar;
                warnings.Add($"Açúcar estimado por fallback de categoria '{category}'.");
                applied = true;
            }

            if (!profile.EstimatedProteinPer100g.HasValue && fallback.Protein.HasValue)
            {
                profile.EstimatedProteinPer100g = fallback.Protein;
                warnings.Add($"Proteína estimada por fallback de categoria '{category}'.");
                applied = true;
            }

            if (!profile.EstimatedFatPer100g.HasValue && fallback.Fat.HasValue)
            {
                profile.EstimatedFatPer100g = fallback.Fat;
                warnings.Add($"Gordura estimada por fallback de categoria '{category}'.");
                applied = true;
            }

            if (!profile.EstimatedSodiumPer100g.HasValue && fallback.Sodium.HasValue)
            {
                profile.EstimatedSodiumPer100g = fallback.Sodium;
                warnings.Add($"Sódio estimado por fallback de categoria '{category}'.");
                applied = true;
            }

            if (applied)
            {
                profile.Basis = $"Estimativa por fallback de categoria: {category}";
            }

            return applied;
        }

        // ════════════════════════════════════════════════════════════════════════
        // STEP 4 – DetectCaloriesInconsistency
        // ════════════════════════════════════════════════════════════════════════

        public static bool DetectCaloriesInconsistency(EstimatedNutritionProfileDto profile)
        {
            if (!profile.CaloriesPer100g.HasValue)
            {
                return false;
            }

            double expectedMin = 0;

            if (profile.EstimatedProteinPer100g.HasValue)
                expectedMin += profile.EstimatedProteinPer100g.Value * 4;

            if (profile.EstimatedCarbsPer100g.HasValue)
                expectedMin += profile.EstimatedCarbsPer100g.Value * 4;

            if (profile.EstimatedFatPer100g.HasValue)
                expectedMin += profile.EstimatedFatPer100g.Value * 9;

            if (expectedMin <= 0)
            {
                return false;
            }

            return profile.CaloriesPer100g.Value < (expectedMin * 0.5);
        }

        // ════════════════════════════════════════════════════════════════════════
        // STEP 5 – DetermineProcessingLevel
        // ════════════════════════════════════════════════════════════════════════

        public static string DetermineProcessingLevel(
            string? category,
            double? sugarPer100g,
            IReadOnlyList<string>? ingredients)
        {
            string level = "desconhecido";

            if (!string.IsNullOrWhiteSpace(category))
            {
                if (UltraProcessedCategories.Contains(category))
                    level = "ultraprocessado";
                else if (ProcessedCategories.Contains(category))
                    level = "processado";
                else if (NaturalCategories.Contains(category))
                    level = "in_natura";
            }

            // Presença de aditivos → elevar para ultraprocessado
            if (level != "ultraprocessado" && ingredients != null)
            {
                foreach (var ingredient in ingredients)
                {
                    foreach (var keyword in AdditiveKeywords)
                    {
                        if (ingredient.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        {
                            level = level == "in_natura" ? "processado" : "ultraprocessado";
                            break;
                        }
                    }
                    if (level == "ultraprocessado") break;
                }
            }

            // Alto açúcar eleva a severidade um nível (se ainda não for ultraprocessado)
            if (sugarPer100g > 10 && level == "in_natura")
            {
                level = "processado";
            }

            return level;
        }

        // ════════════════════════════════════════════════════════════════════════
        // STEP 2b – ValidateSaturatedFat (Req #2)
        // Regra: se gordura >= 40 e saturada < 30% da total → suspeito
        // ════════════════════════════════════════════════════════════════════════

        public static bool IsSaturatedFatSuspicious(EstimatedNutritionProfileDto profile)
        {
            if (!profile.EstimatedFatPer100g.HasValue || !profile.EstimatedSaturatedFatPer100g.HasValue)
                return false;

            var fat    = profile.EstimatedFatPer100g.Value;
            var satFat = profile.EstimatedSaturatedFatPer100g.Value;

            return fat >= 40 && satFat < (fat * 0.3);
        }

        // ════════════════════════════════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════════════════════════════════

        private static double? Sanitize(double? value, double min, double max, string message, List<string> warnings)
        {
            if (!value.HasValue) return null;
            if (value < min || value > max)
            {
                warnings.Add(message);
                return null;
            }
            return value;
        }

        private static void EnsureUnitConsistency(EstimatedNutritionProfileDto profile, List<string> warnings)
        {
            var basis = (profile.Basis ?? string.Empty).ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(profile.NutritionUnit))
            {
                if (basis.Contains("100 ml") || profile.CaloriesPer100ml.HasValue)
                    profile.NutritionUnit = "ml";
                else if (basis.Contains("100 g") || profile.CaloriesPer100g.HasValue)
                    profile.NutritionUnit = "g";
            }

            if (profile.NutritionUnit == "ml")
            {
                if (!profile.CaloriesPer100ml.HasValue && profile.CaloriesPer100g.HasValue)
                {
                    profile.CaloriesPer100ml = profile.CaloriesPer100g;
                    profile.CaloriesPer100g = null;
                    warnings.Add("Calorias ajustadas para base 100 ml para manter consistência.");
                }

                if (profile.CaloriesPer100g.HasValue)
                {
                    profile.CaloriesPer100g = null;
                    warnings.Add("Calorias por 100g removidas por conflito com unidade ml.");
                }
            }

            if (profile.NutritionUnit == "g")
            {
                if (!profile.CaloriesPer100g.HasValue && profile.CaloriesPer100ml.HasValue)
                {
                    profile.CaloriesPer100g = profile.CaloriesPer100ml;
                    profile.CaloriesPer100ml = null;
                    warnings.Add("Calorias ajustadas para base 100 g para manter consistência.");
                }

                if (profile.CaloriesPer100g.HasValue)
                {
                    profile.CaloriesPer100ml = null;
                }
            }
        }

        private static string DetermineConfidence(
            EstimatedNutritionProfileDto profile,
            bool reliable,
            bool fallbackUsed,
            bool caloriesInconsistency)
        {
            // Contagem de campos não-nulos entre os 7 principais
            int fieldScore = 0;
            if (profile.CaloriesPer100g.HasValue || profile.CaloriesPer100ml.HasValue) fieldScore++;
            if (profile.EstimatedCarbsPer100g.HasValue)      fieldScore++;
            if (profile.EstimatedFatPer100g.HasValue)        fieldScore++;
            if (profile.EstimatedProteinPer100g.HasValue)    fieldScore++;
            if (profile.EstimatedSugarPer100g.HasValue)      fieldScore++;
            if (profile.EstimatedSodiumPer100g.HasValue)     fieldScore++;
            if (profile.EstimatedSaturatedFatPer100g.HasValue) fieldScore++;

            // Determinar incertezas
            bool satFatSuspicious   = IsSaturatedFatSuspicious(profile);
            bool missingKeyFields   = !profile.EstimatedSugarPer100g.HasValue
                                   || !profile.EstimatedSodiumPer100g.HasValue;
            bool hasWarnings        = caloriesInconsistency || satFatSuspicious;

            // Regras
            if (fallbackUsed || fieldScore < 3 || (caloriesInconsistency && fieldScore < 5))
                return "baixa";

            if (fieldScore >= 6 && !hasWarnings && !missingKeyFields && !fallbackUsed)
                return "alta";

            return "media";
        }

        private static CategoryFallbackProfile? FindFallback(string category)
        {
            // Busca exata
            if (FallbackProfiles.TryGetValue(category, out var exact))
                return exact;

            // Busca parcial (contém)
            foreach (var kvp in FallbackProfiles)
            {
                if (category.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase)
                    || kvp.Key.Contains(category, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        // ────────────────────────────────────────────────────────────────────────
        // Nested helper record
        // ────────────────────────────────────────────────────────────────────────

        private sealed record CategoryFallbackProfile(
            double? Calories,
            double? Sugar,
            double? Protein,
            double? Fat,
            double? Sodium);
    }
}
