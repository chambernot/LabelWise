# Exemplos Práticos - Fallback Nutricional Seguro

## 🎯 Casos de Uso Reais

---

## Exemplo 1: Achocolatado - Foto da FRENTE

### Request
```http
POST /api/nutrition/analyze
Content-Type: multipart/form-data

image: [foto da frente do Nescau]
```

### Response (Dados NÃO Confiáveis)
```json
{
  "success": true,
  "productName": "Achocolatado em Pó",
  "category": "achocolatado em pó",
  "analysisMode": "FrontOfPackageOnly",
  
  "hasReliableNutritionData": false,  // ← NÃO confiável
  "fallbackType": "category_based",
  "inferredRisks": [
    "alto_acucar",
    "ultraprocessado"
  ],
  
  "estimatedNutritionProfile": {
    "caloriesPer100g": null,          // ← null!
    "estimatedSugarPer100g": null,    // ← null!
    "estimatedProteinPer100g": null,  // ← null!
    "estimatedSodiumPer100g": null,   // ← null!
    "estimatedFatPer100g": null,      // ← null!
    "estimatedFiberPer100g": null,    // ← null!
    "basis": "Análise baseada apenas na categoria, sem dados nutricionais exatos da tabela nutricional"
  },
  
  "score": {
    "value": 38,  // ← <= 55
    "label": "moderado",
    "status": "atencao",
    "color": "yellow"
  },
  
  "summary": "Análise baseada apenas na categoria, sem dados nutricionais exatos. Achocolatado em Pó é um produto da categoria achocolatado em pó com possíveis pontos de atenção: alto teor de açúcar e produto ultraprocessado. Para análise precisa, fotografe a tabela nutricional da embalagem.",
  
  "warnings": [
    "Análise baseada apenas na categoria do produto. Para avaliação precisa dos valores nutricionais, fotografe a tabela nutricional da embalagem.",
    "Categoria tipicamente com alto teor de açúcar. Moderação recomendada.",
    "Produto ultraprocessado. Priorize alimentos in natura quando possível."
  ],
  
  "alerts": [
    "Principal ponto de atenção na categoria: açúcar.",
    "Pontuação calculada com baixa confiança, baseada na categoria e painel frontal."
  ]
}
```

### Interpretação
- ❌ **SEM valores numéricos** (correto!)
- ✅ Riscos inferidos: açúcar alto, ultraprocessado
- ✅ Score limitado a 55
- ✅ Summary transparente sobre limitações
- ✅ Warnings orientam a fotografar tabela

---

## Exemplo 2: Achocolatado - Foto da TABELA NUTRICIONAL

### Request
```http
POST /api/nutrition/analyze
Content-Type: multipart/form-data

image: [foto da tabela nutricional do Nescau]
```

### Response (Dados Confiáveis)
```json
{
  "success": true,
  "productName": "Nescau 2.0",
  "category": "achocolatado em pó",
  "analysisMode": "FullNutritionLabel",
  
  "hasReliableNutritionData": true,  // ← Confiável!
  "fallbackType": "real",
  "inferredRisks": [],  // ← Vazio quando há dados reais
  
  "estimatedNutritionProfile": {
    "caloriesPer100g": 396,           // ← Valores presentes!
    "estimatedSugarPer100g": 72,
    "estimatedProteinPer100g": 8.4,
    "estimatedSodiumPer100g": 180,
    "estimatedFatPer100g": 3.2,
    "estimatedFiberPer100g": 5.0,
    "basis": "Extração da tabela nutricional da embalagem"
  },
  
  "score": {
    "value": 38,  // ← Pode ser > 55 quando confiável
    "label": "moderado",
    "status": "atencao",
    "color": "yellow"
  },
  
  "summary": "Nescau 2.0 tem um perfil nutricional intermediário, principalmente por açúcar elevado. Resumo baseado na tabela nutricional da embalagem.",
  
  "warnings": [
    "Teor estimado de açúcar elevado para consumo frequente.",
    "Alta densidade calórica estimada por 100g."
  ],
  
  "classification": {
    "diabetic": {
      "status": "nao_recomendado",
      "reason": "Alto teor de açúcar detectado"
    },
    "bloodPressure": {
      "status": "consumo_moderado",
      "reason": "Sódio dentro da faixa moderada"
    }
  }
}
```

### Interpretação
- ✅ **Valores numéricos presentes** (extraídos da tabela!)
- ✅ `fallbackType = "real"`
- ✅ Score não limitado artificialmente
- ✅ Summary baseado em dados reais
- ✅ Warnings baseados em valores numéricos

---

## Exemplo 3: Salgadinho - Foto da FRENTE

### Request
```http
POST /api/nutrition/analyze
Content-Type: multipart/form-data

image: [foto da frente do Ruffles]
```

