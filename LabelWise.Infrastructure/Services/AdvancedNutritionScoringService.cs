using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;

namespace LabelWise.Infrastructure.Services
{
    /// <summary>
    /// Implementação do motor de scoring nutricional avançado.
    /// Score composto por 5 blocos com pesos distintos.
    /// </summary>
    public class AdvancedNutritionScoringService : IAdvancedNutritionScoringService
    {
        // Pesos dos blocos (somam 1.0)
        private const double WeightNutritionalQuality = 0.40;
        private const double WeightProcessingLevel    = 0.20;
        private const double WeightProfileAdequacy    = 0.20;
        private const double WeightClaimsEvaluation   = 0.10;
        private const double WeightNutritionalDensity = 0.10;

        // Aditivos / conservantes comuns que indicam ultraprocessamento
        private static readonly HashSet<string> AdditiveKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "corante", "aromatizante", "conservante", "antioxidante", "estabilizante",
            "emulsificante", "espessante", "acidulante", "umectante", "realçador",
            "regulador", "antiumectante", "antiespumante", "goma", "maltodextrina",
            "xarope de milho", "glutamato", "benzoato", "sorbato", "nitrito",
            "nitrato", "dióxido", "fosfato", "citrato de sódio", "carragena",
            "lecitina", "polissorbato", "hidrogenada", "parcialmente hidrogenada"
        };

        public AdvancedNutritionScoreResult Calculate(
            EstimatedNutritionProfileDto? profile,
            IReadOnlyList<string>? ingredients,
            IReadOnlyList<string>? visibleClaims)
        {
            var warnings  = new List<string>();
            var highlights = new List<string>();

            double qualityScore   = CalculateNutritionalQuality(profile, highlights, warnings);
            double processingScore = EvaluateProcessingLevel(ingredients, warnings);
            double profileBase    = CalculateProfileScore(profile, "default", warnings);
            double claimsScore    = EvaluateClaims(profile, visibleClaims, warnings);
            double densityScore   = CalculateDensity(profile, highlights);

            double overall = (qualityScore   * WeightNutritionalQuality)
                           + (processingScore * WeightProcessingLevel)
                           + (profileBase     * WeightProfileAdequacy)
                           + (claimsScore     * WeightClaimsEvaluation)
                           + (densityScore    * WeightNutritionalDensity);

            overall = ApplyGlobalPenalties(overall, profile, warnings);

            return new AdvancedNutritionScoreResult
            {
                OverallScore       = Clamp(overall),
                DiabetesScore      = Clamp(ApplyGlobalPenalties(BuildProfileOverall(profile, ingredients, visibleClaims, "diabetes"),    profile, new List<string>())),
                HypertensionScore  = Clamp(ApplyGlobalPenalties(BuildProfileOverall(profile, ingredients, visibleClaims, "hypertension"), profile, new List<string>())),
                WeightLossScore    = Clamp(ApplyGlobalPenalties(BuildProfileOverall(profile, ingredients, visibleClaims, "weightloss"),   profile, new List<string>())),
                MuscleGainScore    = Clamp(ApplyGlobalPenalties(BuildProfileOverall(profile, ingredients, visibleClaims, "musclegain"),   profile, new List<string>())),
                Breakdown = new ScoreBreakdown
                {
                    NutritionalQuality = Clamp(qualityScore),
                    ProcessingLevel    = Clamp(processingScore),
                    ProfileAdequacy    = Clamp(profileBase),
                    ClaimsEvaluation   = Clamp(claimsScore),
                    NutritionalDensity = Clamp(densityScore)
                },
                Warnings   = warnings,
                Highlights = highlights
            };
        }

        // ────────────────────────────────────────────────────────────────
        // BLOCO 1 — Qualidade Nutricional (peso 40%)
        // ────────────────────────────────────────────────────────────────

