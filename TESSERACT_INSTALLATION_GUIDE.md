# 🔍 Guia Completo de Instalação e Configuração do Tesseract OCR

## 📋 Visão Geral

Este guia detalha todos os passos necessários para configurar o **Tesseract OCR** no projeto **LabelWise**, permitindo a extração real de texto de imagens de rótulos alimentares.

---

## 🎯 O Que Foi Implementado

✅ **TesseractOcrProvider** completo com:
- Suporte a português do Brasil (por) + inglês (eng) como fallback
- Extração de texto com confiança percentual
- Detecção de blocos de texto com coordenadas (bounding boxes)
- Identificação de tipos de bloco (HEADING, SUBHEADING, TEXT)
- Tratamento robusto de erros
- Logging detalhado para troubleshooting

✅ **Integração no Pipeline**:
- `ProductAnalysisPipelineOrchestrator` ajustado para incluir texto OCR bruto
- Campo `ExtractedText` no resultado da análise contém o texto completo extraído
- Metadados do OCR incluem confiança, tamanho do texto e nome do provider

✅ **Injeção de Dependência**:
- `TesseractOcrProvider` registrado como default
- `MockOcrProvider` mantido como alternativa para desenvolvimento

---

## 📦 Passo 1: Instalar Pacote NuGet

### No Visual Studio:

**Opção A - Package Manager Console:**
```powershell
Install-Package Tesseract -Version 5.2.0 -ProjectName LabelWise.Infrastructure
```

**Opção B - .NET CLI:**
```bash
cd LabelWise.Infrastructure
dotnet add package Tesseract --version 5.2.0
```

**Opção C - Gerenciador de Pacotes NuGet (GUI):**
1. Clique com botão direito em `LabelWise.Infrastructure` → **Manage NuGet Packages**
2. Busque por: `Tesseract`
3. Selecione a versão **5.2.0** ou superior
4. Clique em **Install**

### ⚠️ Pacote Correto
- **CORRETO**: `Tesseract` (by Charlesw)
- **EVITAR**: `TesseractOCR`, `Tesseract.Net`, etc.

---

## 📂 Passo 2: Baixar Arquivos de Idioma (tessdata)

### 2.1. Escolha a Versão dos Dados

Existem 3 versões disponíveis:

| Versão | Tamanho | Velocidade | Precisão | Recomendado Para |
|--------|---------|------------|----------|------------------|
| **tessdata_best** | ~10MB/idioma | Lenta | Máxima | **Produção** |
| tessdata (normal) | ~5MB/idioma | Média | Boa | Desenvolvimento |
| tessdata_fast | ~2MB/idioma | Rápida | Menor | Protótipos |

### 2.2. Download dos Arquivos

**Para Português + Inglês (Recomendado):**

```bash
# Criar pasta tessdata
mkdir tessdata

# Baixar Português (Brasil)
curl -L https://github.com/tesseract-ocr/tessdata_best/raw/main/por.traineddata -o tessdata/por.traineddata

# Baixar Inglês (fallback)
curl -L https://github.com/tesseract-ocr/tessdata_best/raw/main/eng.traineddata -o tessdata/eng.traineddata
```

**Ou baixe manualmente:**
1. Acesse: https://github.com/tesseract-ocr/tessdata_best
2. Baixe os arquivos:
   - `por.traineddata` (Português)
   - `eng.traineddata` (Inglês)

### 2.3. Onde Colocar os Arquivos

**Opção A - Dentro do Projeto (Recomendado para desenvolvimento):**
```
LabelWise/
├── LabelWise.Api/
├── LabelWise.Infrastructure/
├── tessdata/                    ← Criar esta pasta na raiz da solução
│   ├── por.traineddata          ← Português
│   └── eng.traineddata          ← Inglês
└── LabelWise.sln
```

**Opção B - Diretório do Sistema:**
```
C:\tessdata\
├── por.traineddata
└── eng.traineddata
```

---

## ⚙️ Passo 3: Configurar o Caminho do Tessdata

### Opção A - Configurar no appsettings.json (Recomendado)

Edite `LabelWise.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "..."
  },
  "Ocr": {
    "TessdataPath": ".\\tessdata",
    "Language": "por+eng"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "LabelWise.Infrastructure.Ocr": "Debug"
    }
  }
}
```

