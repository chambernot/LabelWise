# 🚀 Azure Computer Vision OCR - Guia Completo de Implementação

## 📋 Índice

1. [Visão Geral](#visão-geral)
2. [Pré-requisitos](#pré-requisitos)
3. [Instalação do Pacote NuGet](#instalação-do-pacote-nuget)
4. [Criação do Recurso no Azure](#criação-do-recurso-no-azure)
5. [Configuração](#configuração)
6. [Modos de Uso](#modos-de-uso)
7. [Estratégia Composite (Recomendado)](#estratégia-composite-recomendado)
8. [Comparação de Providers](#comparação-de-providers)
9. [Custos](#custos)
10. [Troubleshooting](#troubleshooting)
11. [Exemplos de Código](#exemplos-de-código)

---

## 🎯 Visão Geral

O LabelWise agora suporta **Azure Computer Vision OCR** como provider de extração de texto de imagens. Esta implementação oferece:

✅ **Alta precisão** - Especialmente em imagens de baixa qualidade  
✅ **Sem instalação local** - API gerenciada pela Microsoft  
✅ **Múltiplos idiomas** - Suporte nativo para português  
✅ **Fallback inteligente** - Combina Azure + Tesseract para máxima precisão  
✅ **Custo otimizado** - Free tier de 5.000 transações/mês  

### Arquitetura

```
┌─────────────────────────────────────────────────────┐
│          CompositeOcrProvider (Recomendado)        │
│                                                     │
│  ┌──────────────────┐      ┌──────────────────┐  │
│  │  Azure CV OCR    │      │  Tesseract OCR   │  │
│  │  (Primary)       │ ──>  │  (Fallback)      │  │
│  │  Alta precisão   │      │  Backup local    │  │
│  └──────────────────┘      └──────────────────┘  │
│                                                     │
│  Lógica: Se confiança < 85% → usa fallback        │
└─────────────────────────────────────────────────────┘
```

---

## 🔧 Pré-requisitos

1. **Conta Azure** (pode usar free tier)
2. **.NET 10** instalado
3. **Visual Studio 2026** ou VS Code
4. Projeto **LabelWise** clonado

---

## 📦 Instalação do Pacote NuGet

### Via Package Manager Console (Visual Studio)

```powershell
# No projeto LabelWise.Infrastructure
Install-Package Azure.AI.Vision.ImageAnalysis -Version 1.0.0
```

### Via .NET CLI

```bash
# Na pasta LabelWise.Infrastructure
dotnet add package Azure.AI.Vision.ImageAnalysis --version 1.0.0
```

### Verificar Instalação

```bash
dotnet list package | findstr Azure.AI.Vision
```

**Saída esperada:**
```
> Azure.AI.Vision.ImageAnalysis    1.0.0
```

---

## ☁️ Criação do Recurso no Azure

### Opção 1: Via Portal Azure (Interface Gráfica)

#### Passo 1: Acessar o Portal

1. Acesse: https://portal.azure.com
2. Faça login com sua conta Microsoft/Azure

#### Passo 2: Criar Recurso

1. Clique em **"Criar um recurso"**
2. Busque por: **"Computer Vision"**
3. Selecione **"Computer Vision"** da Microsoft
4. Clique em **"Criar"**

#### Passo 3: Configurar o Recurso

Preencha os campos:

| Campo | Valor Recomendado |
|-------|-------------------|
| **Assinatura** | Sua assinatura ativa |
| **Grupo de recursos** | `rg-labelwise` (criar novo se não existir) |
| **Região** | `Brazil South` (São Paulo) |
| **Nome** | `labelwise-ocr-cv` (deve ser único globalmente) |
| **Camada de preços** | `F0 (Free)` - 5k chamadas/mês grátis |

> **💡 Dica:** Para produção, use `S1 (Standard)` - $1 por 1.000 transações

4. Clique em **"Revisar + criar"**
5. Aguarde validação
6. Clique em **"Criar"**

#### Passo 4: Aguardar Deployment

- Tempo: ~1-2 minutos
- Quando concluído, clique em **"Ir para o recurso"**

#### Passo 5: Obter Endpoint e Chave

1. No menu lateral, clique em **"Chaves e Ponto de Extremidade"**
2. Copie:
   - **KEY 1** (ou KEY 2) → Sua API Key
   - **Endpoint** → URL do seu recurso

**Exemplo:**
```
Endpoint: https://labelwise-ocr-cv.cognitiveservices.azure.com/
Key: 1a2b3c4d5e6f7g8h9i0j1k2l3m4n5o6p
```

---

### Opção 2: Via Azure CLI (Linha de Comando)

#### Instalar Azure CLI

```powershell
# Windows (via Winget)
winget install Microsoft.AzureCLI

# Ou baixar: https://aka.ms/installazurecliwindows
```

#### Criar Recurso

```bash
# Login no Azure
az login

# Criar grupo de recursos (se não existir)
az group create --name rg-labelwise --location brazilsouth

# Criar recurso Computer Vision (FREE TIER)
az cognitiveservices account create \
  --name labelwise-ocr-cv \
  --resource-group rg-labelwise \
  --kind ComputerVision \
  --sku F0 \
  --location brazilsouth \
  --yes

# Obter endpoint
az cognitiveservices account show \
  --name labelwise-ocr-cv \
  --resource-group rg-labelwise \
  --query "properties.endpoint" -o tsv

# Obter chave
az cognitiveservices account keys list \
  --name labelwise-ocr-cv \
  --resource-group rg-labelwise \
  --query "key1" -o tsv
```

**Saída esperada:**
```
https://labelwise-ocr-cv.cognitiveservices.azure.com/
1a2b3c4d5e6f7g8h9i0j1k2l3m4n5o6p
```

---

## ⚙️ Configuração

### Opção 1: Apenas Azure Computer Vision

Edite `LabelWise.Api/appsettings.json`:

```json
{
  "OCR": {
    "Provider": "AzureComputerVision",
    "Azure": {
      "Endpoint": "https://labelwise-ocr-cv.cognitiveservices.azure.com/",
      "ApiKey": "1a2b3c4d5e6f7g8h9i0j1k2l3m4n5o6p",
      "ValidateOnStartup": true
    }
  }
}
```

### Opção 2: Composite (Azure + Tesseract) - **RECOMENDADO** ⭐

```json
{
  "OCR": {
    "Provider": "Composite",
    "Azure": {
      "Endpoint": "https://labelwise-ocr-cv.cognitiveservices.azure.com/",
      "ApiKey": "1a2b3c4d5e6f7g8h9i0j1k2l3m4n5o6p",
      "ValidateOnStartup": false
    },
    "TessdataPath": null,
    "Language": "por+eng",
    "Composite": {
      "PrimaryProvider": "AzureComputerVision",
      "FallbackProvider": "Tesseract",
      "ConfidenceThreshold": 0.85
    }
  }
}
```

### Opção 3: Apenas Tesseract (Sem Azure)

```json
{
  "OCR": {
    "Provider": "Tesseract",
    "TessdataPath": null,
    "Language": "por+eng",
    "ValidateOnStartup": true
  }
}
```

### Opção 4: Mock (Desenvolvimento)

```json
{
  "OCR": {
    "Provider": "Mock",
    "UseMockProvider": true
  }
}
```

---

## 🎯 Modos de Uso

### 1. Azure Computer Vision (Standalone)

**Quando usar:**
- Você tem API key do Azure
- Quer máxima precisão
- Não quer instalar Tesseract localmente

**Vantagens:**
- ✅ Alta precisão
- ✅ Sem instalação local
- ✅ Múltiplos idiomas nativos

**Desvantagens:**
- ❌ Custo por uso (após 5k grátis)
- ❌ Requer conexão internet
- ❌ Depende de serviço externo

**Configuração:**
```json
{
  "OCR": {
    "Provider": "AzureComputerVision",
    "Azure": {
      "Endpoint": "https://seu-recurso.cognitiveservices.azure.com/",
      "ApiKey": "sua-chave-aqui"
    }
  }
}
```

---

### 2. Tesseract (Standalone)

**Quando usar:**
- Sem acesso ao Azure
- Quer solução 100% local
- Custo zero absoluto

**Vantagens:**
- ✅ Totalmente grátis
- ✅ Funciona offline
- ✅ Sem limites de uso

**Desvantagens:**
- ❌ Precisão menor em imagens ruins
- ❌ Requer instalação local
- ❌ Mais configuração inicial

**Configuração:**
```json
{
  "OCR": {
    "Provider": "Tesseract",
    "TessdataPath": null,
    "Language": "por+eng"
  }
}
```

---

### 3. Composite (Azure + Tesseract) - **RECOMENDADO** ⭐

**Quando usar:**
- Produção real
- Quer máxima precisão + resiliência
- Custo otimizado

**Como funciona:**

```
┌──────────────────────────────────────────┐
│ 1. Tenta Azure Computer Vision          │
│    ↓                                     │
│ 2. Confiança >= 85%?                     │
│    ├─ SIM: Retorna resultado Azure      │
│    └─ NÃO: Tenta Tesseract também       │
│       ↓                                  │
│    3. Compara resultados                 │
│    4. Retorna o melhor                   │
└──────────────────────────────────────────┘
```

**Vantagens:**
- ✅ Melhor dos dois mundos
- ✅ Fallback automático
- ✅ Custo otimizado (usa Azure só quando precisa)
- ✅ Máxima precisão

**Configuração:**
```json
{
  "OCR": {
    "Provider": "Composite",
    "Azure": {
      "Endpoint": "https://seu-recurso.cognitiveservices.azure.com/",
      "ApiKey": "sua-chave-aqui"
    },
    "Composite": {
      "PrimaryProvider": "AzureComputerVision",
      "FallbackProvider": "Tesseract",
      "ConfidenceThreshold": 0.85
    }
  }
}
```

---

## 🔀 Estratégia Composite (Recomendado)

### Como Funciona

O `CompositeOcrProvider` implementa lógica inteligente de fallback:

```csharp
// Pseudo-código da estratégia
async Task<OcrResult> ExtractText(image)
{
    // 1. Tentar provider primário (Azure)
    var primaryResult = await azureOcr.Extract(image);
    
    // 2. Avaliar confiança
    if (primaryResult.Confidence >= 0.85)
    {
        return primaryResult; // Suficientemente bom
    }
    
    // 3. Tentar fallback (Tesseract)
    var fallbackResult = await tesseractOcr.Extract(image);
    
    // 4. Comparar e retornar o melhor
    return SelectBestResult(primaryResult, fallbackResult);
}
```

### Critérios de Seleção

O provider composto seleciona o melhor resultado baseado em:

1. **Confiança (70%)**: Quanto o OCR está "certo" do que leu
2. **Quantidade de texto (30%)**: Mais texto geralmente = melhor

**Fórmula:**
```
Score = (Confidence × 0.7) + (min(TextLength/500, 1.0) × 0.3)
```

### Configuração do Threshold

```json
{
  "Composite": {
    "ConfidenceThreshold": 0.85  // 85%
  }
}
```

**Recomendações:**

| Threshold | Uso Recomendado |
|-----------|-----------------|
| `0.95` (95%) | Imagens de **alta qualidade** - raramente usa fallback |
| `0.85` (85%) | **Balanceado** - usa fallback quando necessário |
| `0.70` (70%) | Imagens de **baixa qualidade** - usa fallback frequentemente |

---

## 📊 Comparação de Providers

| Característica | Azure CV | Tesseract | Composite |
|----------------|----------|-----------|-----------|
| **Precisão (imagens boas)** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Precisão (imagens ruins)** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Custo** | $1/1k após free tier | Grátis | Otimizado |
| **Requer internet** | Sim | Não | Sim (primary) |
| **Instalação local** | Não | Sim (tessdata) | Sim (fallback) |
| **Idiomas** | Automático | Manual | Ambos |
| **Resiliência** | ⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Recomendado para** | Produção cloud | Dev local | **Produção** |

---

## 💰 Custos

### Azure Computer Vision - Pricing

#### Free Tier (F0)
- **5.000 transações/mês** grátis
- **20 chamadas/minuto**
- Ideal para: Desenvolvimento, MVP, low-volume

#### Standard Tier (S1)
- **$1.00 USD** por 1.000 transações
- **10 chamadas/segundo**
- Ideal para: Produção

### Exemplos de Custo Mensal

| Imagens/mês | Custo (F0 Free) | Custo (S1 Paid) |
|-------------|-----------------|-----------------|
| 1.000 | $0 (dentro do free) | $1.00 |
| 5.000 | $0 (dentro do free) | $5.00 |
| 10.000 | $5.00 (5k extra) | $10.00 |
| 50.000 | $45.00 (45k extra) | $50.00 |
| 100.000 | $95.00 (95k extra) | $100.00 |

### Estratégia Composite: Redução de Custo

Com `ConfidenceThreshold = 0.85`:

- **~60-70%** das imagens usam apenas Azure (alta confiança)
- **~30-40%** usam ambos (baixa confiança → fallback Tesseract)

**Economia estimada:** 30-40% vs usar apenas Azure

---

## 🔍 Troubleshooting

### Erro: "Azure endpoint or API key not configured"

**Causa:** Configuração ausente no `appsettings.json`

**Solução:**
```json
{
  "OCR": {
    "Provider": "AzureComputerVision",
    "Azure": {
      "Endpoint": "https://seu-recurso.cognitiveservices.azure.com/",
      "ApiKey": "sua-chave-aqui"
    }
  }
}
```

---

### Erro: "RequestFailedException: Status 401"

**Causa:** API Key inválida ou expirada

**Solução:**
1. Verifique a chave no Portal Azure
2. Regenere se necessário:
   ```bash
   az cognitiveservices account keys regenerate \
     --name labelwise-ocr-cv \
     --resource-group rg-labelwise \
     --key-name key1
   ```

---

### Erro: "RequestFailedException: Status 403"

**Causa:** Recurso não tem permissão para a operação

**Solução:**
1. Verifique se o recurso está na região correta
2. Confirme que o SKU suporta OCR (todos os SKUs suportam)

---

### Erro: "RequestFailedException: Status 429"

**Causa:** Limite de taxa excedido (rate limit)

**Limites:**
- Free (F0): 20 chamadas/minuto
- Standard (S1): 10 chamadas/segundo

**Solução:**
1. **Curto prazo:** Espere 1 minuto e tente novamente
2. **Longo prazo:** 
   - Upgrade para S1
   - Implemente retry com exponential backoff
   - Use Composite para reduzir chamadas

---

### Problema: "Texto extraído está incompleto"

**Causa:** Imagem de baixa qualidade

**Solução:**
1. Use `CompositeOcrProvider` para fallback automático
2. Pré-processe a imagem (já implementado no Tesseract)
3. Ajuste o threshold:
   ```json
   {
     "Composite": {
       "ConfidenceThreshold": 0.70  // Mais agressivo
     }
   }
   ```

---

## 💻 Exemplos de Código

### Exemplo 1: Upload e OCR via API

```bash
# Upload de imagem para análise
curl -X POST "https://localhost:5001/api/pipeline/upload" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@rotulo.jpg"
```

**Response:**
```json
{
  "success": true,
  "ocrResult": {
    "rawText": "INFORMAÇÃO NUTRICIONAL\nPorção: 100g\nCalorias: 250kcal\n...",
    "confidence": 0.92,
    "providerMetadata": {
      "UsedProvider": "Azure Computer Vision OCR",
      "CompositeProvider": "true",
      "PrimaryProvider": "Azure Computer Vision OCR",
      "FallbackProvider": "Tesseract OCR (Local)",
      "FallbackExecuted": "false",
      "Reason": "Primary successful, fallback not needed"
    }
  }
}
```

---

### Exemplo 2: Injetar e Usar IOcrProvider

```csharp
public class MeuServico
{
    private readonly IOcrProvider _ocrProvider;
    
    public MeuServico(IOcrProvider ocrProvider)
    {
        _ocrProvider = ocrProvider;
    }
    
    public async Task<string> ExtrairTexto(string caminhoImagem)
    {
        var request = new OcrRequestDto
        {
            ImagePath = caminhoImagem,
            FileName = Path.GetFileName(caminhoImagem)
        };
        
        var result = await _ocrProvider.ExtractTextAsync(request);
        
        if (!result.Success)
        {
            throw new Exception($"OCR falhou: {result.ErrorMessage}");
        }
        
        Console.WriteLine($"Provider usado: {result.ProviderMetadata["UsedProvider"]}");
        Console.WriteLine($"Confiança: {result.Confidence:P}");
        
        return result.RawText;
    }
}
```

---

### Exemplo 3: Verificar Provider Ativo

```csharp
public class DiagnosticosController : ControllerBase
{
    private readonly IOcrProvider _ocrProvider;
    
    [HttpGet("ocr/info")]
    public IActionResult GetOcrInfo()
    {
        var metadata = _ocrProvider.GetMetadata();
        var isAvailable = await _ocrProvider.IsAvailableAsync();
        
        return Ok(new
        {
            providerName = _ocrProvider.ProviderName,
            isAvailable = isAvailable,
            metadata = metadata
        });
    }
}
```

**Response (Composite):**
```json
{
  "providerName": "Composite OCR (Primary + Fallback)",
  "isAvailable": true,
  "metadata": {
    "CompositeProvider": "true",
    "PrimaryProvider": "Azure Computer Vision OCR",
    "FallbackProvider": "Tesseract OCR (Local)",
    "ConfidenceThreshold": "0.85",
    "Primary_Endpoint": "https://labelwise-ocr-cv.cognitiveservices.azure.com/",
    "Primary_IsConfigured": "true",
    "Fallback_TessdataPath": "C:\\tessdata",
    "Fallback_Language": "por+eng"
  }
}
```

---

## 🚀 Quick Start (5 Minutos)

### 1. Criar Recurso Azure
```bash
az cognitiveservices account create \
  --name labelwise-ocr-cv \
  --resource-group rg-labelwise \
  --kind ComputerVision \
  --sku F0 \
  --location brazilsouth \
  --yes
```

### 2. Obter Credenciais
```bash
# Endpoint
az cognitiveservices account show \
  --name labelwise-ocr-cv \
  --resource-group rg-labelwise \
  --query "properties.endpoint" -o tsv

# Key
az cognitiveservices account keys list \
  --name labelwise-ocr-cv \
  --resource-group rg-labelwise \
  --query "key1" -o tsv
```

### 3. Instalar Pacote
```bash
cd LabelWise.Infrastructure
dotnet add package Azure.AI.Vision.ImageAnalysis
```

### 4. Configurar appsettings.json
```json
{
  "OCR": {
    "Provider": "Composite",
    "Azure": {
      "Endpoint": "SEU_ENDPOINT_AQUI",
      "ApiKey": "SUA_KEY_AQUI"
    },
    "Composite": {
      "PrimaryProvider": "AzureComputerVision",
      "FallbackProvider": "Tesseract",
      "ConfidenceThreshold": 0.85
    }
  }
}
```

### 5. Testar
```bash
dotnet run --project LabelWise.Api
```

**Pronto!** 🎉

---

## 📚 Referências

- **Documentação Azure Computer Vision:**  
  https://learn.microsoft.com/azure/ai-services/computer-vision/overview-ocr

- **Pricing Calculator:**  
  https://azure.microsoft.com/pricing/calculator/

- **SDK .NET:**  
  https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/vision

- **Free Tier Limits:**  
  https://azure.microsoft.com/pricing/details/cognitive-services/computer-vision/

---

## 🎯 Próximos Passos

1. ✅ **Implementar Azure OCR** - Concluído!
2. ✅ **Implementar Composite Provider** - Concluído!
3. ⏭️ **Testar em imagens reais de rótulos**
4. ⏭️ **Configurar retry policies**
5. ⏭️ **Implementar cache de resultados**
6. ⏭️ **Monitorar custos no Azure**

---

**Criado por:** LabelWise Team  
**Última atualização:** 2026  
**Versão:** 1.0.0
