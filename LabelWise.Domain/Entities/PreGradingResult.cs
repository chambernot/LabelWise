namespace LabelWise.Domain.Entities;

public class PreGradingResult
{
    public Guid CardEvaluationId { get; private set; }
    public string CardName { get; private set; }
    public decimal CenteringScore { get; private set; }
    public string CenteringDetails { get; private set; }
    public decimal CornersScore { get; private set; }
    public string CornersDetails { get; private set; }
    public decimal EdgesScore { get; private set; }
    public string EdgesDetails { get; private set; }
    public decimal SurfaceScore { get; private set; }
    public string SurfaceDetails { get; private set; }
    public string EstimatedGrade { get; private set; }
    public bool IsWorthGrading { get; private set; }
    public string VerdictMessage { get; private set; }
    public decimal CurrentRawValue { get; private set; }

    public PreGradingResult(
        Guid cardEvaluationId,
        string cardName,
        decimal centeringScore,
        string centeringDetails,
        decimal cornersScore,
        string cornersDetails,
        decimal edgesScore,
        string edgesDetails,
        decimal surfaceScore,
        string surfaceDetails,
        string estimatedGrade,
        bool isWorthGrading,
        string verdictMessage,
        decimal currentRawValue)
    {
        CardEvaluationId = cardEvaluationId;
        CardName = cardName;
        CenteringScore = centeringScore;
        CenteringDetails = centeringDetails;
        CornersScore = cornersScore;
        CornersDetails = cornersDetails;
        EdgesScore = edgesScore;
        EdgesDetails = edgesDetails;
        SurfaceScore = surfaceScore;
        SurfaceDetails = surfaceDetails;
        EstimatedGrade = estimatedGrade;
        IsWorthGrading = isWorthGrading;
        VerdictMessage = verdictMessage;
        CurrentRawValue = currentRawValue;
    }
}