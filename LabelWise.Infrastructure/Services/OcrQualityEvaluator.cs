using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;

namespace LabelWise.Infrastructure.Services;

/// <summary>
/// Avalia a qualidade de um resultado do Document Intelligence contando quantos
/// macronutrientes-chave foram extraídos e verificando o modo de extração.
///
/// Critérios de qualidade:
///   Alta   → ≥ 3 macros preenchidos E ExtractionMode = "TABLE"
///   Média  → 2 macros preenchidos  OU ExtractionMode = "TABLE" com 1 macro
///   Baixa  → ≤ 1 macro             OU ExtractionMode = "TEXT_ONLY"
///
/// "Macros-chave" = Calorias, Carboidratos, Proteína, Gordura total.
/// </summary>
public sealed class OcrQualityEvaluator : IOcrQualityEvaluator
{
    /// <inheritdoc/>
    public OcrQualityResult Evaluate(DocumentIntelligenceNutritionResult result)
    {
        try
        {
            int macros = CountKeyMacros(result);
            bool isTextOnly = result.ExtractionMode == "TEXT_ONLY";

            if (macros >= 3 && !isTextOnly)
                return new OcrQualityResult(false, "alta",   macros, "≥ 3 macros extraídos via tabela estruturada");

            if (macros == 2 && !isTextOnly)
                return new OcrQualityResult(false, "media",  macros, "2 macros extraídos via tabela estruturada");

            if (macros == 2 && isTextOnly)
                return new OcrQualityResult(true,  "baixa",  macros, "2 macros via texto bruto — sem tabela estruturada");

            if (macros == 1)
                return new OcrQualityResult(true,  "baixa",  macros, "Apenas 1 macro extraído");

            return new OcrQualityResult(true, "baixa", 0, "Nenhum macro-chave extraído");
        }
        catch
        {
            return new OcrQualityResult(true, "baixa", 0, "Erro na avaliação de qualidade");
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Conta quantos dos 4 macros-chave (calorias, carbs, proteína, gordura) foram extraídos.
    /// </summary>
    private static int CountKeyMacros(DocumentIntelligenceNutritionResult r)
    {
        int count = 0;
        if (r.Calories?.Value  > 0) count++;
        if (r.Carbs?.Value     >= 0 && r.Carbs.Value.HasValue)   count++;
        if (r.Protein?.Value   >= 0 && r.Protein.Value.HasValue) count++;
        if (r.Fat?.Value       >= 0 && r.Fat.Value.HasValue)     count++;
        return count;
    }
}
