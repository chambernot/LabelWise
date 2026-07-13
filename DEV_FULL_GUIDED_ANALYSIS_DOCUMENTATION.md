# 🧪 Endpoint de Desenvolvimento - Full Guided Analysis Test

## 📋 Visão Geral

Este endpoint foi criado para facilitar o desenvolvimento e testes do fluxo completo de captura guiada, permitindo que você envie múltiplas imagens em uma única chamada HTTP, sem precisar implementar o fluxo passo-a-passo do app mobile.

**⚠️ IMPORTANTE: Este endpoint está disponível APENAS em ambiente de Development.**

---

## 🎯 Propósito

O fluxo normal de captura guiada envolve múltiplas chamadas:
1. `POST /api/guided-capture/sessions` - Iniciar sessão
2. `POST /api/guided-capture/sessions/{id}/captures` - Adicionar captura frontal
3. `POST /api/guided-capture/sessions/{id}/captures` - Adicionar ingredientes
4. `POST /api/guided-capture/sessions/{id}/captures` - Adicionar nutrição
5. `POST /api/guided-capture/sessions/{id}/captures` - Adicionar alérgenos
6. `POST /api/guided-capture/sessions/{id}/finalize` - Finalizar análise

Este endpoint de desenvolvimento **consolida tudo em uma única chamada**, facilitando:
- ✅ Testes rápidos de ponta a ponta
- ✅ Validação de OCR e parsing
- ✅ Debugging de problemas
- ✅ Desenvolvimento sem frontend
- ✅ Integração com scripts de automação

---

## 🚀 Endpoint

```
POST /api/dev/full-guided-analysis-test
```

### Autenticação
Requer token JWT no header:
```
Authorization: Bearer {seu-token-jwt}
```

### Content-Type
```
Content-Type: multipart/form-data
```

---

## 📝 Parâmetros do Request

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `frontImage` | file | Não | Foto da embalagem frontal do produto |
| `ingredientsImage` | file | Recomendado* | Foto da lista de ingredientes completa |
| `nutritionImage` | file | Recomendado* | Foto da tabela de informação nutricional |
| `allergenImage` | file | Não | Foto da declaração de alérgenos |
| `barcode` | string | Não | Código de barras do produto (pode ser manual) |
| `languageCode` | string | Não | Código do idioma (padrão: `pt-BR`) |
| `deviceInfo` | string | Não | Informações do dispositivo/teste |

\* Pelo menos uma das imagens (ingredients ou nutrition) é fortemente recomendada para análise significativa.

### Formatos de Imagem Aceitos
- `.jpg` / `.jpeg`
- `.png`
- `.webp`

### Tamanho Máximo
- 10MB por imagem

---

## 📤 Exemplo de Request

### PowerShell

```powershell
$apiUrl = "https://localhost:7319/api/dev/full-guided-analysis-test"
$token = "seu-token-jwt"

$form = @{
    frontImage = Get-Item -Path "C:\images\front.jpg"
    ingredientsImage = Get-Item -Path "C:\images\ingredients.jpg"
    nutritionImage = Get-Item -Path "C:\images\nutrition.jpg"
    allergenImage = Get-Item -Path "C:\images\allergen.jpg"
    barcode = "7891234567890"
    languageCode = "pt-BR"
    deviceInfo = "PowerShell Test Script"
}

$response = Invoke-RestMethod -Uri $apiUrl `
    -Method Post `
    -Headers @{ Authorization = "Bearer $token" } `
    -Form $form `
    -SkipCertificateCheck  # Apenas para localhost com HTTPS

$response | ConvertTo-Json -Depth 10
```

### cURL

```bash
curl -X POST "https://localhost:7319/api/dev/full-guided-analysis-test" \
  -H "Authorization: Bearer {token}" \
  -F "frontImage=@/path/to/front.jpg" \
  -F "ingredientsImage=@/path/to/ingredients.jpg" \
  -F "nutritionImage=@/path/to/nutrition.jpg" \
  -F "allergenImage=@/path/to/allergen.jpg" \
  -F "barcode=7891234567890" \
  -F "languageCode=pt-BR" \
  -F "deviceInfo=cURL Test"
```

### Postman

1. Method: `POST`
2. URL: `https://localhost:7319/api/dev/full-guided-analysis-test`
3. Headers:
   - `Authorization: Bearer {seu-token}`
4. Body: `form-data`
   - `frontImage`: [selecionar arquivo]
   - `ingredientsImage`: [selecionar arquivo]
   - `nutritionImage`: [selecionar arquivo]
   - `allergenImage`: [selecionar arquivo]
   - `barcode`: 7891234567890
   - `languageCode`: pt-BR

---

## 📥 Response Structure

### Success Response (200 OK)

