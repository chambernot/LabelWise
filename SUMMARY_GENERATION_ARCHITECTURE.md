# Refatoração: Summary Generation Strategy Pattern

## 📋 Visão Geral

Esta refatoração introduz um padrão **Strategy** para geração de resumos de análise de produtos, permitindo alternar facilmente entre implementações baseadas em **regras** e **IA generativa**.

## 🏗️ Arquitetura

### Componentes Principais

```
LabelWise.Application/
├── Interfaces/
│   └── IAnalysisSummaryGenerator.cs          # Interface principal
├── SummaryGeneration/
│   ├── RuleBasedSummaryGenerator.cs          # Implementação atual (regras)
│   ├── AiSummaryGenerator.cs                  # Implementação futura (IA)
│   └── SummaryGeneratorFactory.cs            # Factory para Strategy Pattern
├── Configuration/
│   └── SummaryGenerationOptions.cs           # Configurações
└── Extensions/
    └── ServiceCollectionExtensions.cs        # Registro DI atualizado
```

## 🎯 Padrões Utilizados

### 1. **Strategy Pattern**
Permite alternar algoritmos de geração de resumo em tempo de execução sem modificar código:
- `IAnalysisSummaryGenerator` - Interface da estratégia
- `RuleBasedSummaryGenerator` - Estratégia concreta #1
- `AiSummaryGenerator` - Estratégia concreta #2

### 2. **Factory Pattern**
`SummaryGeneratorFactory` cria a instância apropriada baseada em configuração:
```csharp
var factory = serviceProvider.GetRequiredService<SummaryGeneratorFactory>();
var generator = factory.CreateGenerator(); // Resolve baseado em appsettings
```

### 3. **Dependency Injection**
Totalmente integrado com DI do .NET:
```csharp
services.AddScoped<IAnalysisSummaryGenerator>(sp => {
    var factory = sp.GetRequiredService<SummaryGeneratorFactory>();
    return factory.CreateGenerator();
});
```

## 🔧 Como Usar

### Configuração (appsettings.json)

#### Modo Rule-Based (Atual)
```json
{
  "SummaryGeneration": {
    "Strategy": "RuleBased"
  }
}
```

#### Modo AI-Powered (Futuro)
```json
{
  "SummaryGeneration": {
    "Strategy": "AiPowered",
    "EnableFallback": true,
    "AiTimeoutSeconds": 10,
    "AiProvider": {
      "Provider": "AzureOpenAI",
      "Endpoint": "https://your-resource.openai.azure.com/",
      "ApiKey": "your-api-key-here",
      "ModelName": "gpt-4",
      "Temperature": 0.7,
      "MaxTokens": 200
    }
  }
}
```

### Injeção em Serviços

```csharp
public class MyService
{
    private readonly IAnalysisSummaryGenerator _summaryGenerator;

    public MyService(IAnalysisSummaryGenerator summaryGenerator)
    {
        _summaryGenerator = summaryGenerator;
    }

    public string GenerateSummary(...)
    {
        return _summaryGenerator.GenerateSummary(...);
    }
}
```

### Uso Direto da Factory (Cenários Avançados)

```csharp
public class AdvancedService
{
    private readonly SummaryGeneratorFactory _factory;

    public AdvancedService(SummaryGeneratorFactory factory)
    {
        _factory = factory;
    }

    public string CompareStrategies(...)
    {
        // Gera com regras
        var ruleBasedGen = _factory.CreateGenerator(SummaryGenerationStrategy.RuleBased);
        var ruleSummary = ruleBasedGen.GenerateSummary(...);

        // Gera com IA
        var aiGen = _factory.CreateGenerator(SummaryGenerationStrategy.AiPowered);
        var aiSummary = aiGen.GenerateSummary(...);

        return $"Regra: {ruleSummary}\nIA: {aiSummary}";
    }
}
```

## 🚀 Roadmap de Implementação IA

### Fase 1: ✅ Estrutura (Completa)
- [x] Interface `IAnalysisSummaryGenerator`
- [x] `RuleBasedSummaryGenerator` (migração do código atual)
- [x] `AiSummaryGenerator` (placeholder)
- [x] `SummaryGeneratorFactory`
- [x] Configuração e DI

### Fase 2: 🔄 Implementação IA (Próximos Passos)
1. **Escolher Provedor**
   - Azure OpenAI (recomendado para enterprise)
   - OpenAI API
   - Outros (Anthropic, etc.)

