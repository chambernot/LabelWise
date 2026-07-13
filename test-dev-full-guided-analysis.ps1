# ═══════════════════════════════════════════════════════════════════════════
# TESTE DO ENDPOINT DE DESENVOLVIMENTO - FULL GUIDED ANALYSIS
# ═══════════════════════════════════════════════════════════════════════════
# Este script demonstra como usar o endpoint /api/dev/full-guided-analysis-test
# para testar o fluxo completo de captura guiada em uma única chamada.
#
# Uso:
#   .\test-dev-full-guided-analysis.ps1
#
# Requisitos:
#   - API rodando em modo Development
#   - Token JWT válido (ou credenciais para login)
#   - Imagens de teste preparadas
# ═══════════════════════════════════════════════════════════════════════════

param(
    [string]$ApiBaseUrl = "https://localhost:7319",
    [string]$Username = "test@example.com",
    [string]$Password = "Test@123",
    [string]$ImagesPath = "C:\temp\test-images",
    [switch]$SkipCertificateCheck
)

Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🧪 TESTE DO ENDPOINT DE DESENVOLVIMENTO - FULL GUIDED ANALYSIS" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Skip certificate validation if needed (for localhost testing)
if ($SkipCertificateCheck) {
    Write-Host "⚠️  Desabilitando validação de certificado SSL..." -ForegroundColor Yellow
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
    # For PowerShell 7+
    if ($PSVersionTable.PSVersion.Major -ge 7) {
        Write-Host "   PowerShell 7+ detectado - usando -SkipCertificateCheck" -ForegroundColor Yellow
    }
}

# ═══════════════════════════════════════════════════════════════════════════
# ETAPA 1: VERIFICAR HEALTH DO ENDPOINT
# ═══════════════════════════════════════════════════════════════════════════
Write-Host "📋 ETAPA 1: Verificando health do endpoint..." -ForegroundColor Green
Write-Host ""

try {
    $healthUrl = "$ApiBaseUrl/api/dev/full-guided-analysis-test/health"
    Write-Host "   URL: $healthUrl" -ForegroundColor Gray
    
    if ($PSVersionTable.PSVersion.Major -ge 7 -and $SkipCertificateCheck) {
        $healthResponse = Invoke-RestMethod -Uri $healthUrl -Method Get -SkipCertificateCheck
    } else {
        $healthResponse = Invoke-RestMethod -Uri $healthUrl -Method Get
    }
    
    Write-Host "   ✅ Endpoint disponível!" -ForegroundColor Green
    Write-Host "   Status: $($healthResponse.status)" -ForegroundColor Gray
    Write-Host "   Ambiente: $($healthResponse.environment)" -ForegroundColor Gray
    Write-Host "   Tipos aceitos: $($healthResponse.acceptedImageTypes -join ', ')" -ForegroundColor Gray
    Write-Host "   Tamanho máximo: $($healthResponse.maxFileSizeMB)MB" -ForegroundColor Gray
    Write-Host ""
} catch {
    Write-Host "   ❌ Erro ao verificar health do endpoint" -ForegroundColor Red
    Write-Host "   Erro: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "   💡 Certifique-se de que:" -ForegroundColor Yellow
    Write-Host "      - A API está rodando em $ApiBaseUrl" -ForegroundColor Yellow
    Write-Host "      - O ambiente é Development" -ForegroundColor Yellow
    Write-Host "      - Use -SkipCertificateCheck se estiver testando localhost com HTTPS" -ForegroundColor Yellow
    exit 1
}

# ═══════════════════════════════════════════════════════════════════════════
# ETAPA 2: FAZER LOGIN E OBTER TOKEN JWT
# ═══════════════════════════════════════════════════════════════════════════
Write-Host "📋 ETAPA 2: Fazendo login para obter token JWT..." -ForegroundColor Green
Write-Host ""

try {
    $loginUrl = "$ApiBaseUrl/api/auth/login"
    $loginBody = @{
        email = $Username
        password = $Password
    } | ConvertTo-Json
    
    Write-Host "   URL: $loginUrl" -ForegroundColor Gray
    Write-Host "   Email: $Username" -ForegroundColor Gray
    
    $loginParams = @{
        Uri = $loginUrl
        Method = "Post"
        Body = $loginBody
        ContentType = "application/json"
    }
    
    if ($PSVersionTable.PSVersion.Major -ge 7 -and $SkipCertificateCheck) {
        $loginParams.SkipCertificateCheck = $true
    }
    
    $loginResponse = Invoke-RestMethod @loginParams
    $token = $loginResponse.token
    
    Write-Host "   ✅ Login realizado com sucesso!" -ForegroundColor Green
    Write-Host "   Token: $($token.Substring(0, 20))..." -ForegroundColor Gray
    Write-Host ""
} catch {
    Write-Host "   ❌ Erro ao fazer login" -ForegroundColor Red
    Write-Host "   Erro: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "   💡 Certifique-se de que as credenciais estão corretas" -ForegroundColor Yellow
    Write-Host "      Username: $Username" -ForegroundColor Yellow
    Write-Host "      Password: [hidden]" -ForegroundColor Yellow
    exit 1
}

