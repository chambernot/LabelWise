# 📂 Onde Colocar os Arquivos Tessdata - Guia Definitivo

## 🎯 Localização Recomendada

A melhor localização para os arquivos de idioma do Tesseract é **na raiz da solução**, criando uma pasta `tessdata`:

```
C:\Users\chamb\source\repos\LabelWise\
├── tessdata\                              ← CRIAR AQUI
│   ├── por.traineddata                    ← Português
│   └── eng.traineddata                    ← Inglês
├── LabelWise.Api\
├── LabelWise.Infrastructure\
├── LabelWise.Application\
└── LabelWise.sln
```

---

## 📥 Download dos Arquivos

### Links Diretos

**Português (Brasil):**
```
https://github.com/tesseract-ocr/tessdata_best/raw/main/por.traineddata
```

**Inglês (fallback):**
```
https://github.com/tesseract-ocr/tessdata_best/raw/main/eng.traineddata
```

### Via PowerShell (Windows)

```powershell
# Na raiz da solução LabelWise
mkdir tessdata

# Baixar Português
Invoke-WebRequest -Uri "https://github.com/tesseract-ocr/tessdata_best/raw/main/por.traineddata" `
                  -OutFile "tessdata\por.traineddata"

# Baixar Inglês
Invoke-WebRequest -Uri "https://github.com/tesseract-ocr/tessdata_best/raw/main/eng.traineddata" `
                  -OutFile "tessdata\eng.traineddata"
```

### Via cURL (se disponível)

```bash
# Na raiz da solução LabelWise
mkdir tessdata

curl -L "https://github.com/tesseract-ocr/tessdata_best/raw/main/por.traineddata" \
     -o "tessdata/por.traineddata"

curl -L "https://github.com/tesseract-ocr/tessdata_best/raw/main/eng.traineddata" \
     -o "tessdata/eng.traineddata"
```

---

## 🔍 Como o TesseractOcrProvider Encontra os Arquivos

O provider busca os arquivos na seguinte ordem de prioridade:

### 1️⃣ Parâmetro do Construtor (Maior Prioridade)

```csharp
var ocrProvider = new TesseractOcrProvider(
    tessdataPath: @"C:\meu-caminho-customizado\tessdata"
);
```

### 2️⃣ Variável de Ambiente `TESSDATA_PREFIX`

**PowerShell (temporário):**
```powershell
$env:TESSDATA_PREFIX = "C:\Users\chamb\source\repos\LabelWise\tessdata"
```

**CMD (temporário):**
```cmd
set TESSDATA_PREFIX=C:\Users\chamb\source\repos\LabelWise\tessdata
```

**Windows Permanente:**
1. Pressione `Win + Pause`
2. **Advanced system settings** → **Environment Variables**
3. Adicionar variável de sistema:
   - Nome: `TESSDATA_PREFIX`
   - Valor: `C:\Users\chamb\source\repos\LabelWise\tessdata`

### 3️⃣ Pasta Padrão: `.\tessdata\` (Menor Prioridade)

Se nenhuma das opções acima for configurada, o provider busca em:
```
[Diretório de Execução]\tessdata\
```

Para a API LabelWise, isso seria:
```
C:\Users\chamb\source\repos\LabelWise\LabelWise.Api\bin\Debug\net10.0\tessdata\
```

⚠️ **Não recomendado** porque a pasta `bin` é limpa a cada compilação.

---

## ✅ Estrutura Final Esperada

Após o download, você deve ter:

```
C:\Users\chamb\source\repos\LabelWise\tessdata\
├── por.traineddata    (10.3 MB)
└── eng.traineddata    (9.8 MB)
```

### Verificar Tamanho dos Arquivos

**PowerShell:**
```powershell
Get-ChildItem .\tessdata\*.traineddata | Format-Table Name, @{Label="Size (MB)"; Expression={[math]::Round($_.Length/1MB, 2)}}
```

**Resultado esperado:**
```
Name               Size (MB)
----               ---------
eng.traineddata         9.80
por.traineddata        10.30
```

---

## 🔄 Opções Alternativas

### Opção 1: Dentro do Projeto Infrastructure

```
LabelWise.Infrastructure\
└── tessdata\
    ├── por.traineddata
    └── eng.traineddata
```

**Configuração:**
```csharp
var tessdataPath = Path.Combine(
    AppDomain.CurrentDomain.BaseDirectory, 
    "..", "..", "..", "..", 
    "LabelWise.Infrastructure", 
    "tessdata"
);
```

⚠️ Complexo de configurar corretamente.

### Opção 2: Diretório do Sistema

```
C:\tessdata\
├── por.traineddata
└── eng.traineddata
```

**Configuração:**
```powershell
$env:TESSDATA_PREFIX = "C:\tessdata"
```

