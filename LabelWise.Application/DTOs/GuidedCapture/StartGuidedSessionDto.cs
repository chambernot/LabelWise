using LabelWise.Domain.Enums;

namespace LabelWise.Application.DTOs.GuidedCapture
{
    /// <summary>
    /// Request para iniciar uma nova sessão de captura guiada.
    /// </summary>
    public class StartGuidedSessionRequest
    {
        /// <summary>
        /// ID do usuário (opcional, para sessões autenticadas).
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// Idioma preferido para OCR e mensagens.
        /// </summary>
        /// <example>pt-BR</example>
        public string LanguageCode { get; set; } = "pt";

        /// <summary>
        /// Nome do dispositivo/app para rastreamento.
        /// </summary>
        /// <example>LabelWise iOS 2.1.0</example>
        public string? DeviceInfo { get; set; }
    }

    /// <summary>
    /// Response ao iniciar uma sessão de captura guiada.
    /// </summary>
    public class StartGuidedSessionResponse
    {
        /// <summary>
        /// Identificador único da sessão criada.
        /// </summary>
        public Guid SessionId { get; set; }

        /// <summary>
        /// Status inicial da sessão.
        /// </summary>
        public string Status { get; set; } = "Started";

        /// <summary>
        /// Data/hora de início (UTC).
        /// </summary>
        public DateTimeOffset StartedAt { get; set; }

        /// <summary>
        /// Primeira etapa recomendada.
        /// </summary>
        public NextStepRecommendationDto FirstStep { get; set; } = null!;

        /// <summary>
        /// Lista ordenada de todas as etapas do fluxo.
        /// </summary>
        public List<CaptureStepDefinitionDto> AllSteps { get; set; } = [];

        /// <summary>
        /// Tempo máximo da sessão em minutos (timeout).
        /// </summary>
        public int SessionTimeoutMinutes { get; set; } = 30;

        /// <summary>
        /// Mensagem de boas-vindas/instrução inicial.
        /// </summary>
        public string WelcomeMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Definição de uma etapa de captura.
    /// </summary>
    public class CaptureStepDefinitionDto
    {
        /// <summary>
        /// Tipo de captura.
        /// </summary>
        public CaptureType CaptureType { get; set; }

        /// <summary>
        /// Nome amigável da etapa.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Descrição detalhada.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Ordem sugerida (1-5).
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Indica se é obrigatória.
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Ícone sugerido para o app.
        /// </summary>
        public string IconName { get; set; } = string.Empty;

        /// <summary>
        /// Dicas para uma boa captura.
        /// </summary>
        public List<string> Tips { get; set; } = [];
    }
}
