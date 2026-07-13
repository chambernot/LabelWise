# Motor de Score Nutricional - Documentação Completa

## Visão Geral

O novo motor de score nutricional implementa um sistema baseado em **pesos reais** (escala 0-100) para avaliar produtos alimentícios de forma justa e baseada em evidências científicas.

## Arquitetura

### Componentes Principais

1. **NutritionalScoringEngine** (`LabelWise.Application\Scoring\NutritionalScoringEngine.cs`)
   - Motor principal de cálculo de scores
   - Implementa pesos e regras nutricionais
   - Ajusta scores por perfil do usuário

2. **NutrientScoringRule** (`LabelWise.Application\Rules\NutrientScoringRule.cs`)
   - Regra que aplica o motor de scoring
   - Converte scores 0-100 para 0-1 (compatibilidade)

3. **UltraProcessedProductRule** (`LabelWise.Application\Rules\UltraProcessedProductRule.cs`)
   - Adiciona alertas específicos sobre ultraprocessamento
   - Não modifica scores (já calculados pelo motor)

4. **RulesEngine** (`LabelWise.Application\Rules\RulesEngine.cs`)
   - Orquestra as regras
   - Determina classificação final

5. **RuleBasedSummaryGenerator** (`LabelWise.Application\SummaryGeneration\RuleBasedSummaryGenerator.cs`)
   - Gera resumo com linguagem realista
   - Evita otimismo excessivo

---

## Sistema de Pesos (Total = 100 pontos)

| Categoria | Peso | Justificativa |
|-----------|------|---------------|
| Açúcar | 25% | Maior impacto em obesidade, diabetes e doenças metabólicas |
| Gordura Ruim | 20% | Trans e saturada: riscos cardiovasculares |
| Fibra | 15% | Essencial para saúde digestiva e controle glicêmico |
| Proteína | 10% | Saciedade e manutenção muscular |
| Sódio | 10% | Hipertensão e doenças cardiovasculares |
| Ultraprocessamento | 10% | Correlação com doenças crônicas (NOVA) |
| Aditivos | 10% | Química alimentar e processamento |

---

## Cálculo de Score por Categoria

### 1. Score de Açúcar (0-100)

```
Açúcar (g)    | Score
--------------|-------
≤ 1.0         | 100
≤ 3.0         | 90
≤ 5.0         | 80
≤ 8.0         | 65
≤ 12.0        | 50
≤ 15.0        | 35
≤ 20.0        | 20
> 20.0        | 5
```

**Regra:** Menos açúcar = maior score

### 2. Score de Gordura Ruim (0-100)

```
Base: 100 pontos

Gordura Trans > 0:      -100 (Score = 0)
Gordura Saturada:
  ≥ 10g:                -70
  ≥ 7g:                 -50
  ≥ 5g:                 -35
  ≥ 3g:                 -20
  ≥ 1.5g:               -10

Gordura Hidrogenada:    -50
```

**Regra:** Trans = zero tolerância

### 3. Score de Fibra (0-100)

```
Fibra (g)     | Score
--------------|-------
≥ 10.0        | 100
≥ 7.0         | 90
≥ 5.0         | 75
≥ 3.0         | 55
≥ 1.5         | 35
≥ 0.5         | 20
< 0.5         | 5
```

**Regra:** Mais fibra = maior score

### 4. Score de Proteína (0-100)

```
Proteína (g)  | Score
--------------|-------
≥ 20.0        | 100
≥ 15.0        | 90
≥ 10.0        | 75
≥ 5.0         | 55
≥ 3.0         | 40
≥ 1.0         | 25
< 1.0         | 10
```

**Regra:** Mais proteína = maior score (até limites razoáveis)

### 5. Score de Sódio (0-100)

```
Sódio (mg)    | Score
--------------|-------
≤ 100         | 100
≤ 200         | 85
≤ 300         | 70
≤ 400         | 55
≤ 600         | 40
≤ 800         | 25
≤ 1000        | 15
> 1000        | 5
```

