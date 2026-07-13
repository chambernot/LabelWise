using LabelWise.Domain.Enums;

namespace LabelWise.Application.DTOs.LabelReading
{
    /// <summary>
    /// Resultado da leitura de rótulo.
    /// Contém todas as informações extraídas das diferentes partes do rótulo.
    /// </summary>
    public class LabelReadingResult
    {
        /// <summary>
        /// Indica se a leitura foi bem-sucedida.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Nível de confiança geral da leitura (0.0 a 1.0).
        /// Calculado como média ponderada da confiança de cada captura.
        /// </summary>
        public double OverallConfidence { get; set; }

        /// <summary>
        /// Resultados da leitura de cada tipo de captura.
        /// </summary>
        public List<CaptureReadingResult> CaptureResults { get; set; } = new();

        /// <summary>
        /// Informações nutricionais extraídas (consolidadas).
        /// </summary>
        public NutritionalInformationDto? NutritionalInfo { get; set; }

        /// <summary>
        /// Lista de ingredientes extraídos.
        /// </summary>
        public List<string> Ingredients { get; set; } = new();

        /// <summary>
        /// Lista de alérgenos identificados.
        /// </summary>
        public List<string> Allergens { get; set; } = new();

        /// <summary>
        /// Claims nutricionais identificados (ex: "Sem glúten", "Rico em fibras").
        /// </summary>
        public List<string> NutritionalClaims { get; set; } = new();

        /// <summary>
        /// Mensagem de erro (se Success = false).
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Warnings sobre qualidade da leitura.
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Metadados do processo de leitura.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>
        /// Tempo total de processamento em segundos.
        /// </summary>
        public double ProcessingTimeSeconds { get; set; }
    }

    /// <summary>
    /// Resultado da leitura de uma captura específica.
    /// </summary>
    public class CaptureReadingResult
    {
        /// <summary>
        /// Tipo de captura processada.
        /// </summary>
        public CaptureType CaptureType { get; set; }

        /// <summary>
        /// Indica se a leitura desta captura foi bem-sucedida.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Nível de confiança desta leitura (0.0 a 1.0).
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Texto bruto extraído por OCR.
        /// </summary>
        public string RawText { get; set; } = string.Empty;

        /// <summary>
        /// Provider de OCR utilizado (ex: "Tesseract", "Azure Vision").
        /// </summary>
        public string OcrProvider { get; set; } = string.Empty;

        /// <summary>
        /// Dados estruturados extraídos (JSON serializado).
        /// Ex: para NutritionTable, contém os valores nutricionais estruturados.
        /// </summary>
        public string? StructuredData { get; set; }

        /// <summary>
        /// Mensagem de erro (se Success = false).
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Metadados específicos desta captura.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>
        /// Tempo de processamento desta captura em segundos.
        /// </summary>
        public double ProcessingTimeSeconds { get; set; }
    }

    /// <summary>
    /// DTO para informações nutricionais consolidadas.
    /// </summary>
    public class NutritionalInformationDto
    {
        /// <summary>
        /// Tamanho da porção (ex: "30g", "200ml").
        /// </summary>
        public string? ServingSize { get; set; }

        /// <summary>
        /// Número de porções por embalagem.
        /// </summary>
        public double? ServingsPerContainer { get; set; }

        /// <summary>
        /// Valor energético (kcal).
        /// </summary>
        public double? Calories { get; set; }

        /// <summary>
        /// Carboidratos totais (g).
        /// </summary>
        public double? Carbohydrates { get; set; }

        /// <summary>
        /// Proteínas (g).
        /// </summary>
        public double? Proteins { get; set; }

        /// <summary>
        /// Gorduras totais (g).
        /// </summary>
        public double? TotalFat { get; set; }

        /// <summary>
        /// Gorduras saturadas (g).
        /// </summary>
        public double? SaturatedFat { get; set; }

        /// <summary>
        /// Gorduras trans (g).
        /// </summary>
        public double? TransFat { get; set; }

        /// <summary>
        /// Fibras alimentares (g).
        /// </summary>
        public double? Fiber { get; set; }

        /// <summary>
        /// Açúcares (g).
        /// </summary>
        public double? Sugars { get; set; }

        /// <summary>
        /// Sódio (mg).
        /// </summary>
        public double? Sodium { get; set; }

        /// <summary>
        /// Outros nutrientes identificados.
        /// </summary>
        public Dictionary<string, double> AdditionalNutrients { get; set; } = new();
    }
}