# ═══════════════════════════════════════════════════════════════════════════
# ETAPA 3: PREPARAR IMAGENS DE TESTE
# ═══════════════════════════════════════════════════════════════════════════
Write-Host "📋 ETAPA 3: Preparando imagens de teste..." -ForegroundColor Green
Write-Host ""

# Verificar se o diretório de imagens existe
if (-not (Test-Path $ImagesPath)) {
    Write-Host "   ⚠️  Diretório de imagens não encontrado: $ImagesPath" -ForegroundColor Yellow
    Write-Host "   📁 Criando diretório..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $ImagesPath -Force | Out-Null
    Write-Host ""
    Write-Host "   💡 Por favor, adicione imagens de teste ao diretório:" -ForegroundColor Yellow
    Write-Host "      - front.jpg (embalagem frontal)" -ForegroundColor Yellow
    Write-Host "      - ingredients.jpg (lista de ingredientes)" -ForegroundColor Yellow
    Write-Host "      - nutrition.jpg (tabela nutricional)" -ForegroundColor Yellow
    Write-Host "      - allergen.jpg (declaração de alérgenos)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "   Ou use imagens com outros nomes e ajuste o script." -ForegroundColor Yellow
    exit 1
}

# Definir caminhos das imagens
$frontImagePath = Join-Path $ImagesPath "front.jpg"
$ingredientsImagePath = Join-Path $ImagesPath "ingredients.jpg"
$nutritionImagePath = Join-Path $ImagesPath "nutrition.jpg"
$allergenImagePath = Join-Path $ImagesPath "allergen.jpg"

# Verificar quais imagens estão disponíveis
$availableImages = @()

if (Test-Path $frontImagePath) {
    $availableImages += "Front"
    Write-Host "   ✅ Front image: $frontImagePath" -ForegroundColor Green
} else {
    Write-Host "   ⚠️  Front image não encontrada (opcional)" -ForegroundColor Yellow
}

if (Test-Path $ingredientsImagePath) {
    $availableImages += "Ingredients"
    Write-Host "   ✅ Ingredients image: $ingredientsImagePath" -ForegroundColor Green
} else {
    Write-Host "   ⚠️  Ingredients image não encontrada (recomendado)" -ForegroundColor Yellow
}

if (Test-Path $nutritionImagePath) {
    $availableImages += "Nutrition"
    Write-Host "   ✅ Nutrition image: $nutritionImagePath" -ForegroundColor Green
} else {
    Write-Host "   ⚠️  Nutrition image não encontrada (recomendado)" -ForegroundColor Yellow
}

if (Test-Path $allergenImagePath) {
    $availableImages += "Allergen"
    Write-Host "   ✅ Allergen image: $allergenImagePath" -ForegroundColor Green
} else {
    Write-Host "   ⚠️  Allergen image não encontrada (opcional)" -ForegroundColor Yellow
}

