# 📱 Migração do app mobile para o endpoint `nutricao-analise-inteligente`

> **Público-alvo:** desenvolvedor(a) responsável pelo app mobile (.NET 9 / MAUI ou similar) que hoje consome `POST /api/nutrition/analyze-simple-image`.
>
> **Objetivo:** substituir todas as chamadas atuais ao endpoint legado pelo novo endpoint inteligente e adaptar a tela de resultado ao novo contrato `IntelligentAnalysisResponse`, aplicando as regras de UI/UX descritas neste documento.

---

## 1. Endpoint

| Item | Valor |
|---|---|
| Método | `POST` |
| URL | `/api/nutrition/nutricao-analise-inteligente` |
| Autenticação | `[AllowAnonymous]` (mas envie `X-Device-Id` no header sempre que houver) |
| Content-Type | `multipart/form-data` |

### Form fields

| Campo | Tipo | Obrigatório | Observações |
|---|---|---|---|
| `file` | binário | ✅ | JPG/JPEG/PNG/WEBP, mínimo **20 KB**, máximo **10 MB** |
| `deviceId` | `string` | ❌ | Pode vir no body **ou** no header `X-Device-Id` |

### Pré-validação no client (antes de enviar)

1. Bloquear envio de imagens **menores que 20 KB** (compressão excessiva quebra OCR no servidor).
2. Bloquear arquivos com extensão diferente de `.jpg / .jpeg / .png / .webp`.
3. Bloquear arquivos acima de 10 MB.
4. Se possível, enviar a foto **original da câmera** (sem reencode agressivo).

---

## 2. Contrato da resposta — `IntelligentAnalysisResponse`

```jsonc
{
  "success": true,
  "source": "openfoodfacts" | "openai-vision" | "none",
  "message": "string amigável | null",

  "product": {
    "name":     "string | null",
    "brand":    "string | null",
    "barcode":  "string | null",
    "category": "string | null"
  },

  "nutrition": {
    "unit": "g" | "ml",
    "serving": { "amount": 30, "unit": "g", "description": "5 unidades" } | null,
    "asLabel":    { /* NutritionValues */ } | null,
    "per100":     { /* NutritionValues */ } | null,
    "perServing": { /* NutritionValues */ } | null
  },

  "scoreBasis":   "per100g" | "per100ml",
  "displayBasis": "asLabel" | "per100",
  "per100Source": "direct"  | "derived",
  "processingLevel": "in_natura" | "processado" | "ultraprocessado" | "desconhecido" | null,

  "score": {
    "global": 0,                       // 0..100
    "globalLabel": "string",
    "principalOffender": "string | null",
    "resumoRapido":   "string",
    "explicacaoScore":"string",
    "pontoPrincipal": "string",
    "profiles": {
      "diabetico":     { "score": 0, "label": "string", "reasons": ["..."] },
      "hipertensao":   { "score": 0, "label": "string", "reasons": ["..."] },
      "emagrecimento": { "score": 0, "label": "string", "reasons": ["..."] },
      "ganho_massa":   { "score": 0, "label": "string", "reasons": ["..."] }
    },
    "strengths":  ["..."],
    "weaknesses": ["..."]
  } | null,

  "diagnostics": {
    "confidence": 0.85,                // 0..1 | null
    "imageWidth": 1536,
    "imageHeight": 2048,
    "preparedSizeBytes": 696527,
    "barcodeAttempted": true,
    "barcodeFound": false,
    "openFoodFactsHit": false,
    "openAiVisionUsed": true,
    "warnings": ["..."]
  },

  "disclaimer": "Análise informativa baseada no rótulo. Não substitui orientação de profissional de saúde.",

  "imageQuality": {
    "retryRequested": false,
    "reason": "string amigável | null",
    "reasonCode": "ok" | "low_confidence" | "insufficient_fields" | "no_critical_fields",
    "confidenceScore": 0.85,           // 0..1 | null
    "completenessPercent": 100         // 0..100 | null
  }
}
```

`NutritionValues` — todos os campos são `double?` (podem vir `null`):

