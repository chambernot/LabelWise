namespace LabelWise.Application.DTOs.Nutrition;

/// <summary>
/// Resposta unificada do endpoint "nutricao-analise-inteligente".
///
/// Pensada para o front consumir diretamente:
///   - Sempre retorna o mesmo contrato, independente da fonte (barcode ou IA).
///   - Mostra a tabela conforme o rótulo + base 100 g/ml (preparada para o
///     score que será adicionado em fase futura).
///   - Inclui metadados úteis (fonte, confiança, avisos).
///
/// NESTA FASE: retorna apenas os dados crus parseados das fontes.
/// Score por perfil (diabético, hipertenso etc.) será adicionado depois,
/// dentro do mesmo contrato (campos já reservados).
/// </summary>
public sealed class IntelligentAnalysisResponse
{
    public bool Success { get; set; }

    /// <summary>"openfoodfacts" | "openai-vision" | "none".</summary>
    public string Source { get; set; } = "none";

    /// <summary>Mensagem amigável para exibir ao usuário (ex.: erro, aviso).</summary>
    public string? Message { get; set; }

    public ProductIdentification Product { get; set; } = new();

    public NutritionTableView Nutrition { get; set; } = new();

    /// <summary>"per100g" | "per100ml" — qual base será usada para score.</summary>
    public string? ScoreBasis { get; set; }

    /// <summary>
    /// Instrui o front sobre qual bloco exibir como tabela principal ao usuário.
    /// "asLabel"   → exibir <see cref="NutritionTableView.AsLabel"/> (valores fiéis ao rótulo,
    ///               por porção quando disponível). Usar quando há porção declarada.
    /// "per100"    → exibir <see cref="NutritionTableView.Per100"/> (base 100g/ml).
    ///               Usar quando o rótulo não tem coluna por porção.
    /// O score e os perfis de saúde SEMPRE usam <see cref="NutritionTableView.Per100"/>,
    /// independente deste campo.
    /// </summary>
    public string DisplayBasis { get; set; } = "per100";

    /// <summary>
    /// Indica a origem dos valores em <see cref="NutritionTableView.Per100"/>.
    /// "direct"  → extraídos diretamente da coluna "100g" ou "100ml" do rótulo.
    /// "derived" → calculados matematicamente a partir da porção declarada
    ///             (ex.: 74 kcal / 12g × 100 = 617 kcal/100g).
    ///             O front deve exibir uma nota como "(estimado a partir da porção)".
    /// </summary>
    public string Per100Source { get; set; } = "direct";

    /// <summary>Nível de processamento inferido: "in_natura" | "processado" | "ultraprocessado" | "desconhecido".</summary>
    public string? ProcessingLevel { get; set; }

    public NutritionProcessingClassificationDto ProcessingClassification { get; set; } = new();

    public List<NutritionQuickFlagDto> QuickFlags { get; set; } = new();

    public NutritionAnalysisQualityDto AnalysisQuality { get; set; } = new();

    public int NutritionReliabilityScore { get; set; } = 0;

    public IngredientContextDto IngredientContext { get; set; } = new();

    public ScoreSection? Score { get; set; }

    public IntelligentAnalysisDiagnostics Diagnostics { get; set; } = new();

    public string Disclaimer { get; set; } =
        "Análise informativa baseada no rótulo. Não substitui orientação de profissional de saúde.";

    /// <summary>
    /// Qualidade da imagem avaliada após retorno da OpenAI Vision.
    /// Quando <see cref="ImageQualityInfo.RetryRequested"/> for true, o front
    /// deve solicitar uma nova foto ao usuário.
    /// </summary>
    public ImageQualityInfo ImageQuality { get; set; } = new();
}

public sealed class NutritionProcessingClassificationDto
{
    public string Level { get; set; } = "unknown";
    public string Confidence { get; set; } = "low";
    public List<string> Reasons { get; set; } = new();
}

public sealed class NutritionQuickFlagDto
{
    public string Type { get; set; } = "positive";
    public string Label { get; set; } = string.Empty;
}

public sealed class NutritionAnalysisQualityDto
{
    public string Mode { get; set; } = "unsafe";
    public string Confidence { get; set; } = "low";
    public string Reason { get; set; } = "Qualidade ainda não avaliada.";
}

public sealed class IngredientContextDto
{
    public string FatSource { get; set; } = "unknown";
    public string ProcessingContext { get; set; } = "unknown";
    public string FoodNature { get; set; } = "unknown";
}

public sealed class ProductIdentification
{
    public string? Name { get; set; }
    public string? Brand { get; set; }
    public string? Barcode { get; set; }
    public string? Category { get; set; }
}

