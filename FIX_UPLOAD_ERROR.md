# 🔧 CORREÇÃO DE ERRO - Upload de Imagem no Swagger

## ❌ Problema Identificado

Você estava recebendo o erro **"Failed to fetch"** ao tentar fazer upload da imagem `arroz.jpg` no Swagger.

### Causas Identificadas:

1. **❌ Endpoint incorreto:** 
   - Você usou: `/api/products/analyze-image`
   - Correto: `/api/pipeline/analyze-image`

2. **❌ CORS não configurado para Swagger:**
   - CORS só permitia `localhost:4443` e `localhost:3000`
   - Swagger roda em `localhost:7319` (não estava permitido)

---

## ✅ Correções Aplicadas

### 1. CORS Corrigido (`Program.cs`)

**Antes:**
```csharp
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new string[] { "*" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
```

**Depois:**
```csharp
// Em desenvolvimento, permite qualquer origem
if (builder.Environment.IsDevelopment())
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
}
// Em produção, usa origins específicas
else
{
    var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
}
```

### 2. Origins Atualizadas (`appsettings.json`)

**Antes:**
```json
"Cors": {
  "AllowedOrigins": [ "https://localhost:4443", "http://localhost:3000" ]
}
```

**Depois:**
```json
"Cors": {
  "AllowedOrigins": [ 
    "https://localhost:7319",
    "https://localhost:7001", 
    "https://localhost:4443", 
    "http://localhost:3000" 
  ]
}
```

---

## 🚀 Como Testar Agora

### Opção 1: Via Swagger (Recomendado)

1. **Reiniciar a API:**
   ```powershell
   # Parar a API atual (Ctrl+C se estiver rodando)
   .\run-api.ps1
   ```

2. **Abrir Swagger:**
   ```
   https://localhost:7319/swagger
   ```

3. **Usar endpoint CORRETO:**
   - ✅ **POST /api/pipeline/analyze-image**
   - ❌ ~~POST /api/products/analyze-image~~

4. **Upload da imagem:**
   - Clicar em "Try it out"
   - Selecionar arquivo `arroz.jpg`
   - Clicar em "Execute"

5. **Verificar resultado:**
   - ✅ Status 200 OK
   - JSON com score, classificação, alertas

---

### Opção 2: Via Script PowerShell

1. **Executar script de teste:**
   ```powershell
   .\test-nutritional-scoring-api.ps1
   ```

2. **Ver resultado formatado:**
   - Score geral e personalizado
   - Classificação (Excellent/Good/Attention/Avoid)
   - Alertas e recomendações

---

### Opção 3: Via cURL (linha de comando)

```bash
curl -X 'POST' \
  'https://localhost:7319/api/pipeline/analyze-image' \
  -H 'accept: text/plain' \
  -H 'Content-Type: multipart/form-data' \
  -F 'file=@arroz.jpg;type=image/jpeg' \
  --insecure
```

---

## 📊 Exemplo de Resposta Esperada

### Produto Saudável (ex: Arroz):
```json
{
  "analysis": {
    "productName": "Arroz Tipo 1",
    "brand": "Marca X",
    "generalScore": 0.75,        // 75/100
    "personalizedScore": 0.75,
    "classification": "Good",
    "shortSummary": "Boa escolha (75/100). Pode consumir regularmente com moderação.",
    "alerts": [],
    "recommendations": []
  },
  "ocrResult": {
    "providerName": "Mock",
    "extractedText": "...",
    "confidence": 0.95
  },
  "totalProcessingTimeMs": 1234
}
```

### Produto Ultraprocessado:
```json
{
  "analysis": {
    "productName": "Biscoito Recheado",
    "generalScore": 0.165,       // 16.5/100
    "personalizedScore": 0.165,
    "classification": "Avoid",
    "shortSummary": "Não recomendado (17/100). Evitar este produto.",
    "alerts": [
      "🚨 CONTÉM GORDURA TRANS - Evite este produto!",
      "🚨 Contém gordura hidrogenada",
      "🚨 Teor de açúcar muito elevado: 28g",
      "🚨 Produto altamente processado: 5 tipos de aditivos químicos"
    ],
    "recommendations": [
      "Prefira alimentos in natura ou minimamente processados"
    ]
  }
}
```

