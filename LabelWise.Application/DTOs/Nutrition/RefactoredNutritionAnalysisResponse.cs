using LabelWise.Domain.Enums;

namespace LabelWise.Application.DTOs.Nutrition
{
    /// <summary>
    /// Resposta refatorada da análise nutricional com separação clara entre
    /// dados extraídos visualmente e dados inferidos/estimados.
    /// </summary>
    public class RefactoredNutritionAnalysisResponse
    {
        /// <summary>
        /// Indica se a análise foi bem-sucedida.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Nome do produto identificado visualmente.
        /// </summary>
        public string? ProductName { get; set; }

        /// <summary>
        /// Marca do produto identificada visualmente.
        /// </summary>
        public string? Brand { get; set; }

        /// <summary>
        /// Categoria do produto (ex: "alimento achocolatado em pó instantâneo").
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Peso/volume da embalagem identificado (ex: "560 g").
        /// </summary>
        public string? PackageWeight { get; set; }

        /// <summary>
        /// Modo de análise que determina se os dados são extraídos ou estimados.
        /// </summary>
        public AnalysisMode AnalysisMode { get; set; }

        /// <summary>
        /// Claims ou declarações visíveis na embalagem.
        /// Ex: "Não contém glúten", "Fonte de vitaminas e minerais", etc.
        /// </summary>
        public List<string> VisibleClaims { get; set; } = new();

        /// <summary>
        /// Perfil nutricional estimado baseado na categoria do produto.
        /// Presente quando não há tabela nutricional oficial disponível.
        /// </summary>
        public EstimatedNutritionProfileDto? EstimatedNutritionProfile { get; set; }

        /// <summary>
        /// Classificação do produto para diferentes perfis de saúde.
        /// </summary>
        public ProfileClassificationDto? Classification { get; set; }

        /// <summary>
        /// Resumo técnico da análise.
        /// </summary>
        public string? Summary { get; set; }

        /// <summary>
        /// Detalhamento de confiança por seção da análise.
        /// </summary>
        public NutritionConfidenceDetailsDto? ConfidenceDetails { get; set; }

        /// <summary>
        /// Avisos e limitações da análise.
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Mensagem de erro caso a análise tenha falhado.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Tempo de processamento em segundos.
        /// </summary>
        public double ProcessingTimeSeconds { get; set; }

        public List<string> ResumoRapido { get; set; } = new();
        public string? ExplicacaoScore { get; set; }
        public string? PontoPrincipal { get; set; }
        public string Tom { get; set; } = "simples e direto";
    }
}