```json
{
  "sessionId": "550e8400-e29b-41d4-a716-446655440000",
  "processedAt": "2024-01-15T10:30:00Z",
  "totalDuration": "00:00:05.234",
  "success": true,
  
  "productIdentification": {
    "productName": "Biscoito Recheado Chocolate",
    "brand": "Marca XYZ",
    "barcode": "7891234567890",
    "category": "Biscoitos",
    "method": "Barcode",
    "confidence": 0.92,
    "alternativeCandidates": []
  },
  
  "ingredients": {
    "detectedIngredients": [
      "farinha de trigo enriquecida com ferro e ácido fólico",
      "açúcar",
      "gordura vegetal",
      "cacau em pó",
      "sal"
    ],
    "totalCount": 15,
    "parseConfidence": 0.88,
    "rawText": "INGREDIENTES: farinha de trigo...",
    "processingWarnings": []
  },
  
  "allergens": {
    "declaredAllergens": ["trigo", "leite", "soja"],
    "mayContainAllergens": ["amendoim", "castanhas"],
    "inferredFromIngredients": [],
    "detectionConfidence": 0.85,
    "rawText": "CONTÉM: TRIGO, LEITE E SOJA..."
  },
  
  "nutritionalFacts": {
    "nutrients": {
      "Calorias": {
        "name": "Calorias",
        "valuePer100g": 450,
        "valuePerServing": 135,
        "unit": "kcal",
        "dailyValuePercent": 7
      },
      "Carboidratos": {
        "name": "Carboidratos",
        "valuePer100g": 65,
        "valuePerServing": 19.5,
        "unit": "g",
        "dailyValuePercent": 6
      }
    },
    "servingSize": "30g (3 unidades)",
    "calories": 450,
    "parseConfidence": 0.85,
    "nutrientsDetected": 6,
    "parsingIssues": []
  },
  
  "finalAnalysis": {
    "productAnalysisId": 42,
    "classification": "NeedsModeration",
    "overallScore": 3.2,
    "alerts": [
      "Alto teor de açúcar",
      "Alto teor de gordura saturada"
    ],
    "recommendations": [
      "Consumir com moderação",
      "Preferir versões integrais"
    ],
    "overallConfidence": "Medium"
  },
  
  "confidenceDetails": {
    "overall": 0.87,
    "dimensions": {
      "OCR": 0.90,
      "Parsing": 0.85,
      "Identification": 0.92,
      "Completeness": 0.80
    }
  },
  
  "processedSteps": [
    {
      "captureType": "FrontPackaging",
      "stepName": "Embalagem Frontal",
      "success": true,
      "duration": "00:00:01.234",
      "fileSizeBytes": 524288,
      "fileStoragePath": "uploads/550e8400.../front.jpg",
      "ocrResult": {
        "success": true,
        "confidence": 0.92,
        "textLength": 150,
        "previewText": "Biscoito Recheado Chocolate\nMarca XYZ...",
        "ocrDuration": "00:00:00.850",
        "provider": "AzureVision"
      },
      "parsingResult": {
        "success": true,
        "confidence": 0.88,
        "itemsExtracted": 2,
        "extractedData": {
          "ProductName": "Biscoito Recheado Chocolate",
          "Brand": "Marca XYZ"
        },
        "parsingDuration": "00:00:00.120"
      },
      "stepWarnings": [],
      "stepErrors": []
    }
  ],
  
  "missingRequiredSteps": [],
  "warnings": [],
  "errors": [],
  
  "debugMetadata": {
    "SessionStartedAt": "2024-01-15T10:29:55Z",
    "TotalImagesProcessed": 4
  }
}
```

### Campos Principais do Response

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `sessionId` | GUID | ID da sessão criada (pode ser usado para consultas posteriores) |
| `processedAt` | DateTime | Timestamp UTC do processamento |
| `totalDuration` | TimeSpan | Duração total do processamento |
| `success` | bool | Indica se a análise foi concluída com sucesso |
| `productIdentification` | object | Dados consolidados de identificação do produto |
| `ingredients` | object | Ingredientes detectados e parseados |
| `allergens` | object | Alérgenos identificados |
| `nutritionalFacts` | object | Informações nutricionais extraídas |
| `finalAnalysis` | object | Análise final com score e classificação |
| `confidenceDetails` | object | Confiança multidimensional da análise |
| `processedSteps` | array | Metadados detalhados de cada etapa processada |
| `missingRequiredSteps` | array | Etapas obrigatórias que não foram fornecidas |
| `warnings` | array | Avisos não-críticos |
| `errors` | array | Erros ocorridos durante o processamento |

---

## 🔄 Fluxo de Processamento

1. **Validação de Ambiente**
   - Verifica se está em Development
   - Retorna 403 Forbidden se não estiver

