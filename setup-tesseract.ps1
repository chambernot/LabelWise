# ========================================
# Script de Setup do Tesseract OCR
# LabelWise - Extração de Texto de Rótulos
# ========================================

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   SETUP TESSERACT OCR - LabelWise                              ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Configurações
$tessdataPath = ".\tessdata"
$porUrl = "https://github.com/tesseract-ocr/tessdata_best/raw/main/por.traineddata"
$engUrl = "https://github.com/tesseract-ocr/tessdata_best/raw/main/eng.traineddata"

# ========================================
# Passo 1: Verificar/Criar Pasta Tessdata
# ========================================

Write-Host "[1/5] Verificando pasta tessdata..." -ForegroundColor Yellow

if (-Not (Test-Path $tessdataPath)) {
    Write-Host "      Pasta tessdata não encontrada. Criando..." -ForegroundColor Gray
    New-Item -ItemType Directory -Path $tessdataPath -Force | Out-Null
    Write-Host "      ✓ Pasta tessdata criada: $tessdataPath" -ForegroundColor Green
} else {
    Write-Host "      ✓ Pasta tessdata já existe" -ForegroundColor Green
}

Write-Host ""

# ========================================
# Passo 2: Download Português (por.traineddata)
# ========================================

Write-Host "[2/5] Baixando arquivo de idioma Português..." -ForegroundColor Yellow

$porFile = Join-Path $tessdataPath "por.traineddata"

