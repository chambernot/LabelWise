# test-refactored-nutrition-analysis.ps1
# Script para testar o endpoint refatorado de análise nutricional

$baseUrl = "https://localhost:7001"
$imagePath = "C:\path\to\product_image.jpg"  # ALTERE PARA O CAMINHO DA SUA IMAGEM

Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🧪 TESTE - ANÁLISE NUTRICIONAL REFATORADA" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan

# ═══════════════════════════════════════════════════════════
# ETAPA 1: VERIFICAR SE A IMAGEM EXISTE
# ═══════════════════════════════════════════════════════════

if (-not (Test-Path $imagePath)) {
    Write-Host "`n❌ ERRO: Imagem não encontrada em: $imagePath" -ForegroundColor Red
    Write-Host "`n💡 DICA: Altere a variável `$imagePath no script para apontar para uma imagem real`n" -ForegroundColor Yellow
    exit 1
}

Write-Host "`n✅ Imagem encontrada: $imagePath" -ForegroundColor Green

# ═══════════════════════════════════════════════════════════
# ETAPA 2: LOGIN
# ═══════════════════════════════════════════════════════════

Write-Host "`n🔐 Fazendo login..." -ForegroundColor Cyan

try {
    $loginBody = @{
        email = "test@example.com"
        password = "Test@123"
    } | ConvertTo-Json

    $loginResponse = Invoke-RestMethod `
        -Uri "$baseUrl/api/Auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body $loginBody `
        -SkipCertificateCheck

    $token = $loginResponse.token
    Write-Host "✅ Login realizado com sucesso!" -ForegroundColor Green
    Write-Host "   Token: $($token.Substring(0, 30))..." -ForegroundColor Gray
}
catch {
    Write-Host "`n❌ ERRO no login: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "`n💡 Certifique-se de que:" -ForegroundColor Yellow
    Write-Host "   1. A API está rodando em $baseUrl" -ForegroundColor Yellow
    Write-Host "   2. O usuário test@example.com existe com senha Test@123`n" -ForegroundColor Yellow
    exit 1
}

# ═══════════════════════════════════════════════════════════
# ETAPA 3: ANÁLISE REFATORADA
# ═══════════════════════════════════════════════════════════

Write-Host "`n📸 Enviando imagem para análise refatorada..." -ForegroundColor Cyan

