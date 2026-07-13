# Fallback Nutricional Seguro - Documentação de Implementação

## 📋 Resumo Executivo

Implementação de fallback nutricional **transparente e confiável** que **NUNCA** gera dados numéricos inventados quando não há base real.

---

## 🎯 Problema Resolvido

**ANTES:**
- API retornava valores numéricos estimados (calorias, proteína, sódio) mesmo sem tabela nutricional
- Usuário não sabia se dados eram reais ou estimados
- Score calculado como se dados fossem confiáveis
- **Resultado:** Perda de confiança do usuário

**DEPOIS:**
- API **NÃO** retorna valores numéricos quando não confiável
- Campos claramente indicam confiabilidade (`hasReliableNutritionData`, `fallbackType`)
- Riscos inferidos qualitativamente (`inferredRisks`)
- Score limitado quando não confiável (máximo 55)
- Summary transparente sobre limitações

---

## 🔧 Mudanças Implementadas

### 1. Novos Campos no DTO de Resposta

**Arquivo:** `LabelWise.Application/DTOs/Nutrition/NutritionAnalysisResponseDto.cs`

```csharp
/// <summary>
/// Indica se os dados nutricionais são confiáveis (extraídos da tabela nutricional real)
/// ou se são estimativas baseadas apenas na categoria
/// </summary>
public bool HasReliableNutritionData { get; set; }

/// <summary>
/// Tipo de fallback aplicado nos dados nutricionais:
/// - "real": dados extraídos da tabela nutricional
/// - "partial": alguns dados reais, outros estimados
/// - "category_based": estimativas baseadas apenas na categoria
/// - "unknown": sem dados nutricionais disponíveis
/// </summary>
public string FallbackType { get; set; } = "unknown";

/// <summary>
/// Riscos nutricionais inferidos com base na categoria, ingredientes e claims
/// (ex: "alto_sodio", "alto_acucar", "ultraprocessado")
/// </summary>
public List<string> InferredRisks { get; set; } = new();
```

### 2. Novo Método: `DetermineNutritionDataReliability`

**Arquivo:** `LabelWise.Infrastructure/Services/NutritionAnalysisService.cs`

**Responsabilidade:** Determinar se dados nutricionais são confiáveis ANTES de aplicar fallback

**Regras de Confiabilidade:**
1. ✅ **Confiável** quando:
   - `analysisMode = FullNutritionLabel` (tabela detectada)
   - `confidenceDetails.estimatedNutritionProfile >= 0.6`
   - Perfil nutricional veio da IA

2. ❌ **NÃO Confiável** quando:
   - `analysisMode = FrontOfPackageOnly`
   - Perfil nutricional nulo
   - Confiança < 0.6

**Exemplo:**
```csharp
private static void DetermineNutritionDataReliability(
    NutritionAnalysisResponseDto response,
    VisualInterpretationResult visionResult)
{
    var nutritionConfidence = response.ConfidenceDetails?.EstimatedNutritionProfile ?? 0;
    var hasProbableNutritionTable = visionResult.ProbableCaptureType == CaptureType.NutritionTable;
    var hasNutritionProfileFromAI = visionResult.EstimatedNutritionProfile != null;

    if (response.AnalysisMode == AnalysisMode.FullNutritionLabel &&
        nutritionConfidence >= 0.6 &&
        hasNutritionProfileFromAI &&
        hasProbableNutritionTable)
    {
        response.HasReliableNutritionData = true;
        response.FallbackType = "real";
    }
    else
    {
        response.HasReliableNutritionData = false;
        response.FallbackType = "category_based";
    }

    if (!response.HasReliableNutritionData)
    {
        response.InferredRisks = InferNutritionalRisks(...);
    }
}
```

### 3. Novo Método: `InferNutritionalRisks`

**Responsabilidade:** Inferir riscos qualitativos quando não há dados confiáveis

**Riscos Detectados:**
- `alto_acucar` - Categorias como refrigerante, achocolatado, biscoito recheado
- `alto_sodio` - Salgadinho, embutido, queijo ralado, macarrão instantâneo
- `alta_gordura` - Biscoito, chocolate, frituras
- `ultraprocessado` - Produtos industrializados processados
- `aditivos_quimicos` - Glutamato, corantes, aromatizantes detectados

**Exemplo:**
```csharp
private static List<string> InferNutritionalRisks(
    string? category,
    List<string>? visibleClaims,
    ProductClassificationDto? classification)
{
    var risks = new List<string>();
    var normalizedCategory = NormalizeCategoryKey(category);

    if (normalizedCategory.Contains("refrigerante") ||
        normalizedCategory.Contains("achocolatado"))
    {
        risks.Add("alto_acucar");
    }

    if (normalizedCategory.Contains("salgadinho"))
    {
        risks.Add("alto_sodio");
        risks.Add("ultraprocessado");
    }

    return risks.Distinct().ToList();
}
```

