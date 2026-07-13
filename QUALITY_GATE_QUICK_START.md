# Quality Gate - Quick Start Guide

## 🚀 Validação Rápida em 5 Minutos

### Passo 1: Build do Projeto
```powershell
dotnet build
# ✅ Deve compilar sem erros
```

### Passo 2: Iniciar API
```powershell
cd LabelWise.Api
dotnet run
# Aguarde: "Now listening on: http://localhost:5000"
```

### Passo 3: Testar Endpoint (outra janela PowerShell)

#### Teste A: Imagem Normal (sem mudanças esperadas)
```powershell
# Usar qualquer imagem de rótulo de boa qualidade
curl -X POST http://localhost:5000/api/pipeline/analyze `
  -F "image=@C:\Users\seu-usuario\Downloads\label.jpg" `
  -H "Content-Type: multipart/form-data"
```

**Verificar no response:**
```json
{
  "analysisResult": {
    "confidenceLevel": "Alto",           // ✅ Se OCR e parsing OK
    "classification": "Safe",             // ✅ Se produto identificado
    "generalScore": 0.75,                 // ✅ Sem penalização
    "summary": "Boa Escolha..."          // ✅ Mensagem positiva
  }
}
```

**Verificar nos logs do console:**
```
🎯 [QUALITY GATE] Aplicando Quality Gate...
   • OCR Quality: High
   • Parsing Completeness: Complete
   • Confidence: Alto → Alto (sem mudança)
   • Classification: Safe → Safe (sem mudança)
```

---

#### Teste B: Simular Imagem Ruim (criar arquivo de teste)

Crie um arquivo de texto simulando OCR ruim:

**Arquivo: `test-bad-ocr.txt`**
```
~~@ Pr0du†0 Al¡m3nt1c10
§§§ 1ngr3d13nt3§ ???
... ??? ### ???
```

Como o endpoint espera imagem, use uma ferramenta de teste como **Postman** ou **Insomnia** para testar.

**Resposta esperada:**
```json
{
  "analysisResult": {
    "productName": "Produto Desconhecido",
    "confidenceLevel": "Baixo",           // ✅ Ajustado pelo Quality Gate
    "classification": "Incomplete",        // ✅ Ajustado pelo Quality Gate
    "generalScore": 0.25,                  // ✅ Penalizado (~70%)
    "personalizedScore": 0.25,
    "summary": "Análise Parcial - Tire nova foto...",
    "shortSummary": "Produto não identificado (25/100)...",
    "alerts": [
      "⚠️ OCR: VeryLow (...) | Parsing: Incomplete (...)"
    ]
  }
}
```

---

### Passo 4: Verificar Logs Detalhados

No console da API, procure:

```
═══════════════════════════════════════════════════════════════════════════
🎯 [QUALITY GATE] Aplicando Quality Gate...
   • OCR Quality: VeryLow
   • Parsing Completeness: Incomplete
   • Confidence: Alto → Baixo
   • Classification: Safe → Incomplete
   • General Score: 0.85 → 0.26
   • Personalized Score: 0.88 → 0.26
═══════════════════════════════════════════════════════════════════════════
```

**✅ Se aparecer esse log, Quality Gate está funcionando!**

---

## 🎯 Pontos-Chave para Validar

### 1. Confidence Coerente
- [ ] Se `productName = "Produto Desconhecido"` → `confidenceLevel != "Alto"`
- [ ] Se OCR extraiu <50% palavras válidas → `confidenceLevel = "Baixo"`
- [ ] Se parsing está incompleto → `confidenceLevel != "Alto"`

### 2. Classification Coerente
- [ ] Se `confidenceLevel = "Baixo"` → `classification != "Safe"`
- [ ] Se produto não identificado → `classification = "Incomplete"`
- [ ] Se há alérgenos + parsing incompleto → `classification` conservadora

### 3. Score Penalizado
- [ ] Se `confidenceLevel = "Baixo"` → score reduzido em ~30-70%
- [ ] Se `confidenceLevel = "Médio"` → score reduzido em ~15-25%

