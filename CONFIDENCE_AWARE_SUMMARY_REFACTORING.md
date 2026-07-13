# Refatoração da Geração de Summary - Baseada em Completude e Confiança

## Resumo das Mudanças

Esta refatoração implementa um sistema de geração de resumos que é **consciente de completude e confiança**, evitando mensagens otimistas quando os dados são insuficientes.

---

## 📋 Regras Implementadas

### 1. Análise Parcial → Nunca Usar Frases Otimistas
Se a análise for parcial (OCR incompleto, produto não identificado, ou dados insuficientes):
- ❌ **NÃO USAR**: "Boa Escolha", "Excelente Escolha"
- ❌ **NÃO USAR**: "Pode consumir regularmente", "Produto adequado para consumo regular"
- ✅ **USAR**: "Análise parcial do rótulo", "Leitura incompleta"
- ✅ **USAR**: "Envie outra imagem para maior precisão"

### 2. Alérgenos Declarados → Evitar Safe
Se há alérgenos declarados no produto:
- ❌ **NÃO USAR**: Classificação `Safe` por padrão
- ✅ **USAR**: Classificação `Caution` com alerta sobre alérgenos

### 3. Produto Não Identificado → Classificação Conservadora
Se o produto não foi identificado com segurança:
- ✅ Classificação deve ser `Caution` ou `Incomplete`
- ✅ Mensagem: "Produto não identificado. Envie imagem mais clara."

### 4. Confiança Alta + Análise Completa → Permitir Afirmativas
Somente quando:
- ✅ Confiança = High
- ✅ Análise completa (OCR + ingredientes + nutrientes)
- ✅ Quality Gate passou
- ✅ Sem alérgenos declarados

Então pode usar mensagens afirmativas como "Excelente Escolha".

---

## 🆕 Novos Arquivos

### `AnalysisContext.cs`
Encapsula o contexto de completude e confiança da análise:
```csharp
public class AnalysisContext
{
    public bool ProductIdentified { get; set; }
    public bool OcrComplete { get; set; }
    public bool AnalysisComplete { get; set; }
    public bool HasDeclaredAllergens { get; set; }
    public ConfidenceLevel OverallConfidenceLevel { get; set; }
    public bool QualityGatePassed { get; set; }
    
    // Propriedades derivadas
    public bool IsPartialAnalysis => !AnalysisComplete || !OcrComplete || !ProductIdentified;
    public bool CanUseAffirmativeMessages => OverallConfidenceLevel == High && AnalysisComplete && QualityGatePassed;
}
```

### `SummaryAdjustmentRules.cs`
Regras explícitas para ajuste de classificação e resumo:
```csharp
public static class SummaryAdjustmentRules
{
    // Frases proibidas em análises parciais
    public static readonly HashSet<string> ProhibitedPhrasesPartialAnalysis = new()
    {
        "Boa Escolha",
        "Excelente Escolha",
        "Pode consumir regularmente",
        // ...
    };

    // Ajusta classificação baseada no contexto
    public static AnalysisClassification AdjustClassification(
        AnalysisClassification original,
        AnalysisContext context,
        out string adjustmentReason);

    // Obtém disclaimers apropriados
    public static List<string> GetDisclaimers(AnalysisContext context);
}
```

### `ConfidenceAwareSummaryGenerator.cs`
Gerador de resumos consciente de confiança:
```csharp
public class ConfidenceAwareSummaryGenerator : IAnalysisSummaryGenerator
{
    public SummaryGenerationResult GenerateSummaryWithContext(
        Product product,
        NutritionalInfo? nutrition,
        IEnumerable<ProductIngredient> ingredients,
        IEnumerable<ProductAllergen> allergens,
        UserProfile? userProfile,
        double generalScore,
        double personalizedScore,
        List<string> alerts,
        List<string> recommendations,
        AnalysisContext context);
}
```

---

## 📊 Before/After - Exemplos de Comportamento

### Cenário 1: OCR Incompleto com Score Alto (85%)

**ANTES:**
```
Classification: Safe
Summary: "**Boa Escolha** (Score: 85/100) - Pode consumir regularmente com moderação."
ShortSummary: "Boa escolha (85/100). Pode consumir regularmente com moderação."
```

