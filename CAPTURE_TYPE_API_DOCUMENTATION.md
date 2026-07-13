# API de Análise de Imagem com CaptureType

Esta documentação descreve os endpoints atualizados para análise de imagens de rótulos de produtos alimentícios com suporte a diferentes tipos de captura.

## Endpoints

### POST /api/pipeline/analyze

Endpoint principal para análise de imagens capturadas.

#### Parâmetros

| Parâmetro | Tipo | Obrigatório | Descrição |
|-----------|------|-------------|-----------|
| `file` | IFormFile | Condicional* | Arquivo de imagem do rótulo |
| `captureType` | int | Sim | Tipo de captura (ver tabela abaixo) |
| `barcode` | string | Condicional** | Código de barras do produto |
| `languageCode` | string | Não | Idioma para OCR (padrão: "pt") |
| `enableExternalDatabaseLookup` | bool | Não | Buscar em bases externas (padrão: true) |
| `enableMultiProviderOcr` | bool | Não | Usar múltiplos providers OCR (padrão: true) |
| `executeNutritionalAnalysis` | bool | Não | Executar análise nutricional (padrão: true) |

\* Obrigatório para todos os tipos exceto `Barcode`
\*\* Obrigatório quando `captureType = 1 (Barcode)`

#### Tipos de Captura (CaptureType)

| Valor | Nome | Descrição | Arquivo | Barcode |
|-------|------|-----------|---------|---------|
| 1 | Barcode | Código de barras (EAN, UPC) | Não* | Sim |
| 2 | FrontPackaging | Embalagem frontal | Sim | Não |
| 3 | NutritionTable | Tabela nutricional | Sim | Não |
| 4 | IngredientsList | Lista de ingredientes | Sim | Não |
| 5 | AllergenStatement | Declaração de alérgenos | Sim | Não |

\* Para `Barcode`, o arquivo é opcional - pode-se enviar apenas o código de barras via parâmetro.

---

## Exemplos de Request

### 1. Análise de Tabela Nutricional

```bash
curl -X POST "https://api.labelwise.com/api/pipeline/analyze" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@tabela_nutricional.jpg" \
  -F "captureType=3"
```

### 2. Análise de Lista de Ingredientes

```bash
curl -X POST "https://api.labelwise.com/api/pipeline/analyze" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@ingredientes.jpg" \
  -F "captureType=4" \
  -F "languageCode=pt"
```

### 3. Identificação por Código de Barras (sem imagem)

```bash
curl -X POST "https://api.labelwise.com/api/pipeline/analyze" \
  -H "Content-Type: multipart/form-data" \
  -F "captureType=1" \
  -F "barcode=7891234567890"
```

### 4. Análise de Embalagem Frontal com Barcode

```bash
curl -X POST "https://api.labelwise.com/api/pipeline/analyze" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@embalagem_frontal.jpg" \
  -F "captureType=2" \
  -F "barcode=7891234567890" \
  -F "enableExternalDatabaseLookup=true"
```

### 5. Análise de Alérgenos

```bash
curl -X POST "https://api.labelwise.com/api/pipeline/analyze" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@alergenos.jpg" \
  -F "captureType=5"
```

---

## Exemplos de Response

### Sucesso (200 OK)

```json
{
  "success": true,
  "captureType": 3,
  "overallConfidence": 0.92,
  "identificationResult": {
    "success": true,
    "method": "OCR",
    "confidence": 0.88,
    "barcode": null,
    "productName": "Biscoito Integral Aveia",
    "brand": "Mãe Terra",
    "category": "Biscoitos",
    "dataSource": "Tesseract OCR",
    "isFromExternalDatabase": false,
    "errorMessage": null
  },
  "labelReadingResult": {
    "success": true,
    "confidence": 0.92,
    "rawText": "INFORMAÇÃO NUTRICIONAL\nPorção de 30g (3 biscoitos)\n\nValor energético 120 kcal\nCarboidratos 18g\nProteínas 3g\nGorduras totais 4g\nGorduras saturadas 1,5g\nGorduras trans 0g\nFibra alimentar 2g\nSódio 65mg",
    "ingredients": [
      "farinha de trigo integral",
      "açúcar mascavo",
      "óleo de palma",
      "aveia em flocos",
      "mel",
      "sal"
    ],
    "allergens": [
      "trigo",
      "aveia"
    ],
    "nutritionalInfo": {
      "servingSize": "30g (3 biscoitos)",
      "calories": 120,
      "totalFat": 4,
      "saturatedFat": 1.5,
      "transFat": 0,
      "cholesterol": null,
      "sodium": 65,
      "totalCarbohydrates": 18,
      "dietaryFiber": 2,
      "sugars": null,
      "protein": 3,
      "additionalNutrients": {}
    },
    "nutritionalClaims": [
      "Fonte de fibras"
    ],
    "ocrProvider": "Tesseract OCR",
    "ocrProcessingTimeMs": 1250.5,
    "errorMessage": null
  },
  "finalAnalysis": {
    "analysisId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "productId": "2c963f66-5717-4562-b3fc-3fa85f64afa6",
    "productName": "Biscoito Integral Aveia",
    "brand": "Mãe Terra",
    "summary": "Produto com perfil nutricional moderado. Rico em fibras, mas com teor moderado de gorduras. Adequado para consumo ocasional como parte de uma dieta equilibrada.",
    "shortSummary": "Produto moderado. Rico em fibras.",
    "generalScore": 0.72,
    "personalizedScore": 0.75,
    "classification": "Moderate",
    "confidenceLevel": "High",
    "alerts": [
      "⚠️ Contém alérgenos: trigo, aveia",
      "ℹ️ Teor moderado de gorduras saturadas"
    ],
    "recommendations": [
      "Consumir com moderação (até 3 porções/dia)",
      "Boa opção para lanches entre refeições"
    ],
    "extractedIngredients": [
      "farinha de trigo integral",
      "açúcar mascavo",
      "óleo de palma",
      "aveia em flocos",
      "mel",
      "sal"
    ],
    "extractedAllergens": [
      "trigo",
      "aveia"
    ],
    "extractedText": "INFORMAÇÃO NUTRICIONAL...",
    "createdAt": "2024-01-15T10:30:00Z"
  },
  "metadata": {
    "processingId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "startTime": "2024-01-15T10:29:58Z",
    "endTime": "2024-01-15T10:30:00Z",
    "totalProcessingTimeMs": 2150.75,
    "fileName": "tabela_nutricional.jpg",
    "fileSizeBytes": 245760,
    "contentType": "image/jpeg",
    "ocrProvider": "Tesseract OCR",
    "ocrProviderVersion": "TesseractOcrProvider",
    "steps": [
      {
        "stepName": "Upload",
        "success": true,
        "durationMs": 125.5,
        "errorMessage": null,
        "additionalData": null
      },
      {
        "stepName": "OCR",
        "success": true,
        "durationMs": 1250.5,
        "errorMessage": null,
        "additionalData": null
      },
      {
        "stepName": "Parsing",
        "success": true,
        "durationMs": 85.25,
        "errorMessage": null,
        "additionalData": null
      },
      {
        "stepName": "Analysis",
        "success": true,
        "durationMs": 689.5,
        "errorMessage": null,
        "additionalData": null
      }
    ],
    "analysisHistoryId": 42,
    "additionalInfo": {}
  },
  "errorMessage": null,
  "warnings": [],
  "recommendations": [
    "Consumir com moderação (até 3 porções/dia)",
    "Boa opção para lanches entre refeições"
  ]
}
```