/// <summary>
/// Tabela nutricional em três blocos:
///   - asLabel:    fiel ao rótulo / fonte (porção declarada).
///   - per100:     normalizado por 100 g ou 100 ml (base do score).
///   - perServing: opcional, valores por porção quando aplicável.
/// </summary>
public sealed class NutritionTableView
{
    public string Unit { get; set; } = "g"; // "g" | "ml"
    public ServingDescriptor? Serving { get; set; }
    public NutritionValues? AsLabel { get; set; }
    public NutritionValues? Per100 { get; set; }
    public NutritionValues? PerServing { get; set; }
}

public sealed class ServingDescriptor
{
    public double? Amount { get; set; }
    public string? Unit { get; set; }
    public string? Description { get; set; }
}

public sealed class NutritionValues
{
    public double? CaloriesKcal { get; set; }
    public double? Carbohydrates { get; set; }
    public double? Sugars { get; set; }
    public double? AddedSugars { get; set; }
    public double? Polyols { get; set; }
    public double? Proteins { get; set; }
    public double? TotalFats { get; set; }
    public double? SaturatedFats { get; set; }
    public double? TransFats { get; set; }
    public double? Fiber { get; set; }
    public double? SodiumMg { get; set; }
}

public sealed class ScoreSection
{
    public int? Global { get; set; }
    public string? GlobalLabel { get; set; }
    public string Confidence { get; set; } = "low";
    public string Reliability { get; set; } = "unsafe_read";

    /// <summary>Principal nutriente problemático do produto (ex: "açúcar", "sódio").</summary>
    public string? PrincipalOffender { get; set; }

    /// <summary>Resumo rápido em linguagem simples para exibir no topo do card.</summary>
    public string? ResumoRapido { get; set; }

    /// <summary>Explicação do motivo do score em português direto.</summary>
    public string? ExplicacaoScore { get; set; }

    /// <summary>Ponto principal de atenção ou destaque do produto.</summary>
    public string? PontoPrincipal { get; set; }

    /// <summary>Avaliação por perfil de saúde: diabetico, hipertensao, emagrecimento, ganho_massa.</summary>
    public Dictionary<string, ProfileScore> Profiles { get; set; } = new();

    /// <summary>Avaliação específica do nível de processamento do alimento.</summary>
    public ProcessingScore? Processing { get; set; }

    public List<string> Strengths { get; set; } = new();
    public List<string> Weaknesses { get; set; } = new();
}

public sealed class ProcessingScore
{
    public int Score { get; set; }
    public string Level { get; set; } = "desconhecido";
    public string Label { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public sealed class ProfileScore
{
    public int Score { get; set; }
    public string Label { get; set; } = string.Empty;
    public List<string> Reasons { get; set; } = new();
}

/// <summary>
/// Resultado da avaliação de qualidade da imagem após extração pela OpenAI Vision.
/// </summary>
public sealed class ImageQualityInfo
{
    public string OverallConfidence { get; set; } = "low";
    public bool TableVisible { get; set; }
    public bool TablePartiallyObstructed { get; set; }
    public bool BlurDetected { get; set; }
    public bool ReflectionDetected { get; set; }
    public bool CroppedTable { get; set; }
    public bool FingerObstructionDetected { get; set; }
    public string TextLegibility { get; set; } = "low";
    public bool SafeForPreciseNutritionAnalysis { get; set; }
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Quando true, a extração foi descartada por baixa confiança ou dados insuficientes.
    /// O front deve exibir uma tela pedindo nova foto ao usuário.
    /// </summary>
    public bool RetryRequested { get; set; }

    /// <summary>Motivo legível para exibir ao usuário (ex.: "Tabela nutricional ilegível").</summary>
    public string? Reason { get; set; }

    /// <summary>Código de motivo para o front tratar programaticamente.</summary>
    /// <remarks>"low_confidence" | "insufficient_fields" | "no_critical_fields" | "ok"</remarks>
    public string ReasonCode { get; set; } = "ok";

    /// <summary>Score de confiança global retornado pela OpenAI (0–1). Null quando fonte não é Vision.</summary>
    public double? ConfidenceScore { get; set; }

    /// <summary>Percentual de campos nutricionais preenchidos (0–100).</summary>
    public int? CompletenessPercent { get; set; }
}

public sealed class IntelligentAnalysisDiagnostics
{
    public double? Confidence { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    public int? PreparedSizeBytes { get; set; }
    public bool BarcodeAttempted { get; set; }
    public bool BarcodeFound { get; set; }
    public bool OpenFoodFactsHit { get; set; }
    public bool OpenAiVisionUsed { get; set; }
    public List<string> Warnings { get; set; } = new();
}
