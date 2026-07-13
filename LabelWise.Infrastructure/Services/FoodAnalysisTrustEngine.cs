using LabelWise.Application.DTOs.FoodAnalysisTrust;
using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services;

public sealed class FoodAnalysisTrustEngine(ILogger<FoodAnalysisTrustEngine> logger) : IFoodAnalysisTrustEngine
{
    public FoodAnalysisTrustReport Evaluate(FoodAnalysisTrustInput input)
    {
        var score = 100;
        var reasons = new List<string>();
        var failures = new List<string>();
        var signals = new List<FoodAnalysisTrustSignalDto>();

        ApplyOcrQuality(input, ref score, reasons, failures, signals);
        ApplyImageQuality(input, ref score, reasons, failures, signals);
        ApplyRegionQuality(input, ref score, reasons, failures, signals);
        ApplySemanticQuality(input, ref score, reasons, failures, signals);
        ApplyNutritionConsistency(input, ref score, reasons, failures, signals);

        score = Math.Clamp(score, 0, 100);
        var trustLevel = score >= 80 ? "high" : score >= 60 ? "medium" : score >= 40 ? "low" : "very_low";
        var hasCriticalFailure = failures.Count > 0;
        var safeToScore = score >= 60 && !hasCriticalFailure && !input.PartialRead && !input.TablePartiallyObstructed;
        var report = new FoodAnalysisTrustReport
        {
            AnalysisTrustScore = score,
            TrustLevel = trustLevel,
            SafeToConclude = score >= 60 && !input.PartialRead && !input.ParsingBroken,
            SafeToRecommend = score >= 70 && !hasCriticalFailure,
            SafeToScore = safeToScore,
            AnalysisMode = ResolveAnalysisMode(score, input, hasCriticalFailure),
            SafeModeRequired = score < 60 || input.PartialRead || input.TablePartiallyObstructed || input.IngredientCompletenessLow || hasCriticalFailure,
            Reasons = reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            QualityGateFailures = failures.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Signals = signals
        };

        logger.LogInformation(
            "[FoodTrust] Source={Source}; Score={Score}; Level={Level}; SafeMode={SafeMode}; Failures={Failures}",
            input.Source,
            report.AnalysisTrustScore,
            report.TrustLevel,
            report.SafeModeRequired,
            string.Join(" | ", report.QualityGateFailures));

        return report;
    }

    private static void ApplyOcrQuality(FoodAnalysisTrustInput input, ref int score, List<string> reasons, List<string> failures, List<FoodAnalysisTrustSignalDto> signals)
    {
        var ocrScore = ConfidenceToScore(input.OcrConfidence);
        signals.Add(Signal("ocr_confidence", input.OcrConfidence, ocrScore / 100d, input.Source));

        if (input.OcrConfidence == "low")
        {
            score -= 28;
            reasons.Add("Confiança OCR baixa limita conclusões fortes.");
        }
        else if (input.OcrConfidence == "medium")
        {
            score -= 10;
            reasons.Add("Confiança OCR média exige cautela.");
        }

        if (input.OcrCorrectionCount > 0)
        {
            score -= Math.Min(24, input.OcrCorrectionCount * 4);
            reasons.Add("Correções de OCR indicam leitura instável.");
        }

        if (input.DuplicatedOcr)
        {
            score -= 10;
            reasons.Add("OCR duplicado ou repetitivo detectado.");
        }

        if (input.PartialRead)
        {
            score -= 25;
            failures.Add("partial_read");
            reasons.Add("Leitura parcial: não é seguro concluir ausência de ingredientes ou claims.");
        }

        if (input.ParsingBroken)
        {
            score -= 30;
            failures.Add("parsing_broken");
            reasons.Add("Parsing quebrado bloqueia score definitivo.");
        }
    }

