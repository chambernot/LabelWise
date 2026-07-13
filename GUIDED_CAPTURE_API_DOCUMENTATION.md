# Guided Capture API - Documentação para Apps Mobile

## Visão Geral

A API de Captura Guiada permite que apps mobile orientem usuários através de um fluxo passo-a-passo para análise completa de produtos alimentícios.

## Fluxo de Uso

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  1. Iniciar     │────▶│  2. Capturar    │────▶│  3. Finalizar   │
│     Sessão      │     │     Etapas      │     │     Análise     │
└─────────────────┘     └─────────────────┘     └─────────────────┘
        │                       │                       │
        ▼                       ▼                       ▼
   SessionId              Progress Update         Resultado Completo
```

## Endpoints

### 1. Iniciar Sessão

```http
POST /api/guided-capture/sessions
Content-Type: application/json

{
  "languageCode": "pt-BR",
  "deviceInfo": "LabelWise iOS 2.1.0"
}
```

**Response (201 Created):**
```json
{
  "sessionId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Started",
  "startedAt": "2024-01-15T10:30:00Z",
  "sessionTimeoutMinutes": 30,
  "welcomeMessage": "Vamos analisar o produto! Siga as etapas para capturar as informações do rótulo.",
  "firstStep": {
    "captureType": 4,
    "stepName": "Lista de Ingredientes",
    "description": "Fotografe a lista de ingredientes completa",
    "tips": [
      "Enquadre toda a lista de ingredientes",
      "Mantenha a câmera paralela ao rótulo",
      "Use boa iluminação"
    ],
    "isRequired": true,
    "suggestedOrder": 2
  },
  "allSteps": [
    {
      "captureType": 2,
      "name": "Embalagem Frontal",
      "order": 1,
      "isRequired": false,
      "iconName": "package"
    },
    {
      "captureType": 4,
      "name": "Lista de Ingredientes",
      "order": 2,
      "isRequired": true,
      "iconName": "list"
    },
    {
      "captureType": 3,
      "name": "Tabela Nutricional",
      "order": 3,
      "isRequired": true,
      "iconName": "table"
    },
    {
      "captureType": 5,
      "name": "Declaração de Alérgenos",
      "order": 4,
      "isRequired": false,
      "iconName": "warning"
    },
    {
      "captureType": 1,
      "name": "Código de Barras",
      "order": 5,
      "isRequired": false,
      "iconName": "barcode"
    }
  ]
}
```

### 2. Adicionar Captura

```http
POST /api/guided-capture/sessions/{sessionId}/captures
Content-Type: multipart/form-data

