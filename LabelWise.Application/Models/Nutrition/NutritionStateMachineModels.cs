namespace LabelWise.Application.Models.Nutrition;

/// <summary>
/// Estados determinísticos da análise nutricional.
/// É a única fonte de verdade para flags, qualidade de dados,
/// comportamento do score e dos perfis de usuário.
/// </summary>
public enum NutritionAnalysisState
{
    /// <summary>Nenhuma informação útil extraída.</summary>
    NoData,

    /// <summary>Dados incompletos (ex.: apenas calorias na frente da embalagem).</summary>
    PartialData,

    /// <summary>Tabela detectada porém sem dados mínimos para score confiável.</summary>
    StructuredData,

    /// <summary>Dados mínimos presentes e confiança suficiente para score.</summary>
    CompleteData,

    /// <summary>Dados mínimos presentes mas com baixa confiança.</summary>
    LowConfidence,

    /// <summary>Erro ou inconsistência grave detectada.</summary>
    Invalid
}

/// <summary>
/// Sinais de entrada usados pela <see cref="NutritionAnalysisState"/>
/// para determinar o estado final da análise.
/// </summary>
public sealed class NutritionContext
{
    public bool HasNutritionTable { get; set; }
    public bool HasMinimumData { get; set; }
    public bool HasAnyNutritionData { get; set; }
    public bool HasCaloriesOnly { get; set; }
    public double Confidence { get; set; }
}
