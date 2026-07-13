using System.Collections.Generic;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Parsing.Strategies
{
    /// <summary>
    /// Resultado do parsing de declarações de alérgenos.
    /// </summary>
    public class AllergenParseResult
    {
        /// <summary>
        /// Alérgenos confirmados (contém, contém derivados de)
        /// </summary>
        public List<string> ConfirmedAllergens { get; set; } = new();

        /// <summary>
        /// Alérgenos potenciais (pode conter)
        /// </summary>
        public List<string> MayContainAllergens { get; set; } = new();

        /// <summary>
        /// Alérgenos explicitamente negados (não contém, isento de)
        /// </summary>
        public List<string> DoesNotContainAllergens { get; set; } = new();

        /// <summary>
        /// Frases críticas extraídas do texto
        /// </summary>
        public List<string> ExtractedPhrases { get; set; } = new();

        // Qualidade do parsing
        public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.High;
        public List<string> ValidationWarnings { get; set; } = new();
        public bool HasConfirmedAllergens => ConfirmedAllergens?.Count > 0;
        public bool HasPotentialAllergens => MayContainAllergens?.Count > 0;
        public bool HasAllergenInfo => HasConfirmedAllergens || HasPotentialAllergens || DoesNotContainAllergens?.Count > 0;
    }
}
