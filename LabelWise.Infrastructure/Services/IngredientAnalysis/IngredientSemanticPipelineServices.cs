using System.Text;
using System.Text.RegularExpressions;
using LabelWise.Application.DTOs.IngredientAnalysis;
using LabelWise.Application.Models.IngredientAnalysis;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services.IngredientAnalysis;

public sealed class OcrCleaningService(ILogger<OcrCleaningService> logger)
{
    private static readonly IReadOnlyDictionary<string, string> CommonCorrections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["NAO CONTEM"] = "NÃO CONTÉM",
        ["NAO CONTÉM"] = "NÃO CONTÉM",
        ["CONTEM"] = "CONTÉM",
        ["GLUTEN"] = "GLÚTEN",
        ["ACUCAR"] = "AÇÚCAR",
        ["LACT0SE"] = "LACTOSE",
        ["LE1TE"] = "LEITE",
        ["GITEE DERIVADOS DE LEITE"] = "DERIVADOS DE LEITE",
        ["AVEIALAVELAS"] = "AVEIA AVELÃS",
        ["AVEIAAVELAS"] = "AVEIA AVELÃS",
        ["AVEIALAVELÃS"] = "AVEIA AVELÃS"
    };

    public CleanedSemanticTextResult Clean(params string?[] texts)
    {
        var corrections = new List<OcrCorrectionDto>();
        var blocks = texts
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => CleanOne(text!, corrections))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var cleaned = string.Join("\n", blocks).Trim();
        logger.LogDebug("[IngredientAnalysis.OcrCleaning] Blocks={BlockCount}; Corrections={CorrectionCount}", blocks.Count, corrections.Count);

        return new CleanedSemanticTextResult(cleaned, corrections
            .GroupBy(item => $"{item.Original}|{item.Corrected}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList());
    }

    private static string CleanOne(string text, List<OcrCorrectionDto> corrections)
    {
        var cleaned = text.Normalize(NormalizationForm.FormC).Replace('\r', '\n');
        cleaned = Regex.Replace(cleaned, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", " ");
        cleaned = Regex.Replace(cleaned, @"[|_~^`´¨•]+", " ");
        cleaned = Regex.Replace(cleaned, @"(?<=\p{L})[\-–—](?=\p{L})", string.Empty);
        cleaned = Regex.Replace(cleaned, @"\bP\s*O\s*D\s*E\s+C\s*O\s*N\s*T\s*E\s*R\b", "PODE CONTER", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"\bN\s*[ÃA]\s*O\s+C\s*O\s*N\s*T\s*[ÉE]\s*M\b", "NÃO CONTÉM", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        // Match ALÉRGICOS with various OCR corruptions: ALERĢICOS, ALERGICOS, ALERGÊNICOS, etc.
        cleaned = Regex.Replace(cleaned, @"\bA\s*L\s*[ÉEĢĘG]\s*R\s*G\s*[ÊEĘĢ]\s*N?\s*I\s*C\s*O\s*S\b", "ALÉRGICOS", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"\b(P0DE|POOE|P0OE)\s+(C0NTER|CONTE[R]?)\b", "PODE CONTER", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"\b(CON\s+TEM|C0NTEM|C0NTÉM)\b", "CONTÉM", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"\b([\p{L}]{2,})\b\s+\b\1\b", "$1", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"([\p{L}])\1{3,}", "$1$1", RegexOptions.CultureInvariant);
        cleaned = RemoveNonFoodNoise(cleaned);

        foreach (var correction in CommonCorrections)
        {
            var pattern = $"\b{Regex.Escape(correction.Key)}\b";
            if (!Regex.IsMatch(cleaned, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                continue;

            cleaned = Regex.Replace(cleaned, pattern, correction.Value, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            corrections.Add(new OcrCorrectionDto
            {
                Original = correction.Key,
                Corrected = correction.Value,
                Confidence = "medium",
                Reason = "Limpeza OCR semântica"
            });
        }

        cleaned = Regex.Replace(cleaned, @"\s+([,.;:])", "$1");
        cleaned = Regex.Replace(cleaned, @"([,.;:])(?=\p{L})", "$1 ");
        cleaned = Regex.Replace(cleaned, @"[ \t]{2,}", " ");
        cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n");

        return cleaned.Trim();
    }

    private static string RemoveNonFoodNoise(string value)
    {
        var lines = value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !IsNonFoodNoise(line))
            .ToList();

        return string.Join("\n", lines);
    }

    private static bool IsNonFoodNoise(string line)
    {
        var normalized = IngredientTextNormalizer.Normalize(line);
        if (normalized.Length < 3) return true;
        if (Regex.IsMatch(normalized, @"^(qr|qrcode|codigo qr|sac|www\.|http|instagram|facebook|whatsapp)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return true;
        if (Regex.IsMatch(normalized, @"\b(lote|validade|fabricacao|fab\.?|vencimento)\b\s*[:\d]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return true;
        if (IngredientTextNormalizer.ContainsAny(normalized, ["distribuido por", "fabricado por", "produzido por", "cnpj", "industria brasileira", "atendimento ao consumidor", "codigo de barras"])) return true;
        // Sugestão de uso, modo de preparo e texto promocional são ruído puro no contexto de ingredientes
        if (Regex.IsMatch(normalized, @"\b(?:sugestao de uso|modo de preparo|forma de preparo|como preparar|basta adicionar|para sua preparacao|experimente tambem|tambem disponivel|caldo de|caldo|pronto para o consumo|totalmente natural|feito basicamente)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return true;
        return false;
    }
}

public sealed class SemanticReconstructionService(ILogger<SemanticReconstructionService> logger)
{
    public CleanedSemanticTextResult Reconstruct(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new CleanedSemanticTextResult(string.Empty, []);

        var corrections = new List<OcrCorrectionDto>();
        var reconstructed = text.Trim();
        reconstructed = NormalizeRegulatoryHeaders(reconstructed);
        reconstructed = Regex.Replace(reconstructed, @"\s+([,.;:])", "$1");
        reconstructed = Regex.Replace(reconstructed, @"[ \t]{2,}", " ");

        logger.LogDebug("[IngredientAnalysis.SemanticReconstruction] Corrections={CorrectionCount}; TextLength={Length}", corrections.Count, reconstructed.Length);
        return new CleanedSemanticTextResult(reconstructed, corrections);
    }

    private static string NormalizeRegulatoryHeaders(string text)
    {
        var value = Regex.Replace(text, @"\b(ALERGICOS|ALERGENICOS|ALERGÊNICOS)\b\s*[:\-]?", "ALÉRGICOS: ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        value = Regex.Replace(value, @"\bINGREDIENTES\b\s*[:\-]?", "INGREDIENTES: ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        value = Regex.Replace(value, @"\bNÃO\s+CONTÉM\b\s*[:\-]?", "NÃO CONTÉM ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        value = Regex.Replace(value, @"\bPODE\s+CONTER\b\s*[:\-]?", "PODE CONTER ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        value = Regex.Replace(value, @"\bCONTÉM\b\s*[:\-]?", "CONTÉM ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return value;
    }

    internal static IEnumerable<string> ResolveFoodTerms(string value)
    {
        var normalized = IngredientTextNormalizer.Normalize(value);
        foreach (var entry in IngredientDictionary.Allergens.Concat(IngredientDictionary.IngredientNormalization))
        {
            foreach (var term in entry.Synonyms.Concat([entry.CanonicalName]))
            {
                if (!normalized.Contains(IngredientTextNormalizer.Normalize(term), StringComparison.Ordinal))
                    continue;

                yield return ResolveExplicitTerm(entry.CanonicalName, term);
                break;
            }
        }
    }

    private static string ResolveExplicitTerm(string canonicalName, string explicitTerm)
    {
        var normalized = IngredientTextNormalizer.Normalize(explicitTerm);
        if (normalized.Contains("lactose", StringComparison.Ordinal)) return "lactose";
        if (normalized.Contains("leite", StringComparison.Ordinal)) return "leite";
        if (normalized.Contains("gluten", StringComparison.Ordinal)) return "glúten";
        if (normalized.Contains("avela", StringComparison.Ordinal)) return "avelãs";
        return canonicalName;
    }
}

public sealed record StructuredTextDocument(List<StructuredTextBlockDto> Blocks)
{
    public IEnumerable<StructuredTextBlockDto> BlocksOfType(params string[] types) =>
        Blocks.Where(block => types.Contains(block.Type, StringComparer.OrdinalIgnoreCase));
}

public sealed class StructuredTextLayer(ILogger<StructuredTextLayer> logger)
{
    private static readonly SemanticRegionTransitionEngine TransitionEngine = new();

    public StructuredTextDocument Build(params string?[] texts)
    {
        var textValues = texts
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text!)
            .ToList();

        var blocks = textValues
            .SelectMany(ExtractExplicitIngredientBlocks)
            .Concat(textValues.SelectMany(SegmentIntoRegions))
            .Where(block => !string.IsNullOrWhiteSpace(block.Text))
            .Where(IsRelevantBlock)
            .GroupBy(block => $"{block.RegionType}:{IngredientTextNormalizer.Normalize(block.Text)}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(block => RegionRank(block.RegionType))
            .ThenBy(block => block.Text.Length)
            .ToList();

        logger.LogDebug("[IngredientAnalysis.StructuredText] Blocks={BlockCount}", blocks.Count);
        return new StructuredTextDocument(blocks);
    }

    private static IEnumerable<StructuredTextBlockDto> SegmentIntoRegions(string text)
    {
        var currentRegion = TextRegionType.Unknown;
        var buffer = new List<string>();

        foreach (var value in TransitionEngine.SplitIntoSemanticSegments(text))
        {
            var region = ResolveRegionType(value, currentRegion);
            if (TransitionEngine.IsSectionTransition(currentRegion, region, value) && buffer.Count > 0)
            {
                yield return BuildBlock(currentRegion, string.Join(" ", buffer));
                buffer.Clear();
            }

            currentRegion = region;
            if (ShouldMergeWithPrevious(region, value) && buffer.Count > 0)
                buffer[^1] = buffer[^1].TrimEnd('.', ';') + " " + value;
            else
                buffer.Add(value);
        }

        if (buffer.Count > 0)
            yield return BuildBlock(currentRegion, string.Join(" ", buffer));
    }

    private static IEnumerable<StructuredTextBlockDto> ExtractExplicitIngredientBlocks(string text)
    {
        // Multi-strategy approach: Try different extraction methods and keep the best one

        // Strategy 1: Standard extraction with lookahead stopping at uppercase section headers or end.
        // Uses [\s\S]+? (any char including newlines/periods) so OCR periods inside the ingredient list
        // do not prematurely truncate the body.
        var standardMatches = Regex.Matches(
            text,
            @"\bINGREDIENTES?\s*[:\-]?\s*(?<body>[\s\S]+?)(?=\s*\n?\s*[A-ZÁÉÍÓÚÂÊÔÃÕÇ]{4,}[\s:,\-]|\bINFORMA[CÇ]|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match match in standardMatches)
        {
            var raw = match.Groups["body"].Value;

            // Split into lines — for single-line OCR output this will be one entry.
            // Use inline uppercase markers as additional split boundaries so "água, sorbitol. ALÉRGICOS:"
            // correctly terminates at the allergen header even without a newline.
            var inlineSplit = Regex.Split(raw, @"(?=\b[A-ZÁÉÍÓÚÂÊÔÃÕÇ]{4,}[\s:,\-])");
            var lines = inlineSplit
                .SelectMany(part => part.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                .ToList();

            // Keep only lines before any uppercase section header (ALÉRGICOS, NÃO CONTÉM, etc.)
            var cleanedLines = lines
                .TakeWhile(line => !Regex.IsMatch(line.Trim(), @"^[A-ZÁÉÍÓÚÂÊÔÃÕÇ]{4,}[\s:,\-]", RegexOptions.None))
                .ToList();

            // If the inline split produced nothing useful, fall back to the raw body
            if (cleanedLines.Count == 0)
                cleanedLines = [raw];

            var ingredientText = string.Join(" ", cleanedLines);

            // Aggressive post-cleaning
            ingredientText = Regex.Replace(ingredientText, @"\b\d+(?:[,.]\d+)?\s*(?:kcal|kj|mg|g|ml)\b", " ", RegexOptions.IgnoreCase);
            ingredientText = Regex.Replace(ingredientText, @"\b(?:A[çc][úu]car|Carboidrato|Prote[íi]na|Gordura|Fibra|S[óo]dio|Valor\s+energ)[^,;]*", " ", RegexOptions.IgnoreCase);
            ingredientText = Regex.Replace(ingredientText, @"\b(?:Ideal|busca|sabor|doce|Conservar|seco|Ap[óo]s|aberto|consumir|preferencialmente)\b[^,;]*", " ", RegexOptions.IgnoreCase);
            ingredientText = Regex.Replace(ingredientText, @"\bcana\s*de\s*a[çc][úu]car[^,;]*", " ", RegexOptions.IgnoreCase);
            // Remove marketing phrases like "como o do açúcar" that may leak into the ingredient block
            ingredientText = Regex.Replace(ingredientText, @"\bcomo\s+o\s+do\s+a[çc][úu]car\b[^,;]*", " ", RegexOptions.IgnoreCase);
            // Remove isolated "açúcar" followed by punctuation + another word (marketing text artifact)
            ingredientText = Regex.Replace(ingredientText, @"\ba[çc][úu]car\s*\.\s*(?=[a-záéíóúâêôãõç])", " ", RegexOptions.IgnoreCase);

            // Remove standalone nutrition terms
            ingredientText = Regex.Replace(ingredientText, @"\b(?:e\s+)?(?:seco|fresco|local)\b", " ", RegexOptions.IgnoreCase);

            // Normalize
            ingredientText = Regex.Replace(ingredientText, @"\s+", " ").Trim(' ', '.', ';', ':', '-', ',');

            if (ingredientText.Length < 10)
                continue;

            yield return BuildBlock(TextRegionType.IngredientList, $"INGREDIENTES: {ingredientText}");
        }
    }

    private static StructuredTextBlockDto BuildBlock(TextRegionType regionType, string text) =>
        new()
        {
            RegionType = regionType,
            Type = ToBlockType(regionType),
            SemanticRegion = ToSemanticRegion(regionType),
            Text = text.Trim(),
            Source = "raw_ocr_layer",
            Confidence = ResolveBlockConfidence(regionType, text)
        };

    private static string ToSemanticRegion(TextRegionType regionType) => regionType switch
    {
        TextRegionType.IngredientList => "INGREDIENTS_BLOCK",
        TextRegionType.AllergenBlock => "ALLERGEN_BLOCK",
        TextRegionType.RegulatoryClaim => "CLAIMS_BLOCK",
        TextRegionType.NutritionTable => "NUTRITION_BLOCK",
        TextRegionType.ManufacturerInfo or TextRegionType.StorageInfo => "FOOTER_BLOCK",
        TextRegionType.MarketingText => "MARKETING_BLOCK",
        _ => "UNKNOWN_BLOCK"
    };

    private static TextRegionType ResolveRegionType(string value, TextRegionType currentRegion)
    {
        var normalized = IngredientTextNormalizer.Normalize(value);
        if (SemanticRegionTransitionEngine.IsIngredientAnchor(normalized)) return TextRegionType.IngredientList;
        if (normalized.StartsWith("alergico", StringComparison.Ordinal) || normalized.StartsWith("alergenico", StringComparison.Ordinal)) return TextRegionType.AllergenBlock;
        if (ClaimContextValidator.IsValidRegulatoryClaim(value)) return TextRegionType.RegulatoryClaim;
        if (IsManufacturerContext(normalized)) return TextRegionType.ManufacturerInfo;
        if (IsStorageContext(normalized)) return TextRegionType.StorageInfo;
        if (IsNutritionContext(normalized)) return TextRegionType.NutritionTable;
        if (IsMarketingContext(normalized)) return TextRegionType.MarketingText;
        return currentRegion is TextRegionType.IngredientList or TextRegionType.NutritionTable ? currentRegion : TextRegionType.Unknown;
    }

    private static bool ShouldMergeWithPrevious(TextRegionType regionType, string value)
    {
        var normalized = IngredientTextNormalizer.Normalize(value);
        return regionType == TextRegionType.IngredientList &&
            !SemanticRegionTransitionEngine.IsIngredientAnchor(normalized) &&
            !normalized.StartsWith("alergico", StringComparison.Ordinal) &&
            !normalized.StartsWith("alergenico", StringComparison.Ordinal) &&
            !normalized.StartsWith("nao contem", StringComparison.Ordinal) &&
            !normalized.StartsWith("sem ", StringComparison.Ordinal) &&
            !normalized.StartsWith("livre de", StringComparison.Ordinal) &&
            !normalized.StartsWith("pode conter", StringComparison.Ordinal) &&
            !IsManufacturerContext(normalized) &&
            !IsNutritionContext(normalized) &&
            !ClaimContextValidator.IsValidRegulatoryClaim(value);
    }

    private static bool IsRelevantBlock(StructuredTextBlockDto block)
    {
        var normalized = IngredientTextNormalizer.Normalize(block.Text);
        if (normalized.Length < 4) return false;
        if (Regex.IsMatch(normalized, @"^[\d\s.,%/]+$", RegexOptions.CultureInvariant)) return false;
        if (block.RegionType == TextRegionType.Unknown && normalized.Length < 25) return false;
        if (block.RegionType == TextRegionType.Unknown && !IngredientTextNormalizer.ContainsAny(normalized, ["gluten", "lactose", "acucar", "ingrediente", "alergico"])) return false;
        return true;
    }

    private static string ToBlockType(TextRegionType regionType) => regionType switch
    {
        TextRegionType.IngredientList => "IngredientBlock",
        TextRegionType.AllergenBlock => "AllergenBlock",
        TextRegionType.RegulatoryClaim => "RegulatoryClaimBlock",
        TextRegionType.NutritionTable => "NutritionBlock",
        TextRegionType.ManufacturerInfo => "ManufacturerBlock",
        TextRegionType.StorageInfo => "StorageBlock",
        TextRegionType.MarketingText => "MarketingBlock",
        _ => "UnknownBlock"
    };

    private static string ResolveBlockConfidence(TextRegionType regionType, string value)
    {
        if (regionType is TextRegionType.IngredientList or TextRegionType.RegulatoryClaim or TextRegionType.AllergenBlock) return "high";
        if (regionType == TextRegionType.NutritionTable) return "medium";
        return "medium";
    }

    private static bool IsNutritionContext(string normalized) =>
        normalized.Contains("informacao nutricional", StringComparison.Ordinal) ||
        normalized.Contains("tabela nutricional", StringComparison.Ordinal) ||
        normalized.Contains("nao contem quantidades significativas", StringComparison.Ordinal) ||
        normalized.Contains("valor energetico", StringComparison.Ordinal) ||
        normalized.Contains("porcao", StringComparison.Ordinal) ||
        normalized.Contains("%vd", StringComparison.Ordinal) ||
        Regex.IsMatch(normalized, @"\b\d+(?:[,.]\d+)?\s*(?:kcal|kj|mg|g)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool IsManufacturerContext(string normalized) =>
        normalized.StartsWith("produzido", StringComparison.Ordinal) ||
        normalized.StartsWith("envasilhado", StringComparison.Ordinal) ||
        normalized.StartsWith("fabricado", StringComparison.Ordinal) ||
        normalized.Contains("fabricante", StringComparison.Ordinal) ||
        normalized.Contains("cnpj", StringComparison.Ordinal) ||
        normalized.Contains("distribuido por", StringComparison.Ordinal);

    private static bool IsStorageContext(string normalized) =>
        normalized.StartsWith("conservar", StringComparison.Ordinal) ||
        normalized.StartsWith("conserve", StringComparison.Ordinal) ||
        normalized.StartsWith("manter", StringComparison.Ordinal) ||
        normalized.StartsWith("validade", StringComparison.Ordinal) ||
        normalized.StartsWith("lote", StringComparison.Ordinal);

    private static bool IsMarketingContext(string normalized) =>
        IngredientTextNormalizer.ContainsAny(normalized, ["sabor", "nova formula", "qualidade", "natural", "premium"]);

    private static int RegionRank(TextRegionType regionType) => regionType switch
    {
        TextRegionType.IngredientList => 0,
        TextRegionType.RegulatoryClaim => 1,
        TextRegionType.NutritionTable => 2,
        TextRegionType.ManufacturerInfo => 3,
        TextRegionType.StorageInfo => 4,
        TextRegionType.MarketingText => 5,
        _ => 6
    };
}

public static class ClaimContextValidator
{
    private static readonly string[] ContextualBlacklist =
    [
        "nao contem quantidades significativas",
        "não contém quantidades significativas",
        "quantidades significativas",
        "valor diario",
        "%vd",
        "porcao",
        "informacao nutricional",
        "tabela nutricional"
    ];

    public static bool IsValidRegulatoryClaim(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = IngredientTextNormalizer.Normalize(value);
        if (ContextualBlacklist.Any(term => normalized.Contains(IngredientTextNormalizer.Normalize(term), StringComparison.Ordinal)))
            return false;

        return IsOfficialWhitelistedClaim(normalized);
    }

    public static bool IsOfficialWhitelistedClaim(string normalized) =>
        normalized.Contains("nao contem gluten", StringComparison.Ordinal) ||
        normalized.Contains("sem gluten", StringComparison.Ordinal) ||
        normalized.Contains("livre de gluten", StringComparison.Ordinal) ||
        normalized.Contains("contem gluten", StringComparison.Ordinal) ||
        normalized.Contains("zero acucar", StringComparison.Ordinal) ||
        normalized.Contains("sem acucar", StringComparison.Ordinal) ||
        normalized.Contains("sem adicao de acucar", StringComparison.Ordinal) ||
        normalized.Contains("livre de acucar", StringComparison.Ordinal) ||
        normalized.Contains("sem lactose", StringComparison.Ordinal) ||
        normalized.Contains("nao contem lactose", StringComparison.Ordinal) ||
        normalized.Contains("livre de lactose", StringComparison.Ordinal) ||
        normalized.Contains("zero lactose", StringComparison.Ordinal) ||
        normalized.StartsWith("pode conter", StringComparison.Ordinal) ||
        normalized.StartsWith("tracos de", StringComparison.Ordinal) ||
        normalized.StartsWith("contem ", StringComparison.Ordinal) ||
        normalized.StartsWith("alergico", StringComparison.Ordinal) ||
        normalized.StartsWith("alergenico", StringComparison.Ordinal) ||
        normalized is "light" or "diet" ||
        normalized.Contains(" light", StringComparison.Ordinal) ||
        normalized.Contains(" diet", StringComparison.Ordinal);
}

public sealed record RegulatoryClaimsResult(
    List<string> ContainsClaims,
    List<string> MayContainClaims,
    List<string> FreeFromClaims,
    List<string> AllergenClaims)
{
    public List<string> AllClaims => ContainsClaims
        .Concat(MayContainClaims)
        .Concat(FreeFromClaims)
        .Concat(AllergenClaims)
        .GroupBy(IngredientTextNormalizer.Normalize)
        .Select(group => group.First())
        .ToList();
}

public sealed class RegulatoryClaimExtractor(ILogger<RegulatoryClaimExtractor> logger)
{
    public RegulatoryClaimsResult Extract(StructuredTextDocument document) =>
        Extract(document.BlocksOfType("RegulatoryClaimBlock", "AllergenBlock").Select(block => block.Text).ToArray());

    public RegulatoryClaimsResult Extract(params string?[] texts)
    {
        var contains = new List<string>();
        var mayContain = new List<string>();
        var freeFrom = new List<string>();
        var allergenStatements = new List<string>();

        foreach (var text in texts.Where(text => !string.IsNullOrWhiteSpace(text)))
        {
            ExtractFromText(text!, contains, mayContain, freeFrom, allergenStatements);
        }

        var result = new RegulatoryClaimsResult(Distinct(contains), Distinct(mayContain), Distinct(freeFrom), Distinct(allergenStatements));
        logger.LogDebug(
            "[IngredientAnalysis.RegulatoryClaims] Contains={Contains}; MayContain={MayContain}; FreeFrom={FreeFrom}; AllergenStatements={AllergenStatements}",
            result.ContainsClaims.Count,
            result.MayContainClaims.Count,
            result.FreeFromClaims.Count,
            result.AllergenClaims.Count);

        return result;
    }

    private static void ExtractFromText(string text, List<string> contains, List<string> mayContain, List<string> freeFrom, List<string> allergenStatements)
    {
        if (!ClaimContextValidator.IsValidRegulatoryClaim(text) && !IngredientTextNormalizer.Normalize(text).StartsWith("alergico", StringComparison.Ordinal))
            return;

        foreach (Match match in Regex.Matches(text, @"\bNÃO\s+CONTÉM\s+([^.;\n]{2,80})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            if (ClaimContextValidator.IsValidRegulatoryClaim(match.Value))
                AddClaims("NÃO CONTÉM", match.Groups[1].Value, freeFrom);
        }

        foreach (Match match in Regex.Matches(text, @"\b(?:SEM|LIVRE\s+DE|ZERO)\s+([^.;\n]{2,80})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            if (ClaimContextValidator.IsValidRegulatoryClaim(match.Value))
                AddClaims("NÃO CONTÉM", match.Groups[1].Value, freeFrom);
        }

        foreach (Match match in Regex.Matches(text, @"\b(?:PODE\s+CONTER|TRA[CÇ]OS\s+DE)\s+([^.;\n]{2,100})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            if (ClaimContextValidator.IsValidRegulatoryClaim(match.Value))
                AddClaims("PODE CONTER", match.Groups[1].Value, mayContain);
        }

        foreach (Match match in Regex.Matches(text, @"\bCONTÉM\s+([^.;\n]{2,100})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            var prefixStart = Math.Max(0, match.Index - 8);
            var prefix = text[prefixStart..match.Index];
            var normalizedPrefix = IngredientTextNormalizer.Normalize(prefix);
            if (normalizedPrefix.EndsWith("pode", StringComparison.Ordinal) || normalizedPrefix.EndsWith("nao", StringComparison.Ordinal))
                continue;

            if (!ClaimContextValidator.IsValidRegulatoryClaim(match.Value))
                continue;

            AddClaims("CONTÉM", match.Groups[1].Value, contains);
        }

        foreach (Match match in Regex.Matches(text, @"\bAL[ÉE]RGICOS?\s*[:\-]?\s*([^.;\n]{2,140})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            var statement = match.Value.Trim().TrimEnd('.', ';', ',');
            allergenStatements.Add(statement);

            var normalized = IngredientTextNormalizer.Normalize(match.Groups[1].Value);
            if (!ClaimContextValidator.IsValidRegulatoryClaim(statement))
                continue;

            if (normalized.Contains("pode conter", StringComparison.Ordinal))
                AddClaims("PODE CONTER", match.Groups[1].Value, mayContain);
            else if (normalized.Contains("nao contem", StringComparison.Ordinal))
                AddClaims("NÃO CONTÉM", match.Groups[1].Value, freeFrom);
            else if (normalized.Contains("contem", StringComparison.Ordinal))
                AddClaims("CONTÉM", match.Groups[1].Value, contains);
            else
                AddClaims("CONTÉM", match.Groups[1].Value, contains);
        }
    }

    private static void AddClaims(string prefix, string value, List<string> target)
    {
        var terms = SemanticReconstructionService.ResolveFoodTerms(value).ToList();
        if (terms.Count == 0)
            return;

        foreach (var term in terms)
            target.Add($"{prefix} {term}");
    }

    private static List<string> Distinct(IEnumerable<string> values) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(IngredientTextNormalizer.Normalize)
            .Select(group => group.First())
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
}

public sealed record StructuredIngredientParseResult(List<string> Ingredients, List<IngredientNormalizedDto> NormalizedIngredients);

public sealed class IngredientParserService(
    IngredientClassifier classifier,
    IngredientNormalizer normalizer,
    ILogger<IngredientParserService> logger)
{
    private static readonly FoodIngredientTokenizer FoodTokenizer = new();
    private static readonly IngredientPurificationLayer PurificationLayer = new(
        new IngredientNoiseFilter(),
        new NutritionLeakBlocker(),
        new IngredientSemanticValidator());

    public StructuredIngredientParseResult Parse(IngredientAnalysisContext context)
    {
        var ingredients = classifier.ClassifyIngredients(context);
        var textSources = new[]
        {
            context.OcrText,
            context.DocumentIntelligenceText,
            string.Join("\n", context.VisionExtraction.RawExtractedText),
            string.Join("\n", context.VisionExtraction.IngredientsDetected)
        };

        ingredients.AddRange(ExtractDictionaryIngredients(textSources));
        ingredients = PurifyIngredients(ingredients);

        var normalizedIngredients = normalizer.Normalize(ingredients);
        logger.LogDebug("[IngredientAnalysis.IngredientParser] Ingredients={IngredientCount}; Normalized={NormalizedCount}", ingredients.Count, normalizedIngredients.Count);
        return new StructuredIngredientParseResult(ingredients, normalizedIngredients);
    }

    public StructuredIngredientParseResult Parse(IngredientAnalysisContext context, StructuredTextDocument structuredText)
    {
        var ingredientText = string.Join("\n", structuredText.BlocksOfType("IngredientBlock").Select(block => block.Text));

        logger.LogDebug("[IngredientAnalysis.Parser] Raw ingredient block text: {Text}", ingredientText);

        // Remove "INGREDIENTES:" prefix to get only the list
        ingredientText = Regex.Replace(ingredientText, @"^INGREDIENTES?\s*[:\-]?\s*", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        logger.LogDebug("[IngredientAnalysis.Parser] After prefix removal: {Text}", ingredientText);

        var ingredientContext = new IngredientAnalysisContext
        {
            OcrText = ingredientText,
            DocumentIntelligenceText = null,
            VisionExtraction = new IngredientExtractionResult()
        };

        // Split by comma, semicolon, and " e " (Portuguese conjunction used as separator
        // in OCR/Vision output where items are concatenated without comma: "tomate seco e pimenta")
        // Split " e " only between multi-word ingredient candidates to avoid splitting inside
        // compound names like "cebola em flocos e alho granulado".
        var rawIngredients = ingredientText
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(item =>
            {
                var trimmed = item.Trim(' ', '.', ':', '-');
                // Split on " e " only when the token is long and contains multiple food words
                // separated by " e " without a comma — e.g. "glutamato monossódico e pimentão vermelho seco"
                var parts = Regex.Split(trimmed, @"(?<=\p{L})\s+e\s+(?=\p{L})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                return parts.Length > 1 ? parts.Select(p => p.Trim(' ', '.', ':', '-')) : [trimmed];
            })
            .Where(item => item.Length >= 3)
            .ToList();

        logger.LogDebug("[IngredientAnalysis.Parser] After split: {Count} items: {Items}", 
            rawIngredients.Count, 
            string.Join(" | ", rawIngredients));

        var filtered = rawIngredients.Where(item => !IsNutritionOrClaimNoise(item)).ToList();

        logger.LogDebug("[IngredientAnalysis.Parser] After noise filter: {Count} items: {Items}", 
            filtered.Count, 
            string.Join(" | ", filtered));

        var tokenizedIngredients = FoodTokenizer.Tokenize(string.Join(", ", filtered))
            .Select(token => token.Text)
            .ToList();

        logger.LogDebug("[IngredientAnalysis.Parser] After tokenization: {Count} items", tokenizedIngredients.Count);

        var ingredients = PurifyIngredients(filtered.Concat(tokenizedIngredients).Concat(classifier.ClassifyIngredients(ingredientContext)));

        logger.LogDebug("[IngredientAnalysis.Parser] After purification: {Count} final ingredients: {Items}", 
            ingredients.Count, 
            string.Join(" | ", ingredients));

        var normalizedIngredients = normalizer.Normalize(ingredients);
        logger.LogDebug("[IngredientAnalysis.IngredientParser] StructuredIngredients={IngredientCount}; Normalized={NormalizedCount}", ingredients.Count, normalizedIngredients.Count);
        return new StructuredIngredientParseResult(ingredients, normalizedIngredients);
    }

    private static bool IsNutritionOrClaimNoise(string item)
    {
        var normalized = IngredientTextNormalizer.Normalize(item);

        // Filter alphanumeric lot/batch codes and traceability tokens: LOT038, VALD10727, etc.
        if (Regex.IsMatch(normalized, @"^[a-z]{2,5}\d{3,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return true;

        // Filter tokens with manufacturer data: email, phone, CNPJ fragment
        if (Regex.IsMatch(normalized,
                @"(?:@|\bemail\b|\bwww\.|\.com\.br|f:\s*\(|\(\d{2}\)|c\.\s*n\.\s*p\.\s*j|\d{2}\.\d{3}\.\d{3})",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return true;

        // Filter marketing / usage suggestion phrases that contain food words accidentally.
        // e.g. "basta adicionar água", "mistura totalmente natural", "para sua preparação"
        if (Regex.IsMatch(normalized,
                @"\b(?:basta|adicionar|adicione|preparo|preparacao|misture|misturar|dissolver|dissolva|consumir|modo de uso|sugestao de uso|instrucoes|instrucao|diluir|como usar|forma de preparo|totalmente natural|feito basicamente|a partir de uma mistura)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return true;

        // Filter lot / traceability codes that contain ingredient words incidentally.
        // e.g. "LOT038 LOTO38 VALD10727 Marpa Alimentos monossódico"
        if (Regex.IsMatch(normalized,
                @"\b(?:lot|lote|loto|vald|val[\s:]|codigo|cnpj|sac\b)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return true;

        // Filter manufacturer / brand noise merged with ingredient text.
        // e.g. "Marpa Alimentos monossódico e pimentão vermelho seco"
        if (Regex.IsMatch(normalized,
                @"\b(?:alimentos|industria|ltda|eireli|s\.a\.|s\.a\b|marpa|distribuidora|comercial|grupo)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return true;

        // Filter nutrition table patterns with units
        if (Regex.IsMatch(normalized, @"\b\d+(?:[,.]\d+)?\s*(?:kcal|kj|mg|g|ml)\b", RegexOptions.IgnoreCase))
            return true;

        // Filter nutrition table row pattern (nutrient name followed by numbers)
        if (Regex.IsMatch(normalized, @"^(?:acucar|carboidrato|proteina|gordura|fibra|sodio|calorias|valor energetico)\s*\d", RegexOptions.IgnoreCase))
            return true;

        // Filter claim patterns (but NOT compound ingredients like "edulcorantes: ciclamato")
        if (normalized.StartsWith("nao contem", StringComparison.Ordinal) ||
            normalized.StartsWith("livre de", StringComparison.Ordinal) ||
            normalized.StartsWith("zero ", StringComparison.Ordinal))
            return true;

        // Allow "sem" only if it's not a compound ingredient descriptor
        if (normalized.StartsWith("sem ", StringComparison.Ordinal) && 
            !normalized.Contains(":", StringComparison.Ordinal))
            return true;

        // Filter standalone nutrition terms (but NOT when they're part of compound ingredients)
        var isStandaloneNutritionTerm = normalized is "acucar" or "acucar adicionado" or "acucares" or 
                                         "carboidrato" or "carboidratos" or "proteina" or "proteinas" or 
                                         "gordura" or "gorduras" or "fibra" or "fibras" or "sodio";

        if (isStandaloneNutritionTerm && !normalized.Contains(":", StringComparison.Ordinal))
            return true;

        // Filter patterns like "açúcar. água" (nutrition table contamination)
        if (Regex.IsMatch(normalized, @"^acucar\.\s*[a-z]", RegexOptions.IgnoreCase))
            return true;

        // Filter tokens that contain marketing contamination: "X açúcar. Y"
        if (Regex.IsMatch(normalized, @"\bacucar\s*\.\s*[a-z]", RegexOptions.IgnoreCase))
            return true;

        // Filter tokens that are a known ingredient merged with marketing sugar reference:
        // e.g. "sorbitol açúcar. água"
        if (Regex.IsMatch(normalized, @"^[a-záéíóúâêôãõç\s]+\s+acucar[\s.]", RegexOptions.IgnoreCase) &&
            Regex.IsMatch(normalized, @"\bagua\b|\bsorbitol\b|\bsucralose\b", RegexOptions.IgnoreCase))
            return true;

        return false;
    }

    public StructuredIngredientParseResult ParseWithRecovery(
        IngredientAnalysisContext context,
        StructuredTextDocument structuredText,
        IngredientRecoveryEngine recoveryEngine)
    {
        var structuredResult = Parse(context, structuredText);
        
        var recovery = recoveryEngine.Recover(
            structuredResult.Ingredients,
            structuredText,
            context.OcrText,
            context.DocumentIntelligenceText,
            string.Join("\n", context.VisionExtraction.RawExtractedText),
            string.Join("\n", context.VisionExtraction.IngredientsDetected));

        if (!recovery.RecoveryApplied || recovery.Ingredients.Count == 0)
        {
            logger.LogDebug("[IngredientAnalysis.Recovery] No recovery applied or no ingredients recovered.");
            return structuredResult;
        }

        var purifiedIngredients = PurifyIngredients(recovery.Ingredients);
        var normalizedIngredients = normalizer.Normalize(purifiedIngredients);
        logger.LogInformation(
            "[IngredientAnalysis.Recovery] Recovered {Count} purified ingredients via {Source} with grammar score {Score}.",
            purifiedIngredients.Count,
            recovery.Source,
            recovery.GrammarScore);

        return new StructuredIngredientParseResult(purifiedIngredients, normalizedIngredients);
    }

    private static List<string> PurifyIngredients(IEnumerable<string> candidates) =>
        PurificationLayer.Purify(candidates).Ingredients;

    private static IEnumerable<string> ExtractDictionaryIngredients(IEnumerable<string?> texts)
    {
        var joined = string.Join("\n", texts.Where(text => !string.IsNullOrWhiteSpace(text)));
        if (string.IsNullOrWhiteSpace(joined))
            yield break;

        var normalized = IngredientTextNormalizer.Normalize(joined);
        var hasIngredientContext = normalized.Contains("ingrediente", StringComparison.Ordinal) || normalized.Length <= 120;
        if (!hasIngredientContext)
            yield break;

        foreach (var entry in IngredientDictionary.IngredientNormalization.Concat(IngredientDictionary.Allergens))
        {
            foreach (var term in entry.Synonyms.Concat([entry.CanonicalName]))
            {
                var normalizedTerm = IngredientTextNormalizer.Normalize(term);
                // For short terms (<=4 chars) use word-boundary match to avoid false-positive substring hits:
                // e.g. "mel" inside "Amélia", "sal" inside "salsa", "ovo" inside "couve".
                var isMatch = normalizedTerm.Length <= 4
                    ? Regex.IsMatch(normalized, $@"(?<![\p{{L}}]){Regex.Escape(normalizedTerm)}(?![\p{{L}}])", RegexOptions.CultureInvariant)
                    : IngredientTextNormalizer.ContainsAny(joined, [term]);

                if (!isMatch)
                    continue;

                yield return entry.CanonicalName;
                break;
            }
        }
    }
}

public sealed record SemanticClaimResolutionResult(List<string> Claims, List<AllergenRiskDto> AllergenRisks);

public sealed class SemanticClaimResolver(ILogger<SemanticClaimResolver> logger)
{
    public SemanticClaimResolutionResult Resolve(IReadOnlyList<string> claims, IReadOnlyList<AllergenRiskDto> allergenRisks)
    {
        var normalizedClaims = NormalizeClaims(claims);
        var resolvedRisks = ResolveRisks(allergenRisks);
        var resolvedClaims = RemoveWeakerClaims(normalizedClaims, resolvedRisks);

        logger.LogDebug(
            "[IngredientAnalysis.SemanticClaimResolver] ClaimsBefore={ClaimsBefore}; ClaimsAfter={ClaimsAfter}; RisksBefore={RisksBefore}; RisksAfter={RisksAfter}",
            claims.Count,
            resolvedClaims.Count,
            allergenRisks.Count,
            resolvedRisks.Count);

        return new SemanticClaimResolutionResult(resolvedClaims, resolvedRisks);
    }

    private static List<string> NormalizeClaims(IEnumerable<string> claims) =>
        claims
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().TrimEnd('.', ';', ','))
            .GroupBy(IngredientTextNormalizer.Normalize)
            .Select(group => group.First())
            .ToList();

    private static List<AllergenRiskDto> ResolveRisks(IReadOnlyList<AllergenRiskDto> risks)
    {
        var groups = risks.GroupBy(risk => ResolveRiskGroup(risk.Name), StringComparer.OrdinalIgnoreCase);
        var resolved = new List<AllergenRiskDto>();

        foreach (var group in groups)
        {
            var strongest = group
                .OrderBy(risk => SeverityRank(risk.RiskType))
                .ThenBy(risk => ConfidenceRank(risk.Confidence))
                .First();

            foreach (var duplicate in group.Where(item => !ReferenceEquals(item, strongest)))
            {
                foreach (var evidence in duplicate.Evidence)
                {
                    if (!strongest.Evidence.Any(existing => IngredientTextNormalizer.Normalize(existing) == IngredientTextNormalizer.Normalize(evidence)))
                        strongest.Evidence.Add(evidence);
                }

                foreach (var evidence in duplicate.SemanticEvidence)
                {
                    if (!strongest.SemanticEvidence.Any(existing => IngredientTextNormalizer.Normalize(existing.Text) == IngredientTextNormalizer.Normalize(evidence.Text)))
                        strongest.SemanticEvidence.Add(evidence);
                }

                foreach (var evidenceType in duplicate.EvidenceTypes)
                {
                    if (!strongest.EvidenceTypes.Contains(evidenceType))
                        strongest.EvidenceTypes.Add(evidenceType);
                }
            }

            resolved.Add(strongest);
        }

        return resolved
            .OrderBy(risk => risk.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> RemoveWeakerClaims(IReadOnlyList<string> claims, IReadOnlyList<AllergenRiskDto> risks)
    {
        var containedGroups = risks
            .Where(risk => risk.RiskType == "contains")
            .Select(risk => ResolveRiskGroup(risk.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return claims
            .Where(claim => !IsWeakerClaimForContainedAllergen(claim, containedGroups))
            .GroupBy(IngredientTextNormalizer.Normalize)
            .Select(group => group.First())
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsWeakerClaimForContainedAllergen(string claim, ISet<string> containedGroups)
    {
        var normalized = IngredientTextNormalizer.Normalize(claim);
        if (!normalized.Contains("pode conter", StringComparison.Ordinal) &&
            !normalized.Contains("tracos de", StringComparison.Ordinal) &&
            !normalized.Contains("fabricado em", StringComparison.Ordinal))
        {
            return false;
        }

        return containedGroups.Any(group => IngredientTextNormalizer.ContainsAny(claim, [group]) || IsMilkLactoseEquivalent(group, claim));
    }

    private static string ResolveRiskGroup(string value)
    {
        var normalized = IngredientTextNormalizer.Normalize(value);
        if (normalized.Contains("lactose", StringComparison.Ordinal) || normalized.Contains("leite", StringComparison.Ordinal))
            return "leite";
        if (normalized.Contains("gluten", StringComparison.Ordinal) || normalized.Contains("trigo", StringComparison.Ordinal) || normalized.Contains("cevada", StringComparison.Ordinal) || normalized.Contains("malte", StringComparison.Ordinal))
            return "glúten";
        if (normalized.Contains("avela", StringComparison.Ordinal) || normalized.Contains("castanha", StringComparison.Ordinal) || normalized.Contains("macadamia", StringComparison.Ordinal))
            return "castanhas";
        return value;
    }

    private static bool IsMilkLactoseEquivalent(string group, string claim) =>
        group.Equals("leite", StringComparison.OrdinalIgnoreCase) && IngredientTextNormalizer.ContainsAny(claim, ["leite", "lactose"]);

    private static int SeverityRank(string riskType) => riskType switch
    {
        "contains" => 0,
        "may_contain" => 1,
        "cross_contamination" => 2,
        _ => 3
    };

    private static int ConfidenceRank(string confidence) => confidence switch
    {
        "high" => 0,
        "medium" => 1,
        _ => 2
    };
}

public sealed class SemanticFoodConsolidator(ILogger<SemanticFoodConsolidator> logger)
{
    public void Consolidate(
        IngredientAnalysisResponse response,
        DietProfilesDto dietProfiles,
        List<AllergenRiskDto> allergenRisks,
        List<ClaimDetectionDto> claimsDetected)
    {
        response.AllergenRisks = allergenRisks
            .GroupBy(risk => $"{IngredientTextNormalizer.Normalize(risk.Name)}:{risk.RiskType}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        response.ClaimsDetected = claimsDetected
            .GroupBy(claim => IngredientTextNormalizer.Normalize(claim.Text), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        response.DietProfiles = dietProfiles;
        response.Warnings = response.Warnings
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(IngredientTextNormalizer.Normalize)
            .Select(group => group.First())
            .ToList();

        logger.LogDebug("[IngredientAnalysis.SemanticConsolidation] Risks={Risks}; Claims={Claims}", response.AllergenRisks.Count, response.ClaimsDetected.Count);
    }
}

public sealed class FoodSemanticResponseBuilder
{
    public void Apply(IngredientAnalysisResponse response)
    {
        response.CategorizedIngredients = BuildCategorizedIngredients(response.NormalizedIngredients);
        response.ConfirmedFacts = BuildConfirmedFacts(response).ToList();
        response.InferredFacts = BuildInferredFacts(response).ToList();
        response.ProcessingAnalysis = new ProcessingAnalysisDto
        {
            Level = response.ProcessingClassification.Level,
            Confidence = response.ProcessingClassification.Confidence,
            ProcessingScore = response.ProcessingClassification.ProcessingScore,
            Reasons = response.ProcessingClassification.Reasons.ToList(),
            IndustrialSignals = response.NormalizedIngredients
                .Where(item => IngredientDictionary.UltraProcessingCategories.Contains(item.Category, StringComparer.OrdinalIgnoreCase))
                .Select(item => string.IsNullOrWhiteSpace(item.Normalized) ? item.Raw : item.Normalized)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static CategorizedIngredientsDto BuildCategorizedIngredients(IEnumerable<IngredientNormalizedDto> ingredients)
    {
        var result = new CategorizedIngredientsDto();
        foreach (var item in ingredients)
        {
            var name = string.IsNullOrWhiteSpace(item.Normalized) ? item.Raw : item.Normalized;
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var category = item.Category;
            if (category.Contains("milk", StringComparison.OrdinalIgnoreCase)) Add(result.Dairy, name);
            else if (category.Contains("gluten", StringComparison.OrdinalIgnoreCase) || item.DietaryRisk == "gluten") Add(result.Gluten, name);
            else if (category.Contains("nut", StringComparison.OrdinalIgnoreCase) || item.DietaryRisk == "nuts") Add(result.Nuts, name);
            else if (category == "artificial_sweetener") Add(result.Sweeteners, name);
            else if (category == "preservative") Add(result.Preservatives, name);
            else if (category == "emulsifier") Add(result.Emulsifiers, name);
            else if (IngredientDictionary.UltraProcessingCategories.Contains(category, StringComparer.OrdinalIgnoreCase)) Add(result.Additives, name);
            else Add(result.Other, name);
        }

        return result;
    }

    private static IEnumerable<FoodSemanticFactDto> BuildConfirmedFacts(IngredientAnalysisResponse response)
    {
        foreach (var claim in response.ClaimsDetected)
        {
            yield return new FoodSemanticFactDto
            {
                Text = claim.Text,
                Source = claim.OriginBlock,
                Confidence = claim.Confidence,
                DetectionType = claim.Type is "may_contain" or "cross_contamination" ? "cross_contamination" : "regulatory_claim",
                SemanticEvidence = claim.Evidence
            };
        }

        foreach (var ingredient in response.NormalizedIngredients.Where(item => item.DetectionType == "confirmed" && item.Confidence != "low"))
        {
            yield return new FoodSemanticFactDto
            {
                Text = string.IsNullOrWhiteSpace(ingredient.Normalized) ? ingredient.Raw : ingredient.Normalized,
                Source = ingredient.Source,
                Confidence = ingredient.Confidence,
                DetectionType = "confirmed",
                SemanticEvidence = ingredient.SemanticEvidence
            };
        }
    }

    private static IEnumerable<FoodSemanticFactDto> BuildInferredFacts(IngredientAnalysisResponse response)
    {
        foreach (var inference in response.SemanticInferences)
        {
            yield return new FoodSemanticFactDto
            {
                Text = inference.Text,
                Source = inference.Source,
                Confidence = inference.Confidence,
                DetectionType = inference.Type is "may_contain" or "cross_contamination" ? "cross_contamination" : "inferred",
                SemanticEvidence = [new SemanticEvidenceDto
                {
                    EvidenceType = EvidenceType.OcrInference,
                    Type = inference.Type,
                    Text = inference.Text,
                    Confidence = inference.Confidence,
                    Source = inference.Source,
                    TrustLevel = inference.TrustLevel,
                    OriginBlock = inference.OriginBlock
                }]
            };
        }

        foreach (var risk in response.AllergenRisks.Where(risk => risk.TrustLevel is EvidenceTrustLevel.SemanticInference or EvidenceTrustLevel.WeakInference))
        {
            yield return new FoodSemanticFactDto
            {
                Text = risk.Name,
                Source = string.IsNullOrWhiteSpace(risk.OriginBlock) ? "SemanticInferenceLayer" : risk.OriginBlock,
                Confidence = risk.Confidence,
                DetectionType = risk.RiskType is "may_contain" or "cross_contamination" ? "cross_contamination" : "suspected",
                SemanticEvidence = risk.SemanticEvidence
            };
        }
    }

    private static void Add(List<string> values, string value)
    {
        if (!values.Contains(value, StringComparer.OrdinalIgnoreCase))
            values.Add(value);
    }
}

public sealed class ProductionSafeFoodAnalysisEngine
{
    public void Apply(IngredientAnalysisResponse response)
    {
        var strongExplicitEvidence =
            response.BlockConfidence.IngredientConfidence is "high" or "medium" &&
            (response.BlockConfidence.ClaimsConfidence is "high" or "medium" || response.ClaimsDetected.Count > 0 || response.IngredientsDetected.Count >= 2);
        var criticalLowConfidence = response.Diagnostics.OverallConfidence is "low" or "very_low";

        // When OCR is high quality and ingredients were detected, do not trigger safe mode purely
        // from analysis completeness — products with short ingredient lists (sweeteners, condiments,
        // waters) are fully readable even if rawTextLength is below generic thresholds.
        var highQualityOcr = response.Diagnostics.OcrConfidence is "high" or "medium" &&
                             response.Diagnostics.ImageQualityConfidence is "high" or "medium";
        var sufficientIngredients = response.IngredientsDetected.Count >= 1 || response.ClaimsDetected.Count >= 1;

        var lowConfidence = response.AnalysisCompleteness.Status == "insufficient" ||
            (response.ProductionSafeModeApplied && !(highQualityOcr && sufficientIngredients)) ||
            (criticalLowConfidence && !strongExplicitEvidence);
        var reasons = response.AnalysisCompleteness.Reasons
            .Concat(response.Warnings.Where(warning => IngredientTextNormalizer.ContainsAny(warning, ["parcial", "desfoc", "reflexo", "ilegível", "ilegiv", "confiança", "confianca"])))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        response.ConfidenceSummary = new ConfidenceSummaryDto
        {
            GlobalConfidence = response.Diagnostics.OverallConfidence,
            OcrConfidence = response.Diagnostics.OcrConfidence,
            Completeness = response.AnalysisCompleteness.Status,
            IsPartialReading = response.AnalysisCompleteness.Status != "complete",
            BlocksAbsoluteConclusions = lowConfidence,
            ScoreIsEstimated = lowConfidence,
            Reasons = reasons.Count > 0 ? reasons : ["Confiança calculada a partir de OCR, completude e consistência semântica."]
        };

        response.NutritionAnalysis = new NutritionAnalysisSafetyDto
        {
            DefinitiveScoreAllowed = !lowConfidence,
            ScoreEstimated = lowConfidence,
            Status = lowConfidence ? "resultado_preliminar" : "score_definitivo_permitido",
            Warnings = lowConfidence ? ["Leitura parcial: o score deve ser tratado como estimado."] : []
        };

        if (!lowConfidence)
            return;

        response.ProductionSafeModeApplied = true;
        response.Warnings.Add("Modo production safe aplicado: não foi possível confirmar conclusões absolutas com a leitura atual.");
        response.Recommendations.Add("Resultado preliminar: tire uma nova foto se precisar confirmar perfis como vegano, sem lactose ou sem glúten.");

        DowngradePositiveProfile(response.DietProfiles.Vegan, "Não foi possível confirmar que o produto é vegano com leitura parcial.");
        DowngradePositiveProfile(response.DietProfiles.Vegetarian, "Não foi possível confirmar que o produto é vegetariano com leitura parcial.");
        DowngradePositiveProfile(response.DietProfiles.LactoseFree, "Não foi possível confirmar ausência de lactose com leitura parcial.");
        DowngradePositiveProfile(response.DietProfiles.GlutenFree, "Não foi possível confirmar ausência de glúten com leitura parcial.");
    }

    private static void DowngradePositiveProfile(DietProfileCompatibilityDto profile, string warning)
    {
        if (profile.CompatibilityStatus is DietCompatibilityStatuses.NotCompatible or DietCompatibilityStatuses.LikelyNotCompatible or DietCompatibilityStatuses.Attention)
            return;

        // Profiles resolved by explicit regulatory evidence (cross-contamination claims, confirmed
        // allergen blocks, or explicit free-from claims) are authoritative and must not be silenced
        // by production safe mode.
        var hasRegulatoryEvidence = profile.EvidenceTypes.Any(e => e is EvidenceType.ClaimDetected or EvidenceType.CrossContamination);
        if (hasRegulatoryEvidence)
            return;

        // ConfirmedCompatible from an explicit claim should never be downgraded
        if (profile.CompatibilityStatus == DietCompatibilityStatuses.ConfirmedCompatible)
            return;

        profile.Compatible = false;
        profile.CompatibilityStatus = DietCompatibilityStatuses.Uncertain;
        profile.CompatibilityLevel = "unknown";
        profile.Confidence = "low";
        profile.Status = CompatibilityStatus.Uncertain;
        if (!profile.Warnings.Contains(warning, StringComparer.OrdinalIgnoreCase))
            profile.Warnings.Add(warning);
    }
}

public sealed record SemanticSafetyValidationResult(
    List<string> Claims,
    List<AllergenRiskDto> AllergenRisks,
    List<SemanticInferenceDto> SemanticInferences,
    List<string> Warnings);

public sealed class SemanticSafetyValidator(ILogger<SemanticSafetyValidator> logger)
{
    public SemanticSafetyValidationResult Validate(
        IReadOnlyList<string> claims,
        IReadOnlyList<AllergenRiskDto> allergenRisks,
        StructuredTextDocument structuredText)
    {
        var explicitClaims = structuredText
            .BlocksOfType("RegulatoryClaimBlock", "AllergenBlock")
            .Select(block => IngredientTextNormalizer.Normalize(block.Text))
            .ToList();
        var explicitIngredientBlocks = structuredText
            .BlocksOfType("IngredientBlock")
            .Select(block => block.Text)
            .ToList();
        var warnings = new List<string>();
        var safeClaims = claims
            .Where(claim => IsSafeClaim(claim, explicitClaims, warnings))
            .GroupBy(IngredientTextNormalizer.Normalize)
            .Select(group => group.First())
            .OrderBy(claim => claim, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var safeRisks = new List<AllergenRiskDto>();
        var inferences = new List<SemanticInferenceDto>();

        foreach (var risk in allergenRisks)
        {
            NormalizeRiskTrust(risk, explicitClaims, explicitIngredientBlocks);
            ApplyRiskConfidenceCeiling(risk);
            if (CanRegulatoryRiskStand(risk))
            {
                safeRisks.Add(risk);
                continue;
            }

            var inference = ToSemanticInference(risk);
            inferences.Add(inference);
            risk.RiskType = inference.Type;
            risk.Severity = "possible";
            risk.Confidence = inference.Confidence;
            risk.TrustLevel = EvidenceTrustLevel.SemanticInference;
            risk.OriginBlock = inference.OriginBlock;
            safeRisks.Add(risk);
            warnings.Add($"Inferência semântica mantida como possível, sem claim regulatório: {risk.Name}.");
        }

        logger.LogDebug(
            "[IngredientAnalysis.SemanticSafety] ClaimsBefore={ClaimsBefore}; ClaimsAfter={ClaimsAfter}; Risks={Risks}; Inferences={Inferences}",
            claims.Count,
            safeClaims.Count,
            safeRisks.Count,
            inferences.Count);

        return new SemanticSafetyValidationResult(
            safeClaims,
            safeRisks
                .GroupBy(risk => $"{IngredientTextNormalizer.Normalize(risk.Name)}:{risk.RiskType}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(risk => risk.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            inferences
                .GroupBy(item => $"{item.Type}:{IngredientTextNormalizer.Normalize(item.Text)}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList(),
            warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static bool IsSafeClaim(string claim, IReadOnlyList<string> explicitClaims, List<string> warnings)
    {
        var normalized = IngredientTextNormalizer.Normalize(claim);
        var isRegulatory = normalized.StartsWith("contem ", StringComparison.Ordinal) ||
            normalized.StartsWith("pode conter", StringComparison.Ordinal) ||
            normalized.StartsWith("nao contem", StringComparison.Ordinal) ||
            normalized.StartsWith("alergico", StringComparison.Ordinal) ||
            normalized.StartsWith("alergenico", StringComparison.Ordinal) ||
            normalized.Contains("sem gluten", StringComparison.Ordinal) ||
            normalized.Contains("sem lactose", StringComparison.Ordinal) ||
            normalized.Contains("zero lactose", StringComparison.Ordinal) ||
            normalized.Contains("sem acucar", StringComparison.Ordinal) ||
            normalized.Contains("zero acucar", StringComparison.Ordinal);

        if (!isRegulatory)
            return true;

        var hasExplicitBlock = explicitClaims.Any(block => block.Contains(normalized, StringComparison.Ordinal) || normalized.Contains(block, StringComparison.Ordinal) || ContainsSameRegulatoryTerms(block, normalized));
        if (hasExplicitBlock)
            return true;

        warnings.Add($"Claim regulatório descartado por falta de evidência explícita: {claim}.");
        return false;
    }

    private static bool ContainsSameRegulatoryTerms(string block, string claim)
    {
        var samePrefix = (block.Contains("pode conter", StringComparison.Ordinal) && claim.Contains("pode conter", StringComparison.Ordinal)) ||
            (block.Contains("nao contem", StringComparison.Ordinal) && claim.Contains("nao contem", StringComparison.Ordinal)) ||
            (block.Contains("contem", StringComparison.Ordinal) && claim.Contains("contem", StringComparison.Ordinal));

        return samePrefix && SemanticReconstructionService.ResolveFoodTerms(claim).Any(term => IngredientTextNormalizer.ContainsAny(block, [term]));
    }

    private static void NormalizeRiskTrust(AllergenRiskDto risk, IReadOnlyList<string> explicitClaims, IReadOnlyList<string> explicitIngredientBlocks)
    {
        var source = string.Join("\n", risk.Evidence.DefaultIfEmpty(risk.Source));
        var normalizedSource = IngredientTextNormalizer.Normalize(source);
        var hasExplicitClaim = explicitClaims.Any(block => ContainsSameRegulatoryTerms(block, normalizedSource) || block.Contains(IngredientTextNormalizer.Normalize(risk.Name), StringComparison.Ordinal));
        var hasExplicitIngredient = explicitIngredientBlocks.Any(block => IngredientTextNormalizer.ContainsAny(block, [risk.Name]) || risk.Evidence.Any(evidence => IngredientTextNormalizer.ContainsAny(block, [evidence])));

        if (hasExplicitClaim && risk.EvidenceType != EvidenceType.OpenAiInference)
        {
            risk.TrustLevel = EvidenceTrustLevel.ExplicitRegulatoryClaim;
            risk.OriginBlock = "RegulatoryClaimBlock";
        }
        else if (hasExplicitIngredient && risk.EvidenceType == EvidenceType.IngredientDetected && !IsMilkDerivativeInferenceOnly(source))
        {
            risk.TrustLevel = EvidenceTrustLevel.ExplicitIngredient;
            risk.OriginBlock = "IngredientBlock";
        }
        else
        {
            risk.TrustLevel = risk.EvidenceType == EvidenceType.OpenAiInference ? EvidenceTrustLevel.SemanticInference : EvidenceTrustLevel.WeakInference;
            risk.OriginBlock = string.IsNullOrWhiteSpace(risk.OriginBlock) ? "SemanticInferenceLayer" : risk.OriginBlock;
        }
    }

    private static bool CanRegulatoryRiskStand(AllergenRiskDto risk) =>
        risk.TrustLevel is EvidenceTrustLevel.ExplicitRegulatoryClaim or EvidenceTrustLevel.ExplicitIngredient &&
        risk.RiskType is "contains" or "may_contain" or "cross_contamination";

    private static void ApplyRiskConfidenceCeiling(AllergenRiskDto risk)
    {
        var ceiling = risk.TrustLevel switch
        {
            EvidenceTrustLevel.ExplicitRegulatoryClaim => "high",
            EvidenceTrustLevel.ExplicitIngredient => "medium",
            EvidenceTrustLevel.StructuredText or EvidenceTrustLevel.SemanticInference => "low",
            _ => "very_low"
        };

        if (ConfidenceRank(risk.Confidence) > ConfidenceRank(ceiling))
            risk.Confidence = ceiling;

        foreach (var evidence in risk.SemanticEvidence)
        {
            if (ConfidenceRank(evidence.Confidence) > ConfidenceRank(ceiling))
                evidence.Confidence = ceiling;
            evidence.TrustLevel = risk.TrustLevel;
            evidence.OriginBlock = risk.OriginBlock;
        }
    }

    private static int ConfidenceRank(string confidence) => confidence switch
    {
        "high" => 4,
        "medium" => 3,
        "low" => 2,
        "very_low" => 1,
        _ => 0
    };

    private static SemanticInferenceDto ToSemanticInference(AllergenRiskDto risk)
    {
        var type = ResolveInferenceType(risk);
        return new SemanticInferenceDto
        {
            Type = type,
            Text = risk.Name,
            Confidence = type is "possible_lactose" or "possible_sugar" || risk.EvidenceType == EvidenceType.OpenAiInference || risk.TrustLevel == EvidenceTrustLevel.SemanticInference ? "low" : "medium",
            Source = "semantic_inference",
            OriginBlock = risk.OriginBlock,
            TrustLevel = EvidenceTrustLevel.SemanticInference
        };
    }

    private static string ResolveInferenceType(AllergenRiskDto risk)
    {
        var normalized = IngredientTextNormalizer.Normalize(string.Join(" ", risk.Name, risk.Source, string.Join(" ", risk.Evidence)));
        if (normalized.Contains("leite", StringComparison.Ordinal) || normalized.Contains("lactose", StringComparison.Ordinal)) return "possible_lactose";
        if (normalized.Contains("acucar", StringComparison.Ordinal)) return "possible_sugar";
        return risk.RiskType is "may_contain" or "cross_contamination" ? risk.RiskType : "possible_allergen";
    }

    private static bool IsMilkDerivativeInferenceOnly(string source)
    {
        var normalized = IngredientTextNormalizer.Normalize(source);
        return normalized.Contains("derivado de leite", StringComparison.Ordinal) ||
            normalized.Contains("derivados de leite", StringComparison.Ordinal) ||
            normalized.Contains("pode conter derivados", StringComparison.Ordinal);
    }
}