file: [arquivo de imagem]
captureType: 4
languageCode: pt
```

**Response (200 OK):**
```json
{
  "success": true,
  "captureId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "captureType": 4,
  "confidence": 0.92,
  "processingTimeMs": 1250,
  "extractedData": {
    "rawTextSummary": "Ingredientes: Farinha de trigo enriquecida com ferro e ácido fólico...",
    "ingredientsCount": 12,
    "mainIngredients": [
      "Farinha de trigo",
      "Açúcar",
      "Gordura vegetal",
      "Sal",
      "Fermento"
    ],
    "allergens": ["Glúten", "Leite"]
  },
  "sessionStatus": {
    "sessionId": "550e8400-e29b-41d4-a716-446655440000",
    "status": "Capturing",
    "progress": {
      "totalSteps": 5,
      "completedSteps": 1,
      "percentComplete": 20,
      "ingredientsListCaptured": true,
      "nutritionTableCaptured": false,
      "readyForAnalysis": false
    },
    "nextStep": {
      "captureType": 3,
      "stepName": "Tabela Nutricional",
      "description": "Fotografe a tabela de informação nutricional",
      "isRequired": true
    }
  },
  "warnings": [],
  "improvementSuggestion": null
}
```

### 3. Verificar Status da Sessão

```http
GET /api/guided-capture/sessions/{sessionId}
```

**Response (200 OK):**
```json
{
  "sessionId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Capturing",
  "startedAt": "2024-01-15T10:30:00Z",
  "progress": {
    "totalSteps": 5,
    "completedSteps": 3,
    "percentComplete": 60,
    "frontPackagingCaptured": true,
    "ingredientsListCaptured": true,
    "nutritionTableCaptured": true,
    "allergenStatementCaptured": false,
    "barcodeCaptured": false,
    "requiredStepsComplete": true,
    "readyForAnalysis": true
  },
  "nextStep": {
    "captureType": 5,
    "stepName": "Declaração de Alérgenos",
    "isRequired": false
  },
  "currentConfidence": 0.89,
  "completedCaptures": [
    {
      "captureId": "...",
      "captureType": 2,
      "captureTypeName": "Embalagem Frontal",
      "success": true,
      "confidence": 0.85,
      "capturedAt": "2024-01-15T10:31:00Z"
    },
    {
      "captureId": "...",
      "captureType": 4,
      "captureTypeName": "Lista de Ingredientes",
      "success": true,
      "confidence": 0.92,
      "capturedAt": "2024-01-15T10:32:00Z"
    },
    {
      "captureId": "...",
      "captureType": 3,
      "captureTypeName": "Tabela Nutricional",
      "success": true,
      "confidence": 0.90,
      "capturedAt": "2024-01-15T10:33:00Z"
    }
  ]
}
```

### 4. Finalizar Análise

```http
POST /api/guided-capture/sessions/{sessionId}/finalize
Content-Type: application/json

