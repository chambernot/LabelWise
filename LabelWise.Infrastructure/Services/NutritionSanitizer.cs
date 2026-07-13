using System.Globalization;
using System.Text.RegularExpressions;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services;

public sealed class NutritionSanitizer : INutritionSanitizer
{
    private static readonly Regex PackageWeightRegex = new(
        @"(?<!\d)(\d{1,4}(?:[\.,]\d{1,2})?)\s*(kg|g|ml|l)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] PackagingKeywords =
    [
        "peso", "líquido", "liquido", "conteúdo", "conteudo", "volume",
        "embalagem", "pacote", "frasco", "garrafa", "lata", "sachê", "sache"
    ];

    private readonly ILogger<NutritionSanitizer> _logger;

    public NutritionSanitizer(ILogger<NutritionSanitizer> logger)
    {
        _logger = logger;
    }

    public SanitizationResult<NutritionAnalysisResponseDto> Sanitize(
        NutritionAnalysisResponseDto response,
        NutritionSanitizationContext? context = null)
    {
        if (response == null)
        {
            return SanitizationResult<NutritionAnalysisResponseDto>.Failure("A resposta nutricional não pode ser nula.");
        }

        response.VisibleClaims ??= new List<string>();
        response.Warnings ??= new List<string>();
        response.ConfidenceDetails ??= new ConfidenceDetailsDto();
        response.EstimatedNutritionProfile ??= new EstimatedNutritionProfileDto();

        var referenceRange = context?.ReferenceCatalog?.Resolve(response.Category, response.ProductName, response.VisibleClaims)
            ?? NutritionReferenceRanges.Resolve(response.Category, response.ProductName, response.VisibleClaims);

        var nutritionChanged = SanitizeNutritionProfile(response, referenceRange);
        var weightChanged = SanitizePackageWeight(response, context);

        if (nutritionChanged || weightChanged)
        {
            RecalculatePackageCalories(response);
        }

        return SanitizationResult<NutritionAnalysisResponseDto>.Success(response);
    }

    private bool SanitizeNutritionProfile(NutritionAnalysisResponseDto response, NutritionReferenceRange referenceRange)
    {
        var profile = response.EstimatedNutritionProfile;
        if (profile == null)
        {
            return false;
        }

        // CORREÇÃO CRÍTICA: Verificar se há leitura real da tabela nutricional
        // Se analysisMode é FullNutritionLabel e basis indica leitura real, ser MUITO MENOS agressivo na sanitização
        var isRealTableReading = IsRealNutritionTableReading(response, profile);

        var changed = false;

        changed |= SanitizeMetric(response, profile, referenceRange.DisplayName, "calorias", referenceRange.CaloriesPer100g, () => profile.CaloriesPer100g, value => profile.CaloriesPer100g = value, isRealTableReading, _logger);
        changed |= SanitizeMetric(response, profile, referenceRange.DisplayName, "açúcar", referenceRange.SugarPer100g, () => profile.EstimatedSugarPer100g, value => profile.EstimatedSugarPer100g = value, isRealTableReading, _logger);
        changed |= SanitizeMetric(response, profile, referenceRange.DisplayName, "proteína", referenceRange.ProteinPer100g, () => profile.EstimatedProteinPer100g, value => profile.EstimatedProteinPer100g = value, isRealTableReading, _logger);
        changed |= SanitizeMetric(response, profile, referenceRange.DisplayName, "gordura", referenceRange.FatPer100g, () => profile.EstimatedFatPer100g, value => profile.EstimatedFatPer100g = value, isRealTableReading, _logger);
        changed |= SanitizeMetric(response, profile, referenceRange.DisplayName, "sódio", referenceRange.SodiumPer100g, () => profile.EstimatedSodiumPer100g, value => profile.EstimatedSodiumPer100g = value, isRealTableReading, _logger);
        changed |= SanitizeMetric(response, profile, referenceRange.DisplayName, "fibra", referenceRange.FiberPer100g, () => profile.EstimatedFiberPer100g, value => profile.EstimatedFiberPer100g = value, isRealTableReading, _logger);

        if (!changed)
        {
            return false;
        }

        // Se houve sanitização em leitura real, não marcar confiança como baixa (pois os dados são reais)
        if (!isRealTableReading)
        {
            MarkNutritionConfidenceLow(response);
        }

        profile.Basis = AppendSanitizationSuffix(profile.Basis, referenceRange.DisplayName);

        _logger.LogInformation(
            "Nutrition profile sanitized for category '{Category}' using reference range '{ReferenceRange}' (RealTable: {IsReal})",
            response.Category ?? "unknown",
            referenceRange.Key,
            isRealTableReading);

        return true;
    }

    /// <summary>
    /// Determina se os dados vêm de leitura real da tabela nutricional.
    /// </summary>
    private static bool IsRealNutritionTableReading(NutritionAnalysisResponseDto response, EstimatedNutritionProfileDto profile)
    {
        // Se o modo de análise é FrontOfPackageOnly, definitivamente não é leitura real
        if (response.AnalysisMode == Domain.Enums.AnalysisMode.FrontOfPackageOnly)
        {
            return false;
        }

        // Verificar se o basis indica leitura real
        if (!string.IsNullOrWhiteSpace(profile.Basis))
        {
            var basisLower = profile.Basis.ToLowerInvariant();

            // Indicadores de leitura real
            if (basisLower.Contains("leitura") && 
                (basisLower.Contains("tabela") || basisLower.Contains("nutricional")))
            {
                return true;
            }

            if (basisLower.Contains("extraído") || 
                basisLower.Contains("identificado") ||
                basisLower.Contains("detectado"))
            {
                return true;
            }

            // Indicadores de estimativa (NÃO é leitura real)
            if (basisLower.Contains("estimativa") || 
                basisLower.Contains("categoria") ||
                basisLower.Contains("média") ||
                basisLower.Contains("inferência"))
            {
                return false;
            }
        }

        // Se temos FullNutritionLabel mas basis não deixa claro, contar campos preenchidos
        int filledFields = 0;
        if (profile.CaloriesPer100g.HasValue) filledFields++;
        if (profile.EstimatedSugarPer100g.HasValue) filledFields++;
        if (profile.EstimatedProteinPer100g.HasValue) filledFields++;
        if (profile.EstimatedSodiumPer100g.HasValue) filledFields++;
        if (profile.EstimatedFatPer100g.HasValue) filledFields++;

        // Se temos 3+ campos preenchidos com FullNutritionLabel, provavelmente é leitura real
        return filledFields >= 3;
    }

    private static bool SanitizeMetric(
        NutritionAnalysisResponseDto response,
        EstimatedNutritionProfileDto profile,
        string referenceDisplayName,
        string metricName,
        NutritionMetricRange range,
        Func<double?> getter,
        Action<double?> setter,
        bool isRealTableReading,
        ILogger<NutritionSanitizer> logger)
    {
        var originalValue = getter();
        if (!originalValue.HasValue)
        {
            return false;
        }

        var value = originalValue.Value;

        // Se for NaN, infinito ou negativo, corrigir sempre
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
        {
            setter(0);
            return true;
        }

        // CORREÇÃO CRÍTICA: Se é leitura real da tabela, ser MUITO mais tolerante
        if (isRealTableReading)
        {
            // Para leitura real, apenas validar se o valor não é absurdamente fora do razoável
            // Expandir range em 3x para dar margem a produtos especiais
            var expandedMinimum = Math.Max(0, range.Minimum - (range.Maximum - range.Minimum) * 1.0);
            var expandedMaximum = range.Maximum + (range.Maximum - range.Minimum) * 2.0;

            if (value >= expandedMinimum && value <= expandedMaximum)
            {
                // Valor plausível para leitura real - manter sem alteração
                setter(RoundMetric(metricName, value));
                return false;
            }

            // Valor extremamente fora do esperado mesmo com margem - registrar warning mas NÃO substituir
            logger.LogWarning(
                "Valor real de {Metric} ({Value}) está fora até do range expandido para {Category}, mas será mantido por ser leitura real da tabela",
                metricName,
                value,
                referenceDisplayName);

            setter(RoundMetric(metricName, value));
            AddWarning(
                response,
                $"Valor de {metricName} ({value:0.#}) está fora do esperado para {referenceDisplayName}, mas foi mantido por ter sido extraído da tabela nutricional.");

            return false; // Não substituir
        }

        // Para estimativas (FrontOfPackageOnly), aplicar validação normal
        if (range.Contains(value))
        {
            setter(RoundMetric(metricName, value));
            return false;
        }

        var replacement = RoundMetric(metricName, range.Average);
        setter(replacement);

        AddWarning(
            response,
            $"Valor estimado de {metricName} fora da faixa esperada para {referenceDisplayName}; substituído pela média segura da categoria.");

        return true;
    }

    private static void MarkNutritionConfidenceLow(NutritionAnalysisResponseDto response)
    {
        response.ConfidenceDetails ??= new ConfidenceDetailsDto();

        var current = response.ConfidenceDetails.EstimatedNutritionProfile;
        response.ConfidenceDetails.EstimatedNutritionProfile = current > 0 && current < 0.25
            ? Math.Round(current, 2)
            : 0.25;
    }

    private bool SanitizePackageWeight(NutritionAnalysisResponseDto response, NutritionSanitizationContext? context)
    {
        var bestEvidence = SelectBestEvidenceCandidate(response, context);
        var hasCurrentWeight = TryParsePackageWeight(response.PackageWeight, out var currentWeight);

        if (hasCurrentWeight && currentWeight != null && !IsCommonMarketSize(currentWeight))
        {
            if (bestEvidence != null && !AreEquivalent(currentWeight, bestEvidence))
            {
                response.PackageWeight = bestEvidence.DisplayValue;
                AddWarning(response, $"Peso da embalagem corrigido com base em evidência textual detectada na imagem: {bestEvidence.DisplayValue}.");
                return true;
            }

            response.PackageWeight = null;
            AddWarning(response, "Peso da embalagem removido por estar fora de um padrão plausível de mercado.");
            return true;
        }

        if (!hasCurrentWeight || currentWeight == null)
        {
            if (bestEvidence == null)
            {
                if (!string.IsNullOrWhiteSpace(response.PackageWeight))
                {
                    response.PackageWeight = null;
                    AddWarning(response, "Peso da embalagem removido porque o formato da unidade não pôde ser validado.");
                    return true;
                }

                return false;
            }

            response.PackageWeight = bestEvidence.DisplayValue;
            AddWarning(response, $"Peso da embalagem preenchido com base em evidência textual detectada na imagem: {bestEvidence.DisplayValue}.");
            return true;
        }

        response.PackageWeight = currentWeight.DisplayValue;

        if (bestEvidence == null || AreEquivalent(currentWeight, bestEvidence))
        {
            return false;
        }

        if (IsDefaultReferenceWeight(currentWeight) || currentWeight.Unit != bestEvidence.Unit)
        {
            response.PackageWeight = bestEvidence.DisplayValue;
            AddWarning(response, $"Peso da embalagem ajustado para priorizar a unidade explicitamente detectada na imagem: {bestEvidence.DisplayValue}.");
            return true;
        }

        return false;
    }

    private static void RecalculatePackageCalories(NutritionAnalysisResponseDto response)
    {
        var profile = response.EstimatedNutritionProfile;
        if (profile?.CaloriesPer100g == null || !TryParsePackageWeight(response.PackageWeight, out var weight) || weight == null)
        {
            return;
        }

        var normalizedAmount = weight.Unit is "kg" or "l"
            ? weight.Value * 1000
            : weight.Value;

        var recalculatedCalories = Math.Round(profile.CaloriesPer100g.Value * (normalizedAmount / 100d), 1);

        if (profile.EstimatedPackageCalories == null || Math.Abs(profile.EstimatedPackageCalories.Value - recalculatedCalories) > 5)
        {
            profile.EstimatedPackageCalories = recalculatedCalories;
        }
    }

    private PackageWeightCandidate? SelectBestEvidenceCandidate(NutritionAnalysisResponseDto response, NutritionSanitizationContext? context)
    {
        var candidates = ExtractEvidenceCandidates(response, context)
            .GroupBy(candidate => $"{candidate.Unit}:{candidate.Value.ToString("0.##", CultureInfo.InvariantCulture)}")
            .Select(group => group.OrderByDescending(candidate => candidate.Score).First())
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => !IsDefaultReferenceWeight(candidate))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        var nonDefaultCandidate = candidates.FirstOrDefault(candidate => !IsDefaultReferenceWeight(candidate));
        return nonDefaultCandidate ?? candidates[0];
    }

    private static IEnumerable<PackageWeightCandidate> ExtractEvidenceCandidates(
        NutritionAnalysisResponseDto response,
        NutritionSanitizationContext? context)
    {
        var sources = new (string Name, string? Text, int BaseScore)[]
        {
            ("raw", context?.RawModelResponseText, 3),
            ("summary", response.Summary, 2),
            ("product", response.ProductName, 1),
            ("claims", response.VisibleClaims.Count == 0 ? null : string.Join(' ', response.VisibleClaims), 1)
        };

        foreach (var source in sources)
        {
            if (string.IsNullOrWhiteSpace(source.Text))
            {
                continue;
            }

            foreach (Match match in PackageWeightRegex.Matches(source.Text))
            {
                if (!TryCreateCandidate(match, source.Text, source.BaseScore, out var candidate))
                {
                    continue;
                }

                yield return candidate;
            }
        }
    }

    private static bool TryCreateCandidate(Match match, string sourceText, int baseScore, out PackageWeightCandidate? candidate)
    {
        candidate = null;

        if (match.Groups.Count < 3)
        {
            return false;
        }

        var normalizedNumber = match.Groups[1].Value.Replace(',', '.');
        if (!double.TryParse(normalizedNumber, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            return false;
        }

        var unit = match.Groups[2].Value.ToLowerInvariant();
        var score = baseScore;

        var surroundingText = sourceText[Math.Max(0, match.Index - 24)..Math.Min(sourceText.Length, match.Index + match.Length + 24)].ToLowerInvariant();
        if (PackagingKeywords.Any(keyword => surroundingText.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            score += 2;
        }

        if (!IsDefaultReferenceWeight(value, unit))
        {
            score += 1;
        }

        var parsedCandidate = new PackageWeightCandidate(value, unit, FormatWeight(value, unit), score);
        if (!IsCommonMarketSize(parsedCandidate))
        {
            return false;
        }

        candidate = parsedCandidate;
        return true;
    }

    private static bool TryParsePackageWeight(string? packageWeight, out PackageWeightCandidate? candidate)
    {
        candidate = null;
        if (string.IsNullOrWhiteSpace(packageWeight))
        {
            return false;
        }

        var match = PackageWeightRegex.Match(packageWeight);
        if (!match.Success)
        {
            return false;
        }

        return TryCreateCandidate(match, packageWeight, 0, out candidate);
    }

    private static bool AreEquivalent(PackageWeightCandidate left, PackageWeightCandidate right)
    {
        return left.Unit == right.Unit && Math.Abs(left.Value - right.Value) < 0.01;
    }

    private static bool IsCommonMarketSize(PackageWeightCandidate candidate)
    {
        return candidate.Unit switch
        {
            "g" or "ml" => candidate.Value >= 5 && candidate.Value <= 5000,
            "kg" => candidate.Value >= 0.05 && candidate.Value <= 10,
            "l" => candidate.Value >= 0.05 && candidate.Value <= 5,
            _ => false
        };
    }

    private static bool IsDefaultReferenceWeight(PackageWeightCandidate candidate)
    {
        return IsDefaultReferenceWeight(candidate.Value, candidate.Unit);
    }

    private static bool IsDefaultReferenceWeight(double value, string unit)
    {
        return unit switch
        {
            "g" or "ml" => Math.Abs(value - 100) < 0.01,
            "kg" or "l" => Math.Abs(value - 1) < 0.01,
            _ => false
        };
    }

    private static string FormatWeight(double value, string unit)
    {
        var roundedValue = value % 1 == 0
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);

        return $"{roundedValue} {unit}";
    }

    private static string AppendSanitizationSuffix(string? basis, string referenceDisplayName)
    {
        var suffix = $"Dados higienizados por faixa de referência de {referenceDisplayName}.";
        if (string.IsNullOrWhiteSpace(basis))
        {
            return suffix;
        }

        if (basis.Contains(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return basis;
        }

        return $"{basis.TrimEnd('.', ' ')}. {suffix}";
    }

    private static double RoundMetric(string metricName, double value)
    {
        return metricName is "calorias" or "sódio"
            ? Math.Round(value, 0)
            : Math.Round(value, 1);
    }

    private static void AddWarning(NutritionAnalysisResponseDto response, string warning)
    {
        if (string.IsNullOrWhiteSpace(warning))
        {
            return;
        }

        if (response.Warnings.Any(existing => string.Equals(existing, warning, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        response.Warnings.Add(warning);
    }

    private sealed record PackageWeightCandidate(double Value, string Unit, string DisplayValue, int Score);
}
