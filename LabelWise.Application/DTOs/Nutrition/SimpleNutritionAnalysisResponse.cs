namespace LabelWise.Application.DTOs.Nutrition
{
    /// <summary>
    /// Response da análise nutricional simplificada.
    /// </summary>
    public class SimpleNutritionAnalysisResponse
    {
        /// <summary>
        /// Indica se a análise foi bem-sucedida.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Nome do produto identificado.
        /// </summary>
        public string? ProductName { get; set; }

        /// <summary>
        /// Marca do produto identificada.
        /// </summary>
        public string? Brand { get; set; }

        /// <summary>
        /// Categoria do produto (ex: "Biscoito doce / amanteigado").
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Peso da embalagem identificado (ex: "350g").
        /// </summary>
        public string? PackageWeight { get; set; }

        /// <summary>
        /// Dados nutricionais estimados.
        /// </summary>
        public EstimatedNutritionDto? EstimatedNutrition { get; set; }

        /// <summary>
        /// Classificação do produto para diferentes perfis de saúde.
        /// </summary>
        public ProfileClassificationDto? Classification { get; set; }

        /// <summary>
        /// Resumo da análise.
        /// </summary>
        public string? Summary { get; set; }

        /// <summary>
        /// Confiança geral da análise (0.0 a 1.0).
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Avisos ou limitações da análise.
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Mensagem de erro (se Success = false).
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Tempo de processamento em segundos.
        /// </summary>
        public double ProcessingTimeSeconds { get; set; }
    }
}
