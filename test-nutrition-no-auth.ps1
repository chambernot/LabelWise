# test-nutrition-no-auth.ps1
# Script para testar o endpoint de análise nutricional SEM autenticação

Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host "  Teste: Endpoint de Análise Nutricional (SEM AUTENTICAÇÃO)" -ForegroundColor Cyan
Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host ""

$apiUrl = "http://localhost:5111"
$endpoint = "$apiUrl/api/nutrition/analyze-simple-image"

# Verificar se a API está rodando
Write-Host "🔍 Verificando se a API está rodando..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$apiUrl/health" -Method Get -TimeoutSec 5 -ErrorAction Stop
    Write-Host "✅ API está rodando!" -ForegroundColor Green
} catch {
    Write-Host "❌ API não está rodando. Inicie a API primeiro:" -ForegroundColor Red
    Write-Host "   dotnet run --project LabelWise.Api" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
Write-Host "📋 TESTE 1: Chamada SEM Token (Deve Funcionar)" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
Write-Host ""

# Solicitar caminho da imagem
Write-Host "📁 Digite o caminho da imagem de teste:" -ForegroundColor Yellow
Write-Host "   (Exemplo: C:\temp\product.jpg)" -ForegroundColor Gray
$imagePath = Read-Host "Caminho"

if (-not (Test-Path $imagePath)) {
    Write-Host "❌ Imagem não encontrada: $imagePath" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "📸 Imagem selecionada:" -ForegroundColor Cyan
Write-Host "   Arquivo: $(Split-Path -Leaf $imagePath)" -ForegroundColor White
Write-Host "   Tamanho: $((Get-Item $imagePath).Length / 1KB) KB" -ForegroundColor White
Write-Host ""

# Preparar formulário
$fileBytes = [System.IO.File]::ReadAllBytes($imagePath)
$fileName = [System.IO.Path]::GetFileName($imagePath)
$boundary = [System.Guid]::NewGuid().ToString()

$LF = "`r`n"
$bodyLines = (
    "--$boundary",
    "Content-Disposition: form-data; name=`"File`"; filename=`"$fileName`"",
    "Content-Type: image/jpeg",
    "",
    [System.Text.Encoding]::GetEncoding("iso-8859-1").GetString($fileBytes),
    "--$boundary",
    "Content-Disposition: form-data; name=`"LanguageCode`"",
    "",
    "pt",
    "--$boundary--"
) -join $LF

$headers = @{
    "Content-Type" = "multipart/form-data; boundary=$boundary"
    # ✅ SEM Authorization header!
}

Write-Host "⏳ Enviando requisição SEM token de autenticação..." -ForegroundColor Yellow
Write-Host "   Endpoint: $endpoint" -ForegroundColor Gray
Write-Host ""

try {
    $response = Invoke-RestMethod `
        -Uri $endpoint `
        -Method Post `
        -Headers $headers `
        -Body ([System.Text.Encoding]::GetEncoding("iso-8859-1").GetBytes($bodyLines)) `
        -TimeoutSec 120

    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Green
    Write-Host "✅ SUCESSO! Endpoint acessível sem autenticação!" -ForegroundColor Green
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Green
    Write-Host ""

    Write-Host "📊 RESULTADO DA ANÁLISE:" -ForegroundColor Cyan
    Write-Host "─────────────────────────────────────────────────────" -ForegroundColor DarkGray
    
    # Success
    if ($response.success) {
        Write-Host "✅ Success: TRUE" -ForegroundColor Green
    } else {
        Write-Host "❌ Success: FALSE" -ForegroundColor Red
    }

    # Product Name
    if ($response.productName) {
        Write-Host "📦 ProductName: $($response.productName)" -ForegroundColor Cyan
    } else {
        Write-Host "📦 ProductName: NULL" -ForegroundColor Yellow
    }

    # Category
    if ($response.category) {
        Write-Host "📂 Category: $($response.category)" -ForegroundColor Cyan
    }

    # Brand
    if ($response.brand) {
        Write-Host "🏷️ Brand: $($response.brand)" -ForegroundColor Cyan
    }

    # Score
    Write-Host ""
    if ($response.score) {
        $score = $response.score
        $scoreColor = if ($score.score -ge 70) { "Green" } elseif ($score.score -ge 40) { "Yellow" } else { "Red" }
        Write-Host "🎯 Score: $($score.score) - $($score.status)" -ForegroundColor $scoreColor
        Write-Host "   Label: $($score.label)" -ForegroundColor White
        Write-Host "   Color: $($score.color)" -ForegroundColor White
    }

    # Nutrition Profile
    if ($response.estimatedNutritionProfile) {
        Write-Host ""
        Write-Host "🥗 Perfil Nutricional Estimado:" -ForegroundColor Magenta
        $profile = $response.estimatedNutritionProfile
        if ($profile.caloriesPer100g) {
            Write-Host "   • Calorias/100g: $($profile.caloriesPer100g) kcal" -ForegroundColor White
        }
        if ($profile.estimatedSugarPer100g) {
            Write-Host "   • Açúcar/100g: $($profile.estimatedSugarPer100g)g" -ForegroundColor White
        }
        if ($profile.estimatedProteinPer100g) {
            Write-Host "   • Proteína/100g: $($profile.estimatedProteinPer100g)g" -ForegroundColor White
        }
    }

    # Analysis Mode
    if ($response.analysisMode) {
        Write-Host ""
        Write-Host "🔍 Analysis Mode: $($response.analysisMode)" -ForegroundColor Cyan
    }

    # Processing Time
    if ($response.processingTimeSeconds) {
        Write-Host "⏱️ Processing Time: $($response.processingTimeSeconds) seconds" -ForegroundColor Cyan
    }

    Write-Host ""
    Write-Host "─────────────────────────────────────────────────────" -ForegroundColor DarkGray

} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    
    if ($statusCode -eq 401) {
        Write-Host ""
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Red
        Write-Host "❌ FALHA! Endpoint ainda exige autenticação!" -ForegroundColor Red
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Red
        Write-Host ""
        Write-Host "Status Code: 401 Unauthorized" -ForegroundColor Red
        Write-Host ""
        Write-Host "Solução:" -ForegroundColor Yellow
        Write-Host "   1. Verifique se [AllowAnonymous] está no método" -ForegroundColor White
        Write-Host "   2. Recompile o projeto" -ForegroundColor White
        Write-Host "   3. Reinicie a API" -ForegroundColor White
    } else {
        Write-Host ""
        Write-Host "❌ Erro ao processar requisição:" -ForegroundColor Red
        Write-Host "   Status Code: $statusCode" -ForegroundColor Red
        Write-Host "   Message: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
Write-Host "📋 TESTE 2: Verificar Swagger (Opcional)" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
Write-Host ""
Write-Host "Para testar via Swagger:" -ForegroundColor Yellow
Write-Host "   1. Acesse: http://localhost:5111/swagger" -ForegroundColor Cyan
Write-Host "   2. Expanda: POST /api/nutrition/analyze-simple-image" -ForegroundColor Cyan
Write-Host "   3. ✅ NÃO clique no cadeado de autenticação" -ForegroundColor Green
Write-Host "   4. Clique em 'Try it out'" -ForegroundColor Cyan
Write-Host "   5. Faça upload de uma imagem" -ForegroundColor Cyan
Write-Host "   6. Clique em 'Execute'" -ForegroundColor Cyan
Write-Host "   7. ✅ Deve retornar status 200" -ForegroundColor Green
Write-Host ""

Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host "  Teste Concluído!" -ForegroundColor Cyan
Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host ""