**Regra:** Menos sódio = maior score

### 6. Score de Ultraprocessamento (0-100)

```
Base: 100 pontos

Número de ingredientes:
  > 20:                 -50
  > 15:                 -35
  > 10:                 -20
  > 7:                  -10

Açúcares alto índice glicêmico:
  Por ingrediente:      -15

Combo ruim (açúcar ≥10g + fibra <2g):
                        -25
```

**Regra:** Baseado na classificação NOVA

### 7. Score de Aditivos (0-100)

```
Aditivos      | Score
--------------|-------
0             | 100
1             | 70
2             | 50
3             | 30
≥ 4           | 10
```

**Regra:** Menos aditivos = maior score

---

## Cálculo Final

### Score Geral

```
Score_Geral = (Açúcar × 0.25) + 
              (Gordura_Ruim × 0.20) + 
              (Fibra × 0.15) + 
              (Proteína × 0.10) + 
              (Sódio × 0.10) + 
              (Ultraprocessamento × 0.10) + 
              (Aditivos × 0.10)
```

### Score Personalizado

O score personalizado ajusta o score geral baseado no perfil do usuário:

#### Violações Críticas (Score = 0)

- **Lactose:** Se perfil tem intolerância e produto contém lactose → Score = 0
- **Glúten:** Se perfil requer sem glúten e produto contém glúten → Score = 0
- **Vegano:** Se perfil é vegano e produto contém derivados animais → Score = 0

#### Ajustes por Objetivo

##### WeightLoss (Perda de Peso)
```
Açúcar ≥ 20g:          -25 pontos
Açúcar ≥ 15g:          -18 pontos
Açúcar ≥ 10g:          -12 pontos
Açúcar ≥ 5g:           -6 pontos
Calorias > 400:        -10 pontos
Calorias > 300:        -5 pontos
Fibra < 3g:            -8 pontos
```

##### Diabetes / DiabeticFriendly
```
Açúcar ≥ 15g:          -35 pontos (crítico)
Açúcar ≥ 10g:          -25 pontos
Açúcar ≥ 5g:           -15 pontos
Açúcar ≥ 3g:           -8 pontos
Maltodextrina presente: -20 pontos
```

##### Hypertension / SodiumControl
```
Sódio ≥ 1000mg:        -30 pontos
Sódio ≥ 800mg:         -22 pontos
Sódio ≥ 600mg:         -15 pontos
Sódio ≥ 400mg:         -8 pontos
```

##### LowSugar
```
Açúcar ≥ 15g:          -30 pontos
Açúcar ≥ 10g:          -20 pontos
Açúcar ≥ 5g:           -10 pontos
```

##### HighProtein
```
Proteína ≥ 20g:        +15 pontos
Proteína ≥ 15g:        +10 pontos
Proteína ≥ 10g:        +5 pontos
Proteína < 5g:         -10 pontos
```

##### Ketogenic
```
Carboidratos ≤ 5g:     +15 pontos
Carboidratos ≤ 10g:    +8 pontos
Carboidratos ≤ 15g:    -5 pontos
Carboidratos > 15g:    -20 pontos
Proteína 10-20g:       +5 pontos
```

---

## Classificação Final

Baseada no **menor score** entre geral e personalizado (abordagem conservadora):

| Score | Classificação | Descrição |
|-------|--------------|-----------|
| 80-100 | **Excellent** | Excelente escolha - Consumo regular |
| 60-79 | **Good** | Boa escolha - Consumo regular com moderação |
| 40-59 | **Attention** | Atenção necessária - Consumo esporádico |
| 0-39 | **Avoid** | Não recomendado - Evitar |

---

## Exemplos de Cálculo

### Exemplo 1: Produto Saudável (Iogurte Natural)

**Dados Nutricionais (por 100g):**
- Calorias: 60 kcal
- Açúcar: 4g
- Gordura Saturada: 2g
- Gordura Trans: 0g
- Fibra: 0g
- Proteína: 6g
- Sódio: 50mg
- Ingredientes: 2 (leite, fermento lácteo)
- Aditivos: 0

