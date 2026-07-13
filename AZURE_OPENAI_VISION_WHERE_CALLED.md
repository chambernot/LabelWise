# 📍 ONDE O AZURE OPENAI VISION É CHAMADO NO CÓDIGO

## 🎯 Resumo Executivo

O **Azure OpenAI Vision** (`IVisualInterpreter`) é chamado no **`ProductIdentificationService`** como um **fallback inteligente** quando o OCR falha ou tem baixa confiança.

---

## 📊 Fluxo de Chamada

### 1. **Registro do Serviço** (Startup)

**Arquivo**: `LabelWise.Infrastructure/Extensions/ServiceCollectionExtensions.cs`

**Linhas**: 100-110

```csharp
// Register Azure OpenAI Vision Service
services.AddSingleton(_ =>
{
    var section = configuration.GetSection(AzureOpenAiVisionOptions.SectionName);
    return Options.Create(new AzureOpenAiVisionOptions
    {
        Endpoint = section["Endpoint"] ?? string.Empty,
        ApiKey = section["ApiKey"] ?? string.Empty,
        VisionDeployment = section["VisionDeployment"] ?? string.Empty
    });
});

services.AddScoped<IVisualInterpreter, AzureOpenAiVisionInterpreter>();
```

**Configuração** (appsettings.json):
```json
"AzureOpenAiVision": {
  "Endpoint": "https://aihca.openai.azure.com/",
  "ApiKey": "GCQChTrDzBL74wApuPNr28s3vau4z6XTj3iglWMqp0nw2WRI6tHJJQQJ99CDACYeBjFXJ3w3AAABACOG7i7f",
  "VisionDeployment": "gpt-4.1"
}
```

---

### 2. **Injeção no ProductIdentificationService**

**Arquivo**: `LabelWise.Infrastructure/Services/ProductIdentificationService.cs`

**Linhas**: 32, 42, 48

```csharp
public class ProductIdentificationService : IProductIdentificationService
{
    private readonly IOcrProvider _ocrProvider;
    private readonly IVisualInterpreter _visualInterpreter;  // ← INJETADO AQUI
    private readonly ICandidateSuggestionService _candidateSuggestionService;
    private readonly IKnownProductSearchService _knownProductSearchService;
    private readonly ILogger<ProductIdentificationService> _logger;

    public ProductIdentificationService(
        IOcrProvider ocrProvider,
        IVisualInterpreter visualInterpreter,  // ← INJETADO AQUI
        ICandidateSuggestionService candidateSuggestionService,
        IKnownProductSearchService knownProductSearchService,
        ILogger<ProductIdentificationService> logger)
    {
        _visualInterpreter = visualInterpreter ?? throw new ArgumentNullException(...);
        // ...
    }
}
```

---

### 3. **Chamadas ao Azure OpenAI Vision**

O Azure OpenAI Vision é chamado em **3 cenários** diferentes:

---

#### **CENÁRIO 1: OCR Falhou Completamente**

**Arquivo**: `ProductIdentificationService.cs`

**Linhas**: 391-409

```csharp
var ocrResult = await _ocrProvider.ExtractTextAsync(ocrRequest);

if (!ocrResult.Success)
{
    _logger.LogWarning("⚠️ OCR falhou: {Error}", ocrResult.ErrorMessage);

    // ═══════════════════════════════════════════════════════════
    // FALLBACK 1: OCR falhou → tentar OpenAI Vision
    // ═══════════════════════════════════════════════════════════
    if (ProductIdentificationPrioritizer.ShouldUseVisionFallback(request, null, null, _logger))
    {
        _logger.LogInformation("🤖 OCR falhou → Tentando OpenAI Vision como fallback");
        var visionResult = await IdentifyByVisionAsync(tempImagePath);  // ← CHAMADA 1

        if (visionResult.Success)
        {
            stopwatch.Stop();
            visionResult.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;
            return visionResult;
        }
    }
    // ...
}
```

**Trigger**: OCR retorna `Success = false`

**Lógica**: Se OCR falhar completamente (erro ao ler imagem, por exemplo), tenta Vision como fallback.

---

#### **CENÁRIO 2: OCR com Baixa Confiança ou Dados Incompletos**

**Arquivo**: `ProductIdentificationService.cs`

**Linhas**: 436-471