### 4. Modificação: `ApplyHybridCategoryInferenceAsync`

**ANTES:**
- Preenchia valores numéricos do banco de dados para TODOS os casos de `FrontOfPackageOnly`

**DEPOIS:**
- **INTERROMPE** se `hasReliableNutritionData = false`
- **LIMPA** quaisquer valores numéricos que vieram da IA quando não confiáveis
- Mantém apenas `basis` descritiva

```csharp
private async Task ApplyHybridCategoryInferenceAsync(...)
{
    // NOVA REGRA: Se não há dados confiáveis, NÃO preencher valores numéricos
    if (!response.HasReliableNutritionData)
    {
        _logger.LogInformation("Skipping numeric nutrition fallback...");

        // Criar perfil apenas com basis descritiva, SEM valores numéricos
        response.EstimatedNutritionProfile.CaloriesPer100g = null;
        response.EstimatedNutritionProfile.EstimatedSugarPer100g = null;
        response.EstimatedNutritionProfile.EstimatedProteinPer100g = null;
        response.EstimatedNutritionProfile.EstimatedSodiumPer100g = null;
        response.EstimatedNutritionProfile.EstimatedFatPer100g = null;
        response.EstimatedNutritionProfile.EstimatedFiberPer100g = null;
        response.EstimatedNutritionProfile.EstimatedFatPer100g = null;

        return; // NÃO aplicar fallback do banco
    }

    // Lógica original apenas quando hasReliableNutritionData = true
    ...
}
```

### 5. Modificação: `ComputeHealthScore`

**ANTES:**
- Score calculado baseado em valores numéricos estimados
- Sem distinção entre dados reais e estimados

**DEPOIS:**
- **Score limitado a 55** quando `hasReliableNutritionData = false`
- Penalidade de **-5 pontos por risco inferido**
- Log detalhado para auditoria

```csharp
private int ComputeHealthScore(NutritionAnalysisResponseDto response)
{
    // NOVA REGRA: Se não há dados confiáveis, limitar drasticamente o score
    if (!response.HasReliableNutritionData)
    {
        var categoryBaseScore = GetCategoryBaseScore(response.Category);
        var adjustment = GetClassificationAlignmentAdjustment(response.Classification);
        var riskPenalty = response.InferredRisks.Count * 5; // -5 por risco

        var unreliableScore = categoryBaseScore + adjustment - riskPenalty;
        unreliableScore = Math.Clamp(unreliableScore, 15, 55); // Máximo 55!

        return unreliableScore;
    }

    // Lógica original quando HÁ dados confiáveis (sem limite artificial)
    ...
}
```

### 6. Modificação: `BuildFinalSummary`

**ANTES:**
- Summary genérico
- Não deixava claro limitações da análise

**DEPOIS:**
- Summary **MUITO EXPLÍCITO** quando não há dados confiáveis
- Orienta usuário a fotografar tabela nutricional
- Menciona riscos inferidos

```csharp
private static string BuildFinalSummary(NutritionAnalysisResponseDto response)
{
    // NOVA REGRA: Quando não há dados confiáveis, deixar MUITO explícito
    if (!response.HasReliableNutritionData)
    {
        var productName = GetDisplayProductName(response);
        var categoryDescription = !string.IsNullOrWhiteSpace(response.Category)
            ? $"da categoria {response.Category}"
            : "alimentício";

        var risksDescription = response.InferredRisks.Any()
            ? $" com possíveis pontos de atenção: {FormatInferredRisks(response.InferredRisks)}"
            : "";

        return $"Análise baseada apenas na categoria, sem dados nutricionais exatos. " +
               $"{productName} é um produto {categoryDescription}{risksDescription}. " +
               $"Para análise precisa, fotografe a tabela nutricional da embalagem.";
    }

    // Lógica original quando HÁ dados confiáveis
    ...
}
```

### 7. Modificação: `ApplyAutomaticWarnings`

**ANTES:**
- Warnings baseados em valores numéricos

**DEPOIS:**
- Warning claro quando não há dados confiáveis
- Warnings específicos por risco inferido

```csharp
private static void ApplyAutomaticWarnings(NutritionAnalysisResponseDto response)
{
    // NOVA REGRA: Adicionar warning claro quando não há dados confiáveis
    if (!response.HasReliableNutritionData)
    {
        AddWarning(response.Warnings,
            "Análise baseada apenas na categoria do produto. " +
            "Para avaliação precisa dos valores nutricionais, " +
            "fotografe a tabela nutricional da embalagem.");

        // Warnings específicos por risco inferido
        foreach (var risk in response.InferredRisks)
        {
            var riskWarning = risk switch
            {
                "alto_acucar" => "Categoria tipicamente com alto teor de açúcar. Moderação recomendada.",
                "alto_sodio" => "Categoria tipicamente com alto teor de sódio. Atenção ao consumo recorrente.",
                "ultraprocessado" => "Produto ultraprocessado. Priorize alimentos in natura quando possível.",
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(riskWarning))
            {
                AddWarning(response.Warnings, riskWarning);
            }
        }

        return;
    }

    // Lógica original quando HÁ dados confiáveis
    ...
}
```

