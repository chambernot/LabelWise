namespace LabelWise.Application.Configuration
{
    /// <summary>
    /// Configurações específicas do Azure AI Vision Read OCR.
    /// 
    /// Azure AI Vision oferece a API "Read" otimizada para texto impresso e manuscrito.
    /// É ideal para rótulos de produtos, documentos escaneados e fotos de celular.
    /// 
    /// REQUISITOS:
    /// 1. Recurso "Azure AI Vision" criado no portal Azure
    /// 2. Endpoint e ApiKey configurados em appsettings.json
    /// 3. Pacote NuGet: Azure.AI.Vision.ImageAnalysis
    /// 
    /// CUSTOS (2024):
    /// - Free: Até 5.000 transações/mês
    /// - Standard S1: $1.00 por 1.000 transações
    /// 
    /// Documentação: https://learn.microsoft.com/azure/ai-services/computer-vision/overview-ocr
    /// </summary>
    public class AzureVisionOptions
    {
        /// <summary>
        /// Endpoint do recurso Azure AI Vision.
        /// Exemplo: "https://your-resource.cognitiveservices.azure.com/"
        /// Obtido no portal Azure após criar o recurso.
        /// </summary>
        public string? Endpoint { get; set; }

        /// <summary>
        /// Chave de API (Key 1 ou Key 2) do recurso Azure AI Vision.
        /// Encontrado em: Portal Azure → Seu recurso → Keys and Endpoint
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Se true, valida a conectividade com Azure na inicialização da aplicação.
        /// Útil para detectar problemas de configuração cedo.
        /// </summary>
        public bool ValidateOnStartup { get; set; } = false;

        /// <summary>
        /// Timeout para chamadas à API do Azure (em segundos).
        /// Default: 30 segundos.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Idioma preferencial para OCR.
        /// Suporta: "pt" (Português), "en" (Inglês), "es" (Espanhol), etc.
        /// Default: "pt" (Português do Brasil).
        /// </summary>
        public string Language { get; set; } = "pt";

        /// <summary>
        /// Threshold de confiança mínima para aceitar resultado (0.0 - 1.0).
        /// Se o resultado tiver confiança abaixo deste valor, pode acionar fallback.
        /// Default: 0.85 (85%).
        /// </summary>
        public double MinimumConfidence { get; set; } = 0.85;

        /// <summary>
        /// Se true, registra informações detalhadas sobre as chamadas Azure (debug).
        /// Útil para troubleshooting mas pode gerar muitos logs.
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = false;
    }
}
