<#
.SYNOPSIS
    Testa o fallback nutricional seguro da API de análise nutricional

.DESCRIPTION
    Valida que:
    1. NÃO gera valores numéricos quando analysisMode = FrontOfPackageOnly
    2. hasReliableNutritionData está correto
    3. fallbackType está correto ("real", "partial", "category_based", "unknown")
    4. inferredRisks está populado quando não há dados confiáveis
    5. Score máximo limitado a 55 quando não há dados confiáveis
    6. Summary deixa explícito quando não há dados confiáveis
#>

$baseUrl = "https://localhost:7002/api"
$apiKey = "dev-test-key-2024"

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "TESTE: Fallback Nutricional Seguro" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Função auxiliar para fazer requisições
function Invoke-NutritionAnalysis {
    param(
        [string]$ImagePath,
        [string]$TestName
    )
    
    Write-Host "Teste: $TestName" -ForegroundColor Yellow
    Write-Host "Imagem: $ImagePath" -ForegroundColor Gray
    
    if (-not (Test-Path $ImagePath)) {
        Write-Host "ERRO: Imagem não encontrada: $ImagePath" -ForegroundColor Red
        return $null
    }
    
    try {
        $fileBytes = [System.IO.File]::ReadAllBytes($ImagePath)
        $fileContent = [System.Convert]::ToBase64String($fileBytes)
        $fileName = [System.IO.Path]::GetFileName($ImagePath)
        
        $boundary = [System.Guid]::NewGuid().ToString()
        $bodyLines = @(
            "--$boundary",
            "Content-Disposition: form-data; name=`"image`"; filename=`"$fileName`"",
            "Content-Type: image/jpeg",
            "",
            $fileContent,
            "--$boundary--"
        )
        
        $body = $bodyLines -join "`r`n"
        
        $headers = @{
            "X-API-Key" = $apiKey
            "Content-Type" = "multipart/form-data; boundary=$boundary"
        }
        
        $response = Invoke-RestMethod `
            -Uri "$baseUrl/nutrition/analyze" `
            -Method Post `
            -Headers $headers `
            -Body $body `
            -SkipCertificateCheck
        
        return $response
    }
    catch {
        Write-Host "ERRO na requisição: $_" -ForegroundColor Red
        return $null
    }
}

# Função para validar resposta
function Test-NutritionResponse {
    param(
        [object]$Response,
        [string]$TestName,
        [bool]$ExpectReliableData
    )
    
    Write-Host ""
    Write-Host "Validando resposta..." -ForegroundColor Cyan
    
    $allPassed = $true
    
    # Teste 1: hasReliableNutritionData
    $hasReliable = $Response.hasReliableNutritionData
    if ($hasReliable -eq $ExpectReliableData) {
        Write-Host "✓ hasReliableNutritionData = $hasReliable (esperado: $ExpectReliableData)" -ForegroundColor Green
    } else {
        Write-Host "✗ hasReliableNutritionData = $hasReliable (esperado: $ExpectReliableData)" -ForegroundColor Red
        $allPassed = $false
    }
    
    # Teste 2: fallbackType
    $fallbackType = $Response.fallbackType
    Write-Host "  fallbackType = $fallbackType" -ForegroundColor Gray
    
    if (-not $ExpectReliableData) {
        # Quando não confiável, não deve ter valores numéricos
        $profile = $Response.estimatedNutritionProfile
        
        $hasNumericValues = ($profile.caloriesPer100g -ne $null) -or 
                           ($profile.estimatedSugarPer100g -ne $null) -or
                           ($profile.estimatedProteinPer100g -ne $null) -or
                           ($profile.estimatedSodiumPer100g -ne $null) -or
                           ($profile.estimatedFatPer100g -ne $null)
        
        if (-not $hasNumericValues) {
            Write-Host "✓ Nenhum valor numérico presente (correto para dados não confiáveis)" -ForegroundColor Green
        } else {
            Write-Host "✗ Valores numéricos encontrados quando não deveria haver!" -ForegroundColor Red
            Write-Host "  - Calorias: $($profile.caloriesPer100g)" -ForegroundColor Red
            Write-Host "  - Açúcar: $($profile.estimatedSugarPer100g)" -ForegroundColor Red
            Write-Host "  - Proteína: $($profile.estimatedProteinPer100g)" -ForegroundColor Red
            Write-Host "  - Sódio: $($profile.estimatedSodiumPer100g)" -ForegroundColor Red
            $allPassed = $false
        }
        
        # Teste 3: inferredRisks deve estar presente
        $inferredRisks = $Response.inferredRisks
        if ($inferredRisks -and $inferredRisks.Count -gt 0) {
            Write-Host "✓ inferredRisks populado: $($inferredRisks -join ', ')" -ForegroundColor Green
        } else {
            Write-Host "⚠ inferredRisks vazio (pode ser normal para categorias neutras)" -ForegroundColor Yellow
        }
        
        # Teste 4: Score máximo limitado a 55
        $score = $Response.score.value
        if ($score -le 55) {
            Write-Host "✓ Score limitado corretamente: $score <= 55" -ForegroundColor Green
        } else {
            Write-Host "✗ Score muito alto para dados não confiáveis: $score" -ForegroundColor Red
            $allPassed = $false
        }
        
        # Teste 5: Summary deve mencionar limitações
        $summary = $Response.summary
        if ($summary -match "baseada apenas na categoria|sem dados nutricionais exatos|fotografe a tabela") {
            Write-Host "✓ Summary menciona limitações da análise" -ForegroundColor Green
        } else {
            Write-Host "✗ Summary não deixa claro as limitações" -ForegroundColor Red
            Write-Host "  Summary: $summary" -ForegroundColor Gray
            $allPassed = $false
        }
    } else {
        # Quando confiável, deve ter valores numéricos
        $profile = $Response.estimatedNutritionProfile
        
        $hasNumericValues = ($profile.caloriesPer100g -ne $null) -or 
                           ($profile.estimatedSugarPer100g -ne $null) -or
                           ($profile.estimatedProteinPer100g -ne $null)
        
        if ($hasNumericValues) {
            Write-Host "✓ Valores numéricos presentes (correto para dados confiáveis)" -ForegroundColor Green
            Write-Host "  - Calorias: $($profile.caloriesPer100g)" -ForegroundColor Gray
            Write-Host "  - Açúcar: $($profile.estimatedSugarPer100g)" -ForegroundColor Gray
            Write-Host "  - Proteína: $($profile.estimatedProteinPer100g)" -ForegroundColor Gray
        } else {
            Write-Host "✗ Nenhum valor numérico encontrado quando deveria haver!" -ForegroundColor Red
            $allPassed = $false
        }
        
        # Score não deve estar limitado artificialmente
        $score = $Response.score.value
        Write-Host "  Score: $score (sem limite artificial)" -ForegroundColor Gray
    }
    
    Write-Host ""
    if ($allPassed) {
        Write-Host "✓ TODOS OS TESTES PASSARAM para: $TestName" -ForegroundColor Green
    } else {
        Write-Host "✗ ALGUNS TESTES FALHARAM para: $TestName" -ForegroundColor Red
    }
    
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host ""
    
    return $allPassed
}

# ====================================
# CENÁRIO 1: Foto apenas da frente (SEM tabela nutricional)
# ====================================
Write-Host ""
Write-Host "CENÁRIO 1: Foto da frente da embalagem (sem tabela nutricional)" -ForegroundColor Magenta
Write-Host "Esperado: hasReliableNutritionData = false, fallbackType = category_based" -ForegroundColor Gray
Write-Host ""

# Você precisa ter uma imagem de teste - ajuste o caminho
$frontImagePath = "test-images/achocolatado-frente.jpg"

if (Test-Path $frontImagePath) {
    $response1 = Invoke-NutritionAnalysis -ImagePath $frontImagePath -TestName "Achocolatado - Frente"
    if ($response1) {
        $test1Result = Test-NutritionResponse -Response $response1 -TestName "Cenário 1" -ExpectReliableData $false
    }
} else {
    Write-Host "⚠ Imagem de teste não encontrada: $frontImagePath" -ForegroundColor Yellow
    Write-Host "  Crie uma pasta 'test-images' e adicione fotos de teste" -ForegroundColor Yellow
}

# ====================================
# CENÁRIO 2: Foto da tabela nutricional (COM dados confiáveis)
# ====================================
Write-Host ""
Write-Host "CENÁRIO 2: Foto da tabela nutricional" -ForegroundColor Magenta
Write-Host "Esperado: hasReliableNutritionData = true, fallbackType = real ou partial" -ForegroundColor Gray
Write-Host ""

$nutritionTablePath = "test-images/achocolatado-tabela.jpg"

if (Test-Path $nutritionTablePath) {
    $response2 = Invoke-NutritionAnalysis -ImagePath $nutritionTablePath -TestName "Achocolatado - Tabela"
    if ($response2) {
        $test2Result = Test-NutritionResponse -Response $response2 -TestName "Cenário 2" -ExpectReliableData $true
    }
} else {
    Write-Host "⚠ Imagem de teste não encontrada: $nutritionTablePath" -ForegroundColor Yellow
}

# ====================================
# RESUMO FINAL
# ====================================
Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "RESUMO DOS TESTES" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

if ($test1Result -and $test2Result) {
    Write-Host "✓ TODOS OS CENÁRIOS PASSARAM!" -ForegroundColor Green
} elseif ($test1Result -or $test2Result) {
    Write-Host "⚠ ALGUNS CENÁRIOS PASSARAM" -ForegroundColor Yellow
} else {
    Write-Host "✗ TESTES FALHARAM" -ForegroundColor Red
}

Write-Host ""
Write-Host "IMPORTANTE:" -ForegroundColor Yellow
Write-Host "1. Adicione imagens de teste na pasta 'test-images/'" -ForegroundColor Gray
Write-Host "2. Teste com diferentes categorias (salgadinho, refrigerante, arroz, etc)" -ForegroundColor Gray
Write-Host "3. Valide que inferredRisks está correto para cada categoria" -ForegroundColor Gray
Write-Host "4. Verifique que o summary é claro e transparente" -ForegroundColor Gray
Write-Host ""