if ($availableImages.Count -eq 0) {
    Write-Host ""
    Write-Host "   ❌ Nenhuma imagem encontrada!" -ForegroundColor Red
    Write-Host "   💡 Adicione pelo menos uma imagem ao diretório: $ImagesPath" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "   📊 Imagens disponíveis: $($availableImages.Count)" -ForegroundColor Cyan
Write-Host ""

# ═══════════════════════════════════════════════════════════════════════════
# ETAPA 4: ENVIAR REQUEST PARA O ENDPOINT
# ═══════════════════════════════════════════════════════════════════════════
Write-Host "📋 ETAPA 4: Enviando request para análise completa..." -ForegroundColor Green
Write-Host ""

try {
    $analysisUrl = "$ApiBaseUrl/api/dev/full-guided-analysis-test"
    Write-Host "   URL: $analysisUrl" -ForegroundColor Gray
    
    # Criar multipart form data
    $form = @{
        languageCode = "pt-BR"
        deviceInfo = "PowerShell Test Script"
    }
    
    # Adicionar barcode opcional (exemplo)
    # $form.barcode = "7891234567890"
    
    # Adicionar imagens disponíveis
    if (Test-Path $frontImagePath) {
        $form.frontImage = Get-Item -Path $frontImagePath
    }
    
    if (Test-Path $ingredientsImagePath) {
        $form.ingredientsImage = Get-Item -Path $ingredientsImagePath
    }
    
    if (Test-Path $nutritionImagePath) {
        $form.nutritionImage = Get-Item -Path $nutritionImagePath
    }
    
    if (Test-Path $allergenImagePath) {
        $form.allergenImage = Get-Item -Path $allergenImagePath
    }
    
    Write-Host "   🚀 Enviando request..." -ForegroundColor Cyan
    $startTime = Get-Date
    
    $headers = @{
        Authorization = "Bearer $token"
    }
    
    $requestParams = @{
        Uri = $analysisUrl
        Method = "Post"
        Headers = $headers
        Form = $form
    }
    
    if ($PSVersionTable.PSVersion.Major -ge 7 -and $SkipCertificateCheck) {
        $requestParams.SkipCertificateCheck = $true
    }
    
    $response = Invoke-RestMethod @requestParams
    
    $endTime = Get-Date
    $duration = $endTime - $startTime
    
    Write-Host ""
    Write-Host "   ✅ Request concluído!" -ForegroundColor Green
    Write-Host "   ⏱️  Duração: $($duration.TotalSeconds)s" -ForegroundColor Gray
    Write-Host ""
    
} catch {
    Write-Host ""
    Write-Host "   ❌ Erro ao enviar request" -ForegroundColor Red
    Write-Host "   Erro: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.ErrorDetails.Message) {
        Write-Host "   Detalhes: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
    
    exit 1
}

# ═══════════════════════════════════════════════════════════════════════════
# ETAPA 5: EXIBIR RESULTADOS
# ═══════════════════════════════════════════════════════════════════════════
Write-Host "📋 ETAPA 5: Resultados da análise" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

Write-Host "🆔 SESSION INFO" -ForegroundColor Cyan
Write-Host "   Session ID: $($response.sessionId)" -ForegroundColor Gray
Write-Host "   Processed At: $($response.processedAt)" -ForegroundColor Gray
Write-Host "   Total Duration: $($response.totalDuration)" -ForegroundColor Gray
Write-Host "   Success: $($response.success)" -ForegroundColor $(if ($response.success) { "Green" } else { "Red" })
Write-Host ""

if ($response.productIdentification) {
    Write-Host "🏷️  PRODUCT IDENTIFICATION" -ForegroundColor Cyan
    Write-Host "   Product Name: $($response.productIdentification.productName ?? 'N/A')" -ForegroundColor Gray
    Write-Host "   Brand: $($response.productIdentification.brand ?? 'N/A')" -ForegroundColor Gray
    Write-Host "   Barcode: $($response.productIdentification.barcode ?? 'N/A')" -ForegroundColor Gray
    Write-Host "   Confidence: $([math]::Round($response.productIdentification.confidence * 100, 2))%" -ForegroundColor Gray
    Write-Host ""
}

if ($response.ingredients) {
    Write-Host "🥗 INGREDIENTS" -ForegroundColor Cyan
    Write-Host "   Total Count: $($response.ingredients.totalCount)" -ForegroundColor Gray
    Write-Host "   Parse Confidence: $([math]::Round($response.ingredients.parseConfidence * 100, 2))%" -ForegroundColor Gray
    if ($response.ingredients.detectedIngredients.Count -gt 0) {
        Write-Host "   Detected: $($response.ingredients.detectedIngredients -join ', ')" -ForegroundColor Gray
    }
    Write-Host ""
}

if ($response.allergens) {
    Write-Host "⚠️  ALLERGENS" -ForegroundColor Cyan
    if ($response.allergens.declaredAllergens.Count -gt 0) {
        Write-Host "   Declared: $($response.allergens.declaredAllergens -join ', ')" -ForegroundColor Yellow
    } else {
        Write-Host "   Declared: None" -ForegroundColor Gray
    }
    if ($response.allergens.mayContainAllergens.Count -gt 0) {
        Write-Host "   May Contain: $($response.allergens.mayContainAllergens -join ', ')" -ForegroundColor Yellow
    }
    Write-Host ""
}

if ($response.nutritionalFacts) {
    Write-Host "📊 NUTRITIONAL FACTS" -ForegroundColor Cyan
    Write-Host "   Serving Size: $($response.nutritionalFacts.servingSize ?? 'N/A')" -ForegroundColor Gray
    Write-Host "   Calories: $($response.nutritionalFacts.calories ?? 'N/A') kcal" -ForegroundColor Gray
    Write-Host "   Nutrients Detected: $($response.nutritionalFacts.nutrientsDetected)" -ForegroundColor Gray
    Write-Host ""
}

if ($response.finalAnalysis) {
    Write-Host "🎯 FINAL ANALYSIS" -ForegroundColor Cyan
    Write-Host "   Classification: $($response.finalAnalysis.classification)" -ForegroundColor $(
        switch ($response.finalAnalysis.classification) {
            "Excellent" { "Green" }
            "Good" { "Green" }
            "NeedsModeration" { "Yellow" }
            "Avoid" { "Red" }
            default { "Gray" }
        }
    )
    Write-Host "   Overall Score: $([math]::Round($response.finalAnalysis.overallScore, 2))/5.0" -ForegroundColor Gray
    
    if ($response.finalAnalysis.alerts.Count -gt 0) {
        Write-Host "   Alerts:" -ForegroundColor Yellow
        foreach ($alert in $response.finalAnalysis.alerts) {
            Write-Host "      - $alert" -ForegroundColor Yellow
        }
    }
    
    if ($response.finalAnalysis.recommendations.Count -gt 0) {
        Write-Host "   Recommendations:" -ForegroundColor Cyan
        foreach ($rec in $response.finalAnalysis.recommendations) {
            Write-Host "      - $rec" -ForegroundColor Cyan
        }
    }
    Write-Host ""
}

Write-Host "📈 PROCESSED STEPS" -ForegroundColor Cyan
foreach ($step in $response.processedSteps) {
    $statusIcon = if ($step.success) { "✅" } else { "❌" }
    $statusColor = if ($step.success) { "Green" } else { "Red" }
    
    Write-Host "   $statusIcon $($step.stepName)" -ForegroundColor $statusColor
    Write-Host "      Duration: $($step.duration)" -ForegroundColor Gray
    Write-Host "      File Size: $([math]::Round($step.fileSizeBytes / 1024, 2)) KB" -ForegroundColor Gray
    
    if ($step.ocrResult) {
        Write-Host "      OCR Confidence: $([math]::Round($step.ocrResult.confidence * 100, 2))%" -ForegroundColor Gray
        Write-Host "      Text Length: $($step.ocrResult.textLength)" -ForegroundColor Gray
    }
    
    if ($step.parsingResult) {
        Write-Host "      Items Extracted: $($step.parsingResult.itemsExtracted)" -ForegroundColor Gray
    }
}
Write-Host ""

if ($response.confidenceDetails) {
    Write-Host "🎓 CONFIDENCE DETAILS" -ForegroundColor Cyan
    Write-Host "   Overall: $([math]::Round($response.confidenceDetails.overall * 100, 2))%" -ForegroundColor Gray
    foreach ($dim in $response.confidenceDetails.dimensions.GetEnumerator()) {
        Write-Host "   $($dim.Key): $([math]::Round($dim.Value * 100, 2))%" -ForegroundColor Gray
    }
    Write-Host ""
}

if ($response.warnings.Count -gt 0) {
    Write-Host "⚠️  WARNINGS" -ForegroundColor Yellow
    foreach ($warning in $response.warnings) {
        Write-Host "   - $warning" -ForegroundColor Yellow
    }
    Write-Host ""
}

if ($response.errors.Count -gt 0) {
    Write-Host "❌ ERRORS" -ForegroundColor Red
    foreach ($error in $response.errors) {
        Write-Host "   - $error" -ForegroundColor Red
    }
    Write-Host ""
}

if ($response.missingRequiredSteps.Count -gt 0) {
    Write-Host "📝 MISSING REQUIRED STEPS" -ForegroundColor Yellow
    foreach ($missing in $response.missingRequiredSteps) {
        Write-Host "   - $missing" -ForegroundColor Yellow
    }
    Write-Host ""
}

# ═══════════════════════════════════════════════════════════════════════════
# ETAPA 6: SALVAR RESULTADO EM JSON
# ═══════════════════════════════════════════════════════════════════════════
Write-Host "📋 ETAPA 6: Salvando resultado completo em JSON..." -ForegroundColor Green
Write-Host ""

$outputFile = Join-Path $PSScriptRoot "dev-full-guided-analysis-result.json"
$response | ConvertTo-Json -Depth 10 | Out-File -FilePath $outputFile -Encoding UTF8

Write-Host "   ✅ Resultado salvo em: $outputFile" -ForegroundColor Green
Write-Host ""

Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "✅ TESTE CONCLUÍDO!" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
