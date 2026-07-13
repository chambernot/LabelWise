# Validação Híbrida OCR - Sistema de Análise Nutricional

## 📋 Visão Geral

O sistema de análise nutricional agora utiliza **validação híbrida** combinando dois serviços da Azure:

1. **Azure OpenAI Vision** (GPT-4 Vision) - Análise com contexto semântico
2. **Azure Computer Vision OCR** - OCR de alta precisão para validação

## 🔄 Fluxo do Sistema

```
┌─────────────────────────────────────────────────────────┐
│           IMAGEM DA TABELA NUTRICIONAL                  │
└─────────────────────────────────────────────────────────┘
                        │
            ┌───────────┴───────────┐
            ▼                       ▼
┌─────────────────────┐   ┌─────────────────────┐
│ Azure OpenAI Vision │   │ Computer Vision OCR │
│  (Contexto + IA)    │   │   (OCR Preciso)     │
└─────────────────────┘   └─────────────────────┘
            │                       │
            ▼                       ▼
   ┌──────────────┐         ┌──────────────┐
   │ Calorias: 436│         │ Calorias: 519│ ✅
   │ Proteína: 6.1│         │ Proteína: 5.2│ ✅
   └──────────────┘         └──────────────┘
                │
                ▼
        ┌────────────────┐
        │   VALIDADOR    │
        │  Se divergir   │
        │  > 15%, usar   │
        │  Computer      │
        │  Vision        │
        └────────────────┘
```

## 🎯 Pipeline de Análise

### Stage 1: Interpretação Visual (Azure OpenAI Vision)
- Extrai dados nutricionais com contexto semântico
- Identifica produto, marca, categoria
- Interpreta claims e informações do rótulo
- Gera valores iniciais com confiança

### Stage 2: Extração de Dados Brutos
- Processa resposta da IA
- Extrai valores numéricos
- Identifica unidades (g/ml)

### Stage 2b: Rebuild da Tabela Nutricional
- Parser de colunas (100g, 100ml, porção)
- Normalização de valores
- Detecção de formato líquido/sólido

### **Stage 2c: Validação Híbrida OCR (NOVO)** ✨
- Executa Computer Vision OCR em paralelo
- Compara valores extraídos:
  - Calorias
  - Proteínas
  - Gorduras totais
  - Carboidratos
  - Sódio
- **Critério de correção**: divergência > 15%
- Se divergente, substitui pelo valor do OCR (mais preciso)
- Adiciona warnings informativos na resposta

### Stage 3-12: Processamento Adicional
- Normalização para base 100g/100ml
- Validação de consistência
- Fallback por categoria (quando necessário)
- Cálculo de score
- Persistência

## 📊 Validação de Divergência

### Threshold Configurável
```csharp
private const double DivergenceThreshold = 0.15; // 15% de divergência
```

### Exemplo de Cálculo

**Cenário 1: Valores Consistentes**
```
OpenAI Vision: 520 kcal
Computer Vision: 519 kcal
Divergência: |520 - 519| / 520 = 0.19% ✅
Ação: Mantém valor original (sem correção)
```

**Cenário 2: Valores Divergentes**
```
OpenAI Vision: 436 kcal
Computer Vision: 519 kcal
Divergência: |436 - 519| / 436 = 19.04% ⚠️
Ação: Substitui por 519 kcal (correção aplicada)
Warning: "⚠️ Calorias corrigidas de 436 para 519 kcal usando OCR de validação"
```

## 🔧 Configuração

### appsettings.json

```json
{
  "OCR": {
    "Provider": "Selector",
    "AzureVision": {
      "Endpoint": "https://appfitnes.cognitiveservices.azure.com/",
      "ApiKey": "YOUR_KEY_HERE",
      "Language": "pt",
      "TimeoutSeconds": 30,
      "EnableDetailedLogging": true
    }
  },
  "AzureOpenAiVision": {
    "Endpoint": "https://aihca.openai.azure.com/",
    "ApiKey": "YOUR_KEY_HERE",
    "VisionDeployment": "gpt-4.1"
  }
}
```

### Injeção de Dependência

```csharp
// ServiceCollectionExtensions.cs
services.AddScoped<IHybridOcrValidator>(sp =>
{
    var config = configuration.GetSection("OCR:AzureVision");
    var endpoint = config["Endpoint"];
    var apiKey = config["ApiKey"];
    var logger = sp.GetRequiredService<ILogger<HybridOcrValidator>>();

    var azureVisionOcr = new AzureComputerVisionOcrProvider(
        endpoint,
        apiKey,
        sp.GetService<ILogger<AzureComputerVisionOcrProvider>>());

    return new HybridOcrValidator(azureVisionOcr, logger);
});
```