### 4. Summary e ShortSummary Coerentes
- [ ] Se análise incompleta → summary não diz "Boa Escolha"
- [ ] Se produto não identificado → summary pede nova foto
- [ ] `summary` e `shortSummary` são coerentes entre si

### 5. Alerts Adicionados
- [ ] Se quality gate falha → alert com detalhes de OCR e Parsing

---

## 🔍 Verificação Detalhada: Caso Real

### Cenário: Upload de imagem borrada/tremida

**1. Request:**
```http
POST /api/pipeline/analyze
Content-Type: multipart/form-data

image: [arquivo de imagem de baixa qualidade]
```

**2. Logs esperados no console:**
```
🔍 [OCR EXECUTION] Provider Information:
   • Provider Name: Tesseract OCR
   • Processing: label_photo.jpg

✅ [OCR SUCCESS] Extracted 45 characters with 42% confidence

🎯 [QUALITY GATE] Aplicando Quality Gate...
   • OCR Quality: Low
   • Parsing Completeness: Partial
   • Confidence: Alto → Baixo
   • Classification: Safe → Caution
   • General Score: 0.70 → 0.35
   • Personalized Score: 0.72 → 0.36
```

**3. Response esperada:**
```json
{
  "metadata": {
    "pipelineId": "...",
    "totalDurationMs": 2500,
    "ocrStep": {
      "success": true,
      "durationMs": 850
    },
    "parsingStep": {
      "success": true
    },
    "analysisStep": {
      "success": true,
      "additionalData": {
        "qualityGatePassed": false
      }
    }
  },
  "analysisResult": {
    "productName": "Produto Desconhecido",
    "brand": null,
    "confidenceLevel": "Baixo",
    "classification": "Incomplete",
    "generalScore": 0.35,
    "personalizedScore": 0.36,
    "summary": "Análise Parcial - Leitura incompleta. Tire outra foto mais próxima do rótulo nutricional.",
    "shortSummary": "Produto não identificado (36/100). Tire outra foto do rótulo.",
    "alerts": [
      "⚠️ OCR: Low (Leitura com dificuldades...) | Parsing: Partial (Leitura incompleta...)"
    ],
    "recommendations": [],
    "extractedText": "~~@ Pr0du†0 ...",
    "extractedIngredients": [],
    "extractedAllergens": []
  }
}
```

**4. Validação:**
- ✅ `confidenceLevel` = "Baixo" (OCR quality = Low)
- ✅ `classification` = "Incomplete" (produto não identificado)
- ✅ `generalScore` penalizado de 0.70 → 0.35 (~50%)
- ✅ `summary` e `shortSummary` coerentes e não otimistas
- ✅ `alerts` contém informação do quality gate

---

## 📊 Tabela de Referência Rápida

| Cenário | OCR Quality | Parsing | Final Confidence | Final Classification |
|---------|-------------|---------|------------------|---------------------|
| Imagem perfeita | High | Complete | Alto | Safe/Excellent |
| Imagem ok | Medium | Mostly | Médio/Alto | Safe/Moderate |
| Imagem ruim | Low | Partial | Médio/Baixo | Caution/Incomplete |
| Imagem péssima | VeryLow | Incomplete | Baixo | Incomplete |
| Produto não identificado | - | Partial | Médio/Baixo | Incomplete |

---

## 🐛 Troubleshooting Rápido

### ❌ "Quality Gate logs não aparecem"
**Causa:** Quality Gate não está sendo executado.

**Solução:**
1. Verificar que `AnalysisQualityGate` está instanciado no construtor do `ProductAnalysisPipelineOrchestrator`
2. Verificar que `ExecuteAnalysisStepAsync` está chamando `_qualityGate.ApplyQualityGate`

---

### ❌ "Confidence ainda está 'Alto' quando deveria ser 'Baixo'"
**Causa:** Quality Gate não está ajustando o valor.