---

## 🔍 Troubleshooting

### Problema: Ainda recebe "Failed to fetch"

**Solução 1:** Reiniciar completamente a API
```powershell
# Parar tudo
Get-Process -Name "dotnet" | Stop-Process -Force

# Limpar e recompilar
dotnet clean
dotnet build

# Reiniciar
.\run-api.ps1
```

**Solução 2:** Verificar porta
```powershell
# Ver se porta 7319 está em uso
netstat -ano | findstr "7319"

# Se estiver, matar processo
Stop-Process -Id <PID> -Force
```

**Solução 3:** Verificar certificado SSL
```powershell
# Recriar certificado de desenvolvimento
dotnet dev-certs https --clean
dotnet dev-certs https --trust
```

---

### Problema: "Bad Request" ou "400"

**Causas comuns:**
- ✅ Arquivo muito grande (máx 5MB)
- ✅ Formato inválido (aceita: .jpg, .jpeg, .png, .webp)
- ✅ Arquivo corrompido

**Solução:**
```powershell
# Verificar tamanho do arquivo
(Get-Item "arroz.jpg").Length / 1MB  # Deve ser < 5

# Verificar tipo
file arroz.jpg  # Deve ser JPEG/PNG
```

---

### Problema: Score sempre 50/100 (neutro)

**Causa:** OCR não está extraindo dados nutricionais corretamente

**Solução:**
1. Verificar se imagem tem tabela nutricional legível
2. Usar OCR real (Azure/Tesseract) ao invés de Mock
3. Ver logs do OCR:
   ```powershell
   # Logs aparecem no console da API
   ```

---

## 📝 Checklist Final

Antes de reportar novo erro, verificar:

- [ ] API está rodando (`run-api.ps1`)
- [ ] Usando endpoint correto: `/api/pipeline/analyze-image`
- [ ] Swagger acessível: `https://localhost:7319/swagger`
- [ ] Arquivo existe e é válido (< 5MB, formato correto)
- [ ] CORS corrigido (código atualizado)
- [ ] Certificado SSL válido
- [ ] Porta 7319 não está bloqueada

---

## 🎯 Endpoints Disponíveis

| Endpoint | Descrição | Método |
|----------|-----------|--------|
| `/api/pipeline/analyze-image` | ✅ Análise completa (USE ESTE) | POST |
| `/api/products/analyze-image` | ❌ NÃO EXISTE | - |
| `/api/products/analyze` | Legacy (sem OCR) | POST |
| `/api/history` | Histórico de análises | GET |

---

## 📚 Documentação Relacionada

- **Quick Start:** [QUICK_START_NUTRITIONAL_SCORING.md](QUICK_START_NUTRITIONAL_SCORING.md)
- **Documentação Completa:** [NUTRITIONAL_SCORING_ENGINE_DOCUMENTATION.md](NUTRITIONAL_SCORING_ENGINE_DOCUMENTATION.md)
- **Exemplos de Validação:** [SCORING_VALIDATION_EXAMPLES.cs](SCORING_VALIDATION_EXAMPLES.cs)

---

## ✅ Teste Rápido

Execute isto para confirmar que tudo está funcionando:

```powershell
# 1. Verificar API
Invoke-WebRequest -Uri "https://localhost:7319/swagger" -UseBasicParsing

# 2. Testar endpoint (com arquivo de teste)
.\test-nutritional-scoring-api.ps1

# 3. Ver resultado no Swagger
Start-Process "https://localhost:7319/swagger"
```

**Resultado esperado:**
- ✅ Swagger abre sem erro
- ✅ Endpoint `/api/pipeline/analyze-image` visível
- ✅ Upload funciona e retorna score
- ✅ Classificação correta (Excellent/Good/Attention/Avoid)

---

## 🎉 Sucesso!

Após seguir estas correções, seu upload de imagem deve funcionar perfeitamente!

**Endpoints corretos:**
- ✅ `POST /api/pipeline/analyze-image` - Análise completa
- ✅ `GET /api/history` - Histórico

**Teste agora:**
```powershell
.\test-nutritional-scoring-api.ps1
```

**Ou no Swagger:**
https://localhost:7319/swagger

---

**🔥 Motor de Score Nutricional funcionando!** 🎯
