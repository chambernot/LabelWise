# 🔧 FIX: Azure OpenAI Vision 404 Error

## ❌ Erro Identificado

```
System.ClientModel.ClientResultException: HTTP 404 (:404)
Resource not found
Source: OpenAI
Status: 404
```

## 🔍 Causa Raiz

O erro **HTTP 404** ao chamar o Azure OpenAI Vision ocorre quando:
1. ❌ O **endpoint** está incorreto
2. ❌ O **deployment name** está incorreto
3. ❌ A **API key** está incorreta ou o recurso não existe

## ✅ Correções Aplicadas

### 1. **Correção na Criação do Cliente**

**Código Anterior (INCORRETO)**:
```csharp
var chatClient = new ChatClient(
    model: _options.VisionDeployment,
    apiKey: _options.ApiKey,
    options: new OpenAIClientOptions { ... }  // ❌ Construtor inválido
);
```

**Código Corrigido**:
```csharp
// Create OpenAIClient first with Azure endpoint
var openAIClient = new OpenAIClient(
    credential: new ApiKeyCredential(_options.ApiKey),
    options: new OpenAIClientOptions
    {
        Endpoint = new Uri(normalizedEndpoint)
    }
);

// Get ChatClient from the OpenAIClient
var chatClient = openAIClient.GetChatClient(_options.VisionDeployment);
```

### 2. **Logging Melhorado**

Adicionado log que mostra o endpoint e deployment sendo usado:
```csharp
_logger.LogInformation("Connecting to Azure OpenAI at {Endpoint} with deployment {Deployment}", 
    normalizedEndpoint, _options.VisionDeployment);
```

## 📋 Checklist de Configuração

### ✅ Passo 1: Verifique o Endpoint

O endpoint deve estar no formato correto no `appsettings.json`:

**✅ CORRETO**:
```json
{
  "AzureOpenAiVision": {
    "Endpoint": "https://your-resource.openai.azure.com/"
  }
}
```

**❌ INCORRETO**:
```json
{
  "AzureOpenAiVision": {
    "Endpoint": "https://your-resource.openai.azure.com/openai/v1"  // ❌ Não incluir path
  }
}
```

### ✅ Passo 2: Verifique o Deployment Name

O deployment name deve corresponder exatamente ao nome do deployment no Azure Portal.

**Como encontrar o deployment name**:
1. Acesse o Azure Portal
2. Vá para seu recurso Azure OpenAI
3. Clique em **"Model deployments"** ou **"Deployments"**
4. Copie o **nome exato** do deployment que suporta visão (ex: `gpt-4o`, `gpt-4-vision`)

**Exemplo de configuração**:
```json
{
  "AzureOpenAiVision": {
    "VisionDeployment": "gpt-4o"  // ✅ Nome exato do deployment
  }
}
```

⚠️ **IMPORTANTE**: O deployment name é **case-sensitive**!

### ✅ Passo 3: Verifique a API Key

**Como obter a API Key**:
1. Acesse o Azure Portal
2. Vá para seu recurso Azure OpenAI
3. Clique em **"Keys and Endpoint"**
4. Copie **KEY 1** ou **KEY 2**

**Configuração**:
```json
{
  "AzureOpenAiVision": {
    "ApiKey": "sua-api-key-aqui"
  }
}
```

### ✅ Passo 4: Verifique se o Deployment Suporta Visão

Nem todos os modelos Azure OpenAI suportam multimodal/visão.

**Modelos que suportam visão**:
- ✅ `gpt-4o`
- ✅ `gpt-4-turbo`
- ✅ `gpt-4-vision-preview`
- ❌ `gpt-4` (sem visão)
- ❌ `gpt-3.5-turbo` (sem visão)

## 🔧 Configuração Completa (appsettings.json)

```json
{
  "AzureOpenAiVision": {
    "Endpoint": "https://seu-recurso.openai.azure.com/",
    "ApiKey": "sua-api-key-completa-aqui",
    "VisionDeployment": "gpt-4o"
  }
}
```

## 🧪 Como Testar a Configuração

### 1. Verifique os Logs

Quando você chamar o serviço, deve ver logs como:
```
[INFO] Starting visual interpretation for image: /path/to/image.jpg
[INFO] Read 124856 bytes from image file
[INFO] Connecting to Azure OpenAI at https://seu-recurso.openai.azure.com with deployment gpt-4o
[INFO] Created ChatClient for deployment: gpt-4o
[INFO] Calling Azure OpenAI Vision model...
```

### 2. Se Ainda Receber 404

**Verifique manualmente no Azure Portal**:

1. Acesse: `https://portal.azure.com`
2. Vá para **Azure OpenAI Service**
3. Selecione seu recurso
4. Clique em **"Model deployments"**
5. **Confirme que existe um deployment com o nome exato que você configurou**

### 3. Teste com cURL

Teste se o endpoint está acessível:

```bash
curl -X POST "https://seu-recurso.openai.azure.com/openai/deployments/gpt-4o/chat/completions?api-version=2024-02-15-preview" \
  -H "Content-Type: application/json" \
  -H "api-key: SUA_API_KEY" \
  -d '{
    "messages": [
      {
        "role": "user",
        "content": "Hello"
      }
    ]
  }'
```

Se retornar 404, o problema está na configuração do Azure.

## 📊 Troubleshooting por Código de Erro

