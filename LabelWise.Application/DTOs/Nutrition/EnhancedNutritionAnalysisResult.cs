using System.Collections.Generic;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.DTOs.Nutrition;

/// <summary>
/// Resultado enriquecido da análise nutricional com metadata de origem dos dados.
/// </summary>
public class EnhancedNutritionAnalysisResult
{
    // ═══════════════════════════════════════════════════════════════
    // CAMPOS BÁSICOS (compatível com response original)
    // ═══════════════════════════════════════════════════════════════
    
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    
    public string? ProductName { get; set; }
    public string? Brand { get; set; }
    public string? Category { get; set; }
    public string? PackageWeight { get; set; }
    public AnalysisMode AnalysisMode { get; set; }
    
    public string[]? VisibleClaims { get; set; }
    public EstimatedNutritionProfileDto? EstimatedNutritionProfile { get; set; }
    public ProductClassificationDto? Classification { get; set; }
    public string? Summary { get; set; }
    public string[]? Alerts { get; set; }
    public ConfidenceDetailsDto? ConfidenceDetails { get; set; }
    public List<string> ResumoRapido { get; set; } = new();
    public string? ExplicacaoScore { get; set; }
    public string? PontoPrincipal { get; set; }
    public string Tom { get; set; } = "simples e direto";
    
    public double ProcessingTimeSeconds { get; set; }
    
    // ═══════════════════════════════════════════════════════════════
    // CAMPOS NOVOS (metadata enriquecida)
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Tipo de fonte dos dados nutricionais.
    /// Real = 100% extraído da tabela
    /// Mixed = Parte extraído, parte estimado
    /// EstimatedByCategory = 100% estimado por perfil da categoria
    /// </summary>
    public DataSourceType DataSourceType { get; set; }
    
    /// <summary>
    /// Quantidade de campos nutricionais extraídos da tabela real.
    /// </summary>
    public int RealFieldsCount { get; set; }
    
    /// <summary>
    /// Quantidade de campos nutricionais estimados por categoria.
    /// </summary>
    public int EstimatedFieldsCount { get; set; }
    
    /// <summary>
    /// Indica se fallback por categoria foi aplicado.
    /// </summary>
    public bool FallbackApplied { get; set; }
    
    /// <summary>
    /// Código da categoria normalizada (ex: laticinio_cremoso).
    /// </summary>
    public string? NormalizedCategoryCode { get; set; }
    
    /// <summary>
    /// Nome da categoria normalizada.
    /// </summary>
    public string? NormalizedCategoryName { get; set; }
    
    /// <summary>
    /// Confiança da normalização de categoria (0.0 a 1.0).
    /// </summary>
    public double CategoryNormalizationConfidence { get; set; }
    
    /// <summary>
    /// Score nutricional ponderado por confiança (0-100).
    /// </summary>
    public double NutritionalScore { get; set; }
    
    /// <summary>
    /// Principal ofensor nutricional identificado.
    /// </summary>
    public PrincipalOffenderDto? PrincipalOffender { get; set; }

    public Dictionary<string, string>? FieldSources { get; set; }
    public string[]? Inconsistencies { get; set; }
    public string[]? CategoryNormalizationEvidence { get; set; }
}

/// <summary>
/// DTO do principal ofensor nutricional.
/// </summary>
public class PrincipalOffenderDto
{
    /// <summary>
    /// Tipo do ofensor (Sugar, Fat, Sodium, etc).
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// Valor do nutriente (g/100g ou mg/100g).
    /// </summary>
    public double Value { get; set; }
    
    /// <summary>
    /// Severidade (Low, Medium, High, Critical).
    /// </summary>
    public string Severity { get; set; } = string.Empty;
    
    /// <summary>
    /// Score do ofensor (0-10, quanto maior pior).
    /// </summary>
    public double Score { get; set; }
}
