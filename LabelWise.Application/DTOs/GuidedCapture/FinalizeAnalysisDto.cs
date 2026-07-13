using LabelWise.Application.DTOs.Analysis;

namespace LabelWise.Application.DTOs.GuidedCapture
{
    /// <summary>
    /// Request para finalizar análise de uma sessão guiada.
    /// </summary>
    public class FinalizeAnalysisRequest
    {
        /// <summary>
        /// ID da sessão a ser finalizada.
        /// </summary>
        public Guid SessionId { get; set; }

        /// <summary>
        /// Forçar análise mesmo se etapas obrigatórias não foram completadas.
        /// </summary>
        public bool ForceAnalysis { get; set; }

        /// <summary>
        /// ID do usuário para personalização da análise.
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// Incluir recomendações personalizadas baseadas no perfil do usuário.
        /// </summary>
        public bool IncludePersonalizedRecommendations { get; set; } = true;

        /// <summary>
        /// Nível de detalhe da explicação.
        /// </summary>
        public string ExplanationLevel { get; set; } = "Standard";
    }

    /// <summary>
    /// Response da análise final consolidada.
    /// </summary>
    public class FinalizeAnalysisResponse
    {
        /// <summary>
        /// Indica se a análise foi concluída com sucesso.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// ID da sessão.
        /// </summary>
        public Guid SessionId { get; set; }

        /// <summary>
        /// ID da análise gerada.
        /// </summary>
        public Guid? AnalysisId { get; set; }

        /// <summary>
        /// ID do produto consolidado.
        /// </summary>
        public Guid? ProductId { get; set; }

        /// <summary>
        /// Informações consolidadas do produto.
        /// </summary>
        public ConsolidatedProductDto? Product { get; set; }

        /// <summary>
        /// Resultado da análise nutricional.
        /// </summary>
        public NutritionalAnalysisResultDto? NutritionalAnalysis { get; set; }

        /// <summary>
        /// Resumo executivo da análise para exibição rápida.
        /// </summary>
        public AnalysisSummaryDto? Summary { get; set; }

        /// <summary>
        /// Alertas identificados.
        /// </summary>
        public List<AlertDto> Alerts { get; set; } = [];

        /// <summary>
        /// Recomendações geradas.
        /// </summary>
        public List<RecommendationDto> Recommendations { get; set; } = [];

        /// <summary>
        /// Metadados da análise.
        /// </summary>
        public AnalysisMetadataDto Metadata { get; set; } = new();

        /// <summary>
        /// Confiança geral da análise (0.0 a 1.0).
        /// </summary>
        public decimal OverallConfidence { get; set; }

        /// <summary>
        /// Detalhes de confiança por dimensão.
        /// </summary>
        public ConfidenceBreakdownDto? ConfidenceBreakdown { get; set; }

        /// <summary>
        /// Mensagem de erro (se Success = false).
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Warnings sobre a análise.
        /// </summary>
        public List<string> Warnings { get; set; } = [];
    }

    /// <summary>
    /// Produto consolidado a partir das capturas.
    /// </summary>
    public class ConsolidatedProductDto
    {
        /// <summary>
        /// ID do produto.
        /// </summary>
        public Guid ProductId { get; set; }

        /// <summary>
        /// Nome do produto.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Marca do produto.
        /// </summary>
        public string? Brand { get; set; }

        /// <summary>
        /// Código de barras.
        /// </summary>
        public string? Barcode { get; set; }

        /// <summary>
        /// Categoria inferida.
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Lista completa de ingredientes.
        /// </summary>
        public List<string> Ingredients { get; set; } = [];

        /// <summary>
        /// Alérgenos declarados.
        /// </summary>
        public List<string> Allergens { get; set; } = [];

        /// <summary>
        /// Informações nutricionais detalhadas.
        /// </summary>
        public GuidedCaptureNutritionalInfoDto? NutritionalInfo { get; set; }

        /// <summary>
        /// Claims identificados na embalagem.
        /// </summary>
        public List<string> Claims { get; set; } = [];

        /// <summary>
        /// Fonte dos dados (OCR, Cache, External Database).
        /// </summary>
        public string DataSource { get; set; } = string.Empty;

        /// <summary>
        /// Indica se os dados foram validados.
        /// </summary>
        public bool IsValidated { get; set; }
    }

