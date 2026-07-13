# 🎨 Refinamento da Camada de Apresentação Nutricional

## 📋 Visão Geral

Este documento descreve o refinamento da camada de apresentação da API de análise nutricional, transformando dados técnicos em informações claras, objetivas e acionáveis para o usuário final.

## 🎯 Objetivos Alcançados

### 1. Summary Aprimorado
- ✅ **Direto e objetivo**: Remove frases genéricas
- ✅ **Destaca principal ofensor**: Explicita açúcar, sódio ou gordura alto
- ✅ **Inclui valores**: "Alto teor de açúcar (75g/100g)"
- ✅ **Contexto claro**: Indica se é tabela nutricional ou estimativa

### 2. Score Refinado
- ✅ **Calibração precisa**: Produtos com alto açúcar não ficam acima de 48
- ✅ **Caps por categoria**: Achocolatado max 48, sobremesa láctea max 42
- ✅ **Penalidades por ofensores**: -45 pontos para ofensores críticos (>90 severidade)
- ✅ **Bonificações por aspectos positivos**: +8 pontos para proteína >20g/100g

### 3. Labels User-Friendly
- ✅ **"Excelente escolha"** (80-100): Produtos nutricionalmente adequados
- ✅ **"Boa escolha"** (65-79): Perfil satisfatório
- ✅ **"Consumo com atenção"** (50-64): Atenção ao ofensor, consumo ocasional
- ✅ **"Evitar consumo frequente"** (35-49): Alto teor de ofensor
- ✅ **"Evitar"** (0-34): Teor crítico, não recomendado

### 4. Retorno Pronto para UI
- ✅ **MainOffender explícito**: Nutriente, valor, unidade, severidade
- ✅ **Alertas contextualizados**: Específicos por perfil de saúde
- ✅ **Recomendações acionáveis**: "Não recomendado para diabéticos"
- ✅ **Coerência total**: Score, classificação e summary alinhados

---

## 🏗️ Arquitetura

### `NutritionPresentationEngine`
Motor central que processa análises nutricionais e gera apresentação refinada.

```csharp
var presentation = NutritionPresentationEngine.ProcessForPresentation(analysis);

// Resultado:
presentation.Score         // RefinedNutritionalScore
presentation.Summary       // string - summary refinado
presentation.Alerts        // List<string> - alertas contextualizados
presentation.MainOffender  // NutrientOffender - principal ofensor
```

### Classes de Resultado

#### `RefinedNutritionalScore`
```csharp
{
    "value": 42,
    "label": "Evitar consumo frequente",
    "status": "ruim",
    "color": "#f97316",
    "recommendation": "Alto teor de açúcar. Reservar para ocasiões especiais."
}
```

#### `NutrientOffender`
```csharp
{
    "nutrient": "Açúcar",
    "value": 75.0,
    "unit": "g",
    "severity": 95,
    "impactMessage": "Nível extremamente elevado de açúcar. Pode comprometer seriamente o controle glicêmico."
}
```

---

## 🔬 Detecção do Principal Ofensor

### Limites de Severidade

#### Açúcar
- **> 50g/100g**: Severidade 95 (extremamente alto)
- **> 30g/100g**: Severidade 85 (muito alto)
- **> 20g/100g**: Severidade 70 (alto)
- **> 15g/100g**: Severidade 55 (moderadamente alto)

#### Sódio
- **> 1200mg/100g**: Severidade 90 (extremamente alto)
- **> 900mg/100g**: Severidade 75 (muito alto)
- **> 600mg/100g**: Severidade 60 (alto)

#### Gordura
- **> 35g/100g**: Severidade 85 (extremamente alta)
- **> 25g/100g**: Severidade 70 (muito alta)
- **> 20g/100g**: Severidade 55 (alta)

### Mensagens de Impacto

**Açúcar > 50g/100g:**
> "Nível extremamente elevado de açúcar. Pode comprometer seriamente o controle glicêmico."

**Sódio > 1200mg/100g:**
> "Sódio muito elevado. Não recomendado para hipertensos."

**Gordura > 35g/100g:**
> "Gordura extremamente alta. Alta densidade calórica."

---

## 🧮 Cálculo do Score Refinado

### Fórmula
```
Score Final = Base (100)
              + Impacto Classificação
              + Impacto Ofensor Principal
              + Impacto Nutricional Geral
              + Ajuste por Categoria
              + Bonificações Positivas
```

### Penalidades

#### Por Classificação
- **3+ perfis "não_recomendado"**: -40 pontos
- **2+ perfis "não_recomendado"**: -30 pontos
- **1 perfil "não_recomendado"**: -20 pontos
- **3+ perfis "consumo_moderado"**: -20 pontos

#### Por Ofensor Principal
- **Severidade >= 90**: -45 pontos
- **Severidade >= 80**: -35 pontos
- **Severidade >= 70**: -28 pontos
- **Severidade >= 60**: -20 pontos

