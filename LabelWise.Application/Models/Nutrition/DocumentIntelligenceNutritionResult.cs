namespace LabelWise.Application.Models.Nutrition;

/// <summary>
/// Valor nutricional extraído de uma fonte específica.
/// </summary>
public sealed class NutritionField
{
    public double? Value  { get; set; }

    /// <summary>
    /// Fonte do valor: "DocumentIntelligence", "OpenFoodFacts" ou "Fallback".
    /// </summary>
    public string Source { get; set; } = "DocumentIntelligence";
}

/// <summary>
/// Resultado estruturado do parser de tabela nutricional via Document Intelligence.
/// </summary>
public sealed class DocumentIntelligenceNutritionResult
{
    public NutritionField? Calories      { get; set; }
    public NutritionField? Carbs         { get; set; }
    public NutritionField? Sugar         { get; set; }
    public NutritionField? AddedSugar    { get; set; }
    public NutritionField? Protein       { get; set; }
    public NutritionField? Fat           { get; set; }
    public NutritionField? SaturatedFat  { get; set; }
    public NutritionField? Sodium        { get; set; }
    public NutritionField? Fiber         { get; set; }

    /// <summary>
    /// Avisos produzidos durante a extração (ex: inconsistência calórica).
    /// </summary>
    public List<string> Warnings { get; set; } = [];

    /// <summary>
    /// Indica que as calorias declaradas divergem mais de 50 kcal do calculado pelos macros.
    /// </summary>
    public bool HasCaloriesInconsistency { get; set; }

    /// <summary>
    /// Indica se uma tabela nutricional estruturada (grid com ≥ 5 linhas e ≥ 2 colunas)
    /// foi detectada na imagem. False significa que os dados vêm de extração por texto bruto.
    /// </summary>
    public bool HasNutritionTable { get; set; }

    /// <summary>
    /// Modo de extração usado: "TABLE" (grid estruturado) ou "TEXT_ONLY" (texto bruto).
    /// </summary>
    public string ExtractionMode { get; set; } = "TABLE";

    /// <summary>
    /// Retorna verdadeiro se ao menos 3 macronutrientes foram extraídos.
    /// </summary>
    public bool HasMinimumData()
    {
        int count = 0;
        if (Calories?.Value     > 0) count++;
        if (Carbs?.Value        >= 0 && Carbs.Value.HasValue)      count++;
        if (Protein?.Value      >= 0 && Protein.Value.HasValue)    count++;
        if (Fat?.Value          >= 0 && Fat.Value.HasValue)        count++;
        if (Sugar?.Value        >= 0 && Sugar.Value.HasValue)      count++;
        if (Sodium?.Value       >= 0 && Sodium.Value.HasValue)     count++;
        return count >= 2;
    }

    /// <summary>
    /// Retorna verdadeiro se ao menos um campo nutricional foi extraído.
    /// Usado para decidir entre caminho parcial e resposta completamente vazia.
    /// </summary>
    public bool HasAnyData() =>
        Calories?.Value.HasValue    == true ||
        Carbs?.Value.HasValue       == true ||
        Protein?.Value.HasValue     == true ||
        Fat?.Value.HasValue         == true ||
        Sugar?.Value.HasValue       == true ||
        SaturatedFat?.Value.HasValue == true ||
        Sodium?.Value.HasValue      == true ||
        Fiber?.Value.HasValue       == true;
}
