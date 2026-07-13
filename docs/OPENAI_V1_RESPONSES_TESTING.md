# 🧪 Guia de Teste - OpenAI /v1/responses API

## ✅ Checklist de Validação

### 1. Compilação
```bash
dotnet build
```
**✅ Esperado:** Build bem-sucedido (confirmado)

---

### 2. Configuração

**Verificar `appsettings.json`:**
```json
{
  "OpenAiVision": {
    "Endpoint": "https://api.openai.com/v1/responses",
    "ApiKey": "sk-proj-...",
    "Model": "gpt-4.1-mini"
  }
}
```

**✅ Checklist:**
- [ ] Endpoint = `/v1/responses` (não `/chat/completions`)
- [ ] Model = `gpt-4.1-mini` (não `gpt-4o-mini`)
- [ ] ApiKey válida (começa com `sk-`)

---

### 3. Request Body

**Estrutura esperada:**
```json
{
  "model": "gpt-4.1-mini",
  "response_format": { "type": "json_object" },
  "input": [
    {
      "role": "system",
      "content": [
        { "type": "text", "text": "..." }
      ]
    },
    {
      "role": "user",
      "content": [
        { "type": "input_text", "text": "..." },
        { "type": "input_image", "image_base64": "data:image/jpeg;base64,..." }
      ]
    }
  ]
}
```

**✅ Validações:**
- [ ] `response_format` presente
- [ ] `input` ao invés de `messages`
- [ ] `input_text` ao invés de `text`
- [ ] `input_image` ao invés de `image_url`

---

### 4. Response Format

**Estrutura esperada:**
```json
{
  "output": [
    {
      "content": [
        {
          "type": "text",
          "text": "{\"productName\":\"X\",\"nutritionPer100g\":{...}}"
        }
      ]
    }
  ]
}
```

**✅ Parsing:**
```csharp
var output = root.GetProperty("output");
var firstOutput = output[0];
var content = firstOutput.GetProperty("content");
var firstContent = content[0];
var jsonText = firstContent.GetProperty("text").GetString();
```

---

## 🧪 Cenários de Teste

### Teste 1: Produto com Barcode (OpenFoodFacts Priority)

**Input:**
```bash
curl -X POST "https://localhost:32779/api/nutrition/analyze-simple-image" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@produto-com-barcode.jpg" \
  -F "deviceId=test-001"
```

**✅ Esperado:**
- `dataSource: "OPENFOODFACTS"`
- Sem chamada ao OpenAI
- Response em < 2s

---

### Teste 2: Produto sem Barcode (OpenAI /v1/responses)

**Input:**
```bash
curl -X POST "https://localhost:32779/api/nutrition/analyze-simple-image" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@tabela-nutricional.jpg" \
  -F "deviceId=test-001"
```

**✅ Esperado:**
- `dataSource: "OPENAI_VISION"`
- Chamada para `https://api.openai.com/v1/responses`
- JSON limpo (sem markdown)
- Response em 3-5s

**✅ Logs esperados:**
```
[OpenAI] Iniciando análise via /v1/responses API
[OpenAI] Endpoint: https://api.openai.com/v1/responses, Model: gpt-4.1-mini
[OpenAI] Análise concluída — Calorias=120, Proteínas=3, Carbs=25
[Orchestrator] Pipeline concluído — Score=75, Label=Bom
```

---

### Teste 3: JSON com Markdown (Fallback)

**Simular resposta:**
```
```json
{
  "productName": "Teste"
}
```
```

**✅ Esperado:**
- `ExtractCleanJson()` remove markdown
- Parsing bem-sucedido
- Log: sem erros

---

### Teste 4: API Error (401 Unauthorized)

**Simular:** API Key inválida

**✅ Esperado:**
- Log: `[OpenAI] Falha na requisição. Status=401`
- Return `null`
- Pipeline retorna resposta vazia

---

### Teste 5: Timeout (30s)

**Simular:** Desconectar internet

**✅ Esperado:**
- Log: `[OpenAI] Timeout na requisição (30s)`
- Return `null`
- Pipeline retorna resposta vazia

---

## 📊 Validação de Dados

### Produto Exemplo: Biscoito

**Input:** Imagem de tabela nutricional

**✅ Response esperada:**
```json
{
  "success": true,
  "dataSource": "OPENAI_VISION",
  "estimatedNutritionProfile": {
    "caloriesPer100g": 450,
    "estimatedCarbsPer100g": 65,
    "estimatedSugarPer100g": 25,
    "estimatedProteinPer100g": 7,
    "estimatedFatPer100g": 18,
    "estimatedSaturatedFatPer100g": 8,
    "estimatedSodiumPer100g": 350,
    "estimatedFiberPer100g": 2,
    "basis": "OpenAI Vision - extração via /v1/responses"
  },
  "score": {
    "value": 45,
    "label": "Regular",
    "color": "#FFA726"
  }
}
```

**✅ Validações:**
- [ ] Valores numéricos corretos
- [ ] `basis` atualizado
- [ ] `score` calculado corretamente
- [ ] `label` e `color` consistentes

---

## 🐛 Troubleshooting

### ❌ Erro: "output not found"

**Causa:** Response format incorreto (ainda usando `/chat/completions`)

**Solução:**
```json
"Endpoint": "https://api.openai.com/v1/responses"  // ✅ Correto
```

---

### ❌ Erro: "Invalid JSON"

**Causa:** Markdown ainda presente

**Solução:**
Verificar que `ExtractCleanJson()` é chamado:
```csharp
jsonContent = ExtractCleanJson(jsonContent);
```

---

### ❌ Erro: "Model not supported"

**Causa:** Modelo antigo

**Solução:**
```json
"Model": "gpt-4.1-mini"  // ✅ Novo modelo
```

---

### ❌ Erro: "Invalid request"

**Causa:** Request body no formato antigo

**Solução:**
Verificar que usa:
- ✅ `input` (não `messages`)
- ✅ `input_text` (não `text`)
- ✅ `input_image` (não `image_url`)

---

## 📈 Métricas de Sucesso

| Métrica | Alvo | Como Medir |
|---------|------|------------|
| Taxa de sucesso | > 95% | Logs sem erros |
| Tempo de resposta | < 5s | Timestamp nos logs |
| JSON limpo | 100% | Sem markdown na resposta |
| Parsing confiável | 100% | Sem erros de deserialização |

---

## 🚀 Comandos Úteis

### Build
```bash
dotnet build
```

### Run
```bash
cd LabelWise.Api
dotnet run
```

### Logs em tempo real
```bash
dotnet run | grep -E "OpenAI|Orchestrator"
```

### Teste via curl
```bash
curl -X POST "https://localhost:32779/api/nutrition/analyze-simple-image" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@tabela.jpg" \
  -F "deviceId=test-001"
```

---

## ✅ Checklist Final

Antes de considerar DONE:

- [ ] Build sem erros
- [ ] Configuração `/v1/responses` correta
- [ ] Request body no formato novo
- [ ] Response parsing funciona
- [ ] JSON limpo (sem markdown)
- [ ] Pipeline completo executa
- [ ] Logs aparecem corretamente
- [ ] Fallback para OpenFoodFacts funciona
- [ ] Tratamento de erro robusto
- [ ] Documentação atualizada

---

**Status:** ✅ **PRONTO PARA TESTES**

🚀 **Reinicie a aplicação e teste com imagens reais!**
