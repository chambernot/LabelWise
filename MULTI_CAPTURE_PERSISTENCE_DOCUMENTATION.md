# Multi-Capture Persistence Architecture

## Overview

Este documento descreve a arquitetura de persistência para suporte a múltiplas capturas por produto no LabelWise.

## Entidades

### ProductCapture

Representa uma captura individual de imagem de um produto.

```csharp
public class ProductCapture : AuditableEntity
{
    public Guid SessionId { get; private set; }          // Sessão de análise
    public Guid? ProductId { get; private set; }         // Produto associado
    public CaptureType CaptureType { get; private set; } // Tipo de captura (Barcode, NutritionTable, etc)
    public string ImagePath { get; private set; }        // Caminho da imagem
    public string OcrProvider { get; private set; }      // Provider OCR usado
    public string ExtractedText { get; private set; }    // Texto extraído
    public decimal Confidence { get; private set; }      // Confiança (0-1)
    public int ProcessingTimeMs { get; private set; }    // Tempo de processamento
    public string? ParsedDataJson { get; private set; }  // Dados estruturados
    public bool IsValidated { get; private set; }        // Validado manualmente/IA
    public bool UsedForConsolidation { get; private set; }// Usado para consolidação
}
```

### ProductAnalysisSession

Agrupa múltiplas capturas de uma sessão de análise.

```csharp
public class ProductAnalysisSession : AuditableEntity
{
    public Guid? UserId { get; private set; }           // Usuário (opcional)
    public Guid? ProductId { get; private set; }        // Produto identificado
    public Guid? AnalysisId { get; private set; }       // Análise resultante
    public SessionStatus Status { get; private set; }   // Status da sessão
    public string? DetectedBarcode { get; private set; }// Barcode detectado
    public bool ProductFromCache { get; private set; }  // Produto veio do cache
    public decimal OverallConfidence { get; private set; } // Confiança média
    
    public ICollection<ProductCapture> Captures { get; }
}
```

### ValidatedProduct

Produto consolidado e validado para cache e reutilização.

```csharp
public class ValidatedProduct : AuditableEntity
{
    public Guid ProductId { get; private set; }
    public string? ValidatedName { get; private set; }
    public string? ValidatedBrand { get; private set; }
    public string? ValidatedBarcode { get; private set; }
    public string? ValidatedIngredientsJson { get; private set; }
    public string? ValidatedAllergensJson { get; private set; }
    public string? ValidatedNutritionalJson { get; private set; }
    public ValidationLevel ValidationLevel { get; private set; }
    public decimal ValidationConfidence { get; private set; }
    public int CaptureCount { get; private set; }
    public int ReuseCount { get; private set; }
    public string? ExternalSourceId { get; private set; }
}
```

## Fluxo de Dados

```
┌──────────────────┐
│ Captura Imagem 1 │
└────────┬─────────┘
         │
         ▼
┌──────────────────┐     ┌──────────────────────┐
│ ProductCapture   │────▶│ ProductAnalysisSession│
│ (NutritionTable) │     │                      │
└──────────────────┘     └──────────┬───────────┘
         │                          │
┌──────────────────┐               │
│ Captura Imagem 2 │               │
└────────┬─────────┘               │
         │                          │
         ▼                          │
┌──────────────────┐               │
│ ProductCapture   │───────────────┤
│ (IngredientsList)│               │
└──────────────────┘               │
         │                          │
         ▼                          ▼
┌──────────────────┐     ┌──────────────────┐
│     Product      │◀────│ ProductAnalysis  │
└────────┬─────────┘     └──────────────────┘
         │
         ▼
┌──────────────────┐
│ ValidatedProduct │ ◀── Cache para reutilização
└──────────────────┘
```

## Serviços

### ICapturePersistenceService

Gerencia o ciclo de vida das capturas e sessões.

```csharp
// Iniciar sessão
var session = await _capturePersistenceService.StartSessionAsync(userId);

// Salvar captura
var capture = await _capturePersistenceService.SaveCaptureAsync(new SaveCaptureRequest(
    SessionId: session.Id,
    CaptureType: CaptureType.NutritionTable,
    ImagePath: "/uploads/image.jpg",
    OcrProvider: "Tesseract",
    ExtractedText: "...",
    Confidence: 0.95m,
    ProcessingTimeMs: 1500));

// Associar capturas ao produto
await _capturePersistenceService.AssociateCapturesToProductAsync(session.Id, productId);

// Completar sessão
await _capturePersistenceService.CompleteSessionAsync(
    session.Id, productId, analysisId, overallConfidence);
```

### IProductCacheService

