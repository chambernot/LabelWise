namespace LabelWise.Application.Configuration
{
    /// <summary>
    /// Configuração do provedor de OCR.
    /// </summary>
    public class OcrOptions
    {
        /// <summary>
        /// Tipo de provedor a ser usado: "Tesseract", "Mock", "AzureComputerVision", "AzureVision", "Composite", "Selector".
        /// 
        /// PROVIDERS DISPONÍVEIS:
        /// - "Tesseract": OCR local gratuito (requer tessdata)
        /// - "AzureComputerVision": Azure Computer Vision (legado)
        /// - "AzureVision": Azure AI Vision Read API (recomendado para novos projetos)
        /// - "Selector": Tesseract primeiro, fallback para AzureVision se confiança baixa (RECOMENDADO)
        /// - "Composite": Múltiplos providers com fallback configurável
        /// - "Mock": Dados simulados para testes
        /// </summary>
        public string Provider { get; set; } = "Tesseract";

        /// <summary>
        /// Se true, usa MockOcrProvider independentemente do valor de Provider.
        /// IMPORTANTE: Deve ser false em produção.
        /// </summary>
        public bool UseMockProvider { get; set; } = false;

        /// <summary>
        /// Caminho customizado para o diretório tessdata (apenas para Tesseract).
        /// Se null, o sistema tentará detectar automaticamente.
        /// </summary>
        public string? TessdataPath { get; set; }

        /// <summary>
        /// Idiomas para OCR (ex: "por+eng" para Português + Inglês).
        /// Usado pelo Tesseract.
        /// </summary>
        public string Language { get; set; } = "por+eng";

        /// <summary>
        /// Se true, valida na inicialização se o Tesseract está configurado corretamente.
        /// </summary>
        public bool ValidateOnStartup { get; set; } = true;

        /// <summary>
        /// Configurações do Azure Computer Vision OCR.
        /// </summary>
        public AzureOcrOptions Azure { get; set; } = new AzureOcrOptions();

        /// <summary>
        /// Configurações do Azure AI Vision Read OCR (recomendado).
        /// </summary>
        public AzureVisionOptions AzureVision { get; set; } = new AzureVisionOptions();

        /// <summary>
        /// Configurações do Selector (Tesseract → Azure fallback).
        /// </summary>
        public SelectorOptions Selector { get; set; } = new SelectorOptions();

        /// <summary>
        /// Configurações do provider composto (quando Provider = "Composite").
        /// </summary>
        public CompositeOcrOptions Composite { get; set; } = new CompositeOcrOptions();
    }

    /// <summary>
    /// Configurações específicas do Azure Computer Vision.
    /// </summary>
    public class AzureOcrOptions
    {
        /// <summary>
        /// Endpoint do recurso Azure Computer Vision.
        /// Exemplo: "https://your-resource.cognitiveservices.azure.com/"
        /// </summary>
        public string? Endpoint { get; set; }

        /// <summary>
        /// Chave de API do recurso Azure Computer Vision.
        /// Pode ser obtida no portal Azure.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Se true, valida a conectividade com Azure na inicialização.
        /// </summary>
        public bool ValidateOnStartup { get; set; } = false;
    }

    /// <summary>
    /// Configurações do provider composto (múltiplos providers com fallback).
    /// </summary>
    public class CompositeOcrOptions
    {
        /// <summary>
        /// Provider primário a ser usado.
        /// Valores: "AzureComputerVision", "AzureVision", "Tesseract".
        /// </summary>
        public string PrimaryProvider { get; set; } = "AzureComputerVision";

        /// <summary>
        /// Provider de fallback caso o primário falhe ou tenha baixa confiança.
        /// Valores: "Tesseract", "AzureComputerVision", "AzureVision".
        /// </summary>
        public string FallbackProvider { get; set; } = "Tesseract";

        /// <summary>
        /// Threshold de confiança mínima para aceitar resultado do provider primário.
        /// Se confiança < threshold, tenta o fallback.
        /// Valor entre 0.0 e 1.0. Default: 0.85 (85%).
        /// </summary>
        public double ConfidenceThreshold { get; set; } = 0.85;
    }

    /// <summary>
    /// Configurações do OcrProviderSelector (Tesseract → Azure fallback).
    /// </summary>
    public class SelectorOptions
    {
        /// <summary>
        /// Threshold de confiança para usar Azure como fallback.
        /// Se Tesseract retornar confiança < threshold, executa Azure Vision.
        /// Valor entre 0.0 e 1.0. Default: 0.85 (85%).
        /// 
        /// RECOMENDAÇÕES:
        /// - 0.90 (90%): Mais agressivo, usa Azure com mais frequência (maior custo, maior qualidade)
        /// - 0.85 (85%): Balanceado (recomendado)
        /// - 0.75 (75%): Mais conservador, usa Azure apenas em casos críticos (menor custo)
        /// </summary>
        public double UseAzureWhenTesseractConfidenceBelow { get; set; } = 0.85;

        /// <summary>
        /// Se true, sempre executa ambos os providers e escolhe o melhor resultado.
        /// Se false, executa Azure apenas se Tesseract tiver baixa confiança.
        /// Default: false (economia de custos).
        /// </summary>
        public bool AlwaysExecuteBoth { get; set; } = false;
    }
}
