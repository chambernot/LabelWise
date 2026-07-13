# API `food/ingredient-analysis` — Guia de Consumo Frontend

## Visão Geral

Endpoint responsável pela leitura semântica de ingredientes de rótulos alimentares.

**Retorna:**
- Lista de ingredientes (raw e normalizada)
- Compatibilidade dietética: vegano, vegetariano, sem glúten, sem lactose
- Alertas de alergênicos (contém / pode conter)
- UX badges no estilo Yuka
- Qualidade técnica da leitura

> **Não retorna** score nutricional, calorias, macros ou perfil diabético.
> Esses dados são responsabilidade exclusiva da API `food/nutrition-analysis`.

---

## Endpoint

```
POST /food/ingredient-analysis
Content-Type: multipart/form-data
```

### Autenticação

O endpoint aceita requisições anônimas (`[AllowAnonymous]`).
Para rastreamento de uso, envie o `deviceId` via form field ou header.

### Headers Opcionais

| Header | Tipo | Descrição |
|---|---|---|
| `X-Device-Id` | `string` | ID do dispositivo para controle de acesso e analytics |

### Form Fields

| Campo | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `file` | `File` | ✅ | Foto da embalagem (JPG, PNG, WEBP) |
| `deviceId` | `string` | ❌ | ID do dispositivo (alternativa ao header) |

### Restrições da Imagem

| Regra | Valor |
|---|---|
| Tamanho mínimo | 10 KB |
| Tamanho máximo | 10 MB |
| Formatos aceitos | `.jpg`, `.jpeg`, `.png`, `.webp` |

---

## Resposta de Sucesso — HTTP 200

```json
{
  "product": { ... },
  "ingredients": { ... },
  "compatibility": { ... },
  "alerts": { ... },
  "userExperience": { ... },
  "analysis": { ... }
}
```

---

## Estrutura Completa

### `product`

Informações identificadas do produto.

```json
"product": {
  "name": "Cereal Matinal X",
  "brand": "Marca Y",
  "category": "cereal",
  "processingLevel": "ultra_processed"
}
```

| Campo | Tipo | Valores possíveis |
|---|---|---|
| `name` | `string \| null` | Nome do produto ou `null` se não detectado |
| `brand` | `string \| null` | Marca ou `null` se não detectada |
| `category` | `string` | `cereal`, `dairy`, `sweetener`, `unknown` |
| `processingLevel` | `string` | `ultra_processed`, `processed`, `minimally_processed`, `processed_culinary_ingredient`, `unknown` |

---

### `ingredients`

Lista de ingredientes detectados.

```json
"ingredients": {
  "raw": [
    "farinha de trigo",
    "lecitina de soja",
    "maltodextrina"
  ],
  "normalized": [
    {
      "name": "farinha de trigo",
      "type": "gluten_source",
      "riskLevel": "high",
      "category": "gluten_source"
    },
    {
      "name": "lecitina de soja",
      "type": "emulsifier",
      "riskLevel": "moderate",
      "category": "emulsifier"
    }
  ]
}
```

#### `ingredients.raw`

Lista de strings com os ingredientes identificados na embalagem, já sanitizados (sem anotações científicas/transgênicas).

#### `ingredients.normalized[]`

| Campo | Tipo | Descrição |
|---|---|---|
| `name` | `string` | Nome normalizado do ingrediente |
| `type` | `string` | Categoria semântica |
| `riskLevel` | `string` | `high`, `moderate`, `low`, `unknown` |
| `category` | `string` | Igual a `type` |

**`riskLevel` — Referência:**

| Valor | Quando aplica |
|---|---|
| `high` | Origem animal, glúten, lactose, nozes |
| `moderate` | Aditivos, conservantes, emulsificantes, adoçantes artificiais |
| `low` | Ingrediente comum sem risco relevante |
| `unknown` | Baixa confiança de leitura |

---

### `compatibility`

Compatibilidade do produto com perfis dietéticos.

```json
"compatibility": {
  "vegan": {
    "compatible": false,
    "status": "unsafe",
    "confidence": 92,
    "summary": "Contém leite ou derivados.",
    "reasons": ["Ingrediente 'whey' detectado na lista."],
    "warnings": []
  },
  "vegetarian": {
    "compatible": true,
    "status": "safe",
    "confidence": 85,
    "summary": "Sem carne, peixe ou gelatina identificados.",
    "reasons": [],
    "warnings": []
  },
  "glutenFree": {
    "compatible": false,
    "status": "unsafe",
    "confidence": 95,
    "summary": "Contém farinha de trigo ou derivado com glúten.",
    "reasons": ["farinha de trigo"],
    "warnings": []
  },
  "lactoseFree": {
    "compatible": true,
    "status": "attention",
    "confidence": 70,
    "summary": "Sem lactose identificada, mas há alerta de possível contaminação cruzada com leite.",
    "reasons": [],
    "warnings": ["Pode conter leite."]
  },
  "allergies": {
    "gluten": {
      "safe": false,
      "risk": "contains",
      "severity": "high",
      "source": "ingredient_list"
    },
    "soy": {
      "safe": false,
      "risk": "cross_contamination",
      "severity": "medium",
      "source": "allergen_block"
    }
  }
}
```

