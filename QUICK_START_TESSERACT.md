# 🚀 QUICK START - Tesseract OCR

## ⚡ 3 Comandos para Começar

### 1️⃣ Setup Automático do Tesseract
```powershell
.\setup-tesseract-complete.ps1
```
**O que faz**:
- Cria diretório `LabelWise.Api\tessdata`
- Baixa `por.traineddata` e `eng.traineddata`
- Valida os arquivos
- Compila o projeto

---

### 2️⃣ Iniciar a API
```powershell
dotnet run --project LabelWise.Api
```

**Verificar os logs**:
```
✅ TESSERACT PROVIDER SELECTED
   🚀 Using TesseractOcrProvider (REAL OCR)
   ✅ Tesseract validated successfully!
```

---

### 3️⃣ Testar no Swagger

1. **Acesse**: https://localhost:7001/swagger
2. **Endpoint**: POST `/api/pipeline/analyze-image`
3. **Faça upload** de uma imagem de rótulo
4. **Verifique o metadata**:

```json
{
  "ocrResult": {
    "providerMetadata": {
      "ProviderName": "Tesseract OCR (Local)",
      "IsMock": "false",
      "TessdataExists": "True"
    }
  }
}
```

---

## ✅ Sucesso: IsMock = "false"
## ❌ Erro: IsMock = "true" (significa que está usando Mock)

---

## 🔧 Troubleshooting Rápido

### Problema: "Tessdata não encontrado"
**Solução**:
1. Execute `.\setup-tesseract-complete.ps1`
2. OU manualmente:
   ```powershell
   cd LabelWise.Api
   mkdir tessdata
   # Baixe de: https://github.com/tesseract-ocr/tessdata
   # Arquivos: por.traineddata, eng.traineddata
   ```

### Problema: Sistema ainda usa Mock
**Solução**: Verifique `appsettings.json`:
```json
{
  "OCR": {
    "UseMockProvider": false
  }
}
```

### Problema: Build não copia arquivos
**Solução**:
```powershell
dotnet clean
dotnet build
```

---

## 📚 Documentação Completa

- **Setup Completo**: `TESSERACT_OCR_SETUP_COMPLETE.md`
- **Validação**: `TESSERACT_OCR_VALIDATION.md`

---

## 🎯 Resultado Esperado

Ao executar a API e fazer uma requisição OCR:

1. ✅ Provider usado: **Tesseract OCR (Local)**
2. ✅ IsMock: **false**
3. ✅ Texto real extraído da imagem
4. ✅ Confidence score real (não simulado)
5. ✅ Metadata completo com caminhos e arquivos

---

**Data**: Hoje
**Status**: ✅ PRONTO PARA USO
