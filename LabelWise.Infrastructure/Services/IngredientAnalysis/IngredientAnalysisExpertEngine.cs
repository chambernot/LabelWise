using LabelWise.Application.DTOs.IngredientAnalysis;
using System.Text.RegularExpressions;

namespace LabelWise.Infrastructure.Services.IngredientAnalysis;

public sealed class ProcessingLevelClassifier
{
    public ProcessingLevelDto Evaluate(IReadOnlyList<string> ingredients, IReadOnlyList<IngredientNormalizedDto> normalizedIngredients)
        => Evaluate(ingredients, normalizedIngredients, []);

    public ProcessingLevelDto Evaluate(IReadOnlyList<string> ingredients, IReadOnlyList<IngredientNormalizedDto> normalizedIngredients, IReadOnlyList<string> claims)
    {
        var classification = Classify(ingredients, normalizedIngredients, claims);
        return new ProcessingLevelDto
        {
            Value = classification.Level == "natural" ? "minimally_processed" : classification.Level,
            Confidence = classification.Confidence,
            ProcessingScore = classification.ProcessingScore,
            Reasons = classification.Reasons
        };
    }

    public ProcessingClassificationDto Classify(IReadOnlyList<string> ingredients, IReadOnlyList<IngredientNormalizedDto> normalizedIngredients)
        => Classify(ingredients, normalizedIngredients, []);

