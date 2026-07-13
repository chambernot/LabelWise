using LabelWise.Domain.Enums;

namespace LabelWise.Application.DTOs.Nutrition
{
    public class NutritionAnalysisResponseDto
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
        public List<string> Warnings { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public double ProcessingTimeSeconds { get; set; }
        public NutritionalScore? Score { get; set; }
        public List<string> Alerts { get; set; } = new();
        public string? PrincipalOffender { get; set; }
        public UserProfileInsightsDto Profiles { get; set; } = new();
        public List<string> ResumoRapido { get; set; } = new();
        public string? ExplicacaoScore { get; set; }
        public string? PontoPrincipal { get; set; }
        public string Tom { get; set; } = "simples e direto";

        /// <summary>
        /// Indica se os dados nutricionais são confiáveis (extraídos da tabela nutricional real)
        /// ou se são estimativas baseadas apenas na categoria
        /// </summary>
        public bool HasReliableNutritionData { get; set; }
        public string DataSource { get; set; } = "Inferred";
        public string ProductForm { get; set; } = "Unknown";
        public bool IsInconsistent { get; set; }
        public bool IsNutritionLocked { get; set; }
        public List<string> NutritionFlags { get; set; } = new();

        /// <summary>
        /// Tipo de fallback aplicado nos dados nutricionais:
        /// - "real": dados extraídos da tabela nutricional
        /// - "partial": alguns dados reais, outros estimados
        /// - "category_based": estimativas baseadas apenas na categoria
        /// - "unknown": sem dados nutricionais disponíveis
        /// </summary>
        public string FallbackType { get; set; } = "unknown";

        /// <summary>
        /// Riscos nutricionais inferidos com base na categoria, ingredientes e claims
        /// (ex: "alto_sodio", "alto_acucar", "ultraprocessado")
        /// </summary>
        public List<string> InferredRisks { get; set; } = new();

        /// <summary>
        /// Lista de ingredientes identificados no produto, quando disponível.
        /// </summary>
        public List<string> Ingredients { get; set; } = new();

        /// <summary>
        /// Linhas brutas da tabela nutricional exatamente como extraídas da imagem.
        /// Campo opcional para uso interno de reconstrução da tabela no pipeline.
        /// </summary>
        public List<string>? RawExtractedText { get; set; }
    }
}