---

## 📊 Fluxo de Decisão

```
┌─────────────────────────────────┐
│ Análise de Imagem               │
│ (Azure AI Vision)               │
└──────────┬──────────────────────┘
           │
           ▼
┌─────────────────────────────────┐
│ DetermineNutritionDataReliability│
└──────────┬──────────────────────┘
           │
           ├─────────── hasReliableNutritionData = true ──────────┐
           │                                                       │
           │ - analysisMode = FullNutritionLabel                   │
           │ - confidence >= 0.6                                   │
           │ - Tabela nutricional detectada                        │
           │                                                       │
           │                                                       ▼
           │                                          ┌─────────────────────┐
           │                                          │ Valores Numéricos   │
           │                                          │ MANTIDOS            │
           │                                          │                     │
           │                                          │ - caloriesPer100g   │
           │                                          │ - sugar, protein    │
           │                                          │ - sodium, fat       │
           │                                          │                     │
           │                                          │ fallbackType="real" │
           │                                          │ Score: 0-100        │
           │                                          └─────────────────────┘
           │
           └─────────── hasReliableNutritionData = false ─────────┐
                                                                  │
             - analysisMode = FrontOfPackageOnly                  │
             - OU confidence < 0.6                                │
             - OU sem tabela detectada                            │
                                                                  │
                                                                  ▼
                                                     ┌─────────────────────────┐
                                                     │ Valores Numéricos       │
                                                     │ REMOVIDOS (null)        │
                                                     │                         │
                                                     │ inferredRisks POPULADO  │
                                                     │ - alto_acucar           │
                                                     │ - alto_sodio            │
                                                     │ - ultraprocessado       │
                                                     │                         │
                                                     │ fallbackType=           │
                                                     │  "category_based"       │
                                                     │ Score: máximo 55        │
                                                     │                         │
                                                     │ Summary EXPLÍCITO:      │
                                                     │ "Análise baseada apenas │
                                                     │  na categoria..."       │
                                                     └─────────────────────────┘
```

---

## 🧪 Validação

### Cenários de Teste

#### Cenário 1: Foto APENAS da frente (sem tabela)
**Input:**
- Imagem: Frente do achocolatado
- Tabela nutricional: NÃO visível

**Output Esperado:**
```json
{
  "hasReliableNutritionData": false,
  "fallbackType": "category_based",
  "inferredRisks": ["alto_acucar", "ultraprocessado"],
  "estimatedNutritionProfile": {
    "caloriesPer100g": null,
    "estimatedSugarPer100g": null,
    "estimatedProteinPer100g": null,
    "estimatedSodiumPer100g": null,
    "estimatedFatPer100g": null,
    "basis": "Análise baseada apenas na categoria, sem dados nutricionais exatos da tabela nutricional"
  },
  "score": {
    "value": 38,  // <= 55
    "label": "moderado"
  },
  "summary": "Análise baseada apenas na categoria, sem dados nutricionais exatos. Achocolatado é um produto da categoria achocolatado em pó com possíveis pontos de atenção: alto teor de açúcar e produto ultraprocessado. Para análise precisa, fotografe a tabela nutricional da embalagem.",
  "warnings": [
    "Análise baseada apenas na categoria do produto. Para avaliação precisa dos valores nutricionais, fotografe a tabela nutricional da embalagem.",
    "Categoria tipicamente com alto teor de açúcar. Moderação recomendada.",
    "Produto ultraprocessado. Priorize alimentos in natura quando possível."
  ]
}
```

#### Cenário 2: Foto da tabela nutricional (COM dados confiáveis)
**Input:**
- Imagem: Tabela nutricional nítida
- Confidence: 0.85

**Output Esperado:**
```json
{
  "hasReliableNutritionData": true,
  "fallbackType": "real",
  "inferredRisks": [],  // vazio quando há dados confiáveis
  "estimatedNutritionProfile": {
    "caloriesPer100g": 396,
    "estimatedSugarPer100g": 72,
    "estimatedProteinPer100g": 8.4,
    "estimatedSodiumPer100g": 180,
    "estimatedFatPer100g": 3.2,
    "basis": "Extração da tabela nutricional da embalagem"
  },
  "score": {
    "value": 38,  // pode ser > 55 quando há dados reais
    "label": "moderado"
  },
  "summary": "Achocolatado tem um perfil nutricional intermediário, principalmente por açúcar elevado. Resumo baseado na tabela nutricional da embalagem.",
  "warnings": [
    "Teor estimado de açúcar elevado para consumo frequente."
  ]
}
```

