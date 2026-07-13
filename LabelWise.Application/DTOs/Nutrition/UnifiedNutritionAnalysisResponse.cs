using LabelWise.Application.Models.Nutrition;
using LabelWise.Application.DTOs.FoodAnalysisTrust;

namespace LabelWise.Application.DTOs.Nutrition
{
    /// <summary>
    /// Resposta unificada do endpoint analyze-simple-image.
    ///
    /// Arquitetura:
    ///   analysis  → dados originais da IA (imutável)
    ///   enriched  → validação + fallback do backend
    ///   score     → único score calculado pelo backend
    /// </summary>
    public class UnifiedNutritionAnalysisResponse
    {
        public Guid? AnalysisId { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public double ProcessingTimeSeconds { get; set; }

        /// <summary>Indica se foi detectada uma tabela nutricional na imagem.</summary>
        public bool HasNutritionTable { get; set; }

        /// <summary>Indica se há dados nutricionais mínimos para realizar análise confiável.</summary>
        public bool HasMinimumNutritionData { get; set; }

        /// <summary>
        /// Estado determinístico da análise (única fonte de verdade — definido pela
        /// <c>INutritionStateMachine</c>). Serializado como string para o frontend.
        /// </summary>
        public string State { get; set; } = nameof(Models.Nutrition.NutritionAnalysisState.NoData);

        /// <summary>
        /// Tipo de análise realizada:
        /// - "full": Tabela nutricional completa detectada
        /// - "partial": Alguns dados detectados, complementados com fallback
        /// - "category_only": Apenas categoria identificada, dados estimados
        /// - "insufficient": Dados insuficientes para análise confiável
        /// </summary>
        public string NutritionDataQuality { get; set; } = "insufficient";

        /// <summary>Dados originais da IA — nunca modificados pelo backend.</summary>
        public AnalysisData Analysis { get; set; } = new();

        /// <summary>Dados enriquecidos pelo backend: validação, fallback e confiança.</summary>
        public NutritionEnrichedData Enriched { get; set; } = new();

        /// <summary>Score unificado — única fonte de verdade para pontuação. Null quando não há dados nutricionais válidos.</summary>
        public UnifiedNutritionScore? Score { get; set; }

        /// <summary>Insights de consumo para perfis específicos de usuário. Null quando não há dados nutricionais válidos.</summary>
        public UserProfileInsightsDto? Profiles { get; set; }

        public NutritionProcessingClassificationDto ProcessingClassification { get; set; } = new();

        public List<NutritionQuickFlagDto> QuickFlags { get; set; } = new();

        public ImageQualityInfo ImageQuality { get; set; } = new();

        public NutritionAnalysisQualityDto AnalysisQuality { get; set; } = new();

        public int NutritionReliabilityScore { get; set; }

        public FoodAnalysisTrustReport Trust { get; set; } = new();

        public IngredientContextDto IngredientContext { get; set; } = new();
    }
}
