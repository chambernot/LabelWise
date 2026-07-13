# 🧪 TESTES RÁPIDOS - VALIDAÇÃO DA IMPLEMENTAÇÃO

## ✅ Status da Implementação
**Build**: ✅ Compilação bem-sucedida  
**Persistência**: ✅ Banco de dados configurado  
**Endpoints**: ✅ Ambos implementados e funcionais  

---

## 🚀 Como Testar Localmente

### Pré-requisitos
1. PostgreSQL rodando (porta 5432)
2. Banco de dados `labelwise` criado
3. .NET 10 SDK instalado
4. Visual Studio 2026 ou VS Code

### Passo 1: Iniciar o Banco de Dados
```bash
# Usar o script existente
.\start-postgres.bat

# OU via Docker
docker-compose up -d
```

### Passo 2: Aplicar Migrações
```bash
cd LabelWise.Api
dotnet ef database update --project ../LabelWise.Infrastructure
```

### Passo 3: Iniciar a API
```bash
# Opção 1: Via script
.\run-api.ps1

# Opção 2: Diretamente
cd LabelWise.Api
dotnet run
```

**API estará disponível em**: `https://localhost:7001`  
**Swagger**: `https://localhost:7001/swagger`

---

## 📝 Testes com cURL

### Teste 1: Análise Simples (Sem Autenticação)

#### Windows (PowerShell)
```powershell
# Criar arquivo de imagem de teste (qualquer imagem .jpg)
# Substitua "C:\temp\test-label.jpg" pelo caminho real

$uri = "https://localhost:7001/api/products/analyze-image"
$imagePath = "C:\temp\test-label.jpg"

$headers = @{
    "Accept" = "application/json"
}

$form = @{
    file = Get-Item -Path $imagePath
}

Invoke-RestMethod -Uri $uri -Method Post -Form $form -Headers $headers -SkipCertificateCheck
```

#### Linux/Mac (curl)
```bash
curl -k -X POST https://localhost:7001/api/products/analyze-image \
  -H "Content-Type: multipart/form-data" \
  -F "file=@/path/to/test-label.jpg"
```

**Resposta Esperada**:
```json
{
  "analysisId": "uuid-gerado",
  "productId": "uuid-gerado",
  "productName": "CEREAL MATINAL INTEGRAL",
  "brand": null,
  "summary": "Resumo gerado pelo motor de regras...",
  "shortSummary": "Produto seguro (nota 7.0/10)...",
  "generalScore": 0.7,
  "personalizedScore": 0.7,
  "classification": "Safe",
  "confidenceLevel": "Alto",
  "alerts": ["Lista de alertas, se houver"],
  "recommendations": ["Lista de recomendações"],
  "extractedIngredients": ["ingrediente1", "ingrediente2", ...],
  "extractedAllergens": ["glúten", "soja", ...],
  "extractedText": "CONTÉM GLÚTEN, CONTÉM DERIVADOS DE TRIGO E SOJA",
  "createdAt": "2024-01-01T15:30:45.123Z"
}
```

---

### Teste 2: Pipeline com Metadados Técnicos

```powershell
# PowerShell
$uri = "https://localhost:7001/api/pipeline/analyze-image"
Invoke-RestMethod -Uri $uri -Method Post -Form $form -Headers $headers -SkipCertificateCheck
```

```bash
# curl
curl -k -X POST https://localhost:7001/api/pipeline/analyze-image \
  -F "file=@/path/to/test-label.jpg"
```

**Resposta Esperada**: Mesma estrutura do Teste 1 + metadados técnicos
```json
{
  "analysisResult": { /* ... resultado completo ... */ },
  "metadata": {
    "pipelineId": "uuid",
    "startTime": "2024-01-01T15:30:40.000Z",
    "endTime": "2024-01-01T15:30:45.234Z",
    "totalDurationMs": 5234.56,
    "uploadStep": {
      "stepName": "Upload",
      "success": true,
      "durationMs": 123.45,
      "additionalData": {
        "fileSize": 524288,
        "contentType": "image/jpeg"
      }
    },
    "ocrStep": {
      "stepName": "OCR",
      "success": true,
      "durationMs": 2345.67,
      "additionalData": {
        "confidence": 0.92,
        "textLength": 1234,
        "blocksCount": 15,
        "providerName": "Mock OCR Provider (Development Only)"
      }
    },
    "parsingStep": { /* ... */ },
    "analysisStep": { /* ... */ }
  }
}
```

---

### Teste 3: Com Autenticação (Análise Personalizada)

#### 3.1. Registrar Usuário
```powershell
# PowerShell
$uri = "https://localhost:7001/api/auth/register"
$body = @{
    email = "test@example.com"
    password = "Test@123"
    name = "Test User"
} | ConvertTo-Json

Invoke-RestMethod -Uri $uri -Method Post -Body $body -ContentType "application/json" -SkipCertificateCheck
```

