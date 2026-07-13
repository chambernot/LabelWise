using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Models.Nutrition;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LabelWise.Infrastructure.Services;

public static class NutritionQualityEvaluator
{
    public static ImageQualityInfo EvaluateImageQuality(
        byte[]? imageBytes,
        EstimatedNutritionProfileDto? profile,
        IReadOnlyList<string>? warnings = null)
    {
        var filledFields = CountFields(profile);
        var completeness = profile is null ? 0 : NutritionCompletenessCalculator.Calculate(profile);
        var confidence = profile?.NutritionConfidence?.GlobalScore;
        var warningList = warnings?.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        var visualWarnings = new List<string>();

        var blurDetected = false;
        var reflectionDetected = false;
        var croppedTable = false;
        var imageConfidencePenalty = 0;

        if (imageBytes is null || imageBytes.Length == 0)
        {
            visualWarnings.Add("Imagem não disponível para avaliação visual.");
            imageConfidencePenalty += 30;
        }
        else
        {
            try
            {
                using var image = Image.Load<Rgba32>(imageBytes);
                if (Math.Min(image.Width, image.Height) < 700)
                {
                    blurDetected = true;
                    visualWarnings.Add("Imagem com resolução baixa para leitura precisa.");
                    imageConfidencePenalty += 15;
                }

                var stats = SampleImageStats(image);
                if (stats.AverageEdge < 7)
                {
                    blurDetected = true;
                    visualWarnings.Add("Possível desfoque detectado.");
                    imageConfidencePenalty += 15;
                }

                if (stats.BrightRatio > 0.18)
                {
                    reflectionDetected = true;
                    visualWarnings.Add("Possível reflexo ou brilho excessivo na imagem.");
                    imageConfidencePenalty += 15;
                }

                if (image.Width < 900 || image.Height < 900)
                {
                    croppedTable = true;
                    visualWarnings.Add("A tabela pode estar cortada ou pequena na foto.");
                    imageConfidencePenalty += 10;
                }
            }
            catch
            {
                visualWarnings.Add("Não foi possível avaliar a qualidade visual da imagem.");
                imageConfidencePenalty += 20;
            }
        }

        if (warningList.Any(IsPartialReadWarning))
            visualWarnings.Add("Tabela parcialmente obstruída ou com leitura inconsistente.");

        if (profile?.IsPer100Derived == true)
            visualWarnings.Add("Valores por 100g/100ml foram derivados da porção; use como leitura parcial.");

        if (filledFields < 5)
            visualWarnings.Add("Nem todos os nutrientes puderam ser confirmados.");

        var reliability = CalculateReliabilityScore(profile, warningList, imageConfidencePenalty);
        var overallConfidence = ToConfidence(reliability);
        var safe = reliability >= 70 && filledFields >= 5 && !warningList.Any(IsSevereWarning) && profile?.IsPer100Derived != true;

        return new ImageQualityInfo
        {
            OverallConfidence = overallConfidence,
            TableVisible = filledFields >= 3,
            TablePartiallyObstructed = warningList.Any(IsPartialReadWarning) || profile?.IsPer100Derived == true || filledFields is > 0 and < 6,
            BlurDetected = blurDetected,
            ReflectionDetected = reflectionDetected,
            CroppedTable = croppedTable,
            FingerObstructionDetected = warningList.Any(w => ContainsAny(w, ["dedo", "mão", "mao", "obstru"])),
            TextLegibility = overallConfidence,
            SafeForPreciseNutritionAnalysis = safe,
            Warnings = visualWarnings.Concat(HumanizeWarnings(warningList)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            RetryRequested = reliability < 35,
            ReasonCode = reliability < 35 ? "unsafe_read" : safe ? "ok" : "partial_read",
            Reason = safe
                ? null
                : reliability < 35
                    ? "A tabela nutricional não ficou legível o suficiente. Recomendamos tirar uma foto mais nítida."
                    : "A análise pode conter imprecisões porque a leitura parece parcial.",
            ConfidenceScore = confidence,
            CompletenessPercent = completeness
        };
    }

    public static NutritionAnalysisQualityDto EvaluateAnalysisQuality(
        EstimatedNutritionProfileDto? profile,
        ImageQualityInfo imageQuality,
        IReadOnlyList<string>? warnings = null)
    {
        var warningList = warnings ?? [];
        var reliability = CalculateReliabilityScore(profile, warningList, imageQuality.SafeForPreciseNutritionAnalysis ? 0 : 10);
        var hasSevereWarnings = warningList.Any(IsSevereWarning);

        if (!imageQuality.TableVisible || reliability < 35 || hasSevereWarnings)
        {
            return new NutritionAnalysisQualityDto
            {
                Mode = "unsafe",
                Confidence = "low",
                Reason = imageQuality.Reason ?? "Leitura nutricional insegura. Evite usar o score como conclusão definitiva."
            };
        }

        if (!imageQuality.SafeForPreciseNutritionAnalysis || profile?.IsPer100Derived == true || reliability < 70)
        {
            return new NutritionAnalysisQualityDto
            {
                Mode = "partial",
                Confidence = ToConfidence(reliability),
                Reason = "Leitura parcial: alguns campos podem estar ausentes ou com baixa confiança."
            };
        }

        return new NutritionAnalysisQualityDto
        {
            Mode = "complete",
            Confidence = ToConfidence(reliability),
            Reason = "Tabela nutricional legível e consistente para análise."
        };
    }

    public static int CalculateReliabilityScore(EstimatedNutritionProfileDto? profile, IReadOnlyList<string>? warnings, int visualPenalty = 0)
    {
        if (profile is null)
            return 0;

        var confidenceScore = profile.NutritionConfidence?.GlobalScore ?? profile.ParserConfidence switch
        {
            "high" => 0.85,
            "medium" => 0.65,
            _ => 0.45
        };
        var completeness = NutritionCompletenessCalculator.Calculate(profile);
        var fieldScore = CountFields(profile) * 100 / 11;
        var score = (int)Math.Round(confidenceScore * 45 + completeness * 0.35 + fieldScore * 0.20);

        if (profile.IsPer100Derived)
            score -= 20;

        foreach (var warning in warnings ?? [])
        {
            if (IsRecoverableServingColumnWarning(warning)) score -= 5;
            else if (IsSevereWarning(warning)) score -= 25;
            else if (IsPartialReadWarning(warning)) score -= 12;
            else if (ContainsAny(warning, ["inconsist", "diverge", "conflit", "anulado", "ignorado", "descartad"])) score -= 10;
        }

        score -= visualPenalty;
        return Math.Clamp(score, 0, 100);
    }

    public static IngredientContextDto BuildIngredientContext(IReadOnlyList<string>? ingredients, string? processingLevel)
    {
        var source = string.Join(" ", ingredients ?? []);
        var hasNaturalFat = ContainsAny(source, ["coco", "castanha", "castanhas", "amendoim", "abacate", "azeite", "nozes", "amêndoa", "amendoa"]);
        var hasIndustrialFat = ContainsAny(source, ["gordura vegetal", "hidrogenada", "óleo interesterificado", "oleo interesterificado", "margarina"]);
        var fatSource = hasNaturalFat && hasIndustrialFat
            ? "mixed"
            : hasNaturalFat
                ? "natural"
                : hasIndustrialFat
                    ? "industrial"
                    : "unknown";
        var processingContext = processingLevel?.Trim().ToLowerInvariant() switch
        {
            "in_natura" => "natural",
            "natural" => "natural",
            "minimamente_processado" => "minimally_processed",
            "minimally_processed" => "minimally_processed",
            "processado" => "processed",
            "processed" => "processed",
            "ultraprocessado" => "ultra_processed",
            "ultra_processed" => "ultra_processed",
            _ => "unknown"
        };

        return new IngredientContextDto
        {
            FatSource = fatSource,
            ProcessingContext = processingContext,
            FoodNature = processingContext is "natural" or "minimally_processed" || (hasNaturalFat && !hasIndustrialFat)
                ? "natural"
                : processingContext == "ultra_processed"
                    ? "industrial"
                    : processingContext
        };
    }

    public static void ApplyScoreReliability(UnifiedNutritionScore? score, NutritionAnalysisQualityDto analysisQuality, int reliabilityScore)
    {
        if (score is null)
            return;

        score.Confidence = ToConfidence(reliabilityScore);
        score.Reliability = analysisQuality.Mode switch
        {
            "complete" => "reliable",
            "partial" => "partial_read",
            _ => "unsafe_read"
        };
    }

    public static void ApplyScoreReliability(ScoreSection? score, NutritionAnalysisQualityDto analysisQuality, int reliabilityScore)
    {
        if (score is null)
            return;

        score.Confidence = ToConfidence(reliabilityScore);
        score.Reliability = analysisQuality.Mode switch
        {
            "complete" => "reliable",
            "partial" => "partial_read",
            _ => "unsafe_read"
        };
    }

    private static int CountFields(EstimatedNutritionProfileDto? profile)
    {
        if (profile is null)
            return 0;

        var count = 0;
        if (profile.CaloriesPer100g.HasValue || profile.CaloriesPer100ml.HasValue) count++;
        if (profile.EstimatedCarbsPer100g.HasValue) count++;
        if (profile.EstimatedSugarPer100g.HasValue) count++;
        if (profile.EstimatedAddedSugarPer100g.HasValue) count++;
        if (profile.EstimatedPolyolsPer100g.HasValue) count++;
        if (profile.EstimatedProteinPer100g.HasValue) count++;
        if (profile.EstimatedFatPer100g.HasValue) count++;
        if (profile.EstimatedSaturatedFatPer100g.HasValue) count++;
        if (profile.EstimatedTransFatPer100g.HasValue) count++;
        if (profile.EstimatedFiberPer100g.HasValue) count++;
        if (profile.EstimatedSodiumPer100g.HasValue) count++;
        return count;
    }

    private static ImageStats SampleImageStats(Image<Rgba32> image)
    {
        var stepX = Math.Max(1, image.Width / 64);
        var stepY = Math.Max(1, image.Height / 64);
        var samples = 0;
        var bright = 0;
        double edgeSum = 0;

        for (var y = stepY; y < image.Height; y += stepY)
        {
            for (var x = stepX; x < image.Width; x += stepX)
            {
                var pixel = image[x, y];
                var left = image[Math.Max(0, x - stepX), y];
                var top = image[x, Math.Max(0, y - stepY)];
                var luma = Luma(pixel);
                if (luma > 245) bright++;
                edgeSum += Math.Abs(luma - Luma(left)) + Math.Abs(luma - Luma(top));
                samples++;
            }
        }

        return samples == 0
            ? new ImageStats(0, 0)
            : new ImageStats(edgeSum / (samples * 2), bright / (double)samples);
    }

    private static double Luma(Rgba32 pixel) => pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114;

    private static IEnumerable<string> HumanizeWarnings(IEnumerable<string> warnings)
    {
        foreach (var warning in warnings)
        {
            if (IsRecoverableServingColumnWarning(warning)) yield return "A coluna por porção foi descartada, mas a base por 100g foi preservada.";
            else if (ContainsAny(warning, ["inconsist", "diverge", "conflit"])) yield return "Foram detectadas inconsistências entre valores nutricionais.";
            else if (ContainsAny(warning, ["ignorado", "anulado", "descartad"])) yield return "Alguns campos foram descartados por baixa confiança.";
            else if (IsPartialReadWarning(warning)) yield return "A análise pode conter imprecisões.";
        }
    }

    private static bool IsSevereWarning(string warning) =>
        !IsRecoverableServingColumnWarning(warning) &&
        ContainsAny(warning, ["rejeitada", "múltiplas inconsistências", "multiplas inconsistencias", "provável leitura desalinhada", "provavel leitura desalinhada", "unsafe"]);

    private static bool IsPartialReadWarning(string warning) =>
        !IsRecoverableServingColumnWarning(warning) &&
        ContainsAny(warning, ["parcial", "obstru", "cortad", "desfoc", "reflex", "ileg", "desalinh", "derivad", "baixa confiança", "baixa confianca"]);

    private static bool IsRecoverableServingColumnWarning(string warning) =>
        ContainsAny(warning, ["coluna por porção descartada", "coluna por porcao descartada", "valor por porção", "valor por porcao", "campo por porção ignorado", "campo por porcao ignorado"]) &&
        ContainsAny(warning, ["coluna 100g", "100g", "base por 100g", "coluna 100ml", "100ml"]);

    private static bool ContainsAny(string source, IEnumerable<string> terms)
    {
        var normalized = source.ToLowerInvariant();
        return terms.Any(term => normalized.Contains(term.ToLowerInvariant(), StringComparison.Ordinal));
    }

    private static string ToConfidence(int reliabilityScore) => reliabilityScore switch
    {
        >= 75 => "high",
        >= 50 => "medium",
        _ => "low"
    };

    private sealed record ImageStats(double AverageEdge, double BrightRatio);
}