**Cálculo:**

| Categoria | Score Individual | Peso | Contribuição |
|-----------|-----------------|------|--------------|
| Açúcar (4g) | 80 | 25% | 20.0 |
| Gordura Ruim (2g sat, 0g trans) | 80 | 20% | 16.0 |
| Fibra (0g) | 20 | 15% | 3.0 |
| Proteína (6g) | 55 | 10% | 5.5 |
| Sódio (50mg) | 100 | 10% | 10.0 |
| Ultraprocessamento (2 ingred) | 100 | 10% | 10.0 |
| Aditivos (0) | 100 | 10% | 10.0 |

**Score Final:** 74.5/100 → **Good**

---

### Exemplo 2: Produto Ultraprocessado (Biscoito Recheado)

**Dados Nutricionais (por 100g):**
- Calorias: 480 kcal
- Açúcar: 28g
- Gordura Saturada: 9g
- Gordura Trans: 0.5g
- Fibra: 1g
- Proteína: 4g
- Sódio: 420mg
- Ingredientes: 22 (farinha, açúcar, gordura hidrogenada, xarope de glicose, aromatizantes, corantes, emulsificantes...)
- Aditivos: 5

**Cálculo:**

| Categoria | Score Individual | Peso | Contribuição |
|-----------|-----------------|------|--------------|
| Açúcar (28g) | 5 | 25% | 1.25 |
| Gordura Ruim (9g sat, 0.5g trans, hidrogenada) | 0 | 20% | 0.0 |
| Fibra (1g) | 25 | 15% | 3.75 |
| Proteína (4g) | 40 | 10% | 4.0 |
| Sódio (420mg) | 55 | 10% | 5.5 |
| Ultraprocessamento (22 ingred, combo ruim) | 10 | 10% | 1.0 |
| Aditivos (5) | 10 | 10% | 1.0 |

**Score Final:** 16.5/100 → **Avoid**

**Alertas:**
- 🚨 CONTÉM GORDURA TRANS - Evite este produto!
- 🚨 Contém gordura hidrogenada - associada a riscos cardiovasculares
- 🚨 Teor de açúcar muito elevado: 28g por porção
- 🚨 Produto altamente processado: 5 tipos de aditivos químicos
- 🚨 PRODUTO ULTRAPROCESSADO (Grau 4 - NOVA) - Evitar consumo regular

---

### Exemplo 3: Produto com Ajuste Personalizado (Diabético)

**Dados Nutricionais (por 100g):**
- Açúcar: 12g
- Maltodextrina: Presente
- Fibra: 2g
- Proteína: 5g
- Sódio: 300mg

**Score Geral:** 48/100 → **Attention**

**Ajustes para Perfil Diabético:**
- Açúcar 12g: -25 pontos
- Maltodextrina: -20 pontos

**Score Personalizado:** 3/100 → **Avoid**

**Classificação Final:** **Avoid** (usa o menor score)

**Alertas:**
- 🚨 ATENÇÃO DIABÉTICOS: Este produto contém ingredientes de alto índice glicêmico

---

## Integração com o Pipeline

### Ordem de Execução

1. **NutrientScoringRule** → Calcula scores (geral e personalizado)
2. **UltraProcessedProductRule** → Adiciona alertas contextuais
3. **AllergenAndIngredientRules** → Valida alergênicos
4. **RecommendationsRule** → Gera recomendações
5. **RulesEngine.DetermineClassification()** → Classificação final
6. **RuleBasedSummaryGenerator** → Gera resumo textual

### Exemplo de Uso