| Erro | Causa Provável | Solução |
|------|----------------|---------|
| **404** | Deployment não existe ou endpoint incorreto | Verifique o nome do deployment no Azure Portal |
| **401** | API key incorreta | Verifique a API key em "Keys and Endpoint" |
| **403** | Sem permissões | Verifique as permissões do recurso Azure |
| **429** | Rate limit excedido | Aguarde ou aumente o quota |
| **500** | Erro interno do Azure | Tente novamente mais tarde |

## 🔐 Configuração no Azure Portal

### Passo a Passo Completo

#### 1. Criar ou Localizar o Recurso Azure OpenAI

```
Azure Portal > Azure OpenAI > [Seu Recurso]
```

#### 2. Obter o Endpoint

```
Seu Recurso > Keys and Endpoint > Endpoint
```

Copie algo como: `https://seu-recurso.openai.azure.com/`

#### 3. Obter a API Key

```
Seu Recurso > Keys and Endpoint > KEY 1
```

#### 4. Verificar/Criar Deployment

```
Seu Recurso > Model deployments > + Create new deployment
```

**Configurações recomendadas**:
- **Model**: `gpt-4o` ou `gpt-4-turbo`
- **Deployment name**: `gpt-4o` (pode ser qualquer nome)
- **Content filter**: Default

#### 5. Atualizar o appsettings.json

```json
{
  "AzureOpenAiVision": {
    "Endpoint": "https://seu-recurso.openai.azure.com/",
    "ApiKey": "a1b2c3d4e5f6...",
    "VisionDeployment": "gpt-4o"
  }
}
```

## 🚨 Erros Comuns

### ❌ Erro 1: Endpoint com path extra

```json
// ❌ INCORRETO
"Endpoint": "https://seu-recurso.openai.azure.com/openai/v1"

// ✅ CORRETO
"Endpoint": "https://seu-recurso.openai.azure.com/"
```

### ❌ Erro 2: Deployment name com typo

```json
// ❌ INCORRETO
"VisionDeployment": "gpt4o"  // sem hífen

// ✅ CORRETO
"VisionDeployment": "gpt-4o"  // com hífen
```

### ❌ Erro 3: Modelo sem suporte a visão

```json
// ❌ INCORRETO - gpt-4 padrão não suporta visão
"VisionDeployment": "gpt-4"

// ✅ CORRETO - gpt-4o suporta visão
"VisionDeployment": "gpt-4o"
```

## 📝 Exemplo de Configuração Funcional

### appsettings.Development.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "LabelWise.Infrastructure.AI": "Debug"  // ← Log detalhado para debugging
    }
  },
  "AzureOpenAiVision": {
    "Endpoint": "https://labelwise-openai.openai.azure.com/",
    "ApiKey": "abc123def456ghi789jkl012mno345pqr678stu901vwx234",
    "VisionDeployment": "gpt-4o"
  }
}
```

### Resultado Esperado nos Logs

```
[2025-01-11 14:32:15.123] [INFO] Starting visual interpretation for image: /uploads/test.jpg
[2025-01-11 14:32:15.145] [INFO] Read 156789 bytes from image file
[2025-01-11 14:32:15.167] [INFO] Connecting to Azure OpenAI at https://labelwise-openai.openai.azure.com with deployment gpt-4o
[2025-01-11 14:32:15.189] [INFO] Created ChatClient for deployment: gpt-4o
[2025-01-11 14:32:15.201] [INFO] Calling Azure OpenAI Vision model...
[2025-01-11 14:32:16.823] [INFO] Azure OpenAI Vision call completed in 1622ms
[2025-01-11 14:32:16.845] [INFO] Received response: {"productName":"Biscoito Recheado",...}
[2025-01-11 14:32:16.867] [INFO] Visual interpretation completed: CaptureType=FrontPackaging, Confidence=High, Product=Biscoito Recheado
```

## ✅ Checklist Final

Antes de testar novamente, confirme:

- [ ] ✅ Endpoint está no formato `https://[resource].openai.azure.com/` (sem path extra)
- [ ] ✅ API Key foi copiada corretamente do Azure Portal
- [ ] ✅ Deployment name corresponde exatamente ao nome no Azure Portal (case-sensitive)
- [ ] ✅ O deployment usa um modelo que suporta visão (`gpt-4o`, `gpt-4-turbo`, etc.)
- [ ] ✅ O recurso Azure OpenAI está ativo e acessível
- [ ] ✅ Reiniciou a aplicação após alterar o appsettings.json

## 🎯 Próximos Passos

1. **Verifique sua configuração** contra este guia
2. **Atualize o appsettings.json** se necessário
3. **Reinicie a aplicação**
4. **Teste novamente**
5. **Verifique os logs** para confirmar a conexão

Se o erro persistir após seguir todos os passos, o problema pode estar no Azure:
- Verifique se o recurso está ativo
- Verifique se há quota disponível
- Verifique se há restrições de rede/firewall

---

## 📞 Suporte

Se o problema persistir:
1. Capture os logs completos
2. Verifique o status do Azure OpenAI no Azure Portal
3. Confirme que o deployment existe e está ativo
4. Teste com uma ferramenta externa (Postman, cURL) para isolar o problema

**Status da Correção**: ✅ **IMPLEMENTADO**
**Build Status**: ✅ **COMPILAÇÃO SUCESSO**
**Próxima Ação**: **VERIFICAR CONFIGURAÇÃO**
