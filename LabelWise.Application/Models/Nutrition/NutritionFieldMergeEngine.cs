using System.Collections.Generic;
using System.Linq;
using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Models.Nutrition;

/// <summary>
/// Field-level merge engine for nutrition profiles.
///
/// Canonical field keys used throughout the pipeline:
///   "Calories", "Carbs", "Sugar", "AddedSugar", "Protein",
///   "Fat", "SaturatedFat", "Fiber", "Sodium"
///
/// Merge priority rule:
///   finalValue = ocrValue ?? fallbackValue
///   OCR / GPT values are NEVER overridden by fallback.
///
/// Post-merge validation (applied before the caller persists the fields):
///   • fat &lt; saturatedFat  → always physically impossible → discard saturatedFat
///   • sugar &gt; carbs       → if Fallback source → discard sugar
///                             if OCR/GPT source  → reduce confidence by 0.30
///
/// Confidence aggregation:
///   • Average of all retained field confidences
///   • Promoted to HIGH (0.90) when ≥ 5 OCR/GPT fields and no validation issues
/// </summary>
public static class NutritionFieldMergeEngine
{
    // ── Canonical keys ────────────────────────────────────────────────
    public const string Calories     = "Calories";
    public const string Carbs        = "Carbs";
    public const string Sugar        = "Sugar";
    public const string AddedSugar   = "AddedSugar";
    public const string Protein      = "Protein";
    public const string Fat          = "Fat";
    public const string SaturatedFat = "SaturatedFat";
    public const string Fiber        = "Fiber";
    public const string Sodium       = "Sodium";

    /// <summary>All canonical nutrient key names in definition order.</summary>
    public static readonly IReadOnlyList<string> AllKeys = new[]
    {
        Calories, Carbs, Sugar, AddedSugar, Protein, Fat, SaturatedFat, Fiber, Sodium
    };

    // ── Confidence thresholds ─────────────────────────────────────────
    /// <summary>Confidence used when a field is promoted to "high" quality.</summary>
    public const double HighConfidence = 0.90;

    /// <summary>Minimum number of OCR/GPT fields required for promotion.</summary>
    public const int HighConfidenceFieldThreshold = 5;

    // ── Core merge ────────────────────────────────────────────────────

    /// <summary>
    /// Picks the best available <see cref="FieldValue"/> for a single nutrient.
    /// OCR and GPT values take absolute priority; fallback fills nulls only.
    /// </summary>
    public static FieldValue? Merge(FieldValue? primary, FieldValue? fallback)
    {
        if (primary?.IsValid == true) return primary;   // OCR / GPT locked
        if (fallback?.IsValid == true) return fallback; // fill absent field
        return null;
    }

    /// <summary>
    /// Merges two complete <see cref="FieldValue"/> dictionaries field by field.
    /// <paramref name="primary"/> is treated as OCR/GPT; <paramref name="fallback"/>
    /// is used only where <paramref name="primary"/> has no value.
    /// Validation and confidence adjustment are applied on the result.
    /// </summary>
    public static Dictionary<string, FieldValue> MergeProfiles(
        IReadOnlyDictionary<string, FieldValue> primary,
        IReadOnlyDictionary<string, FieldValue> fallback)
    {
        var merged = new Dictionary<string, FieldValue>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in AllKeys)
        {
            primary.TryGetValue(key, out var p);
            fallback.TryGetValue(key, out var f);
            var result = Merge(p, f);
            if (result != null)
                merged[key] = result;
        }