### Response
```json
{
  "hasReliableNutritionData": false,
  "fallbackType": "category_based",
  "inferredRisks": [
    "alto_sodio",
    "alta_gordura",
    "ultraprocessado"
  ],
  
  "estimatedNutritionProfile": {
    "caloriesPer100g": null,
    "estimatedSodiumPer100g": null,
    "estimatedFatPer100g": null,
    "basis": "Análise baseada apenas na categoria, sem dados nutricionais exatos da tabela nutricional"
  },
  
  "score": {
    "value": 28  // ← Baixo devido a 3 riscos inferidos
  },
  
  "summary": "Análise baseada apenas na categoria, sem dados nutricionais exatos. Salgadinho é um produto da categoria salgadinho com possíveis pontos de atenção: alto teor de sódio, alta gordura e produto ultraprocessado. Para análise precisa, fotografe a tabela nutricional da embalagem.",
  
  "warnings": [
    "Análise baseada apenas na categoria do produto. Para avaliação precisa dos valores nutricionais, fotografe a tabela nutricional da embalagem.",
    "Categoria tipicamente com alto teor de sódio. Atenção ao consumo recorrente.",
    "Categoria tipicamente com alta gordura. Considere o tamanho da porção.",
    "Produto ultraprocessado. Priorize alimentos in natura quando possível."
  ]
}
```

### Interpretação
- ❌ SEM valores numéricos
- ✅ **3 riscos inferidos** (sódio, gordura, ultraprocessado)
- ✅ Score penalizado: 28 (baseScore 55 - 15 de penalidade por riscos)
- ✅ Warnings específicos por cada risco

---

## Exemplo 4: Arroz Integral - Foto da FRENTE

### Request
```http
POST /api/nutrition/analyze
Content-Type: multipart/form-data

image: [foto da frente do arroz integral]
```

### Response
```json
{
  "hasReliableNutritionData": false,
  "fallbackType": "category_based",
  "inferredRisks": [],  // ← Vazio! Arroz integral não tem riscos óbvios
  
  "estimatedNutritionProfile": {
    "caloriesPer100g": null,
    "estimatedFiberPer100g": null,
    "basis": "Análise baseada apenas na categoria, sem dados nutricionais exatos da tabela nutricional"
  },
  
  "score": {
    "value": 55  // ← Máximo permitido sem dados confiáveis
  },
  
  "summary": "Análise baseada apenas na categoria, sem dados nutricionais exatos. Arroz Integral é um produto da categoria arroz integral. Para análise precisa, fotografe a tabela nutricional da embalagem.",
  
  "warnings": [
    "Análise baseada apenas na categoria do produto. Para avaliação precisa dos valores nutricionais, fotografe a tabela nutricional da embalagem."
  ]
}
```

### Interpretação
- ❌ SEM valores numéricos
- ✅ **Sem riscos inferidos** (categoria saudável)
- ✅ Score no máximo: 55
- ✅ Summary neutro, sem alertas alarmistas

---

## Exemplo 5: Refrigerante - Foto da TABELA com Confiança BAIXA

### Request
```http
POST /api/nutrition/analyze
Content-Type: multipart/form-data

image: [foto embaçada da tabela nutricional]
```

### Response
```json
{
  "hasReliableNutritionData": false,  // ← Confiança < 0.6!
  "fallbackType": "unknown",
  "inferredRisks": [
    "alto_acucar",
    "ultraprocessado"
  ],
  
  "confidenceDetails": {
    "estimatedNutritionProfile": 0.45  // ← < 0.6
  },
  
  "estimatedNutritionProfile": {
    "caloriesPer100g": null,  // ← Valores REMOVIDOS!
    "estimatedSugarPer100g": null,
    "basis": "Análise baseada apenas na categoria, sem dados nutricionais exatos da tabela nutricional"
  },
  
  "score": {
    "value": 18  // ← Baixo
  },
  
  "summary": "Análise baseada apenas na categoria, sem dados nutricionais exatos. Refrigerante é um produto da categoria refrigerante com possíveis pontos de atenção: alto teor de açúcar e produto ultraprocessado. Para análise precisa, fotografe a tabela nutricional da embalagem.",
  
  "warnings": [
    "Análise baseada apenas na categoria do produto. Para avaliação precisa dos valores nutricionais, fotografe a tabela nutricional da embalagem.",
    "Categoria tipicamente com alto teor de açúcar. Moderação recomendada.",
    "Produto ultraprocessado. Priorize alimentos in natura quando possível."
  ]
}
```

### Interpretação
- ⚠️ **Tabela detectada MAS confiança < 0.6**
- ❌ Valores numéricos **REMOVIDOS** por segurança
- ✅ `fallbackType = "unknown"` (não é "real" nem "category_based" puro)
- ✅ Riscos inferidos pela categoria
- ✅ Score baixo devido a riscos

---

