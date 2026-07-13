# Sistema de Confiança Multidimensional - LabelWise

## 📋 Visão Geral

O sistema de confiança foi refatorado para considerar **três dimensões separadas**:

1. **ProductIdentificationConfidence** - Confiança na identificação do produto
2. **LabelReadingConfidence** - Confiança na leitura do rótulo
3. **FinalAnalysisConfidence** - Confiança na análise final

## 🏗️ Arquitetura

```
LabelWise.Application/
└── Confidence/
    ├── ConfidenceScore.cs                    # Estrutura base de confiança
    ├── ConfidenceThresholds.cs               # Thresholds configuráveis
    ├── MultiDimensionalConfidence.cs         # Modelo das 3 dimensões
    ├── MultidimensionalConfidenceCalculator.cs    # Motor de cálculo
    ├── ConfidenceBasedClassificationAdjuster.cs   # Ajustador de classificação
    ├── MultidimensionalQualityGate.cs        # Quality Gate integrado
    └── ConfidenceDetailsDto.cs               # DTO para resposta da API
```

## 📊 Dimensões de Confiança

### 1. ProductIdentificationConfidence

Avalia a confiança na identificação do produto:

| Fator | Peso | Descrição |
|-------|------|-----------|
| Nome do Produto | 50% | Nome identificado com segurança |
| Marca | 30% | Marca identificada |
| Código de Barras | 20% | Barcode validado |

**Regras:**
- Se `productName == "Produto Desconhecido"` ou contém `???`, score é penalizado em 50%
- Se apenas nome identificado (sem marca/barcode), pequena penalização de 15%

### 2. LabelReadingConfidence

Avalia a confiança na leitura do rótulo:

| Fator | Peso | Descrição |
|-------|------|-----------|
| OCR Score | 25% | Qualidade do texto extraído |
| Ingredientes | 30% | Qualidade dos ingredientes parsed |
| Nutrientes | 30% | Completude das informações nutricionais |
| Alérgenos | 15% | Detecção clara de alérgenos |

**Regras:**
- Se `NutritionalCompletenessRatio < 50%`, reduz confiança em 15%
- Se `InvalidIngredientsRatio > 40%`, reduz confiança em 20%
- Se alérgenos claramente detectados, aumenta confiança em 5%

### 3. FinalAnalysisConfidence

Avalia a confiança na análise final:

| Fator | Penalização |
|-------|-------------|
| Produto não identificado | -25% |
| OCR com ruído excessivo | -15% |
| Ingredientes com ruído | -15% |
| Nutrientes incompletos | -10% |
| Parsing muito incompleto | -20% |

**Máximo de penalização:** 50%

## 🔒 Regras de Classificação

### Regra 1: Safe requer identificação segura
```
SE productName NÃO identificado com segurança
   E classificação = "Safe" ou "Excellent"
ENTÃO classificação = "Incomplete"
```

### Regra 2: Safe requer confiança adequada
```
SE overallConfidence <= Low
   E classificação = "Safe" ou "Excellent"
ENTÃO classificação = "Caution"
```

### Regra 3: Análise não confiável não pode ser otimista
```
SE FinalAnalysis.ClassificationReliable = false
   E classificação = "Safe" ou "Excellent"
ENTÃO classificação = "Caution"
```

### Regra 4: Leitura incompleta impede Safe
```
SE nutrientes incompletos E ingredientes com ruído
   E classificação = "Safe" ou "Excellent"
ENTÃO classificação = "Incomplete"
```

### Regra 5: Baixa identificação rebaixa Safe
```
SE overallConfidence = Medium
   E ProductIdentification.Score.Level = Low
   E classificação = "Safe"
ENTÃO classificação = "Moderate"
```

### Regra 6: Múltiplos alérgenos exigem cautela
```
SE AllergensClearlyDetected = true
   E AllergensCount >= 3
   E classificação = "Safe"
ENTÃO classificação = "Caution"
```

## 📈 Thresholds

```csharp
public static class ConfidenceThresholds
{
    public const double High = 0.90;           // ≥90%
    public const double Medium = 0.65;         // ≥65%
    public const double Low = 0.40;            // ≥40%
    public const double SafeClassificationMinimum = 0.70;
    public const double MaxScorePenalty = 0.50;
}
```

## 🔄 Exemplos Before/After

### Exemplo 1: Produto não identificado classificado como Safe

**BEFORE (Sistema Antigo):**
```json
{
  "classification": "Safe",
  "confidenceLevel": "Médio",
  "productName": "Produto Desconhecido",
  "generalScore": 0.75
}
```

**AFTER (Sistema Novo):**
```json
{
  "classification": "Incomplete",
  "confidenceLevel": "Baixo",
  "productName": "Produto Desconhecido",
  "generalScore": 0.45,
  "confidenceDetails": {
    "productIdentification": {
      "score": 0.25,
      "level": "VeryLow",
      "factors": {
        "productNameIdentified": false,
        "brandIdentified": false
      }
    },
    "finalAnalysis": {
      "classificationReliable": false,
      "penaltyApplied": 0.40
    },
    "adjustments": {
      "classificationWasAdjusted": true,
      "originalClassification": "Safe",
      "adjustedClassification": "Incomplete",
      "classificationAdjustmentReason": "Produto não identificado com segurança"
    }
  }
}
```

