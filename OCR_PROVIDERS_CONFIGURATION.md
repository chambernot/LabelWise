# Configuração de OCR Providers

## Providers Disponíveis

O LabelWise suporta múltiplos provedores de OCR. Configure o provider desejado no arquivo `ServiceCollectionExtensions.cs` da camada Infrastructure.

### 1. MockOcrProvider (Desenvolvimento - ATIVO)
**Status**: ✅ Ativo  
**Localização**: `LabelWise.Infrastructure/Ocr/MockOcrProvider.cs`

**Descrição**: Provider simulado para desenvolvimento e testes. Retorna dados realistas de rótulos nutricionais brasileiros.

**Quando usar**:
- Desenvolvimento local
- Testes automatizados
- Quando não há acesso a APIs de OCR externas

**Configuração** (já ativa):
```csharp
services.AddSingleton<IOcrProvider, MockOcrProvider>();
```

**Vantagens**:
- Não requer configuração externa
- Funciona offline
- Retorna dados consistentes

**Desvantagens**:
- Não processa imagens reais
- Limitado a cenários simulados

---

### 2. TesseractOcrProvider (Local - REAL)
**Status**: 🔧 Requer configuração  
**Localização**: `LabelWise.Infrastructure/Ocr/TesseractOcrProvider.cs`

**Descrição**: Provider local usando Tesseract OCR. Processa imagens reais sem custo de APIs externas.

**Requisitos**:
1. Instalar pacote NuGet:
   ```bash
   dotnet add package Tesseract --version 5.2.0
   ```

2. Baixar dados de idioma (tessdata):
   - Acessar: https://github.com/tesseract-ocr/tessdata
   - Baixar `por.traineddata` (Português) e `eng.traineddata` (Inglês)
   - Criar pasta: `C:\tessdata` ou `./tessdata` no projeto
   - Copiar os arquivos `.traineddata` para esta pasta

3. Configurar variável de ambiente (opcional):
   ```bash
   set TESSDATA_PREFIX=C:\tessdata
   ```

**Ativação no código**:
```csharp
// Em LabelWise.Infrastructure/Extensions/ServiceCollectionExtensions.cs
// Substituir:
services.AddSingleton<IOcrProvider, MockOcrProvider>();

// Por:
services.AddSingleton<IOcrProvider>(sp => 
    new TesseractOcrProvider("C:\\tessdata")); // ou caminho personalizado
```

**Vantagens**:
- Processa imagens reais
- Funciona offline
- Sem custos de API
- Boa precisão para textos impressos

**Desvantagens**:
- Requer instalação de dependências
- Pode ter precisão menor que soluções cloud
- Necessita configuração inicial

---

### 3. AzureComputerVisionOcrProvider (Cloud - REAL)
**Status**: 🔧 Requer configuração  
**Localização**: `LabelWise.Infrastructure/Ocr/AzureComputerVisionOcrProvider.cs`

**Descrição**: Provider cloud usando Azure Computer Vision. Máxima precisão para OCR.

**Requisitos**:
1. Criar recurso Azure Computer Vision:
   - Portal Azure: https://portal.azure.com
   - Criar recurso "Computer Vision"
   - Obter endpoint e API key

2. Instalar pacote NuGet:
   ```bash
   dotnet add package Azure.AI.Vision.ImageAnalysis --version 1.0.0
   ```

3. Configurar appsettings.json:
   ```json
   {
     "Azure": {
       "ComputerVision": {
         "Endpoint": "https://seu-recurso.cognitiveservices.azure.com/",
         "ApiKey": "sua-api-key-aqui"
       }
     }
   }
   ```

**Ativação no código**:
```csharp
// Em LabelWise.Infrastructure/Extensions/ServiceCollectionExtensions.cs
var azureEndpoint = configuration["Azure:ComputerVision:Endpoint"];
var azureApiKey = configuration["Azure:ComputerVision:ApiKey"];

services.AddSingleton<IOcrProvider>(sp => 
    new AzureComputerVisionOcrProvider(azureEndpoint, azureApiKey));
```

**Vantagens**:
- Máxima precisão de OCR
- Suporte a múltiplos idiomas
- Reconhecimento de handwriting
- Escalabilidade automática
- Sem manutenção de infraestrutura

**Desvantagens**:
- Requer conexão com internet
- Custos por requisição
- Dependência de serviço externo

**Preços** (Azure Computer Vision - Free Tier):
- 5.000 transações gratuitas/mês
- Depois: ~$1.00 por 1.000 transações

---

## Como Trocar o Provider

### Passo 1: Escolher o Provider
Decida qual provider usar baseado em:
- **Desenvolvimento**: MockOcrProvider
- **Produção (baixo custo)**: TesseractOcrProvider
- **Produção (máxima qualidade)**: AzureComputerVisionOcrProvider

### Passo 2: Instalar Dependências
Siga os requisitos do provider escolhido (pacotes NuGet, arquivos de dados, etc).

