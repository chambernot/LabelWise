# ═══════════════════════════════════════════════════════════════════════════
# Script de Setup Automático do Tesseract OCR para LabelWise
# ═══════════════════════════════════════════════════════════════════════════

Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🔧 TESSERACT OCR - Setup Automático para LabelWise" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Stop"

# Diretórios
$projectRoot = $PSScriptRoot
$apiDir = Join-Path $projectRoot "LabelWise.Api"
$tessdataDir = Join-Path $apiDir "tessdata"

Write-Host "📂 Diretórios:" -ForegroundColor Yellow
Write-Host "   Projeto: $projectRoot"
Write-Host "   API: $apiDir"
Write-Host "   Tessdata: $tessdataDir"
Write-Host ""

# ═══════════════════════════════════════════════════════════════════════════
# Passo 1: Criar diretório tessdata
# ═══════════════════════════════════════════════════════════════════════════

Write-Host "[1/4] Verificando diretório tessdata..." -ForegroundColor Yellow

if (Test-Path $tessdataDir) {
    Write-Host "   ✅ Diretório tessdata já existe: $tessdataDir" -ForegroundColor Green
} else {
    Write-Host "   📁 Criando diretório tessdata..." -ForegroundColor Yellow
    New-Item -Path $tessdataDir -ItemType Directory -Force | Out-Null
    Write-Host "   ✅ Diretório criado: $tessdataDir" -ForegroundColor Green
}

Write-Host ""

# ═══════════════════════════════════════════════════════════════════════════
# Passo 2: Baixar arquivos de idioma
# ═══════════════════════════════════════════════════════════════════════════

Write-Host "[2/4] Verificando arquivos de idioma..." -ForegroundColor Yellow

$languages = @(
    @{ Code = "por"; Name = "Português"; Url = "https://github.com/tesseract-ocr/tessdata/raw/main/por.traineddata" },
    @{ Code = "eng"; Name = "Inglês"; Url = "https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata" }
)

$downloadNeeded = $false

foreach ($lang in $languages) {
    $filePath = Join-Path $tessdataDir "$($lang.Code).traineddata"
    
    if (Test-Path $filePath) {
        $fileSize = (Get-Item $filePath).Length / 1MB
        Write-Host "   ✅ $($lang.Name) ($($lang.Code).traineddata) já existe ($('{0:N2}' -f $fileSize) MB)" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  $($lang.Name) ($($lang.Code).traineddata) não encontrado" -ForegroundColor Yellow
        $downloadNeeded = $true
    }
}

Write-Host ""

if ($downloadNeeded) {
    Write-Host "📥 Baixando arquivos de idioma necessários..." -ForegroundColor Yellow
    Write-Host ""
    
    foreach ($lang in $languages) {
        $filePath = Join-Path $tessdataDir "$($lang.Code).traineddata"
        
        if (-not (Test-Path $filePath)) {
            Write-Host "   📥 Baixando $($lang.Name) ($($lang.Code).traineddata)..." -ForegroundColor Cyan
            Write-Host "      URL: $($lang.Url)" -ForegroundColor Gray
            
            try {
                $ProgressPreference = 'SilentlyContinue'
                Invoke-WebRequest -Uri $lang.Url -OutFile $filePath -UseBasicParsing
                
                $fileSize = (Get-Item $filePath).Length / 1MB
                Write-Host "      ✅ Download completo! Tamanho: $('{0:N2}' -f $fileSize) MB" -ForegroundColor Green
            }
            catch {
                Write-Host "      ❌ ERRO ao baixar: $($_.Exception.Message)" -ForegroundColor Red
                Write-Host ""
                Write-Host "⚠️  SOLUÇÃO MANUAL:" -ForegroundColor Yellow
                Write-Host "   1. Baixe manualmente de: $($lang.Url)" -ForegroundColor Yellow
                Write-Host "   2. Salve em: $filePath" -ForegroundColor Yellow
            }
            
            Write-Host ""
        }
    }
} else {
    Write-Host "   ✅ Todos os arquivos de idioma já estão presentes!" -ForegroundColor Green
    Write-Host ""
}