```csharp
// Motor de scoring
var scoringEngine = new NutritionalScoringEngine();

// Calcula score geral
double generalScore = scoringEngine.CalculateGeneralScore(nutrition, ingredients);
// Resultado: 0-100

// Calcula score personalizado
double personalizedScore = scoringEngine.CalculatePersonalizedScore(
    nutrition, 
    ingredients, 
    allergens, 
    userProfile
);
// Resultado: 0-100 (com ajustes por perfil)

// Determina classificação
string classification = scoringEngine.DetermineClassification(
    Math.Min(generalScore, personalizedScore)
);
// Resultado: "Excellent", "Good", "Attention", "Avoid"

// Breakdown para debugging
string breakdown = scoringEngine.GenerateScoreBreakdown(nutrition, ingredients, userProfile);
Console.WriteLine(breakdown);
```

**Saída do Breakdown:**
```
Açúcar: 80.0/100 (peso 25%)
Gordura Ruim: 75.0/100 (peso 20%)
Fibra: 55.0/100 (peso 15%)
Proteína: 65.0/100 (peso 10%)
Sódio: 85.0/100 (peso 10%)
Ultraprocessamento: 90.0/100 (peso 10%)
Aditivos: 70.0/100 (peso 10%)
```

---

## Vantagens do Novo Sistema

### 1. **Transparência**
- Pesos claros e justificados
- Cálculo reproduzível
- Breakdown detalhado para debugging

### 2. **Precisão**
- Baseado em evidências científicas
- Limiares nutricionais realistas
- Zero tolerância para ingredientes críticos

### 3. **Personalização**
- Ajustes específicos por objetivo
- Violações críticas (alergênicos)
- Contexto do usuário

### 4. **Linguagem Realista**
- Evita otimismo excessivo
- Alertas claros e diretos
- Classificações compreensíveis

### 5. **Extensibilidade**
- Fácil adicionar novos pesos
- Ajustar limiares sem reescrever código
- Novos perfis e objetivos

---

## Validação e Testes

### Casos de Teste Recomendados

1. **Produto com Gordura Trans**
   - Esperado: Score de gordura = 0, classificação "Avoid"

2. **Produto Ultraprocessado (>20 ingredientes, >5 aditivos)**
   - Esperado: Score < 40, classificação "Avoid"

3. **Produto Saudável (baixo açúcar, alta fibra, alta proteína)**
   - Esperado: Score > 80, classificação "Excellent"

4. **Produto com Alergênico Crítico (Lactose para intolerante)**
   - Esperado: Score personalizado = 0, classificação "Avoid"

5. **Produto Alto Açúcar para Diabético**
   - Esperado: Score personalizado muito menor que geral

---

## Troubleshooting

### Problema: Score muito alto para produto ruim

**Diagnóstico:**
1. Verificar dados nutricionais (valores corretos?)
2. Verificar ingredientes (lista completa?)
3. Verificar breakdown no debug

**Solução:**
- Ajustar limiares nas constantes do motor
- Adicionar penalidades específicas

### Problema: Score muito baixo para produto bom

**Diagnóstico:**
1. Verificar se há dados faltando (fibra, proteína)
2. Verificar perfil do usuário (ajustes personalizados)

**Solução:**
- Completar dados nutricionais
- Revisar pesos das categorias

---

## Roadmap Futuro

### Melhorias Planejadas

1. **Vitaminas e Minerais** (peso 5%)
   - Adicionar análise de micronutrientes

2. **Densidade Nutricional**
   - Ratio de nutrientes por caloria

3. **Score Ambiental** (peso 5%)
   - Pegada de carbono
   - Embalagem sustentável

4. **Machine Learning**
   - Ajuste automático de pesos baseado em feedback

5. **Contexto de Refeição**
   - Café da manhã, almoço, jantar, lanche
   - Ajustar expectativas por contexto

---

## Conclusão

O novo motor de score nutricional fornece uma avaliação **justa**, **transparente** e **personalizável** de produtos alimentícios. Com base em pesos reais e evidências científicas, o sistema corrige o problema de produtos ultraprocessados receberem classificações inadequadas, garantindo que consumidores tomem decisões informadas sobre sua alimentação.

**Score = Saúde Baseada em Evidências**
