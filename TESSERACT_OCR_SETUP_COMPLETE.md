# 🔧 Guia de Configuração do Tesseract OCR - LabelWise

## ✅ CORREÇÕES IMPLEMENTADAS

Este documento resume as correções definitivas implementadas para garantir que o Tesseract OCR funcione corretamente no LabelWise.

---

## 📋 O QUE FOI CORRIGIDO

### 1. ❌ Mock OCR NÃO é mais usado por padrão
- **ANTES**: Sistema usava MockOcrProvider silenciosamente em caso de erro
- **AGORA**: Sistema usa **EXCLUSIVAMENTE TesseractOcrProvider** por padrão
- Mock só é usado se `OCR:UseMockProvider = true` no appsettings.json

### 2. 🔍 Estratégia Robusta de Localização do Tessdata
O sistema agora busca o diretório tessdata em múltiplos locais (em ordem de prioridade):

1. **Caminho configurado** em `appsettings.json` (`OCR:TessdataPath`)
2. **Variável de ambiente** `TESSDATA_PREFIX`
3. **Raiz do projeto** `LabelWise.Api\tessdata`
4. **Diretório base da aplicação** `bin\Debug\net10.0\tessdata`
5. **Diretório de trabalho atual** `tessdata`

### 3. 📦 Cópia Automática dos Arquivos Tessdata
O arquivo `LabelWise.Api.csproj` foi configurado para:
- Copiar automaticamente todos os arquivos `.traineddata` para o output do build
- Copiar para o diretório de publish
- Criar o diretório tessdata se não existir
- Avisar se nenhum arquivo .traineddata for encontrado

### 4. ✅ Validação na Inicialização
- Sistema valida se o Tesseract está configurado corretamente no startup
- Logs detalhados mostram:
  - Provider usado
  - Caminho tessdata resolvido
  - Arquivos de idioma encontrados
  - Avisos se algo estiver faltando

### 5. 📊 Metadata do Provider nas Respostas
Todas as respostas de OCR agora incluem `ProviderMetadata` com:
- Nome do provider
- Tipo (classe completa)
- Se é Mock ou Real
- Caminho tessdata usado
- Idiomas configurados
- Arquivos traineddata disponíveis

### 6. ⚙️ Configuração Forte e Tipada
Nova classe `OcrOptions` com todas as configurações:
```json
"OCR": {
  "Provider": "Tesseract",
  "UseMockProvider": false,
  "TessdataPath": null,
  "Language": "por+eng",
  "ValidateOnStartup": true
}
```

### 7. 🚨 Tratamento de Erros Claro
- Se Tesseract falhar e `UseMockProvider = false`, a API retorna erro real
- Nunca retorna dados fake silenciosamente
- Mensagens de erro claras indicam o que fazer

---

## 🚀 COMO USAR

### Passo 1: Criar o Diretório Tessdata

No diretório `LabelWise.Api`, crie uma pasta `tessdata`:

```powershell
cd LabelWise.Api
mkdir tessdata
```

### Passo 2: Baixar os Arquivos de Idioma

Baixe os arquivos `.traineddata` necessários de:
**https://github.com/tesseract-ocr/tessdata**

Arquivos necessários:
- `por.traineddata` (Português)
- `eng.traineddata` (Inglês)

Salve-os em `LabelWise.Api\tessdata\`

### Passo 3: Verificar a Estrutura

Sua estrutura deve ficar assim:
```
LabelWise.Api/
├── tessdata/
│   ├── por.traineddata
│   ├── eng.traineddata
├── Controllers/
├── Program.cs
├── appsettings.json
└── ...
```

### Passo 4: Configurar appsettings.json

Verifique a seção OCR no `appsettings.json`:

```json
{
  "OCR": {
    "Provider": "Tesseract",
    "UseMockProvider": false,
    "TessdataPath": null,
    "Language": "por+eng",
    "ValidateOnStartup": true
  }
}
```

**Importante**:
- `Provider`: Sempre "Tesseract" para produção
- `UseMockProvider`: **false** (nunca true em produção)
- `TessdataPath`: `null` para auto-detect (ou caminho customizado)
- `Language`: "por+eng" para Português e Inglês

### Passo 5: Compilar o Projeto

```powershell
dotnet build
```

Você verá mensagens no build indicando se os arquivos tessdata foram copiados:
```
✅ Diretório tessdata garantido em: C:\...\LabelWise.Api\tessdata
📂 Arquivos tessdata copiados: por.traineddata, eng.traineddata
```

### Passo 6: Executar a API

```powershell
dotnet run --project LabelWise.Api
```

Na inicialização, você verá:
```
═══════════════════════════════════════════════════════════════════════════
📋 OCR PROVIDER CONFIGURATION
═══════════════════════════════════════════════════════════════════════════
🔧 Provider: Tesseract
🎭 Use Mock Provider: False
📂 Tessdata Path: [auto-detect]
🌐 Language: por+eng
✅ Validate On Startup: True
───────────────────────────────────────────────────────────────────────────
✅ TESSERACT PROVIDER SELECTED
   🚀 Using TesseractOcrProvider (REAL OCR)
   ✅ Provider Instantiated: Tesseract OCR (Local)
   🔍 Validation: Available = True
   ✅ Tesseract validated successfully!