### Passo 3: Configurar no ServiceCollectionExtensions
Editar: `LabelWise.Infrastructure/Extensions/ServiceCollectionExtensions.cs`

```csharp
public static IServiceCollection AddInfrastructureServices(
    this IServiceCollection services, 
    IConfiguration configuration)
{
    // ... outras configurações ...

    // ===== ESCOLHA UM DOS PROVIDERS ABAIXO =====

    // OPÇÃO 1: Mock (Desenvolvimento) - ATIVO
    services.AddSingleton<IOcrProvider, MockOcrProvider>();

    // OPÇÃO 2: Tesseract (Local Real)
    // services.AddSingleton<IOcrProvider>(sp => 
    //     new TesseractOcrProvider("C:\\tessdata"));

    // OPÇÃO 3: Azure Computer Vision (Cloud Real)
    // var azureEndpoint = configuration["Azure:ComputerVision:Endpoint"];
    // var azureApiKey = configuration["Azure:ComputerVision:ApiKey"];
    // services.AddSingleton<IOcrProvider>(sp => 
    //     new AzureComputerVisionOcrProvider(azureEndpoint, azureApiKey));

    // ... resto da configuração ...
}
```

### Passo 4: Testar
Execute a API e teste o endpoint:
```bash
POST /api/products/analyze-image
Content-Type: multipart/form-data

file: [sua-imagem.jpg]
```

---

## Implementando um Provider Customizado

Para criar seu próprio provider OCR:

### 1. Criar classe que implementa IOcrProvider
```csharp
public class MeuOcrProvider : IOcrProvider
{
    public string ProviderName => "Meu Provider Customizado";

    public async Task<OcrResultDto> ExtractTextAsync(OcrRequestDto request)
    {
        // Sua implementação aqui
        var text = await ExtrairTextoAsync(request.ImagePath);
        
        return new OcrResultDto
        {
            RawText = text,
            Confidence = 0.9,
            Success = true,
            TextBlocks = new List<OcrTextBlock>()
        };
    }

    public Task<bool> IsAvailableAsync()
    {
        return Task.FromResult(true);
    }
}
```

### 2. Registrar no DI
```csharp
services.AddSingleton<IOcrProvider, MeuOcrProvider>();
```

---

## Providers Recomendados por Cenário

| Cenário | Provider Recomendado | Motivo |
|---------|---------------------|--------|
| Desenvolvimento local | MockOcrProvider | Rápido, sem setup |
| CI/CD e testes | MockOcrProvider | Determinístico |
| Produção (budget limitado) | TesseractOcrProvider | Sem custos recorrentes |
| Produção (alta qualidade) | AzureComputerVisionOcrProvider | Melhor precisão |
| MVP inicial | MockOcrProvider | Deploy rápido |
| Escala empresarial | AzureComputerVisionOcrProvider | Escalabilidade |

---

## Troubleshooting

### MockOcrProvider não está retornando dados
- Verificar se o arquivo de imagem está sendo salvo corretamente
- Verificar logs no console

### TesseractOcrProvider falha com "tessdata not found"
- Verificar se a pasta tessdata existe
- Verificar se os arquivos .traineddata estão presentes
- Confirmar o caminho configurado

### AzureComputerVisionOcrProvider retorna erro 401
- Verificar se a API key está correta
- Verificar se o endpoint está correto
- Verificar se o recurso Azure está ativo

### OCR retorna texto vazio
- Verificar qualidade da imagem
- Confirmar que a imagem contém texto legível
- Testar com imagens de exemplo conhecidas

---

## Status Atual do Projeto

✅ **Implementado e Funcionando**:
- MockOcrProvider (ativo)
- TesseractOcrProvider (estrutura pronta)
- AzureComputerVisionOcrProvider (estrutura pronta)
- Interface IOcrProvider
- Integração com pipeline completo

🔄 **Próximos Passos Sugeridos**:
1. Implementar TesseractOcrProvider completamente (para uso local real)
2. Implementar AzureComputerVisionOcrProvider completamente (para produção)
3. Adicionar testes unitários para cada provider
4. Configurar fallback entre providers (ex: Azure -> Tesseract -> Mock)

---

## Exemplo de Uso

```csharp
// O provider é injetado automaticamente
public class ProductAnalysisPipelineOrchestrator
{
    private readonly IOcrProvider _ocrProvider;

    public ProductAnalysisPipelineOrchestrator(IOcrProvider ocrProvider)
    {
        _ocrProvider = ocrProvider;
    }

    public async Task<string> ProcessImageAsync(string imagePath)
    {
        var request = new OcrRequestDto
        {
            ImagePath = imagePath,
            FileName = Path.GetFileName(imagePath)
        };

        var result = await _ocrProvider.ExtractTextAsync(request);
        
        if (result.Success)
        {
            return result.RawText;
        }
        else
        {
            throw new Exception($"OCR failed: {result.ErrorMessage}");
        }
    }
}
```

---

**Última atualização**: 2024  
**Versão do documento**: 1.0
