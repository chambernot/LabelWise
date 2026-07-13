using System.Text.Json.Serialization;

namespace LabelWise.Application.DTOs.OpenFoodFacts
{
    public class OpenFoodFactsResponse
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("product")]
        public OpenFoodFactsProduct? Product { get; set; }
    }

    public class OpenFoodFactsProduct
    {
        [JsonPropertyName("product_name")]
        public string? ProductName { get; set; }

        [JsonPropertyName("brands")]
        public string? Brands { get; set; }

        [JsonPropertyName("categories")]
        public string? Categories { get; set; }

        [JsonPropertyName("nutriments")]
        public OpenFoodFactsNutriments? Nutriments { get; set; }

        [JsonPropertyName("data_quality_info_tags")]
        public List<string>? DataQualityInfoTags { get; set; }

        /// <summary>
        /// Retorna true se o produto tem dados nutricionais suficientes para análise.
        /// Exige pelo menos 2 macronutrientes primários (carboidratos, proteínas ou gorduras)
        /// ou pelo menos 3 campos nutricionais totais preenchidos com valores significativos.
        /// </summary>
        public bool HasUsableNutritionData()
        {
            if (DataQualityInfoTags != null &&
                DataQualityInfoTags.Contains("en:no-nutrition-data", StringComparer.OrdinalIgnoreCase))
                return false;

            var n = Nutriments;
            if (n == null) return false;

            // Conta quantos macronutrientes primários estão presentes
            int primaryMacros = 0;
            if (n.Carbohydrates100g.HasValue) primaryMacros++;
            if (n.Proteins100g.HasValue) primaryMacros++;
            if (n.Fat100g.HasValue) primaryMacros++;

            // Se temos pelo menos 2 macros primários, consideramos suficiente
            if (primaryMacros >= 2) return true;

            // Caso contrário, conta todos os campos nutricionais significativos
            int totalFields = 0;
            if (n.EnergyKcal100g.HasValue && n.EnergyKcal100g.Value > 0) totalFields++;
            if (n.Carbohydrates100g.HasValue) totalFields++;
            if (n.Proteins100g.HasValue) totalFields++;
            if (n.Fat100g.HasValue) totalFields++;
            if (n.Sugars100g.HasValue) totalFields++;
            if (n.Fiber100g.HasValue) totalFields++;
            if (n.Sodium100g.HasValue) totalFields++;

            // Exige pelo menos 3 campos preenchidos
            return totalFields >= 3;
        }
    }

    public class OpenFoodFactsNutriments
    {
        [JsonPropertyName("energy-kcal_100g")]
        public double? EnergyKcal100g { get; set; }

        [JsonPropertyName("carbohydrates_100g")]
        public double? Carbohydrates100g { get; set; }

        [JsonPropertyName("sugars_100g")]
        public double? Sugars100g { get; set; }

        [JsonPropertyName("proteins_100g")]
        public double? Proteins100g { get; set; }

        [JsonPropertyName("fat_100g")]
        public double? Fat100g { get; set; }

        [JsonPropertyName("saturated-fat_100g")]
        public double? SaturatedFat100g { get; set; }

        [JsonPropertyName("fiber_100g")]
        public double? Fiber100g { get; set; }

        [JsonPropertyName("sodium_100g")]
        public double? Sodium100g { get; set; }
    }
}