```csharp
// ═══════════════════════════════════════════════════════════
// VERIFICAR SE OCR É SUFICIENTE OU PRECISA DE VISION FALLBACK
// ═══════════════════════════════════════════════════════════
bool isOcrSufficient = ProductIdentificationPrioritizer.IsOcrResultSufficient(
    ocrResult, productName, brand, _logger);

if (!isOcrSufficient)
{
    _logger.LogInformation("⚠️ OCR insuficiente (baixa confiança ou dados incompletos)");

    // ═══════════════════════════════════════════════════════════
    // FALLBACK 2: OCR insuficiente → tentar OCR + OpenAI Vision
    // ═══════════════════════════════════════════════════════════
    if (ProductIdentificationPrioritizer.ShouldUseVisionFallback(request, ocrResult, productName, _logger))
    {
        _logger.LogInformation("🤖 Tentando OpenAI Vision para complementar OCR");

        var visionResult = await IdentifyByVisionAsync(tempImagePath);  // ← CHAMADA 2

        if (visionResult.Success || visionResult.MatchedProductName != null)
        {
            _logger.LogInformation("✅ Vision forneceu dados adicionais → Consolidando");

            // Consolidar OCR + Vision
            var consolidatedResult = ProductIdentificationConsolidator.ConsolidateOcrAndVision(
                ocrResult,
                await GetVisionInterpretationResult(tempImagePath),  // ← CHAMADA 3
                ocrResult.Confidence,
                _logger);

            stopwatch.Stop();
            consolidatedResult.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;

            _logger.LogInformation("✅ Produto identificado por OCR + OpenAI Vision");
            return consolidatedResult;
        }
    }
}
```

**Trigger**: OCR teve sucesso mas:
- **Confiança < 0.75** (threshold em `ProductIdentificationPrioritizer`)
- **Nome do produto ausente** ou **muito curto** (<3 caracteres)
- **Marca ausente**

**Lógica**: Vision é usado para **complementar** o OCR e **consolidar** os resultados.

---

#### **CENÁRIO 3: Método Direto `IdentifyByVisionAsync`**

**Arquivo**: `ProductIdentificationService.cs`

**Linhas**: 798-902

```csharp
/// <summary>
/// Identifica produto usando Azure OpenAI Vision (GPT-4 Vision).
/// </summary>
private async Task<ProductIdentificationResult> IdentifyByVisionAsync(string imagePath)
{
    var stopwatch = Stopwatch.StartNew();
    _logger.LogInformation("🤖 Executando Azure OpenAI Vision");

    try
    {
        var visionRequest = new VisualInterpretationRequest
        {
            ImagePath = imagePath
        };

        var visionResult = await _visualInterpreter.InterpretImageAsync(visionRequest);  // ← CHAMADA REAL
        
        stopwatch.Stop();

        // Limpar nome e marca
        var cleanName = ProductIdentificationConsolidator.CleanProductName(visionResult.ProbableProductName);
        var cleanBrand = ProductIdentificationConsolidator.CleanBrand(visionResult.ProbableBrand);

        // Mapear confiança
        double confidence = visionResult.InterpretationConfidence switch
        {
            ConfidenceLevel.High => 0.85,
            ConfidenceLevel.Medium => 0.65,
            ConfidenceLevel.Low => 0.40,
            _ => 0.20
        };

        // ... retornar ProductIdentificationResult
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "❌ Erro ao executar Azure OpenAI Vision");
        // ... retornar erro
    }
}
```

**Esta é a implementação que realmente chama o serviço Azure OpenAI Vision.**

---

#### **Helper: `GetVisionInterpretationResult`**

**Arquivo**: `ProductIdentificationService.cs`

**Linhas**: 907-926

```csharp
/// <summary>
/// Obtém resultado de interpretação visual (helper para consolidação).
/// </summary>
private async Task<VisualInterpretationResult> GetVisionInterpretationResult(string imagePath)
{
    try
    {
        var visionRequest = new VisualInterpretationRequest
        {
            ImagePath = imagePath
        };

        return await _visualInterpreter.InterpretImageAsync(visionRequest);  // ← CHAMADA
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Erro ao obter interpretação visual");
        return new VisualInterpretationResult
        {
            InterpretationConfidence = ConfidenceLevel.Low
        };
    }
}
```

**Usado no Cenário 2** para obter dados Vision para consolidação com OCR.

