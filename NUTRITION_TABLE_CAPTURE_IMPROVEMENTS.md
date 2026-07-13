# Melhorias no Tratamento de CaptureType = NutritionTable

## Resumo das Mudanças

Este documento descreve as melhorias implementadas para tratar corretamente imagens de tabelas nutricionais no LabelWise.

### Problema Anterior

Quando uma imagem de tabela nutricional era enviada com `CaptureType = NutritionTable`, o sistema:

1. ❌ Tentava identificar `productName` e `brand` na imagem
2. ❌ Penalizava a confiança por não encontrar identificação do produto
3. ❌ Retornava `Classification = "Incomplete"` mesmo com boa leitura da tabela
4. ❌ Não retornava os nutrientes extraídos de forma estruturada
5. ❌ Mensagem de erro genérica: "Produto não identificado"

### Solução Implementada

Agora o sistema reconhece capturas parciais e trata cada tipo de forma especializada:

1. ✅ Não tenta identificar produto/marca em tabelas nutricionais
2. ✅ Confiança baseada na qualidade do OCR e quantidade de nutrientes extraídos
3. ✅ Retorna `Classification = "Partial"` indicando análise válida mas incompleta
4. ✅ Retorna `NutritionalFacts` estruturado com todos os valores extraídos
5. ✅ Mensagem clara: "Tabela nutricional identificada com sucesso"
6. ✅ Lista `MissingSteps` para completar a análise

---

## Arquivos Modificados

### 1. `LabelWise.Application\Parsing\IngredientAllergenParseResult.cs`

**Adições:**
- `IsPartialAnalysis` - Flag indicando análise parcial
- `SourceCaptureType` - Tipo de captura que originou o resultado
- `MissingSteps` - Lista de capturas faltantes
- `PartialAnalysisMessage` - Mensagem personalizada
- `NutritionData.AddedSugars`, `Calcium`, `Iron`, `Lactose`, vitaminas
- `NutritionData.DailyValuePercentages` - % Valores Diários
- `NutritionData.HasData` e `FilledFieldsCount` - Métricas de completude

### 2. `LabelWise.Application\Parsing\Strategies\NutritionTableParser.cs`

**Melhorias:**
- Suporte a mais keywords (português brasileiro, abreviações)
- Extração de valores por linha (mais preciso)
- Novos nutrientes: cálcio, ferro, lactose, vitaminas A/C/D
- Extração de %VD (Valores Diários)
- Normalização de números com vírgula
- Validação de valores suspeitos

### 3. `LabelWise.Application\Parsing\Strategies\NutritionTableParseResult.cs`

**Adições:**
- `Calcium`, `Iron`, `Lactose`, `VitaminA`, `VitaminC`, `VitaminD`
- `DailyValuePercentages` - Dictionary de %VD
- `Per100g` - Valores por 100g (opcional)
- `ExtractedFieldsCount` - Contagem de campos preenchidos
- `IsComplete` - Indica se todos os macros principais foram extraídos

### 4. `LabelWise.Application\DTOs\ProductAnalysisResultDto.cs`

**Adições:**
- `IsPartialAnalysis` - Flag de análise parcial
- `CaptureType` - Tipo de captura
- `MissingSteps` - Capturas faltantes
- `NutritionalFacts` - DTO estruturado com nutrientes

### 5. `LabelWise.Infrastructure\Services\ProductAnalysisPipelineOrchestrator.cs`

**Novos Métodos:**
- `ParseNutritionTableCapture()` - Parser especializado para tabelas nutricionais
- `ParseIngredientsListCapture()` - Parser para listas de ingredientes
- `ParseAllergenStatementCapture()` - Parser para declarações de alérgenos
- `ParseFrontPackagingCapture()` - Parser para embalagens frontais
- `ExecutePartialAnalysisAsync()` - Análise otimizada para capturas parciais
- `GeneratePartialAnalysisSummary()` - Sumário apropriado
- `GeneratePartialAnalysisAlerts()` - Alertas relevantes
- `GeneratePartialAnalysisRecommendations()` - Próximos passos

### 6. `LabelWise.Application\Confidence\MultidimensionalQualityGate.cs`

**Novo Método:**
- `ApplyPartialAnalysisQualityGate()` - Quality Gate especializado que não penaliza por falta de identificação do produto

---

## Exemplos de Resposta

### ANTES (Tabela Nutricional)

```json
{
  "success": true,
  "captureType": "NutritionTable",
  "overallConfidence": 0.35,
  "finalAnalysis": {
    "productName": "Produto Desconhecido",
    "classification": "Incomplete",
    "confidenceLevel": "Baixo",
    "generalScore": 0.0,
    "summary": "Produto não identificado. Tire nova foto do rótulo.",
    "alerts": [
      "⚠️ Nome do produto não identificado",
      "⚠️ Marca não encontrada",
      "⚠️ Análise incompleta"
    ],
    "extractedIngredients": [],
    "extractedAllergens": []
  }
}
```