### Opção B - Variável de Ambiente

**Windows (PowerShell):**
```powershell
$env:TESSDATA_PREFIX = "C:\Users\seu-usuario\source\repos\LabelWise\tessdata"
```

**Windows (CMD):**
```cmd
set TESSDATA_PREFIX=C:\Users\seu-usuario\source\repos\LabelWise\tessdata
```

**Permanente (System Properties):**
1. Pressione `Win + Pause` → **Advanced system settings**
2. **Environment Variables**
3. Adicionar variável: `TESSDATA_PREFIX` = `C:\caminho\para\tessdata`

### Opção C - Passar no Construtor (Código)

Modifique `ServiceCollectionExtensions.cs`:

```csharp
services.AddSingleton<IOcrProvider>(sp => 
{
    var logger = sp.GetService<ILogger<TesseractOcrProvider>>();
    var tessdataPath = @"C:\tessdata"; // Caminho customizado
    return new TesseractOcrProvider(logger, tessdataPath);
});
```

---

## 🔍 Passo 4: Verificar Instalação

### 4.1. Criar Script de Teste

Crie `test-tesseract.ps1`:

```powershell
# Test Tesseract Installation
Write-Host "=== Testando Tesseract OCR ===" -ForegroundColor Cyan

# 1. Verificar pacote NuGet
Write-Host "`n1. Verificando pacote NuGet Tesseract..." -ForegroundColor Yellow
$tesseractPackage = Get-Content LabelWise.Infrastructure\LabelWise.Infrastructure.csproj | Select-String "Tesseract"
if ($tesseractPackage) {
    Write-Host "   ✓ Pacote Tesseract encontrado" -ForegroundColor Green
    Write-Host "   $tesseractPackage" -ForegroundColor Gray
} else {
    Write-Host "   ✗ Pacote Tesseract NÃO encontrado!" -ForegroundColor Red
    Write-Host "   Execute: dotnet add LabelWise.Infrastructure package Tesseract" -ForegroundColor Yellow
}

# 2. Verificar pasta tessdata
Write-Host "`n2. Verificando pasta tessdata..." -ForegroundColor Yellow
$tessdataPath = ".\tessdata"
if (Test-Path $tessdataPath) {
    Write-Host "   ✓ Pasta tessdata encontrada em: $tessdataPath" -ForegroundColor Green
    
    # Verificar arquivos de idioma
    $porFile = Test-Path "$tessdataPath\por.traineddata"
    $engFile = Test-Path "$tessdataPath\eng.traineddata"
    
    if ($porFile) {
        $porSize = (Get-Item "$tessdataPath\por.traineddata").Length / 1MB
        Write-Host "   ✓ por.traineddata: $([math]::Round($porSize, 2)) MB" -ForegroundColor Green
    } else {
        Write-Host "   ✗ por.traineddata NÃO encontrado!" -ForegroundColor Red
    }
    
    if ($engFile) {
        $engSize = (Get-Item "$tessdataPath\eng.traineddata").Length / 1MB
        Write-Host "   ✓ eng.traineddata: $([math]::Round($engSize, 2)) MB" -ForegroundColor Green
    } else {
        Write-Host "   ✗ eng.traineddata NÃO encontrado!" -ForegroundColor Red
    }
} else {
    Write-Host "   ✗ Pasta tessdata NÃO encontrada!" -ForegroundColor Red
    Write-Host "   Crie a pasta e baixe os arquivos conforme o guia" -ForegroundColor Yellow
}

# 3. Verificar variável de ambiente
Write-Host "`n3. Verificando variável de ambiente TESSDATA_PREFIX..." -ForegroundColor Yellow
$tessdataEnv = $env:TESSDATA_PREFIX
if ($tessdataEnv) {
    Write-Host "   ✓ TESSDATA_PREFIX definido: $tessdataEnv" -ForegroundColor Green
} else {
    Write-Host "   ⚠ TESSDATA_PREFIX não definido (opcional)" -ForegroundColor Yellow
}