#### Perfis: `vegan`, `vegetarian`, `glutenFree`, `lactoseFree`

| Campo | Tipo | Descrição |
|---|---|---|
| `compatible` | `boolean` | `true` se não há incompatibilidade real |
| `status` | `string` | Estado de compatibilidade (ver tabela abaixo) |
| `confidence` | `int` | Confiança da análise (0–100) |
| `summary` | `string` | Resumo legível da conclusão |
| `reasons` | `string[]` | Ingredientes ou evidências que motivaram a decisão |
| `warnings` | `string[]` | Alertas de atenção (ex: contaminação cruzada) |

**`status` — Valores e significado:**

| Valor | `compatible` | Quando usar no UI |
|---|---|---|
| `safe` | `true` | ✅ Verde — Compatível, confiança ≥ 80% |
| `attention` | `true` | ⚠️ Amarelo — Compatível mas com ressalva (cross-contamination, confiança < 80%) |
| `unsafe` | `false` | ❌ Vermelho — Ingrediente incompatível confirmado |
| `unknown` | `false` | ⬜ Cinza — Sem informação suficiente |

> **Regra de negócio:** `compatible = false` ocorre **somente** quando há ingrediente real incompatível detectado.
> Cross-contamination ou baixa confiança geram `status: "attention"` com `compatible: true`.

#### `allergies` — Mapa de alergênicos

Chave: identificador canônico do alergênico.

**Chaves possíveis:**

| Chave | Alergênico |
|---|---|
| `milk` | Leite / derivados lácteos |
| `gluten` | Glúten (trigo, cevada, centeio, malte) |
| `peanut` | Amendoim |
| `tree_nuts` | Castanhas / nozes / amêndoa / avelã |
| `soy` | Soja |
| `egg` | Ovo |
| `fish` | Peixe |
| `crustaceans` | Crustáceos / camarão |

| Campo | Tipo | Descrição |
|---|---|---|
| `safe` | `boolean` | Sempre `false` (aparece apenas quando há risco) |
| `risk` | `string` | `contains` ou `cross_contamination` |
| `severity` | `string` | `high`, `medium`, `low` |
| `source` | `string` | Origem da detecção (ex: `ingredient_list`, `allergen_block`) |

---

### `alerts`

Alertas condensados de alergênicos em linguagem natural.

```json
"alerts": {
  "contains": ["glúten", "soja"],
  "mayContain": ["amendoim", "leite"]
}
```

| Campo | Tipo | Descrição |
|---|---|---|
| `contains` | `string[]` | Alergênicos confirmados na lista de ingredientes |
| `mayContain` | `string[]` | Alergênicos por contaminação cruzada (nunca duplica `contains`) |

> Usar diretamente para exibição. Os valores já estão em português e formatados para o usuário.

---

### `userExperience`

Camada de apresentação no estilo Yuka — pronto para renderizar no app.

```json
"userExperience": {
  "summary": "Produto ultraprocessado contendo glúten, soja e castanhas. Pode conter amendoim.",
  "badges": [
    "⚠ Vegano com atenção",
    "✅ Vegetariano",
    "❌ Incompatível com sem glúten",
    "⚠ Sem lactose com atenção",
    "❌ Contém glúten",
    "❌ Contém soja",
    "❌ Contém castanhas",
    "⚠ Pode conter amendoim",
    "⚠ Ultraprocessado"
  ]
}
```

| Campo | Tipo | Descrição |
|---|---|---|
| `summary` | `string` | Frase curta e humana com os alertas principais |
| `badges` | `string[]` | Badges ordenadas por severidade, prontas para exibição |

**Prefixos de badge:**

| Prefixo | Cor sugerida | Significado |
|---|---|---|
| `✅` | Verde | Compatível |
| `⚠` | Amarelo/laranja | Atenção / pode conter |
| `❌` | Vermelho | Incompatível / contém |