### DEPOIS (Tabela Nutricional)

```json
{
  "success": true,
  "captureType": "NutritionTable",
  "overallConfidence": 0.78,
  "finalAnalysis": {
    "productName": "Análise Parcial",
    "classification": "Partial",
    "confidenceLevel": "Médio",
    "generalScore": 0.72,
    "isPartialAnalysis": true,
    "captureType": "NutritionTable",
    "missingSteps": ["IngredientsList", "FrontPackaging"],
    "summary": "📊 **Tabela nutricional identificada com sucesso.** Foram extraídos 12 valores nutricionais. Envie a lista de ingredientes ou frente da embalagem para uma análise completa do produto.",
    "shortSummary": "Tabela nutricional lida (72/100). Envie ingredientes para análise completa.",
    "nutritionalFacts": {
      "servingSize": "30 g",
      "calories": 120,
      "totalFat": 3.5,
      "saturatedFat": 1.5,
      "transFat": 0,
      "sodium": 150,
      "totalCarbohydrate": 22,
      "dietaryFiber": 2,
      "sugars": 8,
      "addedSugars": 5,
      "protein": 3,
      "calcium": 120,
      "extractedFieldsCount": 12,
      "isComplete": true,
      "dailyValuePercentages": {
        "Sodium": 6,
        "Carbohydrates": 7,
        "Protein": 6
      }
    },
    "alerts": [
      "⚠️ Alto teor de açúcares adicionados: 5g por porção",
      "ℹ️ Análise parcial (NutritionTable). Complete com mais capturas para resultado final."
    ],
    "recommendations": [
      "📋 Envie foto da lista de ingredientes para verificar aditivos e conservantes",
      "📦 Envie foto da frente da embalagem para identificar o produto"
    ]
  }
}
```

---

## Fluxo de Processamento Atualizado

```
┌─────────────────────────────────────────────────────────────────┐
│                    IMAGEM RECEBIDA                              │
│                 CaptureType = NutritionTable                    │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                        OCR (Azure/Tesseract)                    │
│   ✅ Extrai texto com confiança 0.92                            │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│              PARSING ESPECIALIZADO                              │
│   ✅ ParseNutritionTableCapture()                               │
│   ✅ NÃO tenta extrair productName/brand                        │
│   ✅ Marca IsPartialAnalysis = true                             │
│   ✅ Define MissingSteps = ["IngredientsList", "FrontPackaging"]│
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│              ANÁLISE PARCIAL                                    │
│   ✅ ExecutePartialAnalysisAsync()                              │
│   ✅ Score baseado em nutrientes extraídos                      │
│   ✅ NÃO penaliza por falta de identificação                    │
│   ✅ Gera sumário apropriado para análise parcial               │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│              QUALITY GATE PARCIAL                               │
│   ✅ ApplyPartialAnalysisQualityGate()                          │
│   ✅ Threshold mais baixo (0.40 vs 0.70)                        │
│   ✅ Confiança baseada em OCR + quantidade de nutrientes        │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│              RESPOSTA                                           │
│   ✅ Classification = "Partial"                                 │
│   ✅ IsPartialAnalysis = true                                   │
│   ✅ NutritionalFacts estruturado                               │
│   ✅ MissingSteps indicando próximos passos                     │
│   ✅ Sumário informativo e construtivo                          │
└─────────────────────────────────────────────────────────────────┘
```

---

## Validação

### Testes Recomendados

1. **Tabela Nutricional de Boa Qualidade**
   ```bash
   curl -X POST "http://localhost:5000/api/pipeline/analyze" \
     -F "file=@tabela_nutricional.jpg" \
     -F "captureType=3"
   ```
   - Espera-se: `isPartialAnalysis=true`, `nutritionalFacts` preenchido, `confidence >= 0.70`

2. **Tabela Nutricional de Baixa Qualidade**
   ```bash
   curl -X POST "http://localhost:5000/api/pipeline/analyze" \
     -F "file=@tabela_borrada.jpg" \
     -F "captureType=3"
   ```
   - Espera-se: `isPartialAnalysis=true`, menor confiança, alerta sobre qualidade

3. **Lista de Ingredientes**
   ```bash
   curl -X POST "http://localhost:5000/api/pipeline/analyze" \
     -F "file=@ingredientes.jpg" \
     -F "captureType=4"
   ```
   - Espera-se: `isPartialAnalysis=true`, `extractedIngredients` preenchido, `missingSteps=["NutritionTable", "FrontPackaging"]`

---

## Considerações Técnicas

### Performance
- O parsing especializado por CaptureType é mais eficiente pois não executa lógica desnecessária
- O Quality Gate parcial tem menos cálculos que o completo

### Compatibilidade
- Mudanças são retrocompatíveis
- APIs existentes continuam funcionando
- Novos campos são opcionais na resposta

### Extensibilidade
- Fácil adicionar novos tipos de captura
- Parsers especializados podem ser criados para cada tipo
- Sistema de MissingSteps permite guiar o usuário