        return ValidateAndAdjust(merged);
    }

    // ── Validation ────────────────────────────────────────────────────

    /// <summary>
    /// Applies cross-field validation rules and adjusts or discards invalid fields.
    /// Returns a new dictionary with adjustments applied.
    /// </summary>
    public static Dictionary<string, FieldValue> ValidateAndAdjust(
        Dictionary<string, FieldValue> fields)
    {
        var result = new Dictionary<string, FieldValue>(fields, StringComparer.OrdinalIgnoreCase);

        // Rule 1: fat >= saturatedFat (physically impossible otherwise)
        if (result.TryGetValue(Fat, out var fat) &&
            result.TryGetValue(SaturatedFat, out var satFat) &&
            fat.Value.HasValue && satFat.Value.HasValue &&
            fat.Value < satFat.Value)
        {
            // Always discard saturatedFat — the mapping is wrong regardless of source
            result.Remove(SaturatedFat);
        }

        // Rule 2: sugar <= carbs
        if (result.TryGetValue(Sugar, out var sugar) &&
            result.TryGetValue(Carbs, out var carbs) &&
            sugar.Value.HasValue && carbs.Value.HasValue &&
            sugar.Value > carbs.Value)
        {
            if (sugar.Source == "Fallback")
            {
                // Fallback sugar > OCR carbs → clearly wrong mapping, discard
                result.Remove(Sugar);
            }
            else
            {
                // OCR/GPT anomaly → keep but reduce confidence
                result[Sugar] = sugar.WithConfidence(
                    System.Math.Max(0.0, sugar.Confidence - 0.30));
            }
        }

        return result;
    }

    // ── Confidence aggregation ────────────────────────────────────────

    /// <summary>
    /// Computes the aggregate confidence for a set of merged fields.
    /// Promotes to <see cref="HighConfidence"/> when there are at least
    /// <see cref="HighConfidenceFieldThreshold"/> OCR/GPT fields and no
    /// validation issues in the input set.
    /// </summary>
    public static double ComputeAggregateConfidence(
        IReadOnlyDictionary<string, FieldValue> fields,
        bool hasValidationIssues = false)
    {
        var values = fields.Values.Where(f => f.IsValid).ToList();
        if (values.Count == 0) return 0.0;

        int ocrGptCount = values.Count(f =>
            f.Source is "OCR" or "GPT");

        if (!hasValidationIssues && ocrGptCount >= HighConfidenceFieldThreshold)
            return HighConfidence;

        return values.Average(f => f.Confidence);
    }

    // ── Profile ↔ FieldValues conversion ─────────────────────────────

    /// <summary>
    /// Reads the flat fields of <paramref name="profile"/> and builds a canonical
    /// <see cref="FieldValue"/> dictionary, tagging each present field with
    /// <paramref name="defaultSource"/> and <paramref name="defaultConfidence"/>.
    /// Fields already present in <c>profile.FieldValues</c> are preserved as-is
    /// (i.e. the caller's explicit tagging takes precedence).
    /// </summary>
    public static Dictionary<string, FieldValue> ExtractFromProfile(
        EstimatedNutritionProfileDto profile,
        string defaultSource,
        double defaultConfidence)
    {
        var dict = new Dictionary<string, FieldValue>(
            profile.FieldValues ?? new Dictionary<string, FieldValue>(),
            StringComparer.OrdinalIgnoreCase);

        void Tag(string key, double? value)
        {
            if (dict.ContainsKey(key)) return; // explicit tag wins
            if (!value.HasValue) return;
            dict[key] = defaultSource == "OCR"
                ? FieldValue.FromOcr(value.Value, defaultConfidence)
                : defaultSource == "GPT"
                    ? FieldValue.FromGpt(value.Value, defaultConfidence)
                    : FieldValue.FromFallback(value.Value, defaultConfidence);
        }

        var calories = profile.CaloriesPer100g ?? profile.CaloriesPer100ml;
        Tag(Calories,     calories);
        Tag(Carbs,        profile.EstimatedCarbsPer100g);
        Tag(Sugar,        profile.EstimatedSugarPer100g);
        Tag(AddedSugar,   profile.EstimatedAddedSugarPer100g);
        Tag(Protein,      profile.EstimatedProteinPer100g);
        Tag(Fat,          profile.EstimatedFatPer100g);
        Tag(SaturatedFat, profile.EstimatedSaturatedFatPer100g);
        Tag(Fiber,        profile.EstimatedFiberPer100g);
        Tag(Sodium,       profile.EstimatedSodiumPer100g);

        return dict;
    }

    /// <summary>
    /// Writes back the values from <paramref name="fields"/> into the flat numeric
    /// properties of <paramref name="profile"/> and updates <c>FieldValues</c>.
    /// Only fields present in <paramref name="fields"/> are written; absent fields
    /// in the dictionary are left untouched in the profile.
    /// </summary>
    public static void ApplyToProfile(
        EstimatedNutritionProfileDto profile,
        IReadOnlyDictionary<string, FieldValue> fields,
        string nutritionUnit)
    {
        profile.FieldValues = new Dictionary<string, FieldValue>(fields, StringComparer.OrdinalIgnoreCase);

        if (fields.TryGetValue(Calories, out var cal) && cal.IsValid)
        {
            if (nutritionUnit == "ml")
            {
                profile.CaloriesPer100ml = cal.Value;
                profile.CaloriesPer100g  = null;
            }
            else
            {
                profile.CaloriesPer100g  = cal.Value;
                profile.CaloriesPer100ml = null;
            }
        }

        if (fields.TryGetValue(Carbs,        out var v) && v.IsValid) profile.EstimatedCarbsPer100g        = v.Value;
        if (fields.TryGetValue(Sugar,        out v)     && v.IsValid) profile.EstimatedSugarPer100g        = v.Value;
        if (fields.TryGetValue(AddedSugar,   out v)     && v.IsValid) profile.EstimatedAddedSugarPer100g   = v.Value;
        if (fields.TryGetValue(Protein,      out v)     && v.IsValid) profile.EstimatedProteinPer100g      = v.Value;
        if (fields.TryGetValue(Fat,          out v)     && v.IsValid) profile.EstimatedFatPer100g          = v.Value;
        if (fields.TryGetValue(SaturatedFat, out v)     && v.IsValid) profile.EstimatedSaturatedFatPer100g = v.Value;
        if (fields.TryGetValue(Fiber,        out v)     && v.IsValid) profile.EstimatedFiberPer100g        = v.Value;
        if (fields.TryGetValue(Sodium,       out v)     && v.IsValid) profile.EstimatedSodiumPer100g       = v.Value;
    }
}