{
  "forceAnalysis": false,
  "includePersonalizedRecommendations": true,
  "explanationLevel": "Standard"
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "sessionId": "550e8400-e29b-41d4-a716-446655440000",
  "analysisId": "b2c3d4e5-f6a7-8901-bcde-f23456789012",
  "productId": "c3d4e5f6-a7b8-9012-cdef-345678901234",
  "product": {
    "productId": "c3d4e5f6-a7b8-9012-cdef-345678901234",
    "name": "Biscoito Integral",
    "brand": "Marca X",
    "barcode": "7891234567890",
    "category": "Alimentos",
    "ingredients": [
      "Farinha de trigo integral",
      "Açúcar",
      "Gordura vegetal",
      "Sal",
      "Fermento"
    ],
    "allergens": ["Glúten", "Leite"],
    "nutritionalInfo": {
      "servingSize": "30g",
      "calories": 120,
      "totalCarbohydrates": 18,
      "proteins": 3,
      "totalFat": 4.5,
      "sodium": 150,
      "sugars": 6
    },
    "claims": ["Integral", "Fonte de fibras"],
    "dataSource": "OCR",
    "isValidated": false
  },
  "nutritionalAnalysis": {
    "overallScore": 65,
    "classification": "Bom",
    "indicatorColor": "yellow",
    "nutriScore": "B",
    "categoryScores": {
      "sugarScore": 70,
      "sodiumScore": 70,
      "saturatedFatScore": 60,
      "fiberScore": 80,
      "proteinScore": 50
    },
    "trafficLight": {
      "fatLevel": "Yellow",
      "saturatesLevel": "Yellow",
      "sugarsLevel": "Green",
      "saltLevel": "Green"
    }
  },
  "summary": {
    "title": "Análise de Biscoito Integral",
    "shortDescription": "Este produto tem um bom perfil nutricional com alguns pontos de atenção.",
    "positives": [
      "Baixo teor de açúcar",
      "Baixo teor de sódio",
      "Boa fonte de fibras",
      "Contém ingredientes integrais"
    ],
    "concerns": [
      "Contém alérgenos: Glúten, Leite"
    ],
    "verdict": "Pode ser consumido como parte de uma dieta equilibrada.",
    "visualIndicator": "👍"
  },
  "alerts": [
    {
      "type": "Allergen",
      "severity": "Critical",
      "title": "Contém Glúten",
      "description": "Este produto contém Glúten. Atenção se você tem restrições alimentares.",
      "iconName": "warning"
    },
    {
      "type": "Allergen",
      "severity": "Critical",
      "title": "Contém Leite",
      "description": "Este produto contém Leite. Atenção se você tem restrições alimentares.",
      "iconName": "warning"
    }
  ],
  "recommendations": [
    {
      "type": "Portion",
      "priority": "Medium",
      "title": "Controle as porções",
      "description": "Atente-se ao tamanho da porção indicada na embalagem.",
      "isPersonalized": false
    }
  ],
  "overallConfidence": 0.87,
  "confidenceBreakdown": {
    "ocrConfidence": 0.89,
    "parsingConfidence": 0.80,
    "dataCompletenessConfidence": 0.80,
    "analysisConfidence": 0.85
  },
  "metadata": {
    "processingTimeMs": 2500,
    "startTime": "2024-01-15T10:35:00Z",
    "endTime": "2024-01-15T10:35:02.5Z"
  }
}
```

### 5. Cancelar Sessão

```http
POST /api/guided-capture/sessions/{sessionId}/cancel
```

**Response (200 OK):**
```json
{
  "message": "Sessão cancelada com sucesso",
  "sessionId": "550e8400-e29b-41d4-a716-446655440000"
}
```

### 6. Remover Captura (Refazer)

```http
DELETE /api/guided-capture/sessions/{sessionId}/captures/{captureId}
```

**Response (200 OK):**
Retorna o status atualizado da sessão (mesmo formato de GET /sessions/{sessionId}).

### 7. Listar Etapas Disponíveis

```http
GET /api/guided-capture/steps?languageCode=pt
```

**Response (200 OK):**
```json
[
  {
    "captureType": 2,
    "name": "Embalagem Frontal",
    "description": "Fotografe a parte frontal da embalagem com o nome e marca do produto",
    "order": 1,
    "isRequired": false,
    "iconName": "package",
    "tips": [
      "Certifique-se de que o nome do produto esteja legível",
      "Inclua a marca se visível",
      "Evite reflexos"
    ]
  },
  ...
]
```

## Tipos de Captura (CaptureType)

| Valor | Nome | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| 1 | Barcode | Não | Código de barras (EAN-13, UPC-A) |
| 2 | FrontPackaging | Não | Embalagem frontal (nome, marca, claims) |
| 3 | NutritionTable | **Sim** | Tabela de informação nutricional |
| 4 | IngredientsList | **Sim** | Lista de ingredientes |
| 5 | AllergenStatement | Não | Declaração de alérgenos |

## Estados da Sessão (SessionStatus)

| Status | Descrição |
|--------|-----------|
| Started | Sessão iniciada, aguardando primeira captura |
| Capturing | Capturas em andamento |
| Processing | Processando análise final |
| Completed | Análise concluída com sucesso |
| Failed | Erro durante processamento |
| Cancelled | Sessão cancelada pelo usuário |

## Códigos de Erro

| Código | Descrição |
|--------|-----------|
| 400 | Dados de entrada inválidos |
| 404 | Sessão ou captura não encontrada |
| 413 | Arquivo muito grande (máx. 10MB) |
| 415 | Formato de arquivo não suportado |

## Integração Mobile

### iOS (Swift)

```swift
// 1. Iniciar sessão
func startSession() async throws -> StartSessionResponse {
    let url = URL(string: "\(baseURL)/api/guided-capture/sessions")!
    var request = URLRequest(url: url)
    request.httpMethod = "POST"
    request.setValue("application/json", forHTTPHeaderField: "Content-Type")
    
    let body = StartSessionRequest(languageCode: "pt-BR", deviceInfo: "LabelWise iOS 2.1.0")
    request.httpBody = try JSONEncoder().encode(body)
    
    let (data, _) = try await URLSession.shared.data(for: request)
    return try JSONDecoder().decode(StartSessionResponse.self, from: data)
}