**Ordem dos badges:**
1. Perfis dietéticos (vegano, vegetariano, sem glúten, sem lactose)
2. Alergênicos confirmados (`❌ Contém ...`)
3. Alergênicos por contaminação cruzada (`⚠ Pode conter ...`)
4. Processamento (`⚠ Ultraprocessado`)

---

### `analysis`

Metadados técnicos da qualidade de leitura. Use para adaptar o UI quando a leitura for parcial.

```json
"analysis": {
  "ocrConfidence": 75,
  "ingredientBlockConfidence": 60,
  "partialReading": false,
  "safeMode": false,
  "imageQuality": "fair"
}
```

| Campo | Tipo | Descrição |
|---|---|---|
| `ocrConfidence` | `int` (0–100) | Confiança global do OCR |
| `ingredientBlockConfidence` | `int` (0–100) | Confiança específica no bloco de ingredientes |
| `partialReading` | `boolean` | `true` se a leitura pode estar incompleta |
| `safeMode` | `boolean` | `true` se OCR < 70% — exibir aviso ao usuário |
| `imageQuality` | `string` | `good`, `fair`, `poor`, `unknown` |

**Recomendação de UI por `imageQuality`:**

| Valor | UI sugerido |
|---|---|
| `good` | Exibir resultados normalmente |
| `fair` | Exibir resultados com aviso sutil |
| `poor` | Exibir aviso proeminente + botão "Tirar nova foto" |
| `unknown` | Tratar como `poor` |

---

## Respostas de Erro

### HTTP 400 — Arquivo inválido

```json
{
  "success": false,
  "error": "Imagem muito pequena para leitura de ingredientes. Envie uma foto mais nítida da embalagem."
}
```

**Cenários:**

| Condição | Mensagem |
|---|---|
| Arquivo ausente | `"Arquivo de imagem é obrigatório"` |
| Menor que 10 KB | `"Imagem muito pequena para leitura de ingredientes. Envie uma foto mais nítida da embalagem."` |
| Formato inválido | `"Tipo de arquivo não suportado. Use: .jpg, .jpeg, .png, .webp"` |
| Maior que 10 MB | `"Arquivo muito grande. Tamanho máximo: 10MB"` |

### HTTP 403 — Acesso negado

```json
{
  "success": false,
  "accessDenied": true,
  "reason": "trial_expired",
  "message": "Seu período de teste expirou.",
  "accessState": { ... }
}
```

| Campo `reason` | Quando |
|---|---|
| `trial_expired` | Trial expirado e não é premium |
| `access_denied` | Usuário premium ou trial ativo mas sem permissão |

### HTTP 500 — Erro interno

```json
{
  "success": false,
  "error": "Erro interno ao processar análise de ingredientes"
}
```

---

## Exemplo Completo de Resposta

```json
{
  "product": {
    "name": "Cereal Matinal Integral",
    "brand": null,
    "category": "cereal",
    "processingLevel": "ultra_processed"
  },
  "ingredients": {
    "raw": [
      "farinha de trigo integral",
      "açúcar",
      "lecitina de soja",
      "sal",
      "malte de cevada"
    ],
    "normalized": [
      {
        "name": "farinha de trigo integral",
        "type": "gluten_source",
        "riskLevel": "high",
        "category": "gluten_source"
      },
      {
        "name": "lecitina de soja",
        "type": "emulsifier",
        "riskLevel": "moderate",
        "category": "emulsifier"
      },
      {
        "name": "malte de cevada",
        "type": "gluten_source",
        "riskLevel": "high",
        "category": "gluten_source"
      }
    ]
  },
  "compatibility": {
    "vegan": {
      "compatible": true,
      "status": "safe",
      "confidence": 82,
      "summary": "Nenhum ingrediente de origem animal identificado.",
      "reasons": [],
      "warnings": []
    },
    "vegetarian": {
      "compatible": true,
      "status": "safe",
      "confidence": 82,
      "summary": "Sem carne, peixe ou gelatina identificados.",
      "reasons": [],
      "warnings": []
    },
    "glutenFree": {
      "compatible": false,
      "status": "unsafe",
      "confidence": 95,
      "summary": "Contém farinha de trigo ou derivado com glúten.",
      "reasons": ["farinha de trigo integral", "malte de cevada"],
      "warnings": []
    },
    "lactoseFree": {
      "compatible": true,
      "status": "safe",
      "confidence": 80,
      "summary": "Sem ingredientes lácteos identificados.",
      "reasons": [],
      "warnings": []
    },
    "allergies": {
      "gluten": {
        "safe": false,
        "risk": "contains",
        "severity": "high",
        "source": "ingredient_list"
      },
      "soy": {
        "safe": false,
        "risk": "contains",
        "severity": "medium",
        "source": "ingredient_list"
      }
    }
  },
  "alerts": {
    "contains": ["glúten", "soja"],
    "mayContain": ["amendoim", "leite"]
  },
  "userExperience": {
    "summary": "Produto ultraprocessado contendo glúten e soja. Pode conter amendoim e leite.",
    "badges": [
      "✅ Vegano",
      "✅ Vegetariano",
      "❌ Incompatível com sem glúten",
      "✅ Sem lactose",
      "❌ Contém glúten",
      "❌ Contém soja",
      "⚠ Pode conter amendoim",
      "⚠ Pode conter leite",
      "⚠ Ultraprocessado"
    ]
  },
  "analysis": {
    "ocrConfidence": 88,
    "ingredientBlockConfidence": 90,
    "partialReading": false,
    "safeMode": false,
    "imageQuality": "good"
  }
}
```

