using System.Collections.Generic;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Parsing.Strategies
{
    /// <summary>
    /// Resultado do parsing de lista de ingredientes.
    /// </summary>
    public class IngredientsParseResult
    {
        public List<string> Ingredients { get; set; } = new();
        public string? RawIngredientsSection { get; set; }

        // Qualidade do parsing
        public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.High;
        public List<string> ValidationWarnings { get; set; } = new();
        public bool HasIngredients => Ingredients?.Count > 0;
    }
}