```
caloriesKcal, carbohydrates, sugars, addedSugars, proteins,
totalFats, saturatedFats, transFats, fiber, sodiumMg
```

---

## 3. Regras de tratamento da resposta no frontend

### REGRA A — Pedido de nova foto (precedência máxima)

Se `imageQuality.retryRequested == true`:

1. **NÃO renderizar** card de score nem tabela nutricional como “resultado final”.
2. Mostrar tela/diálogo amigável com `imageQuality.reason`.
3. Ramificar pelo `imageQuality.reasonCode`:
   - `"low_confidence"` → ícone de luz/foco e CTA **“Tirar nova foto”** (“aproxime mais a câmera e garanta boa iluminação”).
   - `"insufficient_fields"` → instruir **“Enquadre toda a tabela nutricional”**.
   - `"no_critical_fields"` → instruir **“Aproxime mais a câmera da tabela”**.
4. Pode opcionalmente exibir um link discreto **“Ver dados parciais”** que mostra o que foi lido (`nutrition.per100` / `asLabel`), mas **deixar claro que NÃO são confiáveis**.

### REGRA B — Sucesso (`success == true && !retryRequested`)

1. **Cabeçalho do card**
   - `product.name` → se `null`, exibir “Produto não identificado”.
   - `product.brand` se presente.
   - Badge da fonte:
     - `source == "openfoodfacts"` → “Base oficial”.
     - `source == "openai-vision"` → “Lido da foto”.
2. **Score** (`score.global` + `score.globalLabel`)
   - Cores sugeridas: `0–39` vermelho · `40–69` amarelo · `70–100` verde.
   - Mostrar `score.resumoRapido` em destaque.
   - Mostrar `score.explicacaoScore` e `score.pontoPrincipal` no detalhamento.
   - Listar `score.strengths` (✅) e `score.weaknesses` (⚠️).
3. **Perfis de saúde** (`score.profiles`)
   - Cards horizontais com `label` + `score` + `reasons` (top‑3).
4. **Tabela nutricional** — usar `displayBasis`:
   - `"asLabel"` → renderizar `nutrition.asLabel` com cabeçalho “Por porção (`serving.amount serving.unit` — `serving.description`)”.
   - `"per100"`  → renderizar `nutrition.per100` com cabeçalho “Por 100 `nutrition.unit`”.
5. **Toggle opcional** “Ver por 100 `nutrition.unit`” quando `asLabel` **e** `per100` estiverem presentes.

### REGRA C — Aviso de “estimado”

Se `per100Source == "derived"`:

- Exibir **rótulo discreto** próximo aos valores per100:
  *“Valores por 100`unit` estimados a partir da porção declarada.”*
- **Não usar vermelho** — é apenas informativo.

### REGRA D — Campos nulos

1. **Sempre tratar `null` como “não disponível”**, jamais como zero.
2. Em valores `null`, exibir `—` ou ocultar a linha (nunca escrever `0`).
3. Em listas (`strengths`, `weaknesses`, `reasons`), se vazias, ocultar a seção.
4. Se `score == null` mas `success == true`, mostrar a tabela e exibir mensagem **“Score não disponível para este produto”** no lugar do card de score.

### REGRA E — Falha total (`success == false && !retryRequested`)

- `source == "none"` indica que produto não foi identificado por nenhuma das vias.
- Mostrar `message` ao usuário.
- Oferecer ações: **“Tentar de novo”** e (se aplicável) **“Inserir dados manualmente”**.

### REGRA F — Acesso negado (HTTP 403)

- Body virá com:
  ```json
  { "accessDenied": true,
    "reason": "trial_expired" | "access_denied",
    "accessState": { ... } }
  ```
- Tratar separadamente: redirecionar para tela de assinatura (sem tentar parsear nutrição).

### REGRA G — Erro de validação (HTTP 400)

- Body é um `IntelligentAnalysisResponse` com `success = false` e `message` preenchido.
- Mostrar toast com a mensagem e voltar para captura.

### REGRA H — Erro interno (HTTP 500)