---

## Guia de Implementação — Flutter / React Native

### 1. Exibir resultado imediatamente ao receber a resposta

Use `userExperience.badges` como a primeira camada visual — são prontos para renderizar.

### 2. Lógica de cores por badge

```dart
Color badgeColor(String badge) {
  if (badge.startsWith('✅')) return Colors.green;
  if (badge.startsWith('⚠'))  return Colors.orange;
  if (badge.startsWith('❌')) return Colors.red;
  return Colors.grey;
}
```

### 3. Tratar leitura parcial

```dart
if (response.analysis.safeMode || response.analysis.imageQuality == 'poor') {
  showRetryBanner("Foto com baixa qualidade. Tente novamente com melhor iluminação.");
}
```

### 4. Compatibilidade por perfil do usuário

```dart
// Exemplo: usuário é vegano
final profile = response.compatibility.vegan;

switch (profile.status) {
  case 'safe':
    showGreenBadge('✅ Adequado para veganos');
    break;
  case 'attention':
    showYellowBadge('⚠ Vegano com atenção');
    showWarningDetail(profile.warnings.join(', '));
    break;
  case 'unsafe':
    showRedBadge('❌ Não adequado para veganos');
    showBlockingAlert(profile.summary);
    break;
  case 'unknown':
    showGreyBadge('Sem informação suficiente');
    break;
}
```

### 5. Alertas de alergênicos

```dart
// Alerta forte
for (final allergen in response.alerts.contains) {
  showAllergenChip(allergen, color: Colors.red, label: 'Contém');
}

// Alerta de atenção
for (final allergen in response.alerts.mayContain) {
  showAllergenChip(allergen, color: Colors.orange, label: 'Pode conter');
}
```

### 6. Lista de ingredientes

Use `ingredients.normalized` para exibição com cores de risco:

```dart
Color riskColor(String riskLevel) => switch (riskLevel) {
  'high'     => Colors.red,
  'moderate' => Colors.orange,
  'low'      => Colors.green,
  _          => Colors.grey,
};
```

---

## Campos que o Frontend NÃO deve usar para lógica

Estes campos são internos ou reservados para o endpoint de debug:

| Campo | Motivo |
|---|---|
| `analysis.ocrConfidence` | Apenas para UI de qualidade de foto, não para lógica de negócio |
| `alerts.contains` / `mayContain` | Já estão representados em `compatibility.allergies` — evite duplicar |
| Quaisquer campos não documentados | Podem mudar sem aviso |

---

## Endpoint de Debug

Para uso em desenvolvimento, existe o endpoint:

```
POST /food/ingredient-analysis-debug
Content-Type: multipart/form-data
```

Retorna campos internos como:
- `ocrText` — texto bruto extraído por OCR
- `structuredBlocks` — blocos semânticos do rótulo
- `rawIngredients` — ingredientes antes da sanitização mobile
- `normalizedIngredients` — ingredientes com metadados internos
- `claims` — claims regulatórios detectados
- `allergenRisks` — detalhes internos de alergênicos
- `diagnostics` — métricas internas de confiança

> **Não usar em produção.** Exclusivo para diagnóstico e validação.

---

## Changelog da API

| Versão | Data | Mudança |
|---|---|---|
| `2.0` | 2025-07 | Adicionado `alerts`, `userExperience` e `summary` por perfil |
| `2.1` | 2025-07 | Badge Priority Engine, sanitização de termos científicos/transgênicos |
| `2.2` | 2025-07 | Confidence-driven status downgrade, remoção do perfil `diabetic` |
| `2.3` | 2025-07 | Allergen display labels canônicos (glúten, soja, leite, etc.) |