═══════════════════════════════════════════════════════════════════════════
```

---

## 🧪 COMO VALIDAR QUE ESTÁ FUNCIONANDO

### Teste 1: Verificar no Swagger

1. Acesse: `https://localhost:7001/swagger`
2. Use o endpoint `/api/pipeline/analyze-image`
3. Faça upload de uma imagem de rótulo
4. Na resposta JSON, verifique o campo `metadata`:

```json
{
  "ocrResult": {
    "providerMetadata": {
      "ProviderName": "Tesseract OCR (Local)",
      "ProviderType": "LabelWise.Infrastructure.Ocr.TesseractOcrProvider",
      "IsMock": "false",
      "TessdataPath": "C:\\...\\tessdata",
      "Language": "por+eng",
      "TesseractInstalled": "true",
      "TessdataExists": "True",
      "TrainedDataFilesCount": "2",
      "TrainedDataFiles": "eng.traineddata, por.traineddata"
    }
  }
}
```

**✅ SE ESTIVER CORRETO**:
- `IsMock` = "false"
- `ProviderName` = "Tesseract OCR (Local)"
- `TessdataExists` = "True"
- `TrainedDataFilesCount` > 0

**❌ SE ESTIVER ERRADO (Mock)**:
- `IsMock` = "true"
- `ProviderName` = "Mock OCR Provider (Development Only)"
- `Warning` = "This is a MOCK provider..."

### Teste 2: Verificar os Logs

Nos logs da aplicação, procure por:
```
✅ Tessdata encontrado em: C:\...\tessdata
✅ Tessdata validado com sucesso. Arquivos encontrados: por.traineddata, eng.traineddata
✅ Todos os idiomas necessários estão disponíveis: por+eng
```

---

## 🛠️ TROUBLESHOOTING

### Problema: "Diretório tessdata não encontrado"

**Solução**:
1. Crie a pasta `LabelWise.Api\tessdata`
2. Baixe os arquivos `.traineddata`
3. Rebuild o projeto: `dotnet build`

### Problema: "Nenhum arquivo .traineddata encontrado"

**Solução**:
1. Baixe `por.traineddata` e `eng.traineddata`
2. Coloque em `LabelWise.Api\tessdata\`
3. Rebuild: `dotnet build`

### Problema: Sistema ainda usa Mock

**Solução**:
1. Verifique `appsettings.json`: `"UseMockProvider": false`
2. Verifique `appsettings.Development.json`: `"UseMockProvider": false`
3. Limpe e rebuild: `dotnet clean && dotnet build`
4. Reinicie a aplicação

### Problema: "TesseractException" ao processar imagem

**Solução**:
1. Verifique se os arquivos estão em `bin\Debug\net10.0\tessdata\`
2. Verifique se `por.traineddata` e `eng.traineddata` existem
3. Tente configurar `TessdataPath` explicitamente:
   ```json
   "TessdataPath": "C:\\caminho\\completo\\para\\tessdata"
   ```

---

## 🔒 GARANTIAS IMPLEMENTADAS

✅ **Sistema NUNCA usa Mock por padrão**
✅ **Mock só é usado se explicitamente configurado**
✅ **Tessdata é localizado automaticamente em múltiplos caminhos**
✅ **Arquivos são copiados automaticamente no build**
✅ **Metadata sempre mostra qual provider foi usado**
✅ **Erros são claros e indicam a solução**
✅ **Validação na inicialização detecta problemas**

---

## 📝 CONFIGURAÇÃO PARA USAR MOCK (Apenas Desenvolvimento)

Se você REALMENTE precisar usar Mock (não recomendado):

```json
{
  "OCR": {
    "Provider": "Mock",
    "UseMockProvider": true,
    "TessdataPath": null,
    "Language": "por+eng",
    "ValidateOnStartup": false
  }
}
```

**⚠️ IMPORTANTE**: 
- Nunca use em produção
- Dados retornados são fake
- Use apenas para testes sem OCR real

---

## 📚 ARQUIVOS MODIFICADOS

1. ✅ `LabelWise.Application\Configuration\OcrOptions.cs` - **NOVO**
2. ✅ `LabelWise.Application\Interfaces\IOcrProvider.cs` - Adicionado `GetMetadata()`
3. ✅ `LabelWise.Application\DTOs\OcrResultDto.cs` - Adicionado `ProviderMetadata`
4. ✅ `LabelWise.Infrastructure\Ocr\TesseractOcrProvider.cs` - Estratégia robusta de localização
5. ✅ `LabelWise.Infrastructure\Ocr\MockOcrProvider.cs` - Adicionado `GetMetadata()`
6. ✅ `LabelWise.Infrastructure\Extensions\ServiceCollectionExtensions.cs` - Configuração forte
7. ✅ `LabelWise.Api\appsettings.json` - Seção OCR atualizada
8. ✅ `LabelWise.Api\appsettings.Development.json` - **NOVO**
9. ✅ `LabelWise.Api\LabelWise.Api.csproj` - Cópia automática de tessdata

---

## 🎯 RESULTADO FINAL

✅ **Tesseract é o provider padrão**
✅ **Mock não é usado silenciosamente**
✅ **Tessdata é encontrado automaticamente**
✅ **Build copia arquivos automaticamente**
✅ **Metadata mostra provider real**
✅ **Erros são claros e tratados corretamente**

---

**Data**: Hoje
**Versão**: LabelWise v1.0 - Tesseract OCR Fixed