- Mensagem genérica: **“Não conseguimos processar a foto agora. Tente novamente.”** + botão de retry.
- `diagnostics.warnings` pode trazer detalhes técnicos para logging interno (não exibir).

### REGRA I — Disclaimer

- Sempre exibir `disclaimer` no rodapé do card de resultado, em fonte menor.

### REGRA J — Categoria e nível de processamento

- Se `processingLevel` presente, exibir como tag (ex.: “Ultraprocessado”).
- Se `product.category` presente, exibir abaixo do nome do produto.

---

## 4. Tabela de mapeamento (endpoint antigo → novo)

| Antigo `UnifiedNutritionAnalysisResponse` | Novo `IntelligentAnalysisResponse` |
|---|---|
| `nutritionPer100g.calories`  | `nutrition.per100.caloriesKcal` |
| `nutritionPer100g.carbs`     | `nutrition.per100.carbohydrates` |
| `nutritionPer100g.protein`   | `nutrition.per100.proteins` |
| `nutritionPer100g.fat`       | `nutrition.per100.totalFats` |
| `nutritionPer100g.sugar`     | `nutrition.per100.sugars` |
| `nutritionPer100g.sodium`    | `nutrition.per100.sodiumMg` |
| `productName`                | `product.name` |
| `brand`                      | `product.brand` |
| `barcode`                    | `product.barcode` |
| `score`                      | `score.global` |
| `confidence`                 | `diagnostics.confidence` ou `imageQuality.confidenceScore` |
| (não existia)                | `imageQuality.retryRequested` ✨ |
| (não existia)                | `displayBasis` / `per100Source` ✨ |
| (não existia)                | `score.profiles` ✨ |

---

## 5. Sequência sugerida de implementação

1. **Criar DTOs** (records imutáveis, `System.Text.Json` com `JsonNamingPolicy.CamelCase`):
   `IntelligentAnalysisResponse`, `ProductIdentification`, `NutritionTableView`,
   `ServingDescriptor`, `NutritionValues`, `ScoreSection`, `ProfileScore`,
   `ImageQualityInfo`, `IntelligentAnalysisDiagnostics`.
2. **Substituir o serviço** `INutritionApiClient.AnalyzeAsync` para apontar ao novo endpoint, mantendo a assinatura, mas agora devolvendo `IntelligentAnalysisResponse`.
3. **Atualizar a ViewModel** da tela de resultado seguindo a precedência:
   1. HTTP 403 → fluxo de paywall.
   2. `imageQuality.retryRequested` → tela de “refazer foto”.
   3. `success == false` → tela de erro com `message`.
   4. `success == true` → tela de resultado completa.
4. **Atualizar a tela**:
   - Header (produto + fonte + categoria + processingLevel).
   - Card de score (global + perfis).
   - Tabela nutricional (regras `displayBasis` e aviso `per100Source`).
   - Pontos fortes/fracos.
   - Disclaimer.
5. **Pré-validação client-side** (regras de tamanho/extensão).
6. **Tratamento de erros HTTP**: 400/403/500 conforme regras E / F / G / H.
7. **Telemetria** (opcional): logar `diagnostics.confidence`, `diagnostics.openAiVisionUsed`, `imageQuality.reasonCode` para acompanhar a qualidade das fotos enviadas.

---

## 6. Exemplo de payload em sucesso (referência visual)

