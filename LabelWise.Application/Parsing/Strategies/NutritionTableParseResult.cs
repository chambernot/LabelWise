using System.Collections.Generic;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Parsing.Strategies
{
    /// <summary>
    /// Resultado do parsing de tabela nutricional.
    /// Inclui todos os campos nutricionais comuns em rótulos brasileiros (ANVISA).
    /// </summary>
    public class NutritionTableParseResult
    {
        // ═══════════════════════════════════════════════════════════════════════════════════════
        // PORÇÃO E SERVINGS
        // ═══════════════════════════════════════════════════════════════════════════════════════
        public string? ServingSize { get; set; }
        public int? ServingsPerContainer { get; set; }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // MACRONUTRIENTES PRINCIPAIS
        // ═══════════════════════════════════════════════════════════════════════════════════════
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

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // MICRONUTRIENTES COMUNS
        // ═══════════════════════════════════════════════════════════════════════════════════════
        public double? Calcium { get; set; }
        public double? Iron { get; set; }
        public double? Lactose { get; set; }
        public double? VitaminA { get; set; }
        public double? VitaminC { get; set; }
        public double? VitaminD { get; set; }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // NUTRIENTES DE SUPLEMENTOS
        // ═══════════════════════════════════════════════════════════════════════════════════════
        public double? Creatine { get; set; }
        public double? Caffeine { get; set; }
        public double? Bcaa { get; set; }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // % VALORES DIÁRIOS (%VD)
        // ═══════════════════════════════════════════════════════════════════════════════════════
        public Dictionary<string, double> DailyValuePercentages { get; set; } = new();

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // VALORES POR 100g (opcional)
        // ═══════════════════════════════════════════════════════════════════════════════════════
        public NutritionPer100gParseResult? Per100g { get; set; }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // QUALIDADE DO PARSING
        // ═══════════════════════════════════════════════════════════════════════════════════════
        public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.High;
        public List<string> ValidationWarnings { get; set; } = new();

        /// <summary>
        /// Indica se há dados nutricionais mínimos (pelo menos calorias ou 2 macronutrientes).
        /// Para suplementos, creatina/cafeína/bcaa também contam.
        /// </summary>
        public bool HasNutritionData => 
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
        public int ExtractedFieldsCount
        {
            get
            {
                var count = 0;
                if (!string.IsNullOrWhiteSpace(ServingSize)) count++;
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
                if (VitaminA.HasValue) count++;
                if (VitaminC.HasValue) count++;
                if (VitaminD.HasValue) count++;
                // Nutrientes de suplementos
                if (Creatine.HasValue) count++;
                if (Caffeine.HasValue) count++;
                if (Bcaa.HasValue) count++;
                return count;
            }
        }

        /// <summary>
        /// Indica se a tabela está completa (todos os macros principais + sódio).
        /// Para suplementos, pode ser considerada completa se tiver o nutriente principal.
        /// </summary>
        public bool IsComplete => 
            (Calories.HasValue &&
             TotalCarbohydrate.HasValue &&
             Protein.HasValue &&
             TotalFat.HasValue &&
             Sodium.HasValue) ||
            // Para suplementos: se tem porção + nutriente ativo
            (!string.IsNullOrWhiteSpace(ServingSize) && 
             (Creatine.HasValue || Caffeine.HasValue || Bcaa.HasValue));
    }

    /// <summary>
    /// Valores nutricionais por 100g (comum em rótulos da UE e Brasil).
    /// </summary>
    public class NutritionPer100gParseResult
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
