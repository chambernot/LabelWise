namespace LabelWise.Domain.Entities;

public class PreGradingResult
{
    public Guid Id { get; private set; }
    public Guid CardEvaluationId { get; private set; }
    public decimal CenteringScore { get; private set; }
    public decimal CornersScore { get; private set; }
    public decimal EdgesScore { get; private set; }
    public decimal SurfaceScore { get; private set; }
    public string EstimatedGrade { get; private set; }
    public bool IsWorthGrading { get; private set; }
    public string VerdictMessage { get; private set; }

    public PreGradingResult(Guid cardEvaluationId, decimal centering, decimal corners, decimal edges, decimal surface, decimal rawValue)
    {
        Id = Guid.NewGuid();
        CardEvaluationId = cardEvaluationId;
        CenteringScore = centering;
        CornersScore = corners;
        EdgesScore = edges;
        SurfaceScore = surface;

        CalculateGradeAndVerdict(rawValue);
    }

    private void CalculateGradeAndVerdict(decimal rawValue)
    {
        // Regra do mercado de gradação: A nota final costuma ser limitada pela menor sub-nota
        var minScore = new[] { CenteringScore, CornersScore, EdgesScore, SurfaceScore }.Min();

        EstimatedGrade = $"Estimativa: PSA/CGC {minScore}";

        // Lógica Financeira (Veredito)
        decimal gradingCost = 150.0m; // Custo médio de envio + serviço (R$)

        // Simula a valorização: Cartas nota 9+ valem 3x mais. Notas menores valorizam pouco.
        decimal estimatedMultiplier = minScore >= 9.0m ? 3.0m : 1.2m;
        decimal estimatedGradedValue = rawValue * estimatedMultiplier;

        if (estimatedGradedValue > (rawValue + gradingCost))
        {
            IsWorthGrading = true;
            VerdictMessage = "🟢 Compensa enviar! O valor estimado da carta graduada cobre o custo do envio e ainda dá lucro.";
        }
        else
        {
            IsWorthGrading = false;
            VerdictMessage = "🔴 Não compensa. O desgaste limita a nota máxima e o preço dela crua (Raw) vale mais a pena.";
        }
    }
}