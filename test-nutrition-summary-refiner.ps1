# Test Script: Nutrition Summary Refiner
# Tests refined summary, score labels, and technical text fixes

Write-Host "=== TESTE: REFINAMENTO DE APRESENTAÇÃO NUTRICIONAL ===" -ForegroundColor Cyan
Write-Host ""

# Test endpoint
$apiUrl = "http://localhost:5000"
$endpoint = "$apiUrl/api/nutrition/analyze"

# Test image (use any valid test image)
$testImage = "test-images\achocolatado-nescau.jpg"

if (-not (Test-Path $testImage)) {
    Write-Host "AVISO: Imagem de teste não encontrada: $testImage" -ForegroundColor Yellow
    Write-Host "Por favor, especifique um caminho válido para testar." -ForegroundColor Yellow
    Write-Host ""
    exit
}

Write-Host "🔍 Testando análise com refinamento de apresentação..." -ForegroundColor Green
Write-Host ""

# Create multipart form data
$boundary = [System.Guid]::NewGuid().ToString()
$fileBin = [System.IO.File]::ReadAllBytes($testImage)
$enc = [System.Text.Encoding]::GetEncoding("iso-8859-1")

$bodyLines = @(
    "--$boundary",
    "Content-Disposition: form-data; name=`"image`"; filename=`"$(Split-Path $testImage -Leaf)`"",
    "Content-Type: application/octet-stream",
    "",
    $enc.GetString($fileBin),
    "--$boundary",
    "Content-Disposition: form-data; name=`"userId`"",
    "",
    "1",
    "--$boundary--"
)

$body = $bodyLines -join "`r`n"

try {
    $response = Invoke-RestMethod -Uri $endpoint `
        -Method Post `
        -ContentType "multipart/form-data; boundary=$boundary" `
        -Body $body `
        -TimeoutSec 60

    Write-Host "✅ Análise concluída com sucesso!" -ForegroundColor Green
    Write-Host ""

    Write-Host "📊 ANÁLISE DE RESULTADOS REFINADOS:" -ForegroundColor Cyan
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
    Write-Host ""

    # 1. Summary refinado
    Write-Host "1️⃣ SUMMARY (Refinado):" -ForegroundColor Yellow
    Write-Host "   $($response.summary)" -ForegroundColor White
    Write-Host ""

    # Check for improvements
    if ($response.summary -notmatch "perfil intermediário" -and 
        $response.summary -notmatch "moderado" -and
        $response.summary -match "(elevado|alto|crítico|acima)") {
        Write-Host "   ✅ Summary mais direto e menos genérico" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  Summary pode estar usando termos genéricos" -ForegroundColor Yellow
    }
    Write-Host ""

    # 2. Score refinado
    Write-Host "2️⃣ NUTRITIONAL SCORE (Recalibrado):" -ForegroundColor Yellow
    Write-Host "   Valor: $($response.score.value)" -ForegroundColor White
    Write-Host "   Label: $($response.score.label)" -ForegroundColor White
    Write-Host "   Status: $($response.score.status)" -ForegroundColor White
    Write-Host "   Cor: $($response.score.color)" -ForegroundColor White
    Write-Host ""

    # Check for friendly labels
    $friendlyLabels = @(
        "Excelente escolha",
        "Boa escolha",
        "Consumo com atenção",
        "Consumo moderado",
        "Evitar consumo frequente",
        "Não recomendado"
    )

    if ($friendlyLabels -contains $response.score.label) {
        Write-Host "   ✅ Label amigável para app mobile" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  Label pode não estar amigável: $($response.score.label)" -ForegroundColor Yellow
    }

    # Check score calibration for sweet products
    $sugar = $response.estimatedNutritionProfile.estimatedSugarPer100g
    if ($sugar -gt 30 -and $response.score.value -gt 42) {
        Write-Host "   ⚠️  Score pode estar alto demais para açúcar elevado ($sugar g)" -ForegroundColor Yellow
    } elseif ($sugar -gt 20 -and $response.score.value -gt 48) {
        Write-Host "   ⚠️  Score pode estar otimista para açúcar elevado ($sugar g)" -ForegroundColor Yellow
    } else {
        Write-Host "   ✅ Score calibrado adequadamente para açúcar: $sugar g" -ForegroundColor Green
    }
    Write-Host ""

    # 3. Technical text fixes
    Write-Host "3️⃣ VERIFICAÇÃO DE TEXTOS TÉCNICOS:" -ForegroundColor Yellow
    
    $allText = "$($response.summary) $($response.estimatedNutritionProfile.basis)"
    
    if ($allText -match "não legível" -or $allText -match "não visível") {
        Write-Host "   ⚠️  Texto técnico encontrado: 'não legível' ou 'não visível'" -ForegroundColor Yellow
    } else {
        Write-Host "   ✅ Textos técnicos corrigidos" -ForegroundColor Green
    }

    if ($allText -match "Per100g" -or $allText -match "Estimated") {
        Write-Host "   ⚠️  Termos em inglês encontrados: Per100g, Estimated" -ForegroundColor Yellow
    } else {
        Write-Host "   ✅ Sem termos técnicos em inglês" -ForegroundColor Green
    }
    Write-Host ""

    # 4. Nutrition profile
    Write-Host "4️⃣ PERFIL NUTRICIONAL:" -ForegroundColor Yellow
    Write-Host "   Açúcar: $($response.estimatedNutritionProfile.estimatedSugarPer100g) g/100g" -ForegroundColor White
    Write-Host "   Sódio: $($response.estimatedNutritionProfile.estimatedSodiumPer100g) mg/100g" -ForegroundColor White
    Write-Host "   Proteína: $($response.estimatedNutritionProfile.estimatedProteinPer100g) g/100g" -ForegroundColor White
    Write-Host "   Calorias: $($response.estimatedNutritionProfile.caloriesPer100g) kcal/100g" -ForegroundColor White
    Write-Host ""

    # 5. Classification
    Write-Host "5️⃣ CLASSIFICAÇÕES:" -ForegroundColor Yellow
    Write-Host "   Diabético: $($response.classification.diabetic.status)" -ForegroundColor White
    Write-Host "   Pressão: $($response.classification.bloodPressure.status)" -ForegroundColor White
    Write-Host "   Emagrecimento: $($response.classification.weightLoss.status)" -ForegroundColor White
    Write-Host "   Ganho muscular: $($response.classification.muscleGain.status)" -ForegroundColor White
    Write-Host ""

    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "✅ TESTE CONCLUÍDO COM SUCESSO!" -ForegroundColor Green
    Write-Host ""

    # Show full JSON for inspection
    Write-Host "📋 Resposta completa (JSON):" -ForegroundColor Cyan
    $response | ConvertTo-Json -Depth 10 | Write-Host -ForegroundColor Gray

} catch {
    Write-Host "❌ Erro ao executar teste:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Stack trace:" -ForegroundColor Yellow
    Write-Host $_.Exception.StackTrace -ForegroundColor Gray
}

Write-Host ""
Write-Host "=== FIM DO TESTE ===" -ForegroundColor Cyan
