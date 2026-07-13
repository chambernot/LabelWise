using System.Collections.Generic;
using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Application.DTOs.Nutrition
{
    /// <summary>
    /// Perfil nutricional estimado baseado na categoria do produto.
    /// Não representa valores extraídos da tabela nutricional oficial.
    /// </summary>
    public class EstimatedNutritionProfileDto
    {
        /// <summary>Calorias estimadas por 100g do produto.</summary>
        public double? CaloriesPer100g { get; set; }

        /// <summary>Calorias totais estimadas da embalagem completa (se peso identificado).</summary>
        public double? EstimatedPackageCalories { get; set; }

        /// <summary>Carboidratos totais por 100g (em gramas).</summary>
        public double? EstimatedCarbsPer100g { get; set; }

        /// <summary>Açúcar total por 100g (em gramas).</summary>
        public double? EstimatedSugarPer100g { get; set; }

        /// <summary>Açúcar adicionado por 100g (em gramas), quando disponível no rótulo ANVISA.</summary>
        public double? EstimatedAddedSugarPer100g { get; set; }

        /// <summary>Polióis por 100g (em gramas), quando declarados no rótulo.</summary>
        public double? EstimatedPolyolsPer100g { get; set; }

        /// <summary>Gordura saturada por 100g (em gramas), quando disponível.</summary>
        public double? EstimatedSaturatedFatPer100g { get; set; }

        /// <summary>Gordura trans por 100g (em gramas). Sinal crítico para o score (ANVISA).</summary>
        public double? EstimatedTransFatPer100g { get; set; }

        /// <summary>Proteína por 100g (em gramas).</summary>
        public double? EstimatedProteinPer100g { get; set; }

        /// <summary>Sódio por 100g (em mg).</summary>
        public double? EstimatedSodiumPer100g { get; set; }

        /// <summary>Fibra alimentar por 100g (em gramas).</summary>
        public double? EstimatedFiberPer100g { get; set; }

        /// <summary>Gordura total por 100g (em gramas).</summary>
        public double? EstimatedFatPer100g { get; set; }

        /// <summary>Base ou metodologia utilizada para a estimativa.</summary>
        public string Basis { get; set; } = "Estimativa por categoria visual, sem leitura da tabela nutricional oficial";

        /// <summary>
        /// Confiança do parser na extração: "high", "medium" ou "low".
        /// high = todos os campos principais presentes.
        /// medium = faltando 1–2 campos.
        /// low = faltando açúcar, proteína ou calorias.
        /// </summary>
        public string? ParserConfidence { get; set; }

        public double? CaloriesPer100ml { get; set; }

        public string? NutritionUnit { get; set; }
        // "g" ou "ml"

        public bool IsCorrectedByOcr { get; set; }

        /// <summary>
        /// Indica se os dados vieram da OpenAI Vision (extração estruturada).
        /// Quando true, o pipeline NÃO deve aplicar fallback ou sobrescrever valores.
        /// </summary>
        public bool IsFromOpenAI { get; set; }

        public Dictionary<string, string> DataSource { get; set; } = new();

        public NutritionConfidenceResult? NutritionConfidence { get; set; }

        /// <summary>
        /// Per-field traceability: nutrient name → extraction result with source and confidence.
        /// Keys match canonical nutrient names used by <see cref="NutritionFieldMergeEngine"/>:
        /// "Calories", "Carbs", "Sugar", "AddedSugar", "Protein", "Fat",
        /// "SaturatedFat", "Fiber", "Sodium".
        /// </summary>
        public Dictionary<string, FieldValue> FieldValues { get; set; } = new();

        /// <summary>Nome do produto extraído do rótulo (quando disponível).</summary>
        public string? ProductName { get; set; }

        /// <summary>Marca extraída do rótulo (quando disponível).</summary>
        public string? Brand { get; set; }

        /// <summary>Quantidade da porção declarada no rótulo (ex.: 40).</summary>
        public double? ServingAmount { get; set; }

        /// <summary>Unidade da porção declarada no rótulo ("g" ou "ml").</summary>
        public string? ServingUnit { get; set; }

        /// <summary>Descrição livre da porção (ex.: "1 unidade", "2 colheres de sopa").</summary>
        public string? ServingDescription { get; set; }

        /// <summary>
        /// Valores nutricionais EXATAMENTE como aparecem no rótulo (por porção).
        /// Mantém o "AsLabel" do front fiel ao impresso, sem normalização para 100 g/ml.
        /// </summary>
        public RawServingNutrition? RawPerServing { get; set; }

        /// <summary>
        /// Indica se os valores por 100g foram calculados matematicamente a partir
        /// da porção declarada (true) ou lidos diretamente da coluna 100g/100ml do
        /// rótulo (false). Usado para definir <c>per100Source</c> no contrato da API.
        /// </summary>
        public bool IsPer100Derived { get; set; } = false;
    }

    /// <summary>Snapshot fiel dos valores nutricionais por porção, como impresso no rótulo.</summary>
    public sealed class RawServingNutrition
    {
        public double? CaloriesKcal { get; set; }
        public double? Carbohydrates { get; set; }
        public double? Sugar { get; set; }
        public double? AddedSugar { get; set; }
        public double? Polyols { get; set; }
        public double? Proteins { get; set; }
        public double? TotalFats { get; set; }
        public double? SaturatedFats { get; set; }
        public double? TransFats { get; set; }
        public double? Fiber { get; set; }
        public double? SodiumMg { get; set; }
    }
}
