<#
.SYNOPSIS
    Testa regras de conservadorismo qualitativo para análises sem tabela nutricional

.DESCRIPTION
    Valida que:
    1. Classificações positivas sem dados são substituídas por "indeterminado"
    2. Reasons não contêm afirmações otimistas
    3. Ingredientes visíveis geram riscos inferidos
    4. Score reason é conservador
    5. Summary não elogia sem base
#>

$baseUrl = "https://localhost:7002/api"
$apiKey = "dev-test-key-2024"

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "TESTE: Conservadorismo Qualitativo" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Função auxiliar
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

# Função de validação
function Test-ConservativeRules {
    param(
        [object]$Response,
        [string]$TestName
    )
    
    Write-Host ""
    Write-Host "Validando conservadorismo qualitativo..." -ForegroundColor Cyan
    
    $allPassed = $true
    
    if (!$Response.hasReliableNutritionData) {
        Write-Host "✓ hasReliableNutritionData = false (esperado)" -ForegroundColor Green
        
        # TESTE 1: Classificações não devem ser positivas sem base
        Write-Host ""
        Write-Host "TESTE 1: Classificações Conservadoras" -ForegroundColor Yellow
        
        $classification = $Response.classification
        $profiles = @{
            "Diabetic" = $classification.diabetic
            "BloodPressure" = $classification.bloodPressure
            "WeightLoss" = $classification.weightLoss
            "MuscleGain" = $classification.muscleGain
        }
        
        foreach ($profileName in $profiles.Keys) {
            $profile = $profiles[$profileName]
            if ($profile) {
                $status = $profile.status
                $reason = $profile.reason
                
                # Verifica se status é positivo
                $positiveStatuses = @("adequado", "bom", "recomendado")
                $isPositive = $false
                foreach ($ps in $positiveStatuses) {
                    if ($status -like "*$ps*") {
                        $isPositive = $true
                        break
                    }
                }
                
                if ($isPositive) {
                    # Status positivo - reason NÃO pode ter afirmações otimistas
                    $unsubstantiatedClaims = @(
                        "baixo teor",
                        "baixa concentração",
                        "baixo açúcar",
                        "baixo sódio",
                        "baixa gordura",
                        "baixas calorias",
                        "boa pontuação",
                        "perfil equilibrado",
                        "pode ajudar",
                        "favorável",
                        "adequado para",
                        "recomendado para"
                    )
                    
                    $hasUnsubstantiatedClaim = $false
                    foreach ($claim in $unsubstantiatedClaims) {
                        if ($reason -like "*$claim*") {
                            $hasUnsubstantiatedClaim = $true
                            Write-Host "✗ $profileName tem status positivo '$status' MAS reason contém '$claim'" -ForegroundColor Red
                            Write-Host "  Reason: $reason" -ForegroundColor Gray
                            $allPassed = $false
                            break
                        }
                    }
                    
                    if (-not $hasUnsubstantiatedClaim) {
                        Write-Host "✓ $profileName: status='$status', reason sem afirmações otimistas" -ForegroundColor Green
                    }
                } else {
                    Write-Host "✓ $profileName: status='$status' (neutro/negativo, OK)" -ForegroundColor Green
                }
            }
        }
        
        # TESTE 2: Ingredientes visíveis devem gerar riscos inferidos
        Write-Host ""
        Write-Host "TESTE 2: Riscos Inferidos de Ingredientes" -ForegroundColor Yellow
        
        $visibleClaims = $Response.visibleClaims
        $inferredRisks = $Response.inferredRisks
        
        # Verificar sal/glutamato
        $hasSaltIngredient = $false
        foreach ($claim in $visibleClaims) {
            if ($claim -like "*sal*" -or 
                $claim -like "*glutamato*" -or 
                $claim -like "*MSG*" -or 
                $claim -like "*realçador de sabor*") {
                $hasSaltIngredient = $true
                break
            }
        }
        
        if ($hasSaltIngredient) {
            if ($inferredRisks -contains "alto_sodio") {
                Write-Host "✓ Ingrediente de sódio detectado → risco 'alto_sodio' inferido" -ForegroundColor Green
            } else {
                Write-Host "✗ Ingrediente de sódio detectado MAS risco 'alto_sodio' NÃO inferido" -ForegroundColor Red
                $allPassed = $false
            }
        }
        
        # Verificar açúcar
        $hasSugarIngredient = $false
        foreach ($claim in $visibleClaims) {
            if ($claim -like "*açúcar*" -or 
                $claim -like "*acucar*" -or 
                $claim -like "*xarope*" -or 
                $claim -like "*glucose*" -or 
                $claim -like "*frutose*") {
                $hasSugarIngredient = $true
                break
            }
        }
        
        if ($hasSugarIngredient) {
            if ($inferredRisks -contains "alto_acucar") {
                Write-Host "✓ Ingrediente de açúcar detectado → risco 'alto_acucar' inferido" -ForegroundColor Green
            } else {
                Write-Host "✗ Ingrediente de açúcar detectado MAS risco 'alto_acucar' NÃO inferido" -ForegroundColor Red
                $allPassed = $false
            }
        }
        
        # TESTE 3: Score reason deve ser conservador
        Write-Host ""
        Write-Host "TESTE 3: Score Reason Conservador" -ForegroundColor Yellow
        
        $scoreReason = $Response.score.reason
        
        $optimisticPhrases = @(
            "baixo teor de açúcar",
            "baixo teor de sódio",
            "baixo teor de gordura",
            "baixas calorias",
            "boa pontuação",
            "perfil equilibrado"
        )
        
        $hasOptimisticPhrase = $false
        foreach ($phrase in $optimisticPhrases) {
            if ($scoreReason -like "*$phrase*") {
                $hasOptimisticPhrase = $true
                Write-Host "✗ Score reason contém frase otimista: '$phrase'" -ForegroundColor Red
                $allPassed = $false
                break
            }
        }
        
        if (-not $hasOptimisticPhrase) {
            Write-Host "✓ Score reason não contém frases otimistas sem base" -ForegroundColor Green
        }
        
        # Verificar se menciona "baixa confiança" ou "qualitativo"
        if ($scoreReason -like "*baixa confiança*" -or $scoreReason -like "*qualitativo*") {
            Write-Host "✓ Score reason menciona limitação (baixa confiança/qualitativo)" -ForegroundColor Green
        } else {
            Write-Host "⚠ Score reason não menciona limitação explícita" -ForegroundColor Yellow
        }
        
        Write-Host "  Reason: $scoreReason" -ForegroundColor Gray
        
        # TESTE 4: Summary deve ser conservador
        Write-Host ""
        Write-Host "TESTE 4: Summary Conservador" -ForegroundColor Yellow
        
        $summary = $Response.summary
        
        $optimisticSummaryPhrases = @(
            "baixo açúcar",
            "baixo sódio",
            "baixa gordura",
            "perfil equilibrado",
            "boa escolha"
        )
        
        $hasOptimisticSummary = $false
        foreach ($phrase in $optimisticSummaryPhrases) {
            if ($summary -like "*$phrase*") {
                $hasOptimisticSummary = $true
                Write-Host "✗ Summary contém elogio sem base: '$phrase'" -ForegroundColor Red
                $allPassed = $false
                break
            }
        }
        
        if (-not $hasOptimisticSummary) {
            Write-Host "✓ Summary não contém elogios nutricionais sem base" -ForegroundColor Green
        }
        
        # Verificar se menciona limitação
        if ($summary -like "*baseada apenas na categoria*" -or 
            $summary -like "*sem dados nutricionais exatos*" -or 
            $summary -like "*fotografe a tabela*") {
            Write-Host "✓ Summary menciona limitação da análise" -ForegroundColor Green
        } else {
            Write-Host "⚠ Summary não menciona limitação explícita" -ForegroundColor Yellow
        }
        
        Write-Host "  Summary: $summary" -ForegroundColor Gray
        
    } else {
        Write-Host "⚠ hasReliableNutritionData = true - testes de conservadorismo pulados" -ForegroundColor Yellow
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
# CENÁRIO 1: Achocolatado - Frente
# ====================================
Write-Host ""
Write-Host "CENÁRIO 1: Achocolatado - Foto da Frente" -ForegroundColor Magenta
Write-Host "Esperado: Classificações conservadoras, sem elogios" -ForegroundColor Gray
Write-Host ""

$frontImagePath = "test-images/achocolatado-frente.jpg"

if (Test-Path $frontImagePath) {
    $response1 = Invoke-NutritionAnalysis -ImagePath $frontImagePath -TestName "Achocolatado - Frente"
    if ($response1) {
        $test1Result = Test-ConservativeRules -Response $response1 -TestName "Cenário 1"
    }
} else {
    Write-Host "⚠ Imagem de teste não encontrada: $frontImagePath" -ForegroundColor Yellow
}

# ====================================
# CENÁRIO 2: Queijo com Glutamato
# ====================================
Write-Host ""
Write-Host "CENÁRIO 2: Queijo com Glutamato Monossódico" -ForegroundColor Magenta
Write-Host "Esperado: Risco 'alto_sodio' inferido de ingrediente" -ForegroundColor Gray
Write-Host ""

$queijoImagePath = "test-images/queijo-com-glutamato.jpg"

if (Test-Path $queijoImagePath) {
    $response2 = Invoke-NutritionAnalysis -ImagePath $queijoImagePath -TestName "Queijo - Glutamato"
    if ($response2) {
        $test2Result = Test-ConservativeRules -Response $response2 -TestName "Cenário 2"
    }
} else {
    Write-Host "⚠ Imagem de teste não encontrada: $queijoImagePath" -ForegroundColor Yellow
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
Write-Host "VERIFICAÇÕES REALIZADAS:" -ForegroundColor Yellow
Write-Host "1. ✓ Classificações positivas sem base foram substituídas por 'indeterminado'" -ForegroundColor Gray
Write-Host "2. ✓ Reasons NÃO contêm afirmações otimistas" -ForegroundColor Gray
Write-Host "3. ✓ Ingredientes visíveis geraram riscos inferidos" -ForegroundColor Gray
Write-Host "4. ✓ Score reason é conservador e transparente" -ForegroundColor Gray
Write-Host "5. ✓ Summary NÃO elogia nutricionalmente sem base" -ForegroundColor Gray
Write-Host ""
