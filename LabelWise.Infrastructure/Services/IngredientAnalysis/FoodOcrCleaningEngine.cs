using System.Text;
using System.Text.RegularExpressions;
using LabelWise.Application.DTOs.IngredientAnalysis;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services.IngredientAnalysis;

public sealed record FoodOcrCleaningResult(
    string Text,
    StructuredTextDocument StructuredText,
    List<OcrCorrectionDto> Corrections,
    List<string> Traces);

public sealed class FoodOcrCleaningEngine(
    OcrCleaningService ocrCleaningService,
    SemanticReconstructionService semanticReconstructionService,
    StructuredTextLayer structuredTextLayer,
    SemanticDeduplicationEngine deduplicationEngine,
    ILogger<FoodOcrCleaningEngine> logger)
{
    public FoodOcrCleaningResult Clean(params string?[] texts)
    {
        var traces = new List<string> { "raw_ocr_received" };
        var cleaned = ocrCleaningService.Clean(texts);
        var deterministicText = ApplyDeterministicNoiseFilters(cleaned.Text, traces);
        var reconstructed = semanticReconstructionService.Reconstruct(deterministicText);
        var finalText = deduplicationEngine.DeduplicateLines(reconstructed.Text);
        var structured = structuredTextLayer.Build(finalText);
        var corrections = cleaned.Corrections
            .Concat(reconstructed.Corrections)
            .GroupBy(item => $"{item.Original}|{item.Corrected}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.Original, StringComparer.OrdinalIgnoreCase)
            .ToList();

        traces.Add($"noise_cleaning_applied:{cleaned.Text.Length}->{deterministicText.Length}");
        traces.Add($"semantic_blocks:{structured.Blocks.Count}");
        logger.LogDebug("[FoodOcrCleaningEngine] TextLength={TextLength}; Blocks={Blocks}; Corrections={Corrections}", finalText.Length, structured.Blocks.Count, corrections.Count);

        return new FoodOcrCleaningResult(finalText, structured, corrections, traces);
    }

    private static string ApplyDeterministicNoiseFilters(string value, List<string> traces)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var corrected = value.Normalize(NormalizationForm.FormC);
        corrected = Regex.Replace(corrected, @"\bNINGI{1,2}EE\s+DERIVADOS\s+DE\s+LEITE\b", "DERIVADOS DE LEITE", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        corrected = Regex.Replace(corrected, @"\bNINGIIEE\b", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        corrected = Regex.Replace(corrected, @"\b(?:QR\s*CODE|QRCODE|SAC|TEL(?:EFONE)?|WHATSAPP|INSTAGRAM|FACEBOOK|HTTPS?://)\b[^\n.;]*", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        corrected = Regex.Replace(corrected, @"\bwww\s*\.\s*[^\s.;]+(?:\s*\.\s*[^\s.;]+)?", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        corrected = Regex.Replace(corrected, @"\b(?:LOTE|VALIDADE|FAB(?:RICA[CÇ][AÃ]O)?|VENCIMENTO)\b\s*[:\-]?\s*[A-Z0-9/.-]+", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        corrected = Regex.Replace(corrected, @"\b(?:FABRICADO|PRODUZIDO|DISTRIBU[IÍ]DO|ENVASILHADO)\s+POR\b[^\n.;]*", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        corrected = Regex.Replace(corrected, @"\bCNPJ\b\s*[:\-]?\s*[\d./-]+", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        corrected = Regex.Replace(corrected, @"[ \t]{2,}", " ", RegexOptions.CultureInvariant);
        corrected = Regex.Replace(corrected, @"\n{3,}", "\n\n", RegexOptions.CultureInvariant).Trim();
        traces.Add("hard_noise_filters:qrcode_sac_manufacturer_lot_validity_urls");
        return corrected;
    }
}

public sealed record FoodIngredientToken(string Text, string Source, string DetectionType, string Confidence);

public sealed class FoodIngredientTokenizer
{
    private static readonly Regex TokenSeparatorRegex = new(@"[,;•|/]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ConjunctionRegex = new(@"\s+e\s+", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public List<FoodIngredientToken> Tokenize(string? value, string source = "IngredientBlock")
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        var cleaned = RemoveClaimAndObservationFragments(value);
        return TokenSeparatorRegex.Split(cleaned)
            .SelectMany(SplitConjunctionsWhenFoodList)
            .Select(NormalizeToken)
            .Select(RemoveIngredientCategoryPrefix)
            .Where(IsCandidateToken)
            .Select(token => new FoodIngredientToken(token, source, "confirmed", ResolveConfidence(token)))
            .GroupBy(token => IngredientTextNormalizer.Normalize(token.Text), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(token => token.Text, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string RemoveClaimAndObservationFragments(string value)
    {
        var cleaned = Regex.Replace(value, @"\b(?:INGREDIENTES?|INGR\.?|COMPOSI[CÇ][AÃ]O)\s*[:\-]?", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"\b(?:AL[ÉE]RGICOS?|ALERG[ÊE]NICOS?|N[ÃA]O\s+CONT[ÉE]M|PODE\s+CONTER|CONT[ÉE]M\s+(?:GL[ÚU]TEN|LEITE|SOJA|OVOS?|LACTOSE))\b.*$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        return cleaned;
    }

    private static IEnumerable<string> SplitConjunctionsWhenFoodList(string segment)
    {
        var trimmed = segment.Trim(' ', ',', ';', '.', ':');
        if (string.IsNullOrWhiteSpace(trimmed))
            yield break;

        var parts = ConjunctionRegex.Split(trimmed)
            .Select(part => part.Trim(' ', ',', ';', '.', ':'))
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();

        if (parts.Count == 2 && IsGenericTrailingObservation(parts[1]))
        {
            yield return parts[0];
            yield break;
        }

        if (parts.Count <= 1 || IsProtectedExpression(trimmed) || parts.Any(IsGenericTrailingObservation))
        {
            yield return trimmed;
            yield break;
        }

        if (parts.All(HasKnownFoodSignal))
        {
            foreach (var part in parts)
                yield return part;
            yield break;
        }

        yield return trimmed;
    }

    private static bool IsProtectedExpression(string value)
    {
        var normalized = IngredientTextNormalizer.Normalize(value);
        return normalized.Contains("mono e diglicerideos", StringComparison.Ordinal) ||
            normalized.Contains("macro e micronutrientes", StringComparison.Ordinal) ||
            normalized.Contains("aroma e sabor", StringComparison.Ordinal);
    }

    private static bool IsGenericTrailingObservation(string value)
    {
        var normalized = IngredientTextNormalizer.Normalize(value);
        return normalized is "derivados" or "derivado" or "tracos" or "outros";
    }

    private static string NormalizeToken(string value) =>
        Regex.Replace(value, @"\s+", " ", RegexOptions.CultureInvariant).Trim(' ', ',', ';', '.', ':', '-', '–', '—');

    private static string RemoveIngredientCategoryPrefix(string value) =>
        Regex.Replace(
            value,
            @"^(?:edulcorantes?|adoçantes?|adocantes?|conservantes?|conservadores?|acidulantes?|aromatizantes?|corantes?|estabilizantes?|emulsificantes?)\s*[:：-]\s*",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim(' ', ',', ';', '.', ':', '-', '–', '—');

    private static bool IsCandidateToken(string token)
    {
        var normalized = IngredientTextNormalizer.Normalize(token);
        if (normalized.Length < 3) return false;
        if (Regex.IsMatch(normalized, @"^(?:derivados?|tracos?|pode|contem|nao contem)$", RegexOptions.CultureInvariant)) return false;
        if (Regex.IsMatch(normalized, @"\b(?:sac|qrcode|cnpj|validade|lote|fabricado|produzido|www|instagram|facebook)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return false;
        if (Regex.IsMatch(normalized, @"^\d+[,.]?\d*\s*(?:g|mg|ml|kcal|kj)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return false;
        return true;
    }

    private static string ResolveConfidence(string token) => HasKnownFoodSignal(token) ? "high" : "medium";

    private static bool HasKnownFoodSignal(string value)
    {
        var normalized = IngredientTextNormalizer.Normalize(value);
        return IngredientDictionary.IngredientNormalization.Concat(IngredientDictionary.Allergens)
            .Any(entry => entry.Synonyms.Concat([entry.CanonicalName])
                .Any(term => normalized == IngredientTextNormalizer.Normalize(term) || normalized.Contains(IngredientTextNormalizer.Normalize(term), StringComparison.Ordinal)));
    }
}

public enum FoodEntityKind
{
    Ingredient,
    Allergen,
    Claim,
    Additive,
    Preservative,
    Noise
}

public sealed record FoodEntityValidationResult(FoodEntityKind Kind, string Category, string Confidence, string Reason);

public sealed class FoodEntityValidator
{
    public FoodEntityValidationResult Validate(string value)
    {
        var normalized = IngredientTextNormalizer.Normalize(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return new FoodEntityValidationResult(FoodEntityKind.Noise, "noise", "low", "empty");

        if (IsNoise(normalized))
            return new FoodEntityValidationResult(FoodEntityKind.Noise, "noise", "high", "non_food_noise");

        if (ClaimContextValidator.IsValidRegulatoryClaim(value))
            return new FoodEntityValidationResult(FoodEntityKind.Claim, "regulatory_claim", "high", "official_claim_pattern");

        var allergen = IngredientDictionary.Allergens.FirstOrDefault(entry => IngredientTextNormalizer.ContainsAny(value, entry.Synonyms.Concat([entry.CanonicalName])));
        if (allergen is not null)
            return new FoodEntityValidationResult(FoodEntityKind.Allergen, allergen.Category, "high", "allergen_dictionary");

        var ingredient = IngredientDictionary.IngredientNormalization.FirstOrDefault(entry => IngredientTextNormalizer.ContainsAny(value, entry.Synonyms.Concat([entry.CanonicalName])));
        if (ingredient is not null)
        {
            var kind = ingredient.Category switch
            {
                "preservative" => FoodEntityKind.Preservative,
                "acidulant" or "colorant" or "emulsifier" or "flavoring" or "stabilizer" or "antioxidant" or "artificial_sweetener" => FoodEntityKind.Additive,
                _ => FoodEntityKind.Ingredient
            };
            return new FoodEntityValidationResult(kind, ingredient.Category, "high", "ingredient_dictionary");
        }

        if (Regex.IsMatch(value, @"^[\p{L}\s-]{3,60}$", RegexOptions.CultureInvariant))
            return new FoodEntityValidationResult(FoodEntityKind.Ingredient, "unknown", "medium", "food_token_shape");

        return new FoodEntityValidationResult(FoodEntityKind.Noise, "noise", "low", "low_semantic_score");
    }

    private static bool IsNoise(string normalized) =>
        Regex.IsMatch(normalized, @"\b(?:sac|qrcode|telefone|whatsapp|instagram|facebook|www|http|cnpj|lote|validade|fabricado|produzido|distribuido)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
        Regex.IsMatch(normalized, @"^\d+(?:[,.]\d+)?\s*(?:g|mg|ml|kcal|kj)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
        IngredientTextNormalizer.ContainsAny(normalized, IngredientDictionary.NutritionFactTerms);
}

public sealed class FoodCanonicalizationEngine
{
    private static readonly IReadOnlyDictionary<string, string> CanonicalMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["proteina lactea"] = "dairy_protein",
        ["proteina do leite"] = "dairy_protein",
        ["castanha de caju"] = "cashew",
        ["castanha-de-caju"] = "cashew",
        ["caju"] = "cashew",
        ["acido ascorbico"] = "vitamin_c",
        ["ascorbico"] = "vitamin_c",
        ["gordura vegetal interesterificada"] = "processed_fat",
        ["gordura interesterificada"] = "processed_fat"
    };

    public string Canonicalize(string value)
    {
        var normalized = IngredientTextNormalizer.Normalize(value.Replace('-', ' '));
        foreach (var item in CanonicalMap)
        {
            if (normalized.Contains(item.Key, StringComparison.Ordinal))
                return item.Value;
        }

        var dictionaryMatch = IngredientDictionary.IngredientNormalization.Concat(IngredientDictionary.Allergens)
            .FirstOrDefault(entry => entry.Synonyms.Concat([entry.CanonicalName])
                .Any(term => normalized.Contains(IngredientTextNormalizer.Normalize(term), StringComparison.Ordinal)));

        if (dictionaryMatch is not null)
            return ToCanonicalKey(dictionaryMatch.CanonicalName);

        return ToCanonicalKey(value);
    }

    private static string ToCanonicalKey(string value)
    {
        var normalized = IngredientTextNormalizer.Normalize(value.Replace('-', ' '));
        normalized = Regex.Replace(normalized, @"[^a-z0-9]+", "_", RegexOptions.CultureInvariant).Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
    }
}

public sealed class SemanticDeduplicationEngine
{
    public List<T> DeduplicateBy<T>(IEnumerable<T> values, Func<T, string> keySelector) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(keySelector(value)))
            .GroupBy(value => IngredientTextNormalizer.Normalize(keySelector(value)), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(item => keySelector(item).Length).First())
            .OrderBy(value => keySelector(value), StringComparer.OrdinalIgnoreCase)
            .ToList();

    public string DeduplicateLines(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return string.Join("\n", value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .GroupBy(IngredientTextNormalizer.Normalize, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(line => line, StringComparer.OrdinalIgnoreCase));
    }
}

public sealed class FoodSemanticFinalizationEngine(
    FoodIngredientTokenizer tokenizer,
    FoodEntityValidator validator,
    FoodCanonicalizationEngine canonicalizationEngine,
    SemanticDeduplicationEngine deduplicationEngine,
    ILogger<FoodSemanticFinalizationEngine> logger)
{
    public void Apply(IngredientAnalysisResponse response, StructuredTextDocument structuredText, string rawOcr, IReadOnlyList<string> parsingTraces)
    {
        response.StructuredFoodEntities = BuildEntities(response, structuredText);
        response.StructuredFoodEntities = deduplicationEngine.DeduplicateBy(response.StructuredFoodEntities, entity => entity.CanonicalName);
        response.IngredientsDetected = deduplicationEngine.DeduplicateBy(response.IngredientsDetected, value => value);
        response.Claims = deduplicationEngine.DeduplicateBy(response.Claims, value => value);
        response.ClaimsDetected = deduplicationEngine.DeduplicateBy(response.ClaimsDetected, claim => claim.Text);
        response.AllergenRisks = deduplicationEngine.DeduplicateBy(response.AllergenRisks, risk => $"{risk.Name}:{risk.RiskType}");
        response.UnifiedSemanticState = BuildUnifiedState(response);
        ApplyFinalConsistency(response);
        BuildPayloads(response, rawOcr, parsingTraces);
        //ApplyHardFilters(response);
        logger.LogDebug("[FoodSemanticFinalization] Entities={Entities}; Ingredients={Ingredients}; Trust={Trust}", response.StructuredFoodEntities.Count, response.IngredientsDetected.Count, response.Trust.AnalysisTrustScore);
    }

    private List<StructuredFoodEntityDto> BuildEntities(IngredientAnalysisResponse response, StructuredTextDocument structuredText)
    {
        var result = new List<StructuredFoodEntityDto>();
        foreach (var ingredient in response.IngredientsDetected)
            result.Add(ToEntity(ingredient, "IngredientBlock"));

        foreach (var block in structuredText.BlocksOfType("IngredientBlock"))
        {
            foreach (var token in tokenizer.Tokenize(block.Text, block.Type))
                result.Add(ToEntity(token.Text, block.Type, token.DetectionType, token.Confidence));
        }

        return result
            .Where(entity => entity.Category != "noise")
            .OrderBy(entity => entity.CanonicalName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private StructuredFoodEntityDto ToEntity(string value, string sourceBlock, string detectionType = "confirmed", string? confidence = null)
    {
        var validation = validator.Validate(value);
        var canonical = canonicalizationEngine.Canonicalize(value);
        var relatedAllergens = ResolveRelatedAllergens(value).ToList();
        var category = validation.Category == "unknown" ? ResolveNormalizedCategory(canonical) : validation.Category;

        return new StructuredFoodEntityDto
        {
            CanonicalName = canonical,
            OriginalText = value,
            Category = category,
            Confidence = confidence ?? validation.Confidence,
            SourceBlock = sourceBlock,
            DetectionType = validation.Kind == FoodEntityKind.Noise ? "rejected" : detectionType,
            IndustrializationImpact = ResolveIndustrializationImpact(category),
            AllergenProfile = new FoodEntityAllergenProfileDto
            {
                IsAllergen = validation.Kind == FoodEntityKind.Allergen || relatedAllergens.Count > 0,
                RiskType = validation.Kind == FoodEntityKind.Allergen ? "contains" : "none",
                RelatedAllergens = relatedAllergens
            },
            SemanticGroup = ResolveSemanticGroup(category, validation.Kind)
        };
    }

    private UnifiedSemanticStateDto BuildUnifiedState(IngredientAnalysisResponse response)
    {
        var confirmedAllergens = response.AllergenRisks
            .Where(risk => risk.RiskType == "contains")
            .Select(risk => risk.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var crossContamination = response.AllergenRisks
            .Where(risk => risk.RiskType is "may_contain" or "cross_contamination")
            .Select(risk => risk.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new UnifiedSemanticStateDto
        {
            Entities = response.StructuredFoodEntities.ToList(),
            Ingredients = response.IngredientsDetected.ToList(),
            Claims = response.Claims.ToList(),
            ConfirmedAllergens = confirmedAllergens,
            CrossContaminationRisks = crossContamination,
            ProcessingClassification = response.ProcessingClassification,
            DietProfiles = response.DietProfiles,
            TrustLevel = response.Trust.TrustLevel,
            TrustScore = response.Trust.AnalysisTrustScore,
            ConsistencyWarnings = ValidateContradictions(response, confirmedAllergens).ToList()
        };
    }

    private static void ApplyFinalConsistency(IngredientAnalysisResponse response)
    {
        response.ProcessingLevel.Value = response.ProcessingClassification.Level;
        response.ProcessingLevel.Confidence = response.ProcessingClassification.Confidence;
        response.ProcessingLevel.ProcessingScore = response.ProcessingClassification.ProcessingScore;
        response.ProcessingLevel.Reasons = response.ProcessingClassification.Reasons.ToList();
        response.ProcessingAnalysis.Level = response.ProcessingClassification.Level;
        response.ProcessingAnalysis.Confidence = response.ProcessingClassification.Confidence;
        response.ProcessingAnalysis.ProcessingScore = response.ProcessingClassification.ProcessingScore;

        foreach (var warning in response.UnifiedSemanticState.ConsistencyWarnings)
        {
            if (!response.Warnings.Contains(warning, StringComparer.OrdinalIgnoreCase))
                response.Warnings.Add(warning);
        }
    }

    private static IEnumerable<string> ValidateContradictions(IngredientAnalysisResponse response, IReadOnlyList<string> confirmedAllergens)
    {
        var claims = string.Join(" ", response.Claims.Concat(response.ClaimsDetected.Select(claim => claim.Text)));
        if (IngredientTextNormalizer.ContainsAny(claims, ["não contém glúten", "nao contem gluten", "sem glúten", "sem gluten"]) &&
            confirmedAllergens.Any(allergen => IngredientTextNormalizer.ContainsAny(allergen, ["glúten", "gluten", "trigo"])))
            yield return "Conflito final: claim sem glúten contradiz alergênico/ingrediente com glúten.";

        if (response.DietProfiles.Vegan.CompatibilityStatus is DietCompatibilityStatuses.ConfirmedCompatible or DietCompatibilityStatuses.LikelyCompatible &&
            confirmedAllergens.Any(allergen => IngredientTextNormalizer.ContainsAny(allergen, ["leite", "lactose", "ovo", "mel"])))
            yield return "Conflito final: perfil vegano contradiz ingrediente/alergênico de origem animal.";

        if (response.ProcessingClassification.Level == "ultra_processed" && response.OverallFoodRating.Level == "healthy")
            yield return "Conflito final: saudável não deve contradizer ultraprocessamento alto.";
    }

    private void BuildPayloads(IngredientAnalysisResponse response, string rawOcr, IReadOnlyList<string> parsingTraces)
    {
        response.PublicResponse = new FoodPublicResponseDto
        {
            Analysis = response.UnifiedFoodAnalysis,
            Compatibility = response.DietProfiles,
            Alerts = response.CriticalAlerts.ToList(),
            Ingredients = response.StructuredFoodEntities.ToList(),
            Nutrition = response.IngredientContext,
            Trust = response.Trust,
            Summary = response.PresentationSummary
        };

        response.DebugResponse = new FoodDebugResponseDto
        {
            RawOcr = rawOcr,
            OcrCorrections = response.OcrCorrections.ToList(),
            Blocks = response.StructuredTextBlocks.ToList(),
            SemanticLogs = response.UnifiedSemanticState.ConsistencyWarnings.ToList(),
            ConflictLogs = response.Warnings.Where(warning => IngredientTextNormalizer.ContainsAny(warning, ["conflito", "contradiz", "inconsist"])).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ParsingTraces = parsingTraces.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            IngredientLineage = response.StructuredFoodEntities.Select(entity => $"{entity.OriginalText}->{entity.CanonicalName}:{entity.SourceBlock}:{entity.Confidence}").ToList(),
            DecisionLineage = response.ReasonSources.Concat(response.ProcessingClassification.Reasons).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private static void ApplyHardFilters(IngredientAnalysisResponse response)
    {
        if (response.Trust.AnalysisTrustScore >= 50)
            return;

        response.ProductionSafeModeApplied = true;
        response.QuickInsights.Clear();
        response.AssistantSummary = new AssistantSummaryDto
        {
            Text = "Leitura insuficiente para exibir conclusões com segurança.",
            Confidence = "low",
            Warnings = ["Faça uma nova foto mais nítida do rótulo."]
        };
        response.CriticalAlerts.Clear();
        response.IngredientsDetected.Clear();
        response.NormalizedIngredients.Clear();
        response.IngredientConfidence.Clear();
        response.StructuredFoodEntities.Clear();
        response.PublicResponse.Ingredients.Clear();
        response.PublicResponse.Alerts.Clear();
        response.PublicResponse.Summary = response.PresentationSummary;
        response.Warnings.Add("Hard filter aplicado: trust abaixo de 50; dados alimentares mantidos apenas no debug.");
    }

    private static IEnumerable<string> ResolveRelatedAllergens(string value) =>
        IngredientDictionary.Allergens
            .Where(entry => IngredientTextNormalizer.ContainsAny(value, entry.Synonyms.Concat([entry.CanonicalName])))
            .Select(entry => entry.CanonicalName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);

    private static string ResolveNormalizedCategory(string canonical) => canonical switch
    {
        "dairy_protein" => "milk_derivative",
        "cashew" => "tree_nut",
        "vitamin_c" => "vitamin",
        "processed_fat" => "processed_fat",
        _ => "unknown"
    };

    private static string ResolveIndustrializationImpact(string category)
    {
        if (IngredientDictionary.UltraProcessingCategories.Contains(category, StringComparer.OrdinalIgnoreCase) || category is "processed_fat" or "hydrogenated_fat")
            return "high";
        if (category is "vitamin" or "mineral" or "whole_grain" or "base")
            return "low";
        return "neutral";
    }

    private static string ResolveSemanticGroup(string category, FoodEntityKind kind) => kind switch
    {
        FoodEntityKind.Allergen => "allergen",
        FoodEntityKind.Claim => "claim",
        FoodEntityKind.Additive or FoodEntityKind.Preservative => "industrial_additive",
        _ when IngredientDictionary.UltraProcessingCategories.Contains(category, StringComparer.OrdinalIgnoreCase) => "industrial_additive",
        _ => "ingredient"
    };
}
