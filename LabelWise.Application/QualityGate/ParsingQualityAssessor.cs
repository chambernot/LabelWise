using System.Collections.Generic;
using System.Linq;
using LabelWise.Application.Parsing;

namespace LabelWise.Application.QualityGate
{
    /// <summary>
    /// Avalia a qualidade do parsing de ingredientes e informações do produto.
    /// Detecta parsing incompleto, dados ausentes, e tokens inválidos.
    /// </summary>
    public class ParsingQualityAssessor
    {
        private static readonly HashSet<string> InvalidTokens = new()
        {
            "?", "???", "...", "---", "###", "***", "n/a", "null", "undefined"
        };

        public ParsingQualityMetrics AssessQuality(IngredientAllergenParseResult parseResult)
        {
            var metrics = new ParsingQualityMetrics();

            // 1. Verificar identificação do produto
            metrics.HasProductName = !string.IsNullOrWhiteSpace(parseResult.ProductName) 
                                     && parseResult.ProductName != "Produto Desconhecido"
                                     && !parseResult.ProductName.Contains("???");
            
            metrics.HasBrand = !string.IsNullOrWhiteSpace(parseResult.Brand);

            // 2. Análise de ingredientes
            metrics.IngredientsCount = parseResult.Ingredients?.Count ?? 0;
            metrics.HasIngredients = metrics.IngredientsCount > 0;

            if (metrics.HasIngredients)
            {
                var invalidIngredients = parseResult.Ingredients!
                    .Count(ing => string.IsNullOrWhiteSpace(ing) 
                                 || ing.Length < 2 
                                 || InvalidTokens.Contains(ing.ToLower().Trim())
                                 || ing.Count(c => !char.IsLetter(c) && c != ' ') > ing.Length * 0.5);

                metrics.InvalidIngredientsCount = invalidIngredients;
                metrics.InvalidIngredientsRatio = (double)invalidIngredients / metrics.IngredientsCount;
                metrics.HasValidIngredients = metrics.IngredientsCount - invalidIngredients >= 1;
            }

            // 3. Análise de alérgenos
            metrics.AllergensCount = parseResult.Allergens?.Count ?? 0;
            metrics.HasAllergens = metrics.AllergensCount > 0;

            // 4. Análise nutricional
            metrics.HasNutritionalInfo = parseResult.Nutrition != null;
            
            if (metrics.HasNutritionalInfo)
            {
                var nutrition = parseResult.Nutrition!;
                var fieldsPopulated = 0;
                var totalFields = 10;

                if (nutrition.Calories > 0) fieldsPopulated++;
                if (nutrition.TotalFat.HasValue && nutrition.TotalFat > 0) fieldsPopulated++;
                if (nutrition.SaturatedFat.HasValue && nutrition.SaturatedFat > 0) fieldsPopulated++;
                if (nutrition.TransFat.HasValue) fieldsPopulated++;
                if (nutrition.Cholesterol.HasValue) fieldsPopulated++;
                if (nutrition.Sodium.HasValue) fieldsPopulated++;
                if (nutrition.TotalCarbohydrate.HasValue) fieldsPopulated++;
                if (nutrition.DietaryFiber.HasValue) fieldsPopulated++;
                if (nutrition.Sugars.HasValue) fieldsPopulated++;
                if (nutrition.Protein.HasValue) fieldsPopulated++;

                metrics.NutritionalFieldsPopulated = fieldsPopulated;
                metrics.NutritionalCompletenessRatio = (double)fieldsPopulated / totalFields;
                metrics.HasMinimalNutritionalData = fieldsPopulated >= 3; // Pelo menos 3 campos
            }

            // 5. Determinar completude geral
            metrics.OverallCompleteness = DetermineCompleteness(metrics);

            // 6. Determinar recomendações
            (metrics.RecommendedConfidenceLevel, metrics.RecommendedMessage) = GetRecommendations(metrics);

            return metrics;
        }

