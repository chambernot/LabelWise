# 🚀 Quick Start - Tesseract OCR no LabelWise

## ⚡ Resumo Executivo

O **Tesseract OCR** foi implementado no projeto LabelWise para substituir o provider mockado e processar **imagens reais** de rótulos alimentares.

---

## 🎯 Status Atual

| Item | Status |
|------|--------|
| **Código implementado** | ✅ 100% Completo |
| **Pacote NuGet** | ⚠️ Aguardando instalação |
| **Tessdata (idiomas)** | ⚠️ Aguardando download |
| **Provider ativo** | MockOcrProvider (temporário) |

---

## 📦 Instalação Rápida (3 minutos)

### Opção 1: Setup Automático (Recomendado)

```powershell
# Executa setup completo
.\setup-tesseract.ps1
```

O script irá:
- ✅ Criar pasta `tessdata`
- ✅ Baixar arquivos de idioma (por + eng)
- ✅ Instalar pacote NuGet Tesseract
- ✅ Validar instalação

### Opção 2: Manual

```powershell
# 1. Instalar pacote
dotnet add LabelWise.Infrastructure package Tesseract --version 5.2.0

# 2. Criar pasta tessdata
mkdir tessdata

# 3. Baixar idiomas
curl -L https://github.com/tesseract-ocr/tessdata_best/raw/main/por.traineddata -o tessdata/por.traineddata
curl -L https://github.com/tesseract-ocr/tessdata_best/raw/main/eng.traineddata -o tessdata/eng.traineddata
```

---

## ⚙️ Ativação do Tesseract

Após instalação, execute estes 3 passos:

### Passo 1: Descomentar #define

Edite: `LabelWise.Infrastructure\Ocr\TesseractOcrProvider.cs`

```csharp
// Linha 12 - Descomente esta linha:
#define TESSERACT_INSTALLED
```

### Passo 2: Ativar na Injeção de Dependência

Edite: `LabelWise.Infrastructure\Extensions\ServiceCollectionExtensions.cs`

```csharp
// Comentar Mock:
// services.AddSingleton<IOcrProvider, MockOcrProvider>();

// Descomentar Tesseract:
services.AddSingleton<IOcrProvider, TesseractOcrProvider>();
```

### Passo 3: Recompilar

```powershell
dotnet build
```

---

## 🧪 Testar

```powershell
# 1. Executar API
.\run-api.ps1

# 2. Testar com imagem
.\test-ocr-image.ps1 -ImagePath "C:\caminho\para\rotulo.jpg"
```

---

## 📚 Documentação Completa

| Documento | Descrição |
|-----------|-----------|
| **TESSERACT_INSTALLATION_GUIDE.md** | Guia completo (14 páginas) |
| **TESSERACT_IMPLEMENTATION_SUMMARY.md** | Resumo executivo detalhado |
| **NUGET_PACKAGES_TESSERACT.md** | Detalhes do pacote NuGet |
| **TESSERACT_USAGE_EXAMPLES.cs** | 8 exemplos práticos |

---

## 🔧 Estrutura de Arquivos

```
LabelWise/
├── tessdata/                                    ← CRIAR ESTA PASTA
│   ├── por.traineddata                         ← BAIXAR (~10 MB)
│   └── eng.traineddata                         ← BAIXAR (~10 MB)
├── LabelWise.Infrastructure/
│   ├── Ocr/
│   │   ├── TesseractOcrProvider.cs            ✅ IMPLEMENTADO
│   │   └── MockOcrProvider.cs                 ✅ MANTIDO
│   └── Extensions/
│       └── ServiceCollectionExtensions.cs      ✅ CONFIGURADO
└── Scripts/
    ├── setup-tesseract.ps1                     ✅ CRIADO
    └── test-ocr-image.ps1                      ✅ CRIADO
```

---

## ⚠️ Troubleshooting Rápido

### "Tesseract não configurado"
```powershell
# Executar setup
.\setup-tesseract.ps1
```

### "Pacote Tesseract não instalado"
```powershell
# Instalar manualmente
dotnet add LabelWise.Infrastructure package Tesseract --version 5.2.0

# Descomentar #define TESSERACT_INSTALLED na linha 12 do TesseractOcrProvider.cs
```

### OCR retorna texto vazio
- Use imagens de **alta qualidade** (mínimo 300 DPI)
- Verifique se os arquivos `.traineddata` estão em `./tessdata/`
- Confirme que o idioma está correto (`por+eng`)

---

## 🎯 Fluxo de Uso

```
[Cliente] → POST /api/analysis/pipeline
              |
              ▼
[ImageUploadService] → Salva imagem temporária
              |
              ▼
[TesseractOcrProvider] → Extrai texto da imagem
              |
              ▼
[IngredientAllergenParser] → Analisa texto
              |
              ▼
[ProductAnalysisEngine] → Gera score e recomendações
              |
              ▼
[ProductAnalysisPipelineResultDto]
   ├── analysisResult.extractedText: "texto completo do OCR"
   ├── analysisResult.extractedIngredients: [...]
   └── metadata.ocrStep.confidence: 0.92
```

---

## 📊 Exemplo de Resposta

```json
{
  "analysisResult": {
    "extractedText": "BISCOITO RECHEADO\n\nINGREDIENTES:\nFarinha de trigo...",
    "extractedIngredients": ["Farinha de trigo", "açúcar", "chocolate"],
    "extractedAllergens": ["GLÚTEN", "SOJA"],
    "generalScore": 0.45,
    "classification": "Caution"
  },
  "metadata": {
    "ocrStep": {
      "providerName": "Tesseract OCR (Local)",
      "confidence": 0.92,
      "textLength": 487,
      "durationMs": 1234.5
    }
  }
}
```

---

## ✅ Checklist de Implementação

- [x] TesseractOcrProvider implementado
- [x] Integração no pipeline completa
- [x] Injeção de dependência configurada
- [x] Scripts de automação criados
- [x] Documentação completa escrita
- [x] Tratamento de erros implementado
- [x] Logging detalhado adicionado
- [ ] **→ Instalar pacote NuGet Tesseract**
- [ ] **→ Baixar arquivos tessdata**
- [ ] **→ Descomentar #define TESSERACT_INSTALLED**
- [ ] **→ Ativar TesseractOcrProvider na DI**
- [ ] **→ Testar com imagem real**

---

## 🚀 Próximos Passos

1. Execute: `.\setup-tesseract.ps1`
2. Descomente: `#define TESSERACT_INSTALLED`
3. Ative na DI: `TesseractOcrProvider`
4. Compile: `dotnet build`
5. Teste: `.\test-ocr-image.ps1`

---

**📖 Para detalhes completos, consulte: [TESSERACT_INSTALLATION_GUIDE.md](TESSERACT_INSTALLATION_GUIDE.md)**