# ═══════════════════════════════════════════════════════════════════════════
# Passo 3: Validar arquivos
# ═══════════════════════════════════════════════════════════════════════════

Write-Host "[3/4] Validando arquivos..." -ForegroundColor Yellow

$allFilesPresent = $true

foreach ($lang in $languages) {
    $filePath = Join-Path $tessdataDir "$($lang.Code).traineddata"
    
    if (Test-Path $filePath) {
        $fileSize = (Get-Item $filePath).Length
        
        if ($fileSize -gt 100KB) {
            Write-Host "   ✅ $($lang.Code).traineddata válido ($('{0:N2}' -f ($fileSize / 1MB)) MB)" -ForegroundColor Green
        } else {
            Write-Host "   ❌ $($lang.Code).traineddata inválido (muito pequeno: $fileSize bytes)" -ForegroundColor Red
            $allFilesPresent = $false
        }
    } else {
        Write-Host "   ❌ $($lang.Code).traineddata não encontrado" -ForegroundColor Red
        $allFilesPresent = $false
    }
}

Write-Host ""

# ═══════════════════════════════════════════════════════════════════════════
# Passo 4: Rebuild do projeto
# ═══════════════════════════════════════════════════════════════════════════

if ($allFilesPresent) {
    Write-Host "[4/4] Compilando projeto..." -ForegroundColor Yellow
    
    Push-Location $projectRoot
    
    try {
        Write-Host "   🔨 Executando: dotnet build" -ForegroundColor Cyan
        dotnet build --nologo --verbosity quiet
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "   ✅ Build concluído com sucesso!" -ForegroundColor Green
        } else {
            Write-Host "   ⚠️  Build completou com avisos/erros" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "   ❌ Erro durante build: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    Pop-Location
    Write-Host ""
} else {
    Write-Host "[4/4] ⚠️  Pulando build - alguns arquivos estão faltando" -ForegroundColor Yellow
    Write-Host ""
}

# ═══════════════════════════════════════════════════════════════════════════
# Resumo Final
# ═══════════════════════════════════════════════════════════════════════════

Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "📋 RESUMO DO SETUP" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

if ($allFilesPresent) {
    Write-Host "✅ Setup do Tesseract OCR CONCLUÍDO COM SUCESSO!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📂 Arquivos tessdata instalados em:" -ForegroundColor Yellow
    Write-Host "   $tessdataDir" -ForegroundColor Gray
    Write-Host ""
    Write-Host "📝 Próximos passos:" -ForegroundColor Yellow
    Write-Host "   1. Execute a API: dotnet run --project LabelWise.Api" -ForegroundColor White
    Write-Host "   2. Acesse o Swagger: https://localhost:7001/swagger" -ForegroundColor White
    Write-Host "   3. Teste o endpoint /api/pipeline/analyze-image" -ForegroundColor White
    Write-Host "   4. Verifique o campo 'providerMetadata' na resposta" -ForegroundColor White
    Write-Host ""
    Write-Host "✅ O provider deve mostrar:" -ForegroundColor Green
    Write-Host "   - ProviderName: 'Tesseract OCR (Local)'" -ForegroundColor Gray
    Write-Host "   - IsMock: 'false'" -ForegroundColor Gray
    Write-Host "   - TessdataExists: 'True'" -ForegroundColor Gray
    Write-Host ""
} else {
    Write-Host "⚠️  Setup INCOMPLETO - alguns arquivos estão faltando" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "📝 Ação necessária:" -ForegroundColor Yellow
    Write-Host "   Baixe manualmente os arquivos de:" -ForegroundColor White
    Write-Host "   https://github.com/tesseract-ocr/tessdata" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "   Salve em: $tessdataDir" -ForegroundColor White
    Write-Host ""
    Write-Host "   Arquivos necessários:" -ForegroundColor Yellow
    Write-Host "   - por.traineddata (Português)" -ForegroundColor White
    Write-Host "   - eng.traineddata (Inglês)" -ForegroundColor White
    Write-Host ""
    Write-Host "   Depois execute novamente este script" -ForegroundColor White
    Write-Host ""
}

Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Pausa ao final
Write-Host "Pressione qualquer tecla para sair..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