    private static void ApplyImageQuality(FoodAnalysisTrustInput input, ref int score, List<string> reasons, List<string> failures, List<FoodAnalysisTrustSignalDto> signals)
    {
        if (input.BlurDetected)
        {
            score -= 15;
            reasons.Add("Imagem desfocada reduz confiança.");
            signals.Add(Signal("blur", "true", 1, input.Source));
        }

        if (input.ReflectionDetected)
        {
            score -= 12;
            reasons.Add("Reflexo detectado pode ocultar texto.");
            signals.Add(Signal("reflection", "true", 1, input.Source));
        }

        if (input.CroppedTable || input.TablePartiallyObstructed)
        {
            score -= 25;
            failures.Add(input.CroppedTable ? "cropped_table" : "table_partially_obstructed");
            reasons.Add("Tabela nutricional cortada ou obstruída.");
        }
    }

    private static void ApplyRegionQuality(FoodAnalysisTrustInput input, ref int score, List<string> reasons, List<string> failures, List<FoodAnalysisTrustSignalDto> signals)
    {
        signals.Add(Signal("ingredient_region", input.IngredientRegionDetected ? "detected" : "missing", input.IngredientRegionDetected ? 0.9 : 0.2, input.Source));
        signals.Add(Signal("nutrition_region", input.NutritionRegionDetected ? "detected" : "missing", input.NutritionRegionDetected ? 0.9 : 0.2, input.Source));
        signals.Add(Signal("regulatory_claim_region", input.RegulatoryClaimRegionDetected ? "detected" : "missing", input.RegulatoryClaimRegionDetected ? 0.9 : 0.4, input.Source));

        if (!input.IngredientRegionDetected && input.Source.Contains("ingredient", StringComparison.OrdinalIgnoreCase))
        {
            score -= 20;
            failures.Add("ingredient_region_missing");
            reasons.Add("Região de ingredientes não foi isolada com segurança.");
        }

        if (input.IngredientCompletenessLow)
        {
            score -= 18;
            failures.Add("ingredient_completeness_low");
            reasons.Add("Lista de ingredientes parece incompleta.");
        }

        if (input.TableCompletenessLow)
        {
            score -= 18;
            failures.Add("table_completeness_low");
            reasons.Add("Tabela nutricional incompleta.");
        }

        if (input.BoundaryLeakDetected)
        {
            score -= 20;
            failures.Add("semantic_boundary_leak");
            reasons.Add("Mistura entre regiões do rótulo foi detectada e bloqueada.");
        }
    }

    private static void ApplySemanticQuality(FoodAnalysisTrustInput input, ref int score, List<string> reasons, List<string> failures, List<FoodAnalysisTrustSignalDto> signals)
    {
        var conflictCount = input.TextConsistencyConflicts + input.IngredientConsistencyConflicts + input.SemanticConflictCount;
        if (conflictCount > 0)
        {
            score -= Math.Min(30, conflictCount * 8);
            failures.Add("semantic_conflicts");
            reasons.Add("Conflitos semânticos reduzem a confiança da análise.");
        }

        if (input.InferredDataCount > 0)
        {
            score -= Math.Min(30, input.InferredDataCount * 5);
            reasons.Add("Dados inferidos foram tratados com menor confiança.");
            signals.Add(Signal("inferred_data", input.InferredDataCount.ToString(), Math.Max(0.1, 1d - input.InferredDataCount * 0.08), input.Source, "inferred"));
        }

        if (input.LowConfidenceIngredientCount > 0)
        {
            score -= Math.Min(25, input.LowConfidenceIngredientCount * 6);
            reasons.Add("Ingredientes com baixa confiança limitam conclusões dietéticas.");
        }

        if (input.ExplicitClaimCount > 0)
        {
            score += Math.Min(8, input.ExplicitClaimCount * 2);
            signals.Add(Signal("explicit_claims", input.ExplicitClaimCount.ToString(), 1, input.Source, "regulatory_claim"));
        }

        foreach (var warning in input.Warnings.Where(IsTrustWarning).Take(6))
            reasons.Add(warning);
    }