---

## 🔄 Fluxo Completo de Identificação de Produto

```
┌─────────────────────────────────────────────────────────────┐
│ ProductIdentificationService.IdentifyProductAsync()         │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
          ┌────────────────────────────────┐
          │ ETAPA 1: Código de Barras?     │ ← Prioridade 100
          └────────────────────────────────┘
                           │
                    ┌──────┴──────┐
                    │             │
                  SIM            NÃO
                    │             │
                    ▼             ▼
         ┌────────────────┐  ┌────────────────────────────────┐
         │ Busca em BD    │  │ ETAPA 2: CaptureType =         │
         │ (Barcode)      │  │ FrontPackaging?                │
         └────────────────┘  └────────────────────────────────┘
              │                            │
              │                     ┌──────┴──────┐
              │                     │             │
              │                   SIM            NÃO
              │                     │             │
              │                     ▼             ▼
              │          ┌────────────────┐  ┌───────────────┐
              │          │ Executar OCR   │  │ ETAPA 3:      │
              │          │ (Tesseract/    │  │ Produtos      │
              │          │  Azure Vision) │  │ Conhecidos    │
              │          └────────────────┘  └───────────────┘
              │                     │                │
              │              ┌──────┴─────┐          │
              │              │            │          │
              │         OCR OK?    OCR FALHOU?       │
              │              │            │          │
              │              │            ▼          │
              │              │   ┌──────────────────────────┐
              │              │   │ 🤖 AZURE OPENAI VISION   │ ← CHAMADA 1
              │              │   │ (FALLBACK 1)             │
              │              │   └──────────────────────────┘
              │              │            │
              │              ▼            │
              │   ┌──────────────────────────┐
              │   │ OCR Confiança >= 0.75?   │
              │   │ Nome válido?             │
              │   │ Marca presente?          │
              │   └──────────────────────────┘
              │              │
              │       ┌──────┴──────┐
              │       │             │
              │     SIM            NÃO
              │       │             │
              │       │             ▼
              │       │   ┌──────────────────────────┐
              │       │   │ 🤖 AZURE OPENAI VISION   │ ← CHAMADA 2
              │       │   │ (FALLBACK 2)             │
              │       │   │ + Consolidação OCR+Vision│
              │       │   └──────────────────────────┘
              │       │            │
              │       ▼            │
              │   ┌────────────────┴─────┐
              │   │ Retornar Resultado   │
              │   └──────────────────────┘
              │              │
              ▼              ▼
          ┌─────────────────────────────┐
          │ Retornar Identificação      │
          └─────────────────────────────┘
```

---

## 📋 Regras de Decisão para Chamar Vision

### Implementação: `ProductIdentificationPrioritizer.ShouldUseVisionFallback()`

**Arquivo**: `LabelWise.Application/Helpers/ProductIdentification/ProductIdentificationPrioritizer.cs`

```csharp
public static bool ShouldUseVisionFallback(
    ProductIdentificationRequest request,
    OcrResultDto? ocrResult,
    string? extractedProductName,
    ILogger logger)
{
    // 1. Vision habilitado na request?
    if (!request.EnableVisionFallback)
    {
        logger.LogDebug("Vision fallback desabilitado na request");
        return false;
    }

    // 2. Imagem é de embalagem frontal?
    if (request.CaptureType != CaptureType.FrontPackaging)
    {
        logger.LogDebug("CaptureType não é FrontPackaging - Vision não aplicável");
        return false;
    }

    // 3. OCR falhou completamente?
    if (ocrResult == null || !ocrResult.Success)
    {
        logger.LogInformation("OCR falhou → Vision será usado como fallback principal");
        return true;
    }

    // 4. OCR com confiança baixa?
    if (ocrResult.Confidence < 0.75)
    {
        logger.LogInformation("OCR confiança baixa ({Confidence}) → Vision será usado", 
            ocrResult.Confidence);
        return true;
    }

    // 5. Nome do produto não identificado ou inválido?
    if (string.IsNullOrWhiteSpace(extractedProductName) || extractedProductName.Length < 3)
    {
        logger.LogInformation("Nome do produto não identificado → Vision será usado");
        return true;
    }

    logger.LogDebug("OCR suficiente - Vision não necessário");
    return false;
}
```

---

## 🎯 Quando o Vision É Chamado?