Write-Host "`n=== Teste Completo ===" -ForegroundColor Cyan
```

Execute:
```powershell
.\test-tesseract.ps1
```

### 4.2. Verificar Logs da API

Execute a API e observe os logs:

```
info: LabelWise.Infrastructure.Ocr.TesseractOcrProvider[0]
      TesseractOcrProvider inicializado. Tessdata path: C:\...\tessdata, Language: por+eng

info: LabelWise.Infrastructure.Ocr.TesseractOcrProvider[0]
      Tessdata encontrado. Arquivos disponíveis: por.traineddata, eng.traineddata
```

---

## 🧪 Passo 5: Testar com Imagem Real

### 5.1. Criar Imagem de Teste

Crie uma imagem simples com texto em um editor ou use uma foto de um rótulo real.

**Exemplo: `test-label.png`**
```
┌─────────────────────────────┐
│   BISCOITO RECHEADO         │
│                             │
│   INGREDIENTES:             │
│   Farinha de trigo,         │
│   açúcar, chocolate         │
│                             │
│   ALÉRGICOS: CONTÉM GLÚTEN  │
└─────────────────────────────┘
```

### 5.2. Testar via API

**PowerShell:**
```powershell
# Fazer upload de uma imagem de teste
$imagePath = "C:\caminho\para\test-label.png"
$url = "http://localhost:5000/api/analysis/pipeline"

$form = @{
    image = Get-Item -Path $imagePath
}

$response = Invoke-RestMethod -Uri $url -Method Post -Form $form
$response | ConvertTo-Json -Depth 10
```

**cURL:**
```bash
curl -X POST "http://localhost:5000/api/analysis/pipeline" \
  -F "image=@test-label.png" \
  -H "accept: application/json"
```

### 5.3. Verificar Resposta

A resposta deve incluir:

```json
{
  "analysisResult": {
    "productName": "BISCOITO RECHEADO",
    "extractedText": "BISCOITO RECHEADO\n\nINGREDIENTES:\nFarinha de trigo,\naçúcar, chocolate\n\nALÉRGICOS: CONTÉM GLÚTEN",
    "extractedIngredients": ["Farinha de trigo", "açúcar", "chocolate"],
    "extractedAllergens": ["GLÚTEN"],
    "summary": "..."
  },
  "metadata": {
    "ocrStep": {
      "stepName": "OCR",
      "success": true,
      "durationMs": 1234.5,
      "additionalData": {
        "confidence": 0.92,
        "textLength": 98,
        "blocksCount": 7,
        "providerName": "Tesseract OCR (Local)"
      }
    }
  }
}
```

---

## 🔧 Troubleshooting

### ❌ Erro: "Tesseract não configurado"

**Problema:** Tessdata não encontrado.

**Solução:**
1. Verifique se a pasta `tessdata` existe
2. Verifique se os arquivos `.traineddata` estão dentro dela
3. Confirme o caminho no appsettings.json ou variável de ambiente

### ❌ Erro: "Failed to initialise tesseract engine"

**Problema:** Arquivos de idioma corrompidos ou versão incompatível.

**Solução:**
1. Re-baixe os arquivos `.traineddata`
2. Use a versão correta (tessdata_best para produção)
3. Verifique se a versão do pacote NuGet é compatível (5.2.0+)

### ❌ Erro: "System.DllNotFoundException: liblept"

**Problema:** Dependências nativas não encontradas.

**Solução:**
1. Reinstale o pacote NuGet `Tesseract`
2. Verifique se os arquivos nativos foram copiados para `bin\Debug\net10.0\`
3. Em alguns casos, pode ser necessário instalar Visual C++ Redistributable

### ❌ OCR retorna texto vazio ou incorreto

**Problema:** Qualidade da imagem ou idioma incorreto.

**Solução:**
1. Use imagens de alta resolução (mínimo 300 DPI)
2. Evite imagens borradas ou com baixo contraste
3. Confirme que o idioma está correto (`por+eng`)
4. Experimente pré-processar a imagem (melhorar contraste, binarização)

### ⚠️ OCR muito lento

**Problema:** Imagens muito grandes ou tessdata_best é pesado.

**Solução:**
1. Redimensione imagens grandes antes do OCR
2. Use `tessdata` normal ao invés de `tessdata_best`
3. Configure `PageSegMode` para o tipo correto de documento

---

## 🔄 Alternar Entre Mock e Tesseract

### Usar MockOcrProvider (para desenvolvimento sem Tesseract)

Edite `LabelWise.Infrastructure\Extensions\ServiceCollectionExtensions.cs`:

```csharp
// OCR Provider - implementação mock
services.AddSingleton<IOcrProvider, MockOcrProvider>();

