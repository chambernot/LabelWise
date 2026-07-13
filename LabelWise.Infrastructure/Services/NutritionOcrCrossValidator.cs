using System.Globalization;
using System.Text.RegularExpressions;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services;

public sealed class NutritionOcrCrossValidator : INutritionOcrCrossValidator
{
    private static readonly Regex NumberRegex = new(@"\d+(?:[\.,]\d+)?", RegexOptions.Compiled);

    private readonly INutritionConsistencyValidator _consistencyValidator;
    private readonly ILogger<NutritionOcrCrossValidator> _logger;

    public NutritionOcrCrossValidator(
        INutritionConsistencyValidator consistencyValidator,
        ILogger<NutritionOcrCrossValidator> logger)
    {
        _consistencyValidator = consistencyValidator;
        _logger = logger;
    }

    public EstimatedNutritionProfileDto ValidateAndCorrect(EstimatedNutritionProfileDto profile, List<string> rawText)
    {
        if (profile == null || rawText == null || rawText.Count == 0)
            return profile;

        InitializeDataSource(profile);

        var ocrValues = ParseOcr(rawText);
        if (ocrValues.Count == 0)
            return profile;

        var snapshot = Clone(profile);
        var hasCorrections = false;

        hasCorrections |= TryCorrect(profile, ocrValues, "calories", profile.CaloriesPer100g,
            v => profile.CaloriesPer100g = v,
            "CaloriesPer100g",
            shouldSkipWhenOcrZero: true);

        hasCorrections |= TryCorrect(profile, ocrValues, "carbs", profile.EstimatedCarbsPer100g,
            v => profile.EstimatedCarbsPer100g = v,
            "EstimatedCarbsPer100g",
            shouldSkipWhenOcrZero: true);

        hasCorrections |= TryCorrect(profile, ocrValues, "sugar", profile.EstimatedSugarPer100g,
            v => profile.EstimatedSugarPer100g = v,
            "EstimatedSugarPer100g",
            shouldSkipWhenOcrZero: true);

        hasCorrections |= TryCorrect(profile, ocrValues, "added_sugar", profile.EstimatedAddedSugarPer100g,
            v => profile.EstimatedAddedSugarPer100g = v,
            "EstimatedAddedSugarPer100g",
            shouldSkipWhenOcrZero: true);

        hasCorrections |= TryCorrect(profile, ocrValues, "protein", profile.EstimatedProteinPer100g,
            v => profile.EstimatedProteinPer100g = v,
            "EstimatedProteinPer100g",
            shouldSkipWhenOcrZero: true);

        hasCorrections |= TryCorrect(profile, ocrValues, "fat", profile.EstimatedFatPer100g,
            v => profile.EstimatedFatPer100g = v,
            "EstimatedFatPer100g",
            shouldSkipWhenOcrZero: true);

        hasCorrections |= TryCorrect(profile, ocrValues, "saturated_fat", profile.EstimatedSaturatedFatPer100g,
            v => profile.EstimatedSaturatedFatPer100g = v,
            "EstimatedSaturatedFatPer100g",
            shouldSkipWhenOcrZero: true);

        hasCorrections |= TryCorrect(profile, ocrValues, "fiber", profile.EstimatedFiberPer100g,
            v => profile.EstimatedFiberPer100g = v,
            "EstimatedFiberPer100g",
            shouldSkipWhenOcrZero: true);

        hasCorrections |= TryCorrect(profile, ocrValues, "sodium", profile.EstimatedSodiumPer100g,
            v => profile.EstimatedSodiumPer100g = v,
            "EstimatedSodiumPer100g",
            shouldSkipWhenOcrZero: false);

        if (!hasCorrections)
            return profile;

        var validation = _consistencyValidator.Validate(profile);
        if (!validation.IsValid)
        {
            return snapshot;
        }

        profile.IsCorrectedByOcr = true;
        return profile;
    }

    private Dictionary<string, double> ParseOcr(List<string> rawText)
    {
        var output = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in rawText)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var normalized = NutritionColumnParser.NormalizeToken(line);
            var key = MapNutrientKey(normalized);
            if (key == null)
                continue;

