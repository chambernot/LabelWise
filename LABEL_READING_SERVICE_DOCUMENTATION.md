# 📖 LabelReadingService - Documentação Completa

## 📋 Índice

1. [Visão Geral](#visão-geral)
2. [Arquitetura](#arquitetura)
3. [Componentes](#componentes)
4. [Uso Básico](#uso-básico)
5. [Exemplos por CaptureType](#exemplos-por-capturetype)
6. [Configuração](#configuração)
7. [Estratégias de Parsing](#estratégias-de-parsing)
8. [Testes](#testes)

---

## 🎯 Visão Geral

O `LabelReadingService` é o serviço responsável por **ler e estruturar informações de rótulos alimentares** usando OCR + parsing especializado.

### Características

✅ **Modular** - Estratégias de parsing por tipo de captura  
✅ **Inteligente** - Integração com OCR Provider Selector (Tesseract → Azure fallback)  
✅ **Robusto** - Validação de qualidade e consolidação de resultados  
✅ **Extensível** - Fácil adicionar novos tipos de captura  
✅ **Observável** - Logging detalhado de todo o processo  

### Responsabilidades

- ❌ **NÃO** identifica produtos (responsabilidade do `ProductIdentificationService`)
- ✅ **SIM** extrai conteúdo estruturado do rótulo
- ✅ **SIM** processa múltiplas capturas (nutrition table, ingredients, allergens)
- ✅ **SIM** consolida informações de todas as capturas

---

## 🏗️ Arquitetura

```
┌─────────────────────────────────────────────────────────┐
│           LabelReadingService                            │
│  (Orquestração principal)                                │
└─────────────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────┐
│  IOcrProvider (Tesseract/Azure/Selector)                │
│  - Extração de texto bruto                               │
└─────────────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────┐
│  Capture Strategy (por CaptureType)                     │
│  - NutritionTableReadingStrategy                         │
│  - IngredientsListReadingStrategy                        │
│  - AllergenStatementReadingStrategy                      │
│  - FrontPackagingReadingStrategy                         │
│  - BarcodeReadingStrategy                                │
└─────────────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────┐
│  Structured Data (resultado final)                      │
│  - NutritionalInfo                                       │
│  - Ingredients                                           │
│  - Allergens                                             │
│  - NutritionalClaims                                     │
└─────────────────────────────────────────────────────────┘
```

---

## 🧩 Componentes

### 1. LabelReadingService (Orquestrador)

**Localização:** `LabelWise.Infrastructure\Services\LabelReadingService.cs`

**Responsabilidades:**
- Coordenar processamento de múltiplas capturas
- Executar OCR para cada captura
- Aplicar estratégia de parsing apropriada
- Consolidar resultados
- Calcular confiança geral

### 2. ICaptureReadingStrategy (Interface)

**Localização:** `LabelWise.Infrastructure\Services\LabelReading\ICaptureReadingStrategy.cs`

**Propósito:** Define contrato para estratégias de parsing por tipo de captura.

### 3. Estratégias de Parsing

#### a) NutritionTableReadingStrategy
- Extrai informações nutricionais estruturadas
- Usa regex para identificar porção, calorias, macronutrientes
- Valida completude dos dados

#### b) IngredientsListReadingStrategy
- Extrai lista de ingredientes
- Usa `IngredientAllergenParser` existente
- Filtra ingredientes válidos

#### c) AllergenStatementReadingStrategy
- Extrai declarações de alérgenos
- Usa `IngredientAllergenParser` existente
- Consolida "contém" e "pode conter"

#### d) FrontPackagingReadingStrategy
- Extrai claims nutricionais
- Identifica selos e declarações promocionais
- Ex: "Sem glúten", "Rico em fibras"

#### e) BarcodeReadingStrategy
- Valida formato de código de barras
- **Nota:** Códigos de barras devem ser lidos com bibliotecas especializadas (ZXing)

---

## 🚀 Uso Básico

### Exemplo 1: Leitura Completa do Rótulo

```csharp
public class ExampleController : ControllerBase
{
    private readonly ILabelReadingService _labelReadingService;

    public ExampleController(ILabelReadingService labelReadingService)
    {
        _labelReadingService = labelReadingService;
    }

    [HttpPost("read-label")]
    public async Task<IActionResult> ReadLabel(IFormFileCollection files)
    {
        var request = new LabelReadingRequest
        {
            UserId = GetCurrentUserId(),
            LanguageCode = "pt",
            EnableMultiProviderOcr = true,
            OcrConfidenceThreshold = 0.85,
            Captures = new List<LabelCapture>()
        };

        // Adicionar capturas
        foreach (var file in files)
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);

            request.Captures.Add(new LabelCapture
            {
                CaptureType = DetermineCaptureType(file.FileName),
                ImageData = memoryStream.ToArray(),
                FileName = file.FileName,
                ContentType = file.ContentType,
                Priority = 1
            });
        }

        // Executar leitura
        var result = await _labelReadingService.ReadLabelAsync(request);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new
        {
            success = result.Success,
            confidence = result.OverallConfidence,
            nutritionalInfo = result.NutritionalInfo,
            ingredients = result.Ingredients,
            allergens = result.Allergens,
            claims = result.NutritionalClaims,
            warnings = result.Warnings,
            processingTime = result.ProcessingTimeSeconds
        });
    }

    private CaptureType DetermineCaptureType(string fileName)
    {
        // Lógica simplificada - idealmente usar ML ou UI
        if (fileName.Contains("nutrition", StringComparison.OrdinalIgnoreCase))
            return CaptureType.NutritionTable;
        if (fileName.Contains("ingredients", StringComparison.OrdinalIgnoreCase))
            return CaptureType.IngredientsList;
        if (fileName.Contains("allergens", StringComparison.OrdinalIgnoreCase))
            return CaptureType.AllergenStatement;
        if (fileName.Contains("front", StringComparison.OrdinalIgnoreCase))
            return CaptureType.FrontPackaging;

        return CaptureType.FrontPackaging; // Default
    }
}
```

### Exemplo 2: Leitura Específica de Tabela Nutricional

```csharp
[HttpPost("read-nutrition-table")]
public async Task<IActionResult> ReadNutritionTable(IFormFile file)
{
    using var memoryStream = new MemoryStream();
    await file.CopyToAsync(memoryStream);

    var nutritionInfo = await _labelReadingService.ReadNutritionTableAsync(
        memoryStream.ToArray(),
        languageCode: "pt");

    if (nutritionInfo == null)
    {
        return BadRequest(new { error = "Não foi possível extrair informações nutricionais" });
    }

    return Ok(nutritionInfo);
}
```

### Exemplo 3: Leitura Específica de Ingredientes

```csharp
[HttpPost("read-ingredients")]
public async Task<IActionResult> ReadIngredients(IFormFile file)
{
    using var memoryStream = new MemoryStream();
    await file.CopyToAsync(memoryStream);

    var ingredients = await _labelReadingService.ReadIngredientsAsync(
        memoryStream.ToArray(),
        languageCode: "pt");

    if (!ingredients.Any())
    {
        return BadRequest(new { error = "Nenhum ingrediente encontrado" });
    }

    return Ok(new { ingredients });
}
```

### Exemplo 4: Leitura Específica de Alérgenos

```csharp
[HttpPost("read-allergens")]
public async Task<IActionResult> ReadAllergens(IFormFile file)
{
    using var memoryStream = new MemoryStream();
    await file.CopyToAsync(memoryStream);

    var allergens = await _labelReadingService.ReadAllergensAsync(
        memoryStream.ToArray(),
        languageCode: "pt");

    return Ok(new { allergens });
}
```

---

## 📸 Exemplos por CaptureType

### NutritionTable (Tabela Nutricional)

**Input (Imagem OCR):**
```
INFORMAÇÃO NUTRICIONAL
Porção: 30g (3/4 xícara)
Valor energético: 115 kcal
Carboidratos: 23 g
Proteínas: 3 g
Gorduras totais: 1 g
Gorduras saturadas: 0 g
Gorduras trans: 0 g
Fibra alimentar: 2 g
Sódio: 200 mg
```

**Output (JSON estruturado):**
```json
{
  "servingSize": "30g",
  "calories": 115,
  "carbohydrates": 23,
  "proteins": 3,
  "totalFat": 1,
  "saturatedFat": 0,
  "transFat": 0,
  "fiber": 2,
  "sodium": 200
}
```

### IngredientsList (Lista de Ingredientes)

**Input (Imagem OCR):**
```
INGREDIENTES: Farinha de trigo enriquecida com ferro e ácido fólico,
açúcar, óleo vegetal, sal, fermento químico e aromatizante.
```

**Output (JSON estruturado):**
```json
[
  "Farinha de trigo enriquecida com ferro e ácido fólico",
  "açúcar",
  "óleo vegetal",
  "sal",
  "fermento químico",
  "aromatizante"
]
```

### AllergenStatement (Declaração de Alérgenos)

**Input (Imagem OCR):**
```
ALÉRGICOS: CONTÉM GLÚTEN E DERIVADOS DE SOJA.
PODE CONTER LEITE, OVO E AMENDOIM.
```

**Output (JSON estruturado):**
```json
[
  "Contém: glúten",
  "Contém: soja",
  "Pode conter: leite",
  "Pode conter: ovo",
  "Pode conter: amendoim"
]
```

### FrontPackaging (Embalagem Frontal)

**Input (Imagem OCR):**
```
SEM GLÚTEN
RICO EM FIBRAS
ZERO AÇÚCAR
```

**Output (JSON estruturado):**
```json
[
  "Sem Glúten",
  "Rico Em Fibras",
  "Zero Açúcar"
]
```

---

## ⚙️ Configuração

### appsettings.json

```json
{
  "OCR": {
    "Provider": "Selector",
    "Language": "por",
    "ValidateOnStartup": true,
    "TessdataPath": null,
    "Selector": {
      "UseAzureWhenTesseractConfidenceBelow": 0.85,
      "AlwaysExecuteBoth": false
    },
    "AzureVision": {
      "Endpoint": "https://your-resource.cognitiveservices.azure.com/",
      "ApiKey": "your-api-key",
      "Language": "pt",
      "TimeoutSeconds": 30,
      "EnableDetailedLogging": true,
      "ValidateOnStartup": true
    }
  }
}
```

### Dependency Injection

O serviço é registrado automaticamente em `ServiceCollectionExtensions`:

```csharp
// LabelWise.Infrastructure\Extensions\ServiceCollectionExtensions.cs

services.AddScoped<ILabelReadingService, LabelReadingService>();
```

**Dependências automáticas:**
- `IOcrProvider` (configurado via appsettings.json)
- `IIngredientAllergenParser` (registrado em ApplicationServices)
- `ILogger<LabelReadingService>`

---

## 🎨 Estratégias de Parsing

### Como Funciona

1. **LabelReadingService** recebe múltiplas capturas
2. Para cada captura, identifica o **CaptureType**
3. Busca a **estratégia de parsing apropriada**
4. Executa **OCR** → **Parsing** → **Validação**
5. Retorna **dados estruturados**

### Adicionar Nova Estratégia

```csharp
// 1. Criar nova estratégia
public class CustomReadingStrategy : ICaptureReadingStrategy
{
    public CaptureReadingStrategyResult Parse(string rawOcrText, double ocrConfidence)
    {
        // Sua lógica de parsing aqui
        var result = new CaptureReadingStrategyResult
        {
            Success = true,
            Confidence = ocrConfidence,
            StructuredData = JsonSerializer.Serialize(yourData)
        };

        return result;
    }
}

// 2. Registrar no LabelReadingService
_strategies = new Dictionary<CaptureType, ICaptureReadingStrategy>
{
    // ... estratégias existentes
    [CaptureType.YourNewType] = new CustomReadingStrategy(_parser, _logger)
};
```

---

## 🧪 Testes

### Teste Unitário - Estratégia de Parsing

```csharp
public class NutritionTableReadingStrategyTests
{
    [Fact]
    public void Parse_ValidNutritionTable_ExtractsData()
    {
        // Arrange
        var parser = new Mock<IIngredientAllergenParser>().Object;
        var logger = new Mock<ILogger>().Object;
        var strategy = new NutritionTableReadingStrategy(parser, logger);

        var rawText = @"
            INFORMAÇÃO NUTRICIONAL
            Porção: 30g
            Valor energético: 115 kcal
            Carboidratos: 23 g
            Proteínas: 3 g
        ";

        // Act
        var result = strategy.Parse(rawText, ocrConfidence: 0.9);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.Confidence > 0.7);

        var nutritionInfo = JsonSerializer.Deserialize<NutritionalInformationDto>(
            result.StructuredData);

        Assert.NotNull(nutritionInfo);
        Assert.Equal("30g", nutritionInfo.ServingSize);
        Assert.Equal(115, nutritionInfo.Calories);
        Assert.Equal(23, nutritionInfo.Carbohydrates);
        Assert.Equal(3, nutritionInfo.Proteins);
    }
}
```

### Teste de Integração

```csharp
public class LabelReadingServiceIntegrationTests
{
    [Fact]
    public async Task ReadLabelAsync_MultipleCaptures_ConsolidatesResults()
    {
        // Arrange
        var ocrProvider = new MockOcrProvider();
        var parser = new IngredientAllergenParser();
        var logger = new Mock<ILogger<LabelReadingService>>().Object;
        var service = new LabelReadingService(ocrProvider, parser, logger);

        var request = new LabelReadingRequest
        {
            UserId = 1,
            Captures = new List<LabelCapture>
            {
                new LabelCapture
                {
                    CaptureType = CaptureType.NutritionTable,
                    ImageData = GetTestImageBytes("nutrition_table.jpg")
                },
                new LabelCapture
                {
                    CaptureType = CaptureType.IngredientsList,
                    ImageData = GetTestImageBytes("ingredients.jpg")
                }
            }
        };

        // Act
        var result = await service.ReadLabelAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.NutritionalInfo);
        Assert.NotEmpty(result.Ingredients);
        Assert.True(result.OverallConfidence > 0.5);
    }
}
```

---

## 📊 Métricas e Observabilidade

### Logging

O serviço gera logs detalhados em cada etapa:

```
🎯 Iniciando leitura de rótulo
   UserId: 123
   Capturas: 3
   Idioma: pt

📸 Processando captura: NutritionTable (Priority: 1)
   → Executando OCR para NutritionTable...
   ✅ OCR concluído: 234 caracteres, confiança 92.50%
   → Aplicando estratégia de parsing para NutritionTable...
   → Porção: 30g
   → Calorias: 115 kcal
   → Carboidratos: 23 g
   ✅ Parsing concluído: Success=True, Confidence=89.50%

📦 Consolidando resultados de 3 capturas...
   ✅ Informação nutricional consolidada

═══════════════════════════════════════════════════════════
📊 RESULTADO DA LEITURA
   Success: True
   Confiança Geral: 88.30%
   Capturas OK: 3/3
   Ingredientes: 8
   Alérgenos: 2
   Info Nutricional: True
   Tempo: 3.45s
═══════════════════════════════════════════════════════════
```

### Metadata no Resultado

```json
{
  "metadata": {
    "OcrProvider": "Smart OCR Selector (Tesseract → Azure)",
    "LanguageCode": "pt",
    "TotalCaptures": "3",
    "ProcessingTimeSeconds": "3.45"
  }
}
```

---

## 🔄 Fluxo Completo

```
┌─────────────────────────────────────────────────────────┐
│ 1. Cliente envia múltiplas imagens                      │
│    - nutrition_table.jpg                                 │
│    - ingredients.jpg                                     │
│    - allergens.jpg                                       │
└─────────────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────┐
│ 2. LabelReadingService processa cada captura            │
│    a) Executa OCR (Tesseract/Azure)                     │
│    b) Aplica estratégia de parsing apropriada           │
│    c) Valida qualidade                                   │
└─────────────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────┐
│ 3. Consolidação de resultados                           │
│    - NutritionalInfo (do NutritionTable)                │
│    - Ingredients (do IngredientsList)                   │
│    - Allergens (do AllergenStatement)                   │
└─────────────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────┐
│ 4. Cálculo de confiança geral                           │
│    - Média ponderada por importância                     │
│    - NutritionTable: peso 2.0                           │
│    - IngredientsList: peso 2.0                          │
│    - AllergenStatement: peso 1.5                        │
└─────────────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────┐
│ 5. Retorno estruturado para cliente                     │
│    {                                                     │
│      success: true,                                      │
│      overallConfidence: 0.88,                           │
│      nutritionalInfo: { ... },                          │
│      ingredients: [ ... ],                              │
│      allergens: [ ... ]                                 │
│    }                                                     │
└─────────────────────────────────────────────────────────┘
```

---

## ✅ Checklist de Implementação

- [x] Interface `ILabelReadingService`
- [x] Implementação `LabelReadingService`
- [x] Interface `ICaptureReadingStrategy`
- [x] `NutritionTableReadingStrategy`
- [x] `IngredientsListReadingStrategy`
- [x] `AllergenStatementReadingStrategy`
- [x] `FrontPackagingReadingStrategy`
- [x] `BarcodeReadingStrategy`
- [x] Registro em DI (`ServiceCollectionExtensions`)
- [x] Documentação completa
- [ ] Testes unitários
- [ ] Testes de integração
- [ ] Endpoint de API

---

## 🎓 Próximos Passos

1. **Criar testes unitários** para todas as estratégias
2. **Criar testes de integração** end-to-end
3. **Criar endpoint de API** (`LabelReadingController`)
4. **Adicionar suporte a múltiplos idiomas** (en, es)
5. **Melhorar regex** para extração de valores nutricionais
6. **Adicionar cache** para resultados de OCR
7. **Adicionar telemetria** (Application Insights)

---

## 📚 Referências

- [OCR Providers Documentation](OCR_PROVIDERS_CONFIGURATION.md)
- [IngredientAllergenParser Documentation](PARSER_IMPROVEMENTS_DOCUMENTATION.md)
- [Azure Vision Read API](AZURE_VISION_READ_IMPLEMENTATION.md)
- [Tesseract OCR Setup](TESSERACT_OCR_SETUP_COMPLETE.md)

---

**Status:** ✅ Implementação Completa  
**Versão:** 1.0  
**Data:** 2025-01-XX  
**Autor:** GitHub Copilot + LabelWise Team
