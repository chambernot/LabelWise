# 🧪 TESTES DO SISTEMA DE SCORING NUTRICIONAL V2

## ✅ Sistema Implementado com Sucesso

- **Arquivo:** `LabelWise.Infrastructure/Services/NutritionScoringServiceV2.cs`
- **Registro DI:** `ServiceCollectionExtensions.cs` (linha 252)
- **Status:** ✅ Compilado e pronto para uso

---

## 📊 CASOS DE TESTE PARA VALIDAÇÃO MANUAL

### **TESTE 1: SÓDIO CRÍTICO (3084mg/100g) - SEU CASO REAL** ⚠️

**Entrada:**
```json
{
  "calories": 105,
  "carbohydrates": 19,
  "sugars": 0,
  "proteins": 6.5,
  "totalFats": 0,
  "saturatedFats": 0,
  "sodiumMg": 3084,
  "fiber": 2
}
```

**Resultado Esperado:**
- **Score:** 0-15
- **Label:** "Muito ruim"
- **Color:** "#dc3545" (vermelho)
- **Principal Offender:** "sódio"
- **Warnings:** Deve conter "⚠️ SÓDIO MUITO ALTO (3084mg/100g = 154% da recomendação diária). EVITAR."

**Como Testar:**
1. Envie a imagem do produto com 3084mg de sódio
2. Verifique o campo `score` no JSON de resposta
3. Confirme que o score está entre 0-15 (não 49-100!)

---

### **TESTE 2: SÓDIO ALTO (1500mg/100g) - EVITAR**

**Entrada:**
```json
{
  "calories": 200,
  "carbohydrates": 30,
  "sugars": 5,
  "proteins": 10,
  "totalFats": 5,
  "saturatedFats": 1,
  "sodiumMg": 1500,
  "fiber": 3
}
```

**Resultado Esperado:**
- **Score:** 20-40
- **Label:** "Evitar" ou "Atenção"
- **Principal Offender:** "sódio"
- **Warnings:** Deve conter referência ao alto teor de sódio

---

### **TESTE 3: PRODUTO SAUDÁVEL - EXCELENTE** ✅

**Entrada:**
```json
{
  "calories": 120,
  "carbohydrates": 15,
  "sugars": 2,
  "proteins": 20,
  "totalFats": 3,
  "saturatedFats": 0.5,
  "sodiumMg": 100,
  "fiber": 6
}
```

**Resultado Esperado:**
- **Score:** 80-100
- **Label:** "Excelente"
- **Color:** "#28a745" (verde escuro)
- **Highlights:** Deve conter "Boa fonte de proteína", "Boa fonte de fibra", "Baixo teor de sódio"
- **Warnings:** []

---

### **TESTE 4: AÇÚCAR CRÍTICO (50g/100g) - MUITO RUIM**

**Entrada:**
```json
{
  "calories": 400,
  "carbohydrates": 80,
  "sugars": 50,
  "addedSugars": 45,
  "proteins": 2,
  "totalFats": 2,
  "saturatedFats": 1,
  "sodiumMg": 200,
  "fiber": 1
}
```

**Resultado Esperado:**
- **Score:** 0-20
- **Label:** "Muito ruim"
- **Principal Offender:** "açúcar"
- **Warnings:** Deve conter "⚠️ AÇÚCAR MUITO ALTO (50.0g/100g). EVITAR."

---

### **TESTE 5: GORDURA SATURADA CRÍTICA (40g/100g)**

**Entrada:**
```json
{
  "calories": 600,
  "carbohydrates": 10,
  "sugars": 2,
  "proteins": 5,
  "totalFats": 50,
  "saturatedFats": 40,
  "sodiumMg": 200,
  "fiber": 1
}
```

**Resultado Esperado:**
- **Score:** 0-30
- **Label:** "Muito ruim" ou "Evitar"
- **Principal Offender:** "gordura saturada"
- **Warnings:** Deve conter "⚠️ GORDURA SATURADA MUITO ALTA (40.0g/100g). EVITAR."

---

### **TESTE 6: PROTEÍNA BAIXA (2g/100g) - GANHO DE MASSA**

**Entrada:**
```json
{
  "calories": 150,
  "carbohydrates": 25,
  "sugars": 3,
  "proteins": 2,
  "totalFats": 3,
  "saturatedFats": 0.5,
  "sodiumMg": 150,
  "fiber": 5
}
```

**Resultado Esperado:**
- **Highlights:** NÃO deve conter "Boa fonte de proteína"
- **Warnings:** Deve conter "Baixo teor de proteína. Complementar com outras fontes."

---

