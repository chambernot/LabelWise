using System;
using System.IO;
using System.Threading.Tasks;
using LabelWise.Application.DTOs;
using LabelWise.Application.Interfaces;

namespace LabelWise.Infrastructure.Services
{
    /// <summary>
    /// Implementação do serviço de análise de produtos usando o pipeline orquestrado.
    /// Delega o processamento completo para o orquestrador do pipeline.
    /// </summary>
    public class ProductAnalysisServiceImpl : IProductAnalysisService
    {
        private readonly IProductAnalysisPipelineOrchestrator _pipelineOrchestrator;

        public ProductAnalysisServiceImpl(IProductAnalysisPipelineOrchestrator pipelineOrchestrator)
        {
            _pipelineOrchestrator = pipelineOrchestrator;
        }

        public async Task<ProductAnalysisResultDto> AnalyzeImageAsync(Stream stream, string fileName, Guid? userId = null)
        {
            // Delega para o orquestrador do pipeline que gerencia todo o fluxo:
            // Upload → OCR → Parser → Motor de Regras → Resumo
            var pipelineResult = await _pipelineOrchestrator.ExecutePipelineAsync(stream, fileName, userId);

            // Retorna apenas o resultado da análise (sem metadados do pipeline)
            return pipelineResult.AnalysisResult;
        }
    }
}