        private double CalculateNutritionalQuality(
            EstimatedNutritionProfileDto? p,
            List<string> highlights,
            List<string> warnings)
        {
            if (p == null) return 50.0;

            var scores = new List<double>
            {
                ScoreSugar(p.EstimatedSugarPer100g),
                ScoreSodium(p.EstimatedSodiumPer100g),
                ScoreProtein(p.EstimatedProteinPer100g),
                ScoreFiber(p.EstimatedFiberPer100g),
                ScoreSaturatedFat(p.EstimatedSaturatedFatPer100g),
                ScoreCalories(p.CaloriesPer100g)
            };

            CollectHighlightsAndWarnings(p, highlights, warnings);

            // Cada nutriente vale 0–10; normalizar para 0–100
            double avg = scores.Average() * 10.0;
            return avg;
        }

        private static double ScoreSugar(double? value)
        {
            if (value == null) return 5.0;
            return value <= 5 ? 10.0 : value <= 10 ? 5.0 : 0.0;
        }

        private static double ScoreSodium(double? value)
        {
            if (value == null) return 5.0;
            return value <= 140 ? 10.0 : value <= 400 ? 5.0 : 0.0;
        }

        private static double ScoreProtein(double? value)
        {
            if (value == null) return 5.0;
            return value >= 10 ? 10.0 : value >= 5 ? 5.0 : 0.0;
        }

        private static double ScoreFiber(double? value)
        {
            if (value == null) return 5.0;
            return value >= 5 ? 10.0 : value >= 2 ? 5.0 : 0.0;
        }

        private static double ScoreSaturatedFat(double? value)
        {
            if (value == null) return 5.0;
            return value <= 2 ? 10.0 : value <= 5 ? 5.0 : 0.0;
        }

        private static double ScoreCalories(double? value)
        {
            if (value == null) return 5.0;
            return value <= 100 ? 10.0 : value <= 250 ? 5.0 : 0.0;
        }

        private static void CollectHighlightsAndWarnings(
            EstimatedNutritionProfileDto p,
            List<string> highlights,
            List<string> warnings)
        {
            if (p.EstimatedProteinPer100g >= 10) highlights.Add("Alto teor de proteína");
            if (p.EstimatedFiberPer100g >= 5) highlights.Add("Boa fonte de fibras");
            if (p.EstimatedSodiumPer100g <= 140) highlights.Add("Baixo teor de sódio");
            if (p.EstimatedSugarPer100g <= 5) highlights.Add("Baixo teor de açúcar");

            if (p.EstimatedSugarPer100g > 10) warnings.Add("Alto teor de açúcar");
            if (p.EstimatedSodiumPer100g > 400) warnings.Add("Alto teor de sódio");
            if (p.EstimatedSaturatedFatPer100g > 5) warnings.Add("Alta gordura saturada");
            if (p.CaloriesPer100g > 250) warnings.Add("Alta densidade calórica");
        }

        // ────────────────────────────────────────────────────────────────
        // BLOCO 2 — Nível de Processamento (peso 20%)
        // ────────────────────────────────────────────────────────────────