**Solução:**
1. Adicionar breakpoint em `AnalysisQualityGate.ApplyQualityGate`
2. Verificar valores de `ocrQuality.OverallQuality` e `parsingQuality.OverallCompleteness`
3. Verificar que `analysisResult.ConfidenceLevel = qualityGateResult.AdjustedConfidence` está sendo executado

---

### ❌ "Enum 'Incomplete' não é reconhecido no banco"
**Causa:** Banco não foi atualizado com novos valores do enum.

**Solução:**
```powershell
# Criar nova migration
dotnet ef migrations add AddNewAnalysisClassifications --project LabelWise.Infrastructure --startup-project LabelWise.Api

# Aplicar migration
dotnet ef database update --project LabelWise.Infrastructure --startup-project LabelWise.Api
```

---

### ❌ "Summary ainda diz 'Boa Escolha' quando deveria dizer 'Análise Parcial'"
**Causa:** Summary original não está sendo sobrescrito.

**Solução:**
Verificar que em `ExecuteAnalysisStepAsync`, a linha:
```csharp
analysisResult.Summary = qualityGateResult.AdjustedSummary;
```
está **DEPOIS** do Quality Gate e **ANTES** da persistência.

---

## ✅ Checklist de Validação Completa

### Funcionalidades
- [ ] Quality Gate é executado no pipeline
- [ ] Logs do Quality Gate aparecem no console
- [ ] Confidence é ajustado baseado em OCR e Parsing
- [ ] Classification é ajustado se necessário
- [ ] Score é penalizado quando aplicável
- [ ] Summary é gerado de forma coerente
- [ ] ShortSummary é gerado de forma coerente
- [ ] Alert é adicionado quando quality gate falha

### Cenários de Teste
- [ ] Imagem de alta qualidade → sem mudanças
- [ ] Imagem de baixa qualidade → confidence baixo, score penalizado
- [ ] Produto não identificado → classification = Incomplete
- [ ] Parsing parcial → confidence médio, summary com disclaimer
- [ ] Alérgenos + parsing incompleto → classificação conservadora

### Persistência
- [ ] Valores ajustados são salvos no banco
- [ ] Enums novos funcionam corretamente
- [ ] Alerts incluem mensagem do quality gate

---

## 🎓 Entendendo o Fluxo Completo

```
1. Upload de Imagem
   ↓
2. OCR (Tesseract/Azure)
   → Extrai texto: "~~@ Pr0du†0 ???"
   → Confidence: 0.35
   ↓
3. Parsing
   → ProductName: "Produto Desconhecido"
   → Ingredients: []
   ↓
4. Motor de Análise (Regras)
   → GeneralScore: 0.70
   → ConfidenceLevel: "Alto" (baseado apenas em dados disponíveis)
   → Classification: "Safe"
   ↓
5. 🎯 QUALITY GATE (NOVO!)
   → OcrQuality: VeryLow (muitos caracteres estranhos)
   → ParsingCompleteness: Incomplete (produto não identificado)
   → Ajusta Confidence: "Alto" → "Baixo"
   → Ajusta Classification: "Safe" → "Incomplete"
   → Penaliza Score: 0.70 → 0.21 (-70%)
   → Gera Summary coerente: "Análise Parcial - Tire nova foto..."
   ↓
6. Persistência
   → Salva valores AJUSTADOS no banco
   ↓
7. Response
   → Retorna resultado COERENTE para o usuário
```

---

## 🎉 Sucesso! Quality Gate Funcionando

Se você viu:
- ✅ Logs do Quality Gate no console
- ✅ Confidence ajustado de "Alto" para "Baixo" (quando aplicável)
- ✅ Score penalizado (quando aplicável)
- ✅ Summary coerente com confiança
- ✅ Alert explicando problema de qualidade

**Parabéns! O Quality Gate está funcionando corretamente! 🎊**

---

**Quick Start Version:** 1.0  
**Duração Estimada:** 5-10 minutos  
**Última Atualização:** 2025-01-XX
