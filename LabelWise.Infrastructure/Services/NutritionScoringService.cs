using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Infrastructure.Services
{
    /// <summary>
    /// Motor de scoring nutricional estilo Yuka (0–100, base 100 com penalidades).
    ///
    /// PENALIDADES:
    ///   Açúcar  > 15g  → -25 | > 10g → -15 | > 5g  → -8
    ///   G.Sat   > 10g  → -25 | > 5g  → -20 | > 2g  → -12
    ///   Sódio   > 1000 → -25 | > 500 → -15 | > 300 → -12 | > 200 → -8
    ///
    /// BÔNUS (limitado a +10 e a no máximo metade da penalidade total):
    ///   Proteína > 10g → +5  | > 5g → +3
    ///   Fibra    > 5g  → +10 | > 2g → +5
    ///
    /// LABELS: ≥ 80 Excelente | ≥ 60 Bom | ≥ 40 Atenção | < 40 Ruim
    /// </summary>
    public class NutritionScoringService : INutritionScoringService
    {
        // ── Thresholds ────────────────────────────────────────────────────
        private const int BaseScore = 100;

        // Penalties
        private const int SugarCriticalPenalty   = 60;   // ≥ 30 g
        private const int SugarHighPenalty       = 40;   // ≥ 20 g
        private const int SugarMediumPenalty     = 20;   // ≥ 10 g
        private const int SugarLowPenalty        = 8;    // ≥  5 g
        private const double SugarCriticalThreshold = 30;
        private const double SugarHighThreshold     = 20;
        private const double SugarMediumThreshold   = 10;
        private const double SugarLowThreshold      =  5;

        private const int SatFatHighPenalty        = 25;
        private const int SatFatMediumPenalty      = 20;
        private const int SatFatLowPenalty         = 12;
        private const double SatFatHighThreshold    = 10;
        private const double SatFatMediumThreshold  = 5;
        private const double SatFatLowThreshold     = 2;

        private const int SodiumHighPenalty          = 25;
        private const int SodiumMediumHighPenalty    = 15;
        private const int SodiumMediumPenalty        = 12;
        private const int SodiumLowPenalty           = 8;
        private const double SodiumHighThreshold     = 1000;
        private const double SodiumMediumHighThreshold = 500;
        private const double SodiumMediumThreshold   = 300;
        private const double SodiumLowThreshold      = 200;

        // Bonuses
        private const int ProteinHighBonus       = 5;
        private const int ProteinMediumBonus     = 3;
        private const double ProteinHighThreshold = 10;
        private const double ProteinMediumThreshold = 5;

        private const int FiberHighBonus         = 5;
        private const int FiberMediumBonus       = 3;
        private const double FiberHighThreshold   = 5;
        private const double FiberMediumThreshold = 2;

        // Score labels
        private const int ExcellentThreshold = 80;
        private const int GoodThreshold      = 60;
        private const int CautionThreshold   = 40;
        private const int BadThreshold       = 25;

        // NOVO
        private const int CalorieHighPenalty = 20;
        private const int CalorieMediumPenalty = 10;
        private const int CalorieLowPenalty = 5;
        private const double CalorieHighThreshold = 400;
        private const double CalorieMediumThreshold = 300;
        private const double CalorieLowThreshold = 200;

        private const int FatHighPenalty = 15;
        private const int FatMediumPenalty = 10;
        private const double FatHighThreshold = 25;
        private const double FatMediumThreshold = 15;
        public UnifiedNutritionScore Calculate(NutritionEnrichedData enriched)
        {

            var profile = enriched.NormalizedProfile;
            var confidence = enriched.ConfidenceDetails ?? profile.NutritionConfidence;

            bool isLowConfidence = confidence?.GlobalScore < 0.5;

            var sugar = profile.EstimatedSugarPer100g;
            var addedSugar = profile.EstimatedAddedSugarPer100g;
            var satFat = profile.EstimatedSaturatedFatPer100g;
            var sodium = profile.EstimatedSodiumPer100g;
            var protein = profile.EstimatedProteinPer100g;
            var fiber = profile.EstimatedFiberPer100g;
            var fat = profile.EstimatedFatPer100g;
            var calories = profile.CaloriesPer100g;

            var isBeverage = string.Equals(profile.NutritionUnit, "ml", StringComparison.OrdinalIgnoreCase);

            if (isBeverage)
            {
                return CalculateBeverageScore(enriched);
            }

            // ─────────────────────────────────────────────
            // 1. PENALTY
            // ─────────────────────────────────────────────
            int totalPenalty =
                Apply(sugar, "sugar", true, v => SugarPenalty(v), confidence) +
                Apply(satFat, "satFat", true, v => SatFatPenalty(v), confidence) +
                Apply(sodium, "sodium", true, v => SodiumPenalty(v), confidence) +
                Apply(calories, "calories", true, v => CaloriePenalty(v), confidence) +
                Apply(fat, "fat", true, v => FatPenalty(v), confidence);

            if (addedSugar >= 4)
                totalPenalty += Apply(12, "addedSugar", true, confidence); // ↓ reduzido (antes 15)

            // baixo valor nutricional (ajuste leve)
            if (protein.HasValue && protein < 5)
                totalPenalty += Apply(6, "protein", false, confidence);

            if (fiber.HasValue && fiber < 2)
                totalPenalty += Apply(6, "fiber", false, confidence);

            // 🔴 LIMITADOR GLOBAL (evita score zerar fácil)
            totalPenalty = Math.Min(totalPenalty, 85);

            // ─────────────────────────────────────────────
            // 2. BONUS (MAIS FORTE)
            // ─────────────────────────────────────────────
            int totalBonus =
                Apply(protein, "protein", false, v => ProteinBonus(v), confidence) +
                    Apply(fiber, "fiber", false, v => FiberBonus(v), confidence);
            totalBonus = Math.Min(totalBonus, 30);

            if (totalPenalty > 0)
                totalBonus = Math.Min(totalBonus, totalPenalty / 2);

            // ─────────────────────────────────────────────
            // 3. BASE SCORE
            // ─────────────────────────────────────────────
            int score = Math.Clamp(BaseScore - totalPenalty + totalBonus, 0, 100);

            // ─────────────────────────────────────────────
            // 4. CAPS CRÍTICOS (AÇÚCAR)
            // ─────────────────────────────────────────────
            score = ApplySugarCaps(score, sugar, addedSugar, isBeverage, protein);

            // ─────────────────────────────────────────────
            // 5. ANTI-INFLAÇÃO (CENTRALIZADO)
            // ─────────────────────────────────────────────
            score = ApplyAntiInflation(score, fat, calories, satFat, protein);

            // ─────────────────────────────────────────────
            // 6. CAPS CARDIOMETABÓLICOS
            // ─────────────────────────────────────────────
            score = ApplyCardiometabolicCaps(score, sugar, addedSugar, sodium, satFat);

            // ─────────────────────────────────────────────
            // 7. CONTEXTO
            // ─────────────────────────────────────────────
            if (enriched.ProcessingLevel == "ultraprocessado")
                score -= 10;

            score = Math.Clamp(score, 0, 100);

            // ─────────────────────────────────────────────
            // 8. CORREÇÃO FINAL (ANTI-ZERO IRREAL)
            // ─────────────────────────────────────────────
            if (score == 0 && protein.HasValue && protein > 20)
                score = 25;

            // ─────────────────────────────────────────────
            // 9. COMPLETENESS (SEM MEXER NO SCORE)
            // ─────────────────────────────────────────────
            int completeness = NutritionCompletenessCalculator.Calculate(profile);

            bool hasCriticalData =
                HasReliableCriticalData(sugar, GetConfidence(confidence, "sugar")) ||
                HasReliableCriticalData(fat, GetConfidence(confidence, "fat")) ||
                HasReliableCriticalData(sodium, GetConfidence(confidence, "sodium"));

            // ─────────────────────────────────────────────
            // 10. CLASSIFICAÇÃO
            // ─────────────────────────────────────────────
            string label;
            string color;

            if (completeness < 50 && !hasCriticalData)
            {
                label = "Dados insuficientes";
                color = "gray";
            }
            else
            {
                (label, color) = ClassifyScore(score);

                if (isLowConfidence)
                    label = "Baixa confiabilidade";
            }

            // ─────────────────────────────────────────────
            // 11. OFFENDER + OUTPUT
            // ─────────────────────────────────────────────
            var offender = hasCriticalData
                ? DetectPrincipalOffender(sugar, satFat, sodium, fat, calories, confidence)
                : completeness < 60
                    ? "dados insuficientes"
                    : DetectPrincipalOffender(sugar, satFat, sodium, fat, calories, confidence);

            var highlights = (completeness < 60 && !hasCriticalData)
                ? ["Análise parcial dos dados disponíveis"]
                : BuildHighlights(sugar, sodium, protein, fiber, enriched.ProcessingLevel);

            var warnings = BuildWarnings(
                sugar, addedSugar, sodium, satFat,
                fat, calories,
                profile.EstimatedCarbsPer100g,
                confidence,
                hasCriticalData,
                enriched.ProcessingLevel == "ultraprocessado",
                enriched.FallbackUsed,
                enriched.Confidence,
                completeness,
                isLowConfidence);

            return new UnifiedNutritionScore
            {
                Value = score,
                Label = label,
                Color = color,
                PrincipalOffender = offender,
                Highlights = highlights,
                Warnings = warnings
            };
        }
        private int Apply(double? value, string field, bool critical,
    Func<double, int> calc,
    NutritionConfidenceResult? confidence)
        {
            if (!value.HasValue) return 0;

            var weight = GetWeight(GetConfidence(confidence, field), critical);
            return ScaleImpact(calc(value.Value), weight);
        }

        private int Apply(int baseValue, string field, bool critical,
            NutritionConfidenceResult? confidence)
        {
            var weight = GetWeight(GetConfidence(confidence, field), critical);
            return ScaleImpact(baseValue, weight);
        }

        private static int CaloriePenalty(double? calories)
        {
            if (!calories.HasValue) return 0;

            var c = calories.Value;

            int continuous = (int)Math.Min(c / 20, 25);

            int threshold = c switch
            {
                > 400 => 10,
                > 300 => 5,
                _ => 0
            };

            return continuous + threshold;
        }

        private UnifiedNutritionScore CalculateBeverageScore(NutritionEnrichedData enriched)
        {
            var profile = enriched.NormalizedProfile;

            var sugar = profile.EstimatedSugarPer100g ?? 0;
            var addedSugar = profile.EstimatedAddedSugarPer100g ?? 0;
            var protein = profile.EstimatedProteinPer100g ?? 0;
            var fiber = profile.EstimatedFiberPer100g ?? 0;
            var calories = profile.CaloriesPer100g ?? 0;

            int score = 100;
            int penalty = 0;

            // 🔴 1. Açúcar (muito mais agressivo em líquidos)
            penalty += (int)(sugar * 3.5); // bebida absorve rápido

            if (sugar >= 10) penalty += 25;
            else if (sugar >= 7) penalty += 15;
            else if (sugar >= 5) penalty += 10;

            // 🔴 2. Açúcar adicionado
            if (addedSugar >= 5) penalty += 15;
            else if (addedSugar >= 2) penalty += 8;

            // 🔴 3. Calorias vazias (CRÍTICO)
            if (protein == 0 && fiber == 0 && sugar > 5)
                penalty += 20;

            // 🔴 4. Baixo valor nutricional
            if (protein < 2) penalty += 5;
            if (fiber < 1) penalty += 5;

            // 🔴 5. Calorias (menos relevante que sólido)
            if (calories >= 80) penalty += 10;
            else if (calories >= 50) penalty += 5;

            // 🔴 CAP GLOBAL
            penalty = Math.Min(penalty, 95);

            score = Math.Clamp(100 - penalty, 0, 100);

            // 🔥 CAP FINAL (controle de segurança)
            if (sugar >= 10) score = Math.Min(score, 30);
            else if (sugar >= 7) score = Math.Min(score, 45);
            else if (sugar >= 5) score = Math.Min(score, 55);

            // ─────────────────────────────
            // CLASSIFICAÇÃO
            // ─────────────────────────────
            var (label, color) = ClassifyScore(score);

            return new UnifiedNutritionScore
            {
                Value = score,
                Label = label,
                Color = color,
                PrincipalOffender = "açúcar",
                Highlights = BuildHighlights(sugar, profile.EstimatedSodiumPer100g, protein, fiber, enriched.ProcessingLevel),
                Warnings = BuildWarnings(
                    sugar, addedSugar,
                    profile.EstimatedSodiumPer100g,
                    profile.EstimatedSaturatedFatPer100g,
                    profile.EstimatedFatPer100g,
                    calories,
                    profile.EstimatedCarbsPer100g,
                    enriched.ConfidenceDetails,
                    true,
                    false,
                    enriched.FallbackUsed,
                    enriched.Confidence,
                    100,
                    false)
            };
        }

        private static int ApplySugarCaps(int score, double? sugar, double? addedSugar, bool isBeverage, double? protein = null)
        {
            if (!sugar.HasValue) return score;

            double proteinFactor = protein ?? 0;

            if (isBeverage)
            {
                if (sugar >= 9) return Math.Min(score, 35);
                if (sugar >= 5) return Math.Min(score, 60);
                if (sugar >= 2.5) return Math.Min(score, 80);
            }
            else
            {
                if (sugar >= 30)
                {
                    // 🔥 cap inteligente (considera proteína)
                    int dynamicCap = (int)(25 + proteinFactor * 0.3); // ex: 27g → ~33
                    return Math.Min(score, dynamicCap);
                }

                if (sugar >= 20)
                {
                    int dynamicCap = (int)(40 + proteinFactor * 0.2);
                    return Math.Min(score, dynamicCap);
                }

                if (sugar > 8)
                {
                    int dynamicCap = (int)(65 + proteinFactor * 0.1);
                    return Math.Min(score, dynamicCap);
                }
            }

            if (isBeverage && addedSugar >= 1)
                score = Math.Min(score, 65);

            return score;
        }

        private static int ApplyCardiometabolicCaps(
            int score,
            double? sugar,
            double? addedSugar,
            double? sodium,
            double? satFat)
        {
            var capped = score;
            var sugarVal = sugar ?? 0;
            var addedSugarVal = addedSugar ?? 0;
            var sodiumVal = sodium ?? 0;
            var satFatVal = satFat ?? 0;

            if (satFatVal >= 10) capped = Math.Min(capped, 55);
            else if (satFatVal >= 8) capped = Math.Min(capped, 65);
            else if (satFatVal >= 6) capped = Math.Min(capped, 69);
            else if (satFatVal >= 3) capped = Math.Min(capped, 79);

            if (sodiumVal >= 1500) capped = Math.Min(capped, 50);
            else if (sodiumVal >= 800) capped = Math.Min(capped, 65);
            else if (sodiumVal >= 400) capped = Math.Min(capped, 79);

            if (sugarVal >= 30) capped = Math.Min(capped, 45);
            else if (sugarVal >= 15) capped = Math.Min(capped, 65);
            else if (sugarVal >= 10) capped = Math.Min(capped, 79);

            if (addedSugarVal >= 5) capped = Math.Min(capped, 79);

            return capped;
        }

        private static int ApplyAntiInflation(int score, double? fat, double? calories, double? satFat, double? protein)
        {
            double proteinFactor = protein ?? 0;

            var fatVal = fat ?? 0;
            var caloriesVal = calories ?? 0;
            var satFatVal = satFat ?? 0;

            // 🔥 CALORIAS
            if (caloriesVal >= 400)
            {
                int cap = (int)(35 + proteinFactor * 0.3);
                score = Math.Min(score, cap);
            }
            else if (caloriesVal >= 300)
            {
                int cap = (int)(55 + proteinFactor * 0.2);
                score = Math.Min(score, cap);
            }

            // 🔥 GORDURA TOTAL
            if (fatVal >= 25)
            {
                int cap = (int)(40 + proteinFactor * 0.3);
                score = Math.Min(score, cap);
            }
            else if (fatVal >= 15)
            {
                int cap = (int)(60 + proteinFactor * 0.2);
                score = Math.Min(score, cap);
            }

            // 🔥 GORDURA SATURADA
            if (satFatVal >= 10)
            {
                int cap = (int)(30 + proteinFactor * 0.3);
                score = Math.Min(score, cap);
            }
            else if (satFatVal >= 5)
            {
                int cap = (int)(50 + proteinFactor * 0.2);
                score = Math.Min(score, cap);
            }

            // ajuste leve
            if (fatVal > 15 && caloriesVal > 300)
                score -= 3;

            return score;
        }

        private static int FatPenalty(double? fat)
        {
            if (!fat.HasValue) return 0;

            var f = fat.Value;

            int continuous = (int)Math.Min(f * 0.8, 25);

            int threshold = f switch
            {
                > 25 => 10,
                > 15 => 5,
                _ => 0
            };

            return continuous + threshold;
        }

        // ── Penalty helpers ───────────────────────────────────────────────


        private static int SugarPenalty(double? sugar)
        {
            if (!sugar.HasValue) return 0;

            var s = sugar.Value;

            // componente contínuo (peso real)
            int continuous = (int)Math.Min(s * 1.8, 50);

            // componente crítico (threshold)
            int threshold = s switch
            {
                > 30 => 20,
                > 20 => 15,
                > 10 => 10,
                > 5 => 5,
                _ => 0
            };

            return continuous + threshold;
        }

        private static int SatFatPenalty(double? satFat)
        {
            if (!satFat.HasValue) return 0;

            var f = satFat.Value;

            int continuous = (int)Math.Min(f * 2.0, 40);

            int threshold = f switch
            {
                > 10 => 15,
                > 5 => 10,
                > 2 => 5,
                _ => 0
            };

            return continuous + threshold;
        }

        private static int SodiumPenalty(double? sodium)
        {
            if (!sodium.HasValue) return 0;

            var s = sodium.Value;

            int continuous = (int)Math.Min(s / 40, 40); // escala mg

            int threshold = s switch
            {
                > 1000 => 15,
                > 500 => 10,
                > 300 => 6,
                > 200 => 3,
                _ => 0
            };

            return continuous + threshold;
        }

        // ── Bonus helpers ─────────────────────────────────────────────────

        private static int ProteinBonus(double? protein)
        {
            if (!protein.HasValue) return 0;

            // mais impacto real
            return (int)Math.Min(protein.Value * 1.0, 20);
        }

        private static int FiberBonus(double? fiber)
        {
            if (!fiber.HasValue) return 0;

            return (int)Math.Min(fiber.Value * 2.5, 12);
        }
        // ── Classification ────────────────────────────────────────────────

        private static (string label, string color) ClassifyScore(int score) => score switch
        {
            >= ExcellentThreshold => ("Excelente",  "green"),
            >= GoodThreshold      => ("Bom",        "light_green"),
            >= CautionThreshold   => ("Atenção",    "yellow"),
            >= BadThreshold       => ("Ruim",       "orange"),
            _                     => ("Muito ruim", "red")
        };

        private static string DetectPrincipalOffender(
            double? sugar, double? satFat, double? sodium,
            double? fat, double? calories,
            NutritionConfidenceResult? confidence)
        {
            var penalties = new Dictionary<string, int>
            {
                ["açúcar"]           = ScaleImpact(SugarPenalty(sugar), GetWeight(GetConfidence(confidence, "sugar"), criticalDecision: true)),
                ["gordura saturada"] = ScaleImpact(SatFatPenalty(satFat), GetWeight(GetConfidence(confidence, "satFat"), criticalDecision: true)),
                ["sódio"]            = ScaleImpact(SodiumPenalty(sodium), GetWeight(GetConfidence(confidence, "sodium"), criticalDecision: true)),
                ["gordura"]          = ScaleImpact(FatPenalty(fat), GetWeight(GetConfidence(confidence, "fat"), criticalDecision: true)),
                ["calorias"]         = ScaleImpact(CaloriePenalty(calories), GetWeight(GetConfidence(confidence, "calories"), criticalDecision: true)),
            };

            int maxPenalty = penalties.Values.Max();

            if (maxPenalty == 0)
                return "nenhum relevante";

            // pega todos que têm a maior penalidade
            var offenders = penalties
                .Where(p => p.Value == maxPenalty)
                .Select(p => p.Key)
                .ToList();

            // regra de desempate inteligente
            return ResolveTie(offenders);
        }

        private static string ResolveTie(List<string> offenders)
        {
            // Prioridade baseada em impacto de saúde
            if (offenders.Contains("gordura saturada")) return "gordura saturada";
            if (offenders.Contains("gordura"))          return "gordura";
            if (offenders.Contains("sódio"))            return "sódio";
            if (offenders.Contains("calorias"))         return "calorias";
            return "açúcar";
        }

        private static List<string> BuildHighlights(
            double? sugar, double? sodium, double? protein, double? fiber, string processingLevel)
        {
            var list = new List<string>();

            if (sodium.HasValue)
            {
                if (sodium < 200)
                    list.Add("Baixo teor de sódio");
                else if (sodium <= 300)
                    list.Add("Sódio moderado");
            }
            if (protein > ProteinHighThreshold)     list.Add("Excelente fonte de proteína");
            else if (protein > ProteinMediumThreshold) list.Add("Boa fonte de proteína");
            if (fiber > FiberHighThreshold)         list.Add("Rico em fibras");
            else if (fiber > FiberMediumThreshold)  list.Add("Bom teor de fibras");
            if (sugar.HasValue && sugar <= 3)       list.Add("Baixo teor de açúcar");
            if (processingLevel?.Replace("_", " ")?.ToLower() == "in natura") list.Add("Alimento in natura");

            return list;
        }

        private static List<string> BuildWarnings(
            double? sugar, double? addedSugar, double? sodium, double? satFat,
            double? fat, double? calories, double? carbs,
            NutritionConfidenceResult? confidenceDetails,
            bool hasCriticalData,
            bool isUltra, bool fallbackUsed, string overallConfidence,
            int completeness, bool isLowConfidenceMode)
        {
            var list = new List<string>();

            var sugarConfidence = GetConfidence(confidenceDetails, "sugar");
            var sodiumConfidence = GetConfidence(confidenceDetails, "sodium");

            if (calories > 300)
                list.Add("Alta densidade calórica");
            if (fat > 15)
                list.Add("Alto teor de gordura");

            // ── Aviso de açúcar consolidado (sem duplicidade) ─────────────
            // "extremamente elevado" só quando o valor absoluto ultrapassa o
            // limiar crítico (≥ 30g/100g). A coincidência açúcar ≈ carbs é
            // normal em bebidas e não indica, por si só, excesso de açúcar.
            bool extremelySugary = sugar >= SugarCriticalThreshold;

            if (sugarConfidence == FieldConfidence.Low)
                list.Add("Valor de açúcar com baixa confiabilidade — interprete com cautela");
            else if (sugarConfidence == FieldConfidence.None)
                list.Add("Teor de açúcar não identificado na tabela nutricional");
            else if (sugarConfidence == FieldConfidence.High)
            {
                if (extremelySugary)
                    list.Add("Produto com teor extremamente elevado de açúcar");
                else if (sugar >= SugarHighThreshold)
                    list.Add("Alto teor de açúcar");
                else if (sugar >= SugarMediumThreshold)
                    list.Add("Açúcar elevado: atenção ao consumo frequente");
            }

            if (addedSugar >= SugarLowThreshold && !extremelySugary && sugarConfidence == FieldConfidence.High)
                list.Add("Contém açúcares adicionados");

            if (sodiumConfidence == FieldConfidence.Low)
                list.Add("Teor de sódio não pôde ser determinado com precisão");
            else if (sodiumConfidence == FieldConfidence.High)
            {
                if (sodium > 500)
                    list.Add("Alto teor de sódio");
                else if (sodium > 200)
                    list.Add("Sódio moderado: atenção ao consumo frequente");
            }
            if (satFat > 5)
                list.Add("Gordura saturada elevada");
            else if (satFat > 2)
                list.Add("Gordura saturada moderada");
            if (isUltra)       list.Add("Produto ultraprocessado");
            if (fallbackUsed)  list.Add("Avaliação estimada por categoria (sem tabela nutricional visível)");
            if (overallConfidence == "baixa") list.Add("Dados nutricionais com baixa confiabilidade");
            if (completeness < 50 && !hasCriticalData)
                list.Add("Dados nutricionais parciais — análise limitada");

            if (isLowConfidenceMode)
            {
                list.Add("Algumas informações podem não estar totalmente precisas");
            }

            return list;
        }

        private static FieldConfidence GetConfidence(NutritionConfidenceResult? confidence, string fieldName)
            => confidence?.GetFieldConfidence(fieldName) ?? FieldConfidence.None;

        // ── Pesos por confiança ───────────────────────────────────────────
        // Princípio da precaução (alinhado a Yuka / Open Food Facts): quando
        // o nutriente é uma DECISÃO CRÍTICA (açúcar, gordura saturada, sódio,
        // açúcar adicionado), uma leitura LOW NÃO deve zerar a penalidade —
        // mantemos metade dela para evitar que produtos claramente ruins sejam
        // classificados como "Excelente" só porque o OCR teve confidence baixa.
        // Para impactos não críticos (bônus de proteína/fibra), Low continua 0
        // — seguimos exigindo evidência sólida para premiar.
        private static double GetWeight(FieldConfidence confidence, bool criticalDecision = false)
        {
            return confidence switch
            {
                FieldConfidence.High   => 1.0,
                FieldConfidence.Medium => 0.7,
                FieldConfidence.Low    => criticalDecision ? 0.5 : 0.0,
                _                       => 0.0
            };
        }

        private static int ScaleImpact(int baseValue, double weight)
            => (int)Math.Round(baseValue * weight, MidpointRounding.AwayFromZero);

        private static bool HasReliableCriticalData(double? value, FieldConfidence confidence)
            => value.HasValue && GetWeight(confidence, criticalDecision: true) > 0;

            }
        }