        private double EvaluateProcessingLevel(IReadOnlyList<string>? ingredients, List<string> warnings)
        {
            if (ingredients == null || ingredients.Count == 0) return 50.0;

            double score = 100.0;

            // Penalizar pelo número de ingredientes
            if (ingredients.Count > 10)
            {
                score -= Math.Min(30.0, (ingredients.Count - 10) * 3.0);
                warnings.Add("Produto com muitos ingredientes");
            }

            // Detectar aditivos
            int additiveCount = 0;
            foreach (var ingredient in ingredients)
            {
                if (AdditiveKeywords.Any(kw => ingredient.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                    additiveCount++;
            }

            if (additiveCount > 0)
            {
                score -= Math.Min(50.0, additiveCount * 10.0);
                warnings.Add($"Contém {additiveCount} aditivo(s) identificado(s)");
            }

            return Math.Max(0, score);
        }

        // ────────────────────────────────────────────────────────────────
        // BLOCO 3 — Adequação ao Perfil (peso 20%)
        // ────────────────────────────────────────────────────────────────

        private double CalculateProfileScore(
            EstimatedNutritionProfileDto? p,
            string profile,
            List<string> warnings)
        {
            if (p == null) return 50.0;

            double score = 70.0; // base neutra

            score += profile switch
            {
                "diabetes"     => CalcDiabetesAdjustment(p),
                "hypertension" => CalcHypertensionAdjustment(p),
                "weightloss"   => CalcWeightLossAdjustment(p),
                "musclegain"   => CalcMuscleGainAdjustment(p),
                _              => CalcDefaultAdjustment(p)
            };

            return score;
        }

        private static double CalcDiabetesAdjustment(EstimatedNutritionProfileDto p)
        {
            double adj = 0;
            if (p.EstimatedSugarPer100g > 10) adj -= 20;
            else if (p.EstimatedSugarPer100g <= 5) adj += 10;
            if (p.EstimatedFiberPer100g >= 5) adj += 10;
            else if (p.EstimatedFiberPer100g >= 2) adj += 5;
            if (p.EstimatedCarbsPer100g > 50) adj -= 10;
            return adj;
        }

        private static double CalcHypertensionAdjustment(EstimatedNutritionProfileDto p)
        {
            double adj = 0;
            if (p.EstimatedSodiumPer100g > 400) adj -= 20;
            else if (p.EstimatedSodiumPer100g <= 140) adj += 15;
            return adj;
        }

        private static double CalcWeightLossAdjustment(EstimatedNutritionProfileDto p)
        {
            double adj = 0;
            if (p.CaloriesPer100g > 250) adj -= 15;
            if (p.EstimatedSugarPer100g > 10) adj -= 10;
            if (p.EstimatedFiberPer100g >= 5) adj += 10;
            if (p.EstimatedProteinPer100g >= 10) adj += 10;
            return adj;
        }

        private static double CalcMuscleGainAdjustment(EstimatedNutritionProfileDto p)
        {
            double adj = 0;
            if (p.EstimatedProteinPer100g >= 10) adj += 20;
            else if (p.EstimatedProteinPer100g >= 5) adj += 10;
            if (p.EstimatedSugarPer100g > 10) adj -= 10;
            if (p.CaloriesPer100g is >= 100 and <= 250) adj += 5;
            return adj;
        }

        private static double CalcDefaultAdjustment(EstimatedNutritionProfileDto p)
        {
            double adj = 0;
            if (p.EstimatedProteinPer100g >= 10) adj += 10;
            if (p.EstimatedFiberPer100g >= 5) adj += 10;
            if (p.EstimatedSugarPer100g > 10) adj -= 10;
            if (p.EstimatedSodiumPer100g > 400) adj -= 10;
            return adj;
        }

        // ────────────────────────────────────────────────────────────────
        // BLOCO 4 — Avaliação de Claims (peso 10%)
        // ────────────────────────────────────────────────────────────────

        private double EvaluateClaims(
            EstimatedNutritionProfileDto? p,
            IReadOnlyList<string>? claims,
            List<string> warnings)
        {
            if (p == null || claims == null || claims.Count == 0) return 100.0;

            double score = 100.0;

            bool claimsZeroSugar  = claims.Any(c => c.Contains("zero açúcar", StringComparison.OrdinalIgnoreCase) || c.Contains("sem açúcar", StringComparison.OrdinalIgnoreCase));
            bool claimsLight      = claims.Any(c => c.Contains("light", StringComparison.OrdinalIgnoreCase));
            bool claimsIntegral   = claims.Any(c => c.Contains("integral", StringComparison.OrdinalIgnoreCase));
            bool claimsHighProtein = claims.Any(c => c.Contains("proteína", StringComparison.OrdinalIgnoreCase) || c.Contains("protein", StringComparison.OrdinalIgnoreCase));

            if (claimsZeroSugar && p.EstimatedSaturatedFatPer100g > 5)
            {
                score -= 10;
                warnings.Add("Claim 'zero açúcar' com gordura saturada elevada");
            }

            if (claimsZeroSugar && p.EstimatedSugarPer100g > 5)
            {
                score -= 10;
                warnings.Add("Claim 'zero açúcar' inconsistente com o teor de açúcar detectado");
            }

            if (claimsLight && p.EstimatedSodiumPer100g > 400)
            {
                score -= 5;
                warnings.Add("Claim 'light' com alto teor de sódio");
            }

            if (claimsIntegral && (p.EstimatedFiberPer100g ?? 0) < 2)
            {
                score -= 5;
                warnings.Add("Claim 'integral' sem fibra relevante");
            }

            if (claimsHighProtein && (p.EstimatedProteinPer100g ?? 0) < 5)
            {
                score -= 10;
                warnings.Add("Claim de proteína elevada inconsistente com os dados nutricionais");
            }

            return Math.Max(0, score);
        }

        // ────────────────────────────────────────────────────────────────
        // BLOCO 5 — Densidade Nutricional (peso 10%)
        // ────────────────────────────────────────────────────────────────

        private double CalculateDensity(EstimatedNutritionProfileDto? p, List<string> highlights)
        {
            if (p == null) return 50.0;

            double beneficialPoints = 0;
            if (p.EstimatedProteinPer100g >= 10) beneficialPoints += 40;
            else if (p.EstimatedProteinPer100g >= 5) beneficialPoints += 20;

            if (p.EstimatedFiberPer100g >= 5) beneficialPoints += 40;
            else if (p.EstimatedFiberPer100g >= 2) beneficialPoints += 20;

            beneficialPoints += 20; // base

            // Penalizar se alta caloria + baixo valor nutricional
            if (p.CaloriesPer100g > 250 && beneficialPoints < 40)
                beneficialPoints = Math.Max(0, beneficialPoints - 20);

            if (beneficialPoints >= 80) highlights.Add("Alta densidade nutricional");

            return Math.Min(100, beneficialPoints);
        }

        // ────────────────────────────────────────────────────────────────
        // PENALIZAÇÕES GLOBAIS
        // ────────────────────────────────────────────────────────────────

        private static double ApplyGlobalPenalties(
            double score,
            EstimatedNutritionProfileDto? p,
            List<string> warnings)
        {
            if (p == null) return score;

            if (p.EstimatedSugarPer100g > 15)
            {
                score -= 20;
                if (warnings != null && !warnings.Any(w => w.Contains("açúcar > 15g")))
                    warnings.Add("Açúcar > 15g por 100g — penalização global aplicada");
            }

            if (p.EstimatedSodiumPer100g > 600)
            {
                score -= 15;
                if (warnings != null && !warnings.Any(w => w.Contains("sódio > 600mg")))
                    warnings.Add("Sódio > 600mg por 100g — penalização global aplicada");
            }

            bool lowProteinFiber = (p.EstimatedProteinPer100g ?? 0) < 5 && (p.EstimatedFiberPer100g ?? 0) < 2;
            if (p.CaloriesPer100g > 250 && lowProteinFiber)
            {
                score -= 10;
                if (warnings != null && !warnings.Any(w => w.Contains("calorias altas")))
                    warnings.Add("Calorias altas com baixo valor proteico e de fibras");
            }

            return score;
        }

        // ────────────────────────────────────────────────────────────────
        // HELPERS
        // ────────────────────────────────────────────────────────────────

        private double BuildProfileOverall(
            EstimatedNutritionProfileDto? profile,
            IReadOnlyList<string>? ingredients,
            IReadOnlyList<string>? claims,
            string profileKey)
        {
            var dummyWarnings   = new List<string>();
            var dummyHighlights = new List<string>();

            double quality    = CalculateNutritionalQuality(profile, dummyHighlights, dummyWarnings);
            double processing = EvaluateProcessingLevel(ingredients, dummyWarnings);
            double profileAdj = CalculateProfileScore(profile, profileKey, dummyWarnings);
            double claims_    = EvaluateClaims(profile, claims, dummyWarnings);
            double density    = CalculateDensity(profile, dummyHighlights);

            return (quality    * WeightNutritionalQuality)
                 + (processing * WeightProcessingLevel)
                 + (profileAdj * WeightProfileAdequacy)
                 + (claims_    * WeightClaimsEvaluation)
                 + (density    * WeightNutritionalDensity);
        }

        private static double Clamp(double value) => Math.Max(0, Math.Min(100, value));
    }
}
