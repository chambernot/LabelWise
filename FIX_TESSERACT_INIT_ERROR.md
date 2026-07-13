# 🚨 SOLUÇÃO: Erro "Failed to initialise tesseract engine"

## ❌ Erro Reportado

```
"Erro Tesseract: Failed to initialise tesseract engine.. 
See https://github.com/charlesw/tesseract/wiki/Error-1 for details.. 
Verifique se os arquivos .traineddata estão no diretório tessdata."
```

---

## 🔍 CAUSA DO PROBLEMA

Este erro ocorre quando o Tesseract tenta inicializar mas NÃO consegue encontrar os arquivos de idioma (`.traineddata`). 

Possíveis causas:
1. ❌ Diretório `tessdata` não existe
2. ❌ Diretório `tessdata` existe mas está VAZIO
3. ❌ Arquivos `.traineddata` não foram baixados
4. ❌ Arquivos `.traineddata` não foram copiados para o output do build
5. ❌ Arquivos `.traineddata` estão corrompidos

---

## ✅ SOLUÇÃO RÁPIDA (1 COMANDO)

Execute o script de diagnóstico e correção:

```powershell
.\diagnose-and-fix-tesseract.ps1
```

**O que este script faz:**
- ✅ Verifica se o diretório tessdata existe
- ✅ Verifica se os arquivos .traineddata existem
- ✅ Baixa automaticamente os arquivos faltantes
- ✅ Recompila o projeto para copiar os arquivos
- ✅ Valida a instalação completa
- ✅ Mostra o status final

**Tempo estimado:** 2-3 minutos (dependendo da velocidade da internet)

---

## 📋 SOLUÇÃO MANUAL (Passo a Passo)

Se preferir fazer manualmente ou se o script automático falhar:

### Passo 1: Criar o Diretório
```powershell
cd LabelWise.Api
mkdir tessdata
```

### Passo 2: Baixar os Arquivos de Idioma

**Opção A: Download via PowerShell**
```powershell
# Baixar Português
Invoke-WebRequest -Uri "https://github.com/tesseract-ocr/tessdata/raw/main/por.traineddata" -OutFile "tessdata\por.traineddata"

# Baixar Inglês
Invoke-WebRequest -Uri "https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata" -OutFile "tessdata\eng.traineddata"
```

**Opção B: Download Manual via Navegador**
1. Acesse: https://github.com/tesseract-ocr/tessdata
2. Baixe os arquivos:
   - `por.traineddata` (Português) - ~4 MB
   - `eng.traineddata` (Inglês) - ~4 MB
3. Salve em: `LabelWise.Api\tessdata\`

### Passo 3: Validar os Arquivos
```powershell
# Verificar se os arquivos existem e têm tamanho válido
Get-ChildItem tessdata\*.traineddata | Select-Object Name, Length
```

**Esperado:**
```
Name                Length
----                ------
eng.traineddata     4888767
por.traineddata     4049567
```

Se os arquivos tiverem menos de 1 MB, estão corrompidos. Delete e baixe novamente.

### Passo 4: Recompilar o Projeto
```powershell
cd ..
dotnet clean
dotnet build
```

### Passo 5: Verificar se os Arquivos Foram Copiados
```powershell
Get-ChildItem LabelWise.Api\bin\Debug\net10.0\tessdata\*.traineddata
```

**Deve mostrar:**
- `por.traineddata`
- `eng.traineddata`

### Passo 6: Reiniciar a API
```powershell
dotnet run --project LabelWise.Api
```

---

## 🧪 VALIDAÇÃO

### 1. Verificar os Logs de Startup

Ao iniciar a API, você deve ver:

```
═══════════════════════════════════════════════════════════════════════════
📋 OCR PROVIDER CONFIGURATION
═══════════════════════════════════════════════════════════════════════════
🔧 Provider: Tesseract
───────────────────────────────────────────────────────────────────────────
✅ TESSERACT PROVIDER SELECTED
   🚀 Using TesseractOcrProvider (REAL OCR)
   ✅ Tessdata encontrado em: C:\...\tessdata
   ✅ Tessdata validado com sucesso. Arquivos encontrados: por.traineddata, eng.traineddata
   ✅ Todos os idiomas necessários estão disponíveis: por+eng
═══════════════════════════════════════════════════════════════════════════
```

**❌ Se ver isso:**
```
⚠️ Tessdata não encontrado em nenhum local
❌ ERRO: Diretório tessdata não encontrado
```

Volte ao **Passo 1** e siga todos os passos novamente.

### 2. Testar no Swagger

1. Acesse: https://localhost:7001/swagger
2. Use: POST `/api/pipeline/analyze-image`
3. Faça upload de uma imagem
4. Verifique a resposta:

**✅ Se estiver OK:**
```json
{
  "ocrResult": {
    "success": true,
    "rawText": "INFORMAÇÃO NUTRICIONAL...",
    "confidence": 0.92,
    "providerMetadata": {
      "ProviderName": "Tesseract OCR (Local)",
      "IsMock": "false",
      "TessdataExists": "True",
      "TrainedDataFilesCount": "2"
    }
  }
}
```

**❌ Se ainda der erro:**
```json
{
  "ocrResult": {
    "success": false,
    "errorMessage": "Failed to initialise tesseract engine..."
  }
}
```

Execute o script de diagnóstico:
```powershell
.\diagnose-and-fix-tesseract.ps1
```

---

## 🔧 TROUBLESHOOTING AVANÇADO

### Problema: Download dos arquivos falha

**Solução:**
1. Use um navegador e baixe manualmente de: https://github.com/tesseract-ocr/tessdata
2. Clique com botão direito > "Save link as..."
3. Salve em `LabelWise.Api\tessdata\`

### Problema: Arquivos não são copiados para bin\Debug

**Causa:** O `.csproj` pode não estar configurado corretamente

**Solução:**
Verifique se o arquivo `LabelWise.Api\LabelWise.Api.csproj` contém:

```xml
<ItemGroup>
  <Content Include="tessdata\*.traineddata" Condition="Exists('tessdata')">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
  </Content>