// Comentar TesseractOcrProvider:
// services.AddSingleton<IOcrProvider, TesseractOcrProvider>();
```

### Usar TesseractOcrProvider (produção)

```csharp
// OCR Provider - implementação real
services.AddSingleton<IOcrProvider, TesseractOcrProvider>();

// Comentar MockOcrProvider:
// services.AddSingleton<IOcrProvider, MockOcrProvider>();
```

---

## 📊 Melhorias de Performance

### 1. Redimensionar Imagens

Antes de processar, redimensione imagens grandes:

```csharp
public async Task<OcrResultDto> ExtractTextAsync(OcrRequestDto request)
{
    // Redimensionar se necessário
    var optimizedPath = await OptimizeImageAsync(request.ImagePath);
    
    using var img = Pix.LoadFromFile(optimizedPath);
    // ... resto do código
}
```

### 2. Ajustar PageSegMode

Para rótulos alimentares, use:

```csharp
engine.DefaultPageSegMode = PageSegMode.Auto; // Detecta automaticamente
// ou
engine.DefaultPageSegMode = PageSegMode.SingleBlock; // Para blocos únicos
```

### 3. Pré-processamento

Melhore a qualidade do texto:

```csharp
using var img = Pix.LoadFromFile(request.ImagePath);
// Converter para escala de cinza
using var gray = img.ConvertRGBToGray();
// Aplicar threshold (binarização)
using var binary = gray.BinarizeOtsuAdaptiveThreshold(2000, 2000, 0, 0, 0.0f);
using var page = engine.Process(binary);
```

---

## 📝 Resumo de Arquivos Alterados

| Arquivo | Alteração |
|---------|-----------|
| `TesseractOcrProvider.cs` | ✅ Implementação completa com Tesseract |
| `ServiceCollectionExtensions.cs` | ✅ Injeção do TesseractOcrProvider |
| `ProductAnalysisPipelineOrchestrator.cs` | ✅ Inclusão do texto OCR em `ExtractedText` |
| `MockOcrProvider.cs` | ⚪ Mantido inalterado (disponível para testes) |

---

## ✅ Checklist Final

- [ ] Pacote NuGet `Tesseract` instalado (5.2.0+)
- [ ] Pasta `tessdata` criada na raiz da solução
- [ ] Arquivo `por.traineddata` baixado e colocado em `tessdata/`
- [ ] Arquivo `eng.traineddata` baixado e colocado em `tessdata/`
- [ ] `ServiceCollectionExtensions.cs` configurado para usar `TesseractOcrProvider`
- [ ] API executada com sucesso e logs mostram "Tessdata encontrado"
- [ ] Teste realizado com imagem real via endpoint `/api/analysis/pipeline`
- [ ] Resposta inclui campo `extractedText` com texto OCR bruto
- [ ] Texto extraído está correto e legível

---

## 📚 Recursos Adicionais

- **Documentação Tesseract:** https://tesseract-ocr.github.io/
- **Tesseract .NET Wrapper:** https://github.com/charlesw/tesseract
- **Trained Data Files:** https://github.com/tesseract-ocr/tessdata_best
- **Tesseract Wiki:** https://github.com/tesseract-ocr/tesseract/wiki

---

## 🎉 Próximos Passos

Com o Tesseract configurado, você pode:

1. **Melhorar a Precisão:** Ajustar pré-processamento de imagens
2. **Otimizar Performance:** Implementar cache de resultados OCR
3. **Expandir Idiomas:** Adicionar mais arquivos `.traineddata`
4. **Criar Testes:** Adicionar testes unitários com imagens mockadas
5. **Monitorar Qualidade:** Implementar métricas de confiança do OCR

---

**🚀 Boa sorte com seu projeto LabelWise!**
