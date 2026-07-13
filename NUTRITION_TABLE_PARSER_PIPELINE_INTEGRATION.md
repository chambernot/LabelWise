# 🔌 INTEGRAÇÃO DO PARSER REFINADO NO PIPELINE

## 🎯 Objetivo

Este documento mostra como integrar o parser refinado de tabela nutricional no pipeline existente do LabelWise.

---

## 📋 ANTES: Pipeline Antigo

```csharp
// ❌ Pipeline antigo - nutritionalFacts frequentemente null
public async Task<ProductAnalysisResult> AnalyzeProduct(string imagePath)
{
    // 1. OCR
    var ocrText = await _ocrProvider.ExtractTextAsync(imagePath);
    
    // 2. Parser antigo (problemático)
    var parseResult = _oldParser.Parse(ocrText);
    
    // 3. nutritionalFacts = null ❌
    if (parseResult.nutritionalFacts == null)
    {
        // Falha silenciosa - dados perdidos
        return new ProductAnalysisResult { Error = "Parsing failed" };
    }
    
    // ...resto do pipeline
}
```

**Problemas**:
- ❌ `nutritionalFacts` frequentemente `null`
- ❌ OCR quebrado causa falhas totais
- ❌ Dados perdidos sem feedback
- ❌ Confiança sempre baixa

---

## ✅ DEPOIS: Pipeline com Parser Refinado

