using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Domain.Enums;

namespace LabelWise.Api.Models
{
    public class MobileNutritionAnalysisResponseDto
    {
        public Guid? AnalysisId { get; set; }
        public bool Success { get; set; }
        public string? ProductName { get; set; }
        public string? Brand { get; set; }
        public string? Category { get; set; }
        public string? PackageWeight { get; set; }
        public AnalysisMode AnalysisMode { get; set; }
        public List<string> VisibleClaims { get; set; } = new();
        public EstimatedNutritionProfileDto? EstimatedNutritionProfile { get; set; }
        public ProductClassificationDto? Classification { get; set; }
        public string? Summary { get; set; }
        public ConfidenceDetailsDto? ConfidenceDetails { get; set; }
        public List<string> Alerts { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public double ProcessingTimeSeconds { get; set; }
        public NutritionalScoreDto? NutritionalScore { get; set; }
        public string? PrincipalOffender { get; set; }
        public List<string> ResumoRapido { get; set; } = new();
        public string? ExplicacaoScore { get; set; }
        public string? PontoPrincipal { get; set; }
        public string Tom { get; set; } = "simples e direto";

        /// <summary>Score nutricional avançado com scores por perfil de saúde.</summary>
        public AdvancedNutritionScoreResult? AdvancedScore { get; set; }

        /// <summary>
        /// Dados enriquecidos pelo backend: perfil normalizado, fallback aplicado,
        /// nível de processamento e confiança. O JSON original da IA é imutável.
        /// </summary>
        public NutritionEnrichedData? Enriched { get; set; }
    }
}