**DEPOIS:**
```
Classification: Caution
Summary: "**📷 Leitura incompleta do rótulo** (Score parcial: 85/100) • ⚠️ Informações nutricionais não disponíveis • Envie outra imagem para maior precisão"
ShortSummary: "Leitura incompleta do rótulo. Envie outra imagem. (Score parcial: 85/100)"
Disclaimers: ["📷 Envie outra imagem para maior precisão", "📋 Análise parcial do rótulo"]
```

### Cenário 2: Produto Não Identificado

**ANTES:**
```
Classification: Safe
Summary: "**Boa Escolha** (Score: 90/100)..."
```

**DEPOIS:**
```
Classification: Incomplete
Summary: "**⚠️ Produto não identificado** (Score parcial: 90/100) • ⚠️ Informações nutricionais não disponíveis • Envie outra imagem para maior precisão"
ShortSummary: "Produto não identificado. Envie imagem mais clara. (Score parcial: 90/100)"
```

### Cenário 3: Com Alérgenos Declarados

**ANTES:**
```
Classification: Safe
Summary: "**Boa Escolha** (Score: 85/100)..."
```

**DEPOIS:**
```
Classification: Caution
Summary: "**Perfil Nutricional Aceitável** (Score: 85/100) • 🔴 Alérgenos: Leite, Glúten"
ShortSummary: "Perfil nutricional positivo, mas CONTÉM ALÉRGENOS (85/100). Verifique."
```

### Cenário 4: Análise Completa com Alta Confiança (sem alérgenos)

**ANTES:**
```
Classification: Safe
Summary: "**Boa Escolha** (Score: 85/100)..."
```

**DEPOIS:**
```
Classification: Safe  ✅ (mesmo comportamento, pois dados são confiáveis)
Summary: "**✅ Boa Escolha** (Score: 85/100) - Pode consumir regularmente com moderação."
ShortSummary: "Boa escolha (85/100). Pode consumir regularmente com moderação."
```

---

## ⚙️ Configuração

No arquivo `appsettings.json`:
```json
{
  "SummaryGeneration": {
    "Strategy": "ConfidenceAware"  // Padrão recomendado
  }
}
```

Estratégias disponíveis:
- `ConfidenceAware` - **Recomendado** - Geração consciente de confiança
- `RuleBased` - Geração baseada em regras simples (legado)
- `AiPowered` - Geração com Azure OpenAI

---

## 🧪 Testes

Os seguintes testes validam o novo comportamento:

1. `PartialAnalysis_ShouldNeverUse_BoaEscolha`
2. `PartialAnalysis_ShouldNeverUse_PodeConsumirRegularmente`
3. `PartialAnalysis_ShouldUse_AnaliseParcialDoRotulo`
4. `PartialAnalysis_WithIncompleteOcr_ShouldSuggest_EnvieOutraImagem`
5. `DeclaredAllergens_ShouldNeverUse_SafeClassification`
6. `DeclaredAllergens_ShouldAlertUser`
7. `UnidentifiedProduct_ShouldUse_CautionOrIncomplete`
8. `UnidentifiedProduct_ShouldNotUse_BoaEscolha_EvenWithHighScore`
9. `HighConfidence_CompleteAnalysis_NoAllergens_ShouldAllow_AffirmativeMessages`
10. `HighConfidence_CompleteAnalysis_ShouldReturn_SafeOrExcellent`

---

## 📁 Arquivos Modificados/Criados

### Criados
- `LabelWise.Application\SummaryGeneration\AnalysisContext.cs`
- `LabelWise.Application\SummaryGeneration\SummaryAdjustmentRules.cs`
- `LabelWise.Application\SummaryGeneration\ConfidenceAwareSummaryGenerator.cs`
- `LabelWise.Application.Tests\SummaryGeneration\ConfidenceAwareSummaryGeneratorTests.cs`

### Modificados
- `LabelWise.Application\SummaryGeneration\SummaryGeneratorFactory.cs` - Adicionada estratégia `ConfidenceAware`
- `LabelWise.Application\Rules\RulesEngine.cs` - Integração com contexto de confiança
- `LabelWise.Application\Extensions\ServiceCollectionExtensions.cs` - Registro do novo gerador

---

## 🔐 Princípios de Segurança

1. **Conservador por padrão**: Na dúvida, sempre usar classificação/mensagem mais conservadora
2. **Transparência**: Sempre informar ao usuário quando a análise é parcial
3. **Orientação**: Sempre sugerir ações para melhorar a análise (ex: "Envie outra imagem")
4. **Não enganar**: Nunca passar falsa segurança quando dados são insuficientes