#### Por Densidade Calórica
- **> 500 kcal/100g**: -10 pontos
- **> 400 kcal/100g**: -6 pontos
- **> 300 kcal/100g**: -3 pontos

#### Por Categoria
- **Sobremesa/doce**: -8 pontos
- **Ultraprocessado**: -12 pontos
- **Achocolatado + alto açúcar**: -10 pontos extras

### Bonificações

#### Proteína
- **> 20g/100g**: +8 pontos
- **> 15g/100g**: +5 pontos
- **> 10g/100g**: +3 pontos

#### Fibra
- **> 8g/100g**: +6 pontos
- **> 5g/100g**: +4 pontos
- **> 3g/100g**: +2 pontos

#### Baixo Açúcar
- **< 5g/100g**: +5 pontos
- **< 10g/100g**: +3 pontos

#### Baixo Sódio
- **< 150mg/100g**: +5 pontos
- **< 300mg/100g**: +3 pontos

### Caps por Categoria
- **Achocolatado**: max 48
- **Sobremesa láctea**: max 42
- **Biscoito recheado**: max 38
- **Refrigerante**: max 30
- **Salgadinho**: max 35
- **Chocolate**: max 45

### Cap por Ofensor Severo
- **Severidade >= 85**: max 45 (qualquer categoria)

---

## 📝 Geração de Summary Refinado

### Estrutura
```
[Produto] + [Característica Nutricional] + [Contexto de Análise] + [Recomendação]
```

### Exemplos

#### Produto com Alto Açúcar (> 50g)
> "Achocolatado em Pó contém teor extremamente elevado de açúcar (75g/100g). Dados extraídos da tabela nutricional presente no rótulo. Não recomendado para diabéticos ou quem busca controle de peso."

#### Produto com Alto Açúcar (30-50g)
> "Sobremesa Láctea apresenta alto teor de açúcar (35g/100g). Análise baseada em leitura parcial da tabela nutricional (calorias, açúcares e gorduras extraídos)."

#### Produto com Alto Sódio
> "Salgadinho possui teor muito elevado de sódio (1100mg/100g). Dados extraídos da tabela nutricional. Não adequado para hipertensos ou dietas com restrição de sal."

#### Produto Equilibrado
> "Queijo Cottage oferece bom aporte proteico (22g/100g). Dados extraídos da tabela nutricional presente no rótulo."

#### Estimativa por Categoria
> "Arroz Branco Tipo 1. Análise baseada na categoria do produto devido à ausência de tabela nutricional legível."

---

## 🚨 Alertas Contextualizados

### Tipos de Alertas

#### Alerta Principal (Ofensor)
```
⚠️ Alto teor de açúcar (75g/100g). Impacto significativo na glicemia e ganho de peso.
```

#### Alertas por Perfil de Saúde

**Diabéticos:**
```
🚫 Não recomendado para diabéticos devido ao alto teor de açúcar.
⚠️ Diabéticos devem consumir com moderação e monitorar glicemia.
```

**Hipertensos:**
```
🚫 Não adequado para hipertensos devido ao alto teor de sódio.
⚠️ Hipertensos devem limitar o consumo devido ao sódio.
```

**Emagrecimento:**
```
🚫 Não recomendado para emagrecimento: alto teor de açúcar e calorias vazias.
🚫 Não recomendado para emagrecimento: alta densidade calórica.
```

#### Alerta de Qualidade da Análise
```
ℹ️ Valores nutricionais estimados por categoria. Para análise precisa, capture a tabela nutricional.
```

---

## 🎯 Casos de Uso

### Caso 1: Achocolatado com Alto Açúcar

**Entrada:**
```json
{
  "productName": "Achocolatado em Pó",
  "category": "Achocolatado",
  "estimatedNutritionProfile": {
    "caloriesPer100g": 380,
    "estimatedSugarPer100g": 75,
    "estimatedProteinPer100g": 4
  }
}
```

**Saída:**
```json
{
  "nutritionalScore": {
    "value": 38,
    "label": "Evitar consumo frequente",
    "status": "ruim",
    "color": "#f97316",
    "reason": "Açúcar: 75g/100g. Alto teor de açúcar. Reservar para ocasiões especiais."
  },
  "summary": "Achocolatado em Pó contém teor extremamente elevado de açúcar (75g/100g). Dados extraídos da tabela nutricional presente no rótulo. Não recomendado para diabéticos ou quem busca controle de peso.",
  "alerts": [
    "⚠️ Nível extremamente elevado de açúcar. Pode comprometer seriamente o controle glicêmico.",
    "🚫 Não recomendado para diabéticos devido ao alto teor de açúcar.",
    "🚫 Não recomendado para emagrecimento: alto teor de açúcar e calorias vazias."
  ]
}
```

### Caso 2: Queijo Cottage (Alto Proteína)

**Entrada:**
```json
{
  "productName": "Queijo Cottage",
  "category": "Queijo",
  "estimatedNutritionProfile": {
    "caloriesPer100g": 98,
    "estimatedSugarPer100g": 3,
    "estimatedProteinPer100g": 22,
    "estimatedSodiumPer100g": 400
  }
}
```

