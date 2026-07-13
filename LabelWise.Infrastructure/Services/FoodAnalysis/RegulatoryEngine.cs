using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;
using LabelWise.Domain.Models;
using LabelWise.Infrastructure.Services.IngredientAnalysis;
using System.Text.RegularExpressions;

namespace LabelWise.Infrastructure.Services.FoodAnalysis;

/// <summary>
/// Engine regulatória para detecção e interpretação de claims oficiais.
/// Claims regulatórios têm PRIORIDADE ABSOLUTA sobre qualquer inferência.
/// </summary>
public sealed class RegulatoryEngine : IRegulatoryEngine
{
    private static readonly Regex ContainsPattern = new(
        @"\bCONT[ÉE]M\s+(?:INGREDIENTES\s+)?(?:AL[ÉE]RG[ÊE]NICOS\s*:\s*)?(.+?)(?:\.|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MayContainPattern = new(
        @"\bPODE\s+CONTER\s+(?:TRA[ÇC]OS\s+DE\s+)?(.+?)(?:\.|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FreeFromPattern = new(
        @"\b(?:SEM|ZERO|N[ÃA]O\s+CONT[ÉE]M)\s+(.+?)(?:\.|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CrossContaminationPattern = new(
        @"\bFABRICADO\s+EM\s+(?:EQUIPAMENTO|LINHA)\s+QUE\s+PROCESSA\s+(.+?)(?:\.|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TracesPattern = new(
        @"\bTRA[ÇC]OS\s+DE\s+(.+?)(?:\.|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<IReadOnlyList<RegulatoryClaim>> DetectClaimsAsync(
        string ocrText,
        IReadOnlyList<string>? ocrBlocks = null)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
            return Array.Empty<RegulatoryClaim>();

        var claims = new List<RegulatoryClaim>();
        var normalizedText = IngredientTextNormalizer.Normalize(ocrText);

        // Detectar "CONTÉM"
        claims.AddRange(DetectClaimsByPattern(
            normalizedText,
            ContainsPattern,
            RegulatoryClaimType.Contains,
            isPositive: true,
            isAbsolute: true,
            ocrText));

        // Detectar "PODE CONTER"
        claims.AddRange(DetectClaimsByPattern(
            normalizedText,
            MayContainPattern,
            RegulatoryClaimType.MayContain,
            isPositive: true,
            isAbsolute: false,
            ocrText));

        // Detectar "SEM / ZERO / NÃO CONTÉM"
        claims.AddRange(DetectClaimsByPattern(
            normalizedText,
            FreeFromPattern,
            RegulatoryClaimType.FreeFrom,
            isPositive: false,
            isAbsolute: true,
            ocrText));

        // Detectar contaminação cruzada
        claims.AddRange(DetectClaimsByPattern(
            normalizedText,
            CrossContaminationPattern,
            RegulatoryClaimType.CrossContamination,
            isPositive: true,
            isAbsolute: false,
            ocrText));

        // Detectar "TRAÇOS DE"
        claims.AddRange(DetectClaimsByPattern(
            normalizedText,
            TracesPattern,
            RegulatoryClaimType.MayContain,
            isPositive: true,
            isAbsolute: false,
            ocrText));

        // Deduplicar baseado em sujeito normalizado
        var deduplicated = claims
            .GroupBy(c => (c.Subject, c.ClaimType, c.IsPositiveClaim))
            .Select(g => g.OrderByDescending(c => c.Confidence).First())
            .ToList();