```csharp
using LabelWise.Application.Parsing.Strategies;
using LabelWise.Domain.Enums;

public class ProductAnalysisPipelineOrchestrator
{
    private readonly INutritionTableParser _refinedParser;
    private readonly IOcrProvider _ocrProvider;
    private readonly ILogger<ProductAnalysisPipelineOrchestrator> _logger;

    public ProductAnalysisPipelineOrchestrator(
        INutritionTableParser refinedParser,
        IOcrProvider ocrProvider,
        ILogger<ProductAnalysisPipelineOrchestrator> logger)
    {
        _refinedParser = refinedParser;
        _ocrProvider = ocrProvider;
        _logger = logger;
    }

    public async Task<ProductAnalysisResult> AnalyzeNutritionTable(string imagePath)
    {
        try
        {
            // ═══════════════════════════════════════════════════════════════════
            // ETAPA 1: OCR
            // ═══════════════════════════════════════════════════════════════════
            _logger.LogInformation("🔍 Iniciando OCR da tabela nutricional...");
            var ocrResult = await _ocrProvider.ExtractTextAsync(imagePath);
            
            if (string.IsNullOrWhiteSpace(ocrResult.Text))
            {
                return new ProductAnalysisResult
                {
                    Success = false,
                    Error = "OCR não extraiu texto da imagem"
                };
            }

            _logger.LogInformation("✅ OCR concluído: {Length} caracteres extraídos", 
                ocrResult.Text.Length);

            // ═══════════════════════════════════════════════════════════════════
            // ETAPA 2: PARSING REFINADO
            // ═══════════════════════════════════════════════════════════════════
            _logger.LogInformation("📊 Iniciando parsing refinado...");
            var parseResult = _refinedParser.Parse(ocrResult.Text);

            // ═══════════════════════════════════════════════════════════════════
            // ETAPA 3: VALIDAÇÃO DE QUALIDADE
            // ═══════════════════════════════════════════════════════════════════
            if (!parseResult.HasNutritionData)
            {
                _logger.LogWarning("⚠️ Nenhum dado nutricional extraído");
                return new ProductAnalysisResult
                {
                    Success = false,
                    Error = "Não foi possível extrair dados nutricionais da imagem",
                    Confidence = ConfidenceLevel.Low,
                    Warnings = parseResult.ValidationWarnings
                };
            }

            // ✅ GARANTIDO: parseResult tem dados válidos aqui
            _logger.LogInformation("✅ Parsing concluído: {Fields} campos extraídos, confiança {Confidence}",
                parseResult.ExtractedFieldsCount,
                parseResult.Confidence);

            // ═══════════════════════════════════════════════════════════════════
            // ETAPA 4: PROCESSAR WARNINGS
            // ═══════════════════════════════════════════════════════════════════
            if (parseResult.ValidationWarnings.Any())
            {
                _logger.LogWarning("⚠️ {Count} warnings de validação:", 
                    parseResult.ValidationWarnings.Count);
                foreach (var warning in parseResult.ValidationWarnings)
                {
                    _logger.LogWarning("   • {Warning}", warning);
                }
            }

            // ═══════════════════════════════════════════════════════════════════
            // ETAPA 5: DECISÃO BASEADA EM CONFIANÇA
            // ═══════════════════════════════════════════════════════════════════
            var shouldProceed = DecideBasedOnConfidence(parseResult);
            
            if (!shouldProceed)
            {
                _logger.LogWarning("⚠️ Confiança muito baixa - solicitar nova captura");
                return new ProductAnalysisResult
                {
                    Success = false,
                    Error = "Qualidade dos dados insuficiente. Por favor, tire uma foto mais nítida.",
                    NeedsRecapture = true,
                    Confidence = parseResult.Confidence
                };
            }

            // ═══════════════════════════════════════════════════════════════════
            // ETAPA 6: CONVERTER PARA DTO
            // ═══════════════════════════════════════════════════════════════════
            var nutritionalFacts = ConvertToNutritionalFactsDto(parseResult);

            // ═══════════════════════════════════════════════════════════════════
            // ETAPA 7: ENRIQUECER COM ANÁLISES
            // ═══════════════════════════════════════════════════════════════════
            var analysis = PerformNutritionalAnalysis(nutritionalFacts);

            // ═══════════════════════════════════════════════════════════════════
            // ETAPA 8: RETORNAR RESULTADO
            // ═══════════════════════════════════════════════════════════════════
            return new ProductAnalysisResult
            {
                Success = true,
                NutritionalFacts = nutritionalFacts,
                Analysis = analysis,
                Confidence = parseResult.Confidence,
                ExtractedFieldsCount = parseResult.ExtractedFieldsCount,
                Warnings = parseResult.ValidationWarnings,
                OcrText = ocrResult.Text
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao analisar tabela nutricional");
            return new ProductAnalysisResult
            {
                Success = false,
                Error = $"Erro interno: {ex.Message}"
            };
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Decide se deve prosseguir baseado na confiança.
    /// </summary>
    private bool DecideBasedOnConfidence(NutritionTableParseResult parseResult)
    {
        // Regra 1: Se confiança for LOW e poucos campos, rejeitar
        if (parseResult.Confidence == ConfidenceLevel.Low && 
            parseResult.ExtractedFieldsCount < 3)
        {
            return false;
        }

        // Regra 2: Se muitos warnings, rejeitar
        if (parseResult.ValidationWarnings.Count >= 3)
        {
            return false;
        }

        // Regra 3: Se não tem macros principais, rejeitar
        if (!parseResult.Calories.HasValue && 
            !parseResult.TotalCarbohydrate.HasValue &&
            !parseResult.Protein.HasValue)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Converte NutritionTableParseResult para DTO de API.
    /// </summary>
    private NutritionalFactsDto ConvertToNutritionalFactsDto(NutritionTableParseResult parseResult)
    {
        return new NutritionalFactsDto
        {
            // Porção
            ServingSize = parseResult.ServingSize,
            ServingsPerContainer = parseResult.ServingsPerContainer,

            // Energia
            Calories = parseResult.Calories,

            // Carboidratos
            TotalCarbohydrate = parseResult.TotalCarbohydrate,
            Sugars = parseResult.Sugars,
            AddedSugars = parseResult.AddedSugars,
            Lactose = parseResult.Lactose,
            DietaryFiber = parseResult.DietaryFiber,

            // Proteínas
            Protein = parseResult.Protein,

            // Gorduras
            TotalFat = parseResult.TotalFat,
            SaturatedFat = parseResult.SaturatedFat,
            TransFat = parseResult.TransFat,

            // Minerais
            Sodium = parseResult.Sodium,
            Calcium = parseResult.Calcium,

            // Suplementos
            Creatine = parseResult.Creatine,

            // Metadados
            ExtractedFieldsCount = parseResult.ExtractedFieldsCount,
            Confidence = parseResult.Confidence.ToString(),
            IsComplete = parseResult.IsComplete
        };
    }

    /// <summary>
    /// Realiza análise nutricional dos dados extraídos.
    /// </summary>
    private NutritionalAnalysisDto PerformNutritionalAnalysis(NutritionalFactsDto facts)
    {
        var analysis = new NutritionalAnalysisDto();

        // Análise de macronutrientes
        if (facts.TotalCarbohydrate.HasValue && 
            facts.Protein.HasValue && 
            facts.TotalFat.HasValue)
        {
            var carbCals = facts.TotalCarbohydrate.Value * 4;
            var proteinCals = facts.Protein.Value * 4;
            var fatCals = facts.TotalFat.Value * 9;
            var total = carbCals + proteinCals + fatCals;

            if (total > 0)
            {
                analysis.MacroDistribution = new MacroDistributionDto
                {
                    CarbohydratePercentage = (carbCals / total) * 100,
                    ProteinPercentage = (proteinCals / total) * 100,
                    FatPercentage = (fatCals / total) * 100
                };
            }
        }

        // Análise de qualidade
        analysis.Flags = new List<string>();

        // Flag: Alto em açúcar
        if (facts.Sugars.HasValue && facts.TotalCarbohydrate.HasValue)
        {
            var sugarRatio = facts.Sugars.Value / facts.TotalCarbohydrate.Value;
            if (sugarRatio > 0.5)
            {
                analysis.Flags.Add("HIGH_SUGAR");
            }
        }

        // Flag: Alto em gordura saturada
        if (facts.SaturatedFat.HasValue && facts.TotalFat.HasValue)
        {
            var saturatedRatio = facts.SaturatedFat.Value / facts.TotalFat.Value;
            if (saturatedRatio > 0.5)
            {
                analysis.Flags.Add("HIGH_SATURATED_FAT");
            }
        }

        // Flag: Contém gordura trans
        if (facts.TransFat.HasValue && facts.TransFat.Value > 0)
        {
            analysis.Flags.Add("CONTAINS_TRANS_FAT");
        }

        // Flag: Alto em sódio (> 400mg por porção)
        if (facts.Sodium.HasValue && facts.Sodium.Value > 400)
        {
            analysis.Flags.Add("HIGH_SODIUM");
        }

        // Flag: Fonte de fibra (> 2.5g por porção)
        if (facts.DietaryFiber.HasValue && facts.DietaryFiber.Value >= 2.5)
        {
            analysis.Flags.Add("GOOD_SOURCE_OF_FIBER");
        }

        // Flag: Fonte de proteína (> 5g por porção)
        if (facts.Protein.HasValue && facts.Protein.Value >= 5)
        {
            analysis.Flags.Add("GOOD_SOURCE_OF_PROTEIN");
        }

        // Flag: Fonte de cálcio (> 100mg por porção)
        if (facts.Calcium.HasValue && facts.Calcium.Value >= 100)
        {
            analysis.Flags.Add("GOOD_SOURCE_OF_CALCIUM");
        }

        return analysis;
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class ProductAnalysisResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool NeedsRecapture { get; set; }
    public NutritionalFactsDto? NutritionalFacts { get; set; }
    public NutritionalAnalysisDto? Analysis { get; set; }
    public ConfidenceLevel Confidence { get; set; }
    public int ExtractedFieldsCount { get; set; }
    public List<string> Warnings { get; set; } = new();
    public string? OcrText { get; set; }
}

public class NutritionalFactsDto
{
    // Porção
    public string? ServingSize { get; set; }
    public int? ServingsPerContainer { get; set; }

    // Energia
    public double? Calories { get; set; }

    // Carboidratos
    public double? TotalCarbohydrate { get; set; }
    public double? Sugars { get; set; }
    public double? AddedSugars { get; set; }
    public double? Lactose { get; set; }
    public double? DietaryFiber { get; set; }

    // Proteínas
    public double? Protein { get; set; }

    // Gorduras
    public double? TotalFat { get; set; }
    public double? SaturatedFat { get; set; }
    public double? TransFat { get; set; }

    // Minerais
    public double? Sodium { get; set; }
    public double? Calcium { get; set; }

    // Suplementos
    public double? Creatine { get; set; }

    // Metadados
    public int ExtractedFieldsCount { get; set; }
    public string Confidence { get; set; }
    public bool IsComplete { get; set; }
}

public class NutritionalAnalysisDto
{
    public MacroDistributionDto? MacroDistribution { get; set; }
    public List<string> Flags { get; set; } = new();
}

public class MacroDistributionDto
{
    public double CarbohydratePercentage { get; set; }
    public double ProteinPercentage { get; set; }
    public double FatPercentage { get; set; }
}
```

