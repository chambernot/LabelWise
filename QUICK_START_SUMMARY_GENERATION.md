# ⚡ Guia Rápido: Summary Generation

## 🎯 O Que Foi Feito

Refatoração do sistema de geração de resumos de análise usando **Strategy Pattern**, permitindo alternar entre:
- ✅ **Rule-Based** (atual, funcionando)
- 🔄 **AI-Powered** (estrutura pronta)

## 📦 Componentes Principais

```csharp
// Interface
IAnalysisSummaryGenerator

// Implementações
RuleBasedSummaryGenerator    // ✅ Pronta
AiSummaryGenerator            // 🔄 Placeholder

// Factory
SummaryGeneratorFactory

// Configuração
SummaryGenerationOptions
```

## 🚀 Uso Imediato

### 1. Injeção Automática (Recomendado)

```csharp
public class MeuController
{
    private readonly IProductAnalysisEngine _engine;
    
    public MeuController(IProductAnalysisEngine engine)
    {
        _engine = engine; // Já vem com gerador configurado
    }
    
    public ProductAnalysisResultDto Analisar(...)
    {
        var result = _engine.Analyze(...);
        // result.Summary já contém o resumo gerado!
        return result;
    }
}
```

### 2. Injeção Direta do Gerador

```csharp
public class MeuServico
{
    private readonly IAnalysisSummaryGenerator _summaryGenerator;
    
    public MeuServico(IAnalysisSummaryGenerator summaryGenerator)
    {
        _summaryGenerator = summaryGenerator;
    }
    
    public string GerarResumo(...)
    {
        return _summaryGenerator.GenerateSummary(
            product, nutrition, ingredients, allergens,
            userProfile, generalScore, personalizedScore,
            alerts, recommendations
        );
    }
}
```

### 3. Uso da Factory (Cenários Avançados)

```csharp
public class ComparacaoService
{
    private readonly SummaryGeneratorFactory _factory;
    
    public ComparacaoService(SummaryGeneratorFactory factory)
    {
        _factory = factory;
    }
    
    public void CompararEstrategias(...)
    {
        // Força uso de regras
        var ruleGen = _factory.CreateGenerator(SummaryGenerationStrategy.RuleBased);
        var resumoRegra = ruleGen.GenerateSummary(...);
        
        // Força uso de IA
        var aiGen = _factory.CreateGenerator(SummaryGenerationStrategy.AiPowered);
        var resumoIA = aiGen.GenerateSummary(...);
    }
}
```

## ⚙️ Configuração

### Modo Atual (Rule-Based)

**appsettings.json**:
```json
{
  "SummaryGeneration": {
    "Strategy": "RuleBased"
  }
}
```

**Ou simplesmente omita a configuração** (RuleBased é o padrão).

### Futuro (AI-Powered)

```json
{
  "SummaryGeneration": {
    "Strategy": "AiPowered",
    "EnableFallback": true,
    "AiTimeoutSeconds": 10,
    "AiProvider": {
      "Provider": "AzureOpenAI",
      "Endpoint": "https://your-resource.openai.azure.com/",
      "ApiKey": "your-api-key",
      "ModelName": "gpt-4",
      "Temperature": 0.7,
      "MaxTokens": 200
    }
  }
}
```

## 🔄 Trocar Estratégia

**Sem redeployprecisa recompilar**:
1. Edite `appsettings.json`
2. Altere `Strategy` para `"RuleBased"` ou `"AiPowered"`
3. Reinicie a aplicação

## 📋 Checklist de Implementação IA

Quando estiver pronto para implementar IA:

- [ ] **1. Decidir provedor**
  - [ ] Azure OpenAI (recomendado)
  - [ ] OpenAI API
  - [ ] Outro

- [ ] **2. Provisionar recurso**
  ```bash
  az cognitiveservices account create \
    --name labelwise-openai \
    --resource-group labelwise-rg \
    --kind OpenAI
  ```

- [ ] **3. Adicionar pacote NuGet**
  ```bash
  cd LabelWise.Infrastructure
  dotnet add package Azure.AI.OpenAI
  ```

- [ ] **4. Implementar provider**
  - [ ] Abrir `LabelWise.Infrastructure/AI/AzureOpenAiProvider.cs`
  - [ ] Descomentar código
  - [ ] Adicionar using statements

- [ ] **5. Registrar no DI**
  ```csharp
  // Em Infrastructure/Extensions/ServiceCollectionExtensions.cs
  services.AddScoped<IAiProviderService, AzureOpenAiProvider>();
  ```

- [ ] **6. Configurar credenciais**
  - [ ] Adicionar ao Azure Key Vault (produção)
  - [ ] Ou appsettings.Development.json (dev)

- [ ] **7. Atualizar AiSummaryGenerator**
  - [ ] Descomentar código de chamada à IA
  - [ ] Implementar tratamento de erros

- [ ] **8. Testar**
  - [ ] Teste unitário
  - [ ] Teste de integração
  - [ ] Teste de custos (!)

- [ ] **9. Deploy gradual**
  - [ ] Staging primeiro
  - [ ] Monitorar custos
  - [ ] A/B testing
  - [ ] Produção

## 📊 Exemplo de Saída

### Rule-Based (Atual)
```
**Boa Escolha** • Score Geral: 70% | Score Personalizado: 65% • 
Alto em calorias (450 kcal), Alto teor de açúcar (25g) • 
⚠️ 2 alerta(s) identificado(s) • 💡 3 recomendação(ões) disponível(is) • 
Analisado para: WeightLoss
```

### AI-Powered (Futuro)
```
Este produto apresenta valor calórico elevado (450 kcal) e alto teor 
de açúcar (25g), não sendo ideal para objetivo de perda de peso. 
Recomenda-se consumo moderado e atenção às porções.
```

## 🐛 Troubleshooting

### "O gerador não está sendo injetado"
```csharp
// Certifique-se de que está chamando:
services.AddApplicationServices(); // Em Startup/Program.cs
```

### "Strategy não muda"
- Verifique que está editando o `appsettings.json` correto
- Reinicie a aplicação
- Verifique logs de startup

### "BindConfiguration not found"
- Removido (não necessário no .NET 10)
- Factory lê configuração diretamente

## 📚 Documentação Completa

- **Arquitetura detalhada**: `SUMMARY_GENERATION_ARCHITECTURE.md`
- **Exemplos práticos**: `SUMMARY_GENERATION_EXAMPLES.cs`
- **Resumo completo**: `REFACTORING_COMPLETE_SUMMARY.md`
- **Config exemplo**: `LabelWise.Shared/appsettings.SummaryGeneration.json`

## 💬 FAQ

**P: Posso usar ambas estratégias ao mesmo tempo?**  
R: Sim! Use `SummaryGeneratorFactory` para criar instâncias específicas.

**P: Como faço A/B testing?**  
R: Veja exemplo #2 em `SUMMARY_GENERATION_EXAMPLES.cs`

**P: Quanto custa usar IA?**  
R: ~$0.021 por análise com GPT-4. Veja análise detalhada em `AzureOpenAiProvider.cs`

**P: Posso usar outro provedor de IA?**  
R: Sim! Implemente `IAiProviderService` para qualquer provedor.

**P: E se a IA falhar?**  
R: Configure `EnableFallback: true` para usar RuleBased automaticamente.

## ✅ Status

- **Build**: ✅ Sucesso
- **Testes**: ⏳ Pendente
- **Produção**: ✅ Pronto (Rule-Based)
- **IA**: 🔄 Estrutura pronta

---

**Dúvidas?** Consulte a documentação completa!