2. **Validação de Autenticação**
   - Extrai userId do token JWT
   - Retorna 401 Unauthorized se inválido

3. **Validação de Entrada**
   - Valida pelo menos uma imagem ou barcode
   - Valida tamanho e formato das imagens
   - Retorna 400 Bad Request se inválido

4. **Criação de Sessão**
   - Cria nova sessão de captura guiada
   - Registra no banco de dados

5. **Processamento de Capturas**
   - Para cada imagem fornecida:
     - Salva arquivo no storage
     - Executa OCR apropriado
     - Faz parsing específico do tipo
     - Registra metadados

6. **Processamento de Barcode** (se fornecido)
   - Registra código de barras
   - Busca produto em catálogo (se disponível)

7. **Validação de Completude**
   - Verifica etapas obrigatórias
   - Adiciona warnings para dados faltantes

8. **Finalização de Análise**
   - Consolida dados de todas as capturas
   - Executa análise nutricional
   - Gera score e classificação
   - Cria alertas e recomendações

9. **Cálculo de Confiança**
   - Confiança de OCR (média de todas as capturas)
   - Confiança de Parsing (média)
   - Confiança de Identificação
   - Score de Completude

10. **Montagem de Response**
    - Consolida todos os dados
    - Inclui metadados detalhados
    - Retorna 200 OK sempre (mesmo com erros parciais)

---

## ⚙️ Configuração

### Registro no DI (ServiceCollectionExtensions.cs)

```csharp
services.AddScoped<IDevFullGuidedAnalysisOrchestrator, DevFullGuidedAnalysisOrchestrator>();
```

### Verificação de Ambiente (Controller)

O controller verifica automaticamente se está em Development:

```csharp
if (!_environment.IsDevelopment())
{
    return StatusCode(403, "Endpoint disponível apenas em Development");
}
```

---

## 🧪 Testing

### Script PowerShell Automatizado

Use o script fornecido:

```powershell
.\test-dev-full-guided-analysis.ps1 `
    -ApiBaseUrl "https://localhost:7319" `
    -Username "test@example.com" `
    -Password "Test@123" `
    -ImagesPath "C:\temp\test-images" `
    -SkipCertificateCheck
```

### Health Check

Antes de testar, verifique se o endpoint está disponível:

```bash
GET /api/dev/full-guided-analysis-test/health
```

Response:
```json
{
  "status": "healthy",
  "endpoint": "/api/dev/full-guided-analysis-test",
  "environment": "Development",
  "timestamp": "2024-01-15T10:30:00Z",
  "acceptedImageTypes": [".jpg", ".jpeg", ".png", ".webp"],
  "maxFileSizeMB": 10
}
```

---

## 📊 Interpretando Resultados

### Classification

| Valor | Significado | Score |
|-------|-------------|-------|
| `Excellent` | Excelente escolha | 4.5 - 5.0 |
| `Good` | Boa escolha | 3.5 - 4.49 |
| `NeedsModeration` | Consumir com moderação | 2.0 - 3.49 |
| `Avoid` | Evitar | < 2.0 |

### Confidence Levels

| Level | Valor | Descrição |
|-------|-------|-----------|
| `High` | > 0.85 | Alta confiança nos dados |
| `Medium` | 0.60 - 0.85 | Confiança moderada |
| `Low` | < 0.60 | Baixa confiança - revisar |

### Dimensões de Confiança

- **OCR**: Confiança na extração de texto das imagens
- **Parsing**: Confiança no parsing estruturado dos dados
- **Identification**: Confiança na identificação do produto
- **Completeness**: Percentual de etapas concluídas

---

## ⚠️ Warnings Comuns

| Warning | Causa | Ação |
|---------|-------|------|
| "Lista de ingredientes não foi capturada" | `ingredientsImage` não fornecida | Adicione imagem de ingredientes |
| "Tabela nutricional não foi capturada" | `nutritionImage` não fornecida | Adicione imagem de nutrição |
| "Baixa confiança no OCR" | Imagem de baixa qualidade | Use imagem com melhor iluminação/foco |
| "Parsing incompleto" | Dados parcialmente extraídos | Verifique formato da imagem |

---

## 🐛 Troubleshooting

### Erro: "Endpoint disponível apenas em Development"

**Causa**: API não está rodando em modo Development

**Solução**:
```bash
# Definir variável de ambiente
set ASPNETCORE_ENVIRONMENT=Development

