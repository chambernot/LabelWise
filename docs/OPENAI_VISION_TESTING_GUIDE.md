# 🧪 Guia de Teste - OpenAI Vision Integration (Regular API)

## 📋 Checklist de Validação

### 1. ✅ Compilação
```bash
dotnet build
```
**Esperado:** Build bem-sucedido sem erros

---

### 2. ✅ Configuração

Verificar `appsettings.json`:

```json
{
  "OpenAiVision": {
    "Endpoint": "https://api.openai.com/v1/chat/completions",
    "ApiKey": "sk-proj-XU-gjf...",
    "Model": "gpt-4o-mini"
  }
}
```

**Verificar:**
- ✅ Endpoint aponta para `api.openai.com` (não Azure)
- ✅ ApiKey começa com `sk-`
- ✅ Model configurado (gpt-4o-mini ou gpt-4o)

---

### 3. 🧪 Teste Manual via Swagger/Postman

#### Endpoint:
```
POST /api/nutrition/analyze-simple-image
```

#### Request (form-data):
```
file: [imagem de rótulo nutricional]
deviceId: "test-device-001"
```

#### Response Esperada:
```json
{
  "success": true,
  "analysisId": "...",
  "dataSource": "OPENAI_VISION",
  "analysisMode": "FullNutritionLabel",
  "estimatedNutritionProfile": {
    "caloriesPer100g": 120,
    "estimatedCarbsPer100g": 25,
    "estimatedProteinPer100g": 3,
    "estimatedFatPer100g": 0.5,
    "basis": "OpenAI Vision - extração da tabela nutricional"
  },
  "score": {
    "value": 75,
    "label": "Bom",
    "color": "#4CAF50"
  }
}
```

---

### 4. 📊 Verificação de Logs

Procure nos logs:

```
[OpenAI] Iniciando análise nutricional via Vision API
[OpenAI] Enviando requisição para: openai/deployments/gpt-4.1/chat/completions
[OpenAI] Análise concluída — Calorias=120, Proteínas=3, Carbs=25
[Orchestrator] Pipeline concluído — Score=75, Label=Bom
```

---

### 5. 🔍 Casos de Teste

#### Teste 1: Imagem com tabela nutricional completa
**Input:** Foto nítida de rótulo com todos os campos
**Esperado:**
- `dataSource: "OPENAI_VISION"`
- `hasReliableNutritionData: true`
- Todos os macros preenchidos

#### Teste 2: Imagem sem tabela nutricional
**Input:** Foto da frente do produto (sem rótulo nutricional)
**Esperado:**
- `dataSource: "OPENAI_VISION"`
- `hasReliableNutritionData: false`
- Profile com valores `null`

#### Teste 3: Produto com barcode
**Input:** Imagem com código de barras visível
**Esperado:**
- `dataSource: "OPENFOODFACTS"` (se encontrado no OFF)
- OU `dataSource: "OPENAI_VISION"` (se não encontrado no OFF)

#### Teste 4: API Timeout
**Simular:** Desconectar internet temporariamente
**Esperado:**
- Log: `[OpenAI] Timeout na requisição`
- Response vazia (BuildEmpty)

#### Teste 5: API Error
**Simular:** API key inválida
**Esperado:**
- Log: `[OpenAI] Falha na requisição. Status=401`
- Response vazia (BuildEmpty)

---

### 6. 🐛 Troubleshooting

#### Erro: "HttpClient 'OpenAI' not found"
**Solução:** Verificar registro no DI:
```csharp
services.AddHttpClient("OpenAI");
```

#### Erro: "Options 'AzureOpenAiVisionOptions' not configured"
**Solução:** Verificar appsettings.json e registro:
```csharp
services.AddSingleton(_ => Options.Create(new AzureOpenAiVisionOptions { ... }));
```

#### Erro: "Timeout após 30 segundos"
**Solução:** Aumentar timeout no appsettings:
```json
"AzureOpenAiVision": {
  "TimeoutSeconds": 60
}
```

#### JSON inválido na resposta OpenAI
**Problema:** IA retornou texto fora do JSON
**Solução:** O código já remove markdown code blocks:
```csharp
if (jsonContent.StartsWith("```"))
{
    var lines = jsonContent.Split('\n');
    jsonContent = string.Join('\n', lines.Skip(1).Take(lines.Length - 2));
}
```

---

### 7. 📈 Métricas de Sucesso

| Métrica | Alvo | Como Medir |
|---------|------|------------|
| Taxa de sucesso | > 90% | Logs `[OpenAI] Análise concluída` |
| Tempo médio de resposta | < 5s | Logs com timestamps |
| Taxa de campos preenchidos | > 70% | Completeness calculator |
| Taxa de fallback | < 30% | `dataSource != "OPENAI_VISION"` |

---

### 8. 🎯 Validação de Arquitetura

Confirmar que:
- ✅ Controller NÃO foi modificado
- ✅ Pipeline RunPipeline() permanece intacto
- ✅ Validator/Enricher/Scoring NÃO foram alterados
- ✅ INutritionImageAnalyzer só extrai dados (não valida)
- ✅ Nenhuma lógica de negócio no analyzer

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

### Teste de endpoint
```bash
curl -X POST "https://localhost:7319/api/nutrition/analyze-simple-image" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@/path/to/nutrition-label.jpg" \
  -F "deviceId=test-001"
```

---

## 📝 Checklist Final

Antes de considerar DONE:

- [ ] Build sem erros
- [ ] Teste com imagem real retorna dados
- [ ] Logs aparecem corretamente
- [ ] Pipeline completo executa (validator → enricher → scoring)
- [ ] Response tem structure esperada
- [ ] Fallback funciona (quando OpenAI falha)
- [ ] Barcode path continua funcionando
- [ ] Documentação atualizada

---

**Boa sorte com os testes! 🚀**