// 2. Enviar captura
func addCapture(sessionId: UUID, image: UIImage, captureType: Int) async throws -> AddCaptureResponse {
    let url = URL(string: "\(baseURL)/api/guided-capture/sessions/\(sessionId)/captures")!
    var request = URLRequest(url: url)
    request.httpMethod = "POST"
    
    let boundary = UUID().uuidString
    request.setValue("multipart/form-data; boundary=\(boundary)", forHTTPHeaderField: "Content-Type")
    
    var body = Data()
    // Add file
    body.append("--\(boundary)\r\n".data(using: .utf8)!)
    body.append("Content-Disposition: form-data; name=\"file\"; filename=\"capture.jpg\"\r\n".data(using: .utf8)!)
    body.append("Content-Type: image/jpeg\r\n\r\n".data(using: .utf8)!)
    body.append(image.jpegData(compressionQuality: 0.8)!)
    body.append("\r\n".data(using: .utf8)!)
    
    // Add captureType
    body.append("--\(boundary)\r\n".data(using: .utf8)!)
    body.append("Content-Disposition: form-data; name=\"captureType\"\r\n\r\n".data(using: .utf8)!)
    body.append("\(captureType)\r\n".data(using: .utf8)!)
    body.append("--\(boundary)--\r\n".data(using: .utf8)!)
    
    request.httpBody = body
    
    let (data, _) = try await URLSession.shared.data(for: request)
    return try JSONDecoder().decode(AddCaptureResponse.self, from: data)
}

// 3. Finalizar análise
func finalizeAnalysis(sessionId: UUID) async throws -> FinalizeAnalysisResponse {
    let url = URL(string: "\(baseURL)/api/guided-capture/sessions/\(sessionId)/finalize")!
    var request = URLRequest(url: url)
    request.httpMethod = "POST"
    request.setValue("application/json", forHTTPHeaderField: "Content-Type")
    
    let body = FinalizeRequest(forceAnalysis: false, includePersonalizedRecommendations: true)
    request.httpBody = try JSONEncoder().encode(body)
    
    let (data, _) = try await URLSession.shared.data(for: request)
    return try JSONDecoder().decode(FinalizeAnalysisResponse.self, from: data)
}
```

### Android (Kotlin)

```kotlin
// 1. Iniciar sessão
suspend fun startSession(): StartSessionResponse {
    return apiService.startSession(
        StartSessionRequest(
            languageCode = "pt-BR",
            deviceInfo = "LabelWise Android 2.1.0"
        )
    )
}

// 2. Enviar captura
suspend fun addCapture(
    sessionId: UUID,
    imageFile: File,
    captureType: CaptureType
): AddCaptureResponse {
    val imagePart = MultipartBody.Part.createFormData(
        "file",
        imageFile.name,
        imageFile.asRequestBody("image/jpeg".toMediaType())
    )
    
    val captureTypePart = captureType.value.toString()
        .toRequestBody("text/plain".toMediaType())
    
    return apiService.addCapture(sessionId, imagePart, captureTypePart)
}

// 3. Finalizar análise
suspend fun finalizeAnalysis(sessionId: UUID): FinalizeAnalysisResponse {
    return apiService.finalizeAnalysis(
        sessionId,
        FinalizeRequest(
            forceAnalysis = false,
            includePersonalizedRecommendations = true
        )
    )
}
```

## Boas Práticas

1. **Sempre verificar `progress.readyForAnalysis`** antes de habilitar o botão de finalização
2. **Mostrar feedback visual** para cada etapa completada usando `completedCaptures`
3. **Exibir warnings** retornados em `AddCaptureResponse` para guiar o usuário
4. **Implementar retry** para capturas com baixa confiança (< 0.7)
5. **Armazenar `sessionId`** localmente para recuperar sessões em caso de crash
6. **Respeitar o timeout** de 30 minutos da sessão
