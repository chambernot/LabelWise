using LabelWise.Domain.Enums;

namespace LabelWise.Application.DTOs.GuidedCapture
{
    /// <summary>
    /// Request para adicionar uma captura a uma sessão guiada.
    /// </summary>
    public class AddCaptureRequest
    {
        /// <summary>
        /// ID da sessão (obrigatório).
        /// </summary>
        public Guid SessionId { get; set; }

        /// <summary>
        /// Tipo de captura sendo enviada.
        /// </summary>
        public CaptureType CaptureType { get; set; }

        /// <summary>
        /// Código de barras (para capturas de código de barras).
        /// </summary>
        public string? Barcode { get; set; }

        /// <summary>
        /// Idioma para OCR.
        /// </summary>
        public string LanguageCode { get; set; } = "pt";

        /// <summary>
        /// Habilitar fallback para múltiplos providers OCR.
        /// </summary>
        public bool EnableMultiProviderOcr { get; set; } = true;

        /// <summary>
        /// Buscar em bases externas (Open Food Facts).
        /// </summary>
        public bool EnableExternalLookup { get; set; } = true;
    }

    /// <summary>
    /// Response ao adicionar uma captura.
    /// </summary>
    public class AddCaptureResponse
    {
        /// <summary>
        /// Indica se a captura foi processada com sucesso.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// ID da captura criada.
        /// </summary>
        public Guid CaptureId { get; set; }

        /// <summary>
        /// Tipo de captura processada.
        /// </summary>
        public CaptureType CaptureType { get; set; }

        /// <summary>
        /// Confiança do processamento OCR (0.0 a 1.0).
        /// </summary>
        public decimal Confidence { get; set; }

        /// <summary>
        /// Tempo de processamento em milissegundos.
        /// </summary>
        public int ProcessingTimeMs { get; set; }

        /// <summary>
        /// Dados extraídos relevantes para preview no app.
        /// </summary>
        public CaptureExtractedDataDto? ExtractedData { get; set; }

        /// <summary>
        /// Status atualizado da sessão.
        /// </summary>
        public GuidedCaptureSessionDto SessionStatus { get; set; } = null!;

        /// <summary>
        /// Mensagem de erro (se Success = false).
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Warnings sobre qualidade da captura.
        /// </summary>
        public List<string> Warnings { get; set; } = [];

        /// <summary>
        /// Sugestão para melhorar a captura (se qualidade baixa).
        /// </summary>
        public string? ImprovementSuggestion { get; set; }
    }

    /// <summary>
    /// Dados extraídos de uma captura para preview.
    /// </summary>
    public class CaptureExtractedDataDto
    {
        /// <summary>
        /// Texto bruto extraído (resumido).
        /// </summary>
        public string? RawTextSummary { get; set; }

        /// <summary>
        /// Nome do produto identificado.
        /// </summary>
        public string? ProductName { get; set; }

        /// <summary>
        /// Marca identificada.
        /// </summary>
        public string? Brand { get; set; }

        /// <summary>
        /// Código de barras detectado.
        /// </summary>
        public string? Barcode { get; set; }

        /// <summary>
        /// Quantidade de ingredientes identificados.
        /// </summary>
        public int? IngredientsCount { get; set; }

        /// <summary>
        /// Lista de ingredientes principais (primeiros 5).
        /// </summary>
        public List<string>? MainIngredients { get; set; }

        /// <summary>
        /// Alérgenos detectados.
        /// </summary>
        public List<string>? Allergens { get; set; }

        /// <summary>
        /// Valores nutricionais principais.
        /// </summary>
        public NutritionPreviewDto? NutritionPreview { get; set; }

        /// <summary>
        /// Claims identificados na embalagem frontal.
        /// </summary>
        public List<string>? Claims { get; set; }
    }

    /// <summary>
    /// Preview dos valores nutricionais.
    /// </summary>
    public class NutritionPreviewDto
    {
        /// <summary>
        /// Calorias (kcal).
        /// </summary>
        public decimal? Calories { get; set; }

        /// <summary>
        /// Carboidratos (g).
        /// </summary>
        public decimal? Carbohydrates { get; set; }

        /// <summary>
        /// Proteínas (g).
        /// </summary>
        public decimal? Proteins { get; set; }

        /// <summary>
        /// Gorduras totais (g).
        /// </summary>
        public decimal? TotalFat { get; set; }

        /// <summary>
        /// Sódio (mg).
        /// </summary>
        public decimal? Sodium { get; set; }

        /// <summary>
        /// Açúcares (g).
        /// </summary>
        public decimal? Sugars { get; set; }

        /// <summary>
        /// Porção de referência.
        /// </summary>
        public string? ServingSize { get; set; }
    }
}