# Ou no launchSettings.json
"environmentVariables": {
  "ASPNETCORE_ENVIRONMENT": "Development"
}
```

### Erro: "Nenhuma entrada fornecida"

**Causa**: Nenhuma imagem ou barcode foi enviado

**Solução**: Forneça pelo menos uma imagem ou código de barras

### Erro: "Arquivo excede o tamanho máximo"

**Causa**: Imagem maior que 10MB

**Solução**: Redimensione ou comprima a imagem antes de enviar

### Erro: "Extensão inválida"

**Causa**: Formato de arquivo não suportado

**Solução**: Use JPG, PNG ou WEBP

### OCR retorna texto vazio

**Causas possíveis**:
- Imagem muito escura ou clara
- Texto muito pequeno
- Imagem desfocada
- Ângulo muito inclinado

**Soluções**:
- Melhorar iluminação
- Usar câmera de maior resolução
- Manter câmera paralela ao rótulo
- Aproximar mais do texto

---

## 📚 Arquitetura

### Componentes

```
DevGuidedAnalysisController
    ↓ (valida e orquestra)
DevFullGuidedAnalysisOrchestrator
    ↓ (usa)
GuidedCaptureService
    ↓ (usa)
├── IOcrProvider (Tesseract, Azure Vision, etc)
├── IIngredientAllergenParser
├── ICapturePersistenceService
└── IProductAnalysisPipelineOrchestrator
```

### DTOs Criados

1. **FullGuidedAnalysisRequest**: Request do endpoint
2. **FullGuidedAnalysisResponse**: Response consolidado
3. **ProductIdentificationSummary**: Sumário de identificação
4. **IngredientsDetectionSummary**: Sumário de ingredientes
5. **AllergensDetectionSummary**: Sumário de alérgenos
6. **NutritionalFactsSummary**: Sumário nutricional
7. **FinalAnalysisSummary**: Sumário da análise final
8. **ProcessedStepMetadata**: Metadados de cada etapa
9. **OcrStepResult**: Resultado de OCR
10. **ParsingStepResult**: Resultado de parsing

---

## 🎯 Casos de Uso

### 1. Teste Rápido de Ponta a Ponta

**Cenário**: Validar se o fluxo completo está funcionando

**Ação**: Enviar todas as 4 imagens + barcode

**Resultado Esperado**: `success: true`, análise completa gerada

### 2. Teste de OCR

**Cenário**: Validar qualidade do OCR

**Ação**: Enviar apenas uma imagem de cada vez

**Resultado Esperado**: `ocrResult.confidence > 0.85`

### 3. Teste de Parsing

**Cenário**: Validar extração estruturada

**Ação**: Enviar imagens com dados conhecidos

**Resultado Esperado**: `parsingResult.itemsExtracted` correto

### 4. Teste de Robustez

**Cenário**: Validar comportamento com dados parciais

**Ação**: Enviar apenas ingredientes OU apenas nutrição

**Resultado Esperado**: `warnings` indicando dados faltantes

### 5. Teste de Performance

**Cenário**: Medir tempo de processamento

**Ação**: Enviar múltiplas imagens

**Resultado Esperado**: `totalDuration` dentro do esperado

---

## 📝 Notas Importantes

1. **Sessões são persistidas**: Cada chamada cria uma sessão no banco de dados
2. **Imagens são armazenadas**: Arquivos vão para o storage configurado
3. **Análise é registrada**: Resultado vai para a tabela ProductAnalysis
4. **Custos**: Cada chamada consome créditos de OCR (se usando Azure)
5. **Concorrência**: Endpoint é thread-safe, pode receber múltiplas chamadas
6. **Timeout**: Request pode demorar devido a OCR (~5-10s por imagem)
7. **Logs**: Todas as operações são logadas para debugging

---

## 🔒 Segurança

- ✅ Autenticação JWT obrigatória
- ✅ Disponível apenas em Development
- ✅ Validação de tamanho de arquivo
- ✅ Validação de tipo de arquivo
- ✅ UserID extraído do token (não pode ser forjado)
- ✅ Rate limiting (se configurado)
- ⚠️ Não use em Production!

---

## 🚀 Próximos Passos

Após validar o endpoint:

1. ✅ Testar com imagens reais de produtos
2. ✅ Validar confiança de OCR e parsing
3. ✅ Ajustar thresholds se necessário
4. ✅ Implementar no app mobile o fluxo passo-a-passo
5. ✅ Usar este endpoint para testes de regressão

---

## 📞 Suporte

Para problemas ou dúvidas:
1. Verifique logs da API (`LabelWise.Api`)
2. Verifique logs do Orquestrador (`DevFullGuidedAnalysisOrchestrator`)
3. Consulte documentação do fluxo guiado (`GUIDED_CAPTURE_API_DOCUMENTATION.md`)
4. Consulte documentação de OCR (`OCR_PIPELINE_DOCUMENTATION.md`)

---

**Desenvolvido para facilitar testes e desenvolvimento do LabelWise** 🏷️