---

## 📊 COMPARAÇÃO: ANTES vs DEPOIS

### ANTES (Pipeline Antigo)
```
OCR → Parser Simples → nutritionalFacts = null → FALHA ❌
```

### DEPOIS (Pipeline com Parser Refinado)
```
OCR → Parser Refinado → Validação → Análise → Resultado Completo ✅
```

**Vantagens**:
- ✅ `nutritionalFacts` sempre preenchido quando há dados
- ✅ Validação automática de qualidade
- ✅ Decisão inteligente baseada em confiança
- ✅ Análise nutricional enriquecida
- ✅ Flags automáticas (HIGH_SUGAR, CONTAINS_TRANS_FAT, etc)
- ✅ Feedback claro ao usuário (NeedsRecapture)

---

## 🔧 REGISTRO DE DEPENDENCY INJECTION

```csharp
// Startup.cs ou Program.cs

public void ConfigureServices(IServiceCollection services)
{
    // Registrar parser refinado
    services.AddScoped<INutritionTableParser, NutritionTableParser>();
    
    // Registrar orchestrator
    services.AddScoped<ProductAnalysisPipelineOrchestrator>();
    
    // ... outros serviços
}
```

---

## 🚀 EXEMPLO DE USO NO CONTROLLER

```csharp
[ApiController]
[Route("api/[controller]")]
public class NutritionAnalysisController : ControllerBase
{
    private readonly ProductAnalysisPipelineOrchestrator _orchestrator;
    private readonly ILogger<NutritionAnalysisController> _logger;

    public NutritionAnalysisController(
        ProductAnalysisPipelineOrchestrator orchestrator,
        ILogger<NutritionAnalysisController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> AnalyzeNutritionTable(IFormFile image)
    {
        if (image == null || image.Length == 0)
        {
            return BadRequest("Imagem não fornecida");
        }

        try
        {
            // Salvar imagem temporariamente
            var tempPath = Path.GetTempFileName();
            using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            // Analisar usando orchestrator
            var result = await _orchestrator.AnalyzeNutritionTable(tempPath);

            // Limpar arquivo temporário
            System.IO.File.Delete(tempPath);

            // Retornar resultado
            if (!result.Success)
            {
                if (result.NeedsRecapture)
                {
                    return BadRequest(new 
                    { 
                        error = result.Error,
                        needsRecapture = true,
                        suggestion = "Tire uma foto mais nítida da tabela nutricional"
                    });
                }

                return BadRequest(new { error = result.Error });
            }

            return Ok(new
            {
                nutritionalFacts = result.NutritionalFacts,
                analysis = result.Analysis,
                confidence = result.Confidence.ToString(),
                extractedFields = result.ExtractedFieldsCount,
                warnings = result.Warnings
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao analisar tabela nutricional");
            return StatusCode(500, "Erro interno do servidor");
        }
    }
}
```

