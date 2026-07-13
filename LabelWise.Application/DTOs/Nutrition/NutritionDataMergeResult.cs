using System.Collections.Generic;

namespace LabelWise.Application.DTOs.Nutrition;

/// <summary>
/// Resultado do merge de dados nutricionais.
/// </summary>
public class NutritionDataMergeResult
{
    public EstimatedNutritionProfileDto FinalProfile { get; set; } = new();
    public DataSourceType DataSourceType { get; set; }
    public int RealFieldsCount { get; set; }
    public int EstimatedFieldsCount { get; set; }
    public bool FallbackApplied { get; set; }
    public string? CategoryCode { get; set; }
    public string? CategoryName { get; set; }
    public double FallbackConfidence { get; set; }
    public Dictionary<string, string> FieldSources { get; set; } = new();
    public List<string> Inconsistencies { get; set; } = [];
    public bool ProfileRejected { get; set; }
}

/// <summary>
/// Tipo de fonte dos dados nutricionais.
/// </summary>
public enum DataSourceType
{
    Unknown,
    Real,
    Mixed,
    EstimatedByCategory
}

/// <summary>
/// Qualidade dos dados nutricionais.
/// </summary>
public enum DataQuality
{
    Missing,
    Partial,
    Complete
}
