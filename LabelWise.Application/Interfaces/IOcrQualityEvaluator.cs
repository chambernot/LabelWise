namespace LabelWise.Application.Interfaces;

/// <summary>
/// Resultado da avaliação de qualidade de um resultado do OCR (Document Intelligence).
/// </summary>
/// <param name="IsLowQuality">true quando os dados extraídos são insuficientes para análise confiável.</param>
/// <param name="Confidence">Nível textual de confiança: "alta", "media" ou "baixa".</param>
/// <param name="FilledMacros">Número de macronutrientes-chave preenchidos (calorias, carbs, proteína, gordura).</param>
/// <param name="Reason">Descrição legível do motivo da avaliação.</param>
public sealed record OcrQualityResult(
    bool   IsLowQuality,
    string Confidence,
    int    FilledMacros,
    string Reason);

/// <summary>
/// Avalia a qualidade do resultado retornado pelo Document Intelligence antes de
/// decidir se o pipeline de fallback com IA deve ser ativado.
/// </summary>
public interface IOcrQualityEvaluator
{
    /// <summary>
    /// Analisa o resultado do DI e retorna indicadores de qualidade.
    /// Nunca lança exceção — retorna <see cref="OcrQualityResult"/> com IsLowQuality=true em caso de erro.
    /// </summary>
    OcrQualityResult Evaluate(Application.Models.Nutrition.DocumentIntelligenceNutritionResult result);
}
