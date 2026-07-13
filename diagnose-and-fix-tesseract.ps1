# ═══════════════════════════════════════════════════════════════════════════
# Script de Diagnóstico e Correção do Tesseract OCR
# ═══════════════════════════════════════════════════════════════════════════

Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🔍 DIAGNÓSTICO E CORREÇÃO DO TESSERACT OCR" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Continue"

# Diretórios
$projectRoot = $PSScriptRoot
$apiDir = Join-Path $projectRoot "LabelWise.Api"
$tessdataDir = Join-Path $apiDir "tessdata"
$binTessdataDir = Join-Path $apiDir "bin\Debug\net10.0\tessdata"

Write-Host "[PASSO 1] Verificando diretórios..." -ForegroundColor Yellow
Write-Host "   Projeto: $projectRoot" -ForegroundColor Gray
Write-Host "   API: $apiDir" -ForegroundColor Gray
Write-Host "   Tessdata fonte: $tessdataDir" -ForegroundColor Gray
Write-Host "   Tessdata bin: $binTessdataDir" -ForegroundColor Gray
Write-Host ""

# Verificar diretório tessdata fonte
if (Test-Path $tessdataDir) {
    Write-Host "   ✅ Diretório tessdata existe: $tessdataDir" -ForegroundColor Green
    
    $files = Get-ChildItem $tessdataDir -Filter "*.traineddata" -ErrorAction SilentlyContinue
    if ($files.Count -gt 0) {
        Write-Host "   ✅ Arquivos .traineddata encontrados: $($files.Count)" -ForegroundColor Green
        foreach ($file in $files) {
            $sizeKB = [math]::Round($file.Length / 1KB, 2)
            Write-Host "      - $($file.Name) ($sizeKB KB)" -ForegroundColor Gray
        }
    } else {
        Write-Host "   ❌ PROBLEMA: Nenhum arquivo .traineddata encontrado!" -ForegroundColor Red
        Write-Host "      O diretório existe mas está vazio" -ForegroundColor Yellow
    }
} else {
    Write-Host "   ❌ PROBLEMA: Diretório tessdata NÃO existe!" -ForegroundColor Red
}

Write-Host ""

