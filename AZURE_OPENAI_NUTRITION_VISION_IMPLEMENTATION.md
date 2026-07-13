# Azure OpenAI Vision Integration para Análise Nutricional

## Visão Geral

Este documento descreve a integração completa do Azure OpenAI Vision para análise nutricional visual de embalagens de produtos alimentícios usando prompts especializados.

## Arquitetura

### Componentes Principais

1. **NutritionVisionPrompts** - Classe estática com prompts centralizados
2. **NutritionVisionInterpreter** - Visual interpreter especializado em análise nutricional
3. **NutritionVisionModelResponse** - DTOs internos para desserialização
4. **NutritionAnalysisService** - Serviço principal que orquestra a análise

### Fluxo de Funcionamento

```
Imagem → NutritionAnalysisService → NutritionVisionInterpreter → Azure OpenAI Vision → JSON Response → DTOs → Resposta Final
```

## System Prompt

O system prompt está definido em `NutritionVisionPrompts.ProductNutritionAnalysisSystemPrompt` e inclui:

- **Regras gerais** de interpretação visual
- **Formato obrigatório** de saída JSON
- **Regras específicas** para extração de dados
- **Classificação** para perfis de consumo (diabético, hipertensão, emagrecimento, ganho muscular)
- **Tratamento de erro** robusto

## Formato de Resposta

O modelo retorna um JSON estruturado com os seguintes campos:

```json
{
  "success": true,
  "productName": "Nome do Produto",
  "brand": "Marca",
  "category": "categoria do produto",
  "packageWeight": "peso da embalagem",
  "analysisMode": "FrontOfPackageOnly | FullNutritionLabel",
  "visibleClaims": ["claim 1", "claim 2"],
  "estimatedNutritionProfile": {
    "caloriesPer100g": 400,
    "estimatedPackageCalories": 200,
    "estimatedSugarPer100g": 25,
    "estimatedProteinPer100g": 8,
    "estimatedSodiumPer100g": 350,
    "estimatedFiberPer100g": 3,
    "estimatedFatPer100g": 15,
    "basis": "Tabela nutricional extraída da imagem"
  },
  "classification": {
    "diabetic": {
      "status": "nao_recomendado",
      "reason": "Alto teor de açúcar"
    },
    "bloodPressure": {
      "status": "nao_recomendado", 
      "reason": "Alto teor de sódio"
    },
    "weightLoss": {
      "status": "consumo_moderado",
      "reason": "Moderado em calorias mas alto em açúcar"
    },
    "muscleGain": {
      "status": "fraco",
      "reason": "Baixo teor proteico"
    }
  },
  "summary": "Biscoito recheado com alto teor de açúcar e sódio, não recomendado para dietas restritivas.",
  "confidenceDetails": {
    "productIdentification": 0.9,
    "visibleClaimsExtraction": 0.8,
    "estimatedNutritionProfile": 0.7,
    "classification": 0.8
  },
  "warnings": [
    "Análise baseada apenas na parte frontal da embalagem"
  ],
  "errorMessage": null
}
```

## Uso Prático

### 1. Endpoint da API

```csharp
[HttpPost]
[Route("analyze")]
public async Task<ActionResult<NutritionAnalysisResponseDto>> AnalyzeProduct(
    [FromForm] NutritionAnalysisFormModel model)
{
    if (model.Image == null || model.Image.Length == 0)
    {
        return BadRequest("Imagem é obrigatória");
    }

    using var memoryStream = new MemoryStream();
    await model.Image.CopyToAsync(memoryStream);
    var imageData = memoryStream.ToArray();

    var result = await _nutritionAnalysisService.AnalyzeProductImageAsync(
        imageData,
        model.Image.FileName,
        model.LanguageCode ?? "pt"
    );

    return Ok(result);
}
```

### 2. Chamada do Serviço

```csharp
// No NutritionAnalysisService
public async Task<NutritionAnalysisResponseDto> AnalyzeProductImageAsync(
    byte[] imageData,
    string fileName,
    string languageCode = "pt",
    List<string>? requestedProfiles = null)
{
    var stopwatch = Stopwatch.StartNew();

    try
    {
        // Chama o visual interpreter especializado
        var visionResult = await PerformVisualInterpretationAsync(imageData);

        if (visionResult == null)
        {
            return CreateErrorResponse("Não foi possível interpretar a imagem", 
                stopwatch.Elapsed.TotalSeconds);
        }

        // O resultado já vem estruturado do NutritionVisionInterpreter
        var response = new NutritionAnalysisResponseDto
        {
            Success = string.IsNullOrWhiteSpace(visionResult.ErrorMessage),
            ProductName = visionResult.ProductName,
            Brand = visionResult.Brand,
            Category = visionResult.Category,
            PackageWeight = visionResult.PackageWeight,
            EstimatedNutritionProfile = visionResult.EstimatedNutritionProfile,
            Classification = visionResult.Classification,
            ConfidenceDetails = visionResult.ConfidenceDetails,
            Summary = visionResult.Summary,
            Warnings = visionResult.Warnings,
            ErrorMessage = visionResult.ErrorMessage,
            ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds
        };

        return response;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Erro durante análise nutricional");
        return CreateErrorResponse($"Erro durante análise: {ex.Message}", 
            stopwatch.Elapsed.TotalSeconds);
    }
}
```

## Configuração

### appsettings.json

```json
{
  "AzureOpenAiVision": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-api-key-here",
    "VisionDeployment": "gpt-4-vision-preview"
  }
}
```

### Dependency Injection

```csharp
// No ServiceCollectionExtensions.cs
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

// Registra o interpreter especializado
services.AddScoped<IVisualInterpreter, NutritionVisionInterpreter>();
services.AddScoped<INutritionAnalysisService, NutritionAnalysisService>();
```

## Tratamento de Erros

O sistema implementa tratamento robusto de erros:

1. **Erro de configuração**: Valida endpoint, API key e deployment
2. **Erro de imagem**: Valida existência e formato do arquivo
3. **Erro de parsing JSON**: Tenta extrair JSON válido de respostas com markdown
4. **Erro de desserialização**: Retorna resultado de erro estruturado
5. **Timeout/rede**: Capturado e logado adequadamente

## Logs e Monitoramento

O sistema produz logs estruturados para:

- Início e fim da análise
- Tempo de processamento
- Confiança dos resultados
- Erros e warnings
- Detalhes da conexão com Azure

## Benefícios da Implementação

1. **Prompt centralizado**: Fácil manutenção e evolução
2. **Estrutura JSON consistente**: Respostas padronizadas
3. **Tratamento de erro robusto**: Falhas gracefuls
4. **Logs detalhados**: Facilita debugging
5. **Separação de responsabilidades**: Código limpo e testável
6. **Flexibilidade**: Fácil de estender para novos casos de uso

## Próximos Passos

1. Implementar cache de respostas para imagens similares
2. Adicionar validação de confiança mínima
3. Implementar retry policy para falhas temporárias
4. Adicionar métricas de performance
5. Implementar batch processing para múltiplas imagens