## Exemplo 6: Queijo com Glutamato Detectado

### Request
```http
POST /api/nutrition/analyze
Content-Type: multipart/form-data

image: [foto com ingredientes: "... glutamato monossódico ..."]
```

### Response
```json
{
  "hasReliableNutritionData": false,
  "fallbackType": "category_based",
  "inferredRisks": [
    "alto_sodio",
    "aditivos_quimicos"  // ← Detectado!
  ],
  
  "visibleClaims": [
    "glutamato monossódico"
  ],
  
  "estimatedNutritionProfile": {
    "caloriesPer100g": null,
    "estimatedSodiumPer100g": null,
    "basis": "Análise baseada apenas na categoria, sem dados nutricionais exatos da tabela nutricional"
  },
  
  "score": {
    "value": 40  // ← Penalizado por 2 riscos
  },
  
  "summary": "Análise baseada apenas na categoria, sem dados nutricionais exatos. Queijo Ralado é um produto da categoria queijo ralado com possíveis pontos de atenção: alto teor de sódio e presença de aditivos químicos. Para análise precisa, fotografe a tabela nutricional da embalagem.",
  
  "warnings": [
    "Análise baseada apenas na categoria do produto. Para avaliação precisa dos valores nutricionais, fotografe a tabela nutricional da embalagem.",
    "Categoria tipicamente com alto teor de sódio. Atenção ao consumo recorrente.",
    "Possível presença de aditivos químicos detectada nos ingredientes."
  ]
}
```

### Interpretação
- ✅ **Aditivo detectado em visibleClaims**
- ✅ Risco `aditivos_quimicos` adicionado
- ✅ Warning específico sobre aditivos
- ✅ Score penalizado adequadamente

---

## 🧪 Como Validar

### Teste 1: Valores Numéricos
```javascript
// Quando hasReliableNutritionData = false
expect(response.estimatedNutritionProfile.caloriesPer100g).toBeNull();
expect(response.estimatedNutritionProfile.estimatedSugarPer100g).toBeNull();
expect(response.estimatedNutritionProfile.estimatedProteinPer100g).toBeNull();
```

### Teste 2: Riscos Inferidos
```javascript
// Refrigerante deve ter riscos
if (response.category.includes('refrigerante')) {
  expect(response.inferredRisks).toContain('alto_acucar');
  expect(response.inferredRisks).toContain('ultraprocessado');
}
```

### Teste 3: Score Limitado
```javascript
// Quando hasReliableNutritionData = false
if (!response.hasReliableNutritionData) {
  expect(response.score.value).toBeLessThanOrEqual(55);
}
```

### Teste 4: Summary Transparente
```javascript
// Quando hasReliableNutritionData = false
if (!response.hasReliableNutritionData) {
  expect(response.summary).toMatch(/baseada apenas na categoria/i);
  expect(response.summary).toMatch(/fotografe a tabela nutricional/i);
}
```

---

## 📊 Comparação Antes x Depois

### ANTES (Problemático)
```json
{
  "estimatedNutritionProfile": {
    "caloriesPer100g": 380,  // ← INVENTADO!
    "estimatedSugarPer100g": 65,  // ← INVENTADO!
    "basis": "Estimativa por categoria"  // ← Enganoso
  },
  "score": { "value": 72 },  // ← Alto demais sem base real
  "summary": "Produto com perfil nutricional equilibrado"  // ← Falso!
}
```

### DEPOIS (Correto)
```json
{
  "hasReliableNutritionData": false,  // ← Transparente
  "fallbackType": "category_based",
  "inferredRisks": ["alto_acucar", "ultraprocessado"],
  
  "estimatedNutritionProfile": {
    "caloriesPer100g": null,  // ← Honesto!
    "estimatedSugarPer100g": null,
    "basis": "Análise baseada apenas na categoria, sem dados nutricionais exatos"
  },
  
  "score": { "value": 38 },  // ← Limitado a 55
  
  "summary": "Análise baseada apenas na categoria, sem dados nutricionais exatos. Para análise precisa, fotografe a tabela nutricional da embalagem."
}
```

---

## ✅ Checklist de Qualidade

Para CADA resposta, valide:

1. [ ] Se `hasReliableNutritionData = false`, TODOS os valores numéricos são null
2. [ ] Se `hasReliableNutritionData = false`, `score.value <= 55`
3. [ ] Se `hasReliableNutritionData = false`, `inferredRisks` tem riscos (para categorias problemáticas)
4. [ ] Se `hasReliableNutritionData = false`, `summary` menciona limitação
5. [ ] Se `hasReliableNutritionData = true`, pelo menos 1 valor numérico presente
6. [ ] `fallbackType` está correto ("real", "partial", "category_based", "unknown")
7. [ ] Warnings são úteis e não alarmistas

---

**Pronto para produção!** 🚀