        return deduplicated;
    }

    public bool ValidateClaim(string claimText)
    {
        if (string.IsNullOrWhiteSpace(claimText))
            return false;

        var normalized = IngredientTextNormalizer.Normalize(claimText);

        return ContainsPattern.IsMatch(normalized) ||
               MayContainPattern.IsMatch(normalized) ||
               FreeFromPattern.IsMatch(normalized) ||
               CrossContaminationPattern.IsMatch(normalized) ||
               TracesPattern.IsMatch(normalized);
    }

    public RegulatoryClaimType ClassifyClaimType(string claimText)
    {
        var normalized = IngredientTextNormalizer.Normalize(claimText);

        if (ContainsPattern.IsMatch(normalized))
            return RegulatoryClaimType.Contains;
        if (MayContainPattern.IsMatch(normalized) || TracesPattern.IsMatch(normalized))
            return RegulatoryClaimType.MayContain;
        if (FreeFromPattern.IsMatch(normalized))
            return RegulatoryClaimType.FreeFrom;
        if (CrossContaminationPattern.IsMatch(normalized))
            return RegulatoryClaimType.CrossContamination;

        return RegulatoryClaimType.Unknown;
    }

    public string ExtractClaimSubject(string claimText)
    {
        var normalized = IngredientTextNormalizer.Normalize(claimText);

        // Tentar extrair de cada padrão
        var patterns = new[] { ContainsPattern, MayContainPattern, FreeFromPattern, CrossContaminationPattern, TracesPattern };
        
        foreach (var pattern in patterns)
        {
            var match = pattern.Match(normalized);
            if (match.Success && match.Groups.Count > 1)
            {
                var subject = match.Groups[1].Value.Trim();
                return NormalizeSubject(subject);
            }
        }

        return normalized;
    }

    public bool IsAbsoluteClaim(RegulatoryClaimType claimType)
    {
        return claimType is 
            RegulatoryClaimType.Contains or 
            RegulatoryClaimType.FreeFrom or 
            RegulatoryClaimType.Certified or 
            RegulatoryClaimType.Prohibited;
    }

    private IEnumerable<RegulatoryClaim> DetectClaimsByPattern(
        string normalizedText,
        Regex pattern,
        RegulatoryClaimType claimType,
        bool isPositive,
        bool isAbsolute,
        string originalText)
    {
        var matches = pattern.Matches(normalizedText);
        
        foreach (Match match in matches)
        {
            if (!match.Success || match.Groups.Count < 2)
                continue;

            var subject = match.Groups[1].Value.Trim();
            var normalizedSubject = NormalizeSubject(subject);

            // Extrair texto original correspondente
            var originalMatch = FindOriginalText(originalText, match.Value);

            var evidence = new Evidence
            {
                Type = claimType.ToString(),
                Text = originalMatch,
                Source = "regulatory_claim_detection",
                Priority = EvidencePriority.RegulatoryClaimExplicit,
                Confidence = 0.95,
                OriginBlock = "RegulatoryClaimBlock"
            };

            yield return new RegulatoryClaim
            {
                OriginalText = originalMatch,
                NormalizedText = match.Value,
                ClaimType = claimType,
                Subject = normalizedSubject,
                IsPositiveClaim = isPositive,
                IsAbsolute = isAbsolute,
                Evidence = evidence,
                Confidence = 0.95
            };
        }
    }

    private static string NormalizeSubject(string subject)
    {
        // Remover pontuações e normalizar
        var normalized = subject
            .Replace(".", "")
            .Replace(",", "")
            .Trim()
            .ToLowerInvariant();

        // Mapear para termos canônicos
        var mappings = new Dictionary<string, string>
        {
            { "leite e derivados", "leite" },
            { "derivados de leite", "leite" },
            { "derivados do leite", "leite" },
            { "lactose", "lactose" },
            { "gluten", "glúten" },
            { "trigo", "glúten" },
            { "soja", "soja" },
            { "ovos", "ovo" },
            { "amendoim", "amendoim" },
            { "castanhas", "castanha" },
            { "peixe", "peixe" },
            { "crustaceos", "crustáceo" },
            { "acucar", "açúcar" }
        };

        foreach (var (key, value) in mappings)
        {
            if (normalized.Contains(key))
                return value;
        }

        return normalized;
    }

    private static string FindOriginalText(string originalText, string normalizedMatch)
    {
        // Tentar encontrar a correspondência no texto original (case-insensitive)
        var index = originalText.IndexOf(normalizedMatch, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            return originalText.Substring(index, normalizedMatch.Length);
        }

        return normalizedMatch;
    }
}
