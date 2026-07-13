# Pipeline de Análise de Produtos com OCR

## Visão Geral

Este documento descreve a arquitetura do pipeline de análise de produtos do LabelWise, preparado para integração com provedores de OCR.

## Arquitetura do Pipeline

O pipeline é composto por 5 etapas principais:

```
┌─────────────┐     ┌─────────┐     ┌────────┐     ┌──────────┐     ┌─────────┐
│   Upload    │ --> │   OCR   │ --> │ Parser │ --> │ Analysis │ --> │ Summary │
│   Imagem    │     │ Extract │     │ Struct │     │  Rules   │     │ Result  │
└─────────────┘     └─────────┘     └────────┘     └──────────┘     └─────────┘
```

### 1. Upload de Imagem
**Responsável**: `IImageUploadService` / `ImageUploadService`

- Valida formato e tamanho do arquivo
- Formatos suportados: `.jpg`, `.jpeg`, `.png`, `.webp`, `.bmp`
- Tamanho máximo: 5MB
- Salva temporariamente em disco
- Retorna: `ImageUploadResultDto`

### 2. OCR (Optical Character Recognition)
**Responsável**: `IOcrProvider` / `MockOcrProvider`

- Extrai texto da imagem do rótulo
- Identifica blocos de texto com coordenadas
- Calcula nível de confiança
- Retorna: `OcrResultDto`

**Implementações disponíveis**:
- `MockOcrProvider`: Implementação mock para desenvolvimento/testes
- *(Futuro)* Azure Computer Vision
- *(Futuro)* Google Cloud Vision
- *(Futuro)* AWS Textract

### 3. Parser
**Responsável**: `IIngredientAllergenParser` / `IngredientAllergenParser`

- Analisa texto extraído pelo OCR
- Identifica estruturas:
  - Nome do produto e marca
  - Informações nutricionais
  - Lista de ingredientes
  - Alérgenos
  - Termos críticos
- Retorna: `IngredientAllergenParseResult`

### 4. Motor de Análise
**Responsável**: `IProductAnalysisEngine` / `ProductAnalysisEngineService`

- Aplica regras de negócio
- Calcula scores (geral e personalizado)
- Gera alertas e recomendações
- Considera perfil do usuário (restrições alimentares, objetivos)
- Retorna: `ProductAnalysisResultDto`

### 5. Orquestrador
**Responsável**: `IProductAnalysisPipelineOrchestrator` / `ProductAnalysisPipelineOrchestrator`

- Coordena todas as etapas do pipeline
- Gerencia timeouts e erros
- Coleta métricas de performance
- Limpa recursos temporários
- Retorna: `ProductAnalysisPipelineResultDto` (com metadados)

## Estrutura de Dados

### DTOs de Entrada

#### OcrRequestDto
```csharp
{
    "ImagePath": "string",
    "FileName": "string",
    "ContentType": "string"
}
```

### DTOs de Saída

#### OcrResultDto
```csharp
{
    "RawText": "string",              // Texto completo extraído
    "Confidence": 0.92,                // 0.0 - 1.0
    "Success": true,
    "ErrorMessage": null,
    "TextBlocks": [
        {
            "Text": "string",
            "Confidence": 0.95,
            "BlockType": "TITLE|TEXT|TABLE",
            "BoundingBox": {
                "Left": 10,
                "Top": 10,
                "Width": 200,
                "Height": 30
            }
        }
    ]
}
```

#### IngredientAllergenParseResult
```csharp
{
    "ProductName": "string",
    "Brand": "string",
    "Nutrition": {
        "ServingSize": "30g",
        "Calories": 130.0,
        "TotalFat": 2.5,
        "SaturatedFat": 0.5,
        "TransFat": 0.0,
        "Cholesterol": null,
        "Sodium": 150.0,
        "TotalCarbohydrate": 23.0,
        "DietaryFiber": 2.0,
        "Sugars": null,
        "Protein": 3.0
    },
    "Ingredients": ["farinha de trigo", "açúcar", "..."],
    "Allergens": ["glúten", "soja"],
    "CriticalTerms": ["contém"],
    "ExtractedPhrases": ["contém glúten"]
}
```

#### ProductAnalysisPipelineResultDto
```csharp
{
    "AnalysisResult": {
        "ProductName": "string",
        "Brand": "string",
        "Summary": "string",
        "GeneralScore": 0.75,
        "PersonalizedScore": 0.65,
        "Alerts": ["Alto teor de sódio"],
        "Recommendations": ["Considere opções com menos açúcar"],
        "ConfidenceLevel": "Alto"
    },
    "Metadata": {
        "PipelineId": "guid",
        "StartTime": "2024-01-01T10:00:00Z",
        "EndTime": "2024-01-01T10:00:02Z",
        "TotalDurationMs": 2150.5,
        "UploadStep": {
            "StepName": "Upload",
            "Success": true,
            "DurationMs": 120.3,
            "AdditionalData": {
                "FileSize": 2048576,
                "ContentType": "image/jpeg"
            }
        },
        "OcrStep": {
            "StepName": "OCR",
            "Success": true,
            "DurationMs": 1200.8,
            "AdditionalData": {
                "Confidence": 0.92,
                "TextLength": 1250,
                "BlocksCount": 5,
                "ProviderName": "Mock OCR Provider"
            }
        },
        "ParsingStep": {
            "StepName": "Parsing",
            "Success": true,
            "DurationMs": 45.2,
            "AdditionalData": {
                "IngredientsCount": 12,
                "AllergensCount": 3,
                "ProductName": "Biscoito"
            }
        },
        "AnalysisStep": {
            "StepName": "Analysis",
            "Success": true,
            "DurationMs": 784.2,
            "AdditionalData": {
                "GeneralScore": 0.75,
                "PersonalizedScore": 0.65,
                "AlertsCount": 2,
                "RecommendationsCount": 3
            }
        }
    }
}
```