```json
{
  "success": true,
  "source": "openai-vision",
  "product": { "name": null, "brand": null, "barcode": null, "category": null },
  "nutrition": {
    "unit": "g",
    "serving": { "amount": 30, "unit": "g", "description": "5 unidades" },
    "asLabel":    { "caloriesKcal": 158, "carbohydrates": 14, "proteins": 5,  "totalFats": 8,  "saturatedFats": 3,  "fiber": 1, "sodiumMg": 29, "sugars": 0.4 },
    "per100":     { "caloriesKcal": 519, "carbohydrates": 46, "proteins": 16, "totalFats": 27, "saturatedFats": 10, "fiber": 3, "sodiumMg": 95, "sugars": 1.3 },
    "perServing": { "caloriesKcal": 158, "carbohydrates": 14, "proteins": 5,  "totalFats": 8,  "saturatedFats": 3,  "fiber": 1, "sodiumMg": 29, "sugars": 0.4 }
  },
  "scoreBasis":   "per100g",
  "displayBasis": "asLabel",
  "per100Source": "direct",
  "score": {
    "global": 38,
    "globalLabel": "Atenção",
    "principalOffender": "gorduras saturadas",
    "resumoRapido": "Biscoito calórico, com gordura saturada relevante.",
    "profiles": {
      "diabetico":     { "score": 55, "label": "Moderado",  "reasons": ["Carbo alto", "Açúcar baixo"] },
      "hipertensao":   { "score": 70, "label": "Bom",        "reasons": ["Sódio moderado"] },
      "emagrecimento": { "score": 25, "label": "Evitar",     "reasons": ["Densidade calórica alta"] },
      "ganho_massa":   { "score": 60, "label": "Aceitável",  "reasons": ["Boa proteína"] }
    },
    "strengths":  ["Possui fibras"],
    "weaknesses": ["Gordura saturada elevada", "Calorias elevadas por 100g"]
  },
  "imageQuality": {
    "retryRequested": false,
    "reasonCode": "ok",
    "confidenceScore": 1.0,
    "completenessPercent": 100
  }
}
```

---

## 7. Exemplo de payload pedindo nova foto

```json
{
  "success": false,
  "source": "none",
  "message": "A tabela nutricional não ficou legível na foto. Tente aproximar mais a câmera e garantir boa iluminação.",
  "product": { "name": null, "brand": null },
  "nutrition": {
    "unit": "g",
    "serving": null,
    "asLabel":    { "caloriesKcal": null, "carbohydrates": 1.4, "...": "..." },
    "per100":     { "caloriesKcal": null, "carbohydrates": 12,  "...": "..." },
    "perServing": { "caloriesKcal": null, "carbohydrates": 1.4, "...": "..." }
  },
  "score": null,
  "imageQuality": {
    "retryRequested": true,
    "reason": "A tabela nutricional não ficou legível na foto. Tente aproximar mais a câmera e garantir boa iluminação.",
    "reasonCode": "low_confidence",
    "confidenceScore": 0.3,
    "completenessPercent": 75
  }
}
```

> Nesse caso, o app **deve ignorar `nutrition` e `score`** e exibir tela pedindo nova foto, conforme **REGRA A**.

---

## 8. Critérios de aceite (QA)

- [ ] App não envia mais nenhuma chamada ao endpoint antigo `analyze-simple-image`.
- [ ] Foto < 20 KB é bloqueada localmente (não chega ao servidor).
- [ ] Quando `imageQuality.retryRequested == true`, **score nunca é exibido**.
- [ ] Valores nulos aparecem como `—` (nunca como `0`).
- [ ] Quando `per100Source == "derived"`, aparece o aviso “estimado a partir da porção”.
- [ ] Toggle entre `asLabel` e `per100` funciona se ambos existirem.
- [ ] Cards de perfil renderizam corretamente todos os 4 perfis quando presentes.
- [ ] HTTP 403 redireciona para fluxo de assinatura sem tentar parsear nutrição.
- [ ] Disclaimer sempre visível no card de resultado.
- [ ] HTTP 400/500 mostram mensagem amigável e botão de retry.

---

## 9. Notas adicionais para o time mobile

- O servidor já realiza **redimensionamento, auto-orient e reencode** da imagem antes de mandar para a IA — o app **não precisa** otimizar a foto, basta enviar o arquivo original com boa qualidade.
- O endpoint sempre retorna o mesmo contrato, **independente da fonte** (`openfoodfacts` ou `openai-vision`). Não há necessidade de tratar respostas diferentes por fonte.
- O campo `diagnostics` é informativo (debug/telemetria) — **não exibir** para o usuário final.
- Em fotos com tabela obstruída (dedo, brilho), o servidor pode entregar a maior parte dos macros mas anular calorias. **Confiar no servidor**: se um campo veio `null`, ele foi descartado por motivo de segurança nutricional — não tente recalcular no client.