```bash
# curl
curl -k -X POST https://localhost:7001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test@123",
    "name": "Test User"
  }'
```

**Resposta**:
```json
{
  "userId": "uuid",
  "email": "test@example.com",
  "name": "Test User",
  "message": "User registered successfully"
}
```

#### 3.2. Fazer Login
```powershell
# PowerShell
$uri = "https://localhost:7001/api/auth/login"
$body = @{
    email = "test@example.com"
    password = "Test@123"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri $uri -Method Post -Body $body -ContentType "application/json" -SkipCertificateCheck
$token = $response.token
Write-Host "Token: $token"
```

```bash
# curl
curl -k -X POST https://localhost:7001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test@123"
  }'
```

**Resposta**:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "userId": "uuid",
  "email": "test@example.com",
  "expiresAt": "2024-01-02T15:30:00Z"
}
```

#### 3.3. Criar Perfil do Usuário
```powershell
# PowerShell
$uri = "https://localhost:7001/api/profile"
$headers = @{
    "Authorization" = "Bearer $token"
}
$body = @{
    lactoseIntolerance = $true
    glutenFree = $false
    diabetes = $false
    sodiumControl = $true
    goal = "WeightLoss"
    preferredExplanationLevel = "Detailed"
} | ConvertTo-Json

Invoke-RestMethod -Uri $uri -Method Post -Body $body -ContentType "application/json" -Headers $headers -SkipCertificateCheck
```

```bash
# curl
curl -k -X POST https://localhost:7001/api/profile \
  -H "Authorization: Bearer eyJhbGc..." \
  -H "Content-Type: application/json" \
  -d '{
    "lactoseIntolerance": true,
    "glutenFree": false,
    "diabetes": false,
    "sodiumControl": true,
    "goal": "WeightLoss",
    "preferredExplanationLevel": "Detailed"
  }'
```

#### 3.4. Analisar Imagem com Perfil
```powershell
# PowerShell
$uri = "https://localhost:7001/api/products/analyze-image"
$headers = @{
    "Authorization" = "Bearer $token"
}

Invoke-RestMethod -Uri $uri -Method Post -Form $form -Headers $headers -SkipCertificateCheck
```

```bash
# curl
curl -k -X POST https://localhost:7001/api/products/analyze-image \
  -H "Authorization: Bearer eyJhbGc..." \
  -F "file=@/path/to/test-label.jpg"
```

**Diferença**: O `personalizedScore` será calculado baseado no perfil do usuário!

---

### Teste 4: Consultar Histórico

```powershell
# PowerShell - Listar todas as análises
$uri = "https://localhost:7001/api/history"
$headers = @{ "Authorization" = "Bearer $token" }
Invoke-RestMethod -Uri $uri -Method Get -Headers $headers -SkipCertificateCheck

# Detalhes de uma análise específica
$analysisId = "uuid-da-analise"
$uri = "https://localhost:7001/api/history/$analysisId"
Invoke-RestMethod -Uri $uri -Method Get -Headers $headers -SkipCertificateCheck
```

```bash
# curl
curl -k -X GET https://localhost:7001/api/history \
  -H "Authorization: Bearer eyJhbGc..."

curl -k -X GET https://localhost:7001/api/history/{analysisId} \
  -H "Authorization: Bearer eyJhbGc..."
```

---

## 🧪 Testes de Validação

### Teste 5: Arquivo Inválido (Extensão)
```bash
curl -k -X POST https://localhost:7001/api/products/analyze-image \
  -F "file=@documento.pdf"
```

**Resposta Esperada** (400 Bad Request):
```json
{
  "error": "Formato de arquivo não suportado. Formatos aceitos: .jpg, .jpeg, .png, .webp"
}
```

### Teste 6: Arquivo Muito Grande
```bash
# Criar arquivo de 10MB
dd if=/dev/zero of=big-file.jpg bs=1M count=10

curl -k -X POST https://localhost:7001/api/products/analyze-image \
  -F "file=@big-file.jpg"
```

**Resposta Esperada** (400 Bad Request):
```json
{
  "error": "Arquivo muito grande. Tamanho máximo permitido: 5MB."
}
```

### Teste 7: Sem Arquivo
```bash
curl -k -X POST https://localhost:7001/api/products/analyze-image
```

**Resposta Esperada** (400 Bad Request):
```json
{
  "error": "Arquivo é obrigatório."
}
```

---

## 🔍 Validação no Banco de Dados

### Verificar Produtos Criados
```sql
-- Conectar ao PostgreSQL
psql -h localhost -U postgres -d labelwise

-- Consultar produtos
SELECT id, name, brand, created_at FROM products ORDER BY created_at DESC LIMIT 5;

-- Consultar análises
SELECT id, product_id, user_id, classification, confidence, created_at 
FROM product_analyses 
ORDER BY created_at DESC 
LIMIT 5;

