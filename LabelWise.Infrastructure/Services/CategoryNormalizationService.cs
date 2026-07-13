using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services;

/// <summary>
/// Serviço para normalizar categorias detectadas pela IA para categorias normalizadas do banco.
/// Usa sinais textuais genéricos vindos do nome do produto, categoria, claims e marca.
/// </summary>
public class CategoryNormalizationService : ICategoryNormalizationService
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "de", "da", "do", "das", "dos", "e", "em", "com", "sem", "para", "por", "the", "and"
    };

    private readonly ICategoryMappingRepository _mappingRepository;
    private readonly ILogger<CategoryNormalizationService> _logger;

    public CategoryNormalizationService(
        ICategoryMappingRepository mappingRepository,
        ILogger<CategoryNormalizationService> logger)
    {
        _mappingRepository = mappingRepository;
        _logger = logger;
    }

    public async Task<CategoryNormalizationResult> NormalizeAsync(
        string? detectedCategory,
        string? productName,
        IEnumerable<string>? visibleClaims = null,
        string? brand = null)
    {
        try
        {
            var signals = BuildSignals(detectedCategory, productName, visibleClaims, brand);
            if (signals.Count == 0)
            {
                return CategoryNormalizationResult.Unknown("unknown");
            }

            var allMappings = await _mappingRepository.GetAllActiveAsync();
            if (allMappings.Count == 0)
            {
                _logger.LogWarning("No active category mappings found while normalizing category.");
                return CategoryNormalizationResult.Unknown(string.Join(" | ", signals.Select(s => s.RawText)));
            }

            var rankedCandidates = allMappings
                .Select(mapping => new
                {
                    Mapping = mapping,
                    Score = CalculateScore(mapping.RawCategoryName, signals, (double)mapping.Confidence),
                    Evidence = BuildEvidence(mapping.RawCategoryName, signals)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Mapping.Confidence)
                .ToList();

            if (rankedCandidates.Count == 0)
            {
                _logger.LogWarning(
                    "Could not normalize category. DetectedCategory={DetectedCategory}, ProductName={ProductName}, Brand={Brand}",
                    detectedCategory,
                    productName,
                    brand);

                return CategoryNormalizationResult.Unknown(string.Join(" | ", signals.Select(s => s.RawText)));
            }

            var aggregatedByCategory = rankedCandidates
                .GroupBy(x => x.Mapping.NormalizedCategoryCode)
                .Select(group => new
                {
                    CategoryCode = group.Key,
                    CategoryName = group.First().Mapping.NormalizedCategory?.Name,
                    Best = group.First(),
                    Score = group.Max(x => x.Score) + (group.Skip(1).Sum(x => x.Score) * 0.15),
                    Evidence = group.SelectMany(x => x.Evidence).Distinct().Take(5).ToArray()
                })
                .OrderByDescending(x => x.Score)
                .ToList();

            var winner = aggregatedByCategory[0];
            var runnerUp = aggregatedByCategory.Skip(1).FirstOrDefault();
            var ambiguous = runnerUp != null && Math.Abs(winner.Score - runnerUp.Score) < 0.12;
            var confidence = Math.Clamp(winner.Score, 0.0, 1.0);

            if (confidence < 0.45)
            {
                return CategoryNormalizationResult.Unknown(string.Join(" | ", signals.Select(s => s.RawText)));
            }

            var result = new CategoryNormalizationResult
            {
                IsNormalized = true,
                NormalizedCategoryCode = winner.CategoryCode,
                NormalizedCategoryName = winner.CategoryName,
                RawInput = string.Join(" | ", signals.Select(s => s.RawText)),
                MatchedAlias = winner.Best.Mapping.RawCategoryName,
                Confidence = ambiguous ? Math.Max(0.35, confidence - 0.15) : confidence,
                MatchType = DetermineMatchType(winner.Best.Mapping.RawCategoryName, signals, ambiguous),
                Evidence = winner.Evidence,
                CandidateCategories = aggregatedByCategory.Take(3).Select(c => c.CategoryCode).ToArray(),
                IsAmbiguous = ambiguous
            };

            _logger.LogInformation(
                "Category normalization resolved DetectedCategory={DetectedCategory}, ProductName={ProductName}, Normalized={NormalizedCategory}, Confidence={Confidence}, Evidence={Evidence}",
                detectedCategory,
                productName,
                result.NormalizedCategoryCode,
                result.Confidence,
                string.Join("; ", result.Evidence));

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error normalizing category: {Category}", detectedCategory);
            return CategoryNormalizationResult.Unknown(detectedCategory ?? productName ?? "error");
        }
    }

    private static List<CategorySignal> BuildSignals(
        string? detectedCategory,
        string? productName,
        IEnumerable<string>? visibleClaims,
        string? brand)
    {
        var signals = new List<CategorySignal>();

        AddSignal(signals, detectedCategory, 0.90, "category");
        AddSignal(signals, productName, 1.00, "productName");

        if (visibleClaims != null)
        {
            foreach (var claim in visibleClaims.Where(c => !string.IsNullOrWhiteSpace(c)))
            {
                AddSignal(signals, claim, 0.55, "visibleClaim");
            }
        }

        AddSignal(signals, brand, 0.15, "brand");
        return signals;
    }

    private static void AddSignal(List<CategorySignal> signals, string? text, double weight, string source)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        signals.Add(new CategorySignal(text.Trim(), Normalize(text), weight, source));
    }

    private static double CalculateScore(string alias, IReadOnlyCollection<CategorySignal> signals, double mappingConfidence)
    {
        var normalizedAlias = Normalize(alias);
        if (string.IsNullOrWhiteSpace(normalizedAlias))
        {
            return 0;
        }

        var aliasTokens = Tokenize(normalizedAlias);
        var bestSignalScore = 0.0;
        var combinedTokenScore = 0.0;

        foreach (var signal in signals)
        {
            var exactScore = signal.NormalizedText == normalizedAlias ? 1.00 : 0.0;
            var containsScore = signal.NormalizedText.Contains(normalizedAlias, StringComparison.Ordinal) ||
                                normalizedAlias.Contains(signal.NormalizedText, StringComparison.Ordinal)
                ? 0.78
                : 0.0;

            var signalTokens = Tokenize(signal.NormalizedText);
            var tokenOverlap = aliasTokens.Count == 0
                ? 0.0
                : aliasTokens.Intersect(signalTokens).Count() / (double)aliasTokens.Count;

            var weighted = Math.Max(exactScore, Math.Max(containsScore, tokenOverlap * 0.82)) * signal.Weight;
            bestSignalScore = Math.Max(bestSignalScore, weighted);
            combinedTokenScore += tokenOverlap * signal.Weight * 0.15;
        }

        return Math.Min(1.0, (bestSignalScore * 0.75) + combinedTokenScore + (mappingConfidence * 0.20));
    }

    private static string[] BuildEvidence(string alias, IEnumerable<CategorySignal> signals)
    {
        var normalizedAlias = Normalize(alias);
        var aliasTokens = Tokenize(normalizedAlias);
        var evidence = new List<string>();

        foreach (var signal in signals)
        {
            if (signal.NormalizedText == normalizedAlias)
            {
                evidence.Add($"{signal.Source}: exact match '{signal.RawText}'");
                continue;
            }

            var overlap = aliasTokens.Intersect(Tokenize(signal.NormalizedText)).ToArray();
            if (overlap.Length > 0)
            {
                evidence.Add($"{signal.Source}: tokens [{string.Join(", ", overlap)}]");
            }
        }

        return evidence.Distinct().ToArray();
    }

    private static string DetermineMatchType(string alias, IEnumerable<CategorySignal> signals, bool ambiguous)
    {
        var normalizedAlias = Normalize(alias);
        if (signals.Any(s => s.NormalizedText == normalizedAlias))
        {
            return ambiguous ? "exact_but_ambiguous" : "exact";
        }

        return ambiguous ? "contextual_ambiguous" : "contextual";
    }

    private static string Normalize(string text)
    {
        var formD = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(formD.Length);

        foreach (var ch in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        var withoutAccents = builder.ToString().Normalize(NormalizationForm.FormC);
        return Regex.Replace(withoutAccents, "[^a-z0-9]+", " ").Trim();
    }

    private static HashSet<string> Tokenize(string text)
    {
        return text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length > 2 && !StopWords.Contains(token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record CategorySignal(string RawText, string NormalizedText, double Weight, string Source);
}
