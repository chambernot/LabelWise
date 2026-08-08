using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;
using LabelWise.Domain.ValueObjects;

namespace LabelWise.Infrastructure.Services;

public class VisionAnalysisService : IVisionAnalysisService
{
    public async Task<VisionConditionResult> AnalyzeCardConditionAsync(Stream frontImage, Stream backImage)
    {
        // TODO: Aqui entrará a chamada HTTP ou SDK para a sua IA (ou Azure Custom Vision).
        // Por enquanto, simulamos o tempo de processamento e retornamos um resultado "Mockado".
        await Task.Delay(1500);

        // Simulando que a IA encontrou dois defeitos na imagem
        var mockDefects = new List<DefectMap>
        {
            new DefectMap("Whitening", 120.5f, 45.0f, 10.0f, 5.0f),
            new DefectMap("Scratch", 300.0f, 150.2f, 2.0f, 45.0f)
        };

        return new VisionConditionResult(
            IsAuthentic: true,
            Condition: CardCondition.SP, // Retornou Slightly Played por causa dos defeitos
            Defects: mockDefects
        );
    }

    public Task<PreGradingAiResult> AnalyzePreGradingAsync(Stream frontImage, Stream backImage)
    {
        throw new NotImplementedException();
    }
}