## 📝 Exemplo de Resposta da API

```json
{
  "success": true,
  "analysis": {
    "productName": "Cookies de Chocolate",
    "category": "biscoito",
    "calories": 519,
    "protein": 5.2,
    "fat": 25.0,
    "carbs": 64.0,
    "sodium": 450,
    "dataSource": {
      "ValidationMethod": "Azure Computer Vision OCR",
      "CaloriesSource": "Azure Computer Vision OCR (corrected)"
    }
  },
  "enriched": {
    "validationWarnings": [
      "⚠️ Calorias corrigidas de 436 para 519 kcal usando OCR de validação",
      "⚠️ Proteínas corrigido de 6.1 para 5.2 usando OCR de validação"
    ],
    "confidence": "alta",
    "fallbackUsed": false
  },
  "score": {
    "value": 35,
    "label": "Ruim",
    "principalOffender": "Alto em açúcar e gordura saturada"
  },
  "hybridOcrValidation": {
    "applied": true,
    "correctionApplied": true,
    "method": "Azure Computer Vision OCR"
  }
}
```

## 🎯 Benefícios

### 1. **Maior Precisão**
- Combina o melhor dos dois mundos:
  - IA contextual (OpenAI) para compreensão
  - OCR preciso (Computer Vision) para números

### 2. **Detecção de Erros**
- Identifica automaticamente valores incorretos
- Corrige divergências significativas (> 15%)
- Mantém transparência via warnings

### 3. **Rastreabilidade**
- Todos os valores indicam sua origem
- Logs detalhados do processo
- Flags de validação no contexto

### 4. **Fallback Inteligente**
- Se Computer Vision falhar, usa OpenAI Vision
- Se ambos falharem, aplica fallback por categoria
- Sempre retorna uma análise válida

## 📈 Métricas e Logs

### Logs de Validação

```
[Pipeline.Stage2c] ┌──────────────────────────────────────────┐
[Pipeline.Stage2c] │  VALIDAÇÃO HÍBRIDA OCR (Azure Vision)   │
[Pipeline.Stage2c] └──────────────────────────────────────────┘
[HYBRID_OCR] Starting validation with Azure Computer Vision
[HYBRID_OCR] OCR extracted 12 lines with confidence 98.50%
[HYBRID_OCR] Calories divergence detected: AI=436, OCR=519, Divergence=19.04%
[HYBRID_OCR] Protein divergence detected: AI=6.1, OCR=5.2, Divergence=14.75%
[HYBRID_OCR] ✅ Corrections applied successfully
[Pipeline.Stage2c] ✅ Correções aplicadas via Computer Vision OCR
[Pipeline.Stage2c] 📊 Valores corrigidos:
[Pipeline.Stage2c]    • Calorias: 519 kcal
[Pipeline.Stage2c]    • Proteína: 5.2 g
[Pipeline.Stage2c]    • Gordura: 25.0 g
[Pipeline.Stage2c]    • Carboidratos: 64.0 g
[Pipeline.Stage2c]    • Sódio: 450 mg
```

## 🔒 Segurança e Confiabilidade

### Tratamento de Erros
- Try-catch em todas as operações
- Fallback para valores originais em caso de erro
- Logs detalhados de exceções

### Limpeza de Recursos
- Arquivos temporários sempre deletados
- Finally blocks garantem cleanup
- Sem vazamento de recursos

### Validação de Entrada
- Verifica se profile não é nulo
- Valida path da imagem
- Garante que os serviços estão configurados

## 🚀 Próximos Passos

1. **Métricas de Performance**
   - Adicionar telemetria para medir tempo de validação
   - Rastrear taxa de correções aplicadas

2. **Threshold Dinâmico**
   - Ajustar threshold baseado em categoria
   - Diferentes limites para diferentes nutrientes

3. **Machine Learning**
   - Treinar modelo para prever quando validação é necessária
   - Otimizar chamadas ao Computer Vision

4. **Testes A/B**
   - Comparar resultados com/sem validação
   - Medir impacto na precisão geral

## 📚 Referências

- [Azure OpenAI Vision Documentation](https://learn.microsoft.com/azure/ai-services/openai/gpt-v-quickstart)
- [Azure Computer Vision OCR](https://learn.microsoft.com/azure/ai-services/computer-vision/overview-ocr)
- [.NET Dependency Injection](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection)