-- Consultar ingredientes de um produto
SELECT pi.name, pi."order" 
FROM product_ingredients pi 
WHERE pi.product_id = 'uuid-do-produto'
ORDER BY pi."order";

-- Consultar alérgenos de um produto
SELECT allergen_name, is_declared 
FROM product_allergens 
WHERE product_id = 'uuid-do-produto';

-- Consultar alerts de uma análise
SELECT message, severity, confidence 
FROM analysis_alerts 
WHERE product_analysis_id = 'uuid-da-analise';

-- Consultar recommendations de uma análise
SELECT recommendation, reason 
FROM analysis_recommendations 
WHERE product_analysis_id = 'uuid-da-analise';
```

---

## 📊 Exemplo de Dados Reais no Banco

Após executar uma análise, você deve encontrar:

### Tabela: products
```
id                                   | name                      | brand | created_at
-------------------------------------|---------------------------|-------|-------------------
123e4567-e89b-12d3-a456-426614174000 | CEREAL MATINAL INTEGRAL   | NULL  | 2024-01-01 15:30:45
```

### Tabela: product_ingredients
```
id   | product_id | name                              | order
-----|------------|-----------------------------------|------
...  | 123e...    | farinha de trigo enriquecida      | 1
...  | 123e...    | açúcar                            | 2
...  | 123e...    | gordura vegetal                   | 3
```

### Tabela: product_allergens
```
id   | product_id | allergen_name | is_declared
-----|------------|---------------|------------
...  | 123e...    | glúten        | true
...  | 123e...    | trigo         | true
...  | 123e...    | soja          | true
```

### Tabela: product_analyses
```
id       | product_id | user_id | classification | confidence | summary
---------|------------|---------|----------------|------------|--------
456e...  | 123e...    | 789a... | Safe           | High       | Produto...
```

### Tabela: analysis_alerts
```
id   | product_analysis_id | message                              | severity
-----|---------------------|--------------------------------------|----------
...  | 456e...             | Contains gluten or gluten-derived... | Caution
```

### Tabela: analysis_recommendations
```
id   | product_analysis_id | recommendation                       | reason
-----|---------------------|--------------------------------------|--------
...  | 456e...             | Considere produtos com menos açúcar  | NULL
```

---

## ✅ Checklist de Validação

Marque cada item após testar:

- [ ] API inicia sem erros
- [ ] Swagger está acessível
- [ ] Endpoint `/api/products/analyze-image` aceita arquivo
- [ ] Endpoint retorna JSON com todos os campos esperados
- [ ] `analysisId` e `productId` são UUIDs válidos
- [ ] `extractedIngredients` contém lista de strings
- [ ] `extractedAllergens` contém lista de strings
- [ ] `classification` é uma das opções: Safe, Caution, Unsafe
- [ ] `confidenceLevel` é: Alto, Médio ou Baixo
- [ ] Scores estão entre 0 e 1
- [ ] Dados são persistidos no PostgreSQL
- [ ] Consulta `SELECT * FROM products` retorna o produto
- [ ] Consulta `SELECT * FROM product_analyses` retorna a análise
- [ ] Endpoint `/api/pipeline/analyze-image` retorna metadados técnicos
- [ ] Validação de extensão funciona (rejeita .pdf)
- [ ] Validação de tamanho funciona (rejeita > 5MB)
- [ ] Validação de arquivo ausente funciona
- [ ] Login JWT funciona
- [ ] Análise autenticada vincula `user_id` corretamente
- [ ] Perfil do usuário afeta `personalizedScore`
- [ ] Histórico retorna análises persistidas

---

## 🐛 Troubleshooting

### Erro: "Failed to connect to PostgreSQL"
**Solução**: Verificar se o PostgreSQL está rodando
```bash
# Windows
netstat -an | findstr 5432

# Linux/Mac
lsof -i :5432
```

### Erro: "Tesseract not configured"
**Solução**: Normal! O MockOcrProvider está ativo. Para usar OCR real, consulte `OCR_PROVIDERS_CONFIGURATION.md`

### Erro: "Certificate validation failed"
**Solução**: Usar flag `-k` (curl) ou `-SkipCertificateCheck` (PowerShell) para desenvolvimento

### API não inicia
**Solução**: Verificar logs no console
```bash
cd LabelWise.Api
dotnet run --verbosity detailed
```

### Dados não aparecem no banco
**Solução**: Verificar connection string
```bash
# Verificar appsettings.json
cat LabelWise.Api/appsettings.json | grep ConnectionStrings
```

---

## 📚 Próximos Passos

1. ✅ Validar todos os testes acima
2. 🔄 Implementar Tesseract OCR para processar imagens reais
3. 🔄 Integrar com Azure Computer Vision (produção)
4. 🔄 Adicionar mais testes automatizados
5. 🔄 Configurar CI/CD
6. 🔄 Deploy em ambiente de staging

---

**Última atualização**: 2024  
**Status**: ✅ Implementação Completa e Testável
