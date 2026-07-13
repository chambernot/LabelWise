using System;
using System.Collections.Generic;
using System.Linq;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services;

/// <summary>
/// Detecta o principal ofensor nutricional de um produto.
/// </summary>
public class PrincipalOffenderDetector : IPrincipalOffenderDetector
{
    private readonly ILogger<PrincipalOffenderDetector> _logger;

    public PrincipalOffenderDetector(ILogger<PrincipalOffenderDetector> logger)
    {
        _logger = logger;
    }

    public PrincipalOffenderResult Detect(EstimatedNutritionProfileDto profile)
    {
        try
        {
            var offenders = new List<OffenderScore>();

            // 1. Avaliar açúcar
            if (profile.EstimatedSugarPer100g.HasValue)
            {
                var sugarScore = CalculateSugarOffenderScore(profile.EstimatedSugarPer100g.Value);
                if (sugarScore > 0)
                {
                    offenders.Add(new OffenderScore
                    {
                        Type = OffenderType.Sugar,
                        Score = sugarScore,
                        Value = profile.EstimatedSugarPer100g.Value,
                        Severity = DetermineSeverity(sugarScore)
                    });
                }
            }

            // 2. Avaliar gordura total
            if (profile.EstimatedFatPer100g.HasValue)
            {
                var fatScore = CalculateFatOffenderScore(profile.EstimatedFatPer100g.Value);
                if (fatScore > 0)
                {
                    offenders.Add(new OffenderScore
                    {
                        Type = OffenderType.Fat,
                        Score = fatScore,
                        Value = profile.EstimatedFatPer100g.Value,
                        Severity = DetermineSeverity(fatScore)
                    });
                }
            }

            // 3. Avaliar sódio
            if (profile.EstimatedSodiumPer100g.HasValue)
            {
                var sodiumScore = CalculateSodiumOffenderScore(profile.EstimatedSodiumPer100g.Value);
                if (sodiumScore > 0)
                {
                    offenders.Add(new OffenderScore
                    {
                        Type = OffenderType.Sodium,
                        Score = sodiumScore,
                        Value = profile.EstimatedSodiumPer100g.Value,
                        Severity = DetermineSeverity(sodiumScore)
                    });
                }
            }

            // 4. Avaliar densidade calórica
            if (profile.CaloriesPer100g.HasValue)
            {
                var calorieScore = CalculateCalorieOffenderScore(profile.CaloriesPer100g.Value);
                if (calorieScore > 0)
                {
                    offenders.Add(new OffenderScore
                    {
                        Type = OffenderType.CalorieDensity,
                        Score = calorieScore,
                        Value = profile.CaloriesPer100g.Value,
                        Severity = DetermineSeverity(calorieScore)
                    });
                }
            }

            // 5. Avaliar baixa proteína (se aplicável)
            if (profile.EstimatedProteinPer100g.HasValue && profile.CaloriesPer100g.HasValue)
            {
                var proteinScore = CalculateLowProteinOffenderScore(
                    profile.EstimatedProteinPer100g.Value,
                    profile.CaloriesPer100g.Value);
                
                if (proteinScore > 0)
                {
                    offenders.Add(new OffenderScore
                    {
                        Type = OffenderType.LowProtein,
                        Score = proteinScore,
                        Value = profile.EstimatedProteinPer100g.Value,
                        Severity = DetermineSeverity(proteinScore)
                    });
                }
            }

            // 6. Avaliar baixa fibra
            if (profile.EstimatedFiberPer100g.HasValue)
            {
                var fiberScore = CalculateLowFiberOffenderScore(profile.EstimatedFiberPer100g.Value);
                if (fiberScore > 0)
                {
                    offenders.Add(new OffenderScore
                    {
                        Type = OffenderType.LowFiber,
                        Score = fiberScore,
                        Value = profile.EstimatedFiberPer100g.Value,
                        Severity = DetermineSeverity(fiberScore)
                    });
                }
            }

            // 7. Determinar principal ofensor
            var principalOffender = offenders.OrderByDescending(o => o.Score).FirstOrDefault();

            if (principalOffender != null)
            {
                _logger.LogInformation(
                    "Principal offender detected: {Type} (score: {Score}, severity: {Severity})",
                    principalOffender.Type, principalOffender.Score, principalOffender.Severity);

                return new PrincipalOffenderResult
                {
                    HasOffender = true,
                    PrincipalOffender = principalOffender,
                    AllOffenders = offenders.OrderByDescending(o => o.Score).ToList()
                };
            }

            return PrincipalOffenderResult.NoOffender();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting principal offender");
            return PrincipalOffenderResult.NoOffender();
        }
    }

    private double CalculateSugarOffenderScore(double sugarPer100g)
    {
        // Baseado em diretrizes WHO e Anvisa
        // > 15g/100g = alto
        // > 22.5g/100g = muito alto
        if (sugarPer100g > 22.5) return 10.0;
        if (sugarPer100g > 15) return 7.0;
        if (sugarPer100g > 10) return 4.0;
        if (sugarPer100g > 5) return 2.0;
        return 0;
    }

    private double CalculateFatOffenderScore(double fatPer100g)
    {
        // > 17.5g/100g = alto
        // > 25g/100g = muito alto
        if (fatPer100g > 25) return 9.0;
        if (fatPer100g > 17.5) return 6.0;
        if (fatPer100g > 10) return 3.0;
        return 0;
    }

    private double CalculateSodiumOffenderScore(double sodiumPer100g)
    {
        // > 600mg/100g = alto
        // > 900mg/100g = muito alto
        if (sodiumPer100g > 900) return 10.0;
        if (sodiumPer100g > 600) return 7.0;
        if (sodiumPer100g > 400) return 4.0;
        if (sodiumPer100g > 200) return 2.0;
        return 0;
    }

    private double CalculateCalorieOffenderScore(double caloriesPer100g)
    {
        // Densidade calórica alta
        // > 400 kcal/100g = muito alto
        // > 300 kcal/100g = alto
        if (caloriesPer100g > 400) return 8.0;
        if (caloriesPer100g > 300) return 5.0;
        if (caloriesPer100g > 200) return 2.0;
        return 0;
    }

    private double CalculateLowProteinOffenderScore(double proteinPer100g, double caloriesPer100g)
    {
        // Se produto tem muita caloria mas pouca proteína
        // Densidade proteica baixa em produto calórico
        if (caloriesPer100g > 200 && proteinPer100g < 5) return 5.0;
        if (caloriesPer100g > 300 && proteinPer100g < 7) return 4.0;
        return 0;
    }

    private double CalculateLowFiberOffenderScore(double fiberPer100g)
    {
        // Fibra muito baixa (< 1g/100g) pode ser um problema
        // Mas só consideramos ofensor se for MUITO baixo
        if (fiberPer100g < 0.5) return 3.0;
        if (fiberPer100g < 1.0) return 1.5;
        return 0;
    }

    private OffenderSeverity DetermineSeverity(double score)
    {
        if (score >= 9.0) return OffenderSeverity.Critical;
        if (score >= 6.0) return OffenderSeverity.High;
        if (score >= 3.0) return OffenderSeverity.Medium;
        return OffenderSeverity.Low;
    }
}
