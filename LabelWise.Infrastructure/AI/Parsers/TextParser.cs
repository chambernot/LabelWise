using Azure.AI.FormRecognizer.DocumentAnalysis;
using LabelWise.Application.Models.Nutrition;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.AI.Parsers;

/// <summary>
/// Estratégia de texto bruto: delega para <see cref="DocumentIntelligenceNutritionParser.ParseFromRawText"/>.
/// Sempre aplicável — funciona mesmo quando nenhuma tabela foi detectada.
/// </summary>
internal sealed class TextParser : INutritionParser
{
    private readonly DocumentIntelligenceNutritionParser _inner;

    public string Name => "Text";

    public TextParser(ILogger logger)
    {
        _inner = new DocumentIntelligenceNutritionParser(logger);
    }

    public DocumentIntelligenceNutritionResult? Parse(AnalyzeResult result)
    {
        var parsed = _inner.ParseFromRawText(result.Content);
        parsed.ExtractionMode    = "TEXT_ONLY";
        parsed.HasNutritionTable = false;
        return parsed;
    }

    public int GetConfidenceScore(DocumentIntelligenceNutritionResult result)
        => NutritionParserScoring.Calculate(result);
}
