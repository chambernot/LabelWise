using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace LabelWise.Application.DTOs
{
    public class ClientAppResponse
    {
        public ProductInfo Product { get; set; } = new();
        public ScoreInfo Score { get; set; } = new();
        public List<DietaryBadge> DietaryBadges { get; set; } = new();
        public List<string> Strengths { get; set; } = new();
        public List<string> Weaknesses { get; set; } = new();
        public Dictionary<string, ProfileEvaluation> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public NutritionInfo Nutrition { get; set; } = new();
        public List<IngredientItem> IngredientsList { get; set; } = new();
        public List<string> CriticalAlerts { get; set; } = new();
    }

    public class ProductInfo
    {
        public string? Name { get; set; }
        public string? Brand { get; set; }
        public string Category { get; set; } = "general";
    }

    public class ScoreInfo
    {
        public int Global { get; set; }
        public string GlobalLabel { get; set; } = string.Empty; // Ex: "Muito ruim", "Evitar", "Bom"
        public string ExplicacaoScore { get; set; } = string.Empty;
    }

    public class ProfileEvaluation
    {
        public int Score { get; set; }
        public string Label { get; set; } = string.Empty; // Ex: "Evitar", "Atenção"
        public List<string> Reasons { get; set; } = new();
    }

    public class NutritionInfo
    {
        public NutritionValues AsLabel { get; set; } = new();
        public NutritionValues Per100 { get; set; } = new();
    }

    public class NutritionValues
    {
        public decimal? CaloriesKcal { get; set; }
        public decimal? Carbohydrates { get; set; }
        public decimal? Sugars { get; set; }
        public decimal? Proteins { get; set; }
        public decimal? TotalFats { get; set; }
        public decimal? SaturatedFats { get; set; }
        public decimal? TransFats { get; set; }
        public decimal? Fiber { get; set; }
        public decimal? SodiumMg { get; set; }
    }

    public class DietaryBadge
    {
        public string Type { get; set; } = string.Empty;
        public bool IsCompatible { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public class IngredientItem
    {
        public string Name { get; set; } = string.Empty;
        public bool IsAllergen { get; set; }
    }

    public class ImageAttachmentDto
    {
        public string FileName { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
    }

    // Usado para mapear o JSON bruto que o Gemini devolve antes de passar pelo motor de regras
    public class GeminiRawExtraction
    {
        public List<string> Ingredients { get; set; } = new();
        public GeminiNutritionFacts? NutritionFacts { get; set; }
    }

    public class GeminiNutritionFacts
    {
        public string? ServingSize { get; set; }
        public decimal? CaloriesKcal { get; set; }
        public decimal? Carbohydrates { get; set; }
        public decimal? Sugars { get; set; }
        public decimal? Proteins { get; set; }
        public decimal? TotalFats { get; set; }
        public decimal? SaturatedFats { get; set; }
        public decimal? TransFats { get; set; }
        public decimal? Fiber { get; set; }
        public decimal? SodiumMg { get; set; }
    }
}
