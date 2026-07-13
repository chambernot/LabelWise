namespace LabelWise.Application.Configuration
{
    /// <summary>
    /// Configurações para geração de resumo de análise.
    /// Permite alternar entre estratégias de geração (rule-based, IA, etc).
    /// </summary>
    public class SummaryGenerationOptions
    {
        /// <summary>
        /// Seção de configuração no appsettings.json
        /// </summary>
        public const string SectionName = "SummaryGeneration";

        /// <summary>
        /// Estratégia de geração: "RuleBased" ou "AiPowered"
        /// Default: "RuleBased"
        /// </summary>
        public string Strategy { get; set; } = "RuleBased";

        /// <summary>
        /// Configurações específicas do provedor de IA (quando Strategy = "AiPowered")
        /// </summary>
        public AiProviderOptions? AiProvider { get; set; }

        /// <summary>
        /// Habilita fallback para RuleBased se IA falhar
        /// </summary>
        public bool EnableFallback { get; set; } = true;

        /// <summary>
        /// Timeout em segundos para chamadas à IA
        /// </summary>
        public int AiTimeoutSeconds { get; set; } = 10;
    }

    /// <summary>
    /// Configurações do provedor de IA
    /// </summary>
    public class AiProviderOptions
    {
        /// <summary>
        /// Tipo de provedor: "OpenAI", "AzureOpenAI", "Custom"
        /// </summary>
        public string Provider { get; set; } = "AzureOpenAI";

        /// <summary>
        /// Endpoint da API
        /// </summary>
        public string? Endpoint { get; set; }

        /// <summary>
        /// Chave de API
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Nome do modelo/deployment
        /// </summary>
        public string? ModelName { get; set; }

        /// <summary>
        /// Temperatura para geração (0.0 - 1.0)
        /// </summary>
        public double Temperature { get; set; } = 0.7;

        /// <summary>
        /// Máximo de tokens na resposta
        /// </summary>
        public int MaxTokens { get; set; } = 200;
    }
}
