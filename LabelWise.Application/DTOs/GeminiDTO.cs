using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LabelWise.Application.DTOs
{
    public class ClientAppResponse
    {
        [JsonPropertyName("product")]
        public ProductInfo Product { get; set; } = new();

        [JsonPropertyName("score")]
        public ScoreInfo Score { get; set; } = new();

        [JsonPropertyName("dietaryBadges")]
        public List<DietaryBadge> DietaryBadges { get; set; } = new();

        [JsonPropertyName("strengths")]
        public List<string> Strengths { get; set; } = new();

        [JsonPropertyName("weaknesses")]
        public List<string> Weaknesses { get; set; } = new();

        [JsonPropertyName("profiles")]
        public Dictionary<string, ProfileEvaluation> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        [JsonPropertyName("nutrition")]
        public NutritionInfo Nutrition { get; set; } = new();

        [JsonPropertyName("ingredientsList")]
        public List<IngredientItem> IngredientsList { get; set; } = new();

        [JsonPropertyName("criticalAlerts")]
        public List<string> CriticalAlerts { get; set; } = new();
    }

    public class ProductInfo
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("brand")]
        public string? Brand { get; set; }

        [JsonPropertyName("category")]
        public string Category { get; set; } = "general";
    }

    public class ScoreInfo
    {
        [JsonPropertyName("global")]
        public int Global { get; set; }

        [JsonPropertyName("globalLabel")]
        public string GlobalLabel { get; set; } = string.Empty;

        [JsonPropertyName("explicacaoScore")]
        public string ExplicacaoScore { get; set; } = string.Empty;
    }

    public class ProfileEvaluation
    {
        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("reasons")]
        public List<string> Reasons { get; set; } = new();
    }

    public class NutritionInfo
    {
        [JsonPropertyName("asLabel")]
        public NutritionValues AsLabel { get; set; } = new();

        [JsonPropertyName("per100")]
        public NutritionValues Per100 { get; set; } = new();
    }

    public class NutritionValues
    {
        [JsonPropertyName("caloriesKcal")]
        public decimal? CaloriesKcal { get; set; }

        [JsonPropertyName("carbohydrates")]
        public decimal? Carbohydrates { get; set; }

        [JsonPropertyName("sugars")]
        public decimal? Sugars { get; set; }

        [JsonPropertyName("addedSugars")]
        public decimal? AddedSugars { get; set; }

        [JsonPropertyName("proteins")]
        public decimal? Proteins { get; set; }

        [JsonPropertyName("totalFats")]
        public decimal? TotalFats { get; set; }

        [JsonPropertyName("saturatedFats")]
        public decimal? SaturatedFats { get; set; }

        [JsonPropertyName("transFats")]
        public decimal? TransFats { get; set; }

        [JsonPropertyName("fiber")]
        public decimal? Fiber { get; set; }

        [JsonPropertyName("sodiumMg")]
        public decimal? SodiumMg { get; set; }
    }

    public class DietaryBadge
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("isCompatible")]
        public bool IsCompatible { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;
    }

    public class IngredientItem
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("isAllergen")]
        public bool IsAllergen { get; set; }
    }

    public class ImageAttachmentDto
    {
        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("mimeType")]
        public string MimeType { get; set; } = string.Empty;

        [JsonPropertyName("bytes")]
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
    }

    public class GeminiRawExtraction
    {
        [JsonPropertyName("productName")]
        public string? ProductName { get; set; }

        [JsonPropertyName("brand")]
        public string? Brand { get; set; }

        [JsonPropertyName("ingredients")]
        public List<string> Ingredients { get; set; } = new();

        [JsonPropertyName("allergenWarnings")]
        public List<string> AllergenWarnings { get; set; } = new();

        [JsonPropertyName("nutritionFacts")]
        public GeminiNutritionFactsDto? NutritionFacts { get; set; }
    }

    public class GeminiNutritionFactsDto
    {
        [JsonPropertyName("servingSize")]
        public string? ServingSize { get; set; }

        [JsonPropertyName("servingsPerPackage")]
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? ServingsPerPackage { get; set; }

        [JsonPropertyName("per100g")]
        public GeminiNutritionalValuesDto? Per100g { get; set; }

        [JsonPropertyName("perServing")]
        public GeminiNutritionalValuesDto? PerServing { get; set; }
    }

    public class GeminiNutritionalValuesDto
    {
        [JsonPropertyName("caloriesKcal")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double? CaloriesKcal { get; set; }

        [JsonPropertyName("carbohydrates")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double? Carbohydrates { get; set; }

        [JsonPropertyName("sugars")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double? Sugars { get; set; }

        [JsonPropertyName("addedSugars")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double? AddedSugars { get; set; }

        [JsonPropertyName("proteins")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double? Proteins { get; set; }

        [JsonPropertyName("totalFats")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double? TotalFats { get; set; }

        [JsonPropertyName("saturatedFats")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double? SaturatedFats { get; set; }

        [JsonPropertyName("transFats")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double? TransFats { get; set; }

        [JsonPropertyName("fiber")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double? Fiber { get; set; }

        [JsonPropertyName("sodiumMg")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double? SodiumMg { get; set; }
    }

    public class FlexibleStringConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                return System.Text.Encoding.UTF8.GetString(reader.ValueSpan);
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString();
            }

            return null;
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            if (value == null)
                writer.WriteNullValue();
            else
                writer.WriteStringValue(value);
        }
    }


}