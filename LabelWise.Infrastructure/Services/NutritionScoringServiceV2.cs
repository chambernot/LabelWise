using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services;

/// <summary>
/// Sistema de Scoring Nutricional V2 - Baseado em Evidências Científicas
/// 
/// PRINCÍPIOS:
/// 1. Thresholds OMS/ANVISA (não arbitrários)
/// 2. Penalidade progressiva multi-fator
/// 3. Avaliação holística (todos os nutrientes contam)
/// 4. Scores realistas (sem otimismo exagerado)
/// 
/// REFERÊNCIAS:
/// - OMS: < 2000mg sódio/dia (recomendado < 1500mg)
/// - ANVISA: Rótulo frontal (excesso sódio > 600mg/100g)
/// - Nutri-Score: Sistema europeu validado
/// </summary>
public sealed class NutritionScoringServiceV2 : INutritionScoringService
{
    private readonly ILogger<NutritionScoringServiceV2> _logger;

    public NutritionScoringServiceV2(ILogger<NutritionScoringServiceV2> logger)
    {
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════
    // THRESHOLDS BASEADOS EM EVIDÊNCIAS
    // ═══════════════════════════════════════════════════════════════════

    // ── SÓDIO (mg/100g) ──
    // Fonte: OMS recomenda < 2000mg/dia. ANVISA rotulagem frontal: > 600mg = ALTO
    private const double Sodium_Excellent = 120;     // < 5% DV/100g
    private const double Sodium_Good = 400;          // < 20% DV
    private const double Sodium_Caution = 800;       // < 40% DV
    private const double Sodium_Bad = 1500;          // 75% DV (quase limite diário)
    private const double Sodium_Critical = 3000;     // 150% DV (**SEU CASO**)

    // ── AÇÚCAR (g/100g) ──
    // Fonte: OMS recomenda < 50g/dia (idealmente < 25g). ANVISA frontal: > 15g = ALTO
    private const double Sugar_Low = 5;              // Baixo (OK)
    private const double Sugar_Medium = 10;          // Moderado
    private const double Sugar_High = 15;            // Alto (ANVISA)
    private const double Sugar_VeryHigh = 30;        // Muito alto

    // ── GORDURA SATURADA (g/100g) ──
    // Fonte: OMS < 10% calorias = ~22g/dia. ANVISA frontal: > 6g = ALTO
    private const double SatFat_Low = 1.5;           // < 10% DV
    private const double SatFat_Medium = 3;          // 20% DV
    private const double SatFat_High = 6;            // 40% DV (ANVISA)
    private const double SatFat_VeryHigh = 10;       // 67% DV

    // ── PROTEÍNA (g/100g) ──
    // Fonte: ~50g/dia recomendado. Mínimo 15g/100g para "boa fonte"
    private const double Protein_VeryHigh = 20;      // Excelente
    private const double Protein_High = 15;          // Boa fonte
    private const double Protein_Medium = 10;        // Moderado
    private const double Protein_Low = 5;            // Baixo

    // ── FIBRA (g/100g) ──
    // Fonte: 25g/dia recomendado. ANVISA "fonte de fibra": ≥ 2.5g/porção
    private const double Fiber_VeryHigh = 6;         // Excelente (> 20% DV)
    private const double Fiber_High = 3;             // Boa fonte
    private const double Fiber_Medium = 1.5;         // Moderado

    // ── CALORIAS (kcal/100g) ──
    // Fonte: Densidade energética (kcal/100g) para classificação
    private const double Calories_Low = 150;         // Baixa densidade
    private const double Calories_Medium = 300;      // Média
    private const double Calories_High = 450;        // Alta

    // ═══════════════════════════════════════════════════════════════════
    // MÉTODO PRINCIPAL
    // ═══════════════════════════════════════════════════════════════════

    public UnifiedNutritionScore Calculate(NutritionEnrichedData enriched)
    {
        var profile = enriched.NormalizedProfile;

        _logger.LogInformation(
            "[ScoringV2] Iniciando cálculo — Calorias={Cal}, Prot={Prot}g, " +
            "Carbs={Carbs}g, Açúcar={Sugar}g, Polióis={Polyols}g, Gordura={Fat}g, GordSat={SatFat}g, " +
            "Sódio={Sodium}mg, Fibra={Fiber}g",
            profile.CaloriesPer100g, profile.EstimatedProteinPer100g,
            profile.EstimatedCarbsPer100g, profile.EstimatedSugarPer100g, profile.EstimatedPolyolsPer100g,
            profile.EstimatedFatPer100g, profile.EstimatedSaturatedFatPer100g,
            profile.EstimatedSodiumPer100g, profile.EstimatedFiberPer100g);

        // ── Extrair valores ─────────────────────────────────────────────
        var sodium = profile.EstimatedSodiumPer100g ?? 0;
        var carbs = profile.EstimatedCarbsPer100g ?? 0;
        var sugar = profile.EstimatedSugarPer100g ?? 0;
        var addedSugar = profile.EstimatedAddedSugarPer100g ?? 0;
        var polyols = profile.EstimatedPolyolsPer100g ?? 0;
        var satFat = profile.EstimatedSaturatedFatPer100g ?? 0;
        var fat = profile.EstimatedFatPer100g ?? 0;
        var protein = profile.EstimatedProteinPer100g ?? 0;
        var fiber = profile.EstimatedFiberPer100g ?? 0;
        var calories = profile.CaloriesPer100g ?? 0;
        var isLiquidBasis = string.Equals(profile.NutritionUnit, "ml", StringComparison.OrdinalIgnoreCase);
        var isBeverage = isLiquidBasis && !string.Equals(profile.ServingUnit, "g", StringComparison.OrdinalIgnoreCase);
        var usesLiquidSugarScale = isLiquidBasis;
        var unitLabel = isLiquidBasis ? "100ml" : "100g";

        // ═══════════════════════════════════════════════════════════════
        // SISTEMA DE PONTUAÇÃO MULTI-FATOR
        // ═══════════════════════════════════════════════════════════════

        int score = 100; // Base: produto perfeito

        // ── 1. PENALIDADE: SÓDIO (MAIS CRÍTICO PARA SEU CASO) ──────────
        score -= CalculateSodiumPenalty(sodium);

        // ── 2. PENALIDADE: AÇÚCAR ──────────────────────────────────────
        score -= CalculateSugarPenalty(sugar, addedSugar, usesLiquidSugarScale);

        // ── 3. PENALIDADE: IMPACTO GLICÊMICO ───────────────────────────
        score -= CalculateGlycemicImpactPenalty(carbs, sugar, addedSugar, polyols);

        // ── 4. PENALIDADE: GORDURA SATURADA ────────────────────────────
        score -= CalculateSatFatPenalty(satFat);

        // ── 5. PENALIDADE: CALORIAS E GORDURA TOTAL ────────────────────
        score -= CalculateCaloriePenalty(calories, fat);

        // ── 6. BÔNUS: PROTEÍNA ─────────────────────────────────────────
        score += CalculateProteinBonus(protein, sugar, addedSugar, satFat, calories, fat, enriched.ProcessingLevel);

        // ── 7. BÔNUS: FIBRA ────────────────────────────────────────────
        score += CalculateFiberBonus(fiber);

        // ── 8. AJUSTE POR PROCESSAMENTO ─────────────────────────────────
        score += CalculateProcessingAdjustment(enriched.ProcessingLevel);

        // ── 9. CAPS CARDIOMETABÓLICOS ──────────────────────────────────
        score = ApplyCardiometabolicCaps(
            score,
            sodium,
            sugar,
            addedSugar,
            polyols,
            satFat,
            calories,
            carbs,
            fat,
            protein,
            enriched.ProcessingLevel,
            isBeverage,
            usesLiquidSugarScale);

        // ── 10. LIMITAR RANGE ──────────────────────────────────────────
        score = Math.Clamp(score, 0, 100);

        _logger.LogInformation("[ScoringV2] ✅ Score final: {Score}/100", score);

        // ═══════════════════════════════════════════════════════════════
        // CLASSIFICAÇÃO E OUTPUT
        // ═══════════════════════════════════════════════════════════════

        var (label, color) = ClassifyScore(score);
        var offender = DetectPrincipalOffender(sodium, sugar, addedSugar, polyols, satFat, fat, calories, carbs, usesLiquidSugarScale);
        var highlights = BuildHighlights(
            profile.EstimatedSugarPer100g,
            profile.EstimatedSodiumPer100g,
            profile.EstimatedProteinPer100g,
            profile.EstimatedFiberPer100g,
            usesLiquidSugarScale);
        var warnings = BuildWarnings(
            profile.EstimatedSodiumPer100g,
            profile.EstimatedSugarPer100g,
            profile.EstimatedAddedSugarPer100g,
            profile.EstimatedPolyolsPer100g,
            profile.EstimatedSaturatedFatPer100g,
            profile.EstimatedProteinPer100g,
            isBeverage,
            usesLiquidSugarScale,
            unitLabel);

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

    // ═══════════════════════════════════════════════════════════════════
    // CÁLCULO DE PENALIDADES
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Penalidade de Sódio (mg/100g)
    /// 
    /// THRESHOLDS:
    /// - < 120mg   : 0 (excelente)
    /// - 120-400mg : -5 a -15 (bom→moderado)
    /// - 400-800mg : -15 a -30 (atenção)
    /// - 800-1500mg: -30 a -50 (evitar)
    /// - 1500-3000mg: -50 a -80 (crítico)
    /// - > 3000mg  : -90 (EVITAR TOTALMENTE)
    /// </summary>
    private int CalculateSodiumPenalty(double sodium)
    {
        int penalty = sodium switch
        {
            < Sodium_Excellent => 0,
            < Sodium_Good => 5 + (int)((sodium - Sodium_Excellent) / (Sodium_Good - Sodium_Excellent) * 10),       // 5→15
            < Sodium_Caution => 15 + (int)((sodium - Sodium_Good) / (Sodium_Caution - Sodium_Good) * 15),          // 15→30
            < Sodium_Bad => 30 + (int)((sodium - Sodium_Caution) / (Sodium_Bad - Sodium_Caution) * 20),            // 30→50
            < Sodium_Critical => 50 + (int)((sodium - Sodium_Bad) / (Sodium_Critical - Sodium_Bad) * 30),          // 50→80
            _ => 90  // > 3000mg = CRÍTICO (**SEU CASO**)
        };

        _logger.LogInformation("[ScoringV2] Sódio: {Sodium}mg → Penalidade: -{Penalty}", 
            sodium, penalty);

        return penalty;
    }

    /// <summary>
    /// Penalidade de Açúcar (g/100g)
    /// 
    /// THRESHOLDS:
    /// - < 5g   : 0 (baixo)
    /// - 5-10g  : -5 a -15 (moderado)
    /// - 10-15g : -15 a -25 (alto)
    /// - 15-30g : -25 a -40 (muito alto)
    /// - > 30g  : -50 (crítico)
    /// 
    /// BÔNUS: +10 de penalidade se for açúcar ADICIONADO
    /// </summary>
    private int CalculateSugarPenalty(double sugar, double addedSugar, bool isBeverage)
    {
        int penalty = isBeverage
            ? sugar switch
            {
                >= 9 => 28,
                >= 5 => 18 + (int)((sugar - 5) / 4 * 10),
                >= 2.5 => 8 + (int)((sugar - 2.5) / 2.5 * 10),
                > 0 => 3,
                _ => 0
            }
            : sugar switch
        {
            < Sugar_Low => 0,
            < Sugar_Medium => 5 + (int)((sugar - Sugar_Low) / 5 * 10),   // 5→15
            < Sugar_High => 15 + (int)((sugar - Sugar_Medium) / 5 * 10), // 15→25
            < Sugar_VeryHigh => 25 + (int)((sugar - Sugar_High) / 15 * 15), // 25→40
            _ => 50  // > 30g
        };

        // Açúcar adicionado é pior que açúcar naturalmente presente
        if (isBeverage && addedSugar >= 5)
        {
            penalty += 18;
            _logger.LogInformation("[ScoringV2] Açúcar adicionado em bebida: {Added}g → +18 penalidade",
                addedSugar);
        }
        else if (isBeverage && addedSugar >= 1)
        {
            penalty += 10;
            _logger.LogInformation("[ScoringV2] Açúcar adicionado em bebida: {Added}g → +10 penalidade",
                addedSugar);
        }
        else if (addedSugar >= 10)
        {
            penalty += 18;
            _logger.LogInformation("[ScoringV2] Açúcar adicionado: {Added}g → +18 penalidade",
                addedSugar);
        }
        else if (addedSugar >= 5)
        {
            penalty += 14;
            _logger.LogInformation("[ScoringV2] Açúcar adicionado: {Added}g → +14 penalidade", 
                addedSugar);
        }
        else if (addedSugar > 0)
        {
            penalty += 4;
            _logger.LogInformation("[ScoringV2] Açúcar adicionado: {Added}g → +4 penalidade",
                addedSugar);
        }

        _logger.LogInformation("[ScoringV2] Açúcar: {Sugar}g → Penalidade: -{Penalty}", 
            sugar, penalty);

        return penalty;
    }

    /// <summary>
    /// Penalidade de Gordura Saturada (g/100g)
    /// 
    /// THRESHOLDS:
    /// - < 1.5g  : 0 (baixo)
    /// - 1.5-3g  : -3 a -8 (moderado)
    /// - 3-6g    : -8 a -15 (alto)
    /// - 6-10g   : -15 a -25 (muito alto)
    /// - > 10g   : -30 (crítico)
    /// </summary>
    private int CalculateSatFatPenalty(double satFat)
    {
        int penalty = satFat switch
        {
            < SatFat_Low => 0,
            < SatFat_Medium => 3 + (int)((satFat - SatFat_Low) / 1.5 * 5),   // 3→8
            < SatFat_High => 8 + (int)((satFat - SatFat_Medium) / 3 * 7),    // 8→15
            < SatFat_VeryHigh => 15 + (int)((satFat - SatFat_High) / 4 * 10),// 15→25
            _ => 30  // > 10g
        };

        _logger.LogInformation("[ScoringV2] Gordura saturada: {SatFat}g → Penalidade: -{Penalty}", 
            satFat, penalty);

        return penalty;
    }

    /// <summary>
    /// Penalidade de Calorias + Gordura Total
    /// 
    /// THRESHOLDS:
    /// - < 150 kcal : 0
    /// - 150-300 kcal : -2 a -5
    /// - 300-450 kcal : -5 a -10
    /// - > 450 kcal : -10 a -20
    /// 
    /// GORDURA TOTAL:
    /// - < 10g : 0
    /// - 10-20g : -2 a -5
    /// - > 20g : -5 a -10
    /// </summary>
    private int CalculateCaloriePenalty(double calories, double fat)
    {
        int penalty = 0;

        // Calorias
        if (calories >= Calories_High)
            penalty += 10 + (int)Math.Min((calories - Calories_High) / 50, 10); // 10→20
        else if (calories >= Calories_Medium)
            penalty += 5 + (int)((calories - Calories_Medium) / 150 * 5); // 5→10
        else if (calories >= Calories_Low)
            penalty += (int)((calories - Calories_Low) / 150 * 5); // 0→5

        // Gordura Total
        if (fat >= 20)
            penalty += 5 + (int)Math.Min((fat - 20) / 10, 5); // 5→10
        else if (fat >= 10)
            penalty += (int)((fat - 10) / 10 * 5); // 0→5

        _logger.LogInformation("[ScoringV2] Calorias: {Cal}kcal, Gordura: {Fat}g → Penalidade: -{Penalty}", 
            calories, fat, penalty);

        return penalty;
    }

    private int CalculateGlycemicImpactPenalty(double carbs, double sugar, double addedSugar, double polyols)
    {
        var glycemicCarbs = Math.Max(0, carbs - polyols * 0.5);

        int penalty = glycemicCarbs switch
        {
            >= 60 => 12,
            >= 45 => 8,
            >= 30 => 4,
            _ => 0
        };

        if (polyols >= 20)
            penalty += 3;
        else if (polyols >= 10)
            penalty += 1;

        if (addedSugar <= 0 && sugar <= Sugar_Low && glycemicCarbs <= 10 && polyols > 0)
            penalty = Math.Max(0, penalty - 1);

        if (penalty > 0)
        {
            _logger.LogInformation(
                "[ScoringV2] Impacto glicêmico: Carbs={Carbs}g, Polióis={Polyols}g, CarbsImpacto≈{GlycemicCarbs}g → Penalidade: -{Penalty}",
                carbs, polyols, glycemicCarbs, penalty);
        }

        return penalty;
    }

    private int ApplyCardiometabolicCaps(
        int score,
        double sodium,
        double sugar,
        double addedSugar,
        double polyols,
        double satFat,
        double calories,
        double carbs,
        double fat,
        double protein,
        string? processingLevel,
        bool isBeverage,
        bool usesLiquidSugarScale)
    {
        var capped = score;

        // Para ser "Excelente", o produto precisa estar limpo nos principais
        // marcadores cardiometabólicos. Proteína/fibra não devem mascarar excesso
        // de gordura saturada, sódio ou açúcar.
        if (satFat >= SatFat_VeryHigh)
            capped = Math.Min(capped, 30);
        else if (satFat >= 8)
            capped = Math.Min(capped, 65);
        else if (satFat >= SatFat_High)
            capped = Math.Min(capped, 69);
        else if (satFat >= SatFat_Medium)
            capped = Math.Min(capped, 79);

        if (sodium >= Sodium_Bad)
            capped = Math.Min(capped, 39);
        else if (sodium >= Sodium_Caution)
            capped = Math.Min(capped, 65);
        else if (sodium >= Sodium_Good)
            capped = Math.Min(capped, 70);

        if (usesLiquidSugarScale && isBeverage && sugar >= 9)
            capped = Math.Min(capped, 59);
        else if (usesLiquidSugarScale && isBeverage && sugar >= 7.5)
            capped = Math.Min(capped, 59);
        else if (usesLiquidSugarScale && isBeverage && sugar >= 5)
            capped = Math.Min(capped, 69);
        else if (usesLiquidSugarScale && sugar >= 9)
            capped = Math.Min(capped, 69);
        else if (usesLiquidSugarScale && sugar >= 7.5)
            capped = Math.Min(capped, 74);
        else if (sugar >= Sugar_VeryHigh)
            capped = Math.Min(capped, 45);
        else if (sugar >= Sugar_High)
            capped = Math.Min(capped, 65);
        else if (sugar >= Sugar_Medium)
            capped = Math.Min(capped, 79);

        if (usesLiquidSugarScale && isBeverage && addedSugar >= 5)
            capped = Math.Min(capped, 45);
        else if (usesLiquidSugarScale && isBeverage && addedSugar >= 4)
            capped = Math.Min(capped, 59);
        else if (usesLiquidSugarScale && isBeverage && addedSugar >= 1)
            capped = Math.Min(capped, 74);
        else if (addedSugar >= 10)
            capped = Math.Min(capped, 65);
        else if (usesLiquidSugarScale && addedSugar >= 5)
            capped = Math.Min(capped, 69);
        else if (usesLiquidSugarScale && addedSugar >= 4)
            capped = Math.Min(capped, 79);
        else if (addedSugar >= 5)
            capped = Math.Min(capped, 74);

        if (polyols >= 20 && satFat >= 8)
            capped = Math.Min(capped, 62);
        else if (polyols >= 20)
            capped = Math.Min(capped, 74);

        // Produtos com densidade energética, carboidratos e gordura relevantes
        // não devem receber nota quase perfeita só porque também têm algum teor
        // de fibra/proteína. A regra é nutricional e genérica, sem depender da
        // categoria do produto.
        if (calories >= 300 && fat >= 10 && protein < 10)
            capped = Math.Min(capped, 79);
        else if (calories >= 200 && carbs >= 25 && fat >= 8 && protein < 8)
            capped = Math.Min(capped, 89);

        if (string.Equals(processingLevel, "ultraprocessado", StringComparison.OrdinalIgnoreCase))
            capped = Math.Min(capped, 82);

        if (capped != score)
        {
            _logger.LogInformation(
                "[ScoringV2] Cap cardiometabólico aplicado: {Original} → {Capped} (Sódio={Sodium}mg, Açúcar={Sugar}g, AçúcarAdicionado={AddedSugar}g, Polióis={Polyols}g, GordSat={SatFat}g)",
                score, capped, sodium, sugar, addedSugar, polyols, satFat);
        }

        return capped;
    }

    // ═══════════════════════════════════════════════════════════════════
    // CÁLCULO DE BÔNUS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Bônus de Proteína (g/100g)
    /// 
    /// THRESHOLDS:
    /// - < 5g   : 0
    /// - 5-10g  : +2 a +5
    /// - 10-15g : +5 a +8
    /// - 15-20g : +8 a +10
    /// - > 20g  : +10 (cap)
    /// </summary>
    private int CalculateProteinBonus(
        double protein,
        double sugar,
        double addedSugar,
        double satFat,
        double calories,
        double fat,
        string? processingLevel)
    {
        int bonus = protein switch
        {
            < Protein_Low => 0,
            < Protein_Medium => 2 + (int)((protein - Protein_Low) / 5 * 3),   // 2→5
            < Protein_High => 5 + (int)((protein - Protein_Medium) / 5 * 3),  // 5→8
            < Protein_VeryHigh => 8 + (int)((protein - Protein_High) / 5 * 2),// 8→10
            _ => 10  // cap
        };

        var originalBonus = bonus;

        if (bonus > 0)
        {
            var negativeSignals = 0;

            if (sugar >= Sugar_High || addedSugar >= 5)
                negativeSignals++;

            if (satFat >= SatFat_High)
                negativeSignals++;

            if (calories >= Calories_Medium && fat >= 10)
                negativeSignals++;

            if (string.Equals(processingLevel, "ultraprocessado", StringComparison.OrdinalIgnoreCase))
                negativeSignals++;

            bonus = negativeSignals switch
            {
                >= 3 => (int)Math.Floor(bonus * 0.35),
                2 => (int)Math.Floor(bonus * 0.5),
                1 => (int)Math.Floor(bonus * 0.75),
                _ => bonus
            };
        }

        if (bonus > 0)
        {
            _logger.LogInformation("[ScoringV2] Proteína: {Protein}g → Bônus: +{Bonus} (original={OriginalBonus})",
                protein, bonus, originalBonus);
        }

        return bonus;
    }

    private int CalculateProcessingAdjustment(string? processingLevel)
    {
        var adjustment = processingLevel?.Trim().ToLowerInvariant() switch
        {
            "in_natura" => 3,
            "minimamente_processado" => 2,
            "processado" => -5,
            "ultraprocessado" => -9,
            _ => 0
        };

        if (adjustment != 0)
            _logger.LogInformation("[ScoringV2] Ajuste por processamento: {ProcessingLevel} → {Adjustment:+#;-#;0}",
                processingLevel, adjustment);

        return adjustment;
    }

    /// <summary>
    /// Bônus de Fibra (g/100g)
    ///
    /// THRESHOLDS:
    /// - < 1.5g : 0
    /// - 1.5-3g : +2 a +5
    /// - 3-6g   : +5 a +8
    /// - > 6g   : +10 (cap)
    /// </summary>
    private int CalculateFiberBonus(double fiber)
    {
        int bonus = fiber switch
        {
            < Fiber_Medium => 0,
            < Fiber_High => 2 + (int)((fiber - Fiber_Medium) / 1.5 * 3),
            < Fiber_VeryHigh => 5 + (int)((fiber - Fiber_High) / 3 * 3),
            _ => 10
        };

        if (bonus > 0)
            _logger.LogInformation("[ScoringV2] Fibra: {Fiber}g → Bônus: +{Bonus}",
                fiber, bonus);

        return bonus;
    }

    // ═══════════════════════════════════════════════════════════════════
    // CLASSIFICAÇÃO E HELPERS
    // ═══════════════════════════════════════════════════════════════════

    private (string Label, string Color) ClassifyScore(int score)
    {
        return score switch
        {
            >= 90 => ("Excelente", "#28a745"),  // Verde escuro
            >= 70 => ("Bom", "#6fc142"),        // Verde claro
            >= 60 => ("Moderado", "#8bc34a"),   // Verde-amarelado
            >= 40 => ("Atenção", "#ffc107"),    // Amarelo
            >= 20 => ("Evitar", "#ff8c00"),     // Laranja
            _ => ("Muito ruim", "#dc3545")      // Vermelho
        };
    }

    private string DetectPrincipalOffender(
        double sodium,
        double sugar,
        double addedSugar,
        double polyols,
        double satFat,
        double fat,
        double calories,
        double carbs,
        bool usesLiquidSugarScale)
    {
        // Calcula "impacto negativo" de cada nutriente
        var sodiumImpact = CalculateSodiumPenalty(sodium);
        var sugarImpact = CalculateSugarPenalty(sugar, addedSugar, usesLiquidSugarScale);
        var glycemicImpact = CalculateGlycemicImpactPenalty(carbs, sugar, addedSugar, polyols);
        var satFatImpact = CalculateSatFatPenalty(satFat);
        var calorieImpact = CalculateCaloriePenalty(calories, fat);

        var impacts = new[]
        {
            (Name: "sódio", Impact: sodiumImpact),
            (Name: "açúcar", Impact: sugarImpact),
            (Name: "impacto glicêmico", Impact: glycemicImpact),
            (Name: "gordura saturada", Impact: satFatImpact),
            (Name: "calorias", Impact: calorieImpact)
        };

        var worst = impacts.OrderByDescending(x => x.Impact).First();
        return worst.Impact >= 5 ? worst.Name : "nenhum relevante";
    }

    private List<string> BuildHighlights(double? sugar, double? sodium, double? protein, double? fiber, bool usesLiquidSugarScale)
    {
        var highlights = new List<string>();

        var lowSugarLimit = usesLiquidSugarScale ? 2.5 : Sugar_Low;

        if (sugar.HasValue && sugar.Value < lowSugarLimit)
            highlights.Add("Baixo teor de açúcar");

        if (sodium.HasValue && sodium.Value < Sodium_Good)
            highlights.Add("Baixo teor de sódio");

        if (protein.HasValue && protein.Value >= Protein_High)
            highlights.Add("Boa fonte de proteína");

        if (fiber.HasValue && fiber.Value >= Fiber_High)
            highlights.Add("Boa fonte de fibra");

        return highlights.Count > 0 ? highlights : ["Sem destaques positivos"];
    }

    private List<string> BuildWarnings(
        double? sodium,
        double? sugar,
        double? addedSugar,
        double? polyols,
        double? satFat,
        double? protein,
        bool isBeverage,
        bool usesLiquidSugarScale,
        string unitLabel)
    {
        var warnings = new List<string>();

        if (sodium >= Sodium_Critical)
            warnings.Add($"⚠️ SÓDIO MUITO ALTO ({sodium.Value:F0}mg/{unitLabel} = {sodium.Value / 20:F0}% da recomendação diária). EVITAR.");
        else if (sodium >= Sodium_Bad)
            warnings.Add($"Sódio alto ({sodium.Value:F0}mg/{unitLabel}). Consumir com moderação.");
        else if (sodium >= Sodium_Caution)
            warnings.Add($"Sódio elevado ({sodium.Value:F0}mg/{unitLabel}). Atenção ao consumo frequente.");

        if (usesLiquidSugarScale && isBeverage && sugar >= 9)
            warnings.Add($"Açúcar muito alto para bebida ({sugar.Value:F1}g/{unitLabel}). Consumir esporadicamente.");
        else if (usesLiquidSugarScale && isBeverage && sugar >= 5)
            warnings.Add($"Açúcar alto para bebida ({sugar.Value:F1}g/{unitLabel}). Consumir com moderação.");
        else if (usesLiquidSugarScale && sugar >= 9)
            warnings.Add($"Açúcar alto ({sugar.Value:F1}g/{unitLabel}). Consumir com moderação.");
        else if (usesLiquidSugarScale && sugar >= 7.5)
            warnings.Add($"Açúcar moderado ({sugar.Value:F1}g/{unitLabel}). Atenção ao consumo frequente.");
        else if (sugar >= Sugar_VeryHigh)
            warnings.Add($"⚠️ AÇÚCAR MUITO ALTO ({sugar.Value:F1}g/{unitLabel}). EVITAR.");
        else if (sugar >= Sugar_High)
            warnings.Add($"Açúcar alto ({sugar.Value:F1}g/{unitLabel}). Consumir esporadicamente.");

        if (usesLiquidSugarScale && isBeverage && addedSugar >= 1)
            warnings.Add($"Contém açúcar adicionado em bebida ({addedSugar.Value:F1}g/{unitLabel}).");
        else if (usesLiquidSugarScale && addedSugar >= 4)
            warnings.Add($"Contém açúcar adicionado ({addedSugar.Value:F1}g/{unitLabel}).");

        if (polyols >= 20)
            warnings.Add($"Polióis elevados ({polyols.Value:F1}g/{unitLabel}). Podem somar calorias e causar desconforto intestinal; atenção à porção.");
        else if (polyols >= 10)
            warnings.Add($"Contém polióis ({polyols.Value:F1}g/{unitLabel}). Impacto glicêmico menor que açúcar, mas não é livre de efeito.");

        if (satFat >= SatFat_VeryHigh)
            warnings.Add($"⚠️ GORDURA SATURADA MUITO ALTA ({satFat.Value:F1}g/{unitLabel}). EVITAR.");
        else if (satFat >= SatFat_High)
            warnings.Add($"Gordura saturada alta ({satFat.Value:F1}g/{unitLabel}). Consumir com moderação.");

        if (protein.HasValue && protein.Value < Protein_Low)
            warnings.Add("Baixo teor de proteína. Complementar com outras fontes.");

        return warnings.Count > 0 ? warnings : [];
    }
}