### Exemplo 2: Nutrientes incompletos com Safe

**BEFORE:**
```json
{
  "classification": "Safe",
  "confidenceLevel": "Alto",
  "productName": "Biscoito X",
  "generalScore": 0.82,
  "nutritionalFieldsCount": 2
}
```

**AFTER:**
```json
{
  "classification": "Caution",
  "confidenceLevel": "Médio",
  "productName": "Biscoito X",
  "generalScore": 0.65,
  "confidenceDetails": {
    "labelReading": {
      "score": 0.58,
      "level": "Low",
      "details": "2/10 campos nutricionais",
      "factors": {
        "nutrientsIncomplete": true,
        "nutritionalCompletenessRatio": 0.20
      }
    },
    "adjustments": {
      "classificationWasAdjusted": true,
      "originalClassification": "Safe",
      "adjustedClassification": "Caution",
      "scorePenaltyApplied": 0.20
    }
  }
}
```

### Exemplo 3: OCR com ruído excessivo

**BEFORE:**
```json
{
  "classification": "Safe",
  "confidenceLevel": "Médio",
  "extractedIngredients": ["???", "n/a", "farinha", "...", "açúcar"]
}
```

**AFTER:**
```json
{
  "classification": "Incomplete",
  "confidenceLevel": "Baixo",
  "confidenceDetails": {
    "labelReading": {
      "score": 0.42,
      "level": "Low",
      "factors": {
        "hasExcessiveNoise": true,
        "ingredientsHaveExcessiveNoise": true,
        "invalidIngredientsRatio": 0.60
      }
    },
    "overall": {
      "score": 0.38,
      "level": "VeryLow",
      "qualityGatePassed": false,
      "summary": "❌ Leitura muito limitada - tire uma nova foto com melhor qualidade"
    }
  }
}
```

### Exemplo 4: Alérgenos claramente detectados com Safe

**BEFORE:**
```json
{
  "classification": "Safe",
  "extractedAllergens": ["glúten", "leite", "soja", "ovos"]
}
```

**AFTER:**
```json
{
  "classification": "Caution",
  "confidenceDetails": {
    "labelReading": {
      "factors": {
        "allergensClearlyDetected": true,
        "allergensCount": 4,
        "allergensScore": 0.95
      }
    },
    "adjustments": {
      "classificationWasAdjusted": true,
      "originalClassification": "Safe",
      "adjustedClassification": "Caution",
      "classificationAdjustmentReason": "Múltiplos alérgenos detectados (4)"
    }
  }
}
```

### Exemplo 5: Análise completa e confiável

**BEFORE:**
```json
{
  "classification": "Safe",
  "confidenceLevel": "Alto",
  "productName": "Iogurte Natural",
  "brand": "Nestlé",
  "generalScore": 0.88
}
```

**AFTER:**
```json
{
  "classification": "Safe",
  "confidenceLevel": "Alto",
  "productName": "Iogurte Natural",
  "brand": "Nestlé",
  "generalScore": 0.86,
  "confidenceDetails": {
    "productIdentification": {
      "score": 0.92,
      "level": "High",
      "factors": {
        "productNameIdentified": true,
        "brandIdentified": true,
        "identificationSource": "OCR (Nome + Marca)"
      }
    },
    "labelReading": {
      "score": 0.88,
      "level": "Medium",
      "factors": {
        "ingredientsExtracted": true,
        "validIngredientsCount": 8,
        "nutrientsExtracted": true,
        "nutritionalCompletenessRatio": 0.80
      }
    },
    "finalAnalysis": {
      "score": 0.85,
      "level": "Medium",
      "factors": {
        "classificationReliable": true,
        "penaltyApplied": 0.05
      }
    },
    "overall": {
      "score": 0.87,
      "level": "Medium",
      "qualityGatePassed": true,
      "summary": "⚠️ Análise com algumas limitações - verifique os detalhes"
    },
    "adjustments": {
      "classificationWasAdjusted": false
    }
  }
}
```

## 🧪 Testes

Execute os testes do sistema de confiança:

```bash
dotnet test --filter "FullyQualifiedName~MultidimensionalConfidenceCalculatorTests"
```

## 📝 Integração com API

O campo `confidenceDetails` é automaticamente incluído na resposta do pipeline:

```csharp
// ProductAnalysisResultDto
public ConfidenceDetailsDto? ConfidenceDetails { get; set; }
```

O campo `confidenceLevel` (legado) continua disponível para compatibilidade.

## 🔧 Configuração

Os thresholds podem ser ajustados em `ConfidenceThresholds.cs`:

```csharp
public const double High = 0.90;    // Ajustar para ser mais/menos exigente
public const double Medium = 0.65;
public const double Low = 0.40;
```

## 📚 Referências

- [Quality Gate Documentation](QUALITY_GATE_DOCUMENTATION.md)
- [Parser Improvements](PARSER_IMPROVEMENTS_DOCUMENTATION.md)
- [OCR Pipeline](OCR_PIPELINE_DOCUMENTATION.md)
