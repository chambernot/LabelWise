using LabelWise.Application.Interfaces;
using LabelWise.Infrastructure.Services.OcrParsing;

namespace LabelWise.Infrastructure.Services;

/// <summary>
/// Orquestrador do pipeline de parsing OCR em camadas.
///
/// Camadas (ordem de prioridade decrescente):
///   1. LineParser      — detecta rotulo + valor na mesma linha (PT e ES)
///   2. ProximityParser — look-ahead de ate 2 linhas para textos quebrados
///   3. OcrNormalizer   — mescla resultados e converte porcao para 100 g quando necessario
///
/// Regras fundamentais:
///   Nenhuma camada sobrescreve valores ja preenchidos por uma camada anterior.
///   Somente o OcrNormalizer decide sobre conversoes de unidade.
///   O StructuredParser (Document Intelligence / tabelas com bounding box) e executado
///   antes desta classe no NutritionController e tem prioridade maxima absoluta.
/// </summary>
public sealed class OcrParserService : IOcrParserService
{
    private readonly LineParser      _lineParser      = new();
    private readonly ProximityParser _proximityParser = new();
    private readonly OcrNormalizer   _normalizer      = new();

    public OcrParseResult Parse(IReadOnlyList<string> rawLines)
    {
        if (rawLines == null || rawLines.Count == 0)
            return new OcrParseResult();

        var normalizationWarnings = new List<string>();

        // Camada 1: LineParser
        var lineResult = _lineParser.Parse(rawLines);

        // Camada 2: ProximityParser
        var proximityResult = _proximityParser.Parse(rawLines);

        // Camada 3: OcrNormalizer (merge + porcao para 100 g)
        var normalizedProfile = _normalizer.Normalize(
            lineResult, proximityResult, rawLines, normalizationWarnings);

        // Consolidar candidatos para gordura saturada de ambas as camadas
        var satCandidates = lineResult.SaturatedFatCandidates
            .Concat(proximityResult.SaturatedFatCandidates)
            .Distinct()
            .ToList();

        return new OcrParseResult
        {
            Profile                = normalizedProfile,
            SaturatedFatCandidates = satCandidates,
            NormalizationWarnings  = normalizationWarnings
        };
    }
}
