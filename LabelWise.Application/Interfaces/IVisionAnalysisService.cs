using LabelWise.Domain.Enums;
using LabelWise.Domain.ValueObjects;

namespace LabelWise.Application.Interfaces;

public record VisionConditionResult(bool IsAuthentic, CardCondition Condition, List<DefectMap> Defects);
public record PreGradingAiResult(decimal Centering, decimal Corners, decimal Edges, decimal Surface);

public interface IVisionAnalysisService
{
    Task<VisionConditionResult> AnalyzeCardConditionAsync(Stream frontImage, Stream backImage);
    Task<PreGradingAiResult> AnalyzePreGradingAsync(Stream frontImage, Stream backImage);
}