Gerencia o cache de produtos validados.

```csharp
// Verificar cache
var cached = await _productCacheService.GetByBarcodeAsync("7891234567890");

if (cached is not null && !cached.RequiresRevalidation)
{
    // Usar produto do cache
    await _productCacheService.IncrementReuseCountAsync(cached.Product.Id);
}
else
{
    // Processar normalmente e depois cachear
    await _productCacheService.CacheProductAsync(product, validatedData);
}
```

## Tabelas do Banco de Dados

### product_captures

| Coluna | Tipo | Descrição |
|--------|------|-----------|
| id | uuid | PK |
| session_id | uuid | FK para session |
| product_id | uuid | FK para product (nullable) |
| capture_type | int | Enum CaptureType |
| image_path | varchar(1024) | Caminho da imagem |
| ocr_provider | varchar(100) | Provider OCR |
| extracted_text | text | Texto extraído |
| confidence | decimal(5,4) | Confiança |
| processing_time_ms | int | Tempo de processamento |
| parsed_data_json | jsonb | Dados estruturados |
| is_validated | bool | Validado |
| used_for_consolidation | bool | Usado para consolidação |
| captured_at | timestamptz | Data da captura |
| created_at | timestamptz | Data de criação |

### product_analysis_sessions

| Coluna | Tipo | Descrição |
|--------|------|-----------|
| id | uuid | PK |
| user_id | uuid | FK para user (nullable) |
| product_id | uuid | FK para product (nullable) |
| analysis_id | uuid | FK para analysis (nullable) |
| status | int | Enum SessionStatus |
| started_at | timestamptz | Início da sessão |
| completed_at | timestamptz | Fim da sessão |
| detected_barcode | varchar(64) | Barcode detectado |
| product_from_cache | bool | Produto veio do cache |
| overall_confidence | decimal(5,4) | Confiança média |
| error_message | varchar(2000) | Mensagem de erro |
| created_at | timestamptz | Data de criação |

### validated_products

| Coluna | Tipo | Descrição |
|--------|------|-----------|
| id | uuid | PK |
| product_id | uuid | FK para product (unique) |
| validated_name | varchar(500) | Nome validado |
| validated_brand | varchar(200) | Marca validada |
| validated_barcode | varchar(64) | Barcode validado |
| validated_ingredients_json | jsonb | Ingredientes validados |
| validated_allergens_json | jsonb | Alérgenos validados |
| validated_nutritional_json | jsonb | Nutricionais validados |
| validation_level | int | Enum ValidationLevel |
| validation_confidence | decimal(5,4) | Confiança da validação |
| capture_count | int | Número de capturas |
| last_validated_at | timestamptz | Última validação |
| reuse_count | int | Vezes reutilizado |
| external_source_id | varchar(200) | ID fonte externa |
| external_source_name | varchar(100) | Nome fonte externa |
| data_hash | varchar(64) | Hash dos dados |
| created_at | timestamptz | Data de criação |

## Índices

### product_captures
- `ix_product_captures_session_id`
- `ix_product_captures_product_id`
- `ix_product_captures_capture_type`
- `ix_product_captures_captured_at`
- `ix_product_captures_product_id_capture_type` (composite)

### product_analysis_sessions
- `ix_product_analysis_sessions_user_id`
- `ix_product_analysis_sessions_product_id`
- `ix_product_analysis_sessions_status`
- `ix_product_analysis_sessions_started_at`
- `ix_product_analysis_sessions_detected_barcode`

### validated_products
- `ix_validated_products_product_id` (unique)
- `ix_validated_products_validated_barcode`
- `ix_validated_products_validation_level`
- `ix_validated_products_external_source_id`
- `ix_validated_products_data_hash`
- `ix_validated_products_barcode_level` (composite)

## Migration

Para aplicar a migration:

```bash
dotnet ef database update --project LabelWise.Infrastructure --startup-project LabelWise.Api
```

## Níveis de Validação

```csharp
public enum ValidationLevel
{
    None = 0,                    // Não validado
    AutoValidated = 1,           // Validação automática por IA
    MultiCaptureValidated = 2,   // Múltiplas capturas consistentes
    ManuallyValidated = 3,       // Validação manual
    ExternalSourceValidated = 4  // Fonte externa (Open Food Facts)
}
```

## Status da Sessão

```csharp
public enum SessionStatus
{
    Started = 1,     // Sessão iniciada
    Capturing = 2,   // Capturas em andamento
    Processing = 3,  // Processamento OCR
    Completed = 4,   // Concluída com sucesso
    Failed = 5,      // Falhou
    Cancelled = 6    // Cancelada
}
```

