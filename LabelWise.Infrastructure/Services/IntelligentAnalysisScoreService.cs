using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services;

/// <summary>
/// Implementação do orquestrador de score para o endpoint
/// <c>nutricao-analise-inteligente</c>.
///
/// Reaproveita o motor único <see cref="INutritionScoringService"/>
/// (mesma fonte de verdade usada pelo <c>NutritionAnalysisOrchestrator</c>),
/// evitando duplicação de regra de negócio.
/// </summary>
public sealed class IntelligentAnalysisScoreService : IIntelligentAnalysisScoreService
{
    private readonly INutritionScoringService _scoringService;
    private readonly ILogger<IntelligentAnalysisScoreService> _logger;

    public IntelligentAnalysisScoreService(
        INutritionScoringService scoringService,
        ILogger<IntelligentAnalysisScoreService> logger)
    {
        _scoringService = scoringService ?? throw new ArgumentNullException(nameof(scoringService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Apply(IntelligentAnalysisResponse response, NutritionConfidenceResult? confidence)
    {
        if (response is null) throw new ArgumentNullException(nameof(response));

        var per100 = response.Nutrition.Per100;
        if (per100 is null)
        {
            _logger.LogDebug("[IntelligentScore] Per100 ausente — score não será calculado.");
            return;
        }

        if (RequiresCompleteVisionScoreInputs(response) && !HasMinimumScoreInputs(per100))
        {
            response.Score = null;
            response.Diagnostics.Warnings.Add(
                "Score nutricional não calculado: a imagem não permitiu ler calorias, carboidratos, gorduras, gordura saturada e sódio com segurança.");
            _logger.LogWarning(
                "[IntelligentScore] Score não calculado por campos críticos ausentes. " +
                "Calorias={Cal}, Carbs={Carbs}, Açúcar={Sugar}, Gordura={Fat}, GordSat={SatFat}, Sódio={Sodium}",
                per100.CaloriesKcal, per100.Carbohydrates, per100.Sugars,
                per100.TotalFats, per100.SaturatedFats, per100.SodiumMg);
            return;
        }

        var profile = BuildProfileFromResponse(response, per100, confidence);
        var enriched = BuildEnrichedData(profile, confidence, response.ProcessingLevel);

        try
        {
            var unified = _scoringService.Calculate(enriched);

            response.ProcessingLevel = enriched.ProcessingLevel;

            // Sinaliza se o produto é uma bebida (regra genérica: unit="ml"),
            // permitindo que os perfis apliquem escala de líquidos do Nutri-Score 2023.
            var isLiquidBasis = string.Equals(response.Nutrition.Unit, "ml", StringComparison.OrdinalIgnoreCase);
            var isBeverage = isLiquidBasis && !string.Equals(response.Nutrition.Serving?.Unit, "g", StringComparison.OrdinalIgnoreCase);
            var usesLiquidSugarScale = isLiquidBasis;
            var unitLabel = isLiquidBasis ? "100ml" : "100g";

            response.Score = new ScoreSection
            {
                Global          = unified.Value,
                GlobalLabel     = unified.Label,
                PrincipalOffender = unified.PrincipalOffender,
                ResumoRapido    = BuildResumoRapido(unified, per100, isBeverage, unitLabel),
                ExplicacaoScore = BuildExplicacaoScore(unified, per100, unitLabel),
                PontoPrincipal  = BuildPontoPrincipal(unified, per100),
                Profiles        = BuildProfiles(per100, isBeverage, usesLiquidSugarScale, unitLabel),
                Processing      = BuildProcessingScore(enriched.ProcessingLevel),
                Strengths       = unified.Highlights ?? [],
                Weaknesses      = unified.Warnings   ?? []
            };
            NutritionQualityEvaluator.ApplyScoreReliability(response.Score, response.AnalysisQuality, response.NutritionReliabilityScore);
            response.ProcessingClassification = BuildProcessingClassification(enriched.ProcessingLevel, unified.Warnings);
            response.QuickFlags = BuildQuickFlags(response.Score, response.ProcessingClassification);

            _logger.LogInformation(
                "[IntelligentScore] Score calculado — Value={Value}, Label={Label}, Offender={Offender}, Rules={Rules}",
                unified.Value, unified.Label, unified.PrincipalOffender, string.Join(" | ", response.ProcessingClassification.Reasons));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[IntelligentScore] Falha ao calcular score nutricional. Resposta seguirá sem score.");
        }
    }

    /// <summary>
    /// Monta um <see cref="EstimatedNutritionProfileDto"/> mínimo a partir
    /// do que já está consolidado no contrato de saída. Bebidas (unit="ml")
    /// alimentam os campos per100g via aproximação de densidade ≈ 1
    /// (regra genérica já documentada no analyzer).
    /// </summary>
    private static EstimatedNutritionProfileDto BuildProfileFromResponse(
        IntelligentAnalysisResponse response,
        NutritionValues per100,
        NutritionConfidenceResult? confidence)
    {
        var isMl = string.Equals(response.Nutrition.Unit, "ml", StringComparison.OrdinalIgnoreCase);

        return new EstimatedNutritionProfileDto
        {
            CaloriesPer100g = per100.CaloriesKcal,
            EstimatedCarbsPer100g = per100.Carbohydrates,
            EstimatedSugarPer100g = per100.Sugars,
            EstimatedAddedSugarPer100g = per100.AddedSugars,
            EstimatedPolyolsPer100g = per100.Polyols,
            EstimatedProteinPer100g = per100.Proteins,
            EstimatedFatPer100g = per100.TotalFats,
            EstimatedSaturatedFatPer100g = per100.SaturatedFats,
            EstimatedTransFatPer100g = per100.TransFats,
            EstimatedFiberPer100g = per100.Fiber,
            EstimatedSodiumPer100g = per100.SodiumMg,
            CaloriesPer100ml = isMl ? per100.CaloriesKcal : null,
            NutritionUnit = response.Nutrition.Unit,
            ServingAmount = response.Nutrition.Serving?.Amount,
            ServingUnit = response.Nutrition.Serving?.Unit,
            ServingDescription = response.Nutrition.Serving?.Description,
            NutritionConfidence = confidence
        };
    }

    private static NutritionEnrichedData BuildEnrichedData(
        EstimatedNutritionProfileDto profile,
        NutritionConfidenceResult? confidence,
        string? processingLevel) => new()
        {
            NormalizedProfile = profile,
            ConfidenceDetails = confidence,
            ProcessingLevel = NormalizeProcessingLevel(processingLevel),
            Confidence = confidence?.GlobalScore switch
            {
                >= 0.8 => "alta",
                >= 0.5 => "media",
                _      => "baixa"
            }
        };

    private static bool RequiresCompleteVisionScoreInputs(IntelligentAnalysisResponse response) =>
        string.Equals(response.Source, "openai-vision", StringComparison.OrdinalIgnoreCase);

    private static bool HasMinimumScoreInputs(NutritionValues per100) =>
        per100.CaloriesKcal.HasValue &&
        per100.Carbohydrates.HasValue &&
        per100.TotalFats.HasValue &&
        (per100.TotalFats.Value == 0 || per100.SaturatedFats.HasValue) &&
        per100.SodiumMg.HasValue;

    private static string NormalizeProcessingLevel(string? processingLevel) =>
        string.IsNullOrWhiteSpace(processingLevel)
            ? "desconhecido"
            : processingLevel.Trim().ToLowerInvariant();

    private static ProcessingScore BuildProcessingScore(string? processingLevel)
    {
        var level = NormalizeProcessingLevel(processingLevel);
        return level switch
        {
            "in_natura" => new ProcessingScore
            {
                Score = 100,
                Level = level,
                Label = "Natural",
                Reason = "Alimento com baixo nível de processamento."
            },
            "minimamente_processado" => new ProcessingScore
            {
                Score = 90,
                Level = level,
                Label = "Minimamente processado",
                Reason = "Alimento com processamento limitado."
            },
            "processado" => new ProcessingScore
            {
                Score = 70,
                Level = level,
                Label = "Processado",
                Reason = "Produto processado; vale observar ingredientes e frequência de consumo."
            },
            "ultraprocessado" => new ProcessingScore
            {
                Score = 45,
                Level = level,
                Label = "Ultraprocessado",
                Reason = "Produto com alto nível de processamento; proteína ou fibra não anulam esse alerta."
            },
            _ => new ProcessingScore
            {
                Score = 60,
                Level = "desconhecido",
                Label = "Não identificado",
                Reason = "Não foi possível identificar com segurança o nível de processamento."
            }
        };
    }

    private static NutritionProcessingClassificationDto BuildProcessingClassification(string? processingLevel, IReadOnlyList<string> warnings)
    {
        var normalized = NormalizeProcessingLevel(processingLevel);
        var level = normalized switch
        {
            "in_natura" or "minimamente_processado" => "natural",
            "processado" => "processed",
            "ultraprocessado" => "ultra_processed",
            _ => "unknown"
        };

        var reasons = new List<string>();
        if (level == "ultra_processed")
            reasons.Add("Produto com alto nível de processamento; proteína ou fibra não anulam esse alerta.");
        else if (level == "processed")
            reasons.Add("Produto processado; vale observar ingredientes e frequência de consumo.");
        else if (level == "natural")
            reasons.Add("Alimento com baixo nível de processamento.");

        reasons.AddRange(warnings.Where(warning => warning.Contains("açúcar", StringComparison.OrdinalIgnoreCase) || warning.Contains("sódio", StringComparison.OrdinalIgnoreCase)).Take(2));

        return new NutritionProcessingClassificationDto
        {
            Level = level,
            Confidence = level == "unknown" ? "low" : "medium",
            Reasons = reasons.DefaultIfEmpty("Não foi possível identificar com segurança o nível de processamento.").Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private static List<NutritionQuickFlagDto> BuildQuickFlags(ScoreSection score, NutritionProcessingClassificationDto processingClassification)
    {
        var flags = new List<NutritionQuickFlagDto>();

        foreach (var strength in score.Strengths.Take(2))
            flags.Add(new NutritionQuickFlagDto { Type = "positive", Label = HumanizeNutritionText(strength) });

        if (processingClassification.Level == "ultra_processed")
            flags.Add(new NutritionQuickFlagDto { Type = "warning", Label = "Ultraprocessado" });

        foreach (var warning in score.Weaknesses.Take(3))
            flags.Add(new NutritionQuickFlagDto { Type = warning.Contains("EVITAR", StringComparison.OrdinalIgnoreCase) ? "danger" : "warning", Label = HumanizeNutritionText(warning) });

        return flags
            .Where(flag => !string.IsNullOrWhiteSpace(flag.Label))
            .GroupBy(flag => flag.Label, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(6)
            .ToList();
    }

    private static string HumanizeNutritionText(string value)
    {
        var text = value.Replace("⚠️", string.Empty).Trim();
        if (text.Contains("açúcar", StringComparison.OrdinalIgnoreCase)) return "Contém açúcar adicionado.";
        if (text.Contains("sódio", StringComparison.OrdinalIgnoreCase)) return "Atenção ao sódio.";
        if (text.Contains("gordura saturada", StringComparison.OrdinalIgnoreCase)) return "Atenção à gordura saturada.";
        return text.TrimEnd('.') + ".";
    }

    // ── Textos de análise em linguagem simples ────────────────────────────

    private static string BuildResumoRapido(UnifiedNutritionScore unified, NutritionValues per100, bool isBeverage, string unitLabel)
    {
        var nome = ProductDescription(per100, unitLabel);
        if (isBeverage && ((per100.Sugars ?? 0) >= 7.5 || (per100.AddedSugars ?? 0) >= 5))
            return $"Bebida açucarada. O consumo frequente deve ser evitado, especialmente por conter açúcar adicionado. {nome}";

        return unified.Value switch
        {
            >= 80 => $"Produto com boa composição nutricional. {nome}",
            >= 60 => $"Produto razoável, com alguns pontos de atenção. {nome}",
            >= 40 => $"Produto com nutrientes que merecem cautela. {nome}",
            >= 25 => $"Produto com composição nutricional ruim. {nome}",
            _     => $"Produto com composição nutricional muito ruim. {nome}"
        };
    }

    private static string BuildExplicacaoScore(UnifiedNutritionScore unified, NutritionValues per100, string unitLabel)
    {
        var offender = unified.PrincipalOffender;
        var score    = unified.Value;

        if (string.IsNullOrEmpty(offender) || offender == "nenhum relevante")
        {
            return score >= 70
                ? "Não foram encontrados nutrientes críticos em excesso. O produto apresenta um perfil equilibrado."
                : "Os dados nutricionais disponíveis são insuficientes para uma avaliação detalhada.";
        }

        var impacto = offender switch
        {
            "açúcar"           => $"Alto teor de açúcar ({per100.Sugars:F1} g/{unitLabel}), o que pode causar picos de glicemia e contribuir para o ganho de peso.",
            "gordura saturada" => $"Alto teor de gordura saturada ({per100.SaturatedFats:F1} g/{unitLabel}), associada ao aumento do colesterol ruim.",
            "sódio"            => $"Alto teor de sódio ({per100.SodiumMg:F0} mg/{unitLabel}), o que pode elevar a pressão arterial com o consumo frequente.",
            "gordura"          => $"Alto teor de gordura total ({per100.TotalFats:F1} g/{unitLabel}), tornando o produto muito calórico.",
            "calorias"         => $"Muitas calorias ({per100.CaloriesKcal:F0} kcal/{unitLabel}) para o perfil nutricional oferecido.",
            _                  => $"O principal problema identificado foi: {offender}."
        };

        return $"A nota {score} reflete principalmente: {impacto}";
    }

    private static string BuildPontoPrincipal(UnifiedNutritionScore unified, NutritionValues per100)
    {
        var offenderMessage = unified.PrincipalOffender switch
        {
            "açúcar"           => "Consumo excessivo de açúcar pode levar ao diabetes tipo 2 e obesidade.",
            "gordura saturada" => "Gordura saturada em excesso aumenta o risco de doenças cardiovasculares.",
            "sódio"            => "Excesso de sódio está ligado à hipertensão arterial.",
            "gordura"          => "Gordura total elevada aumenta a densidade calórica do produto.",
            "calorias"         => per100.CaloriesKcal switch
            {
                >= 450 => "Produto altamente calórico — atenção ao tamanho da porção.",
                >= 300 => "Calorias elevadas — atenção ao tamanho da porção.",
                >= 150 => "Produto calórico — controle o tamanho da porção.",
                _      => null
            },
            _                  => null
        };

        if (!string.IsNullOrWhiteSpace(offenderMessage))
            return offenderMessage;

        if (unified.Highlights is { Count: > 0 })
            return unified.Highlights[0];

        return "Verifique os ingredientes e consuma com moderação.";
    }

    // ── Perfis de saúde ───────────────────────────────────────────────────

    private static Dictionary<string, ProfileScore> BuildProfiles(
        NutritionValues per100,
        bool isBeverage,
        bool usesLiquidSugarScale,
        string unitLabel)
    {
        return new Dictionary<string, ProfileScore>
        {
            ["diabetico"]     = BuildDiabeticoProfile(per100, isBeverage, usesLiquidSugarScale, unitLabel),
            ["hipertensao"]   = BuildHipertensaoProfile(per100, isBeverage, usesLiquidSugarScale, unitLabel),
            ["emagrecimento"] = BuildEmagrecimentoProfile(per100, isBeverage, usesLiquidSugarScale, unitLabel),
            ["ganho_massa"]   = BuildGanhoMassaProfile(per100, isBeverage, usesLiquidSugarScale, unitLabel)
        };
    }

    private static ProfileScore BuildDiabeticoProfile(NutritionValues per100, bool isBeverage, bool usesLiquidSugarScale, string unitLabel)
    {
        var reasons = new List<string>();
        int penalty = 0;
        int bonus = 0;

        var sugar      = per100.Sugars;
        var addedSugar = per100.AddedSugars;
        var polyols    = per100.Polyols ?? 0;
        var carbs      = per100.Carbohydrates ?? 0;
        var glycemicCarbs = Math.Max(0, carbs - polyols * 0.5);
        var fiber      = per100.Fiber ?? 0;
        var protein    = per100.Proteins;
        var satFat     = per100.SaturatedFats ?? 0;
        var sodium     = per100.SodiumMg ?? 0;

        // Açúcar — escala depende do estado físico (Nutri-Score 2023):
        //   Sólidos: 0–5 excelente · 5–8 bom · 8–12.5 médio · 12.5–18 ruim · >18 muito ruim
        //   Bebidas: 0 ótimo · 0–2.5 bom · 2.5–5 médio · 5–9 ruim · ≥9 muito ruim
        // Para diabéticos, líquidos açucarados têm impacto glicêmico maior que sólidos
        // (sem matriz alimentar, absorção rápida) — daí a calibração mais rígida.
        if (sugar.HasValue && usesLiquidSugarScale)
        {
            var suffix = isBeverage ? " para bebida" : string.Empty;
            if (sugar.Value >= 9)        { penalty += 55; reasons.Add($"Açúcar muito alto{suffix} ({sugar.Value:F1} g/{unitLabel}) — evitar"); }
            else if (sugar.Value >= 5)   { penalty += 35; reasons.Add($"Açúcar alto{suffix} ({sugar.Value:F1} g/{unitLabel}) — controlar consumo"); }
            else if (sugar.Value >= 2.5) { penalty += 18; reasons.Add($"Açúcar moderado{suffix} ({sugar.Value:F1} g/{unitLabel}) — moderar"); }
            else if (sugar.Value > 0)    { penalty += 5;  reasons.Add($"Açúcar baixo ({sugar.Value:F1} g/{unitLabel})"); }
            else                   {                reasons.Add(isBeverage ? "Bebida sem açúcar — excelente" : "Sem açúcar — excelente"); }
        }
        else if (sugar.HasValue)
        {
            if (sugar.Value > 18)        { penalty += 50; reasons.Add($"Açúcar muito elevado ({sugar.Value:F1} g/{unitLabel}) — evitar"); }
            else if (sugar.Value > 12.5) { penalty += 35; reasons.Add($"Açúcar alto ({sugar.Value:F1} g/{unitLabel}) — controlar consumo"); }
            else if (sugar.Value > 8)    { penalty += 20; reasons.Add($"Açúcar moderado ({sugar.Value:F1} g/{unitLabel}) — consumir com moderação"); }
            else if (sugar.Value > 5)    { penalty += 10; reasons.Add($"Açúcar presente ({sugar.Value:F1} g/{unitLabel}) — moderação"); }
            else if (sugar.Value > 2)    { penalty += 3;  reasons.Add($"Açúcar baixo ({sugar.Value:F1} g/{unitLabel}) — favorável"); }
            else                         {                reasons.Add($"Açúcar muito baixo ({sugar.Value:F1} g/{unitLabel}) — excelente"); }
        }

        // Açúcares adicionados — red flag direto. Em bebida, qualquer presença relevante
        // já é prejudicial ao controle glicêmico.
        if (addedSugar.HasValue && usesLiquidSugarScale)
        {
            var suffix = isBeverage ? " em bebida" : string.Empty;
            if (addedSugar.Value >= 5)      { penalty += 25; reasons.Add($"Açúcar adicionado elevado{suffix} ({addedSugar.Value:F1} g/{unitLabel}) — evitar"); }
            else if (addedSugar.Value >= 1) { penalty += 15; reasons.Add($"Açúcar adicionado{suffix} ({addedSugar.Value:F1} g/{unitLabel}) — atenção"); }
            else if (addedSugar.Value > 0)  { penalty += 5;  reasons.Add($"Contém açúcar adicionado ({addedSugar.Value:F1} g/{unitLabel})"); }
            else                      {                reasons.Add("Sem açúcares adicionados"); }
        }
        else if (addedSugar.HasValue)
        {
            if (addedSugar.Value > 10)      { penalty += 20; reasons.Add($"Açúcar adicionado elevado ({addedSugar.Value:F1} g/{unitLabel}) — evitar"); }
            else if (addedSugar.Value > 5)  { penalty += 10; reasons.Add($"Açúcar adicionado presente ({addedSugar.Value:F1} g/{unitLabel}) — atenção"); }
            else if (addedSugar.Value > 0)  { penalty += 4;  reasons.Add($"Contém açúcar adicionado ({addedSugar.Value:F1} g/{unitLabel})"); }
            else                      {                reasons.Add("Sem açúcares adicionados"); }
        }

        if (polyols > 0)
            reasons.Add($"Contém polióis ({polyols:F1} g/{unitLabel}) — impacto glicêmico menor que açúcar, mas ainda exige atenção à porção");

        // Carboidratos com impacto glicêmico estimado: polióis declarados são
        // descontados do total porque costumam elevar menos a glicemia que açúcar/amido.
        if (glycemicCarbs > 60)      { penalty += 18; reasons.Add($"Carboidratos com impacto estimado muito altos ({glycemicCarbs:F1} g/{unitLabel}) — evitar"); }
        else if (glycemicCarbs > 45) { penalty += 10; reasons.Add($"Carboidratos com impacto estimado altos ({glycemicCarbs:F1} g/{unitLabel}) — controlar a porção"); }
        else if (glycemicCarbs > 30) { penalty += 5;  reasons.Add($"Carboidratos com impacto estimado moderados ({glycemicCarbs:F1} g/{unitLabel}) — atenção à quantidade"); }
        else if (glycemicCarbs <= 10) {              reasons.Add($"Baixo teor de carboidratos com impacto estimado ({glycemicCarbs:F1} g/{unitLabel}) — ótimo"); }

        // ⚠️ NOVO: Sódio alto é problema para diabéticos (risco cardiovascular + diabetes comum)
        if (sodium > 3000)     { penalty += 50; reasons.Add($"⚠️ Sódio CRÍTICO ({sodium:F0} mg/{unitLabel}) — EVITAR (risco cardiovascular)"); }
        else if (sodium > 1500){ penalty += 30; reasons.Add($"Sódio muito alto ({sodium:F0} mg/{unitLabel}) — EVITAR"); }
        else if (sodium > 800) { penalty += 20; reasons.Add($"Sódio alto ({sodium:F0} mg/{unitLabel}) — risco cardiovascular"); }
        else if (sodium > 400) { penalty += 10; reasons.Add($"Sódio elevado ({sodium:F0} mg/{unitLabel}) — moderar"); }

        // Gordura saturada — risco cardiovascular relevante para diabéticos tipo 2.
        // Para receber classificação adequada, precisa estar muito baixa.
        if (satFat > 10)       { penalty += 35; reasons.Add($"Gordura saturada muito alta ({satFat:F1} g/{unitLabel}) — evitar pelo risco cardiovascular"); }
        else if (satFat > 7)   { penalty += 25; reasons.Add($"Gordura saturada elevada ({satFat:F1} g/{unitLabel}) — aumenta risco cardiovascular"); }
        else if (satFat > 4)   { penalty += 15; reasons.Add($"Gordura saturada alta ({satFat:F1} g/{unitLabel}) — consumir com moderação"); }
        else if (satFat > 1.5) { penalty += 5;  reasons.Add($"Gordura saturada presente ({satFat:F1} g/{unitLabel}) — atenção ao consumo frequente"); }
        else if (satFat >= 0) {               bonus += 5; reasons.Add($"Gordura saturada baixa ({satFat:F1} g/{unitLabel}) — favorável"); }

        // Bônus por nutrientes positivos
        if (protein > 15)     { bonus += 8;  reasons.Add($"Excelente fonte de proteína ({protein.Value:F1} g/{unitLabel}) — baixo impacto glicêmico"); }
        else if (protein > 10) { bonus += 5; reasons.Add($"Boa fonte de proteína ({protein.Value:F1} g/{unitLabel})"); }
        if (fiber > 5)        { bonus += 12; reasons.Add($"Rico em fibras ({fiber:F1} g/{unitLabel}) — ajuda no controle glicêmico"); }
        else if (fiber > 2)   { bonus += 6;  reasons.Add($"Fibras moderadas ({fiber:F1} g/{unitLabel})"); }

        // Bônus de "perfil cardiovascular limpo" — relevante para diabéticos
        if (sodium <= 120 && satFat <= 1.5) { bonus += 5; }

        // Cap: bônus pode compensar até 60% da penalidade
        if (penalty > 0) bonus = Math.Min(bonus, (int)(penalty * 0.6));

        int score = Math.Clamp(100 - penalty + bonus, 0, 100);

        // Cap rígido para bebidas com açúcar relevante: bebidas açucaradas
        // não devem cair na faixa "Adequado" para diabéticos, independente
        // de outros sinais (princípio aceito por SBD/ADA).
        if (usesLiquidSugarScale)
        {
            if (isBeverage && (sugar >= 9 || addedSugar >= 5))      score = Math.Min(score, 25);
            else if (sugar >= 9 || addedSugar >= 5)                 score = Math.Min(score, 45);
            else if (isBeverage && (sugar >= 5 || addedSugar >= 1)) score = Math.Min(score, 50);
            else if (sugar >= 5 || addedSugar >= 1)                 score = Math.Min(score, 65);
            else if (sugar >= 2.5)                                  score = Math.Min(score, 70);
        }

        // ⚠️ NOVO: Cap para sódio crítico (mesmo sem açúcar)
        if (sodium > 3000) score = Math.Min(score, 20);

        // Cap cardiovascular: proteína/fibra não devem mascarar gordura saturada
        // em perfil diabético, pois o risco cardíaco é parte central da avaliação.
        if (satFat > 10)       score = Math.Min(score, 45);
        else if (satFat > 7)   score = Math.Min(score, 59);
        else if (satFat > 4)   score = Math.Min(score, 69);
        else if (satFat > 1.5) score = Math.Min(score, 79);

        return new ProfileScore { Score = score, Label = ClassifyProfileScore(score), Reasons = reasons };
    }

    private static ProfileScore BuildHipertensaoProfile(NutritionValues per100, bool isBeverage, bool usesLiquidSugarScale, string unitLabel)
    {
        var reasons = new List<string>();
        int score = 100;

        var sodium    = per100.SodiumMg ?? 0;
        var satFat    = per100.SaturatedFats ?? 0;
        var transFat  = per100.TransFats ?? 0;
        var sugar     = per100.Sugars ?? 0;
        var addedSug  = per100.AddedSugars ?? 0;

        // ── Sódio (driver principal — DASH / SBC / OMS) ───────────────────
        // ⚠️ NOVO: Thresholds estendidos para casos extremos (> 3000mg)
        if (sodium > 3000)     { score -= 95; reasons.Add($"⚠️ Sódio CRÍTICO ({sodium:F0} mg/{unitLabel} = {sodium/20:F0}% da recomendação diária) — EVITAR TOTALMENTE"); }
        else if (sodium > 1500){ score -= 70; reasons.Add($"Sódio extremamente alto ({sodium:F0} mg/{unitLabel}) — EVITAR"); }
        else if (sodium > 800) { score -= 50; reasons.Add($"Sódio muito alto ({sodium:F0} mg/{unitLabel}) — evitar"); }
        else if (sodium > 400) { score -= 30; reasons.Add($"Sódio elevado ({sodium:F0} mg/{unitLabel}) — consumir com moderação"); }
        else if (sodium > 200) { score -= 15; reasons.Add($"Sódio moderado ({sodium:F0} mg/{unitLabel}) — atenção ao consumo frequente"); }
        else if (sodium > 0)   { reasons.Add($"Baixo teor de sódio ({sodium:F0} mg/{unitLabel}) — favorável"); }
        else                   { reasons.Add("Produto sem sódio declarado"); }

        // ── Gordura saturada ──────────────────────────────────────────────
        // Para hipertensão, o foco não é só sódio: gordura saturada também pesa
        // no risco cardiovascular e impede classificação adequada quando elevada.
        if (satFat > 10)       { score -= 35; reasons.Add($"Gordura saturada muito alta ({satFat:F1} g/{unitLabel}) — evitar pelo risco cardiovascular"); }
        else if (satFat > 7)   { score -= 25; reasons.Add($"Gordura saturada elevada ({satFat:F1} g/{unitLabel}) — prejudicial ao coração"); }
        else if (satFat > 4)   { score -= 15; reasons.Add($"Gordura saturada alta ({satFat:F1} g/{unitLabel}) — consumir com moderação"); }
        else if (satFat > 1.5) { score -= 5;  reasons.Add($"Gordura saturada presente ({satFat:F1} g/{unitLabel}) — atenção ao consumo frequente"); }
        else if (satFat >= 0) {              reasons.Add($"Gordura saturada baixa ({satFat:F1} g/{unitLabel}) — positivo"); }

        // ── Gordura trans (AHA/OMS: zero é o ideal — disfunção endotelial → ↑PA) ──
        if (transFat > 0.5)      { score -= 25; reasons.Add($"Gordura trans presente ({transFat:F1} g/{unitLabel}) — evitar (eleva PA e risco cardiovascular)"); }
        else if (transFat > 0.1) { score -= 10; reasons.Add($"Pequena quantidade de gordura trans ({transFat:F1} g/{unitLabel}) — atenção"); }

        // ── Açúcar adicionado (AHA Scientific Statement 2014: ↑PA independente de peso) ──
        // Em bebidas a evidência é especialmente forte (estudos com SSB e HTN — Cohen 2012, Kim 2018).
        // DASH diet limita explicitamente açúcares adicionados.
        if (usesLiquidSugarScale)
        {
            var prefix = isBeverage ? "Bebida com" : "Produto com";
            if (addedSug >= 5)      { score -= 30; reasons.Add($"{prefix} muito açúcar adicionado ({addedSug:F1} g/{unitLabel}) — eleva PA (DASH/AHA)"); }
            else if (addedSug >= 1) { score -= 15; reasons.Add($"{prefix} açúcar adicionado ({addedSug:F1} g/{unitLabel}) — desfavorável para PA"); }

            if (sugar >= 9)         { score -= 15; reasons.Add($"Açúcar alto ({sugar:F1} g/{unitLabel}) — associado a maior risco de hipertensão"); }
            else if (sugar >= 5)    { score -= 8;  reasons.Add($"Açúcar presente ({sugar:F1} g/{unitLabel}) — moderar consumo"); }
        }
        else
        {
            if (addedSug > 10)     { score -= 18; reasons.Add($"Açúcar adicionado elevado ({addedSug:F1} g/{unitLabel}) — eleva PA (AHA)"); }
            else if (addedSug > 5) { score -= 10; reasons.Add($"Açúcar adicionado relevante ({addedSug:F1} g/{unitLabel}) — desfavorável"); }
            else if (addedSug > 0) { score -= 4;  reasons.Add($"Contém açúcar adicionado ({addedSug:F1} g/{unitLabel})"); }

            if (sugar > 22)        { score -= 10; reasons.Add($"Açúcar total muito alto ({sugar:F1} g/{unitLabel}) — desfavorável para PA"); }
        }

        if (satFat > 10)       score = Math.Min(score, 45);
        else if (satFat > 7)   score = Math.Min(score, 65);
        else if (satFat > 4)   score = Math.Min(score, 78);
        else if (satFat > 1.5) score = Math.Min(score, 79);

        score = Math.Clamp(score, 0, 100);
        return new ProfileScore { Score = score, Label = ClassifyProfileScore(score), Reasons = reasons };
    }

    private static ProfileScore BuildEmagrecimentoProfile(NutritionValues per100, bool isBeverage, bool usesLiquidSugarScale, string unitLabel)
    {
        var reasons = new List<string>();
        int penalty = 0;

        var calories = per100.CaloriesKcal ?? 0;
        var sugar    = per100.Sugars;
        var addedSug = per100.AddedSugars ?? 0;
        var polyols  = per100.Polyols ?? 0;
        var fat      = per100.TotalFats ?? 0;
        var satFat   = per100.SaturatedFats ?? 0;
        var protein  = per100.Proteins ?? 0;
        var fiber    = per100.Fiber ?? 0;
        var sodium   = per100.SodiumMg ?? 0;

        // Calorias — em bebidas, calorias líquidas são particularmente críticas
        // (não saciam, segundo Mattes 2006 / Pan & Hu 2011). Limiares mais rígidos.
        if (isBeverage)
        {
            if (calories >= 60)      { penalty += 25; reasons.Add($"Bebida calórica ({calories:F0} kcal/{unitLabel}) — calorias líquidas dificultam emagrecimento"); }
            else if (calories >= 30) { penalty += 12; reasons.Add($"Bebida com calorias relevantes ({calories:F0} kcal/{unitLabel}) — controlar quantidade"); }
            else if (calories > 0)   {                reasons.Add($"Bebida com poucas calorias ({calories:F0} kcal/{unitLabel}) — favorável"); }
        }
        else
        {
            if (calories > 400)                       { penalty += 30; reasons.Add($"Muito calórico ({calories:F0} kcal/{unitLabel}) — dificulta emagrecimento"); }
            else if (calories > 300)                  { penalty += 20; reasons.Add($"Calorias elevadas ({calories:F0} kcal/{unitLabel}) — atenção à quantidade"); }
            else if (calories > 250)                  { penalty += 15; reasons.Add($"Moderadamente calórico ({calories:F0} kcal/{unitLabel}) — atenção à quantidade"); }
            else if (calories > 150)                  { penalty += 5;  reasons.Add($"Calórico ({calories:F0} kcal/{unitLabel}) — controlar porção"); }
            else if (calories > 0 && calories <= 100) {                reasons.Add($"Poucas calorias ({calories:F0} kcal/{unitLabel}) — favorável"); }
        }

        // Açúcar — escala depende do estado físico
        if (sugar.HasValue && usesLiquidSugarScale)
        {
            var suffix = isBeverage ? " em bebida" : string.Empty;
            if (sugar.Value >= 9)        { penalty += 30; reasons.Add($"Açúcar muito alto{suffix} ({sugar.Value:F1} g/{unitLabel}) — favorece acúmulo de gordura"); }
            else if (sugar.Value >= 5)   { penalty += 18; reasons.Add($"Açúcar alto{suffix} ({sugar.Value:F1} g/{unitLabel}) — dificulta emagrecimento"); }
            else if (sugar.Value >= 2.5) { penalty += 8;  reasons.Add($"Açúcar moderado{suffix} ({sugar.Value:F1} g/{unitLabel}) — controlar consumo"); }
            else if (sugar.Value <= 1)   {                reasons.Add($"Sem açúcar relevante ({sugar.Value:F1} g/{unitLabel}) — positivo"); }
        }
        else if (sugar.HasValue)
        {
            if (sugar.Value > 18)        { penalty += 25; reasons.Add($"Açúcar muito alto ({sugar.Value:F1} g/{unitLabel}) — favorece acúmulo de gordura"); }
            else if (sugar.Value > 12.5) { penalty += 18; reasons.Add($"Açúcar alto ({sugar.Value:F1} g/{unitLabel}) — dificulta emagrecimento"); }
            else if (sugar.Value > 8)    { penalty += 10; reasons.Add($"Açúcar moderado ({sugar.Value:F1} g/{unitLabel}) — controlar consumo"); }
            else if (sugar.Value > 5)    { penalty += 5;  reasons.Add($"Açúcar presente ({sugar.Value:F1} g/{unitLabel}) — moderação"); }
            else if (sugar.Value <= 3)   {                reasons.Add($"Baixo teor de açúcar ({sugar.Value:F1} g/{unitLabel}) — positivo"); }
        }

        // Açúcares adicionados em bebidas: penalidade adicional
        if (usesLiquidSugarScale && addedSug >= 1)
        {
            penalty += 8;
            reasons.Add($"Açúcar adicionado ({addedSug:F1} g/{unitLabel}) — preferível evitar");
        }

        if (polyols >= 20)      { penalty += 8; reasons.Add($"Polióis elevados ({polyols:F1} g/{unitLabel}) — podem somar calorias e causar desconforto intestinal"); }
        else if (polyols >= 10) { penalty += 4; reasons.Add($"Polióis presentes ({polyols:F1} g/{unitLabel}) — atenção à quantidade consumida"); }

        // Gordura total — 9 kcal/g, impacto direto na densidade calórica
        if (fat > 20)       { penalty += 25; reasons.Add($"Alto teor de gordura ({fat:F1} g/{unitLabel}) — aumenta densidade calórica"); }
        else if (fat > 13)  { penalty += 15; reasons.Add($"Gordura total moderada ({fat:F1} g/{unitLabel}) — atenção à quantidade consumida"); }
        else if (fat > 7)   { penalty += 5;  reasons.Add($"Gordura moderada ({fat:F1} g/{unitLabel})"); }
        else if (fat <= 3)  {               reasons.Add($"Baixo teor de gordura ({fat:F1} g/{unitLabel}) — positivo"); }

        // Gordura saturada
        if (satFat > 10)     { penalty += 25; reasons.Add($"Gordura saturada muito alta ({satFat:F1} g/{unitLabel}) — prejudicial à saúde"); }
        else if (satFat > 7) { penalty += 20; reasons.Add($"Gordura saturada elevada ({satFat:F1} g/{unitLabel}) — prejudicial ao processo de emagrecimento"); }
        else if (satFat > 3) { penalty += 8;  reasons.Add($"Gordura saturada moderada ({satFat:F1} g/{unitLabel})"); }
        else if (satFat >= 0) {              reasons.Add($"Gordura saturada baixa ({satFat:F1} g/{unitLabel}) — positivo"); }

        // ⚠️ NOVO: Sódio alto causa retenção de líquido (falsa perda de peso)
        if (sodium > 3000)     { penalty += 35; reasons.Add($"⚠️ Sódio CRÍTICO ({sodium:F0} mg/{unitLabel}) — causa retenção de líquido"); }
        else if (sodium > 1500){ penalty += 20; reasons.Add($"Sódio muito alto ({sodium:F0} mg/{unitLabel}) — prejudica resultados"); }
        else if (sodium > 800) { penalty += 10; reasons.Add($"Sódio alto ({sodium:F0} mg/{unitLabel}) — causa retenção"); }

        // Bônus
        int bonus = 0;
        if (protein > 15)     { bonus += 12; reasons.Add($"Excelente fonte de proteína ({protein:F1} g/{unitLabel}) — ajuda na saciedade"); }
        else if (protein > 8) { bonus += 6;  reasons.Add($"Boa fonte de proteína ({protein:F1} g/{unitLabel}) — ajuda na saciedade"); }
        if (fiber > 5)        { bonus += 10; reasons.Add($"Rico em fibras ({fiber:F1} g/{unitLabel}) — promove saciedade"); }
        else if (fiber > 2)   { bonus += 5;  reasons.Add($"Fibras moderadas ({fiber:F1} g/{unitLabel})"); }

        if (penalty > 0) bonus = Math.Min(bonus, (int)(penalty * 0.6));

        int score = Math.Clamp(100 - penalty + bonus, 0, 100);

        // Cap rígido para bebidas açucaradas — calorias líquidas são
        // amplamente reconhecidas como obstaculizadoras de emagrecimento.
        if (usesLiquidSugarScale && isBeverage && ((sugar.HasValue && sugar.Value >= 5) || addedSug >= 1))
            score = Math.Min(score, 55);
        else if (usesLiquidSugarScale && ((sugar.HasValue && sugar.Value >= 5) || addedSug >= 1))
            score = Math.Min(score, 70);
        if (usesLiquidSugarScale && isBeverage && sugar >= 9)
            score = Math.Min(score, 35);
        else if (usesLiquidSugarScale && sugar >= 9)
            score = Math.Min(score, 55);

        // ⚠️ NOVO: Cap para sódio crítico
        if (sodium > 3000) score = Math.Min(score, 25);

        if (satFat > 10)       score = Math.Min(score, 58);
        else if (satFat > 7)   score = Math.Min(score, 68);
        else if (satFat > 4)   score = Math.Min(score, 75);

        return new ProfileScore { Score = score, Label = ClassifyProfileScore(score), Reasons = reasons };
    }

    private static ProfileScore BuildGanhoMassaProfile(NutritionValues per100, bool isBeverage, bool usesLiquidSugarScale, string unitLabel)
    {
        var reasons = new List<string>();
        int penalty = 0;
        int bonus = 0;

        var protein  = per100.Proteins;
        var calories = per100.CaloriesKcal;
        var sugar    = per100.Sugars;
        var addedSug = per100.AddedSugars;
        var satFat   = per100.SaturatedFats ?? 0;
        var carbs    = per100.Carbohydrates;
        var sodium   = per100.SodiumMg ?? 0;

        // ═══════════════════════════════════════════════════════════════════
        // ⚠️ PROTEÍNA - FATOR MAIS CRÍTICO PARA GANHO DE MASSA
        // ═══════════════════════════════════════════════════════════════════
        // Faixas SEM GAPS para cobrir todos os valores possíveis
        if (protein >= 25)      { bonus += 25; reasons.Add($"Proteína muito alta ({protein.Value:F1} g/{unitLabel}) — excelente para hipertrofia"); }
        else if (protein >= 15) { bonus += 15; reasons.Add($"Alta fonte de proteína ({protein.Value:F1} g/{unitLabel}) — ótimo para recuperação muscular"); }
        else if (protein >= 10) { bonus += 8;  reasons.Add($"Boa fonte de proteína ({protein.Value:F1} g/{unitLabel})"); }
        else if (protein >= 5)  { penalty += 35; reasons.Add($"⚠️ Proteína BAIXA ({protein.Value:F1} g/{unitLabel}) — INSUFICIENTE para ganho muscular"); }

        // ═══════════════════════════════════════════════════════════════════
        // CALORIAS - Necessário para superávit calórico
        // ═══════════════════════════════════════════════════════════════════
        if (calories >= 350)      { bonus += 8;  reasons.Add($"Produto muito calórico ({calories.Value:F0} kcal/{unitLabel}) — ajuda no superávit calórico"); }
        else if (calories >= 250) { bonus += 5;  reasons.Add($"Produto calórico ({calories.Value:F0} kcal/{unitLabel}) — auxilia no superávit"); }
        else if (calories >= 150) { penalty += 5; reasons.Add($"Calorias moderadas ({calories.Value:F0} kcal/{unitLabel}) — consumir em maior quantidade"); }
        else if (calories.HasValue) { penalty += 20; reasons.Add($"⚠️ POUCAS calorias ({calories.Value:F0} kcal/{unitLabel}) — INSUFICIENTE para superávit"); }

        // ═══════════════════════════════════════════════════════════════════
        // CARBOIDRATOS - Energia para treino
        // ═══════════════════════════════════════════════════════════════════
        if (carbs > 50)      { bonus += 5;  reasons.Add($"Alto teor de carboidratos ({carbs.Value:F1} g/{unitLabel}) — energia para treino"); }
        else if (carbs < 5)  { penalty += 5; reasons.Add($"Carboidratos baixos ({carbs.Value:F1} g/{unitLabel}) — complementar para energia"); }

        // ═══════════════════════════════════════════════════════════════════
        // AÇÚCAR - Preferência por carboidratos complexos
        // ═══════════════════════════════════════════════════════════════════
        if (sugar.HasValue && usesLiquidSugarScale)
        {
            var suffix = isBeverage ? " em bebida" : string.Empty;
            if (sugar.Value >= 9)        { penalty += 18; reasons.Add($"Açúcar alto{suffix} ({sugar.Value:F1} g/{unitLabel}) — prefira carboidratos de melhor qualidade"); }
            else if (sugar.Value >= 5)   { penalty += 10; reasons.Add($"Açúcar relevante{suffix} ({sugar.Value:F1} g/{unitLabel}) — preferível carboidrato complexo"); }
            else if (sugar.Value >= 2.5) { penalty += 4;  reasons.Add($"Açúcar moderado{suffix} ({sugar.Value:F1} g/{unitLabel})"); }
        }
        else if (sugar.HasValue)
        {
            if (sugar.Value > 20)        { penalty += 18; reasons.Add($"Açúcar excessivo ({sugar.Value:F1} g/{unitLabel}) — prefira carboidratos complexos"); }
            else if (sugar.Value > 12.5) { penalty += 8;  reasons.Add($"Açúcar alto ({sugar.Value:F1} g/{unitLabel}) — preferível carboidrato complexo"); }
            else if (sugar.Value > 8)    { penalty += 3;  reasons.Add($"Açúcar moderado ({sugar.Value:F1} g/{unitLabel})"); }
            else if (sugar.Value <= 3)   {                reasons.Add($"Baixo teor de açúcar ({sugar.Value:F1} g/{unitLabel}) — carboidratos de qualidade"); }
        }

        if (usesLiquidSugarScale && addedSug >= 1)
        {
            penalty += 6;
            reasons.Add($"Açúcar adicionado ({addedSug.Value:F1} g/{unitLabel}) — calorias vazias");
        }

        // ═══════════════════════════════════════════════════════════════════
        // GORDURA SATURADA - Saúde cardiovascular do atleta
        // ═══════════════════════════════════════════════════════════════════
        if (satFat > 10)     { penalty += 22; reasons.Add($"Gordura saturada muito elevada ({satFat:F1} g/{unitLabel}) — prejudica o perfil cardiovascular"); }
        else if (satFat > 7) { penalty += 14; reasons.Add($"Gordura saturada elevada ({satFat:F1} g/{unitLabel}) — moderar o consumo diário"); }
        else if (satFat > 4) { penalty += 6;  reasons.Add($"Gordura saturada moderada ({satFat:F1} g/{unitLabel})"); }

        // ═══════════════════════════════════════════════════════════════════
        // SÓDIO - Recuperação muscular e performance
        // ═══════════════════════════════════════════════════════════════════
        if (sodium > 3000)     { penalty += 30; reasons.Add($"⚠️ Sódio CRÍTICO ({sodium:F0} mg/{unitLabel}) — prejudica recuperação muscular"); }
        else if (sodium > 1500){ penalty += 20; reasons.Add($"Sódio muito alto ({sodium:F0} mg/{unitLabel}) — desfavorável para atletas"); }
        else if (sodium > 800) { penalty += 10; reasons.Add($"Sódio alto ({sodium:F0} mg/{unitLabel}) — moderar"); }

        // ═══════════════════════════════════════════════════════════════════
        // CÁLCULO DO SCORE
        // ═══════════════════════════════════════════════════════════════════
        if (penalty > 0) bonus = Math.Min(bonus, (int)(penalty * 0.6));

        int score = Math.Clamp(100 - penalty + bonus, 0, 100);

        // ═══════════════════════════════════════════════════════════════════
        // ⚠️ CAPS MÚLTIPLOS - Garante scores realistas
        // ═══════════════════════════════════════════════════════════════════

        // Cap 1: Açúcar crítico + Gordura saturada crítica = TOTALMENTE INADEQUADO
        if (sugar > 40 && satFat > 15)
        {
            score = Math.Min(score, 25);
            reasons.Add("⚠️ Produto INADEQUADO para ganho muscular (açúcar e gordura saturada críticos)");
        }
        // Cap 2: Açúcar alto + Gordura saturada alta = MUITO INADEQUADO
        else if (sugar > 25 && satFat > 10)
        {
            score = Math.Min(score, 45);
            reasons.Add("Produto com perfil nutricional ruim para ganho muscular (açúcar e gordura saturada altos)");
        }
        // Cap 3: Açúcar alto isolado
        else if (sugar > 40)
        {
            score = Math.Min(score, 50);
        }

        // Cap 4: Proteína baixa + Sódio crítico = TOTALMENTE INADEQUADO
        if (protein < 10 && sodium > 3000)
        {
            score = Math.Min(score, 10);
            reasons.Add("⚠️ Produto TOTALMENTE INADEQUADO para ganho muscular (proteína baixa + sódio crítico)");
        }
        // Cap 5: Proteína baixa + Sódio alto = MUITO INADEQUADO
        else if (protein < 10 && sodium > 1500)
        {
            score = Math.Min(score, 20);
            reasons.Add("Produto muito inadequado para ganho muscular (proteína baixa + sódio alto)");
        }
        // Cap 6: Apenas proteína baixa = INADEQUADO
        else if (protein.HasValue && protein.Value < 10)
        {
            score = Math.Min(score, 35);
            reasons.Add("Produto inadequado para ganho muscular (proteína insuficiente)");
        }

        if (satFat > 10)       score = Math.Min(score, 65);
        else if (satFat > 7)   score = Math.Min(score, 79);
        else if (satFat > 4)   score = Math.Min(score, 85);

        if (isBeverage && ((sugar.HasValue && sugar.Value >= 5) || addedSug >= 1))
            score = Math.Min(score, 55);

        return new ProfileScore { Score = score, Label = ClassifyProfileScore(score), Reasons = reasons };
    }

    private static string ClassifyProfileScore(int score) => score switch
    {
        >= 80 => "Adequado",
        >= 60 => "Moderado",
        >= 40 => "Atenção",
        _     => "Evitar"
    };

    private static string ProductDescription(NutritionValues per100, string unitLabel)
    {
        var cal = per100.CaloriesKcal;
        return cal.HasValue ? $"Contém {cal.Value:F0} kcal por {unitLabel}." : string.Empty;
    }
}
