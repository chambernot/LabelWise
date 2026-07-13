using System.Collections.Generic;

namespace LabelWise.Application.DTOs.Nutrition;

/// <summary>
/// Resultado da detecção de principal ofensor.
/// </summary>
public class PrincipalOffenderResult
{
    public bool HasOffender { get; set; }
    public OffenderScore? PrincipalOffender { get; set; }
    public List<OffenderScore> AllOffenders { get; set; } = new();

    public static PrincipalOffenderResult NoOffender()
    {
        return new PrincipalOffenderResult
        {
            HasOffender = false,
            AllOffenders = new List<OffenderScore>()
        };
    }
}

/// <summary>
/// Score de um ofensor nutricional.
/// </summary>
public class OffenderScore
{
    public OffenderType Type { get; set; }
    public double Score { get; set; }
    public double Value { get; set; }
    public OffenderSeverity Severity { get; set; }
}

/// <summary>
/// Tipo de ofensor nutricional.
/// </summary>
public enum OffenderType
{
    Sugar,
    Fat,
    Sodium,
    CalorieDensity,
    LowProtein,
    LowFiber
}

/// <summary>
/// Severidade do ofensor.
/// </summary>
public enum OffenderSeverity
{
    Low,
    Medium,
    High,
    Critical
}
