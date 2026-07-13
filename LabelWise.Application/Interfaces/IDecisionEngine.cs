using LabelWise.Domain.Enums;
using LabelWise.Domain.Models;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// Engine central de decisão unificada.
/// TODAS as decisões finais devem passar por aqui.
/// Garante consistência entre scores, cards, recomendações e insights.
/// </summary>
public interface IDecisionEngine
{
    /// <summary>
    /// Toma decisão final sobre compatibilidade alimentar.
    /// </summary>
    /// <param name="input">Entrada de decisão com todas as evidências</param>
    /// <returns>Decisão final unificada</returns>
    Task<FoodDecision> MakeDecisionAsync(DecisionInput input);

    /// <summary>
    /// Valida se a decisão pode ser tomada com os dados disponíveis.
    /// </summary>
    /// <param name="input">Entrada de decisão</param>
    /// <returns>True se há dados suficientes</returns>
    bool CanMakeDecision(DecisionInput input);

    /// <summary>
    /// Calcula nível de confiança da decisão baseado em qualidade de evidências.
    /// </summary>
    /// <param name="evidences">Lista de evidências</param>
    /// <returns>Nível de confiança (0.0 a 1.0)</returns>
    double CalculateDecisionConfidence(IReadOnlyList<Evidence> evidences);
}

/// <summary>
/// Entrada para o engine de decisão.
/// </summary>
public sealed class DecisionInput
{
    /// <summary>
    /// Claims regulatórios detectados (prioridade máxima)
    /// </summary>
    public required IReadOnlyList<RegulatoryClaim> RegulatoryInformation { get; init; }

    /// <summary>
    /// Ingredientes explicitamente detectados
    /// </summary>
    public required IReadOnlyList<Evidence> ExplicitIngredients { get; init; }

    /// <summary>
    /// Inferências semânticas
    /// </summary>
    public required IReadOnlyList<Evidence> SemanticInferences { get; init; }

    /// <summary>
    /// Conflitos detectados
    /// </summary>
    public required IReadOnlyList<AnalysisConflict> Conflicts { get; init; }

    /// <summary>
    /// Qualidade geral da análise
    /// </summary>
    public required AnalysisQuality AnalysisQuality { get; init; }

    /// <summary>
    /// Dados nutricionais (se disponíveis)
    /// </summary>
    public Dictionary<string, double> NutritionalData { get; init; } = new();

    /// <summary>
    /// Categoria do produto (se conhecida)
    /// </summary>
    public string? ProductCategory { get; init; }

    /// <summary>
    /// Perfis alimentares solicitados
    /// </summary>
    public IReadOnlyList<string> RequestedProfiles { get; init; } = [];
}

/// <summary>
/// Decisão final unificada.
/// </summary>
public sealed class FoodDecision
{
    /// <summary>
    /// Classificação alimentar consolidada para consumidores de API.
    /// </summary>
    public string FoodClassification { get; init; } = "unknown";

    /// <summary>
    /// Alertas consolidados pela engine central.
    /// </summary>
    public IReadOnlyList<string> Alerts { get; init; } = [];

    /// <summary>
    /// Avisos consolidados pela engine central.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Score de processamento de 0 a 100, onde maior indica menor industrialização.
    /// </summary>
    public int ProcessingScore { get; init; }

    /// <summary>
    /// Scores específicos por perfil alimentar.
    /// </summary>
    public IReadOnlyDictionary<string, int> ProfileScores { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// Hints de apresentação para impedir que o frontend invente semântica.
    /// </summary>
    public IReadOnlyList<PresentationHint> PresentationHints { get; init; } = [];

    /// <summary>
    /// Compatibilidade por perfil alimentar
    /// </summary>
    public required Dictionary<string, ProfileCompatibility> ProfileCompatibilities { get; init; }

    /// <summary>
    /// Score nutricional geral
    /// </summary>
    public required int NutritionalScore { get; init; }

    /// <summary>
    /// Nível de processamento
    /// </summary>
    public required ProcessingLevel ProcessingLevel { get; init; }

    /// <summary>
    /// Qualidade da análise
    /// </summary>
    public required AnalysisQuality Quality { get; init; }

    /// <summary>
    /// Confiança geral da decisão (0.0 a 1.0)
    /// </summary>
    public required double OverallConfidence { get; init; }

    /// <summary>
    /// Cards de resumo para UI
    /// </summary>
    public required IReadOnlyList<SummaryCard> SummaryCards { get; init; }

    /// <summary>
    /// Insights rápidos
    /// </summary>
    public required IReadOnlyList<QuickInsight> QuickInsights { get; init; }

    /// <summary>
    /// Recomendações acionáveis
    /// </summary>
    public required IReadOnlyList<Recommendation> Recommendations { get; init; }

    /// <summary>
    /// Resumo executivo para o assistente
    /// </summary>
    public required string AssistantSummary { get; init; }

    /// <summary>
    /// Conflitos críticos que requerem atenção
    /// </summary>
    public required IReadOnlyList<AnalysisConflict> CriticalConflicts { get; init; }

    /// <summary>
    /// Trail de evidências para auditoria
    /// </summary>
    public required IReadOnlyList<Evidence> EvidenceTrail { get; init; }
}

/// <summary>
/// Compatibilidade para um perfil alimentar específico.
/// </summary>
public sealed class ProfileCompatibility
{
    public required string ProfileName { get; init; }
    public required FoodCompatibilityStatus Status { get; init; }
    public required double Confidence { get; init; }
    public required IReadOnlyList<string> Reasons { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
    public required IReadOnlyList<Evidence> SupportingEvidence { get; init; }
}

/// <summary>
/// Card de resumo para apresentação.
/// </summary>
public sealed class SummaryCard
{
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string Severity { get; init; }
    public required string Color { get; init; }
    public required string Icon { get; init; }
    public required string ActionableMessage { get; init; }
    public PresentationHint PresentationHint { get; init; } = new();
}

/// <summary>
/// Modelo semântico de apresentação para UI.
/// </summary>
public sealed class PresentationHint
{
    public string Severity { get; init; } = "info";
    public string DisplayMode { get; init; } = "default";
    public bool Highlight { get; init; }
    public int Priority { get; init; } = 5;
    public string UiStyle { get; init; } = "neutral";
}

/// <summary>
/// Insight rápido.
/// </summary>
public sealed class QuickInsight
{
    public required string Text { get; init; }
    public required string Type { get; init; }
    public required string Severity { get; init; }
}

/// <summary>
/// Recomendação acionável.
/// </summary>
public sealed class Recommendation
{
    public required string Text { get; init; }
    public required string Type { get; init; }
    public required string Priority { get; init; }
}
