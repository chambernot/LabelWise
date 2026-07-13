# test-nutrition-acceptance-fix.ps1
# Script para testar a correção da lógica de aceitação da análise nutricional

Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host "  Teste da Correção da Lógica de Aceitação - Análise Nutricional" -ForegroundColor Cyan
Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host ""

$apiUrl = "http://localhost:5111"

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

# Função para fazer upload de imagem
function Test-NutritionAnalysis {
    param(
        [string]$ImagePath,
        [string]$Token,
        [string]$TestName
    )

    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
    Write-Host "📋 Teste: $TestName" -ForegroundColor Cyan
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray

    if (-not (Test-Path $ImagePath)) {
        Write-Host "⚠️ Imagem não encontrada: $ImagePath" -ForegroundColor Yellow
        Write-Host "   Pulando este teste..." -ForegroundColor Yellow
        return
    }

    try {
        $fileBytes = [System.IO.File]::ReadAllBytes($ImagePath)
        $fileName = [System.IO.Path]::GetFileName($ImagePath)
        $boundary = [System.Guid]::NewGuid().ToString()

        $LF = "`r`n"
        $bodyLines = (
            "--$boundary",
            "Content-Disposition: form-data; name=`"image`"; filename=`"$fileName`"",
            "Content-Type: image/jpeg",
            "",
            [System.Text.Encoding]::GetEncoding("iso-8859-1").GetString($fileBytes),
            "--$boundary--"
        ) -join $LF

        $headers = @{
            "Content-Type" = "multipart/form-data; boundary=$boundary"
        }

        if ($Token) {
            $headers["Authorization"] = "Bearer $Token"
        }

        Write-Host "⏳ Enviando imagem para análise..." -ForegroundColor Yellow

        $response = Invoke-RestMethod `
            -Uri "$apiUrl/api/nutrition/analyze" `
            -Method Post `
            -Headers $headers `
            -Body ([System.Text.Encoding]::GetEncoding("iso-8859-1").GetBytes($bodyLines)) `
            -TimeoutSec 60

        Write-Host ""
        Write-Host "📊 RESULTADO DA ANÁLISE:" -ForegroundColor Green
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
        } else {
            Write-Host "📂 Category: NULL" -ForegroundColor Yellow
        }

        # Brand
        if ($response.brand) {
            Write-Host "🏷️ Brand: $($response.brand)" -ForegroundColor Cyan
        }

        # Nutrition Profile
        Write-Host ""
        Write-Host "🥗 Estimated Nutrition Profile:" -ForegroundColor Magenta
        if ($response.estimatedNutritionProfile) {
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
            if ($profile.estimatedSodiumPer100g) {
                Write-Host "   • Sódio/100g: $($profile.estimatedSodiumPer100g)mg" -ForegroundColor White
            }
        } else {
            Write-Host "   NULL" -ForegroundColor Yellow
        }

        # Classification
        Write-Host ""
        Write-Host "🎯 Classification:" -ForegroundColor Magenta
        if ($response.classification) {
            $class = $response.classification
            if ($class.diabetic) {
                $status = $class.diabetic.status
                $color = if ($status -eq "indeterminado") { "Yellow" } elseif ($status -eq "evitar") { "Red" } elseif ($status -eq "consumo_moderado") { "Yellow" } else { "Green" }
                Write-Host "   • Diabético: $status" -ForegroundColor $color
            }
            if ($class.bloodPressure) {
                $status = $class.bloodPressure.status
                $color = if ($status -eq "indeterminado") { "Yellow" } elseif ($status -eq "evitar") { "Red" } elseif ($status -eq "consumo_moderado") { "Yellow" } else { "Green" }
                Write-Host "   • Pressão Alta: $status" -ForegroundColor $color
            }
            if ($class.weightLoss) {
                $status = $class.weightLoss.status
                $color = if ($status -eq "indeterminado") { "Yellow" } elseif ($status -eq "evitar") { "Red" } elseif ($status -eq "consumo_moderado") { "Yellow" } else { "Green" }
                Write-Host "   • Emagrecimento: $status" -ForegroundColor $color
            }
            if ($class.muscleGain) {
                $status = $class.muscleGain.status
                $color = if ($status -eq "indeterminado") { "Yellow" } elseif ($status -eq "evitar") { "Red" } elseif ($status -eq "consumo_moderado") { "Yellow" } else { "Green" }
                Write-Host "   • Ganho Muscular: $status" -ForegroundColor $color
            }
        } else {
            Write-Host "   NULL" -ForegroundColor Yellow
        }

        # Score
        Write-Host ""
        if ($response.score) {
            $score = $response.score
            $scoreColor = if ($score.score -ge 70) { "Green" } elseif ($score.score -ge 40) { "Yellow" } else { "Red" }
            Write-Host "🎯 Score: $($score.score) - $($score.status) ($($score.color))" -ForegroundColor $scoreColor
            Write-Host "   Label: $($score.label)" -ForegroundColor White
        } else {
            Write-Host "🎯 Score: NULL" -ForegroundColor Yellow
        }

        # Error Message
        if ($response.errorMessage) {
            Write-Host ""
            Write-Host "⚠️ Error Message: $($response.errorMessage)" -ForegroundColor Red
        }

        # Analysis Mode
        if ($response.analysisMode) {
            Write-Host ""
            Write-Host "🔍 Analysis Mode: $($response.analysisMode)" -ForegroundColor Cyan
        }

        # Warnings
        if ($response.warnings -and $response.warnings.Count -gt 0) {
            Write-Host ""
            Write-Host "⚠️ Warnings:" -ForegroundColor Yellow
            foreach ($warning in $response.warnings) {
                Write-Host "   • $warning" -ForegroundColor Yellow
            }
        }

        Write-Host ""
        Write-Host "─────────────────────────────────────────────────────" -ForegroundColor DarkGray

        # Validação da correção
        Write-Host ""
        Write-Host "🔍 VALIDAÇÃO DA CORREÇÃO:" -ForegroundColor Cyan
        Write-Host "─────────────────────────────────────────────────────" -ForegroundColor DarkGray

        $hasCategory = -not [string]::IsNullOrWhiteSpace($response.category)
        $hasNutrition = $response.estimatedNutritionProfile -ne $null -and (
            $response.estimatedNutritionProfile.caloriesPer100g -ne $null -or
            $response.estimatedNutritionProfile.estimatedSugarPer100g -ne $null -or
            $response.estimatedNutritionProfile.estimatedProteinPer100g -ne $null
        )
        $hasClassification = $response.classification -ne $null -and (
            ($response.classification.diabetic -ne $null -and $response.classification.diabetic.status -ne "indeterminado") -or
            ($response.classification.bloodPressure -ne $null -and $response.classification.bloodPressure.status -ne "indeterminado") -or
            ($response.classification.weightLoss -ne $null -and $response.classification.weightLoss.status -ne "indeterminado") -or
            ($response.classification.muscleGain -ne $null -and $response.classification.muscleGain.status -ne "indeterminado")
        )

        $shouldBeSuccess = $hasCategory -or $hasNutrition -or $hasClassification

        if ($shouldBeSuccess -eq $response.success) {
            Write-Host "✅ Success está correto!" -ForegroundColor Green
        } else {
            Write-Host "❌ Success está INCORRETO!" -ForegroundColor Red
            Write-Host "   Esperado: $shouldBeSuccess" -ForegroundColor Yellow
            Write-Host "   Recebido: $($response.success)" -ForegroundColor Yellow
        }

        # Validação do fallback de productName
        if ($hasCategory -and [string]::IsNullOrWhiteSpace($response.productName)) {
            Write-Host "⚠️ Category preenchida mas ProductName está NULL!" -ForegroundColor Yellow
            Write-Host "   ❌ Fallback NÃO foi aplicado corretamente!" -ForegroundColor Red
        } elseif ($hasCategory -and -not [string]::IsNullOrWhiteSpace($response.productName)) {
            Write-Host "✅ ProductName preenchido (fallback aplicado ou original)!" -ForegroundColor Green
        }

        # Validação do score
        if (-not $response.success -and $response.score -and $response.score.score -ne 0) {
            Write-Host "❌ Score NÃO deveria ser calculado para success=false!" -ForegroundColor Red
            Write-Host "   Score deveria ser 0 (indeterminado)" -ForegroundColor Yellow
        } elseif (-not $response.success -and $response.score -and $response.score.score -eq 0) {
            Write-Host "✅ Score indeterminado correto para falha!" -ForegroundColor Green
        } elseif ($response.success -and $response.score -and $response.score.score -gt 0) {
            Write-Host "✅ Score calculado corretamente para sucesso!" -ForegroundColor Green
        }

        Write-Host ""

    } catch {
        Write-Host ""
        Write-Host "❌ Erro ao processar imagem:" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
        Write-Host ""
    }
}

# ===== TESTES =====

Write-Host ""
Write-Host "🧪 Iniciando testes de validação..." -ForegroundColor Cyan
Write-Host ""

# Solicitar token (opcional)
Write-Host "🔑 Token JWT (deixe em branco se autenticação não estiver habilitada):" -ForegroundColor Yellow
$token = Read-Host

Write-Host ""

# Teste 1: Imagem completa
$testImagePath1 = "C:\temp\nutrition_full.jpg"
if (Test-Path $testImagePath1) {
    Test-NutritionAnalysis -ImagePath $testImagePath1 -Token $token -TestName "Imagem com Tabela Nutricional Completa"
}

# Teste 2: Imagem frontal (provavelmente retorna só category)
$testImagePath2 = "C:\temp\front_package.jpg"
if (Test-Path $testImagePath2) {
    Test-NutritionAnalysis -ImagePath $testImagePath2 -Token $token -TestName "Imagem Frontal (Category Only)"
}

# Teste 3: Imagem de baixa qualidade (provavelmente retorna dados parciais)
$testImagePath3 = "C:\temp\low_quality.jpg"
if (Test-Path $testImagePath3) {
    Test-NutritionAnalysis -ImagePath $testImagePath3 -Token $token -TestName "Imagem de Baixa Qualidade (Dados Parciais)"
}

# Se nenhuma imagem foi encontrada, mostrar mensagem
if (-not (Test-Path $testImagePath1) -and -not (Test-Path $testImagePath2) -and -not (Test-Path $testImagePath3)) {
    Write-Host ""
    Write-Host "⚠️ Nenhuma imagem de teste encontrada!" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Para testar, coloque imagens em:" -ForegroundColor White
    Write-Host "   • C:\temp\nutrition_full.jpg (imagem com tabela nutricional)" -ForegroundColor Cyan
    Write-Host "   • C:\temp\front_package.jpg (imagem frontal da embalagem)" -ForegroundColor Cyan
    Write-Host "   • C:\temp\low_quality.jpg (imagem de baixa qualidade)" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Ou edite o script para usar suas próprias imagens." -ForegroundColor White
    Write-Host ""
}

Write-Host ""
Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host "  Testes Concluídos!" -ForegroundColor Cyan
Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "✅ Verifique os resultados acima para validar a correção." -ForegroundColor Green
Write-Host ""
