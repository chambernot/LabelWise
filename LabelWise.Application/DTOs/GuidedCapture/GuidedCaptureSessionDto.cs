using LabelWise.Domain.Enums;

namespace LabelWise.Application.DTOs.GuidedCapture
{
    /// <summary>
    /// Representa o estado atual de uma sessão de captura guiada.
    /// </summary>
    public class GuidedCaptureSessionDto
    {
        /// <summary>
        /// Identificador único da sessão.
        /// </summary>
        /// <example>550e8400-e29b-41d4-a716-446655440000</example>
        public Guid SessionId { get; set; }

        /// <summary>
        /// Status atual da sessão.
        /// </summary>
        /// <example>Capturing</example>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Data/hora de início da sessão (UTC).
        /// </summary>
        public DateTimeOffset StartedAt { get; set; }

        /// <summary>
        /// Data/hora de conclusão da sessão (UTC), se aplicável.
        /// </summary>
        public DateTimeOffset? CompletedAt { get; set; }

        /// <summary>
        /// Progresso das etapas de captura.
        /// </summary>
        public CaptureStepsProgressDto Progress { get; set; } = new();

        /// <summary>
        /// Próxima etapa recomendada para o usuário.
        /// </summary>
        public NextStepRecommendationDto? NextStep { get; set; }

        /// <summary>
        /// Código de barras detectado (se disponível).
        /// </summary>
        public string? DetectedBarcode { get; set; }

        /// <summary>
        /// Nome do produto identificado (se disponível).
        /// </summary>
        public string? IdentifiedProductName { get; set; }

        /// <summary>
        /// Indica se o produto foi encontrado em cache/base externa.
        /// </summary>
        public bool ProductFromCache { get; set; }

        /// <summary>
        /// Confiança geral da sessão até o momento (0.0 a 1.0).
        /// </summary>
        public decimal CurrentConfidence { get; set; }

        /// <summary>
        /// Lista de capturas já realizadas nesta sessão.
        /// </summary>
        public List<CaptureStepResultDto> CompletedCaptures { get; set; } = [];

        /// <summary>
        /// Mensagem de erro (se houver).
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Progresso das etapas de captura.
    /// </summary>
    public class CaptureStepsProgressDto
    {
        /// <summary>
        /// Total de etapas disponíveis.
        /// </summary>
        public int TotalSteps { get; set; } = 5;

        /// <summary>
        /// Etapas já completadas.
        /// </summary>
        public int CompletedSteps { get; set; }

        /// <summary>
        /// Percentual de conclusão (0-100).
        /// </summary>
        public int PercentComplete => TotalSteps > 0 ? (CompletedSteps * 100) / TotalSteps : 0;

        /// <summary>
        /// Indica se a embalagem frontal foi capturada.
        /// </summary>
        public bool FrontPackagingCaptured { get; set; }

        /// <summary>
        /// Indica se os ingredientes foram capturados.
        /// </summary>
        public bool IngredientsListCaptured { get; set; }

        /// <summary>
        /// Indica se a tabela nutricional foi capturada.
        /// </summary>
        public bool NutritionTableCaptured { get; set; }

        /// <summary>
        /// Indica se os alérgenos foram capturados.
        /// </summary>
        public bool AllergenStatementCaptured { get; set; }

        /// <summary>
        /// Indica se o código de barras foi capturado.
        /// </summary>
        public bool BarcodeCaptured { get; set; }

        /// <summary>
        /// Indica se as etapas obrigatórias foram completadas.
        /// </summary>
        public bool RequiredStepsComplete => NutritionTableCaptured && IngredientsListCaptured;

        /// <summary>
        /// Indica se a sessão está pronta para análise final.
        /// </summary>
        public bool ReadyForAnalysis => RequiredStepsComplete;
    }

    /// <summary>
    /// Recomendação da próxima etapa.
    /// </summary>
    public class NextStepRecommendationDto
    {
        /// <summary>
        /// Tipo de captura recomendado.
        /// </summary>
        public CaptureType CaptureType { get; set; }

        /// <summary>
        /// Nome amigável da etapa.
        /// </summary>
        /// <example>Tabela Nutricional</example>
        public string StepName { get; set; } = string.Empty;

        /// <summary>
        /// Descrição do que capturar.
        /// </summary>
        /// <example>Fotografe a tabela de informação nutricional do produto</example>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Dicas para uma boa captura.
        /// </summary>
        public List<string> Tips { get; set; } = [];

        /// <summary>
        /// Indica se esta etapa é obrigatória.
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Ordem sugerida da etapa (1-5).
        /// </summary>
        public int SuggestedOrder { get; set; }
    }

    /// <summary>
    /// Resultado de uma etapa de captura.
    /// </summary>
    public class CaptureStepResultDto
    {
        /// <summary>
        /// Identificador da captura.
        /// </summary>
        public Guid CaptureId { get; set; }

        /// <summary>
        /// Tipo de captura realizada.
        /// </summary>
        public CaptureType CaptureType { get; set; }

        /// <summary>
        /// Nome amigável do tipo de captura.
        /// </summary>
        public string CaptureTypeName { get; set; } = string.Empty;

        /// <summary>
        /// Indica se a captura foi bem-sucedida.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Confiança do OCR (0.0 a 1.0).
        /// </summary>
        public decimal Confidence { get; set; }

        /// <summary>
        /// Data/hora da captura.
        /// </summary>
        public DateTimeOffset CapturedAt { get; set; }

        /// <summary>
        /// Tempo de processamento em milissegundos.
        /// </summary>
        public int ProcessingTimeMs { get; set; }

        /// <summary>
        /// Resumo do que foi extraído.
        /// </summary>
        public string? ExtractedSummary { get; set; }

        /// <summary>
        /// Warnings sobre qualidade da captura.
        /// </summary>
        public List<string> Warnings { get; set; } = [];

        /// <summary>
        /// Indica se esta captura pode ser refeita.
        /// </summary>
        public bool CanRetry { get; set; } = true;
    }
}
