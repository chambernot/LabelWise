using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;

namespace LabelWise.Application.Services;

public class PreGradingService
{
    private readonly IVisionAnalysisService _visionService;

    public PreGradingService(IVisionAnalysisService visionService)
    {
        _visionService = visionService;
    }

    public async Task<PreGradingResult> SimulateGradingAsync(Guid evaluationId, decimal currentRawValue, Stream frontImage, Stream backImage)
    {
        // 1. Chama a IA real (OpenAI Vision) para analisar a carta e extrair as sub-notas
        var aiScores = await _visionService.AnalyzePreGradingAsync(frontImage, backImage);

        // 2. Gera a entidade de domínio que calcula automaticamente o veredito financeiro (Compensa ou não)
        var result = new PreGradingResult(
            evaluationId,
            aiScores.Centering,
            aiScores.Corners,
            aiScores.Edges,
            aiScores.Surface,
            currentRawValue
        );

        // 3. Aqui você salvaria no banco de dados
        // await _preGradingRepository.SaveAsync(result);

        return result;
    }
}