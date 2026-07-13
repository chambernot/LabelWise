# ========================================
# Script de Teste do OCR com Imagem Real
# LabelWise - Tesseract OCR
# ========================================

param(
    [string]$ImagePath = "",
    [string]$ApiUrl = "http://localhost:5000"
)

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   TESTE OCR COM IMAGEM REAL - LabelWise                       ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# ========================================
# Verificar se a API está rodando
# ========================================

Write-Host "[1/4] Verificando se a API está rodando..." -ForegroundColor Yellow

try {
    $healthCheck = Invoke-WebRequest -Uri "$ApiUrl/swagger/index.html" -Method GET -UseBasicParsing -TimeoutSec 5 -ErrorAction SilentlyContinue
    Write-Host "      ✓ API está respondendo em $ApiUrl" -ForegroundColor Green
} catch {
    Write-Host "      ✗ API não está respondendo em $ApiUrl" -ForegroundColor Red
    Write-Host "      → Execute a API primeiro com: .\run-api.ps1" -ForegroundColor Yellow
    Write-Host ""
    exit
}

Write-Host ""

# ========================================
# Verificar Imagem
# ========================================

Write-Host "[2/4] Verificando imagem de teste..." -ForegroundColor Yellow

if ($ImagePath -eq "") {
    Write-Host "      ⚠ Nenhuma imagem fornecida" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Uso:" -ForegroundColor Cyan
    Write-Host "  .\test-ocr-image.ps1 -ImagePath 'C:\caminho\para\imagem.jpg'" -ForegroundColor White
    Write-Host ""
    
    # Tentar encontrar imagens de teste
    $testImages = Get-ChildItem -Path . -Include *.jpg,*.jpeg,*.png,*.bmp -Recurse -ErrorAction SilentlyContinue | Select-Object -First 5
    
    if ($testImages) {
        Write-Host "Imagens encontradas no diretório:" -ForegroundColor Cyan
        $i = 1
        foreach ($img in $testImages) {
            Write-Host "  [$i] $($img.FullName)" -ForegroundColor White
            $i++
        }
        Write-Host ""
        $choice = Read-Host "Digite o número da imagem para testar (ou ENTER para sair)"
        
        if ($choice -match '^\d+$' -and [int]$choice -le $testImages.Count -and [int]$choice -gt 0) {
            $ImagePath = $testImages[[int]$choice - 1].FullName
        } else {
            Write-Host "      → Saindo..." -ForegroundColor Gray
            exit
        }
    } else {
        Write-Host "      ✗ Nenhuma imagem encontrada" -ForegroundColor Red
        Write-Host "      → Forneça o caminho de uma imagem de rótulo" -ForegroundColor Yellow
        exit
    }
}

if (-Not (Test-Path $ImagePath)) {
    Write-Host "      ✗ Imagem não encontrada: $ImagePath" -ForegroundColor Red
    exit
}

$imageFile = Get-Item $ImagePath
$imageSizeMB = $imageFile.Length / 1MB

Write-Host "      ✓ Imagem encontrada: $($imageFile.Name)" -ForegroundColor Green
Write-Host "        Tamanho: $([math]::Round($imageSizeMB, 2)) MB" -ForegroundColor Gray

Write-Host ""

# ========================================
# Enviar Imagem para API
# ========================================

Write-Host "[3/4] Enviando imagem para API (OCR + Análise)..." -ForegroundColor Yellow
Write-Host "      Endpoint: POST $ApiUrl/api/analysis/pipeline" -ForegroundColor Gray
Write-Host "      Aguarde... isso pode levar alguns segundos" -ForegroundColor Gray
Write-Host ""

$endpoint = "$ApiUrl/api/analysis/pipeline"

try {
    # Criar formulário multipart
    $form = @{
        image = Get-Item -Path $ImagePath
    }
    
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $response = Invoke-RestMethod -Uri $endpoint -Method Post -Form $form -ContentType "multipart/form-data"
    $stopwatch.Stop()
    
    Write-Host "      ✓ Resposta recebida em $($stopwatch.Elapsed.TotalSeconds.ToString('F2'))s" -ForegroundColor Green
    
} catch {
    Write-Host "      ✗ ERRO ao enviar imagem: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host ""
        Write-Host "Detalhes do erro:" -ForegroundColor Yellow
        Write-Host $responseBody -ForegroundColor Gray
    }
    
    exit
}

