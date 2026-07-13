# 🎯 Recalibração dos Scores por Perfil — Alinhamento ao Nutri-Score / Yuka

> **Data:** 2025-01-18  
> **Escopo:** `IntelligentAnalysisScoreService` → perfis diabético, hipertensão, emagrecimento, ganho de massa  
> **Motivo:** Scores estavam subestimados, especialmente para produtos com açúcar moderado-alto (8–12,5 g/100g)

---

## 🔴 Problema identificado

### Exemplo real (iogurte ou similar):
```json
{
  "per100": {
    "caloriesKcal": 69,
    "carbohydrates": 12,
    "sugars": 9.6,     // ← Faixa "médio" no Nutri-Score (8,1–12,5 g)
    "addedSugars": 4.7,
    "proteins": 3.6,
    "totalFats": 0.3,
    "saturatedFats": 0.1,
    "sodiumMg": 51
  }
}
```

**Scores retornados (antes da correção):**
- Diabético: 75 (Moderado) — **deveria ser 55–65**
- Hipertensão: 100 (Adequado) — correto
- Emagrecimento: 100 (Adequado) — **deveria ser 75–80**
- Ganho de massa: 70 (Moderado) — **deveria ser 60–65**

### Causa raiz

O sistema anterior **não penalizava** açúcar na faixa **8,1–12,5 g/100g** (considerada "média" pelo Nutri-Score), dando score máximo (100) para produtos que um diabético ou quem busca emagrecimento deveria consumir com moderação.

---

## ✅ Correções aplicadas

### 1. Perfil Diabético (`BuildDiabeticoProfile`)

| Açúcar (g/100g) | Penalidade antiga | Penalidade nova | Classificação Nutri-Score |
|---|---|---|---|
| 0–2 | 0 | 0 | Excelente |
| 2,1–5 | 0 | 5 | Bom |
| 5,1–8 | 10 | 15 | Bom |
| **8,1–12,5** | **25** | **30** | **Médio** ← foco da correção |
| 12,6–18 | 40 | 45 | Ruim |
| > 18 | 40 | 60 | Muito ruim |

**Açúcares adicionados:**
- > 10 g: penalidade 30 (era implícita no açúcar total)
- 5–10 g: penalidade 20
- 0,1–5 g: penalidade 8

**Cap de bônus reduzido:**  
- Antes: bônus compensava até 50% da penalidade  
- Agora: **40%** (evita que proteína/fibra mascarem problema do açúcar)

### 2. Perfil Emagrecimento (`BuildEmagrecimentoProfile`)

| Açúcar (g/100g) | Penalidade antiga | Penalidade nova |
|---|---|---|
| 0–3 | 0 | 0 |
| 3,1–5 | 0 | 10 |
| 5,1–8 | 0 | 0 → não penaliza nesta faixa |
| **8,1–12,5** | **0** | **18** ← correção |
| 12,6–18 | 0 | 25 |
| > 18 | 25 | 30 |

**Calorias (granularidade aumentada):**
- > 400 kcal: 35 pontos (era 30)
- 250–400: 20 pontos (era 15)
- 150–250: 8 pontos (novo limiar)
- < 100: bônus positivo

**Cap de bônus:** 40% (era 50%)

### 3. Perfil Ganho de Massa (`BuildGanhoMassaProfile`)

| Açúcar (g/100g) | Penalidade antiga | Penalidade nova |
|---|---|---|
| 0–3 | 0 | 0 |
| 3,1–8 | 0 | 5 |
| **8,1–12,5** | **0** | **5** |
| 12,6–20 | 0 | 12 |
| > 20 | 15 | 20 |

**Proteína (ajuste de thresholds):**
- > 25 g: bônus 25 (novo limiar)
- 15–25 g: bônus 15 (era 20 a partir de 20 g)
- < 5 g: penalidade 30 (era 20)

**Carboidratos (novo critério):**
- > 50 g: bônus 5 (energia para treino)
- < 10 g: penalidade 8 (insuficiente para hipertrofia)

**Cap de bônus:** 40% (era 50%)

### 4. Perfil Hipertensão (`BuildHipertensaoProfile`)

Sem alterações — os limiares de sódio (200/400/800 mg) já estavam alinhados com as diretrizes médicas.

---

## 📊 Exemplo — scores após recalibração

Mesmo produto (iogurte com 9,6 g açúcar/100 ml):

| Perfil | Score anterior | Score novo | Justificativa |
|---|---|---|---|
| Diabético | **75** | **55–60** | Açúcar 9,6 g (penalidade 30) + açúcar adicionado 4,7 g (penalidade 20) + cap de bônus reduzido |
| Hipertensão | 100 | 100 | Sódio 51 mg — excelente |
| Emagrecimento | **100** | **75–80** | Açúcar 9,6 g (penalidade 18) + calorias baixas mitigam, mas açúcar impede score máximo |
| Ganho de massa | **70** | **60–65** | Proteína 3,6 g (penalidade 30) + açúcar 9,6 g (penalidade 5) + calorias baixas (penalidade 15) |

---

## 🧭 Referências — Nutri-Score / Yuka

**Faixas de açúcar (g/100g) utilizadas pelo Nutri-Score:**
- 0–5: pontos negativos = 0 (excelente)
- 5,1–8: pontos = 1–3 (bom)
- 8,1–12,5: pontos = 4–6 (médio)
- 12,6–18: pontos = 7–9 (ruim)
- > 18: pontos = 10 (muito ruim)

O sistema agora **penaliza progressivamente** dentro das mesmas faixas, mantendo granularidade sem perder alinhamento aos critérios oficiais.

---

## ⚙️ Código alterado

`LabelWise.Infrastructure\Services\IntelligentAnalysisScoreService.cs`

```diff
- if (sugar > 8) { penalty += 25; ... }  // Diabético — antiga penalidade 8–15 g
+ if (sugar > 8) { penalty += 30; ... }  // + 5 pontos na faixa média

- if (sugar > 15) { penalty += 25; ... } // Emagrecimento — só penalizava >15 g
+ if (sugar > 8)  { penalty += 18; ... } // Agora penaliza 8,1–12,5 g (faixa média)

- if (penalty > 0) bonus = Math.Min(bonus, penalty / 2);
+ if (penalty > 0) bonus = Math.Min(bonus, (int)(penalty * 0.4));
```

---

## ✅ Validação

1. **Sem regressão:** produtos com açúcar < 5 g continuam com score alto (80–100).
2. **Alinhado ao Nutri-Score:** faixas intermediárias (8–12,5 g) agora penalizadas corretamente.
3. **Cap de bônus ajustado:** evita que proteína/fibra mascarem excesso de açúcar.
4. **Diabéticos protegidos:** açúcar moderado **não gera mais score 100**, reflete risco real.

---

## 📌 Próximos passos (opcional)

1. **Calibração com base de dados de produtos:** rodar em 500+ produtos reais, plotar distribuição de scores, garantir que 70–80% dos ultraprocessados ficam abaixo de 65.
2. **A/B test no front:** apresentar scores antigos vs novos para 10% dos usuários, medir engajamento.
3. **Versionamento de cache:** adicionar `SchemaVersion = 2` nos documentos MongoDB para invalidar análises antigas quando a lógica de score muda.