        private ParsingCompletenessLevel DetermineCompleteness(ParsingQualityMetrics metrics)
        {
            // Score baseado em múltiplos fatores (0-100)
            int completenessScore = 0;

            // Identificação do produto (0-30 pontos)
            if (metrics.HasProductName && metrics.HasBrand)
                completenessScore += 30;
            else if (metrics.HasProductName || metrics.HasBrand)
                completenessScore += 15;

            // Ingredientes (0-30 pontos)
            if (metrics.HasValidIngredients && metrics.IngredientsCount >= 5)
                completenessScore += 30;
            else if (metrics.HasValidIngredients && metrics.IngredientsCount >= 3)
                completenessScore += 20;
            else if (metrics.HasIngredients)
                completenessScore += 10;

            // Informações nutricionais (0-25 pontos)
            if (metrics.HasNutritionalInfo)
            {
                if (metrics.NutritionalCompletenessRatio >= 0.70)
                    completenessScore += 25;
                else if (metrics.NutritionalCompletenessRatio >= 0.50)
                    completenessScore += 20;
                else if (metrics.NutritionalCompletenessRatio >= 0.30)
                    completenessScore += 15;
                else if (metrics.HasMinimalNutritionalData)
                    completenessScore += 10;
            }

            // Alérgenos (0-15 pontos - opcional mas importante)
            if (metrics.HasAllergens)
                completenessScore += 15;

            // Penalidades
            if (metrics.InvalidIngredientsRatio > 0.5)
                completenessScore -= 20; // Muitos ingredientes inválidos
            if (!metrics.HasProductName)
                completenessScore -= 15; // Produto não identificado é grave

            completenessScore = System.Math.Max(0, System.Math.Min(100, completenessScore));

            return completenessScore switch
            {
                >= 80 => ParsingCompletenessLevel.Complete,
                >= 60 => ParsingCompletenessLevel.Mostly,
                >= 40 => ParsingCompletenessLevel.Partial,
                _ => ParsingCompletenessLevel.Incomplete
            };
        }

        private (string confidenceLevel, string message) GetRecommendations(ParsingQualityMetrics metrics)
        {
            return metrics.OverallCompleteness switch
            {
                ParsingCompletenessLevel.Complete =>
                    ("Alto", "Informações completas do produto identificadas."),

                ParsingCompletenessLevel.Mostly =>
                    ("Médio", "Maioria das informações identificadas. Análise confiável."),

                ParsingCompletenessLevel.Partial =>
                    ("Médio", "Análise parcial do rótulo. Algumas informações não foram identificadas."),

                ParsingCompletenessLevel.Incomplete =>
                    ("Baixo", "Leitura incompleta. Tire outra foto mais próxima do rótulo nutricional."),

                _ =>
                    ("Baixo", "Completude desconhecida.")
            };
        }
    }

    /// <summary>
    /// Métricas de qualidade do parsing
    /// </summary>
    public class ParsingQualityMetrics
    {
        public bool HasProductName { get; set; }
        public bool HasBrand { get; set; }
        
        public int IngredientsCount { get; set; }
        public bool HasIngredients { get; set; }
        public bool HasValidIngredients { get; set; }
        public int InvalidIngredientsCount { get; set; }
        public double InvalidIngredientsRatio { get; set; }
        
        public int AllergensCount { get; set; }
        public bool HasAllergens { get; set; }
        
        public bool HasNutritionalInfo { get; set; }
        public int NutritionalFieldsPopulated { get; set; }
        public double NutritionalCompletenessRatio { get; set; }
        public bool HasMinimalNutritionalData { get; set; }
        
        public ParsingCompletenessLevel OverallCompleteness { get; set; }
        public string RecommendedConfidenceLevel { get; set; } = "Baixo";
        public string RecommendedMessage { get; set; } = string.Empty;
    }

    public enum ParsingCompletenessLevel
    {
        Incomplete,
        Partial,
        Mostly,
        Complete
    }
}