    /// <summary>
    /// Informações nutricionais detalhadas para captura guiada.
    /// </summary>
    public class GuidedCaptureNutritionalInfoDto
    {
        public string? ServingSize { get; set; }
        public decimal? Calories { get; set; }
        public decimal? TotalFat { get; set; }
        public decimal? SaturatedFat { get; set; }
        public decimal? TransFat { get; set; }
        public decimal? Cholesterol { get; set; }
        public decimal? Sodium { get; set; }
        public decimal? TotalCarbohydrates { get; set; }
        public decimal? DietaryFiber { get; set; }
        public decimal? Sugars { get; set; }
        public decimal? AddedSugars { get; set; }
        public decimal? Proteins { get; set; }
    }

    /// <summary>
    /// Resultado da análise nutricional.
    /// </summary>
    public class NutritionalAnalysisResultDto
    {
        /// <summary>
        /// Score geral (0-100).
        /// </summary>
        public int OverallScore { get; set; }

        /// <summary>
        /// Classificação geral (Excelente, Bom, Moderado, Ruim).
        /// </summary>
        public string Classification { get; set; } = string.Empty;

        /// <summary>
        /// Cor indicativa (verde, amarelo, laranja, vermelho).
        /// </summary>
        public string IndicatorColor { get; set; } = string.Empty;

        /// <summary>
        /// Nutri-Score (A-E), se aplicável.
        /// </summary>
        public string? NutriScore { get; set; }

        /// <summary>
        /// NOVA Score (1-4), se aplicável.
        /// </summary>
        public int? NovaScore { get; set; }

        /// <summary>
        /// Scores por categoria.
        /// </summary>
        public CategoryScoresDto? CategoryScores { get; set; }

        /// <summary>
        /// Indicadores críticos (semáforo nutricional).
        /// </summary>
        public NutritionalTrafficLightDto? TrafficLight { get; set; }
    }

    /// <summary>
    /// Scores por categoria nutricional.
    /// </summary>
    public class CategoryScoresDto
    {
        public int? SugarScore { get; set; }
        public int? SodiumScore { get; set; }
        public int? SaturatedFatScore { get; set; }
        public int? FiberScore { get; set; }
        public int? ProteinScore { get; set; }
    }

    /// <summary>
    /// Semáforo nutricional.
    /// </summary>
    public class NutritionalTrafficLightDto
    {
        public string FatLevel { get; set; } = string.Empty;      // Green, Yellow, Red
        public string SaturatesLevel { get; set; } = string.Empty;
        public string SugarsLevel { get; set; } = string.Empty;
        public string SaltLevel { get; set; } = string.Empty;
    }

    /// <summary>
    /// Resumo executivo da análise.
    /// </summary>
    public class AnalysisSummaryDto
    {
        /// <summary>
        /// Título do resumo.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Descrição curta (1-2 frases).
        /// </summary>
        public string ShortDescription { get; set; } = string.Empty;

        /// <summary>
        /// Principais pontos positivos.
        /// </summary>
        public List<string> Positives { get; set; } = [];

        /// <summary>
        /// Principais pontos de atenção.
        /// </summary>
        public List<string> Concerns { get; set; } = [];

        /// <summary>
        /// Veredicto final (uma frase).
        /// </summary>
        public string Verdict { get; set; } = string.Empty;

        /// <summary>
        /// Indicador visual sugerido (emoji ou ícone).
        /// </summary>
        public string VisualIndicator { get; set; } = string.Empty;
    }

    /// <summary>
    /// Alerta identificado na análise.
    /// </summary>
    public class AlertDto
    {
        public string Type { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;  // Critical, Warning, Info
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? IconName { get; set; }
    }

    /// <summary>
    /// Recomendação gerada pela análise.
    /// </summary>
    public class RecommendationDto
    {
        public string Type { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;  // High, Medium, Low
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ActionText { get; set; }
        public bool IsPersonalized { get; set; }
    }

    /// <summary>
    /// Breakdown de confiança por dimensão.
    /// </summary>
    public class ConfidenceBreakdownDto
    {
        public decimal OcrConfidence { get; set; }
        public decimal ParsingConfidence { get; set; }
        public decimal DataCompletenessConfidence { get; set; }
        public decimal AnalysisConfidence { get; set; }
    }
}