if (Test-Path $porFile) {
    $fileSize = (Get-Item $porFile).Length / 1MB
    Write-Host "      ⚠ Arquivo por.traineddata já existe ($([math]::Round($fileSize, 2)) MB)" -ForegroundColor Yellow
    $overwrite = Read-Host "      Deseja baixar novamente? (S/N)"
    
    if ($overwrite -ne "S" -and $overwrite -ne "s") {
        Write-Host "      → Mantendo arquivo existente" -ForegroundColor Gray
    } else {
        Write-Host "      Baixando por.traineddata (~10 MB)..." -ForegroundColor Gray
        try {
            Invoke-WebRequest -Uri $porUrl -OutFile $porFile -UseBasicParsing
            $fileSize = (Get-Item $porFile).Length / 1MB
            Write-Host "      ✓ por.traineddata baixado com sucesso ($([math]::Round($fileSize, 2)) MB)" -ForegroundColor Green
        } catch {
            Write-Host "      ✗ ERRO ao baixar por.traineddata: $($_.Exception.Message)" -ForegroundColor Red
            Write-Host "      → Baixe manualmente de: $porUrl" -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "      Baixando por.traineddata (~10 MB)..." -ForegroundColor Gray
    Write-Host "      Isso pode levar alguns minutos..." -ForegroundColor Gray
    
    try {
        Invoke-WebRequest -Uri $porUrl -OutFile $porFile -UseBasicParsing
        $fileSize = (Get-Item $porFile).Length / 1MB
        Write-Host "      ✓ por.traineddata baixado com sucesso ($([math]::Round($fileSize, 2)) MB)" -ForegroundColor Green
    } catch {
        Write-Host "      ✗ ERRO ao baixar por.traineddata: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "      → Baixe manualmente de: $porUrl" -ForegroundColor Yellow
    }
}

Write-Host ""

# ========================================
# Passo 3: Download Inglês (eng.traineddata)
# ========================================

Write-Host "[3/5] Baixando arquivo de idioma Inglês..." -ForegroundColor Yellow

$engFile = Join-Path $tessdataPath "eng.traineddata"

if (Test-Path $engFile) {
    $fileSize = (Get-Item $engFile).Length / 1MB
    Write-Host "      ⚠ Arquivo eng.traineddata já existe ($([math]::Round($fileSize, 2)) MB)" -ForegroundColor Yellow
    $overwrite = Read-Host "      Deseja baixar novamente? (S/N)"
    
    if ($overwrite -ne "S" -and $overwrite -ne "s") {
        Write-Host "      → Mantendo arquivo existente" -ForegroundColor Gray
    } else {
        Write-Host "      Baixando eng.traineddata (~10 MB)..." -ForegroundColor Gray
        try {
            Invoke-WebRequest -Uri $engUrl -OutFile $engFile -UseBasicParsing
            $fileSize = (Get-Item $engFile).Length / 1MB
            Write-Host "      ✓ eng.traineddata baixado com sucesso ($([math]::Round($fileSize, 2)) MB)" -ForegroundColor Green
        } catch {
            Write-Host "      ✗ ERRO ao baixar eng.traineddata: $($_.Exception.Message)" -ForegroundColor Red
            Write-Host "      → Baixe manualmente de: $engUrl" -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "      Baixando eng.traineddata (~10 MB)..." -ForegroundColor Gray
    Write-Host "      Isso pode levar alguns minutos..." -ForegroundColor Gray
    
    try {
        Invoke-WebRequest -Uri $engUrl -OutFile $engFile -UseBasicParsing
        $fileSize = (Get-Item $engFile).Length / 1MB
        Write-Host "      ✓ eng.traineddata baixado com sucesso ($([math]::Round($fileSize, 2)) MB)" -ForegroundColor Green
    } catch {
        Write-Host "      ✗ ERRO ao baixar eng.traineddata: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "      → Baixe manualmente de: $engUrl" -ForegroundColor Yellow
    }
}

Write-Host ""

# ========================================
# Passo 4: Verificar Pacote NuGet
# ========================================

Write-Host "[4/5] Verificando pacote NuGet Tesseract..." -ForegroundColor Yellow

$csprojPath = "LabelWise.Infrastructure\LabelWise.Infrastructure.csproj"

if (Test-Path $csprojPath) {
    $csprojContent = Get-Content $csprojPath -Raw
    
    if ($csprojContent -match 'PackageReference.*Tesseract') {
        Write-Host "      ✓ Pacote Tesseract encontrado no projeto" -ForegroundColor Green
    } else {
        Write-Host "      ✗ Pacote Tesseract NÃO encontrado!" -ForegroundColor Red
        Write-Host "      → Instalando pacote Tesseract..." -ForegroundColor Yellow
        
        try {
            dotnet add LabelWise.Infrastructure package Tesseract --version 5.2.0
            Write-Host "      ✓ Pacote Tesseract instalado com sucesso" -ForegroundColor Green
        } catch {
            Write-Host "      ✗ ERRO ao instalar pacote. Execute manualmente:" -ForegroundColor Red
            Write-Host "        dotnet add LabelWise.Infrastructure package Tesseract" -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "      ⚠ Arquivo LabelWise.Infrastructure.csproj não encontrado" -ForegroundColor Yellow
    Write-Host "      → Certifique-se de executar este script da raiz da solução" -ForegroundColor Yellow
}

Write-Host ""

# ========================================
# Passo 5: Verificação Final
# ========================================

Write-Host "[5/5] Verificação Final..." -ForegroundColor Yellow
Write-Host ""

$allGood = $true

# Verificar tessdata
if (Test-Path $tessdataPath) {
    Write-Host "   ✓ Pasta tessdata existe" -ForegroundColor Green
} else {
    Write-Host "   ✗ Pasta tessdata NÃO existe" -ForegroundColor Red
    $allGood = $false
}

# Verificar por.traineddata
if (Test-Path $porFile) {
    $size = (Get-Item $porFile).Length / 1MB
    Write-Host "   ✓ por.traineddata ($([math]::Round($size, 2)) MB)" -ForegroundColor Green
} else {
    Write-Host "   ✗ por.traineddata NÃO encontrado" -ForegroundColor Red
    $allGood = $false
}

# Verificar eng.traineddata
if (Test-Path $engFile) {
    $size = (Get-Item $engFile).Length / 1MB
    Write-Host "   ✓ eng.traineddata ($([math]::Round($size, 2)) MB)" -ForegroundColor Green
} else {
    Write-Host "   ✗ eng.traineddata NÃO encontrado" -ForegroundColor Red
    $allGood = $false
}

Write-Host ""

# ========================================
# Resultado Final
# ========================================

if ($allGood) {
    Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║   ✓ SETUP CONCLUÍDO COM SUCESSO!                              ║" -ForegroundColor Green
    Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Green
    Write-Host ""
    Write-Host "Próximos passos:" -ForegroundColor Cyan
    Write-Host "  1. Compile o projeto: dotnet build" -ForegroundColor White
    Write-Host "  2. Execute a API: .\run-api.ps1 ou dotnet run --project LabelWise.Api" -ForegroundColor White
    Write-Host "  3. Teste o OCR enviando uma imagem via endpoint:" -ForegroundColor White
    Write-Host "     POST http://localhost:5000/api/analysis/pipeline" -ForegroundColor White
    Write-Host ""
    Write-Host "Para mais detalhes, consulte: TESSERACT_INSTALLATION_GUIDE.md" -ForegroundColor Gray
} else {
    Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Red
    Write-Host "║   ✗ SETUP INCOMPLETO - Verifique os erros acima               ║" -ForegroundColor Red
    Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Red
    Write-Host ""
    Write-Host "Consulte o guia completo: TESSERACT_INSTALLATION_GUIDE.md" -ForegroundColor Yellow
}

Write-Host ""