try {
    $headers = @{
        "Authorization" = "Bearer $token"
    }

    $form = @{
        image = Get-Item -Path $imagePath
        languageCode = "pt"
    }

    $response = Invoke-RestMethod `
        -Uri "$baseUrl/api/RefactoredNutrition/analyze" `
        -Method Post `
        -Headers $headers `
        -Form $form `
        -SkipCertificateCheck

    # ═══════════════════════════════════════════════════════════
    # EXIBIR RESULTADO
    # ═══════════════════════════════════════════════════════════

    Write-Host "`n═══════════════════════════════════════════════════════════" -ForegroundColor Yellow
    Write-Host "📊 RESULTADO DA ANÁLISE REFATORADA" -ForegroundColor Yellow
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Yellow

    if ($response.success) {
        Write-Host "`n✅ ANÁLISE BEM-SUCEDIDA`n" -ForegroundColor Green
    } else {
        Write-Host "`n❌ ANÁLISE FALHOU" -ForegroundColor Red
        Write-Host "   Erro: $($response.errorMessage)`n" -ForegroundColor Red
        exit 1
    }

    # Identificação do Produto
    Write-Host "🏷️  IDENTIFICAÇÃO DO PRODUTO:" -ForegroundColor Cyan
    Write-Host "   Nome: $($response.productName)" -ForegroundColor White
    Write-Host "   Marca: $($response.brand)" -ForegroundColor White
    Write-Host "   Categoria: $($response.category)" -ForegroundColor White
    Write-Host "   Peso: $($response.packageWeight)" -ForegroundColor White

    # Modo de Análise
    Write-Host "`n📋 MODO DE ANÁLISE:" -ForegroundColor Cyan
    $modeColor = if ($response.analysisMode -eq "FrontOfPackageOnly") { "Yellow" } else { "Green" }
    Write-Host "   $($response.analysisMode)" -ForegroundColor $modeColor

    # Claims Visíveis
    Write-Host "`n✨ CLAIMS VISÍVEIS NA EMBALAGEM:" -ForegroundColor Cyan
    if ($response.visibleClaims.Count -gt 0) {
        foreach ($claim in $response.visibleClaims) {
            Write-Host "   • $claim" -ForegroundColor White
        }
    } else {
        Write-Host "   (Nenhum claim identificado)" -ForegroundColor DarkGray
    }

    # Perfil Nutricional Estimado
    Write-Host "`n🍎 PERFIL NUTRICIONAL ESTIMADO:" -ForegroundColor Cyan
    $nutrition = $response.estimatedNutritionProfile
    Write-Host "   Calorias/100g: $($nutrition.caloriesPer100g) kcal" -ForegroundColor White
    if ($nutrition.estimatedPackageCalories) {
        Write-Host "   Calorias/embalagem: $($nutrition.estimatedPackageCalories) kcal" -ForegroundColor White
    }
    Write-Host "   Açúcar/100g: $($nutrition.estimatedSugarPer100g) g" -ForegroundColor White
    Write-Host "   Proteína/100g: $($nutrition.estimatedProteinPer100g) g" -ForegroundColor White
    Write-Host "   Sódio/100g: $($nutrition.estimatedSodiumPer100g) mg" -ForegroundColor White
    Write-Host "   Fibra/100g: $($nutrition.estimatedFiberPer100g) g" -ForegroundColor White
    Write-Host "   Gordura/100g: $($nutrition.estimatedFatPer100g) g" -ForegroundColor White
    Write-Host "`n   📌 Base: $($nutrition.basis)" -ForegroundColor Yellow

    # Classificação por Perfil
    Write-Host "`n👥 CLASSIFICAÇÃO POR PERFIL DE SAÚDE:" -ForegroundColor Cyan
    
    function Get-StatusColor($status) {
        switch ($status) {
            "mais_adequado" { return "Green" }
            "bom" { return "Green" }
            "consumo_moderado" { return "Yellow" }
            "moderado" { return "Yellow" }
            "fraco" { return "DarkYellow" }
            "nao_recomendado" { return "Red" }
            "nao_indicado" { return "Red" }
            default { return "White" }
        }
    }

    Write-Host "`n   🩺 Diabético:" -ForegroundColor White
    Write-Host "      Status: $($response.classification.diabetic.status)" -ForegroundColor (Get-StatusColor $response.classification.diabetic.status)
    Write-Host "      Razão: $($response.classification.diabetic.reason)" -ForegroundColor Gray

    Write-Host "`n   💓 Pressão Alta:" -ForegroundColor White
    Write-Host "      Status: $($response.classification.bloodPressure.status)" -ForegroundColor (Get-StatusColor $response.classification.bloodPressure.status)
    Write-Host "      Razão: $($response.classification.bloodPressure.reason)" -ForegroundColor Gray

    Write-Host "`n   ⚖️  Perda de Peso:" -ForegroundColor White
    Write-Host "      Status: $($response.classification.weightLoss.status)" -ForegroundColor (Get-StatusColor $response.classification.weightLoss.status)
    Write-Host "      Razão: $($response.classification.weightLoss.reason)" -ForegroundColor Gray

    Write-Host "`n   💪 Ganho Muscular:" -ForegroundColor White
    Write-Host "      Status: $($response.classification.muscleGain.status)" -ForegroundColor (Get-StatusColor $response.classification.muscleGain.status)
    Write-Host "      Razão: $($response.classification.muscleGain.reason)" -ForegroundColor Gray

    # Confiança Detalhada
    Write-Host "`n📊 CONFIANÇA DETALHADA:" -ForegroundColor Cyan
    $confidence = $response.confidenceDetails
    $prodIdPercent = [math]::Round($confidence.productIdentification * 100, 0)
    $claimsPercent = [math]::Round($confidence.visibleClaimsExtraction * 100, 0)
    $nutritionPercent = [math]::Round($confidence.estimatedNutritionProfile * 100, 0)
    $classificationPercent = [math]::Round($confidence.classification * 100, 0)

    Write-Host "   Identificação do Produto: $prodIdPercent%" -ForegroundColor $(if ($prodIdPercent -ge 70) { "Green" } else { "Yellow" })
    Write-Host "   Extração de Claims: $claimsPercent%" -ForegroundColor $(if ($claimsPercent -ge 70) { "Green" } else { "Yellow" })
    Write-Host "   Perfil Nutricional: $nutritionPercent%" -ForegroundColor $(if ($nutritionPercent -ge 70) { "Green" } else { "Yellow" })
    Write-Host "   Classificação: $classificationPercent%" -ForegroundColor $(if ($classificationPercent -ge 70) { "Green" } else { "Yellow" })

    # Avisos
    Write-Host "`n⚠️  AVISOS E LIMITAÇÕES:" -ForegroundColor Yellow
    if ($response.warnings.Count -gt 0) {
        foreach ($warning in $response.warnings) {
            Write-Host "   • $warning" -ForegroundColor Yellow
        }
    } else {
        Write-Host "   (Nenhum aviso)" -ForegroundColor Green
    }

    # Resumo
    Write-Host "`n📝 RESUMO TÉCNICO:" -ForegroundColor Cyan
    Write-Host "   $($response.summary)" -ForegroundColor White

    # Tempo de Processamento
    Write-Host "`n⏱️  PROCESSAMENTO:" -ForegroundColor Cyan
    Write-Host "   Tempo: $($response.processingTimeSeconds) segundos" -ForegroundColor White

    Write-Host "`n═══════════════════════════════════════════════════════════" -ForegroundColor Yellow
    Write-Host "✅ TESTE CONCLUÍDO COM SUCESSO!" -ForegroundColor Green
    Write-Host "═══════════════════════════════════════════════════════════`n" -ForegroundColor Yellow

    # Salvar JSON completo
    $jsonOutput = $response | ConvertTo-Json -Depth 10
    $jsonPath = "refactored_nutrition_result.json"
    $jsonOutput | Out-File -FilePath $jsonPath -Encoding UTF8
    Write-Host "💾 Resultado completo salvo em: $jsonPath`n" -ForegroundColor Cyan
}
catch {
    Write-Host "`n❌ ERRO na análise: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "`nDetalhes do erro:" -ForegroundColor Yellow
    Write-Host $_.Exception | Format-List -Force
    exit 1
}
