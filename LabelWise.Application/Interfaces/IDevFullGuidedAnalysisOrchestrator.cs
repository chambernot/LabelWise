using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LabelWise.Application.DTOs.Development;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Orquestrador para testes de desenvolvimento do fluxo completo de captura guiada.
    /// </summary>
    public interface IDevFullGuidedAnalysisOrchestrator
    {
        /// <summary>
        /// Processa múltiplas imagens em um fluxo completo simulando captura guiada.
        /// </summary>
        /// <param name="images">Dicionário de imagens (chave: tipo, valor: stream e filename).</param>
        /// <param name="barcode">Código de barras opcional.</param>
        /// <param name="userId">ID do usuário para vincular análise.</param>
        /// <param name="languageCode">Código do idioma.</param>
        /// <param name="deviceInfo">Informações do dispositivo.</param>
        /// <param name="cancellationToken">Token de cancelamento.</param>
        /// <returns>Response completo com análise consolidada e metadados detalhados.</returns>
        Task<FullGuidedAnalysisResponse> ProcessFullGuidedAnalysisAsync(
            Dictionary<string, (Stream stream, string fileName)> images,
            string? barcode,
            int userId,
            string languageCode = "pt-BR",
            string deviceInfo = "DevEndpoint-Test",
            CancellationToken cancellationToken = default);
    }
}