Write-Host ""

# ========================================
# Exibir Resultado
# ========================================

Write-Host "[4/4] Resultado da Análise OCR" -ForegroundColor Yellow
Write-Host ""

# Metadados do Pipeline
if ($response.metadata) {
    Write-Host "═══ METADADOS DO PIPELINE ═══" -ForegroundColor Cyan
    
    if ($response.metadata.ocrStep) {
        $ocrStep = $response.metadata.ocrStep
        Write-Host "  OCR Provider: $($ocrStep.additionalData.providerName)" -ForegroundColor White
        Write-Host "  Duração OCR: $([math]::Round($ocrStep.durationMs, 2)) ms" -ForegroundColor White
        Write-Host "  Confiança: $([math]::Round($ocrStep.additionalData.confidence * 100, 2))%" -ForegroundColor White
        Write-Host "  Texto extraído: $($ocrStep.additionalData.textLength) caracteres" -ForegroundColor White
        Write-Host "  Blocos de texto: $($ocrStep.additionalData.blocksCount)" -ForegroundColor White
        Write-Host ""
    }
    
    Write-Host "  Duração Total: $([math]::Round($response.metadata.totalDurationMs, 2)) ms" -ForegroundColor White
    Write-Host ""
}

# Resultado da Análise
if ($response.analysisResult) {
    $result = $response.analysisResult
    
    Write-Host "═══ RESULTADO DA ANÁLISE ═══" -ForegroundColor Cyan
    Write-Host "  Produto: $($result.productName)" -ForegroundColor White
    
    if ($result.brand) {
        Write-Host "  Marca: $($result.brand)" -ForegroundColor White
    }
    
    Write-Host "  Score Geral: $([math]::Round($result.generalScore * 100, 1))%" -ForegroundColor White
    Write-Host "  Classificação: $($result.classification)" -ForegroundColor White
    Write-Host ""
    
    # Texto OCR Extraído
    if ($result.extractedText) {
        Write-Host "═══ TEXTO OCR BRUTO ═══" -ForegroundColor Cyan
        Write-Host "────────────────────────────────────────────────────────────────" -ForegroundColor DarkGray
        Write-Host $result.extractedText -ForegroundColor Gray
        Write-Host "────────────────────────────────────────────────────────────────" -ForegroundColor DarkGray
        Write-Host ""
    }
    
    # Ingredientes Extraídos
    if ($result.extractedIngredients -and $result.extractedIngredients.Count -gt 0) {
        Write-Host "═══ INGREDIENTES DETECTADOS ═══" -ForegroundColor Cyan
        foreach ($ingredient in $result.extractedIngredients) {
            Write-Host "  • $ingredient" -ForegroundColor White
        }
        Write-Host ""
    }
    
    # Alérgenos Extraídos
    if ($result.extractedAllergens -and $result.extractedAllergens.Count -gt 0) {
        Write-Host "═══ ALÉRGENOS DETECTADOS ═══" -ForegroundColor Cyan
        foreach ($allergen in $result.extractedAllergens) {
            Write-Host "  ⚠ $allergen" -ForegroundColor Yellow
        }
        Write-Host ""
    }
    
    # Alertas
    if ($result.alerts -and $result.alerts.Count -gt 0) {
        Write-Host "═══ ALERTAS ═══" -ForegroundColor Cyan
        foreach ($alert in $result.alerts) {
            Write-Host "  ⚠ $alert" -ForegroundColor Yellow
        }
        Write-Host ""
    }
    
    # Recomendações
    if ($result.recommendations -and $result.recommendations.Count -gt 0) {
        Write-Host "═══ RECOMENDAÇÕES ═══" -ForegroundColor Cyan
        foreach ($rec in $result.recommendations) {
            Write-Host "  → $rec" -ForegroundColor White
        }
        Write-Host ""
    }
}

# ========================================
# Salvar Resultado Completo
# ========================================

$outputFile = "ocr-result-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
$response | ConvertTo-Json -Depth 10 | Out-File $outputFile -Encoding UTF8

Write-Host "═══ RESULTADO COMPLETO SALVO ═══" -ForegroundColor Cyan
Write-Host "  Arquivo: $outputFile" -ForegroundColor White
Write-Host ""

Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║   ✓ TESTE CONCLUÍDO COM SUCESSO!                              ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
