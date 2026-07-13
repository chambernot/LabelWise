using Azure.AI.FormRecognizer.DocumentAnalysis;
using LabelWise.Application.Models.Nutrition;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.AI.Parsers;

/// <summary>
/// Executa todas as estratégias de parsing em ordem de prioridade e seleciona
/// o resultado com maior score de confiança.
///
/// Ordem de execução:
///   1. <see cref="TableStrictParser"/>  — grid perfeito (maior precisão)
///   2. <see cref="TableFlexibleParser"/> — grid irregular (layout atípico)
///   3. <see cref="TextParser"/>          — fallback por texto bruto
///
/// Critério de seleção: score calculado por <see cref="NutritionParserScoring"/>.
/// Em caso de empate, o parser com maior prioridade (posição na lista) vence.
/// </summary>
internal sealed class NutritionParserOrchestrator
{
    private readonly IReadOnlyList<INutritionParser> _parsers;
    private readonly ILogger _logger;

    public NutritionParserOrchestrator(ILogger logger)
    {
        _logger = logger;
        _parsers =
        [
            new TableStrictParser(logger),
            new TableFlexibleParser(logger),
            new TextParser(logger),
        ];
    }

    /// <summary>
    /// Executa todas as estratégias e devolve o resultado com maior score.
    /// Nunca retorna null — o <see cref="TextParser"/> garante sempre um resultado.
    /// </summary>
    public DocumentIntelligenceNutritionResult ParseBest(AnalyzeResult result)
    {
        DocumentIntelligenceNutritionResult? best      = null;
        int                                  bestScore = -1;
        string                               bestName  = string.Empty;

        foreach (var parser in _parsers)
        {
            DocumentIntelligenceNutritionResult? candidate;
            try
            {
                candidate = parser.Parse(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Orchestrator] Estratégia {Parser} lançou exceção — ignorada.", parser.Name);
                continue;
            }

            if (candidate is null)
            {
                _logger.LogDebug("[Orchestrator] Estratégia {Parser} retornou null — não aplicável.", parser.Name);
                continue;
            }

            int score = parser.GetConfidenceScore(candidate);

            _logger.LogInformation(
                "[Orchestrator] Estratégia={Parser} Score={Score} Campos={Fields} Modo={Mode}",
                parser.Name, score,
                NutritionParserScoring.CountFilledFields(candidate),
                candidate.ExtractionMode);

            if (score > bestScore)
            {
                bestScore = score;
                best      = candidate;
                bestName  = parser.Name;
            }
        }

        // TextParser sempre produz resultado — best nunca será null aqui
        best ??= new DocumentIntelligenceNutritionResult
        {
            ExtractionMode    = "TEXT_ONLY",
            HasNutritionTable = false
        };

        _logger.LogInformation(
            "[Orchestrator] Melhor estratégia selecionada: {Parser} (score={Score}, modo={Mode})",
            bestName, bestScore, best.ExtractionMode);

        return best;
    }
}
