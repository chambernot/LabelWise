namespace LabelWise.Domain.Parsing;

/// <summary>
/// Evidência de extração por linha — usada para debug e auditoria de mapeamento.
/// </summary>
/// <param name="MatchedRowText">Texto completo da linha que gerou o match.</param>
/// <param name="ExtractedValue">Valor numérico selecionado para este nutriente.</param>
/// <param name="CandidateValues">Todos os valores numéricos encontrados na linha.</param>
public sealed record NutrientRowMatch(
    string MatchedRowText,
    double ExtractedValue,
    IReadOnlyList<double> CandidateValues);

/// <summary>
/// Modelo de dados nutricionais extraídos da tabela nutricional.
/// Todos os valores são por 100g ou 100ml.
/// </summary>
public sealed class NutritionData
{
    public double? CaloriesPer100g { get; set; }
    public double? CarbsPer100g { get; set; }
    public double? SugarPer100g { get; set; }
    public double? AddedSugarPer100g { get; set; }
    public double? ProteinPer100g { get; set; }
    public double? FatPer100g { get; set; }
    public double? SaturatedFatPer100g { get; set; }
    public double? TransFatPer100g { get; set; }
    public double? FiberPer100g { get; set; }
    public double? SodiumPer100g { get; set; }

    /// <summary>
    /// Unidade base da tabela nutricional (g ou ml)
    /// </summary>
    public string Unit { get; set; } = "g";

    /// <summary>
    /// Score de confiança da extração (0-100)
    /// Baseado na quantidade e consistência dos dados extraídos
    /// </summary>
    public int ConfidenceScore { get; set; }

    /// <summary>
    /// Estratégia de parsing que teve sucesso
    /// </summary>
    public string? ParsingStrategy { get; set; }

    /// <summary>
    /// Nutrientes críticos extraídos (usado para calcular confiança)
    /// </summary>
    public int ExtractedNutrientsCount { get; set; }

    /// <summary>
    /// Warnings detectados durante a extração
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Evidência de mapeamento linha→nutriente para cada campo extraído.
    /// Chaves: "calories", "fat", "saturatedFat", "protein", "carbs", "sugar", "fiber", "sodium", etc.
    /// Útil para debug e auditoria de erros de coluna.
    /// </summary>
    public Dictionary<string, NutrientRowMatch> RowMatches { get; set; } = new();

    /// <summary>
    /// Indica se há dados mínimos para análise
    /// (pelo menos: calorias + 2 macronutrientes)
    /// </summary>
    public bool HasMinimumData =>
        CaloriesPer100g.HasValue &&
        CountValidMacros() >= 2;

    /// <summary>
    /// Indica se extração foi completa (5+ nutrientes)
    /// </summary>
    public bool IsComplete => ExtractedNutrientsCount >= 5;

    private int CountValidMacros()
    {
        int count = 0;
        if (CarbsPer100g.HasValue) count++;
        if (ProteinPer100g.HasValue) count++;
        if (FatPer100g.HasValue) count++;
        return count;
    }

    /// <summary>
    /// Retorna um resumo textual dos dados extraídos
    /// </summary>
    public string GetSummary()
    {
        var parts = new List<string>();

        if (CaloriesPer100g.HasValue)
            parts.Add($"Cal: {CaloriesPer100g:F0} kcal");

        if (ProteinPer100g.HasValue)
            parts.Add($"Prot: {ProteinPer100g:F1}g");

        if (CarbsPer100g.HasValue)
            parts.Add($"Carbs: {CarbsPer100g:F1}g");

        if (FatPer100g.HasValue)
            parts.Add($"Gord: {FatPer100g:F1}g");

        if (SodiumPer100g.HasValue)
            parts.Add($"Na: {SodiumPer100g:F0}mg");

        return parts.Count > 0 
            ? string.Join(", ", parts) 
            : "Nenhum dado extraído";
    }
}
