using LabelWise.Application.DTOs.Orchestration;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Orquestrador do pipeline completo de análise de produtos.
    /// 
    /// RESPONSABILIDADES:
    /// - Coordenar execução sequencial das etapas:
    ///   1. Identificação do produto (IProductIdentificationService)
    ///   2. Leitura do rótulo (ILabelReadingService)
    ///   3. Análise nutricional (IProductAnalysisEngine)
    /// - Gerenciar estratégia de fallback entre etapas
    /// - Aplicar quality gates
    /// - Consolidar resultados
    /// - Salvar histórico de análise
    /// - Fornecer resumo executivo com IA (opcional)
    /// 
    /// FLUXO TÍPICO:
    /// 1. IDENTIFICAÇÃO:
    ///    - Recebe imagem(ns) do produto
    ///    - Identifica produto (barcode/OCR/visual)
    ///    - Se confiança < threshold, retorna warning
    /// 
    /// 2. LEITURA:
    ///    - Recebe capturas do rótulo
    ///    - Extrai informações (OCR + parsing)
    ///    - Valida qualidade da extração
    /// 
    /// 3. ANÁLISE:
    ///    - Executa análise nutricional
    ///    - Aplica regras de scoring
    ///    - Gera recomendações personalizadas
    /// 
    /// 4. QUALITY GATE:
    ///    - Avalia qualidade geral do processo
    ///    - Identifica problemas e sugere melhorias
    /// 
    /// 5. CONSOLIDAÇÃO:
    ///    - Consolida resultados de todas as etapas
    ///    - Calcula confiança geral
    ///    - Salva histórico
    ///    - Retorna resultado completo
    /// </summary>
    public interface IProductAnalysisOrchestrator
    {
        /// <summary>
        /// Executa o pipeline completo de análise de produto.
        /// Coordena todas as etapas: identificação → leitura → análise → quality gate.
        /// </summary>
        /// <param name="request">Request com dados de entrada e configurações</param>
        /// <returns>Resultado consolidado de todas as etapas</returns>
        Task<ProductAnalysisOrchestrationResult> ExecuteFullPipelineAsync(ProductAnalysisOrchestrationRequest request);

        /// <summary>
        /// Executa apenas a etapa de identificação do produto.
        /// Útil para fluxos onde o usuário quer confirmar o produto antes de prosseguir.
        /// </summary>
        /// <param name="request">Request de identificação</param>
        /// <returns>Resultado da identificação</returns>
        Task<ProductAnalysisOrchestrationResult> ExecuteIdentificationOnlyAsync(
            DTOs.ProductIdentification.ProductIdentificationRequest request);

        /// <summary>
        /// Executa as etapas de leitura e análise para um produto já identificado.
        /// Útil quando o produto já foi identificado em uma etapa anterior.
        /// </summary>
        /// <param name="request">Request de leitura e análise</param>
        /// <param name="identificationResult">Resultado da identificação já realizada</param>
        /// <returns>Resultado consolidado de leitura + análise</returns>
        Task<ProductAnalysisOrchestrationResult> ExecuteLabelReadingAndAnalysisAsync(
            DTOs.LabelReading.LabelReadingRequest request,
            DTOs.ProductIdentification.ProductIdentificationResult identificationResult);

        /// <summary>
        /// Reexecuta a análise nutricional usando dados já extraídos.
        /// Útil quando o usuário atualiza suas preferências e quer nova análise.
        /// </summary>
        /// <param name="analysisHistoryId">ID do histórico de análise anterior</param>
        /// <param name="userPreferences">Novas preferências do usuário</param>
        /// <returns>Resultado da nova análise</returns>
        Task<ProductAnalysisOrchestrationResult> ReanalyzeWithNewPreferencesAsync(
            int analysisHistoryId,
            UserAnalysisPreferences userPreferences);

        /// <summary>
        /// Obtém o status atual do orquestrador e suas dependências.
        /// </summary>
        /// <returns>
        /// Dictionary com status:
        /// - "IdentificationService": "Available" | "Unavailable"
        /// - "LabelReadingService": "Available" | "Unavailable"
        /// - "AnalysisEngine": "Available" | "Unavailable"
        /// - "QualityGate": "Available" | "Unavailable"
        /// - "HistoryService": "Available" | "Unavailable"
        /// </returns>
        Task<Dictionary<string, string>> GetOrchestratorStatusAsync();

        /// <summary>
        /// Obtém estatísticas gerais do pipeline.
        /// </summary>
        /// <returns>
        /// Dictionary com estatísticas:
        /// - "TotalPipelinesExecuted": número total
        /// - "SuccessRate": taxa de sucesso (0.0 a 1.0)
        /// - "AverageProcessingTime": tempo médio em segundos
        /// - "AverageConfidence": confiança média
        /// - "StageSuccessRates": taxa de sucesso por etapa (JSON)
        /// </returns>
        Task<Dictionary<string, string>> GetPipelineStatisticsAsync();

        /// <summary>
        /// Valida a qualidade de um resultado de pipeline.
        /// Aplica quality gates e retorna avaliação detalhada.
        /// </summary>
        /// <param name="result">Resultado do pipeline a ser validado</param>
        /// <returns>Avaliação de qualidade com score e recomendações</returns>
        Task<DTOs.Orchestration.QualityGateResult> ValidatePipelineQualityAsync(
            ProductAnalysisOrchestrationResult result);
    }
}