**Saída:**
```json
{
  "nutritionalScore": {
    "value": 72,
    "label": "Boa escolha",
    "status": "bom",
    "color": "#84cc16",
    "reason": "Produto com perfil nutricional satisfatório."
  },
  "summary": "Queijo Cottage oferece bom aporte proteico (22g/100g). Dados extraídos da tabela nutricional presente no rótulo.",
  "alerts": [
    "⚠️ Hipertensos devem limitar o consumo devido ao sódio."
  ]
}
```

---

## 🧪 Testes

### Executar Testes
```powershell
.\test-nutrition-presentation.ps1
```

### Cobertura de Testes
- ✅ Detecção de ofensor principal (açúcar, sódio, gordura)
- ✅ Cálculo de score com caps por categoria
- ✅ Geração de summary claro e direto
- ✅ Labels user-friendly (não genéricos)
- ✅ Alertas contextualizados por perfil de saúde
- ✅ Bonificações por aspectos positivos (proteína, fibra)
- ✅ Indicação de estimativa vs dados reais

---

## 📊 Comparação Antes vs Depois

### Antes
```json
{
  "nutritionalScore": {
    "value": 52,
    "label": "Moderado"
  },
  "summary": "Produto com perfil intermediário para a categoria."
}
```

### Depois
```json
{
  "nutritionalScore": {
    "value": 38,
    "label": "Evitar consumo frequente",
    "reason": "Açúcar: 75g/100g. Alto teor de açúcar. Reservar para ocasiões especiais."
  },
  "summary": "Achocolatado em Pó contém teor extremamente elevado de açúcar (75g/100g). Dados extraídos da tabela nutricional. Não recomendado para diabéticos.",
  "alerts": [
    "⚠️ Nível extremamente elevado de açúcar. Pode comprometer seriamente o controle glicêmico.",
    "🚫 Não recomendado para diabéticos devido ao alto teor de açúcar."
  ]
}
```

---

## 🚀 Integração

### No Controller
```csharp
using LabelWise.Application.Presentation;

var presentation = NutritionPresentationEngine.ProcessForPresentation(analysis);

return new MobileNutritionAnalysisResponseDto
{
    Summary = presentation.Summary,
    Alerts = presentation.Alerts,
    NutritionalScore = MapRefinedScore(presentation.Score, presentation.MainOffender)
};
```

### Mapeamento do Score
```csharp
private NutritionalScoreDto MapRefinedScore(
    RefinedNutritionalScore score, 
    NutrientOffender? offender)
{
    var reason = score.Recommendation;
    
    if (offender != null)
    {
        reason = $"{offender.Nutrient}: {offender.Value:0.#}{offender.Unit}/100g. {reason}";
    }
    
    return new NutritionalScoreDto
    {
        Value = score.Value,
        Label = score.Label,
        Status = score.Status,
        Color = score.Color,
        Reason = reason
    };
}
```

---

## 📱 UI Mobile - Sugestões

### Card de Score
```
┌─────────────────────────────┐
│   38                        │
│   ⚠️ Evitar consumo frequente │
│                             │
│   Açúcar: 75g/100g          │
│   Reservar para ocasiões    │
│   especiais                 │
└─────────────────────────────┘
```

### Summary
```
Achocolatado em Pó contém teor 
extremamente elevado de açúcar 
(75g/100g). Não recomendado para 
diabéticos.
```

### Alertas
```
⚠️ Nível extremamente elevado de açúcar. 
   Pode comprometer controle glicêmico.

🚫 Não recomendado para diabéticos.
```

---

## ✅ Validação

### Checklist de Qualidade
- [x] Score <= 48 para achocolatados com alto açúcar
- [x] Score <= 42 para sobremesas lácteas doces
- [x] Summary menciona o principal ofensor com valor
- [x] Labels são user-friendly (não genéricos)
- [x] Alertas estão alinhados com os dados reais
- [x] Coerência entre score, classificação e summary
- [x] Bonificações para produtos com alta proteína/fibra
- [x] Penalidades progressivas por severidade do ofensor

---

## 🎓 Princípios de Design

1. **Clareza**: Informação direta, sem jargões técnicos
2. **Objetividade**: Destaque para o principal ponto de atenção
3. **Consistência**: Score, summary e alertas alinhados
4. **Acionabilidade**: Recomendações práticas para o usuário
5. **Contexto**: Sempre indicar fonte dos dados (tabela vs estimativa)

---

## 📚 Referências

- **Código**: `LabelWise.Application\Presentation\NutritionPresentationEngine.cs`
- **Testes**: `LabelWise.Application.Tests\Presentation\NutritionPresentationEngineTests.cs`
- **Controller**: `LabelWise.Api\Controllers\NutritionController.cs`
- **Script**: `test-nutrition-presentation.ps1`