    public ProcessingClassificationDto Classify(IReadOnlyList<string> ingredients, IReadOnlyList<IngredientNormalizedDto> normalizedIngredients, IReadOnlyList<string> claims)
    {
        var sources = ingredients
            .Concat(normalizedIngredients.Select(item => item.Raw))
            .Concat(normalizedIngredients.Select(item => item.Normalized))
            .Concat(claims)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        if (sources.Count == 0)
        {
            return new ProcessingClassificationDto
            {
                Level = "unknown",
                Confidence = "low",
                ProcessingScore = 0,
                Reasons = ["Lista de ingredientes insuficiente para classificar o processamento."]
            };
        }

        var markerReasons = IngredientDictionary.ProcessingMarkerTerms
            .Where(term => sources.Any(source => IngredientTextNormalizer.ContainsAny(source, [term])))
            .Select(ToProcessingReason)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var additiveCategories = normalizedIngredients
            .Where(item => IngredientDictionary.UltraProcessingCategories.Contains(item.Category, StringComparer.OrdinalIgnoreCase))
            .Select(item => item.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var markerCount = Math.Max(markerReasons.Count, additiveCategories.Count);
        var industrialIngredientCount = normalizedIngredients.Count(item => IngredientDictionary.UltraProcessingCategories.Contains(item.Category, StringComparer.OrdinalIgnoreCase));
        var industrialDensity = ingredients.Count == 0 ? 0 : (double)industrialIngredientCount / ingredients.Count;
        var hasArtificialSweetener = sources.Any(source => IngredientTextNormalizer.ContainsAny(source, IngredientDictionary.ArtificialSweetenerTerms));
        var hasPreservative = sources.Any(source => IngredientTextNormalizer.ContainsAny(source, IngredientDictionary.PreservativeTerms));
        var hasEmulsifier = normalizedIngredients.Any(item => item.Category == "emulsifier") || sources.Any(source => IngredientTextNormalizer.ContainsAny(source, ["emulsificante", "mono e diglicerídeos", "mono e diglicerideos", "lecitina"]));
        var hasStabilizer = normalizedIngredients.Any(item => item.Category == "stabilizer") || sources.Any(source => IngredientTextNormalizer.ContainsAny(source, ["estabilizante", "espessante", "goma xantana", "goma guar", "carragena"]));
        var hasIndustrialClaim = sources.Any(source => IngredientTextNormalizer.ContainsAny(source, IngredientDictionary.IndustrialClaimTerms));
        var hasAddedSugar = sources.Any(source => IngredientClassifier.IsRealSugarIngredient(source));
        var naturalMatches = sources.Count(source => IngredientTextNormalizer.ContainsAny(source, IngredientDictionary.NaturalPredominantTerms));
        var naturalPredominant = naturalMatches > 0 && naturalMatches >= Math.Max(1, ingredients.Count / 2) && markerCount == 0;
        var processingScore = CalculateProcessingScore(markerReasons.Count, additiveCategories.Count, hasArtificialSweetener, hasPreservative, hasEmulsifier, hasStabilizer, hasIndustrialClaim, hasAddedSugar, naturalPredominant, industrialDensity);
        var scoreLevel = ResolveProcessingLevel(processingScore);

        if (!naturalPredominant && ingredients.Count <= 2 && markerCount == 0 && !hasAddedSugar)
        {
            return new ProcessingClassificationDto
            {
                Level = "unknown",
                Confidence = "low",
                ProcessingScore = 0,
                Reasons = ["Lista de ingredientes curta ou parcial: não é seguro classificar processamento como mínimo."]
            };
        }

        if (processingScore == 0 && (markerReasons.Count > 0 || additiveCategories.Count > 0 || hasArtificialSweetener || hasPreservative || hasIndustrialClaim))
        {
            processingScore = new ProcessingSignalRecovery().RecoverProcessingScore(processingScore, ingredients.ToArray(), sources.ToArray());
            scoreLevel = ResolveProcessingLevel(processingScore);
        }

        if (!naturalPredominant && (markerCount >= 2 || industrialDensity >= 0.25 || (hasEmulsifier && hasStabilizer) || (hasArtificialSweetener && hasPreservative) || (hasIndustrialClaim && (markerCount >= 1 || hasArtificialSweetener || hasPreservative))))
        {
            var reasons = markerReasons.Count > 0
                ? markerReasons
                : ["Contém múltiplos aditivos artificiais."];

            if (hasArtificialSweetener && !reasons.Any(reason => reason.Contains("adoçante", StringComparison.OrdinalIgnoreCase)))
                reasons.Add("Contém adoçante artificial");

            if (hasPreservative && !reasons.Any(reason => reason.Contains("conservante", StringComparison.OrdinalIgnoreCase)))
                reasons.Add("Contém conservante artificial");

            if (hasEmulsifier && !reasons.Any(reason => reason.Contains("emulsificante", StringComparison.OrdinalIgnoreCase)))
                reasons.Add("Contém emulsificante industrial");

            if (hasStabilizer && !reasons.Any(reason => reason.Contains("estabilizante", StringComparison.OrdinalIgnoreCase)))
                reasons.Add("Contém estabilizante/espessante industrial");

            if (hasIndustrialClaim && !reasons.Any(reason => reason.Contains("claim", StringComparison.OrdinalIgnoreCase)))
                reasons.Add("Apresenta claim industrial associado a formulação processada");

            return new ProcessingClassificationDto
            {
                Level = scoreLevel == "processed" && (industrialDensity >= 0.25 || markerCount >= 2) ? "ultra_processed" : scoreLevel,
                Confidence = markerCount >= 3 || industrialDensity >= 0.35 ? "high" : "medium",
                ProcessingScore = processingScore,
                Reasons = reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };
        }

        if (hasIndustrialClaim)
        {
            return new ProcessingClassificationDto
            {
                Level = scoreLevel,
                Confidence = "medium",
                ProcessingScore = processingScore,
                Reasons = ["Claim industrial detectado mesmo com lista de ingredientes parcial."]
            };
        }

        if (markerCount == 1)
        {
            return new ProcessingClassificationDto
            {
                Level = scoreLevel,
                Confidence = naturalPredominant ? "medium" : "medium",
                ProcessingScore = processingScore,
                Reasons = naturalPredominant
                    ? ["Ingrediente natural predominante com poucos aditivos detectados.", ..(markerReasons.Count > 0 ? markerReasons : ["Contém aditivo alimentar detectado."])]
                    : markerReasons.Count > 0 ? markerReasons : ["Contém aditivo alimentar detectado."]
            };
        }

        return new ProcessingClassificationDto
        {
            Level = scoreLevel,
            Confidence = naturalPredominant ? "medium" : "low",
            ProcessingScore = processingScore,
            Reasons = naturalPredominant
                ? ["Ingredientes naturais predominam na lista visível."]
                : ["Não foram detectados marcadores comuns de ultraprocessamento, mas a análise depende da lista visível."]
        };
    }

    private static int CalculateProcessingScore(
        int markerReasonCount,
        int additiveCategoryCount,
        bool hasArtificialSweetener,
        bool hasPreservative,
        bool hasEmulsifier,
        bool hasStabilizer,
        bool hasIndustrialClaim,
        bool hasAddedSugar,
        bool naturalPredominant,
        double industrialDensity)
    {
        var additiveSignalCount = Math.Max(markerReasonCount, additiveCategoryCount);
        var score = Math.Min(75, additiveSignalCount * 24);
        if (hasArtificialSweetener) score += 12;
        if (hasPreservative) score += 12;
        if (hasEmulsifier) score += 12;
        if (hasStabilizer) score += 10;
        if (industrialDensity >= 0.25) score += 18;
        if (hasIndustrialClaim) score += 10;
        if (hasAddedSugar) score += 14;
        if (naturalPredominant) score -= 12;
        return Math.Clamp(score, 0, 100);
    }

    private static string ResolveProcessingLevel(int processingScore) => processingScore switch
    {
        <= 20 => "minimally_processed",
        <= 50 => "processed",
        _ => "ultra_processed"
    };

    private static string ToProcessingReason(string term)
    {
        var normalized = IngredientTextNormalizer.Normalize(term);
        if (normalized.Contains("aroma", StringComparison.Ordinal)) return "Contém aromatizante";
        if (normalized.Contains("corante", StringComparison.Ordinal)) return "Contém corante";
        if (normalized.Contains("estabilizante", StringComparison.Ordinal)) return "Contém estabilizante";
        if (normalized.Contains("acidulante", StringComparison.Ordinal)) return "Contém acidulante";
        if (normalized.Contains("emulsificante", StringComparison.Ordinal)) return "Contém emulsificante";
        if (normalized.Contains("adocante", StringComparison.Ordinal) || normalized.Contains("sucralose", StringComparison.Ordinal) || normalized.Contains("aspartame", StringComparison.Ordinal)) return "Contém adoçante artificial";
        if (normalized.Contains("conservante", StringComparison.Ordinal) || normalized.Contains("benzoato", StringComparison.Ordinal) || normalized.Contains("sorbato", StringComparison.Ordinal)) return "Contém conservante";
        return $"Contém {term}";
    }
}

public sealed class PositiveIngredientDetector
{
    public List<PositiveIngredientDto> Detect(IReadOnlyList<IngredientNormalizedDto> normalizedIngredients)
    {
        return normalizedIngredients
            .Where(item => IngredientDictionary.PositiveIngredientCategories.Contains(item.Category, StringComparer.OrdinalIgnoreCase))
            .Select(item => new PositiveIngredientDto
            {
                Name = string.IsNullOrWhiteSpace(item.Normalized) ? item.Raw : item.Normalized,
                Category = item.Category
            })
            .GroupBy(item => IngredientTextNormalizer.Normalize(item.Name))
            .Select(group => group.First())
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public sealed class AnalysisCompletenessEvaluator
{
    public AnalysisCompletenessDto Evaluate(
        string ocrConfidence,
        int rawTextLength,
        int ingredientCount,
        int claimCount,
        IReadOnlyList<string> warnings)
    {
        var reasons = new List<string>();

        if (rawTextLength < 60)
            reasons.Add("Texto OCR insuficiente para leitura confiável.");
        else if (rawTextLength < 180 && ingredientCount == 0)
            // Only flag a short text as partial when no ingredients were detected at all.
            // Simple products (sweeteners, condiments, waters) legitimately have few ingredients
            // and should not be penalised for list length when OCR quality is good.
            reasons.Add("Lista de ingredientes possivelmente parcial ou curta.");

        if (ingredientCount == 0)
            reasons.Add("Nenhuma lista de ingredientes legível foi detectada.");

        if (warnings.Any(warning => IngredientTextNormalizer.ContainsAny(warning, ["parcial", "reflexo", "desfoc", "ilegível", "ilegiv"])))
            reasons.Add("Imagem ou leitura indica possível visibilidade parcial.");

        if (ocrConfidence == "low")
            reasons.Add("Qualidade de OCR baixa.");

        if (ingredientCount == 0 && claimCount == 0)
        {
            return new AnalysisCompletenessDto
            {
                Status = "insufficient",
                Reasons = reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };
        }

        if (ocrConfidence == "low" || reasons.Count > 0)
        {
            return new AnalysisCompletenessDto
            {
                Status = "partial",
                Reasons = reasons.Distinct(StringComparer.OrdinalIgnoreCase).DefaultIfEmpty("Análise baseada em evidência limitada.").ToList()
            };
        }

        return new AnalysisCompletenessDto
        {
            Status = "complete",
            Reasons = ["Lista de ingredientes e/ou claims legíveis o suficiente para análise."]
        };
    }
}

public sealed class OcrSemanticCleaner
{
    private static readonly IReadOnlyDictionary<string, string> CommonCorrections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["NAO CONTEM"] = "NÃO CONTÉM",
        ["NAO CONTÉM"] = "NÃO CONTÉM",
        ["CON TEM"] = "CONTÉM",
        ["CONTEM"] = "CONTÉM",
        ["GLUTEN"] = "GLÚTEN",
        ["LACT0SE"] = "LACTOSE",
        ["ACUCAR"] = "AÇÚCAR",
        ["LE1TE"] = "LEITE",
        ["OV0"] = "OVO"
    };

    public CleanedSemanticTextResult Clean(params string?[] texts)
    {
        var corrections = new List<OcrCorrectionDto>();
        var parts = texts
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => CleanOne(text!, corrections))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        var cleaned = string.Join("\n", parts)
            .Trim();

        return new CleanedSemanticTextResult(cleaned, corrections
            .GroupBy(item => $"{item.Original}|{item.Corrected}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList());
    }

    private static string CleanOne(string text, List<OcrCorrectionDto> corrections)
    {
        var cleaned = text.Replace('\r', '\n');
        cleaned = Regex.Replace(cleaned, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", " ");
        cleaned = Regex.Replace(cleaned, @"([A-ZÁ-Ú]{2,})\1{1,}", "$1", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"([A-Za-zÁ-ú])\1{3,}", "$1$1", RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"\bP\s*O\s*D\s*E\s+C\s*O\s*N\s*T\s*E\s*R\b", "PODE CONTER", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"\bN\s*[ÃA]\s*O\s+C\s*O\s*N\s*T\s*[ÉE]\s*M\b", "NÃO CONTÉM", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"\bA\s*L\s*[ÉE]\s*R\s*G\s*I\s*C\s*O\s*S\b", "ALÉRGICOS", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        foreach (var correction in CommonCorrections)
        {
            var pattern = $"\\b{Regex.Escape(correction.Key)}\\b";
            if (!Regex.IsMatch(cleaned, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                continue;

            cleaned = Regex.Replace(cleaned, pattern, correction.Value, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            corrections.Add(new OcrCorrectionDto
            {
                Original = correction.Key,
                Corrected = correction.Value,
                Confidence = "medium",
                Reason = "Limpeza semântica de OCR"
            });
        }

        cleaned = Regex.Replace(cleaned, @"\b(PAO|P0DE|POOE|P0OE)\s+(C0NTER|CONTER|C0NTE[R]?|CONTE[R]?)\b", "PODE CONTER", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"\bPODE\s+CONTER\s+([A-ZÁ-Ú\s]{2,80})(?=\.|;|\n|$)", match => CleanMayContainPhrase(match.Value), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"[|_~^`´¨]+", " ");
        cleaned = Regex.Replace(cleaned, @"\s+([,.;:])", "$1");
        cleaned = Regex.Replace(cleaned, @"[ \t]{2,}", " ");
        cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n");

        return cleaned.Trim();
    }

    private static string CleanMayContainPhrase(string value)
    {
        var normalized = IngredientTextNormalizer.Normalize(value);
        var allergens = IngredientDictionary.Allergens
            .SelectMany(entry => entry.Synonyms.Concat([entry.CanonicalName]))
            .Where(term => IngredientTextNormalizer.ContainsAny(normalized, [term]))
            .GroupBy(IngredientTextNormalizer.Normalize)
            .Select(group => group.First().ToUpperInvariant())
            .ToList();

        return allergens.Count == 0
            ? "PODE CONTER"
            : $"PODE CONTER {string.Join(", ", allergens)}";
    }
}

public sealed record CleanedSemanticTextResult(string Text, List<OcrCorrectionDto> Corrections);

public sealed class IngredientSemanticEngine
{
    public IngredientSemanticProfileDto Build(IReadOnlyList<IngredientNormalizedDto> normalizedIngredients)
    {
        var profile = new IngredientSemanticProfileDto();

        foreach (var ingredient in normalizedIngredients)
        {
            var name = string.IsNullOrWhiteSpace(ingredient.Normalized) ? ingredient.Raw : ingredient.Normalized;
            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (IsProhibited(ingredient))
                profile.ProhibitedIngredients.Add(CreateItem(name, ingredient.Category, "Ingrediente incompatível com algum perfil alimentar restritivo."));
            else if (IngredientDictionary.PositiveIngredientCategories.Contains(ingredient.Category, StringComparer.OrdinalIgnoreCase))
                profile.PositiveIngredients.Add(CreateItem(name, ingredient.Category, "Ingrediente com papel nutricional positivo identificado."));
            else if (IsControversial(ingredient))
                profile.ControversialIngredients.Add(CreateItem(name, ingredient.Category, "Ingrediente associado a atenção de consumo ou ultraprocessamento."));
            else
                profile.ToleratedIngredients.Add(CreateItem(name, ingredient.Category, "Ingrediente sem restrição semântica detectada neste fluxo."));
        }

        profile.ProhibitedIngredients = Distinct(profile.ProhibitedIngredients);
        profile.PositiveIngredients = Distinct(profile.PositiveIngredients);
        profile.ControversialIngredients = Distinct(profile.ControversialIngredients);
        profile.ToleratedIngredients = Distinct(profile.ToleratedIngredients);
        profile.EvidenceTypes = [EvidenceType.IngredientDetected];
        profile.Evidence = normalizedIngredients
            .Where(item => !string.IsNullOrWhiteSpace(item.Raw))
            .Take(10)
            .Select(item => new SemanticEvidenceDto
            {
                EvidenceType = EvidenceType.IngredientDetected,
                Type = EvidenceType.IngredientDetected.ToString(),
                Text = item.Raw,
                Confidence = item.Confidence,
                Source = "normalized_ingredient"
            })
            .ToList();

        return profile;
    }

    private static bool IsProhibited(IngredientNormalizedDto ingredient) =>
        IngredientTextNormalizer.ContainsAny(ingredient.Raw, IngredientDictionary.VeganBlockedTerms) ||
        IngredientTextNormalizer.ContainsAny(ingredient.Normalized, IngredientDictionary.VeganBlockedTerms) ||
        ingredient.Category.Contains("derivative", StringComparison.OrdinalIgnoreCase);

    private static bool IsControversial(IngredientNormalizedDto ingredient) =>
        IngredientDictionary.ControversialTerms.Any(term =>
            IngredientTextNormalizer.ContainsAny(ingredient.Raw, [term]) ||
            IngredientTextNormalizer.ContainsAny(ingredient.Normalized, [term]));

    private static IngredientSemanticItemDto CreateItem(string name, string category, string reason) =>
        new()
        {
            Name = name,
            Category = category,
            Reasons = [reason]
        };

    private static List<IngredientSemanticItemDto> Distinct(IReadOnlyList<IngredientSemanticItemDto> items) =>
        items
            .GroupBy(item => IngredientTextNormalizer.Normalize(item.Name))
            .Select(group => group.First())
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}

public sealed class OcrCorrectionEngine
{
    private static readonly IReadOnlyList<OcrCorrectionRule> Rules =
    [
        new("IVA", "UVA", "medium", ["suco", "concentrado", "nectar", "néctar", "bebida"]),
        new("ACUCAR", "AÇÚCAR", "high", ["ingrediente", "contém", "xarope", "adicionado"]),
        new("GLUTEN", "GLÚTEN", "high", ["sem", "contém", "contem", "pode conter"]),
        new("LACT0SE", "LACTOSE", "medium", ["leite", "derivado", "sem", "contém", "contem"]),
        new("LE1TE", "LEITE", "medium", ["integral", "pó", "po", "derivado", "lactose"]),
        new("C0CO", "COCO", "medium", ["leite", "óleo", "oleo", "ralado", "extrato"]),
        new("S0JA", "SOJA", "medium", ["proteína", "proteina", "lecitina", "extrato"])
    ];

    public OcrCorrectionResult Correct(string? ocrText, string? documentText)
    {
        var corrections = new List<OcrCorrectionDto>();
        var correctedOcr = CorrectText(ocrText, corrections);
        var correctedDocument = CorrectText(documentText, corrections);

        return new OcrCorrectionResult(
            correctedOcr,
            correctedDocument,
            corrections
                .GroupBy(item => $"{item.Original}|{item.Corrected}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList());
    }

    private static string? CorrectText(string? text, List<OcrCorrectionDto> corrections)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var corrected = text;
        var normalizedText = IngredientTextNormalizer.Normalize(text);

        foreach (var rule in Rules)
        {
            if (!rule.ContextTerms.Any(term => normalizedText.Contains(IngredientTextNormalizer.Normalize(term), StringComparison.OrdinalIgnoreCase)))
                continue;

            var pattern = $"\\b{Regex.Escape(rule.Original)}\\b";
            if (!Regex.IsMatch(corrected, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                continue;

            corrected = Regex.Replace(corrected, pattern, rule.Corrected, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            corrections.Add(new OcrCorrectionDto
            {
                Original = rule.Original,
                Corrected = rule.Corrected,
                Confidence = rule.Confidence,
                Reason = "Correção contextual alimentar"
            });
        }

        return corrected;
    }

    private sealed record OcrCorrectionRule(string Original, string Corrected, string Confidence, IReadOnlyList<string> ContextTerms);
}

public sealed record OcrCorrectionResult(string? CorrectedOcrText, string? CorrectedDocumentIntelligenceText, List<OcrCorrectionDto> Corrections);

public sealed class IngredientPresentationBuilder
{
    public FoodPresentationBuildResult Build(IngredientAnalysisResponse response)
    {
        var summaryCards = BuildSummaryCards(response);
        var quickInsights = BuildQuickInsights(response, summaryCards);
        var rating = BuildOverallRating(response, quickInsights);
        var presentationSummary = BuildPresentationSummary(response, summaryCards, rating);
        var quickFlags = BuildQuickFlags(summaryCards);

        return new FoodPresentationBuildResult(summaryCards, quickInsights, quickFlags, rating, presentationSummary);
    }

    public UnifiedFoodAnalysisResponse BuildUnified(IngredientAnalysisResponse response)
    {
        return new UnifiedFoodAnalysisResponse
        {
            ProductName = response.ProductName,
            Brand = response.Brand,
            SummaryCards = response.SummaryCards,
            QuickInsights = response.QuickInsights,
            QuickFlags = response.QuickFlags,
            ProcessingClassification = response.ProcessingClassification,
            IngredientContext = response.IngredientContext,
            OverallFoodRating = response.OverallFoodRating,
            PresentationSummary = response.PresentationSummary,
            OcrCorrections = response.OcrCorrections,
            IngredientConfidence = response.IngredientConfidence,
            IngredientAnalysis = new IngredientAnalysisTechnicalSnapshotDto
            {
                NormalizedIngredients = response.NormalizedIngredients,
                DietProfiles = response.DietProfiles,
                ProcessingLevel = response.ProcessingLevel,
                ProcessingClassification = response.ProcessingClassification,
                IngredientContext = response.IngredientContext,
                SemanticProfile = response.SemanticProfile,
                AssistantSummary = response.AssistantSummary,
                Diagnostics = response.Diagnostics,
                ClaimsDetected = response.ClaimsDetected,
                IngredientConfidence = response.IngredientConfidence,
                PositiveIngredients = response.PositiveIngredients,
                AllergenRisks = response.AllergenRisks,
                ReasonSources = response.ReasonSources
            }
        };
    }

    private static List<QuickFlagDto> BuildQuickFlags(IReadOnlyList<SummaryCardDto> cards) =>
        cards
            .Where(card => !string.IsNullOrWhiteSpace(card.Label))
            .Select(card => new QuickFlagDto
            {
                Type = card.Status switch
                {
                    "positive" => "positive",
                    "danger" => "danger",
                    _ => "warning"
                },
                Label = card.Label
            })
            .GroupBy(flag => flag.Label, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(6)
            .ToList();

    private static List<SummaryCardDto> BuildSummaryCards(IngredientAnalysisResponse response)
    {
        var cards = new List<SummaryCardDto>
        {
            BuildDietCard("vegan", response.DietProfiles.Vegan, "Compatível com veganos", "Compatibilidade vegana incerta", "Possui derivados de origem animal", "vegan"),
            BuildDietCard("gluten_free", response.DietProfiles.GlutenFree, "Não contém glúten", "Pode conter glúten", "Contém glúten", "gluten_free"),
            BuildDietCard("lactose_free", response.DietProfiles.LactoseFree, "Não contém lactose", "Pode conter leite/lactose", "Contém lactose", "lactose_free")
        };

        cards.Add(BuildProcessingCard(response.ProcessingLevel));

        var sugarCard = BuildSugarCard(response);
        if (sugarCard is not null)
            cards.Add(sugarCard);

        if (response.AllergenRisks.Count > 0)
        {
            var hasHighRelevantAllergen = HasHighRelevantAllergen(response);
            var hasCrossContamination = response.AllergenRisks.Any(risk => risk.RiskType is "may_contain" or "cross_contamination");
            cards.Add(new SummaryCardDto
            {
                Type = "allergen",
                Status = hasHighRelevantAllergen ? "danger" : "warning",
                Label = hasCrossContamination ? "Pode conter alergênicos" : hasHighRelevantAllergen ? "Contém alergênicos" : "Atenção a ingrediente sensível",
                Icon = "allergen"
            });
        }

        if (response.AnalysisCompleteness.Status == "partial")
            cards.Add(new SummaryCardDto { Type = "analysis_completeness", Status = "warning", Label = "Análise parcial", Icon = "info" });

        if (response.Diagnostics.OverallSemanticConfidence == "low")
            cards.Add(new SummaryCardDto { Type = "semantic_confidence", Status = "warning", Label = "Baixa confiança", Icon = "info" });

        return cards;
    }

    private static SummaryCardDto BuildDietCard(string type, DietProfileCompatibilityDto profile, string positiveLabel, string warningLabel, string dangerLabel, string icon)
    {
        var status = profile.CompatibilityStatus switch
        {
            DietCompatibilityStatuses.ConfirmedCompatible or DietCompatibilityStatuses.LikelyCompatible => "positive",
            DietCompatibilityStatuses.NotCompatible => "danger",
            DietCompatibilityStatuses.Attention or DietCompatibilityStatuses.LikelyNotCompatible => "warning",
            _ => "neutral"
        };

        return new SummaryCardDto
        {
            Type = type,
            Status = status,
            Label = status switch
            {
                "positive" => profile.CompatibilityStatus == DietCompatibilityStatuses.ConfirmedCompatible ? positiveLabel : "Provavelmente compatível",
                "danger" => dangerLabel,
                "warning" => profile.CompatibilityStatus == DietCompatibilityStatuses.LikelyNotCompatible ? warningLabel.Replace("Pode conter", "Provavelmente incompatível:") : warningLabel,
                _ => "Incerto"
            },
            Icon = status == "danger" ? "warning" : icon
        };
    }

    private static SummaryCardDto BuildProcessingCard(ProcessingLevelDto processingLevel)
    {
        return processingLevel.Value switch
        {
            "ultra_processed" => new SummaryCardDto { Type = "ultra_processed", Status = "warning", Label = "Ultraprocessado", Icon = "warning" },
            "processed" => new SummaryCardDto { Type = "processing", Status = "neutral", Label = "Processado", Icon = "processing" },
            "minimally_processed" => new SummaryCardDto { Type = "processing", Status = "positive", Label = "Pouco processado", Icon = "natural" },
            _ => new SummaryCardDto { Type = "processing", Status = "neutral", Label = "Processamento incerto", Icon = "info" }
        };
    }

    private static SummaryCardDto? BuildSugarCard(IngredientAnalysisResponse response)
    {
        if (!ContainsSugar(response))
            return null;

        return new SummaryCardDto
        {
            Type = "sugar",
            Status = response.DietProfiles.DiabeticFriendly.CompatibilityStatus is DietCompatibilityStatuses.NotCompatible or DietCompatibilityStatuses.Attention ? "danger" : "warning",
            Label = "Açúcar detectado",
            Icon = "sugar"
        };
    }

    private static List<QuickInsightDto> BuildQuickInsights(IngredientAnalysisResponse response, IReadOnlyList<SummaryCardDto> summaryCards)
    {
        var insights = new List<QuickInsightDto>();

        if (response.ProcessingLevel.Value == "ultra_processed")
            insights.Add(CreateInsight("processing", "Produto ultraprocessado", "warning", "warning"));
        else if (response.ProcessingLevel.Value == "minimally_processed")
            insights.Add(CreateInsight("processing", "Produto pouco processado", "positive", "natural"));

        if (ContainsSugar(response))
            insights.Add(CreateInsight("sugar", "Possui açúcar adicionado", "danger", "sugar"));

        if (IsPositive(response.DietProfiles.GlutenFree))
            insights.Add(CreateInsight("gluten_free", "Sem glúten", "positive", "gluten_free"));

        if (IsPositive(response.DietProfiles.Vegan))
            insights.Add(CreateInsight("vegan", "Provavelmente vegano", "positive", "vegan"));
        else if (response.DietProfiles.Vegan.CompatibilityStatus == DietCompatibilityStatuses.Uncertain)
            insights.Add(CreateInsight("vegan", "Compatibilidade vegana incerta", "warning", "vegan"));

        if (response.PositiveIngredients.Count > 0)
            insights.Add(CreateInsight("positive_ingredient", $"Contém {response.PositiveIngredients[0].Name}", "positive", "positive"));

        foreach (var allergen in response.AllergenRisks.Take(2))
        {
            var severity = allergen.AllergenSeverity.RegulatoryLevel == "high" && allergen.RiskType == "contains" ? "danger" : "warning";
            insights.Add(CreateInsight("allergen", allergen.RiskType == "contains" ? $"Contém {allergen.Name}" : $"Pode conter {allergen.Name}", severity, "allergen"));
        }

        foreach (var claim in response.ClaimsDetected.Where(claim => claim.Type.Contains("vitamin", StringComparison.OrdinalIgnoreCase)).Take(1))
            insights.Add(CreateInsight("claim", "Contém vitaminas adicionadas", "info", "vitamin"));

        if (insights.Count == 0 && summaryCards.Count > 0)
            insights.Add(CreateInsight("general", summaryCards[0].Label, summaryCards[0].Status, summaryCards[0].Icon));

        return insights
            .GroupBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(6)
            .ToList();
    }

    private static OverallFoodRatingDto BuildOverallRating(IngredientAnalysisResponse response, IReadOnlyList<QuickInsightDto> quickInsights)
    {
        var reasons = new List<string>();

        if (response.AnalysisCompleteness.Status == "insufficient")
        {
            return new OverallFoodRatingDto
            {
                Level = "unknown",
                Label = "Análise inconclusiva",
                Reasons = response.AnalysisCompleteness.Reasons.DefaultIfEmpty("Pouca informação legível.").Take(3).ToList()
            };
        }

        if (response.ProcessingLevel.Value == "ultra_processed")
            reasons.Add("Produto ultraprocessado");

        if (ContainsSugar(response))
            reasons.Add("Açúcar detectado");

        var hasArtificialSweetener = response.NormalizedIngredients.Any(item => item.Category.Equals("artificial_sweetener", StringComparison.OrdinalIgnoreCase)) ||
            response.IngredientsDetected.Any(item => IngredientTextNormalizer.ContainsAny(item, IngredientDictionary.ArtificialSweetenerTerms));
        if (hasArtificialSweetener && !ContainsSugar(response))
            reasons.Add("Adoçante artificial detectado");

        var hasHighRelevantAllergen = HasHighRelevantAllergen(response);
        if (hasHighRelevantAllergen)
            reasons.Add("Alergênico relevante detectado");
        else if (response.AllergenRisks.Count > 0)
            reasons.Add("Ingrediente sensível de baixo risco detectado");

        if (response.SemanticProfile.ControversialIngredients.Count > 0)
            reasons.Add("Ingredientes que pedem atenção");

        if (response.ProcessingLevel.Value == "minimally_processed" && response.AllergenRisks.Count == 0 && !ContainsSugar(response))
            reasons.Add("Poucos sinais de alerta detectados");

        var severeProblemCount = 0;
        if (hasHighRelevantAllergen) severeProblemCount++;
        if (response.ProcessingLevel.Value == "ultra_processed") severeProblemCount++;
        if (ContainsSugar(response)) severeProblemCount++;
        if (response.SemanticProfile.ControversialIngredients.Count >= 3) severeProblemCount++;

        var level = severeProblemCount >= 3
            ? "avoid"
            : reasons.Any(reason => reason.Contains("ultraprocessado", StringComparison.OrdinalIgnoreCase) || reason.Contains("Açúcar", StringComparison.OrdinalIgnoreCase) || reason.Contains("Adoçante", StringComparison.OrdinalIgnoreCase))
                ? "attention"
                : response.ProcessingLevel.Value == "minimally_processed" && IsPositive(response.DietProfiles.GlutenFree)
                    ? "good"
                    : "unknown";

        return new OverallFoodRatingDto
        {
            Level = level,
            Label = level switch
            {
                "excellent" => "Ótima escolha",
                "good" => "Boa opção",
                "attention" => "Consumir com moderação",
                "avoid" => "Evitar para alguns perfis",
                _ => quickInsights.Count > 0 ? "Ver pontos principais" : "Análise inconclusiva"
            },
            Reasons = reasons.DefaultIfEmpty("Resultado baseado nos ingredientes visíveis.").Take(4).ToList()
        };
    }

    private static PresentationSummaryDto BuildPresentationSummary(IngredientAnalysisResponse response, IReadOnlyList<SummaryCardDto> cards, OverallFoodRatingDto rating)
    {
        var title = rating.Level switch
        {
            "avoid" => "Produto com alertas importantes",
            "attention" when response.ProcessingLevel.Value == "ultra_processed" && ContainsSugar(response) => "Produto ultraprocessado com açúcar",
            "attention" when response.ProcessingLevel.Value == "ultra_processed" => "Produto ultraprocessado",
            "good" => "Produto com boa compatibilidade",
            _ => "Análise de ingredientes concluída"
        };

        var positiveLabels = cards
            .Where(card => card.Status == "positive")
            .Select(card => card.Label.ToLowerInvariant())
            .Take(2)
            .ToList();

        return new PresentationSummaryDto
        {
            Title = title,
            Subtitle = positiveLabels.Count > 0
                ? string.Join(" e ", positiveLabels)
                : "Veja os principais alertas e compatibilidades.",
            Highlight = rating.Label
        };
    }

    private static QuickInsightDto CreateInsight(string type, string text, string severity, string icon) =>
        new()
        {
            Type = type,
            Text = text,
            Severity = severity,
            Icon = icon
        };

    private static bool IsPositive(DietProfileCompatibilityDto profile) =>
        profile.CompatibilityStatus is DietCompatibilityStatuses.ConfirmedCompatible or DietCompatibilityStatuses.LikelyCompatible;

    private static bool ContainsSugar(IngredientAnalysisResponse response)
    {
        static bool HasSugar(string value) => IngredientClassifier.IsRealSugarIngredient(value) && !IngredientClassifier.IsContextualSugarReference(value);

        return response.IngredientsDetected.Any(HasSugar) ||
            response.NormalizedIngredients.Any(item => HasSugar(item.Raw) || HasSugar(item.Normalized) || (item.Category.Contains("sugar", StringComparison.OrdinalIgnoreCase) && !IngredientClassifier.IsContextualSugarReference(item.Raw)));
    }

    private static bool HasHighRelevantAllergen(IngredientAnalysisResponse response) =>
        response.AllergenRisks.Any(risk =>
            risk.RiskType == "contains" &&
            risk.AllergenSeverity.RegulatoryLevel == "high" &&
            risk.AllergenSeverity.RiskWeight >= 70);
}

public sealed record FoodPresentationBuildResult(
    List<SummaryCardDto> SummaryCards,
    List<QuickInsightDto> QuickInsights,
    List<QuickFlagDto> QuickFlags,
    OverallFoodRatingDto OverallFoodRating,
    PresentationSummaryDto PresentationSummary);
