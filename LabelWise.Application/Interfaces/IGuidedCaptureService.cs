using LabelWise.Application.DTOs.GuidedCapture;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Serviço para orquestrar o fluxo de captura guiada para apps mobile.
    /// </summary>
    public interface IGuidedCaptureService
    {
        /// <summary>
        /// Inicia uma nova sessão de captura guiada.
        /// </summary>
        /// <param name="request">Dados para iniciar a sessão.</param>
        /// <param name="cancellationToken">Token de cancelamento.</param>
        /// <returns>Response com SessionId e instruções iniciais.</returns>
        Task<StartGuidedSessionResponse> StartSessionAsync(
            StartGuidedSessionRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Obtém o status atual de uma sessão.
        /// </summary>
        /// <param name="sessionId">ID da sessão.</param>
        /// <param name="cancellationToken">Token de cancelamento.</param>
        /// <returns>Estado atual da sessão com progresso.</returns>
        Task<GuidedCaptureSessionDto?> GetSessionStatusAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Adiciona uma captura a uma sessão existente.
        /// </summary>
        /// <param name="sessionId">ID da sessão.</param>
        /// <param name="captureType">Tipo de captura.</param>
        /// <param name="imageStream">Stream da imagem (opcional para barcode).</param>
        /// <param name="fileName">Nome do arquivo de imagem.</param>
        /// <param name="barcode">Código de barras (para CaptureType.Barcode).</param>
        /// <param name="languageCode">Idioma para OCR.</param>
        /// <param name="enableMultiProviderOcr">Habilitar múltiplos providers OCR.</param>
        /// <param name="enableExternalLookup">Buscar em bases externas.</param>
        /// <param name="cancellationToken">Token de cancelamento.</param>
        /// <returns>Resultado da captura com dados extraídos.</returns>
        Task<AddCaptureResponse> AddCaptureAsync(
            Guid sessionId,
            CaptureType captureType,
            Stream? imageStream,
            string? fileName,
            string? barcode,
            string languageCode = "pt",
            bool enableMultiProviderOcr = true,
            bool enableExternalLookup = true,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Remove/refaz uma captura específica.
        /// </summary>
        /// <param name="sessionId">ID da sessão.</param>
        /// <param name="captureId">ID da captura a remover.</param>
        /// <param name="cancellationToken">Token de cancelamento.</param>
        /// <returns>Status atualizado da sessão.</returns>
        Task<GuidedCaptureSessionDto?> RemoveCaptureAsync(
            Guid sessionId,
            Guid captureId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Finaliza a sessão e gera a análise consolidada.
        /// </summary>
        /// <param name="request">Parâmetros para finalização.</param>
        /// <param name="cancellationToken">Token de cancelamento.</param>
        /// <returns>Resultado completo da análise.</returns>
        Task<FinalizeAnalysisResponse> FinalizeAnalysisAsync(
            FinalizeAnalysisRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancela uma sessão em andamento.
        /// </summary>
        /// <param name="sessionId">ID da sessão.</param>
        /// <param name="cancellationToken">Token de cancelamento.</param>
        /// <returns>True se cancelada com sucesso.</returns>
        Task<bool> CancelSessionAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Obtém as definições de todas as etapas de captura.
        /// </summary>
        /// <param name="languageCode">Idioma para as descrições.</param>
        /// <returns>Lista de etapas com descrições e dicas.</returns>
        List<CaptureStepDefinitionDto> GetCaptureStepDefinitions(string languageCode = "pt");

        /// <summary>
        /// Obtém a próxima etapa recomendada para uma sessão.
        /// </summary>
        /// <param name="sessionId">ID da sessão.</param>
        /// <param name="cancellationToken">Token de cancelamento.</param>
        /// <returns>Recomendação da próxima etapa.</returns>
        Task<NextStepRecommendationDto?> GetNextStepRecommendationAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default);
    }
}
