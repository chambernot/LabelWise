using Azure.AI.FormRecognizer.DocumentAnalysis;
using LabelWise.Application.Models.Nutrition;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.AI.Parsers;

/// <summary>
/// Estratégia estrita: delega para <see cref="DocumentIntelligenceNutritionParser.Parse"/>
/// que exige estrutura de tabela limpa (grid de células por linha/coluna).
/// Aplicável apenas quando há ao menos uma tabela com ≥ 5 linhas e ≥ 2 colunas.
/// </summary>
internal sealed class TableStrictParser : INutritionParser
{
    private readonly DocumentIntelligenceNutritionParser _inner;

    public string Name => "TableStrict";

    public TableStrictParser(ILogger logger)
    {
        _inner = new DocumentIntelligenceNutritionParser(logger);
    }

    public DocumentIntelligenceNutritionResult? Parse(AnalyzeResult result)
    {
        if (!result.Tables.Any(t => t.RowCount >= 5 && t.ColumnCount >= 2))
            return null;

        var parsed = _inner.Parse(result);
        parsed.ExtractionMode    = "TABLE";
        parsed.HasNutritionTable = true;
        return parsed;
    }

    public int GetConfidenceScore(DocumentIntelligenceNutritionResult result)
        => NutritionParserScoring.Calculate(result);
}