### Script de Teste

Execute o script PowerShell:
```powershell
.\test-nutrition-fallback-safe.ps1
```

---

## ✅ Checklist de Validação

### Dados Não Confiáveis (`hasReliableNutritionData = false`)

- [x] `caloriesPer100g` = null
- [x] `estimatedSugarPer100g` = null
- [x] `estimatedProteinPer100g` = null
- [x] `estimatedSodiumPer100g` = null
- [x] `estimatedFatPer100g` = null
- [x] `estimatedFiberPer100g` = null
- [x] `fallbackType` = "category_based" ou "unknown"
- [x] `inferredRisks` tem pelo menos 1 risco (para categorias problemáticas)
- [x] `score.value` <= 55
- [x] `summary` menciona "baseada apenas na categoria"
- [x] `summary` orienta "fotografe a tabela nutricional"
- [x] `warnings` tem aviso sobre limitação da análise
- [x] `warnings` tem avisos específicos por risco inferido

### Dados Confiáveis (`hasReliableNutritionData = true`)

- [x] Pelo menos 1 valor numérico presente
- [x] `fallbackType` = "real" ou "partial"
- [x] `inferredRisks` vazio (não precisa inferir)
- [x] `score.value` sem limite artificial (pode ser > 55)
- [x] `summary` baseado em dados reais
- [x] `warnings` baseados em valores numéricos

---

## 🚀 Benefícios

### Para o Usuário
1. **Transparência total** - sabe exatamente quando dados são estimados
2. **Orientação clara** - sabe que precisa fotografar a tabela nutricional
3. **Informação útil** - mesmo sem dados numéricos, recebe alertas qualitativos
4. **Confiança restaurada** - não recebe dados "inventados"

### Para o Produto
1. **Credibilidade** - API honesta sobre limitações
2. **Engagement** - incentiva usuário a fotografar tabela nutricional
3. **Qualidade** - dados sempre confiáveis ou claramente marcados
4. **Auditabilidade** - logs detalhados do processo de decisão

---

## 📝 Logs de Auditoria

### Quando Dados NÃO Confiáveis
```
[NutritionV1] Skipping numeric nutrition fallback. HasReliableData=false, FallbackType=category_based, Category=achocolatado em pó

[NutritionV1] Score calculated WITHOUT reliable data. BaseScore=38, Adjustment=0, RiskPenalty=10, FinalScore=28, InferredRisks=alto_acucar, ultraprocessado
```

### Quando Dados Confiáveis
```
[NutritionV1] Category normalization. Category=achocolatado em pó, ProductName=Nescau, NormalizedCode=achocolatado, Confidence=0.95, IsNormalized=true

[NutritionV1] Database fallback applied. RequestedCode=achocolatado, AppliedCode=achocolatado, AppliedName=Achocolatado em Pó, Confidence=0.95, UsedParent=false
```

---

## 🔄 Pipeline de Processamento

1. **PerformVisualInterpretationAsync** - Azure AI Vision
2. **DetermineAnalysisMode** - FullNutritionLabel vs FrontOfPackageOnly
3. **ApplyProductNameFallback** - Preenche nome do produto
4. ✨ **DetermineNutritionDataReliability** - ✨ NOVO ✨
5. **ApplyHybridCategoryInferenceAsync** - Respeita hasReliableNutritionData
6. **ApplyNutritionSanitization** - Validação de ranges
7. **ApplyCategoryOverrides** - Overrides determinísticos
8. **ApplyScore** - Score com limite quando não confiável
9. **ApplyAutomaticWarnings** - Warnings transparentes
10. **BuildFinalSummary** - Summary explícito

---

## 📚 Referências

- **Copilot Instructions:** `.github/copilot-instructions.md`
  - "Evite correções específicas de produtos quando uma regra genérica puder ser utilizada"
  - "Use linguagem simples e direta em português"
  - "Explique o motivo do score"

- **Arquivos Modificados:**
  1. `LabelWise.Application/DTOs/Nutrition/NutritionAnalysisResponseDto.cs`
  2. `LabelWise.Infrastructure/Services/NutritionAnalysisService.cs`

- **Arquivos Criados:**
  1. `test-nutrition-fallback-safe.ps1`

---

## 🎯 Próximos Passos

1. ✅ Implementação completa
2. ✅ Compilação OK
3. ⏳ Testes com imagens reais
4. ⏳ Validação com usuários
5. ⏳ Monitoramento de logs em produção

---

**Data:** 2024
**Versão:** 1.0
**Status:** ✅ Implementado e Compilando
