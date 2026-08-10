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

    public async Task<PreGradingResult> SimulateGradingAsync(
        Guid evaluationId,
        decimal currentRawValue,
        Stream frontStraight,
        Stream frontAngled,
        Stream backStraight,
        Stream backAngled)
    {
        var aiScores = await _visionService.AnalyzePreGradingAsync(
            frontStraight, frontAngled, backStraight, backAngled);

        // Repassando os novos campos recebidos da IA
        var result = new PreGradingResult(
            evaluationId,
            aiScores.CardName,
            aiScores.CenteringScore,
            aiScores.CenteringDetails,
            aiScores.CornersScore,
            aiScores.CornersDetails,
            aiScores.EdgesScore,
            aiScores.EdgesDetails,
            aiScores.SurfaceScore,
            aiScores.SurfaceDetails,
            aiScores.EstimatedGrade,
            aiScores.IsWorthGrading,
            aiScores.VerdictMessage,
            currentRawValue
        );

        return result;
    }
}