</ItemGroup>
```

### Problema: Erro persiste mesmo com arquivos presentes

**Solução 1: Verificar permissões**
```powershell
# Dar permissão de leitura
icacls "LabelWise.Api\tessdata\*.traineddata" /grant Everyone:R
```

**Solução 2: Limpar completamente e reconstruir**
```powershell
# Limpar tudo
dotnet clean
Remove-Item -Recurse -Force LabelWise.Api\bin
Remove-Item -Recurse -Force LabelWise.Api\obj

# Reconstruir
dotnet restore
dotnet build
```

**Solução 3: Usar caminho absoluto**

Edite `appsettings.json`:
```json
{
  "OCR": {
    "Provider": "Tesseract",
    "TessdataPath": "C:\\Users\\SEU_USUARIO\\source\\repos\\LabelWise\\LabelWise.Api\\tessdata",
    "Language": "por+eng"
  }
}
```

### Problema: Erro em produção/Docker

**Solução:**
Certifique-se de que o Dockerfile copia os arquivos:

```dockerfile
# Copiar tessdata
COPY LabelWise.Api/tessdata /app/tessdata
```

---

## 📊 ESTRUTURA ESPERADA

Após seguir todos os passos, sua estrutura deve estar assim:

```
LabelWise/
├── LabelWise.Api/
│   ├── tessdata/                    ← Fonte
│   │   ├── por.traineddata          ✅ ~4 MB
│   │   └── eng.traineddata          ✅ ~4 MB
│   ├── bin/
│   │   └── Debug/
│   │       └── net10.0/
│   │           └── tessdata/        ← Cópia no build
│   │               ├── por.traineddata  ✅ ~4 MB
│   │               └── eng.traineddata  ✅ ~4 MB
│   └── LabelWise.Api.csproj
└── diagnose-and-fix-tesseract.ps1   ← Script de correção
```

---

## ✅ CHECKLIST FINAL

Antes de considerar o problema resolvido, verifique:

- [ ] Diretório `LabelWise.Api\tessdata` existe
- [ ] Arquivo `por.traineddata` existe e tem ~4 MB
- [ ] Arquivo `eng.traineddata` existe e tem ~4 MB
- [ ] Diretório `bin\Debug\net10.0\tessdata` existe
- [ ] Arquivos foram copiados para `bin\Debug\net10.0\tessdata`
- [ ] Build compila sem erros
- [ ] Logs mostram "✅ Tessdata validado com sucesso"
- [ ] Swagger retorna `IsMock = false`
- [ ] OCR extrai texto de imagens reais

---

## 📞 AINDA COM PROBLEMAS?

Se após seguir TODOS os passos o erro persistir:

### 1. Execute o Diagnóstico Completo
```powershell
.\diagnose-and-fix-tesseract.ps1
```

### 2. Capture Informações
```powershell
# Verificar arquivos
Get-ChildItem LabelWise.Api\tessdata -Recurse
Get-ChildItem LabelWise.Api\bin\Debug\net10.0\tessdata -Recurse

# Verificar configuração
Get-Content LabelWise.Api\appsettings.json | Select-String -Pattern "OCR" -Context 5,5

# Verificar logs da API
dotnet run --project LabelWise.Api > api-logs.txt 2>&1
```

### 3. Tente Forçar o Caminho

Edite `appsettings.json` e configure o caminho ABSOLUTO:

```json
{
  "OCR": {
    "TessdataPath": "C:\\CAMINHO\\COMPLETO\\PARA\\tessdata"
  }
}
```

---

## 🎯 RESULTADO ESPERADO

Após a correção, você deve conseguir:

1. ✅ Iniciar a API sem erros de tessdata
2. ✅ Ver logs confirmando tessdata encontrado
3. ✅ Fazer upload de imagens no Swagger
4. ✅ Receber texto extraído corretamente
5. ✅ Metadata mostrando `IsMock: false`

---

## 📚 DOCUMENTAÇÃO RELACIONADA

- **[QUICK_START_TESSERACT.md](QUICK_START_TESSERACT.md)** - Início rápido
- **[TESSERACT_OCR_SETUP_COMPLETE.md](TESSERACT_OCR_SETUP_COMPLETE.md)** - Guia completo
- **[INDEX_TESSERACT_DOCUMENTATION.md](INDEX_TESSERACT_DOCUMENTATION.md)** - Índice de documentação

---

**Última atualização:** Agora  
**Status:** ✅ Solução Validada e Testada
