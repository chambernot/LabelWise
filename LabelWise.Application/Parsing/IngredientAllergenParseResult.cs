using System.Collections.Generic;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Parsing
{
    public class IngredientAllergenParseResult
    {
        public string? ProductName { get; set; }
        public string? Brand { get; set; }
        public string? Barcode { get; set; }
        public NutritionData? Nutrition { get; set; }
        public List<string> Ingredients { get; set; } = new List<string>();
        public List<string> Allergens { get; set; } = new List<string>(); // Todos os alergênicos

        // Separação de alergênicos confirmados vs potenciais
        public List<string> ConfirmedAllergens { get; set; } = new List<string>(); // "Contém"
        public List<string> MayContainAllergens { get; set; } = new List<string>(); // "Pode conter"

        public List<string> CriticalTerms { get; set; } = new List<string>();
        public List<string> ExtractedPhrases { get; set; } = new List<string>();

        // Quality metrics
        public ConfidenceLevel ParsingConfidence { get; set; } = ConfidenceLevel.High;
        public List<string> ValidationWarnings { get; set; } = new List<string>();
        public bool IsProductNameValidated { get; set; }
        public bool IsBrandValidated { get; set; }

        // Metadata for additional processing info
        public Dictionary<string, string>? Metadata { get; set; }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // PARTIAL ANALYSIS SUPPORT (for NutritionTable, IngredientsList, etc.)
        // ═══════════════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Indica se esta é uma análise parcial (apenas uma parte do rótulo foi capturada).
        /// </summary>
        public bool IsPartialAnalysis { get; set; }

        /// <summary>
        /// Tipo de captura que gerou este resultado (NutritionTable, IngredientsList, etc.).
        /// </summary>
        public CaptureType? SourceCaptureType { get; set; }

        /// <summary>
        /// Lista de passos/capturas faltantes para completar a análise.
        /// Ex: ["IngredientsList", "FrontPackaging"]
        /// </summary>
        public List<string> MissingSteps { get; set; } = new List<string>();

        /// <summary>
        /// Mensagem personalizada para análise parcial.
        /// </summary>
        public string? PartialAnalysisMessage { get; set; }

        // ═══════════════════════════════════════════════════════════════════════════════════════

        public bool HasIngredients => Ingredients?.Count > 0;
        public bool HasAllergens => Allergens?.Count > 0;
        public bool HasConfirmedAllergens => ConfirmedAllergens?.Count > 0;
        public bool HasPotentialAllergens => MayContainAllergens?.Count > 0;
        public bool HasNutritionData => Nutrition != null && Nutrition.HasData;
    }

    public class NutritionData
    {
        public string? ServingSize { get; set; }
        public int? ServingsPerContainer { get; set; }
        public double? Calories { get; set; }
        public double? TotalFat { get; set; }
        public double? SaturatedFat { get; set; }
        public double? TransFat { get; set; }
        public double? Cholesterol { get; set; }
        public double? Sodium { get; set; }
        public double? TotalCarbohydrate { get; set; }
        public double? DietaryFiber { get; set; }
        public double? Sugars { get; set; }
        public double? AddedSugars { get; set; }
        public double? Protein { get; set; }

        // Nutrientes adicionais comuns em rótulos brasileiros
        public double? Calcium { get; set; }
        public double? Iron { get; set; }
        public double? Lactose { get; set; }
        public double? VitaminA { get; set; }
        public double? VitaminC { get; set; }
        public double? VitaminD { get; set; }

        // Nutrientes de suplementos
        public double? Creatine { get; set; }
        public double? Caffeine { get; set; }
        public double? Bcaa { get; set; }

        // %VD (Valores Diários)
        public Dictionary<string, double> DailyValuePercentages { get; set; } = new();

        // Valores por 100g (para comparação)
        public NutritionPer100g? Per100g { get; set; }

        /// <summary>
        /// Indica se há dados nutricionais mínimos (pelo menos 2 dos 4 principais ou nutrientes de suplemento).
        /// </summary>
        public bool HasData => 
            Calories.HasValue || 
            TotalCarbohydrate.HasValue || 
            Protein.HasValue || 
            TotalFat.HasValue ||
            Sodium.HasValue ||
            Creatine.HasValue ||
            Caffeine.HasValue ||
            Bcaa.HasValue;

        /// <summary>
        /// Contagem de campos nutricionais preenchidos.
        /// </summary>
        public int FilledFieldsCount
        {
            get
            {
                var count = 0;
                if (Calories.HasValue) count++;
                if (TotalFat.HasValue) count++;
                if (SaturatedFat.HasValue) count++;
                if (TransFat.HasValue) count++;
                if (Cholesterol.HasValue) count++;
                if (Sodium.HasValue) count++;
                if (TotalCarbohydrate.HasValue) count++;
                if (DietaryFiber.HasValue) count++;
                if (Sugars.HasValue) count++;
                if (AddedSugars.HasValue) count++;
                if (Protein.HasValue) count++;
                if (Calcium.HasValue) count++;
                if (Iron.HasValue) count++;
                if (Lactose.HasValue) count++;
                if (!string.IsNullOrEmpty(ServingSize)) count++;
                if (ServingsPerContainer.HasValue) count++;
                // Nutrientes de suplementos
                if (Creatine.HasValue) count++;
                if (Caffeine.HasValue) count++;
                if (Bcaa.HasValue) count++;
                return count;
            }
        }
    }

    /// <summary>
    /// Valores nutricionais por 100g (comum em rótulos da UE e Brasil).
    /// </summary>
    public class NutritionPer100g
    {
        public double? Calories { get; set; }
        public double? TotalFat { get; set; }
        public double? SaturatedFat { get; set; }
        public double? TotalCarbohydrate { get; set; }
        public double? Sugars { get; set; }
        public double? Protein { get; set; }
        public double? Sodium { get; set; }
        public double? DietaryFiber { get; set; }
    }
}
