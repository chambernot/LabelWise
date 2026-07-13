using System.Collections.Generic;
using System.Text.RegularExpressions;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services;

public sealed class NutritionAutoCorrector : INutritionAutoCorrector
{
    private static readonly Regex NumberRegex = new(@"\d+(?:[\.,]\d+)?", RegexOptions.Compiled);

    private readonly INutritionConsistencyValidator _validator;
    private readonly ILogger<NutritionAutoCorrector> _logger;

    public NutritionAutoCorrector(
        INutritionConsistencyValidator validator,
        ILogger<NutritionAutoCorrector> logger)
    {
        _validator = validator;
        _logger = logger;
    }

    public EstimatedNutritionProfileDto AutoCorrect(EstimatedNutritionProfileDto profile, List<string> rawText)
    {
        if (profile == null || rawText == null || rawText.Count == 0)
            return profile;

        var initialValidation = _validator.Validate(profile);
        if (initialValidation.IsValid)
            return profile;

        var table = ParseTable(rawText);
        if (table.Count == 0)
            return profile;

        var columns = NutritionColumnParser.DetectColumns(rawText);
        var targetIndex = ResolveTargetColumnIndex(profile, columns, table);
        if (!targetIndex.HasValue)
            return profile;

        var corrected = Clone(profile);

        ApplyIfPresent(corrected, table, "calories", targetIndex.Value, value =>
        {
            if (IsMlBasis(profile.Basis, profile.NutritionUnit, columns))
            {
                corrected.CaloriesPer100ml = value;
                corrected.CaloriesPer100g = null;
                corrected.NutritionUnit = "ml";
            }
            else
            {
                corrected.CaloriesPer100g = value;
                corrected.CaloriesPer100ml = null;
                corrected.NutritionUnit = "g";
            }
        });

        ApplyIfPresent(corrected, table, "carbs", targetIndex.Value, v => corrected.EstimatedCarbsPer100g = v);
        ApplyIfPresent(corrected, table, "sugar", targetIndex.Value, v => corrected.EstimatedSugarPer100g = v);
        ApplyIfPresent(corrected, table, "added_sugar", targetIndex.Value, v => corrected.EstimatedAddedSugarPer100g = v);
        ApplyIfPresent(corrected, table, "protein", targetIndex.Value, v => corrected.EstimatedProteinPer100g = v);
        ApplyIfPresent(corrected, table, "fat", targetIndex.Value, v => corrected.EstimatedFatPer100g = v);
        ApplyIfPresent(corrected, table, "saturated_fat", targetIndex.Value, v => corrected.EstimatedSaturatedFatPer100g = v);
        ApplyIfPresent(corrected, table, "fiber", targetIndex.Value, v => corrected.EstimatedFiberPer100g = v);
        ApplyIfPresent(corrected, table, "sodium", targetIndex.Value, v => corrected.EstimatedSodiumPer100g = v);

        if (corrected.NutritionUnit == "ml")
            corrected.Basis = "100 ml (produto preparado)";
        else
            corrected.Basis = "100 g";

        if (!HasMinimumConfidenceForReplacement(corrected, table, targetIndex.Value))
            return profile;

        var correctedValidation = _validator.Validate(corrected);
        if (!correctedValidation.IsValid)
            return profile;

        _logger.LogWarning("AUTO-CORREÇÃO aplicada: coluna reconstruída via OCR");
        return corrected;
    }

    public Dictionary<string, List<double>> ParseTable(List<string> rawText)
    {
        var parsed = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in rawText)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var normalized = NutritionColumnParser.NormalizeToken(line);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            var label = MapLabel(normalized);
            if (label == null)
                continue;

            var values = NumberRegex.Matches(normalized)
                .Select(m => double.TryParse(m.Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v)
                    ? (double?)v
                    : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            if (values.Count == 0)
                continue;

            parsed[label] = values;
        }

