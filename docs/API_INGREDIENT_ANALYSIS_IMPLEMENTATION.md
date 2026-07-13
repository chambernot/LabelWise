# 📋 API de Análise de Ingredientes - Guia de Implementação

**Endpoint**: `POST /api/food/ingredient-analysis`  
**Versão**: 2.0  
**Status**: ✅ Produção  
**Última Atualização**: Janeiro 2025  

---

## 📑 ÍNDICE

1. [Visão Geral](#visão-geral)
2. [Estrutura de Request](#estrutura-de-request)
3. [Estrutura de Response](#estrutura-de-response)
4. [Regras de Negócio](#regras-de-negócio)
5. [Fluxo de Processamento](#fluxo-de-processamento)
6. [Perfis Alimentares](#perfis-alimentares)
7. [Claims Regulatórios](#claims-regulatórios)
8. [Detecção de Alergênicos](#detecção-de-alergênicos)
9. [Classificação de Processamento](#classificação-de-processamento)
10. [Casos de Uso](#casos-de-uso)
11. [Exemplos Práticos](#exemplos-práticos)
12. [Códigos de Erro](#códigos-de-erro)

---

## 🎯 VISÃO GERAL

### O que faz?

A API de Análise de Ingredientes processa imagens de rótulos alimentares e retorna:

- ✅ **Lista de ingredientes detectados** (normalizados e classificados)
- ✅ **Claims regulatórios** ("CONTÉM GLÚTEN", "SEM LACTOSE", etc.)
- ✅ **Riscos alergênicos** (detectados + possível contaminação cruzada)
- ✅ **Compatibilidade com perfis alimentares** (vegano, vegetariano, sem glúten, sem lactose, diabético)
- ✅ **Nível de processamento** (natural, processado, ultraprocessado)
- ✅ **Resumo assistido por IA** (explicação em linguagem natural)
- ✅ **Cards de apresentação** (prontos para UI)
- ✅ **Insights rápidos** (alertas e recomendações)

### Tecnologias Utilizadas

- **Azure Document Intelligence** - OCR estruturado
- **OpenAI Vision (GPT-4o)** - Análise visual inteligente
- **Regex avançado** - Detecção de claims regulatórios
- **Knowledge Base** - Base de conhecimento alimentar
- **Engines especializados** - RegulatoryEngine, ConflictEngine, DietProfileEngine

---

## 📤 ESTRUTURA DE REQUEST

### Endpoint

```
POST /api/food/ingredient-analysis
Content-Type: multipart/form-data
```

### Headers

```http
Content-Type: multipart/form-data
Authorization: Bearer {token}  // Opcional, se houver autenticação
```

### Body (Form Data)

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `image` | File | ✅ Sim | Imagem do rótulo (JPEG, PNG, WEBP) |
| `analysisMode` | String | ❌ Não | `"fast"` ou `"detailed"` (default: `"fast"`) |

### Formatos Aceitos

- **JPEG** (.jpg, .jpeg)
- **PNG** (.png)
- **WEBP** (.webp)

### Limites

- **Tamanho máximo**: 10 MB
- **Resolução mínima**: 800x600 pixels (recomendado)
- **Resolução máxima**: 4096x4096 pixels

### Exemplo de Request (cURL)

```bash
curl -X POST "https://api.labelwise.com/api/food/ingredient-analysis" \
  -H "Content-Type: multipart/form-data" \
  -F "image=@rotulo.jpg" \
  -F "analysisMode=detailed"
```

### Exemplo de Request (JavaScript)

```javascript
const formData = new FormData();
formData.append('image', fileInput.files[0]);
formData.append('analysisMode', 'detailed');

const response = await fetch('/api/food/ingredient-analysis', {
  method: 'POST',
  body: formData
});

const result = await response.json();
```

---

## 📥 ESTRUTURA DE RESPONSE

### Response Principal

```typescript
{
  // Ingredientes detectados
  "ingredients": string[],
  "ingredientsNormalized": IngredientNormalizedDto[],
  
  // Claims regulatórios
  "claims": ClaimDetectionDto[],
  "claimsSummary": string,
  
  // Alergênicos
  "allergens": AllergenRiskDto[],
  "allergenWarnings": string[],
  
  // Perfis alimentares
  "profiles": DietProfilesDto,
  
  // Processamento
  "processingLevel": string,
  "processingScore": number,
  "processingDescription": string,
  
  // Apresentação
  "summaryCards": SummaryCardDto[],
  "quickInsights": QuickInsightDto[],
  "assistantSummary": AssistantSummaryDto,
  
  // Metadados
  "confidence": number,
  "analysisQuality": string,
  "warnings": string[],
  "conflictsDetected": AnalysisConflictDto[]
}
```

### IngredientNormalizedDto

```typescript
{
  "raw": string,              // Texto original
  "normalized": string,       // Texto normalizado
  "confidence": number,       // 0.0 a 1.0
  "source": string,           // "ocr", "vision", "inference"
  "isCompound": boolean,      // Se é ingrediente composto
  "components": string[]      // Sub-ingredientes (se isCompound)
}
```

### ClaimDetectionDto

```typescript
{
  "text": string,                    // Texto do claim
  "type": string,                    // "contains", "may_contain", "free_from", etc.
  "confidence": string,              // "high", "medium", "low"
  "subject": string,                 // "glúten", "lactose", "leite", etc.
  "isAbsolute": boolean,             // Se é absoluto ou probabilístico
  "evidenceType": string,            // "ClaimDetected", "RegulatoryInformation"
  "trustLevel": number,              // 0-100
  "evidence": SemanticEvidenceDto[]  // Evidências que suportam o claim
}
```

### AllergenRiskDto

```typescript
{
  "name": string,               // Nome do alergênico
  "riskType": string,           // "contains", "may_contain", "cross_contamination"
  "confidence": string,         // "high", "medium", "low"
  "severity": string,           // "critical", "moderate", "low"
  "evidence": string[],         // Evidências da detecção
  "sources": string[]           // Fontes (claim, ingrediente, inferência)
}
```

### DietProfilesDto

```typescript
{
  "vegan": DietProfileCompatibilityDto,
  "vegetarian": DietProfileCompatibilityDto,
  "lactoseFree": DietProfileCompatibilityDto,
  "glutenFree": DietProfileCompatibilityDto,
  "diabeticFriendly": DietProfileCompatibilityDto
}
```

### DietProfileCompatibilityDto

```typescript
{
  "compatible": boolean,
  "compatibilityStatus": string,     // "compatible", "incompatible", "uncertain", etc.
  "compatibilityLevel": string,      // "high", "low", "unknown"
  "confidence": string,              // "high", "medium", "low"
  "reasons": string[],               // Motivos da decisão
  "warnings": string[],              // Avisos importantes
  "reasonSources": string[],         // Fontes das decisões
  "status": string,                  // FoodCompatibilityStatus
  "evidence": SemanticEvidenceDto[]  // Evidências
}
```

### SummaryCardDto

```typescript
{
  "title": string,
  "subtitle": string,
  "severity": string,      // "info", "warning", "critical"
  "color": string,         // "green", "yellow", "red"
  "icon": string,          // Nome do ícone
  "actionableMessage": string
}
```

### AssistantSummaryDto

```typescript
{
  "overallRecommendation": string,
  "mainPoints": string[],
  "dietaryConsiderations": string[],
  "healthWarnings": string[],
  "positiveAspects": string[]
}
```

---

## 🎯 REGRAS DE NEGÓCIO

### 1. Hierarquia de Evidências

**REGRA CRÍTICA**: Claims regulatórios SEMPRE vencem inferências.

```
Prioridade de Evidências:
100 - RegulatoryClaimExplicit  ("CONTÉM GLÚTEN")
 90 - IngredientExplicit        ("farinha de trigo")
 80 - OcrConfirmed              (OCR estruturado)
 70 - VisionConfirmed           (OpenAI Vision)
 50 - SemanticInference         (Inferência semântica)
 20 - SimilarityGuess           (Chute por similaridade)
```

**Exemplo**:
```
Claim: "SEM GLÚTEN" (prioridade 100)
Ingrediente: "farinha de trigo" (prioridade 90)

→ CONFLITO DETECTADO
→ Requer revisão manual
→ Status: "inconsistent"
```

### 2. Detecção de Claims Regulatórios

#### Padrões Detectados

| Padrão | Regex | Tipo | Absoluto? |
|--------|-------|------|-----------|
| CONTÉM X | `CONTÉM\s+(.+?)` | `contains` | ✅ Sim |
| PODE CONTER X | `PODE CONTER\s+(.+?)` | `may_contain` | ❌ Não |
| SEM X | `SEM\s+(.+?)` | `free_from` | ✅ Sim |
| ZERO X | `ZERO\s+(.+?)` | `free_from` | ✅ Sim |
| NÃO CONTÉM X | `NÃO CONTÉM\s+(.+?)` | `free_from` | ✅ Sim |
| TRAÇOS DE X | `TRAÇOS DE\s+(.+?)` | `may_contain` | ❌ Não |
| FABRICADO EM... | `FABRICADO EM EQUIPAMENTO...` | `cross_contamination` | ❌ Não |

#### Normalização de Claims

```csharp
// Normalização automática
"sem glúten" → "NÃO CONTÉM GLÚTEN"
"zero lactose" → "NÃO CONTÉM LACTOSE"
"sem açúcar" → "ZERO AÇÚCAR"
"vegano" → "VEGANO"
"vegetariano" → "VEGETARIANO"
"plant based" → "PLANT BASED"
"orgânico" → "ORGÂNICO"
```

### 3. Detecção de Ingredientes

#### Blocos de Ingredientes

O sistema identifica blocos com padrões:
- `"Ingredientes:"` ou `"Ingredients:"`
- `"Composição:"` ou `"Ingredientes:"`
- `"INGR.:"` ou variações

#### Normalização

```csharp
// Passos de normalização:
1. Extração do bloco de ingredientes
2. Separação por vírgulas, ponto-e-vírgula, quebras de linha
3. Remoção de prefixos ("Conservador:", "Corante:", etc.)
4. Expansão de compostos ("água, açúcar e sal" → ["água", "açúcar", "sal"])
5. Sanitização semântica (remove ruído de OCR)
6. Deduplicação por normalização
7. Remoção de fragmentos incompletos
```

#### Filtros Aplicados

**Excluídos da lista de ingredientes**:
- ❌ Instruções de conservação ("conserve em local fresco")
- ❌ Informações nutricionais ("100g", "kcal", "%VD")
- ❌ Fragmentos incompletos ("açúcar de" sem continuação)
- ❌ Claims regulatórios misturados

### 4. Detecção de Alergênicos

#### Alergênicos Detectados

**Lista completa**:
- Glúten (trigo, cevada, centeio, aveia*)
- Lactose / Leite (e derivados)
- Ovo
- Soja
- Amendoim
- Castanhas (caju, pará, nozes, amêndoas)
- Peixe
- Crustáceos (camarão, caranguejo)

**Níveis de risco**:
```typescript
"contains"             // Contém explicitamente
"may_contain"          // Pode conter (claim)
"cross_contamination"  // Fabricado em linha compartilhada
```

#### Fontes de Detecção

1. **Claims explícitos** (prioridade 100)
   - "CONTÉM LEITE"
   - "PODE CONTER TRAÇOS DE CASTANHAS"

2. **Ingredientes detectados** (prioridade 90)
   - "farinha de trigo" → glúten
   - "leite em pó" → lactose

3. **Inferência semântica** (prioridade 50)
   - Categoria do produto sugere alergênico

### 5. Perfis Alimentares

#### Vegano

**Incompatível com**:
- Carne (bovina, suína, frango, peixe)
- Laticínios (leite, queijo, manteiga, iogurte)
- Ovos (e derivados como albumina)
- Mel
- Gelatina (origem animal)
- Corantes de origem animal (cochonilha)

**Status possíveis**:
```typescript
"compatible"                  // Confirmado vegano
"likely_compatible"           // Sem evidência contrária
"uncertain"                   // Dados insuficientes
"likely_not_compatible"       // Inferência negativa
"attention"                   // Risco de traços
"not_compatible"              // Confirmado não-vegano
```

#### Vegetariano

**Incompatível com**:
- Carne (bovina, suína, frango, peixe)
- Gelatina animal

**Compatível com**:
- Laticínios ✅
- Ovos ✅
- Mel ✅

#### Sem Lactose

**Incompatível com**:
- Leite (e derivados)
- Lactose explícita
- Soro de leite (whey)
- Caseína
- Fermento lácteo

**Claim "ZERO LACTOSE"**:
- Se presente → `compatible = true` (prioridade 100)
- Mesmo que contenha "leite", o produto pode ser "zero lactose"

#### Sem Glúten

**Incompatível com**:
- Trigo
- Cevada
- Centeio
- Malte
- Aveia (exceto certificada sem glúten)

**Claim "NÃO CONTÉM GLÚTEN"**:
- Se presente → `compatible = true` (prioridade 100)
- Certificação celíaca

#### Diabético

**Avalia**:
- Quantidade de açúcar
- Tipo de açúcar (refinado, xarope, mel)
- Carboidratos totais
- Índice glicêmico (inferido)
- Adoçantes artificiais

**Critérios**:
```typescript
Alto risco: açúcar >= 10g/100g
Médio risco: açúcar 5-10g/100g
Baixo risco: açúcar < 5g/100g
```

### 6. Classificação de Processamento (NOVA)

#### Níveis

**NOVA 1 - Minimamente Processado** (Score: 90)
- Alimentos naturais ou com processamento mínimo
- Exemplos: arroz, feijão, frutas, vegetais, carnes frescas
- Indicadores: poucos ingredientes, sem aditivos

**NOVA 2 - Ingredientes Culinários** (Score: 60)
- Substâncias extraídas de alimentos naturais
- Exemplos: óleo, manteiga, açúcar, sal
- Uso: preparação culinária

**NOVA 3 - Processados** (Score: 40)
- Alimentos com adição de sal, açúcar ou óleo
- Exemplos: conservas, queijos, pães simples
- Indicadores: conservantes, sal, açúcar

**NOVA 4 - Ultraprocessados** (Score: 20)
- Formulações industriais com 5+ ingredientes
- Exemplos: refrigerantes, salgadinhos, biscoitos recheados
- Indicadores:
  - Aditivos (corantes, aromatizantes, emulsificantes)
  - Gordura hidrogenada
  - Xarope de milho
  - Proteína isolada
  - Glutamato monossódico

#### Regras de Classificação

```csharp
// Regra 1: Indicadores fortes de ultraprocessado
if (HasUltraProcessedIndicators(ingredients)) {
    return ProcessingLevel.UltraProcessed;
}

// Regra 2: Múltiplos indicadores fracos
if (UltraProcessedScore >= 2) {
    return ProcessingLevel.UltraProcessed;
}

// Regra 3: Muitos ingredientes + aditivos
if (ingredients.Count > 5 && HasAdditives) {
    return ProcessingLevel.UltraProcessed;
}

// Regra 4: Processado simples
if (HasSimpleProcessing && NoUltraprocessedIndicators) {
    return ProcessingLevel.Processed;
}

// Regra 5: Natural/minimamente processado
if (ingredients.Count <= 3 && NoAdditives) {
    return ProcessingLevel.MinimallyProcessed;
}
```

**Indicadores de Ultraprocessamento**:
```typescript
// Aditivos
"corante", "conservante", "estabilizante", "emulsificante",
"aromatizante", "realçador de sabor"

// Específicos
"glutamato monossódico", "tartrazina", "carragena",
"gordura hidrogenada", "xarope de milho", "maltodextrina",
"proteína isolada", "aroma artificial"
```

### 7. Detecção de Conflitos

#### Tipos de Conflito

| Tipo | Descrição | Severidade |
|------|-----------|------------|
| `ClaimIngredientMismatch` | Claim contradiz ingrediente | Crítica |
| `ClaimConflict` | Claims contraditórios | Crítica |
| `NutritionIngredientMismatch` | Nutricional vs ingrediente | Moderada |
| `MultiSourceConflict` | Fontes contraditórias | Moderada |
| `InvalidNutritionData` | Dados impossíveis | Crítica |

#### Exemplo de Conflito

```typescript
{
  "type": "ClaimIngredientMismatch",
  "severity": "Critical",
  "description": "Claim 'SEM GLÚTEN' contradiz ingrediente 'farinha de trigo'",
  "evidenceA": {
    "text": "SEM GLÚTEN",
    "priority": 100,
    "source": "regulatory_claim"
  },
  "evidenceB": {
    "text": "farinha de trigo",
    "priority": 90,
    "source": "ingredient_list"
  },
  "requiresManualReview": true
}
```

#### Resolução de Conflitos

```
1. Comparar prioridades
2. Se prioridades iguais → usar confiança
3. Se prioridades diferentes → usar maior prioridade
4. Aplicar impacto na confiança geral
5. Marcar para revisão manual se crítico
```

### 8. Qualidade da Análise

#### Níveis de Qualidade

```typescript
"reliable"      // Análise confiável (OCR claro, sem conflitos)
"partial"       // Análise parcial (OCR médio, alguns dados)
"insufficient"  // Dados insuficientes (OCR ruim)
"inconsistent"  // Conflitos detectados (requer revisão)
```

#### Cálculo de Qualidade

```csharp
if (CriticalConflicts > 0) return AnalysisQuality.Inconsistent;
if (ModerateConflicts > 2) return AnalysisQuality.Partial;
if (OcrConfidence < 0.5) return AnalysisQuality.Insufficient;
return AnalysisQuality.Reliable;
```

### 9. Confiança (Confidence)

#### Cálculo

```typescript
BaseConfidence = 0.8  // 80% base

// Ajustes
+0.15  // Se tem claim regulatório claro
+0.10  // Se OCR de alta qualidade
-0.20  // Se tem conflito moderado
-0.40  // Se tem conflito crítico
-0.15  // Se análise parcial
-0.30  // Se dados insuficientes

FinalConfidence = Clamp(BaseConfidence + Adjustments, 0.0, 1.0)
```

---

## 🔄 FLUXO DE PROCESSAMENTO

### Diagrama de Fluxo

```
[Upload Imagem]
    │
    ▼
[Pré-processamento]
    │
    ├──► [Otimização de imagem]
    ├──► [Detecção de orientação]
    └──► [Ajuste de qualidade]
    │
    ▼
[Camada 1: OCR]
    │
    ├──► [Azure Document Intelligence]
    └──► [Extração estruturada]
    │
    ▼
[Camada 2: Semantic Extraction]
    │
    ├──► [IngredientClassifier.ParseIngredients()]
    ├──► [IngredientClassifier.ExtractClaims()]
    └──► [AllergenDetector.Detect()]
    │
    ▼
[Camada 3: Regulatory Detection]
    │
    ├──► [RegulatoryEngine.DetectClaims()]
    ├──► [Classificação de tipos]
    └──► [Normalização de sujeitos]
    │
    ▼
[Camada 3.5: Conflict Detection]
    │
    ├──► [ConflictEngine.DetectConflicts()]
    ├──► [Resolução por prioridade]
    └──► [Avaliação de qualidade]
    │
    ▼
[Camada 4: Profile Evaluation]
    │
    ├──► [DietProfileEngineV2.EvaluateGlutenFree()]
    ├──► [DietProfileEngineV2.EvaluateLactoseFree()]
    ├──► [DietProfileEngine.EvaluateVegan()]
    ├──► [DietProfileEngine.EvaluateVegetarian()]
    └──► [DietProfileEngine.EvaluateDiabeticFriendly()]
    │
    ▼
[Camada 5: Processing Classification]
    │
    └──► [ProcessingLevelEngine.Classify()]
    │
    ▼
[Camada 6: Presentation]
    │
    ├──► [Geração de SummaryCards]
    ├──► [Geração de QuickInsights]
    └──► [Geração de AssistantSummary]
    │
    ▼
[Response Final]
```

### Tempo de Processamento

| Etapa | Tempo Médio |
|-------|-------------|
| Upload | 0.1s |
| Pré-processamento | 0.3s |
| OCR (Azure) | 2-4s |
| Semantic Extraction | 0.5s |
| Regulatory Detection | 0.2s |
| Conflict Resolution | 0.1s |
| Profile Evaluation | 0.3s |
| Processing Classification | 0.1s |
| Presentation | 0.2s |
| **Total** | **4-6s** |

---

## 📋 PERFIS ALIMENTARES

### Vegano

#### O que é avaliado?

- ✅ Ausência de ingredientes de origem animal
- ✅ Ausência de laticínios
- ✅ Ausência de ovos
- ✅ Ausência de mel
- ✅ Ausência de gelatina animal
- ✅ Verificação de claims regulatórios

#### Ingredientes Bloqueados

```typescript
const veganBlockedTerms = [
  // Carnes
  "carne", "frango", "porco", "boi", "peixe", "pescado",
  
  // Laticínios
  "leite", "queijo", "manteiga", "iogurte", "nata", "creme de leite",
  "soro de leite", "whey", "caseína", "lactose",
  
  // Ovos
  "ovo", "ovos", "albumina", "clara de ovo", "gema",
  
  // Outros
  "mel", "gelatina", "colágeno", "própolis",
  
  // Corantes
  "cochonilha", "carmim"
];
```

#### Exemplo de Response

```typescript
{
  "vegan": {
    "compatible": false,
    "compatibilityStatus": "not_compatible",
    "confidence": "high",
    "reasons": [
      "Contém ingrediente de origem animal: leite em pó"
    ],
    "warnings": [
      "Produto contém laticínios, não adequado para veganos."
    ],
    "evidence": [
      {
        "text": "leite em pó",
        "type": "IngredientDetected",
        "trustLevel": 90,
        "source": "ingredient_list"
      }
    ]
  }
}
```

### Vegetariano

#### O que é avaliado?

- ✅ Ausência de carne (incluindo peixe)
- ✅ Ausência de gelatina animal
- ✅ Permite laticínios ✅
- ✅ Permite ovos ✅
- ✅ Permite mel ✅

#### Ingredientes Bloqueados

```typescript
const vegetarianBlockedTerms = [
  "carne", "frango", "porco", "boi",
  "peixe", "pescado", "bacon", "presunto",
  "gelatina", "colágeno"
];
```

### Sem Lactose

#### O que é avaliado?

- ✅ Ausência de lactose explícita
- ✅ Ausência de leite e derivados
- ✅ Verificação de "ZERO LACTOSE" (prioridade máxima)
- ✅ Risco de contaminação cruzada

#### Casos Especiais

**Claim "ZERO LACTOSE" com "leite"**:
```typescript
// Produto pode ter leite mas ser "zero lactose"
if (hasClaim("ZERO LACTOSE")) {
  return {
    compatible: true,
    confidence: "high",
    reasons: ["Claim regulatório: ZERO LACTOSE"]
  };
}
```

### Sem Glúten

#### O que é avaliado?

- ✅ Ausência de glúten
- ✅ Ausência de trigo, cevada, centeio
- ✅ Verificação de "SEM GLÚTEN" (prioridade máxima)
- ✅ Risco de contaminação cruzada

#### Fontes de Glúten

```typescript
const glutenSources = [
  "trigo", "farinha de trigo",
  "cevada", "centeio",
  "malte", "malte de cevada",
  "aveia" // Exceto se certificada
];
```

### Diabético

#### O que é avaliado?

- ✅ Quantidade de açúcar
- ✅ Tipo de açúcar (refinado, xarope)
- ✅ Carboidratos totais
- ✅ Presença de adoçantes
- ✅ Índice glicêmico estimado

#### Critérios

```typescript
// Alto risco
if (sugar >= 10g per 100g) {
  return {
    compatible: false,
    reason: "Alto teor de açúcar"
  };
}

// Médio risco
if (sugar >= 5g && sugar < 10g) {
  return {
    compatible: false,
    reason: "Teor moderado de açúcar. Consumir com moderação."
  };
}

// Baixo risco
if (sugar < 5g) {
  return {
    compatible: true,
    reason: "Baixo teor de açúcar"
  };
}
```

---

## 🏷️ CLAIMS REGULATÓRIOS

### Tipos de Claims

#### CONTÉM (Absoluto)

**Padrões**:
- "CONTÉM GLÚTEN"
- "CONTÉM LEITE"
- "CONTÉM AMENDOIM"

**Características**:
- ✅ Absoluto (não probabilístico)
- ✅ Prioridade máxima (100)
- ✅ Obrigatório por lei
- ✅ Sempre respeitado

**Response**:
```typescript
{
  "text": "CONTÉM GLÚTEN",
  "type": "contains",
  "isAbsolute": true,
  "confidence": "high",
  "trustLevel": 100,
  "subject": "glúten"
}
```

#### PODE CONTER (Probabilístico)

**Padrões**:
- "PODE CONTER TRAÇOS DE LEITE"
- "PODE CONTER CASTANHAS"

**Características**:
- ❌ Não absoluto (probabilístico)
- ✅ Prioridade máxima (100)
- ✅ Indica risco de contaminação cruzada

**Response**:
```typescript
{
  "text": "PODE CONTER TRAÇOS DE LEITE",
  "type": "may_contain",
  "isAbsolute": false,
  "confidence": "high",
  "trustLevel": 100,
  "subject": "leite"
}
```

#### SEM / ZERO (Absoluto Positivo)

**Padrões**:
- "SEM GLÚTEN"
- "ZERO LACTOSE"
- "NÃO CONTÉM AÇÚCAR"

**Características**:
- ✅ Absoluto (garantia)
- ✅ Prioridade máxima (100)
- ✅ Certificação regulatória

**Response**:
```typescript
{
  "text": "SEM GLÚTEN",
  "type": "free_from",
  "isAbsolute": true,
  "confidence": "high",
  "trustLevel": 100,
  "subject": "glúten"
}
```

#### CONTAMINAÇÃO CRUZADA

**Padrões**:
- "FABRICADO EM EQUIPAMENTO QUE PROCESSA LEITE"
- "FABRICADO EM LINHA QUE PROCESSA AMENDOIM"

**Características**:
- ❌ Não absoluto
- ✅ Alerta de risco
- ✅ Importante para alérgicos

**Response**:
```typescript
{
  "text": "FABRICADO EM EQUIPAMENTO QUE PROCESSA LEITE",
  "type": "cross_contamination",
  "isAbsolute": false,
  "confidence": "high",
  "trustLevel": 100,
  "subject": "leite"
}
```

---

## 🚨 DETECÇÃO DE ALERGÊNICOS

### Alergênicos Monitorados

| Alergênico | Sinônimos | Risco |
|------------|-----------|-------|
| **Glúten** | trigo, cevada, centeio, aveia | Alto |
| **Leite** | lactose, laticínio, whey, caseína | Alto |
| **Ovo** | ovos, albumina, gema, clara | Alto |
| **Soja** | lecitina de soja, proteína de soja | Médio |
| **Amendoim** | pasta de amendoim | Alto |
| **Castanhas** | castanha de caju, castanha do pará, nozes | Alto |
| **Peixe** | pescado | Alto |
| **Crustáceos** | camarão, caranguejo, lagosta | Alto |

### Fontes de Detecção

#### 1. Claims Explícitos (Prioridade 100)

```typescript
// Exemplo
"CONTÉM LEITE E DERIVADOS"

→ AllergenRiskDto {
  name: "leite",
  riskType: "contains",
  confidence: "high",
  severity: "critical",
  evidence: ["Claim regulatório: CONTÉM LEITE E DERIVADOS"]
}
```

#### 2. Ingredientes Detectados (Prioridade 90)

```typescript
// Exemplo
Ingredientes: "farinha de trigo, açúcar, ovos"

→ AllergenRiskDto[] [
  { name: "glúten", riskType: "contains", source: "farinha de trigo" },
  { name: "ovo", riskType: "contains", source: "ovos" }
]
```

#### 3. Inferência Semântica (Prioridade 50)

```typescript
// Exemplo
Categoria: "Biscoito recheado"
OpenAI Vision: "Provavelmente contém leite"

→ AllergenRiskDto {
  name: "leite",
  riskType: "may_contain",
  confidence: "medium",
  severity: "moderate",
  evidence: ["Inferência baseada em categoria e análise visual"]
}
```

### Response de Alergênicos

```typescript
{
  "allergens": [
    {
      "name": "glúten",
      "riskType": "contains",
      "confidence": "high",
      "severity": "critical",
      "evidence": [
        "Claim regulatório: CONTÉM GLÚTEN",
        "Ingrediente detectado: farinha de trigo"
      ],
      "sources": [
        "regulatory_claim",
        "ingredient_list"
      ]
    },
    {
      "name": "leite",
      "riskType": "may_contain",
      "confidence": "high",
      "severity": "moderate",
      "evidence": [
        "Claim regulatório: PODE CONTER TRAÇOS DE LEITE"
      ],
      "sources": [
        "regulatory_claim"
      ]
    }
  ],
  "allergenWarnings": [
    "⚠️ ATENÇÃO: Este produto contém GLÚTEN",
    "⚠️ Risco de contaminação cruzada com LEITE"
  ]
}
```

---

## 🎨 CLASSIFICAÇÃO DE PROCESSAMENTO

### NOVA 1 - Minimamente Processado

**Características**:
- Poucos ingredientes (≤ 3)
- Sem aditivos
- Processamento físico simples

**Exemplos**:
- Arroz
- Feijão
- Frutas secas
- Carnes frescas

**Score**: 90

### NOVA 3 - Processado

**Características**:
- 3-5 ingredientes
- Conservantes simples
- Sal, açúcar, óleo

**Exemplos**:
- Conservas
- Queijos
- Pães simples

**Score**: 40

### NOVA 4 - Ultraprocessado

**Características**:
- 5+ ingredientes
- Múltiplos aditivos
- Ingredientes industriais

**Exemplos**:
- Refrigerantes
- Salgadinhos
- Biscoitos recheados
- Macarrão instantâneo

**Score**: 20

**Indicadores**:
```typescript
[
  "corante artificial",
  "aromatizante",
  "emulsificante",
  "realçador de sabor",
  "glutamato monossódico",
  "gordura hidrogenada",
  "xarope de milho",
  "proteína isolada",
  "maltodextrina"
]
```

### Response de Processamento

```typescript
{
  "processingLevel": "ultra_processed",
  "processingScore": 20,
  "processingDescription": "Alimento ultraprocessado",
  "processingWarning": "Alimentos ultraprocessados devem ser evitados. Preferir alimentos in natura ou minimamente processados.",
  "processingIndicators": [
    "Múltiplos aditivos detectados",
    "Presença de aromatizantes artificiais",
    "Presença de emulsificantes"
  ]
}
```

---

## 💡 CASOS DE USO

### Caso 1: Produto Vegano Certificado

**Cenário**: Biscoito com claim "VEGANO"

**Request**:
```bash
POST /api/food/ingredient-analysis
image: biscoito_vegano.jpg
```

**Response**:
```typescript
{
  "ingredients": ["farinha de trigo", "açúcar", "óleo vegetal", "sal"],
  "claims": [
    {
      "text": "VEGANO",
      "type": "certified",
      "isAbsolute": true,
      "confidence": "high"
    }
  ],
  "profiles": {
    "vegan": {
      "compatible": true,
      "compatibilityStatus": "compatible",
      "confidence": "high",
      "reasons": ["Claim regulatório confirmado: VEGANO"]
    }
  },
  "processingLevel": "processed",
  "processingScore": 40
}
```

### Caso 2: Produto com Conflito

**Cenário**: Rótulo com "SEM GLÚTEN" mas contém "farinha de trigo"

**Request**:
```bash
POST /api/food/ingredient-analysis
image: produto_conflito.jpg
```

**Response**:
```typescript
{
  "ingredients": ["farinha de trigo", "açúcar"],
  "claims": [
    {
      "text": "SEM GLÚTEN",
      "type": "free_from",
      "isAbsolute": true
    }
  ],
  "profiles": {
    "glutenFree": {
      "compatible": false,
      "compatibilityStatus": "inconsistent",
      "confidence": "low",
      "reasons": [
        "Conflito crítico: claim diz 'SEM GLÚTEN' mas ingrediente contém 'farinha de trigo'"
      ],
      "warnings": [
        "⚠️ CRÍTICO: Dados contraditórios detectados",
        "Este produto requer revisão manual"
      ]
    }
  },
  "analysisQuality": "inconsistent",
  "conflictsDetected": [
    {
      "type": "ClaimIngredientMismatch",
      "severity": "Critical",
      "description": "Claim 'SEM GLÚTEN' contradiz ingrediente 'farinha de trigo'",
      "requiresManualReview": true
    }
  ]
}
```

### Caso 3: Produto Ultraprocessado

**Cenário**: Salgadinho com muitos aditivos

**Request**:
```bash
POST /api/food/ingredient-analysis
image: salgadinho.jpg
```

**Response**:
```typescript
{
  "ingredients": [
    "farinha de milho",
    "óleo vegetal",
    "sal",
    "queijo em pó",
    "aromatizante artificial",
    "realçador de sabor glutamato monossódico",
    "corante tartrazina",
    "antiumectante dióxido de silício"
  ],
  "processingLevel": "ultra_processed",
  "processingScore": 20,
  "processingDescription": "Alimento ultraprocessado",
  "processingWarning": "Alimentos ultraprocessados devem ser evitados. Preferir alimentos in natura ou minimamente processados.",
  "quickInsights": [
    {
      "text": "Produto ultraprocessado com múltiplos aditivos",
      "type": "processing",
      "severity": "warning"
    },
    {
      "text": "Contém glutamato monossódico (realçador de sabor)",
      "type": "additive",
      "severity": "warning"
    },
    {
      "text": "Contém corante artificial (tartrazina)",
      "type": "additive",
      "severity": "warning"
    }
  ],
  "summaryCards": [
    {
      "title": "Produto Ultraprocessado",
      "subtitle": "Consumir com moderação",
      "severity": "warning",
      "color": "orange",
      "icon": "warning",
      "actionableMessage": "Este produto contém múltiplos aditivos artificiais. Preferir alimentos naturais."
    }
  ]
}
```

### Caso 4: Produto com Alergênicos

**Cenário**: Chocolate com claim de contaminação cruzada

**Request**:
```bash
POST /api/food/ingredient-analysis
image: chocolate.jpg
```

**Response**:
```typescript
{
  "ingredients": ["cacau", "açúcar", "manteiga de cacau", "leite em pó"],
  "claims": [
    {
      "text": "CONTÉM LEITE",
      "type": "contains",
      "isAbsolute": true
    },
    {
      "text": "PODE CONTER AMENDOIM E CASTANHAS",
      "type": "may_contain",
      "isAbsolute": false
    }
  ],
  "allergens": [
    {
      "name": "leite",
      "riskType": "contains",
      "confidence": "high",
      "severity": "critical",
      "evidence": [
        "Claim regulatório: CONTÉM LEITE",
        "Ingrediente detectado: leite em pó"
      ]
    },
    {
      "name": "amendoim",
      "riskType": "may_contain",
      "confidence": "high",
      "severity": "moderate",
      "evidence": [
        "Claim regulatório: PODE CONTER AMENDOIM"
      ]
    },
    {
      "name": "castanhas",
      "riskType": "may_contain",
      "confidence": "high",
      "severity": "moderate",
      "evidence": [
        "Claim regulatório: PODE CONTER CASTANHAS"
      ]
    }
  ],
  "allergenWarnings": [
    "⚠️ ATENÇÃO: Este produto contém LEITE",
    "⚠️ Risco de contaminação cruzada com AMENDOIM",
    "⚠️ Risco de contaminação cruzada com CASTANHAS"
  ],
  "profiles": {
    "vegan": {
      "compatible": false,
      "compatibilityStatus": "not_compatible",
      "reasons": ["Contém leite em pó"]
    },
    "lactoseFree": {
      "compatible": false,
      "compatibilityStatus": "not_compatible",
      "reasons": ["Claim regulatório: CONTÉM LEITE"]
    }
  }
}
```

---

## 📝 EXEMPLOS PRÁTICOS

### Exemplo 1: Request Básico (JavaScript)

```javascript
async function analyzeFood(imageFile) {
  const formData = new FormData();
  formData.append('image', imageFile);
  
  try {
    const response = await fetch('/api/food/ingredient-analysis', {
      method: 'POST',
      body: formData
    });
    
    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }
    
    const result = await response.json();
    
    console.log('Ingredientes:', result.ingredients);
    console.log('Vegano?', result.profiles.vegan.compatible);
    console.log('Processamento:', result.processingLevel);
    console.log('Score:', result.processingScore);
    
    return result;
    
  } catch (error) {
    console.error('Erro na análise:', error);
    throw error;
  }
}

// Uso
const fileInput = document.getElementById('imageInput');
const result = await analyzeFood(fileInput.files[0]);
```

### Exemplo 2: Request com Análise Detalhada (Python)

```python
import requests

def analyze_food_label(image_path: str) -> dict:
    url = "https://api.labelwise.com/api/food/ingredient-analysis"
    
    with open(image_path, 'rb') as image_file:
        files = {'image': image_file}
        data = {'analysisMode': 'detailed'}
        
        response = requests.post(url, files=files, data=data)
        response.raise_for_status()
        
        return response.json()

# Uso
result = analyze_food_label('rotulo.jpg')

print(f"Ingredientes: {result['ingredients']}")
print(f"Vegano: {result['profiles']['vegan']['compatible']}")
print(f"Processamento: {result['processingLevel']}")
print(f"Qualidade: {result['analysisQuality']}")

# Verificar alergênicos
for allergen in result['allergens']:
    print(f"⚠️ {allergen['name']}: {allergen['riskType']}")
```

### Exemplo 3: Verificação de Perfil Específico (C#)

```csharp
public async Task<bool> IsProductVeganAsync(IFormFile image)
{
    using var client = new HttpClient();
    using var content = new MultipartFormDataContent();
    
    var imageContent = new StreamContent(image.OpenReadStream());
    imageContent.Headers.ContentType = new MediaTypeHeaderValue(image.ContentType);
    content.Add(imageContent, "image", image.FileName);
    
    var response = await client.PostAsync(
        "https://api.labelwise.com/api/food/ingredient-analysis", 
        content
    );
    
    response.EnsureSuccessStatusCode();
    
    var result = await response.Content.ReadFromJsonAsync<IngredientAnalysisResponse>();
    
    return result.Profiles.Vegan.Compatible;
}

// Uso
var isVegan = await IsProductVeganAsync(imageFile);
if (isVegan)
{
    Console.WriteLine("✅ Produto é vegano!");
}
else
{
    Console.WriteLine("❌ Produto não é vegano");
    Console.WriteLine($"Motivo: {result.Profiles.Vegan.Reasons[0]}");
}
```

### Exemplo 4: Detecção de Conflitos (TypeScript)

```typescript
interface AnalysisResult {
  ingredients: string[];
  claims: ClaimDetectionDto[];
  profiles: DietProfilesDto;
  conflictsDetected: AnalysisConflictDto[];
  analysisQuality: string;
}

async function analyzeAndCheckConflicts(image: File): Promise<void> {
  const formData = new FormData();
  formData.append('image', image);
  
  const response = await fetch('/api/food/ingredient-analysis', {
    method: 'POST',
    body: formData
  });
  
  const result: AnalysisResult = await response.json();
  
  // Verificar qualidade
  if (result.analysisQuality === 'inconsistent') {
    console.warn('⚠️ Análise inconsistente - conflitos detectados!');
    
    // Listar conflitos
    result.conflictsDetected.forEach(conflict => {
      console.error(`🚨 ${conflict.description}`);
      
      if (conflict.requiresManualReview) {
        console.error('   → Requer revisão manual');
      }
    });
  }
  
  // Verificar perfis
  Object.entries(result.profiles).forEach(([profile, data]) => {
    console.log(`${profile}: ${data.compatible ? '✅' : '❌'}`);
    
    if (data.warnings.length > 0) {
      console.warn(`  Avisos: ${data.warnings.join(', ')}`);
    }
  });
}
```

---

## ❌ CÓDIGOS DE ERRO

### HTTP Status Codes

| Código | Descrição | Solução |
|--------|-----------|---------|
| 200 | ✅ Sucesso | - |
| 400 | Bad Request | Verificar formato da imagem |
| 401 | Não autorizado | Verificar token de autenticação |
| 413 | Payload muito grande | Imagem > 10MB |
| 415 | Tipo de mídia não suportado | Usar JPEG, PNG ou WEBP |
| 422 | Entidade não processável | OCR falhou completamente |
| 429 | Too Many Requests | Rate limit excedido |
| 500 | Erro interno | Contatar suporte |
| 503 | Serviço indisponível | Tentar novamente |

### Erros de Validação

```typescript
{
  "error": "ValidationError",
  "message": "Imagem inválida",
  "details": {
    "field": "image",
    "issue": "Tamanho da imagem excede 10MB"
  }
}
```

### Erros de OCR

```typescript
{
  "error": "OcrFailure",
  "message": "Não foi possível extrair texto da imagem",
  "details": {
    "reason": "Imagem muito desfocada",
    "suggestions": [
      "Tire uma foto mais próxima do rótulo",
      "Certifique-se de que a imagem está nítida",
      "Evite reflexos e sombras"
    ]
  }
}
```

### Erros de Análise

```typescript
{
  "error": "AnalysisIncomplete",
  "message": "Análise parcial - dados insuficientes",
  "details": {
    "analysisQuality": "insufficient",
    "completeness": 0.3,
    "missingData": [
      "ingredient_list",
      "nutrition_table"
    ]
  }
}
```

---

## 🔒 SEGURANÇA E PRIVACIDADE

### Dados Processados

- ✅ Imagens são processadas em memória
- ✅ Não armazenamos imagens permanentemente
- ✅ Cache temporário (24h) para performance
- ✅ Dados anonimizados

### Compliance

- ✅ LGPD compliant (Brasil)
- ✅ GDPR compliant (Europa)
- ✅ Não compartilhamos dados com terceiros
- ✅ Criptografia em trânsito (TLS 1.3)

---

## 📊 MÉTRICAS E MONITORAMENTO

### SLA

- **Disponibilidade**: 99.9%
- **Tempo de resposta**: < 6s (p95)
- **Taxa de erro**: < 0.5%

### Rate Limits

- **Free tier**: 100 requests/dia
- **Basic**: 1000 requests/dia
- **Pro**: 10,000 requests/dia
- **Enterprise**: Ilimitado

---

## 📞 SUPORTE

### Documentação Adicional

- **API Reference**: `/docs/api`
- **Swagger UI**: `/swagger`
- **Postman Collection**: [Download](link)

### Contato

- **Email**: support@labelwise.com
- **Slack**: [Community Channel](link)
- **GitHub Issues**: [Report Bug](link)

---

**Versão**: 2.0  
**Última Atualização**: Janeiro 2025  
**Responsável**: LabelWise API Team