## 🎯 VALIDAÇÃO DOS THRESHOLDS

### **SÓDIO (mg/100g):**
| Valor | Score Esperado | Label |
|-------|----------------|-------|
| 50 | ~95 | Excelente |
| 200 | ~85 | Excelente |
| 500 | ~70 | Bom |
| 800 | ~50 | Atenção |
| 1500 | ~30 | Evitar |
| 3000 | ~10 | Muito ruim |
| 3084 | ~5-10 | Muito ruim |

### **AÇÚCAR (g/100g):**
| Valor | Score Esperado | Label |
|-------|----------------|-------|
| 2 | ~90+ | Excelente |
| 7 | ~70 | Bom |
| 12 | ~50 | Atenção |
| 20 | ~30 | Evitar |
| 50 | ~5 | Muito ruim |

### **GORDURA SATURADA (g/100g):**
| Valor | Score Esperado | Label |
|-------|----------------|-------|
| 0.5 | ~95 | Excelente |
| 2 | ~80 | Bom |
| 5 | ~60 | Atenção |
| 10 | ~30 | Evitar |
| 40 | ~5 | Muito ruim |

---

## 📝 COMO EXECUTAR OS TESTES

### **1. Teste via API (Recomendado)**

```bash
# Envie uma imagem real
curl -X POST http://localhost:5000/api/nutrition/analyze \
  -F "image=@produto_3084mg_sodio.jpg" \
  -F "mode=intelligent"
```

### **2. Teste via Swagger**

1. Acesse `http://localhost:5000/swagger`
2. Encontre o endpoint `/api/nutrition/analyze`
3. Faça upload da imagem
4. Verifique o JSON de resposta:

```json
{
  "score": {
    "global": 8,
    "globalLabel": "Muito ruim",
    "principalOffender": "sódio",
    "warnings": [
      "⚠️ SÓDIO MUITO ALTO (3084mg/100g = 154% da recomendação diária). EVITAR."
    ]
  }
}
```

### **3. Teste Unitário (Se tiver projeto de testes)**

Execute o arquivo `LabelWise.Tests/Services/NutritionScoringServiceV2Tests.cs`:

```bash
dotnet test --filter "FullyQualifiedName~NutritionScoringServiceV2Tests"
```

---

## 🔍 VERIFICAÇÃO DE LOGS

Após testar, verifique os logs da aplicação:

```
[ScoringV2] Iniciando cálculo — Calorias=105, Prot=6.5g, Carbs=19g, Açúcar=0g, Gordura=0g, GordSat=0g, Sódio=3084mg, Fibra=2g
[ScoringV2] Sódio: 3084mg → Penalidade: -90
[ScoringV2] Açúcar: 0g → Penalidade: -0
[ScoringV2] Gordura saturada: 0g → Penalidade: -0
[ScoringV2] Calorias: 105kcal, Gordura: 0g → Penalidade: -0
[ScoringV2] Proteína: 6.5g → Bônus: +3
[ScoringV2] Fibra: 2g → Bônus: +2
[ScoringV2] ✅ Score final: 15/100
```

---

## ✅ CHECKLIST DE VALIDAÇÃO

- [ ] **TESTE 1 PASSOU:** Produto com 3084mg sódio recebe score < 15
- [ ] **TESTE 2 PASSOU:** Produto com 1500mg sódio recebe score 20-40
- [ ] **TESTE 3 PASSOU:** Produto saudável recebe score 80-100
- [ ] **TESTE 4 PASSOU:** Produto com açúcar crítico recebe score < 20
- [ ] **TESTE 5 PASSOU:** Produto com gordura saturada crítica recebe score < 30
- [ ] **TESTE 6 PASSOU:** Produto com proteína baixa NÃO recebe bônus
- [ ] **LOGS CORRETOS:** Logs mostram penalidades e bônus aplicados
- [ ] **WARNINGS CORRETOS:** Mensagens de alerta apropriadas aparecem

---

## 🚀 PRÓXIMOS PASSOS

1. **Execute os testes acima** com produtos reais
2. **Ajuste thresholds** se necessário (baseado em feedback)
3. **Documente** casos extremos encontrados
4. **Monitore** scores em produção

---

## 📚 REFERÊNCIAS

- **OMS:** https://www.who.int/news-room/fact-sheets/detail/salt-reduction
- **ANVISA:** RDC 429/2020 (Rotulagem Frontal)
- **Nutri-Score:** https://www.santepubliquefrance.fr/nutri-score

---

**Data de Implementação:** 2025-01-XX  
**Versão:** 2.0.0  
**Autor:** Sistema de IA LabelWise