            var value = ExtractFirstNumericValue(line);
            if (!value.HasValue)
                continue;

            output[key] = value.Value;
        }

        return output;
    }

    private bool TryCorrect(
        EstimatedNutritionProfileDto profile,
        IReadOnlyDictionary<string, double> ocrValues,
        string key,
        double? aiValue,
        Action<double> apply,
        string dataSourceKey,
        bool shouldSkipWhenOcrZero)
    {
        if (!ocrValues.TryGetValue(key, out var ocrValue))
            return false;

        if (ocrValue == 0 && shouldSkipWhenOcrZero && (aiValue ?? 0) > 0)
            return false;

        if (!aiValue.HasValue)
        {
            apply(ocrValue);
            profile.DataSource[dataSourceKey] = "OCR";
            _logger.LogWarning("Correção OCR aplicada: {Nutrient} AI=null → OCR={OcrValue}", dataSourceKey, ocrValue);
            return true;
        }

        var diff = Math.Abs(aiValue.Value - ocrValue) / Math.Max(ocrValue, 1);
        if (diff <= 0.2)
            return false;

        apply(ocrValue);
        profile.DataSource[dataSourceKey] = "OCR";

        _logger.LogWarning(
            "Correção OCR aplicada: {Nutrient} AI={AiValue} → OCR={OcrValue}",
            dataSourceKey,
            aiValue.Value,
            ocrValue);

        return true;
    }

    private static void InitializeDataSource(EstimatedNutritionProfileDto profile)
    {
        profile.DataSource ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        MarkAi(profile.DataSource, "CaloriesPer100g", profile.CaloriesPer100g);
        MarkAi(profile.DataSource, "EstimatedCarbsPer100g", profile.EstimatedCarbsPer100g);
        MarkAi(profile.DataSource, "EstimatedSugarPer100g", profile.EstimatedSugarPer100g);
        MarkAi(profile.DataSource, "EstimatedAddedSugarPer100g", profile.EstimatedAddedSugarPer100g);
        MarkAi(profile.DataSource, "EstimatedProteinPer100g", profile.EstimatedProteinPer100g);
        MarkAi(profile.DataSource, "EstimatedFatPer100g", profile.EstimatedFatPer100g);
        MarkAi(profile.DataSource, "EstimatedSaturatedFatPer100g", profile.EstimatedSaturatedFatPer100g);
        MarkAi(profile.DataSource, "EstimatedFiberPer100g", profile.EstimatedFiberPer100g);
        MarkAi(profile.DataSource, "EstimatedSodiumPer100g", profile.EstimatedSodiumPer100g);
    }

    private static void MarkAi(Dictionary<string, string> dataSource, string key, double? value)
    {
        if (!value.HasValue)
            return;

        if (!dataSource.ContainsKey(key))
            dataSource[key] = "AI";
    }

    private static string? MapNutrientKey(string normalized)
    {
        if (normalized.Contains("valor energetico") || normalized.Contains("energetico") || normalized.Contains("energ"))
            return "calories";

        if (normalized.Contains("carboidr"))
            return "carbs";

        if (normalized.Contains("acucar") || normalized.Contains("acucares"))
        {
            if (normalized.Contains("adicionado") || normalized.Contains("adicionados"))
                return "added_sugar";

            return "sugar";
        }

        if (normalized.Contains("prote"))
            return "protein";

        if (normalized.Contains("gordura") || normalized.Contains("gorduras"))
        {
            if (normalized.Contains("saturad"))
                return "saturated_fat";

            if (normalized.Contains("total") || normalized.Contains("totais"))
                return "fat";
        }

        if (normalized.Contains("fibra"))
            return "fiber";

        if (normalized.Contains("sodio"))
            return "sodium";

        return null;
    }

    private static double? ExtractFirstNumericValue(string line)
    {
        var matches = NumberRegex.Matches(NutritionColumnParser.NormalizeToken(line));
        if (matches.Count == 0)
            return null;

        var raw = matches[0].Value.Replace(',', '.');
        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            return value;

        return null;
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
