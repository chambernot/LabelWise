namespace LabelWise.Application.Interfaces;

/// <summary>
/// Corrige valores nutricionais detectando inconsistências matemáticas sem sobrescrever
/// dados válidos com estimativas. Toda correção gera um warning explícito.
/// </summary>
public interface INutritionFixerService
{
    /// <summary>
    /// Corrige gordura saturada quando o valor está ausente ou inconsistente em relação à gordura total.
    /// </summary>
    double? FixSaturatedFat(
        double? fat,
        double? saturatedFat,
        IReadOnlyList<double> candidates,
        List<string> warnings);

    /// <summary>
    /// Corrige calorias calculando a partir dos macronutrientes quando há divergência > 100 kcal.
    /// Retorna 0 quando macros insuficientes.
    /// </summary>
    double FixCalories(
        double? fat,
        double? carbs,
        double? protein,
        double? calories,
        List<string> warnings);
}