    private static void ApplyNutritionConsistency(FoodAnalysisTrustInput input, ref int score, List<string> reasons, List<string> failures, List<FoodAnalysisTrustSignalDto> signals)
    {
        if (input.NutritionValues.Count == 0)
            return;

        var inconsistencies = DetectNutritionInconsistencies(input.NutritionValues);
        foreach (var inconsistency in inconsistencies)
        {
            score -= 18;
            failures.Add(inconsistency.Code);
            reasons.Add(inconsistency.Reason);
            signals.Add(Signal(inconsistency.Code, "inconsistent", 1, input.Source));
        }
    }

    private static IEnumerable<(string Code, string Reason)> DetectNutritionInconsistencies(IReadOnlyDictionary<string, double?> values)
    {
        var sugar = Get(values, "sugar");
        var addedSugar = Get(values, "added_sugar");
        var carbs = Get(values, "carbs");
        var fat = Get(values, "fat");
        var saturatedFat = Get(values, "saturated_fat");
        var protein = Get(values, "protein");
        var calories = Get(values, "calories");
        var sodium = Get(values, "sodium");

        if (sugar.HasValue && addedSugar.HasValue && sugar.Value + 0.1 < addedSugar.Value)
            yield return ("added_sugar_gt_total_sugar", "Açúcar adicionado maior que açúcar total.");
        if (carbs.HasValue && sugar.HasValue && carbs.Value + 0.1 < sugar.Value)
            yield return ("sugar_gt_carbs", "Açúcar total maior que carboidratos.");
        if (fat.HasValue && saturatedFat.HasValue && fat.Value + 0.1 < saturatedFat.Value)
            yield return ("saturated_fat_gt_total_fat", "Gordura saturada maior que gordura total.");
        if (protein is > 100)
            yield return ("protein_impossible", "Proteína por 100g acima do limite físico.");
        if (sodium is > 10000)
            yield return ("sodium_impossible", "Sódio por 100g acima do limite plausível.");
        if (calories.HasValue && (carbs.HasValue || protein.HasValue || fat.HasValue))
        {
            var macroCalories = (carbs ?? 0) * 4 + (protein ?? 0) * 4 + (fat ?? 0) * 9;
            if (macroCalories > 0 && Math.Abs(calories.Value - macroCalories) > Math.Max(120, macroCalories * 0.45))
                yield return ("kcal_macro_mismatch", "Calorias incompatíveis com os macronutrientes lidos.");
        }
    }

    private static double? Get(IReadOnlyDictionary<string, double?> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static string ResolveAnalysisMode(int score, FoodAnalysisTrustInput input, bool hasCriticalFailure)
    {
        if (score < 40 || input.ParsingBroken) return "unsafe";
        if (input.TableCompletenessLow && !input.NutritionRegionDetected) return "category_only";
        if (score < 75 || input.PartialRead || hasCriticalFailure) return "partial";
        return "complete";
    }

    private static bool IsTrustWarning(string warning) =>
        warning.Contains("parcial", StringComparison.OrdinalIgnoreCase) ||
        warning.Contains("desfoc", StringComparison.OrdinalIgnoreCase) ||
        warning.Contains("reflex", StringComparison.OrdinalIgnoreCase) ||
        warning.Contains("ileg", StringComparison.OrdinalIgnoreCase) ||
        warning.Contains("fronteira", StringComparison.OrdinalIgnoreCase) ||
        warning.Contains("confiança", StringComparison.OrdinalIgnoreCase) ||
        warning.Contains("confianca", StringComparison.OrdinalIgnoreCase) ||
        warning.Contains("inconsist", StringComparison.OrdinalIgnoreCase);

    private static int ConfidenceToScore(string confidence) => confidence switch
    {
        "high" => 90,
        "medium" => 65,
        "low" => 35,
        "very_low" => 15,
        _ => 35
    };

    private static FoodAnalysisTrustSignalDto Signal(string name, string value, double confidence, string source, string detectionType = "confirmed") =>
        new()
        {
            Name = name,
            Value = value,
            Confidence = Math.Clamp(confidence, 0, 1),
            Source = source,
            DetectionType = detectionType
        };
}