        return parsed;
    }

    private static int? ResolveTargetColumnIndex(
        EstimatedNutritionProfileDto profile,
        NutritionColumnParser.NutritionColumns columns,
        Dictionary<string, List<double>> table)
    {
        var basis = profile.Basis?.ToLowerInvariant() ?? string.Empty;

        if (basis.Contains("100 ml") && columns.Per100mlIndex.HasValue)
            return columns.Per100mlIndex.Value;

        if (basis.Contains("100 g") && columns.Per100gIndex.HasValue)
            return columns.Per100gIndex.Value;

        var portionMatch = Regex.Match(basis, @"(\d+)\s*g");
        if (portionMatch.Success && columns.PortionIndex.HasValue)
            return columns.PortionIndex.Value;

        var currentCalories = profile.CaloriesPer100g ?? profile.CaloriesPer100ml;
        if (currentCalories.HasValue)
        {
            if (currentCalories.Value >= 250 && columns.Per100gIndex.HasValue)
                return columns.Per100gIndex.Value;

            if (currentCalories.Value <= 180 && columns.PortionIndex.HasValue)
                return columns.PortionIndex.Value;
        }

        if (columns.Per100mlIndex.HasValue)
            return columns.Per100mlIndex.Value;

        if (columns.Per100gIndex.HasValue)
            return columns.Per100gIndex.Value;

        if (columns.PortionIndex.HasValue)
            return columns.PortionIndex.Value;

        return InferByEnergySpread(table);
    }

    private static int? InferByEnergySpread(Dictionary<string, List<double>> table)
    {
        if (!table.TryGetValue("calories", out var energyValues) || energyValues.Count == 0)
            return null;

        var max = energyValues.Max();
        var min = energyValues.Min();

        if (max >= 250)
            return energyValues.IndexOf(max);

        if (min <= 180)
            return energyValues.IndexOf(min);

        return 0;
    }

    private static bool IsMlBasis(string? basis, string? unit, NutritionColumnParser.NutritionColumns columns)
    {
        if (string.Equals(unit, "ml", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(basis) && basis.Contains("100 ml", StringComparison.OrdinalIgnoreCase))
            return true;

        return columns.Per100mlIndex.HasValue && !columns.Per100gIndex.HasValue;
    }

    private static string? MapLabel(string normalized)
    {
        if (normalized.Contains("energ")) return "calories";
        if (normalized.Contains("carbo")) return "carbs";
        if (normalized.Contains("acucar") || normalized.Contains("acucares"))
        {
            if (normalized.Contains("adicionado") || normalized.Contains("adicionados"))
                return "added_sugar";

            return "sugar";
        }

        if (normalized.Contains("prote")) return "protein";

        if (normalized.Contains("gordura") || normalized.Contains("gorduras"))
        {
            if (normalized.Contains("saturad"))
                return "saturated_fat";

            if (normalized.Contains("total") || normalized.Contains("totais"))
                return "fat";
        }

        if (normalized.Contains("fibra")) return "fiber";
        if (normalized.Contains("sodio")) return "sodium";

        return null;
    }

    private static void ApplyIfPresent(
        EstimatedNutritionProfileDto profile,
        IReadOnlyDictionary<string, List<double>> table,
        string key,
        int index,
        Action<double> apply)
    {
        if (!table.TryGetValue(key, out var values))
            return;

        if (index < 0 || index >= values.Count)
            return;

        apply(values[index]);
    }

    private static bool HasMinimumConfidenceForReplacement(
        EstimatedNutritionProfileDto corrected,
        IReadOnlyDictionary<string, List<double>> table,
        int index)
    {
        var hasCalories = table.TryGetValue("calories", out var calories) && index < calories.Count;

        var macroCount = 0;
        if (table.TryGetValue("carbs", out var carbs) && index < carbs.Count) macroCount++;
        if (table.TryGetValue("protein", out var protein) && index < protein.Count) macroCount++;
        if (table.TryGetValue("fat", out var fat) && index < fat.Count) macroCount++;

        if (!hasCalories || macroCount < 2)
            return false;

        return corrected.CaloriesPer100g.HasValue || corrected.CaloriesPer100ml.HasValue;
    }

    private static EstimatedNutritionProfileDto Clone(EstimatedNutritionProfileDto source)
    {
        return new EstimatedNutritionProfileDto
        {
            CaloriesPer100g = source.CaloriesPer100g,
            CaloriesPer100ml = source.CaloriesPer100ml,
            EstimatedPackageCalories = source.EstimatedPackageCalories,
            EstimatedCarbsPer100g = source.EstimatedCarbsPer100g,
            EstimatedSugarPer100g = source.EstimatedSugarPer100g,
            EstimatedAddedSugarPer100g = source.EstimatedAddedSugarPer100g,
            EstimatedSaturatedFatPer100g = source.EstimatedSaturatedFatPer100g,
            EstimatedProteinPer100g = source.EstimatedProteinPer100g,
            EstimatedSodiumPer100g = source.EstimatedSodiumPer100g,
            EstimatedFiberPer100g = source.EstimatedFiberPer100g,
            EstimatedFatPer100g = source.EstimatedFatPer100g,
            Basis = source.Basis,
            ParserConfidence = source.ParserConfidence,
            NutritionUnit = source.NutritionUnit,
            IsCorrectedByOcr = source.IsCorrectedByOcr,
            DataSource = source.DataSource != null
                ? new Dictionary<string, string>(source.DataSource, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
    }
}