---

## 📈 EXEMPLO DE RESPOSTA DA API

### Sucesso (Alta Confiança)
```json
{
  "nutritionalFacts": {
    "servingSize": "30g (3 biscoitos)",
    "calories": 140,
    "totalCarbohydrate": 21,
    "sugars": 12,
    "addedSugars": 12,
    "protein": 1.5,
    "totalFat": 5.5,
    "saturatedFat": 2.5,
    "transFat": 0,
    "dietaryFiber": 0.6,
    "sodium": 95,
    "extractedFieldsCount": 11,
    "confidence": "High",
    "isComplete": true
  },
  "analysis": {
    "macroDistribution": {
      "carbohydratePercentage": 62.5,
      "proteinPercentage": 5.5,
      "fatPercentage": 32.0
    },
    "flags": [
      "HIGH_SUGAR"
    ]
  },
  "confidence": "High",
  "extractedFields": 11,
  "warnings": []
}
```

### Sucesso (Média Confiança)
```json
{
  "nutritionalFacts": {
    "servingSize": "30g",
    "calories": 150,
    "totalCarbohydrate": 22.5,
    "protein": 2,
    "totalFat": 6,
    "sodium": 100,
    "extractedFieldsCount": 6,
    "confidence": "Medium",
    "isComplete": false
  },
  "analysis": {
    "flags": []
  },
  "confidence": "Medium",
  "extractedFields": 6,
  "warnings": [
    "Tabela parcial - alguns campos não puderam ser extraídos"
  ]
}
```

### Falha (Solicitar Recaptura)
```json
{
  "error": "Qualidade dos dados insuficiente. Por favor, tire uma foto mais nítida.",
  "needsRecapture": true,
  "suggestion": "Tire uma foto mais nítida da tabela nutricional"
}
```

---

## ✅ CHECKLIST DE INTEGRAÇÃO

### Fase 1: Preparação
- [ ] Registrar `NutritionTableParser` no DI container
- [ ] Criar `ProductAnalysisPipelineOrchestrator`
- [ ] Definir DTOs de resposta

### Fase 2: Implementação
- [ ] Integrar parser no pipeline existente
- [ ] Adicionar validação de confiança
- [ ] Implementar análise nutricional
- [ ] Criar endpoint de API

### Fase 3: Testes
- [ ] Testar com imagens reais (Oreo, Creatina, Iogurte)
- [ ] Testar com OCR quebrado
- [ ] Testar threshold de confiança
- [ ] Validar resposta da API

### Fase 4: Produção
- [ ] Deploy para staging
- [ ] Monitorar métricas de sucesso
- [ ] Ajustar thresholds se necessário
- [ ] Deploy para produção

---

## 🎯 CONCLUSÃO

O parser refinado está **pronto para integração** no pipeline existente. Com as estratégias de validação e análise implementadas, você terá:

✅ **Dados confiáveis** - `nutritionalFacts` sempre preenchido quando há dados  
✅ **Feedback claro** - Usuário sabe quando precisa recapturar  
✅ **Análise enriquecida** - Flags automáticas (HIGH_SUGAR, etc)  
✅ **Pipeline robusto** - Lida com OCR quebrado e edge cases  

**🚀 Pronto para produção!**
