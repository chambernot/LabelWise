using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Application.Interfaces;

/// <summary>
/// Resultado do parsing OCR determinístico.
/// </summary>
public class OcrParseResult
{
    public NutritionProfile Profile { get; set; } = new();

    /// <summary>
    /// Valores numéricos candidatos a gordura saturada encontrados no texto OCR.
    /// Usados pelo INutritionFixerService para corrigir o valor final.
    /// </summary>
    public List<double> SaturatedFatCandidates { get; set; } = new();

    /// <summary>
    /// Avisos gerados pelo OcrNormalizer durante a mesclagem e conversão de unidades
    /// (ex: conversão de porção → 100 g).
    /// </summary>
    public List<string> NormalizationWarnings { get; set; } = new();
}

/// <summary>
/// Responsável por transformar texto OCR bruto em perfil nutricional estruturado.
/// Regras: prefere coluna 100 g, ignora %VD, retorna dados parciais sem fallback.
/// </summary>
public interface IOcrParserService
{
    OcrParseResult Parse(IReadOnlyList<string> rawLines);
}
