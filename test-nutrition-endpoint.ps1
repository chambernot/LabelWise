# Script de teste para o novo endpoint de Análise Nutricional Simplificada
# Uso: .\test-nutrition-endpoint.ps1

$baseUrl = "https://localhost:7223"

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🧪 TESTE DO ENDPOINT DE ANÁLISE NUTRICIONAL SIMPLIFICADA" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# ═══════════════════════════════════════════════════════════════════════
# ETAPA 1: Registrar usuário de teste (caso não exista)
# ═══════════════════════════════════════════════════════════════════════
Write-Host "📝 ETAPA 1: Registrando usuário de teste..." -ForegroundColor Yellow

$registerBody = @{
    email = "test.nutrition@example.com"
    password = "Test123!@#"
    name = "Test Nutrition User"
} | ConvertTo-Json

try {
    $registerResponse = Invoke-RestMethod `
        -Uri "$baseUrl/api/auth/register" `
        -Method Post `
        -Body $registerBody `
        -ContentType "application/json" `
        -SkipCertificateCheck

    Write-Host "✅ Usuário registrado com sucesso" -ForegroundColor Green
    Write-Host "   User ID: $($registerResponse.user.id)" -ForegroundColor Gray
} catch {
    if ($_.Exception.Response.StatusCode -eq 409) {
        Write-Host "ℹ️  Usuário já existe (OK)" -ForegroundColor Cyan
    } else {
        Write-Host "⚠️  Erro ao registrar: $_" -ForegroundColor Yellow
    }
}

Write-Host ""

# ═══════════════════════════════════════════════════════════════════════
# ETAPA 2: Fazer login e obter token JWT
# ═══════════════════════════════════════════════════════════════════════
Write-Host "🔐 ETAPA 2: Fazendo login..." -ForegroundColor Yellow

$loginBody = @{
    email = "test.nutrition@example.com"
    password = "Test123!@#"
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod `
        -Uri "$baseUrl/api/auth/login" `
        -Method Post `
        -Body $loginBody `
        -ContentType "application/json" `
        -SkipCertificateCheck

    $token = $loginResponse.token
    Write-Host "✅ Login realizado com sucesso" -ForegroundColor Green
    Write-Host "   Token obtido: $($token.Substring(0, 20))..." -ForegroundColor Gray
} catch {
    Write-Host "❌ Erro ao fazer login: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# ═══════════════════════════════════════════════════════════════════════
# ETAPA 3: Testar endpoint de análise nutricional simplificada
# ═══════════════════════════════════════════════════════════════════════
Write-Host "🍎 ETAPA 3: Testando análise nutricional simplificada..." -ForegroundColor Yellow

# Criar imagem de teste (1x1 pixel PNG)
$testImageBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="
$testImageBytes = [System.Convert]::FromBase64String($testImageBase64)
$testImagePath = Join-Path $env:TEMP "test-nutrition-image.png"
[System.IO.File]::WriteAllBytes($testImagePath, $testImageBytes)

Write-Host "   📁 Imagem de teste criada: $testImagePath" -ForegroundColor Gray

# Criar multipart form data
$boundary = [System.Guid]::NewGuid().ToString()
$LF = "`r`n"

$bodyLines = @(
    "--$boundary",
    "Content-Disposition: form-data; name=`"file`"; filename=`"test-product.png`"",
    "Content-Type: image/png",
    "",
    [System.Text.Encoding]::GetEncoding("iso-8859-1").GetString($testImageBytes),
    "--$boundary",
    "Content-Disposition: form-data; name=`"languageCode`"",
    "",
    "pt",
    "--$boundary--"
)

$body = $bodyLines -join $LF

$headers = @{
    Authorization = "Bearer $token"
    "Content-Type" = "multipart/form-data; boundary=$boundary"
}

try {
    Write-Host "   🚀 Enviando requisição..." -ForegroundColor Gray
    
    $nutritionResponse = Invoke-RestMethod `
        -Uri "$baseUrl/api/nutrition/analyze-simple-image" `
        -Method Post `
        -Headers $headers `
        -Body $body `
        -SkipCertificateCheck

    Write-Host "✅ Análise concluída com sucesso!" -ForegroundColor Green
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "📊 RESULTADO DA ANÁLISE NUTRICIONAL" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "🔸 Success: $($nutritionResponse.success)" -ForegroundColor White
    Write-Host "🔸 Product Name: $($nutritionResponse.productName ?? 'N/A')" -ForegroundColor White
    Write-Host "🔸 Brand: $($nutritionResponse.brand ?? 'N/A')" -ForegroundColor White
    Write-Host "🔸 Category: $($nutritionResponse.category ?? 'N/A')" -ForegroundColor White
    Write-Host "🔸 Package Weight: $($nutritionResponse.packageWeight ?? 'N/A')" -ForegroundColor White
    Write-Host ""
    
    if ($nutritionResponse.estimatedNutrition) {
        Write-Host "📈 Estimated Nutrition:" -ForegroundColor Cyan
        Write-Host "   Calories per 100g: $($nutritionResponse.estimatedNutrition.caloriesPer100g)" -ForegroundColor Gray
        if ($nutritionResponse.estimatedNutrition.estimatedPackageCalories) {
            Write-Host "   Package Calories: $($nutritionResponse.estimatedNutrition.estimatedPackageCalories)" -ForegroundColor Gray
        }
    }
    
    Write-Host ""
    
    if ($nutritionResponse.classification) {
        Write-Host "🏷️  Classification:" -ForegroundColor Cyan
        Write-Host "   Diabetic: $($nutritionResponse.classification.diabetic.status)" -ForegroundColor Gray
        Write-Host "     └─ $($nutritionResponse.classification.diabetic.reason)" -ForegroundColor DarkGray
        Write-Host "   Blood Pressure: $($nutritionResponse.classification.bloodPressure.status)" -ForegroundColor Gray
        Write-Host "     └─ $($nutritionResponse.classification.bloodPressure.reason)" -ForegroundColor DarkGray
        Write-Host "   Weight Loss: $($nutritionResponse.classification.weightLoss.status)" -ForegroundColor Gray
        Write-Host "     └─ $($nutritionResponse.classification.weightLoss.reason)" -ForegroundColor DarkGray
        Write-Host "   Muscle Gain: $($nutritionResponse.classification.muscleGain.status)" -ForegroundColor Gray
        Write-Host "     └─ $($nutritionResponse.classification.muscleGain.reason)" -ForegroundColor DarkGray
    }
    
    Write-Host ""
    Write-Host "📝 Summary: $($nutritionResponse.summary ?? 'N/A')" -ForegroundColor White
    Write-Host "🎯 Confidence: $([math]::Round($nutritionResponse.confidence * 100, 2))%" -ForegroundColor White
    Write-Host "⏱️  Processing Time: $([math]::Round($nutritionResponse.processingTimeSeconds, 2))s" -ForegroundColor White
    
    if ($nutritionResponse.warnings -and $nutritionResponse.warnings.Count -gt 0) {
        Write-Host ""
        Write-Host "⚠️  Warnings:" -ForegroundColor Yellow
        foreach ($warning in $nutritionResponse.warnings) {
            Write-Host "   - $warning" -ForegroundColor DarkYellow
        }
    }
    
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    
    # Salvar resposta completa em arquivo JSON
    $responseFile = "nutrition-analysis-response.json"
    $nutritionResponse | ConvertTo-Json -Depth 10 | Out-File $responseFile
    Write-Host ""
    Write-Host "💾 Resposta completa salva em: $responseFile" -ForegroundColor Green

} catch {
    Write-Host "❌ Erro ao executar análise nutricional: $_" -ForegroundColor Red
    Write-Host "   Status Code: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Yellow
    Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Yellow
}

# Limpar arquivo temporário
if (Test-Path $testImagePath) {
    Remove-Item $testImagePath -Force
}

Write-Host ""
Write-Host "✅ Teste concluído!" -ForegroundColor Green