✅ Funciona bem para múltiplos projetos.  
⚠️ Requer permissões de administrador.

### Opção 3: User Profile

```
C:\Users\chamb\.tessdata\
├── por.traineddata
└── eng.traineddata
```

**Configuração:**
```powershell
$env:TESSDATA_PREFIX = "$env:USERPROFILE\.tessdata"
```

✅ Sem necessidade de permissões admin.  
⚠️ Específico para o usuário logado.

---

## 🧪 Validar Instalação

### Script de Validação

```powershell
# Validar estrutura
Write-Host "Validando tessdata..." -ForegroundColor Cyan

$tessdataPath = ".\tessdata"

if (Test-Path $tessdataPath) {
    Write-Host "✓ Pasta tessdata encontrada" -ForegroundColor Green
    
    $porFile = Test-Path "$tessdataPath\por.traineddata"
    $engFile = Test-Path "$tessdataPath\eng.traineddata"
    
    if ($porFile) {
        $size = (Get-Item "$tessdataPath\por.traineddata").Length / 1MB
        Write-Host "✓ por.traineddata: $([math]::Round($size, 2)) MB" -ForegroundColor Green
    } else {
        Write-Host "✗ por.traineddata NÃO encontrado" -ForegroundColor Red
    }
    
    if ($engFile) {
        $size = (Get-Item "$tessdataPath\eng.traineddata").Length / 1MB
        Write-Host "✓ eng.traineddata: $([math]::Round($size, 2)) MB" -ForegroundColor Green
    } else {
        Write-Host "✗ eng.traineddata NÃO encontrado" -ForegroundColor Red
    }
} else {
    Write-Host "✗ Pasta tessdata NÃO encontrada" -ForegroundColor Red
}
```

### Verificar via API

Após iniciar a API, verifique os logs:

```
info: LabelWise.Infrastructure.Ocr.TesseractOcrProvider[0]
      TesseractOcrProvider inicializado. Tessdata path: C:\...\tessdata, Language: por+eng
info: LabelWise.Infrastructure.Ocr.TesseractOcrProvider[0]
      Tessdata encontrado. Arquivos disponíveis: por.traineddata, eng.traineddata
```

---

## 🚨 Problemas Comuns

### "Tessdata não encontrado"

**Causa:** Pasta no lugar errado ou nome incorreto.

**Solução:**
1. Verifique se a pasta se chama exatamente `tessdata` (minúsculo)
2. Confirme que está na raiz da solução
3. Execute o script de validação acima

### "Failed to initialize tesseract engine"

**Causa:** Arquivos `.traineddata` corrompidos ou versão errada.

**Solução:**
1. Delete os arquivos existentes
2. Re-baixe do GitHub (tessdata_best)
3. Confirme o tamanho dos arquivos (~10 MB cada)

### OCR sempre retorna erro

**Causa:** Caminho absoluto vs relativo.

**Solução:**
1. Use caminho absoluto na variável de ambiente
2. Ou garanta que a pasta `tessdata` está na raiz da solução
3. Verifique permissões de leitura nos arquivos

---

## 📋 Checklist Final

Antes de usar o Tesseract, confirme:

- [ ] Pasta `tessdata` criada na raiz da solução
- [ ] Arquivo `por.traineddata` (~10 MB) presente
- [ ] Arquivo `eng.traineddata` (~10 MB) presente
- [ ] Permissões de leitura nos arquivos OK
- [ ] Pacote NuGet `Tesseract` instalado (5.2.0+)
- [ ] `#define TESSERACT_INSTALLED` descomentado
- [ ] API compilada com sucesso
- [ ] Logs mostram "Tessdata encontrado"

---

## 🔗 Links Úteis

- **tessdata_best (Recomendado):** https://github.com/tesseract-ocr/tessdata_best
- **tessdata (Normal):** https://github.com/tesseract-ocr/tessdata
- **tessdata_fast (Rápido):** https://github.com/tesseract-ocr/tessdata_fast
- **Lista de Idiomas:** https://tesseract-ocr.github.io/tessdoc/Data-Files-in-different-versions.html

---

## 🎯 Resumo: Localização Ideal

```
✅ RECOMENDADO:
C:\Users\chamb\source\repos\LabelWise\tessdata\
├── por.traineddata
└── eng.traineddata

⚠️ ALTERNATIVO:
Variável de ambiente TESSDATA_PREFIX apontando para qualquer pasta

❌ NÃO RECOMENDADO:
C:\Users\chamb\source\repos\LabelWise\LabelWise.Api\bin\Debug\net10.0\tessdata\
(pasta bin é limpa a cada compilação)
```

---

**🚀 Pronto! Execute `.\setup-tesseract.ps1` para configuração automática.**