| Condição | Vision Chamado? |
|----------|----------------|
| **OCR falha completamente** | ✅ SIM (FALLBACK 1) |
| **OCR confiança < 0.75** | ✅ SIM (FALLBACK 2) |
| **Nome produto não identificado** | ✅ SIM (FALLBACK 2) |
| **Nome produto < 3 caracteres** | ✅ SIM (FALLBACK 2) |
| **OCR confiança >= 0.75 + Nome válido** | ❌ NÃO |
| **CaptureType ≠ FrontPackaging** | ❌ NÃO |
| **EnableVisionFallback = false** | ❌ NÃO |

---

## 📊 Exemplo de Request que Aciona Vision

```http
POST /api/guided-capture/sessions/{sessionId}/captures
Content-Type: multipart/form-data

captureType: FrontPackaging
image: [arquivo de imagem]
enableVisionFallback: true  ← IMPORTANTE
```

Se o OCR falhar ou tiver baixa confiança, o Vision será chamado automaticamente.

---

## 💰 Considerações de Custo

### Quando Vision É Chamado:
- **FALLBACK 1**: OCR falhou → Vision é chamado **1 vez**
- **FALLBACK 2**: OCR insuficiente → Vision é chamado **1 ou 2 vezes**
  - 1x em `IdentifyByVisionAsync()`
  - 1x em `GetVisionInterpretationResult()` (consolidação)

### Otimização de Custo:
- Vision **NÃO** é chamado se OCR for suficiente
- Vision **NÃO** é chamado para NutritionTable, IngredientsList, etc.
- Vision **APENAS** para `CaptureType.FrontPackaging`
- Vision **APENAS** se `EnableVisionFallback = true`

---

## 📚 Arquivos Relacionados

| Arquivo | Responsabilidade |
|---------|------------------|
| `ServiceCollectionExtensions.cs` | Registra `IVisualInterpreter` |
| `ProductIdentificationService.cs` | **Chama Vision** (linhas 401, 450, 459, 811) |
| `AzureOpenAiVisionInterpreter.cs` | **Implementa** chamada ao Azure OpenAI |
| `ProductIdentificationPrioritizer.cs` | **Decide** quando chamar Vision |
| `ProductIdentificationConsolidator.cs` | **Consolida** resultados OCR + Vision |
| `appsettings.json` | **Configura** endpoint e API key |

---

## 🔍 Como Rastrear Chamadas

### Logs para Monitorar:

```csharp
// Quando Vision é chamado:
_logger.LogInformation("🤖 OCR falhou → Tentando OpenAI Vision como fallback");
_logger.LogInformation("🤖 Tentando OpenAI Vision para complementar OCR");
_logger.LogInformation("🤖 Executando Azure OpenAI Vision");

// Resultado:
_logger.LogInformation("✅ Vision forneceu dados adicionais → Consolidando");
_logger.LogInformation("✅ Produto identificado por OCR + OpenAI Vision");
```

### Buscar nos Logs:

```bash
# Quantas vezes Vision foi chamado
grep "Executando Azure OpenAI Vision" logs.txt | wc -l

# Sucessos
grep "Vision forneceu dados adicionais" logs.txt

# Falhas
grep "Erro ao executar Azure OpenAI Vision" logs.txt
```

---

## 🎯 Resumo Final

| Aspecto | Detalhes |
|---------|----------|
| **Onde é Registrado** | `ServiceCollectionExtensions.cs` linha ~105 |
| **Onde é Injetado** | `ProductIdentificationService.cs` linha 32, 42 |
| **Onde é Chamado** | `ProductIdentificationService.cs` linhas 401, 450, 811 |
| **Quando é Chamado** | OCR falha OU confiança < 0.75 OU nome inválido |
| **Custo** | 1-2 chamadas por identificação (apenas se necessário) |
| **Tipo de Captura** | **APENAS** `FrontPackaging` |
| **Flag Necessária** | `EnableVisionFallback = true` |

---

**Próximos Passos para Análise**:
1. Verificar logs de produção para ver frequência de chamadas Vision
2. Analisar custo por request
3. Ajustar thresholds se necessário (atual: 0.75)
4. Considerar cache de resultados Vision

---

**Arquivo de Referência**: `LabelWise.Infrastructure/Services/ProductIdentificationService.cs`
