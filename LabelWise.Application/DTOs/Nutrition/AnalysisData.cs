using LabelWise.Domain.Enums;

namespace LabelWise.Application.DTOs.Nutrition
{
    /// <summary>
    /// Dados originais retornados pela IA — imutável após extração.
    /// Nunca deve ser modificado pelo backend.
    /// </summary>
    public class AnalysisData
    {
        public string? ProductName { get; set; }
        public string? Brand { get; set; }
        public string? Category { get; set; }
        public string? PackageWeight { get; set; }
        public AnalysisMode AnalysisMode { get; set; }
        public List<string> VisibleClaims { get; set; } = new();
        public List<string> Ingredients { get; set; } = new();

        /// <summary>Perfil nutricional bruto extraído pela IA (por 100g).</summary>
        public EstimatedNutritionProfileDto? NutritionProfile { get; set; }

        public ConfidenceDetailsDto? ConfidenceDetails { get; set; }
    }
}