### Erro de Validação (400 Bad Request)

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Erro de validação",
  "status": 400,
  "errors": {
    "barcode": [
      "Barcode é obrigatório quando CaptureType = Barcode."
    ]
  }
}
```

### Erro de Arquivo (400 Bad Request)

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Erro de validação",
  "status": 400,
  "errors": {
    "file": [
      "Arquivo é obrigatório para este tipo de captura.",
      "Formato de arquivo não suportado. Formatos aceitos: .jpg, .jpeg, .png, .webp"
    ]
  }
}
```

---

## GET /api/pipeline/capture-types

Retorna a lista de tipos de captura suportados.

### Response

```json
[
  {
    "value": 1,
    "name": "Barcode",
    "description": "Código de barras (EAN, UPC) para identificação do produto em bases externas.",
    "requiresFile": false,
    "requiresBarcode": true
  },
  {
    "value": 2,
    "name": "FrontPackaging",
    "description": "Embalagem frontal do produto com marca, nome e claims nutricionais.",
    "requiresFile": true,
    "requiresBarcode": false
  },
  {
    "value": 3,
    "name": "NutritionTable",
    "description": "Tabela nutricional com valores energéticos, macro e micronutrientes.",
    "requiresFile": true,
    "requiresBarcode": false
  },
  {
    "value": 4,
    "name": "IngredientsList",
    "description": "Lista de ingredientes em ordem decrescente de quantidade.",
    "requiresFile": true,
    "requiresBarcode": false
  },
  {
    "value": 5,
    "name": "AllergenStatement",
    "description": "Declaração de alérgenos com alertas sobre presença ou traços.",
    "requiresFile": true,
    "requiresBarcode": false
  }
]
```

---

## Códigos de Status HTTP

| Código | Descrição |
|--------|-----------|
| 200 | Análise executada com sucesso |
| 400 | Parâmetros inválidos ou erro de validação |
| 401 | Token JWT inválido ou expirado |
| 500 | Erro interno do servidor |

---

## Regras de Validação

### CaptureType = Barcode
- `barcode` é **obrigatório**
- `file` é **opcional**
- Formato de barcode aceito: EAN-8, EAN-13, UPC-A

### Outros CaptureTypes
- `file` é **obrigatório**
- `barcode` é **opcional** (pode ajudar na identificação)
- Formatos de arquivo aceitos: `.jpg`, `.jpeg`, `.png`, `.webp`
- Tamanho máximo: 5MB

### Formato de Barcode
- EAN-8: 8 dígitos
- EAN-13: 13 dígitos
- UPC-A: 12 dígitos

---

## Notas de Implementação

### Fluxo de Processamento

```
┌─────────────────┐
│   Request       │
│   (file + tipo) │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Validação     │
│   (tipo + file) │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Upload        │
│   (se file)     │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   OCR           │
│   (Tesseract/   │
│    Azure)       │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Parsing       │
│   (por tipo)    │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Análise       │
│   Nutricional   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Quality Gate  │
│   (ajustes)     │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Response      │
│   consolidado   │
└─────────────────┘
```

### Compatibilidade

O endpoint antigo `/api/pipeline/analyze-image` continua funcionando para compatibilidade com versões anteriores, mas está marcado como `[Obsolete]`. Recomenda-se migrar para o novo endpoint `/api/pipeline/analyze`.
