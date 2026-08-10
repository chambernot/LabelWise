using LabelWise.Domain.Enums;
using LabelWise.Domain.ValueObjects;

namespace LabelWise.Application.Interfaces;

public record VisionConditionResult(bool IsAuthentic, CardCondition Condition, List<DefectMap> Defects);

// ESTRUTURA EXPANDIDA COM OS TEXTOS DETALHADOS
public record PreGradingAiResult(
    string CardName,
    decimal CenteringScore,
    string CenteringDetails,
    decimal CornersScore,
    string CornersDetails,
    decimal EdgesScore,
    string EdgesDetails,
    decimal SurfaceScore,
    string SurfaceDetails,
    string EstimatedGrade,
    bool IsWorthGrading,
    string VerdictMessage);

public interface IVisionAnalysisService
{
    Task<VisionConditionResult> AnalyzeCardConditionAsync(Stream frontImage, Stream backImage);
    Task<PreGradingAiResult> AnalyzePreGradingAsync(
            Stream frontStraight,
            Stream frontAngled,
            Stream backStraight,
            Stream backAngled);
}