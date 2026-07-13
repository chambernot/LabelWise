using System;
using System.IO;
using System.Threading.Tasks;
using LabelWise.Application.DTOs;
using LabelWise.Application.DTOs.Analysis;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Orquestrador do pipeline completo de análise de produto.
    /// Coordena as etapas: Upload → OCR → Parsing → Análise → Resumo
    /// </summary>
    public interface IProductAnalysisPipelineOrchestrator
    {
        /// <summary>
        /// Executa o pipeline completo de análise de produto a partir de uma imagem.
        /// </summary>
        /// <param name="imageStream">Stream da imagem do rótulo</param>
        /// <param name="fileName">Nome do arquivo</param>
        /// <param name="userId">ID do usuário (opcional, para análise personalizada)</param>
        /// <returns>Resultado completo do pipeline incluindo metadados</returns>
        Task<ProductAnalysisPipelineResultDto> ExecutePipelineAsync(
            Stream imageStream, 
            string fileName, 
            Guid? userId = null);

        /// <summary>
        /// Executa o pipeline de análise com suporte a tipo de captura específico.
        /// Permite processamento otimizado baseado no tipo de imagem capturada.
        /// </summary>
        /// <param name="imageStream">Stream da imagem do rótulo</param>
        /// <param name="fileName">Nome do arquivo</param>
        /// <param name="captureType">Tipo de captura (tabela nutricional, ingredientes, etc.)</param>
        /// <param name="request">Parâmetros adicionais da requisição</param>
        /// <param name="userId">ID do usuário (opcional, para análise personalizada)</param>
        /// <returns>Resultado completo do pipeline incluindo metadados</returns>
        Task<ProductAnalysisPipelineResultDto> ExecutePipelineWithCaptureTypeAsync(
            Stream imageStream,
            string fileName,
            CaptureType captureType,
            CapturedImageAnalysisRequest request,
            Guid? userId = null);

        /// <summary>
        /// Processa um produto apenas pelo código de barras, sem necessidade de imagem.
        /// Busca informações em bases de dados externas (Open Food Facts, etc.).
        /// </summary>
        /// <param name="barcode">Código de barras do produto (EAN-8, EAN-13, UPC-A)</param>
        /// <param name="userId">ID do usuário (opcional, para análise personalizada)</param>
        /// <returns>Resultado do pipeline com informações do produto</returns>
        Task<ProductAnalysisPipelineResultDto> ProcessBarcodeAsync(
            string barcode,
            Guid? userId = null);
    }
}
