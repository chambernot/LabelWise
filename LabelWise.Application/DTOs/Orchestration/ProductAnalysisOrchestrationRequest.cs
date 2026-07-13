namespace LabelWise.Application.DTOs.Orchestration
{
    /// <summary>
    /// Request para o orquestrador do pipeline completo de análise de produtos.
    /// Coordena as etapas de identificação, leitura de rótulo e análise nutricional.
    /// </summary>
    public class ProductAnalysisOrchestrationRequest
    {
        /// <summary>
        /// ID do usuário que está fazendo a requisição.
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Request de identificação do produto.
        /// </summary>
        public required ProductIdentification.ProductIdentificationRequest IdentificationRequest { get; set; }

        /// <summary>
        /// Request de leitura do rótulo (opcional na primeira chamada).
        /// Pode ser fornecido posteriormente se a identificação for bem-sucedida.
        /// </summary>
        public LabelReading.LabelReadingRequest? LabelReadingRequest { get; set; }

        /// <summary>
        /// Indica se deve executar a análise nutricional após leitura bem-sucedida.
        /// </summary>
        public bool ExecuteNutritionalAnalysis { get; set; } = true;

        /// <summary>
        /// Indica se deve executar análise de qualidade do processo (quality gate).
        /// </summary>
        public bool EnableQualityGate { get; set; } = true;

        /// <summary>
        /// Threshold mínimo para considerar o pipeline bem-sucedido (0.0 a 1.0).
        /// Se a confiança geral for menor, retorna warning ou erro.
        /// </summary>
        public double MinimumConfidenceThreshold { get; set; } = 0.70;

        /// <summary>
        /// Indica se deve salvar o histórico de análise.
        /// </summary>
        public bool SaveAnalysisHistory { get; set; } = true;

        /// <summary>
        /// Preferências do usuário para análise personalizada (opcional).
        /// </summary>
        public UserAnalysisPreferences? UserPreferences { get; set; }
    }

    /// <summary>
    /// Preferências do usuário para análise personalizada.
    /// </summary>
    public class UserAnalysisPreferences
    {
        /// <summary>
        /// Objetivos nutricionais do usuário.
        /// </summary>
        public List<string> Goals { get; set; } = new();

        /// <summary>
        /// Restrições alimentares do usuário.
        /// </summary>
        public List<string> DietaryRestrictions { get; set; } = new();

        /// <summary>
        /// Alérgenos a serem destacados.
        /// </summary>
        public List<string> AllergenAlerts { get; set; } = new();

        /// <summary>
        /// Nível de detalhe desejado na análise.
        /// </summary>
        public string DetailLevel { get; set; } = "Standard"; // "Basic", "Standard", "Detailed"
    }
}
