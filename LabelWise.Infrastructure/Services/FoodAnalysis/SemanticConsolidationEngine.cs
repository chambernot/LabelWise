using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;

namespace LabelWise.Infrastructure.Services.FoodAnalysis;

/// <summary>
/// Consolida linguagem e sinais finais a partir de uma única decisão central.
/// </summary>
public sealed class SemanticConsolidationEngine
{
    private readonly QuickInsightSafetyFilter _quickInsightSafetyFilter;

    public SemanticConsolidationEngine(QuickInsightSafetyFilter quickInsightSafetyFilter)
    {
        _quickInsightSafetyFilter = quickInsightSafetyFilter;
    }

    public IReadOnlyList<SummaryCard> BuildSummaryCards(FoodDecisionDraft draft)
    {
        var cards = new List<SummaryCard>();

        cards.Add(new SummaryCard
        {
            Title = "Classificação alimentar",
            Subtitle = draft.FoodClassification,
            Severity = draft.HasCriticalAlert ? "critical" : draft.Quality == AnalysisQuality.Reliable ? "info" : "warning",
            Color = draft.HasCriticalAlert ? "danger" : "neutral",
            Icon = draft.HasCriticalAlert ? "alert" : "info",
            ActionableMessage = draft.HasCriticalAlert ? "Revise os alertas antes de consumir." : "Use a análise como apoio à decisão.",
            PresentationHint = new PresentationHint
            {
                Severity = draft.HasCriticalAlert ? "critical" : "info",
                DisplayMode = draft.HasCriticalAlert ? "warning" : "summary",
                Highlight = draft.HasCriticalAlert,
                Priority = 1,
                UiStyle = draft.HasCriticalAlert ? "danger" : "neutral"
            }
        });

        cards.Add(new SummaryCard
        {
            Title = "Processamento",
            Subtitle = ToProcessingText(draft.ProcessingLevel),
            Severity = draft.ProcessingLevel == ProcessingLevel.UltraProcessed ? "warning" : "info",
            Color = draft.ProcessingLevel == ProcessingLevel.UltraProcessed ? "warning" : "neutral",
            Icon = "processing",
            ActionableMessage = draft.ProcessingLevel == ProcessingLevel.Unknown
                ? "Classificação de processamento não confirmada."
                : "Considere o nível de processamento na frequência de consumo.",
            PresentationHint = new PresentationHint
            {
                Severity = draft.ProcessingLevel == ProcessingLevel.UltraProcessed ? "warning" : "info",
                DisplayMode = "summary",
                Highlight = draft.ProcessingLevel == ProcessingLevel.UltraProcessed,
                Priority = 2,
                UiStyle = draft.ProcessingLevel == ProcessingLevel.UltraProcessed ? "warning" : "neutral"
            }
        });

        return cards;
    }

    public IReadOnlyList<QuickInsight> BuildQuickInsights(FoodDecisionDraft draft)
    {
        var insights = new List<QuickInsight>();

        foreach (var alert in draft.Alerts.Take(3))
        {
            insights.Add(new QuickInsight
            {
                Text = alert,
                Type = "alert",
                Severity = "critical"
            });
        }

        if (draft.Quality != AnalysisQuality.Reliable)
        {
            insights.Add(new QuickInsight
            {
                Text = "Resultado preliminar: algumas informações não foram confirmadas com alta confiança.",
                Type = "quality",
                Severity = "warning"
            });
        }

        if (draft.ProcessingLevel == ProcessingLevel.UltraProcessed && draft.SafeToConclude)
        {
            insights.Add(new QuickInsight
            {
                Text = "Há sinais consistentes de ultraprocessamento.",
                Type = "processing",
                Severity = "warning"
            });
        }

        return _quickInsightSafetyFilter.Apply(draft, insights).Take(5).ToList();
    }

    public IReadOnlyList<Recommendation> BuildRecommendations(FoodDecisionDraft draft)
    {
        if (draft.Quality is AnalysisQuality.Insufficient or AnalysisQuality.Inconsistent)
        {
            return
            [
                new Recommendation
                {
                    Text = "Envie uma foto mais nítida do rótulo para confirmar a análise.",
                    Type = "retry",
                    Priority = "high"
                }
            ];
        }

        if (draft.HasCriticalAlert)
        {
            return
            [
                new Recommendation
                {
                    Text = "Não trate o produto como compatível sem revisar o alerta principal.",
                    Type = "safety",
                    Priority = "high"
                }
            ];
        }

        return
        [
            new Recommendation
            {
                Text = "Compare com produtos menos processados quando possível.",
                Type = "general",
                Priority = "medium"
            }
        ];
    }

    public string BuildAssistantSummary(FoodDecisionDraft draft)
    {
        if (draft.Quality is AnalysisQuality.Insufficient or AnalysisQuality.Inconsistent)
            return "Resultado preliminar: não foi possível confirmar todas as informações do rótulo com segurança.";

        if (draft.HasCriticalAlert)
            return $"Atenção: {draft.Alerts.First()}";

        return draft.ProcessingLevel == ProcessingLevel.UltraProcessed
            ? "Foram detectados sinais de ultraprocessamento. Considere consumir com moderação."
            : "Análise consolidada sem alerta crítico detectado.";
    }

    private static string ToProcessingText(ProcessingLevel level) => level switch
    {
        ProcessingLevel.MinimallyProcessed => "natural ou minimamente processado",
        ProcessingLevel.ProcessedCulinaryIngredients => "ingrediente culinário processado",
        ProcessingLevel.Processed => "processado",
        ProcessingLevel.UltraProcessed => "ultraprocessado",
        _ => "não confirmado"
    };
}

public sealed record FoodDecisionDraft(
    string FoodClassification,
    ProcessingLevel ProcessingLevel,
    AnalysisQuality Quality,
    IReadOnlyList<string> Alerts,
    IReadOnlyList<string> Warnings,
    bool SafeToConclude,
    int AnalysisTrustScore,
    bool SafeModeRequired)
{
    public bool HasCriticalAlert => Alerts.Count > 0;
}

public sealed class QuickInsightSafetyFilter
{
    private static readonly string[] UnsafeTerms =
    [
        "pouco processado", "saudável", "saudavel", "excelente", "ruim", "evitar", "recomendado", "produto natural", "natural"
    ];

    public IReadOnlyList<QuickInsight> Apply(FoodDecisionDraft draft, IReadOnlyList<QuickInsight> insights)
    {
        if (draft.SafeToConclude && draft.AnalysisTrustScore >= 60 && !draft.SafeModeRequired)
            return insights.Where(IsSafeText).ToList();

        var safeInsights = insights
            .Where(insight => insight.Severity == "critical" || insight.Type == "alert")
            .Where(IsSafeText)
            .ToList();

        safeInsights.Add(new QuickInsight
        {
            Text = ResolveLimitedAnalysisText(draft),
            Type = "limited_analysis",
            Severity = "warning"
        });

        return safeInsights;
    }

    private static bool IsSafeText(QuickInsight insight) => !UnsafeTerms.Any(term =>
        insight.Text.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string ResolveLimitedAnalysisText(FoodDecisionDraft draft)
    {
        if (draft.Quality == AnalysisQuality.Partial)
            return "Leitura parcial: resultado preliminar com informações insuficientes para conclusões fortes.";

        if (draft.AnalysisTrustScore < 60)
            return "Análise limitada: confiança insuficiente para recomendações definitivas.";

        return "Resultado preliminar: não foi possível confirmar todos os dados com segurança.";
    }
}