# Verificar diretório bin
if (Test-Path $binTessdataDir) {
    Write-Host "   ✅ Diretório bin/tessdata existe: $binTessdataDir" -ForegroundColor Green
    
    $binFiles = Get-ChildItem $binTessdataDir -Filter "*.traineddata" -ErrorAction SilentlyContinue
    if ($binFiles.Count -gt 0) {
        Write-Host "   ✅ Arquivos .traineddata no bin: $($binFiles.Count)" -ForegroundColor Green
        foreach ($file in $binFiles) {
            $sizeKB = [math]::Round($file.Length / 1KB, 2)
            Write-Host "      - $($file.Name) ($sizeKB KB)" -ForegroundColor Gray
        }
    } else {
        Write-Host "   ❌ PROBLEMA: Nenhum arquivo .traineddata no bin!" -ForegroundColor Red
    }
} else {
    Write-Host "   ⚠️  Diretório bin/tessdata não existe (ainda não foi compilado)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Determinar ações necessárias
$needsDownload = $false
$needsCompile = $false

if (-not (Test-Path $tessdataDir)) {
    Write-Host "🔧 AÇÃO 1: Criar diretório tessdata" -ForegroundColor Yellow
    New-Item -Path $tessdataDir -ItemType Directory -Force | Out-Null
    Write-Host "   ✅ Diretório criado: $tessdataDir" -ForegroundColor Green
    $needsDownload = $true
} else {
    $files = Get-ChildItem $tessdataDir -Filter "*.traineddata" -ErrorAction SilentlyContinue
    if ($files.Count -eq 0) {
        $needsDownload = $true
    }
}

Write-Host ""

if ($needsDownload) {
    Write-Host "🔧 AÇÃO 2: Baixar arquivos de idioma" -ForegroundColor Yellow
    Write-Host ""
    
    $languages = @(
        @{ 
            Code = "por"
            Name = "Português"
            Url = "https://github.com/tesseract-ocr/tessdata/raw/main/por.traineddata"
        },
        @{ 
            Code = "eng"
            Name = "Inglês"
            Url = "https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata"
        }
    )
    
    $downloadSuccess = $true
    
    foreach ($lang in $languages) {
        $filePath = Join-Path $tessdataDir "$($lang.Code).traineddata"
        
        if (Test-Path $filePath) {
            $fileSize = (Get-Item $filePath).Length / 1MB
            Write-Host "   ⚠️  $($lang.Name) ($($lang.Code).traineddata) já existe ($('{0:N2}' -f $fileSize) MB)" -ForegroundColor Yellow
            Write-Host "      Pulando download..." -ForegroundColor Gray
        } else {
            Write-Host "   📥 Baixando $($lang.Name) ($($lang.Code).traineddata)..." -ForegroundColor Cyan
            Write-Host "      URL: $($lang.Url)" -ForegroundColor Gray
            
            try {
                $ProgressPreference = 'SilentlyContinue'
                Invoke-WebRequest -Uri $lang.Url -OutFile $filePath -UseBasicParsing -TimeoutSec 120
                
                if (Test-Path $filePath) {
                    $fileSize = (Get-Item $filePath).Length / 1MB
                    if ($fileSize -gt 0.1) {
                        Write-Host "      ✅ Download completo! Tamanho: $('{0:N2}' -f $fileSize) MB" -ForegroundColor Green
                    } else {
                        Write-Host "      ❌ Arquivo baixado mas muito pequeno ($fileSize MB)" -ForegroundColor Red
                        Remove-Item $filePath -Force
                        $downloadSuccess = $false
                    }
                } else {
                    Write-Host "      ❌ Arquivo não foi criado" -ForegroundColor Red
                    $downloadSuccess = $false
                }
            }
            catch {
                Write-Host "      ❌ ERRO ao baixar: $($_.Exception.Message)" -ForegroundColor Red
                $downloadSuccess = $false
                
                Write-Host ""
                Write-Host "      ⚠️  SOLUÇÃO ALTERNATIVA:" -ForegroundColor Yellow
                Write-Host "         1. Acesse: $($lang.Url)" -ForegroundColor Yellow
                Write-Host "         2. Baixe manualmente o arquivo" -ForegroundColor Yellow
                Write-Host "         3. Salve em: $filePath" -ForegroundColor Yellow
            }
            
            Write-Host ""
        }
    }
    
    if (-not $downloadSuccess) {
        Write-Host ""
        Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Red
        Write-Host "❌ FALHA NO DOWNLOAD DOS ARQUIVOS" -ForegroundColor Red
        Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Red
        Write-Host ""
        Write-Host "📝 PRÓXIMOS PASSOS MANUAIS:" -ForegroundColor Yellow
        Write-Host "   1. Baixe manualmente os arquivos de:" -ForegroundColor White
        Write-Host "      https://github.com/tesseract-ocr/tessdata" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "   2. Arquivos necessários:" -ForegroundColor White
        Write-Host "      - por.traineddata (Português)" -ForegroundColor White
        Write-Host "      - eng.traineddata (Inglês)" -ForegroundColor White
        Write-Host ""
        Write-Host "   3. Salve em: $tessdataDir" -ForegroundColor White
        Write-Host ""
        Write-Host "   4. Execute novamente este script" -ForegroundColor White
        Write-Host ""
        Write-Host "Pressione qualquer tecla para sair..." -ForegroundColor Gray
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        exit 1
    }
    
    $needsCompile = $true
} else {
    Write-Host "✅ Arquivos de idioma já existem no diretório fonte" -ForegroundColor Green
}

Write-Host ""

# Verificar se precisa compilar
$binFiles = Get-ChildItem $binTessdataDir -Filter "*.traineddata" -ErrorAction SilentlyContinue
if (-not $binFiles -or $binFiles.Count -eq 0) {
    $needsCompile = $true
}

if ($needsCompile) {
    Write-Host "🔧 AÇÃO 3: Recompilar projeto para copiar arquivos" -ForegroundColor Yellow
    Write-Host ""
    
    Push-Location $projectRoot
    
    try {
        Write-Host "   🔨 Limpando build anterior..." -ForegroundColor Cyan
        dotnet clean --nologo --verbosity quiet
        
        Write-Host "   🔨 Compilando projeto..." -ForegroundColor Cyan
        $buildOutput = dotnet build --nologo --verbosity minimal 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "   ✅ Build concluído com sucesso!" -ForegroundColor Green
        } else {
            Write-Host "   ⚠️  Build completou mas com erros:" -ForegroundColor Yellow
            Write-Host $buildOutput -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "   ❌ Erro durante build: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    Pop-Location
    Write-Host ""
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🔍 VALIDAÇÃO FINAL" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$allGood = $true

# Validar tessdata fonte
Write-Host "[1] Diretório tessdata fonte:" -ForegroundColor Yellow
if (Test-Path $tessdataDir) {
    Write-Host "   ✅ Existe: $tessdataDir" -ForegroundColor Green
    
    $porFile = Join-Path $tessdataDir "por.traineddata"
    $engFile = Join-Path $tessdataDir "eng.traineddata"
    
    if (Test-Path $porFile) {
        $size = [math]::Round((Get-Item $porFile).Length / 1MB, 2)
        Write-Host "   ✅ por.traineddata: $size MB" -ForegroundColor Green
    } else {
        Write-Host "   ❌ por.traineddata: NÃO ENCONTRADO" -ForegroundColor Red
        $allGood = $false
    }
    
    if (Test-Path $engFile) {
        $size = [math]::Round((Get-Item $engFile).Length / 1MB, 2)
        Write-Host "   ✅ eng.traineddata: $size MB" -ForegroundColor Green
    } else {
        Write-Host "   ❌ eng.traineddata: NÃO ENCONTRADO" -ForegroundColor Red
        $allGood = $false
    }
} else {
    Write-Host "   ❌ NÃO EXISTE" -ForegroundColor Red
    $allGood = $false
}

Write-Host ""

# Validar tessdata bin
Write-Host "[2] Diretório tessdata bin:" -ForegroundColor Yellow
if (Test-Path $binTessdataDir) {
    Write-Host "   ✅ Existe: $binTessdataDir" -ForegroundColor Green
    
    $porFile = Join-Path $binTessdataDir "por.traineddata"
    $engFile = Join-Path $binTessdataDir "eng.traineddata"
    
    if (Test-Path $porFile) {
        $size = [math]::Round((Get-Item $porFile).Length / 1MB, 2)
        Write-Host "   ✅ por.traineddata: $size MB" -ForegroundColor Green
    } else {
        Write-Host "   ❌ por.traineddata: NÃO ENCONTRADO" -ForegroundColor Red
        $allGood = $false
    }
    
    if (Test-Path $engFile) {
        $size = [math]::Round((Get-Item $engFile).Length / 1MB, 2)
        Write-Host "   ✅ eng.traineddata: $size MB" -ForegroundColor Green
    } else {
        Write-Host "   ❌ eng.traineddata: NÃO ENCONTRADO" -ForegroundColor Red
        $allGood = $false
    }
} else {
    Write-Host "   ❌ NÃO EXISTE" -ForegroundColor Red
    $allGood = $false
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

if ($allGood) {
    Write-Host "✅ DIAGNÓSTICO: TUDO OK!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📝 Próximos passos:" -ForegroundColor Yellow
    Write-Host "   1. Inicie a API: dotnet run --project LabelWise.Api" -ForegroundColor White
    Write-Host "   2. Acesse o Swagger: https://localhost:7001/swagger" -ForegroundColor White
    Write-Host "   3. Teste o endpoint /api/pipeline/analyze-image" -ForegroundColor White
    Write-Host ""
    Write-Host "   O erro 'Failed to initialise tesseract engine' deve estar RESOLVIDO!" -ForegroundColor Green
} else {
    Write-Host "❌ DIAGNÓSTICO: PROBLEMAS ENCONTRADOS" -ForegroundColor Red
    Write-Host ""
    Write-Host "📝 Ações necessárias:" -ForegroundColor Yellow
    Write-Host "   1. Baixe os arquivos manualmente:" -ForegroundColor White
    Write-Host "      - por.traineddata: https://github.com/tesseract-ocr/tessdata/raw/main/por.traineddata" -ForegroundColor Cyan
    Write-Host "      - eng.traineddata: https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "   2. Salve em: $tessdataDir" -ForegroundColor White
    Write-Host ""
    Write-Host "   3. Execute: dotnet clean; dotnet build" -ForegroundColor White
    Write-Host ""
    Write-Host "   4. Execute novamente este script para validar" -ForegroundColor White
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "Pressione qualquer tecla para sair..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