## API Endpoints

### 1. Análise Simples (compatível com versão anterior)
```http
POST /api/products/analyze-image
Content-Type: multipart/form-data

file: [imagem]
```

**Resposta**: Apenas `ProductAnalysisResultDto`

### 2. Análise com Metadados do Pipeline
```http
POST /api/pipeline/analyze-image
Content-Type: multipart/form-data

file: [imagem]
```

**Resposta**: `ProductAnalysisPipelineResultDto` completo com métricas

## Configuração de Injeção de Dependências

```csharp
// Infrastructure/Extensions/ServiceCollectionExtensions.cs

// OCR Provider
services.AddSingleton<IOcrProvider, MockOcrProvider>();

// Upload Service
services.AddScoped<IImageUploadService, ImageUploadService>();

// Parser
services.AddScoped<IIngredientAllergenParser, IngredientAllergenParser>();

// Analysis Engine
services.AddScoped<IProductAnalysisEngine, ProductAnalysisEngineService>();

// Pipeline Orchestrator
services.AddScoped<IProductAnalysisPipelineOrchestrator, ProductAnalysisPipelineOrchestrator>();

// Main Service (usa o orquestrador)
services.AddScoped<IProductAnalysisService, ProductAnalysisServiceImpl>();
```

## Próximos Passos para Integração com OCR Real

### 1. Implementar Azure Computer Vision

```csharp
public class AzureComputerVisionOcrProvider : IOcrProvider
{
    private readonly string _endpoint;
    private readonly string _apiKey;
    
    public string ProviderName => "Azure Computer Vision";
    
    public async Task<OcrResultDto> ExtractTextAsync(OcrRequestDto request)
    {
        // Chamar API do Azure Computer Vision
        // Converter resposta para OcrResultDto
    }
}
```

Configurar em `appsettings.json`:
```json
{
  "Azure": {
    "ComputerVision": {
      "Endpoint": "https://your-resource.cognitiveservices.azure.com/",
      "ApiKey": "your-api-key"
    }
  }
}
```

### 2. Implementar Google Cloud Vision

```csharp
public class GoogleCloudVisionOcrProvider : IOcrProvider
{
    public string ProviderName => "Google Cloud Vision";
    
    public async Task<OcrResultDto> ExtractTextAsync(OcrRequestDto request)
    {
        // Usar Google Cloud Vision API
    }
}
```

### 3. Selecionar Provedor Dinamicamente

```csharp
// Registrar múltiplos provedores
services.AddSingleton<IOcrProvider, MockOcrProvider>();
services.AddSingleton<IOcrProvider, AzureComputerVisionOcrProvider>();
services.AddSingleton<IOcrProvider, GoogleCloudVisionOcrProvider>();

// Criar factory para seleção
services.AddSingleton<IOcrProviderFactory, OcrProviderFactory>();
```

## Tratamento de Erros

Cada etapa do pipeline captura erros específicos:

1. **Upload**: Formato inválido, tamanho excedido
2. **OCR**: API indisponível, baixa confiança, timeout
3. **Parser**: Texto malformado, estrutura não reconhecida
4. **Analysis**: Dados insuficientes, regras não aplicáveis

Os erros são propagados para o resultado final com informações detalhadas.

## Monitoramento e Métricas

O pipeline coleta automaticamente:
- Duração de cada etapa (ms)
- Taxa de sucesso por etapa
- Confiança do OCR
- Quantidade de dados extraídos
- Erros e exceções

Essas métricas podem ser enviadas para sistemas de observabilidade (Application Insights, Datadog, etc).

## Testes

### Teste Unitário do Mock OCR
```csharp
var provider = new MockOcrProvider();
var request = new OcrRequestDto { ImagePath = "test.jpg" };
var result = await provider.ExtractTextAsync(request);

Assert.True(result.Success);
Assert.True(result.Confidence > 0.9);
Assert.NotEmpty(result.RawText);
```

### Teste de Integração do Pipeline
```csharp
var orchestrator = serviceProvider.GetService<IProductAnalysisPipelineOrchestrator>();
var stream = File.OpenRead("test-label.jpg");
var result = await orchestrator.ExecutePipelineAsync(stream, "test.jpg");

Assert.True(result.Metadata.UploadStep.Success);
Assert.True(result.Metadata.OcrStep.Success);
Assert.True(result.Metadata.ParsingStep.Success);
Assert.True(result.Metadata.AnalysisStep.Success);
```

## Limitações Atuais

1. **Mock OCR**: Retorna sempre o mesmo texto simulado
2. **Parser**: Heurísticas básicas em PT-BR, pode falhar com layouts complexos
3. **Sem Cache**: Cada análise processa tudo do zero
4. **Sem Fila**: Processamento síncrono pode ter timeout em imagens grandes
5. **Sem Retry**: Falhas não são reprocessadas automaticamente

## Melhorias Futuras

1. ✅ Integrar Azure Computer Vision
2. ✅ Adicionar cache de resultados OCR
3. ✅ Implementar fila assíncrona (Azure Service Bus / RabbitMQ)
4. ✅ Adicionar retry com backoff exponencial
5. ✅ Melhorar parser com ML/NLP
6. ✅ Suporte multilíngue
7. ✅ Detecção de qualidade da imagem
8. ✅ Sugestão de reposicionamento da imagem