2. **Implementar `IAiProviderService`**
   ```csharp
   public interface IAiProviderService
   {
       Task<string> GenerateCompletionAsync(string prompt);
   }
   ```

3. **Criar Implementações Concretas**
   - `AzureOpenAiProvider`
   - `OpenAiProvider`

4. **Completar `AiSummaryGenerator`**
   - Descomentar código de chamada à IA
   - Implementar fallback para RuleBased em caso de erro
   - Adicionar retry logic
   - Implementar caching (opcional)

5. **Adicionar Pacotes NuGet**
   ```bash
   dotnet add package Azure.AI.OpenAI
   # ou
   dotnet add package OpenAI
   ```

### Fase 3: 🧪 Testes e Otimização
- [ ] Testes unitários para ambas estratégias
- [ ] Testes de integração com IA real
- [ ] Monitoramento de custos e latência
- [ ] A/B testing entre estratégias

## 📊 Vantagens da Arquitetura

### ✅ Desacoplamento
- Lógica de geração de resumo isolada
- Fácil substituição de implementações
- Testabilidade individual de cada estratégia

### ✅ Flexibilidade
- Alternar estratégias via configuração (sem deploy)
- Múltiplas implementações coexistem
- Possibilidade de estratégias híbridas

### ✅ Escalabilidade
- Adicionar novas estratégias sem modificar código existente
- Suporta múltiplos provedores de IA
- Preparado para cenários complexos (fallback, cache, etc.)

### ✅ Manutenibilidade
- Responsabilidades bem definidas
- Código limpo e SOLID
- Facilita onboarding de novos desenvolvedores

## 🔍 Exemplos de Uso

### Exemplo 1: Resumo Rule-Based (Atual)
```
**Boa Escolha** • Score Geral: 70% | Score Personalizado: 65% • 
Alto em calorias (450 kcal), Alto teor de açúcar (25g) • 
⚠️ 2 alerta(s) identificado(s) • 💡 3 recomendação(ões) disponível(is)
```

### Exemplo 2: Resumo AI-Powered (Futuro)
```
Este produto apresenta valor calórico elevado e açúcar acima do recomendado 
para consumo diário. Apesar disso, é uma boa fonte de proteínas. 
Considere consumir com moderação, especialmente se seu objetivo é 
redução de peso.
```

## 🎓 Próximos Passos para Desenvolvedores

### Para Integrar Azure OpenAI:

1. **Provisionar recurso Azure OpenAI**
   ```bash
   az cognitiveservices account create \
     --name labelwise-openai \
     --resource-group labelwise-rg \
     --kind OpenAI \
     --sku S0 \
     --location eastus
   ```

2. **Criar implementação do provider**
   ```csharp
   public class AzureOpenAiProvider : IAiProviderService
   {
       private readonly OpenAIClient _client;
       
       public async Task<string> GenerateCompletionAsync(string prompt)
       {
           var response = await _client.GetChatCompletionsAsync(
               new ChatCompletionsOptions
               {
                   Messages = { new ChatMessage(ChatRole.User, prompt) },
                   MaxTokens = 200,
                   Temperature = 0.7f
               }
           );
           return response.Value.Choices[0].Message.Content;
       }
   }
   ```

3. **Registrar no DI**
   ```csharp
   services.AddScoped<IAiProviderService, AzureOpenAiProvider>();
   ```

4. **Atualizar configuração**
   ```json
   {
     "SummaryGeneration": {
       "Strategy": "AiPowered"
     }
   }
   ```

## 📝 Notas Técnicas

- **Thread-Safety**: Todas as implementações são stateless e thread-safe
- **Performance**: RuleBased é síncrono e rápido (~1ms); AI será assíncrono (~500-2000ms)
- **Custos**: RuleBased é gratuito; AI tem custo por token (monitorar!)
- **Fallback**: Recomendado habilitar fallback para RuleBased quando usar IA

## 🤝 Contribuindo

Para adicionar nova estratégia:

1. Implemente `IAnalysisSummaryGenerator`
2. Adicione enum em `SummaryGenerationStrategy`
3. Registre no DI em `ServiceCollectionExtensions`
4. Atualize factory em `SummaryGeneratorFactory`
5. Documente configuração necessária

---

**Autor**: Arquitetura LabelWise  
**Data**: 2025  
**Versão**: 1.0