## Exemplos de Uso

### Fluxo Completo de Análise

```csharp
public class ProductAnalysisWorkflow
{
    private readonly ICapturePersistenceService _capturePersistence;
    private readonly IProductCacheService _productCache;
    private readonly IOcrProvider _ocrProvider;
    
    public async Task<ProductAnalysisResult> AnalyzeProductAsync(
        List<ImageUpload> images,
        Guid? userId,
        CancellationToken ct)
    {
        // 1. Iniciar sessão
        var session = await _capturePersistence.StartSessionAsync(userId, ct);
        
        try
        {
            string? detectedBarcode = null;
            
            // 2. Processar cada imagem
            foreach (var image in images)
            {
                var ocrResult = await _ocrProvider.ExtractTextAsync(image.Path, ct);
                
                var capture = await _capturePersistence.SaveCaptureAsync(
                    new SaveCaptureRequest(
                        SessionId: session.Id,
                        CaptureType: image.CaptureType,
                        ImagePath: image.Path,
                        OcrProvider: _ocrProvider.ProviderName,
                        ExtractedText: ocrResult.Text,
                        Confidence: ocrResult.Confidence,
                        ProcessingTimeMs: ocrResult.ProcessingTimeMs), ct);
                
                // Detectar barcode se aplicável
                if (image.CaptureType == CaptureType.Barcode)
                {
                    detectedBarcode = ExtractBarcode(ocrResult.Text);
                }
            }
            
            // 3. Verificar cache
            if (detectedBarcode is not null)
            {
                var cached = await _productCache.GetByBarcodeAsync(detectedBarcode, ct);
                
                if (cached is not null && !cached.RequiresRevalidation)
                {
                    // Usar produto do cache
                    await _productCache.IncrementReuseCountAsync(cached.Product.Id, ct);
                    
                    await _capturePersistence.CompleteSessionAsync(
                        session.Id,
                        cached.Product.Id,
                        Guid.Empty, // Sem nova análise
                        cached.ValidatedData.ValidationConfidence, ct);
                    
                    return new ProductAnalysisResult(
                        cached.Product,
                        FromCache: true);
                }
            }
            
            // 4. Criar/atualizar produto
            var product = await CreateOrUpdateProductAsync(session.Id, ct);
            
            // 5. Executar análise
            var analysis = await AnalyzeProductDataAsync(product, ct);
            
            // 6. Consolidar produto
            var validated = await _capturePersistence.ConsolidateProductAsync(
                new ConsolidateProductRequest(
                    ProductId: product.Id,
                    ValidatedName: product.Name,
                    ValidatedBrand: product.Brand,
                    ValidatedBarcode: detectedBarcode,
                    ValidationLevel: ValidationLevel.AutoValidated,
                    ValidationConfidence: analysis.Confidence), ct);
            
            // 7. Completar sessão
            await _capturePersistence.CompleteSessionAsync(
                session.Id,
                product.Id,
                analysis.Id,
                analysis.Confidence, ct);
            
            return new ProductAnalysisResult(product, FromCache: false);
        }
        catch (Exception ex)
        {
            await _capturePersistence.FailSessionAsync(session.Id, ex.Message, ct);
            throw;
        }
    }
}
```

### Consolidação com Múltiplas Capturas

```csharp
// Consolidar produto após várias capturas consistentes
var captures = await _capturePersistence.GetProductCaptureHistoryAsync(productId, ct);

var ingredientCaptures = captures
    .Where(c => c.CaptureType == CaptureType.IngredientsList)
    .OrderByDescending(c => c.Confidence)
    .ToList();

// Verificar consistência entre capturas
if (AreConsistent(ingredientCaptures))
{
    await _capturePersistence.ConsolidateProductAsync(
        new ConsolidateProductRequest(
            ProductId: productId,
            ValidatedIngredientsJson: ingredientCaptures.First().ParsedDataJson,
            ValidationLevel: ValidationLevel.MultiCaptureValidated,
            ValidationConfidence: ingredientCaptures.Average(c => c.Confidence)), ct);
}
```

## Dependências Registradas

No `ServiceCollectionExtensions.cs`:

```csharp
// Repositories
services.AddScoped<IProductCaptureRepository, ProductCaptureRepository>();
services.AddScoped<IProductAnalysisSessionRepository, ProductAnalysisSessionRepository>();
services.AddScoped<IValidatedProductRepository, ValidatedProductRepository>();

// Services
services.AddScoped<ICapturePersistenceService, CapturePersistenceService>();
services.AddScoped<IProductCacheService, ProductCacheService>();
```
