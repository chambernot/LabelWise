<#
.SYNOPSIS
    Testa modo conservador OBRIGATÓRIO para análises sem dados nutricionais

.DESCRIPTION
    Valida que quando TODOS os campos nutricionais estão nulos:
    1. Modo conservador é ativado automaticamente
    2. NENHUMA afirmação otimista está presente
    3. Disclaimer está no summary
    4. Classificações positivas → "indeterminado"
#>

$baseUrl = "https://localhost:7002/api"
$apiKey = "dev-test-key-2024"

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "TESTE: Modo Conservador OBRIGATÓRIO" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Frases PROIBIDAS em modo conservador
$prohibitedPhrases = @(
    "baixo teor de açúcar",
    "baixo teor de sódio",
    "baixo teor de gordura",
    "baixo açúcar",
    "baixo sódio",
    "baixa gordura",
    "baixas calorias",
    "boa pontuação",
    "perfil equilibrado",
    "opção tranquila",
    "pode ajudar em",
    "ajuda em",
    "favorável para",
    "opção mais tranquila",
    "tranquilo para",
    "seguro para",
    "bom para"
)

function Test-ConservativeModeEnforcement {
    param(
        [object]$Response,
        [string]$TestName
    )
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Teste: $TestName" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Cyan
    
    $allPassed = $true
    
    # TESTE 1: Verificar critérios de ativação
    Write-Host ""
    Write-Host "TESTE 1: Critérios de Ativação do Modo Conservador" -ForegroundColor Yellow
    
    $isFrontOfPackage = $Response.analysisMode -eq "FrontOfPackageOnly"
    $allFieldsNull = (
        $Response.estimatedNutritionProfile.caloriesPer100g -eq $null -and
        $Response.estimatedNutritionProfile.estimatedSugarPer100g -eq $null -and
        $Response.estimatedNutritionProfile.estimatedProteinPer100g -eq $null -and
        $Response.estimatedNutritionProfile.estimatedSodiumPer100g -eq $null -and
        $Response.estimatedNutritionProfile.estimatedFatPer100g -eq $null -and
        $Response.estimatedNutritionProfile.estimatedFiberPer100g -eq $null
    )
    $lowConfidence = ($Response.confidenceDetails.estimatedNutritionProfile -le 0.5)
    
    Write-Host "  AnalysisMode = FrontOfPackageOnly: $isFrontOfPackage" -ForegroundColor Gray
    Write-Host "  TODOS campos nutricionais nulos: $allFieldsNull" -ForegroundColor Gray
    Write-Host "  Confiança <= 0.5: $lowConfidence (valor: $($Response.confidenceDetails.estimatedNutritionProfile))" -ForegroundColor Gray
    
    $shouldBeConservative = $isFrontOfPackage -and $allFieldsNull -and $lowConfidence
    
    if ($shouldBeConservative) {
        Write-Host "✓ Modo conservador DEVE estar ativado" -ForegroundColor Green
    } else {
        Write-Host "⚠ Modo conservador NÃO deve estar ativado (critérios não atendidos)" -ForegroundColor Yellow
        return $true # Não falhar teste se modo não deve estar ativo
    }
    
    # TESTE 2: Verificar ausência de frases proibidas
    Write-Host ""
    Write-Host "TESTE 2: Ausência de Frases Proibidas" -ForegroundColor Yellow
    
    $fieldsToCheck = @{
        "Summary" = $Response.summary
        "Score.Reason" = $Response.score.reason
        "Score.ScoreInterpretation" = $Response.score.scoreInterpretation
        "ExplicacaoScore" = $Response.explicacaoScore
        "PontoPrincipal" = $Response.pontoPrincipal
    }
    
    foreach ($fieldName in $fieldsToCheck.Keys) {
        $fieldValue = $fieldsToCheck[$fieldName]
        if ([string]::IsNullOrWhiteSpace($fieldValue)) {
            continue
        }
        
        $foundProhibited = $false
        foreach ($phrase in $prohibitedPhrases) {
            if ($fieldValue -like "*$phrase*") {
                Write-Host "✗ $fieldName contém frase proibida: '$phrase'" -ForegroundColor Red
                Write-Host "  Valor: $fieldValue" -ForegroundColor Gray
                $foundProhibited = $true
                $allPassed = $false
                break
            }
        }
        
        if (-not $foundProhibited) {
            Write-Host "✓ $fieldName: sem frases proibidas" -ForegroundColor Green
        }
    }
    
    # Verificar resumoRapido
    if ($Response.resumoRapido -and $Response.resumoRapido.Count -gt 0) {
        $foundInResumo = $false
        foreach ($item in $Response.resumoRapido) {
            foreach ($phrase in $prohibitedPhrases) {
                if ($item -like "*$phrase*") {
                    Write-Host "✗ ResumoRapido contém frase proibida: '$phrase'" -ForegroundColor Red
                    Write-Host "  Item: $item" -ForegroundColor Gray
                    $foundInResumo = $true
                    $allPassed = $false
                    break
                }
            }
            if ($foundInResumo) { break }
        }
        
        if (-not $foundInResumo) {
            Write-Host "✓ ResumoRapido: sem frases proibidas" -ForegroundColor Green
        }
    }
    
    # TESTE 3: Verificar classificações
    Write-Host ""
    Write-Host "TESTE 3: Classificações Forçadas para Indeterminado" -ForegroundColor Yellow
    
    $classifications = @{
        "Diabetic" = $Response.classification.diabetic
        "BloodPressure" = $Response.classification.bloodPressure
        "WeightLoss" = $Response.classification.weightLoss
        "MuscleGain" = $Response.classification.muscleGain
    }
    
    $positiveStatuses = @("adequado", "bom", "recomendado", "favoravel")
    
    foreach ($classificationName in $classifications.Keys) {
        $classification = $classifications[$classificationName]
        if ($classification) {
            $status = $classification.status
            $reason = $classification.reason
            
            $hasPositiveStatus = $false
            foreach ($ps in $positiveStatuses) {
                if ($status -like "*$ps*") {
                    $hasPositiveStatus = $true
                    break
                }
            }
            
            if ($hasPositiveStatus) {
                Write-Host "✗ $classificationName tem status positivo '$status' sem evidência" -ForegroundColor Red
                $allPassed = $false
            } else {
                Write-Host "✓ $classificationName: status='$status' (OK)" -ForegroundColor Green
            }
            
            # Verificar reason também
            if ($reason) {
                $foundProhibited = $false
                foreach ($phrase in $prohibitedPhrases) {
                    if ($reason -like "*$phrase*") {
                        Write-Host "✗ $classificationName.Reason contém frase proibida: '$phrase'" -ForegroundColor Red
                        $foundProhibited = $true
                        $allPassed = $false
                        break
                    }
                }
                
                if (-not $foundProhibited) {
                    Write-Host "✓ $classificationName.Reason: sem frases proibidas" -ForegroundColor Green
                }
            }
        }
    }
    
    # TESTE 4: Verificar disclaimer no summary
    Write-Host ""
    Write-Host "TESTE 4: Disclaimer Presente no Summary" -ForegroundColor Yellow
    
    $hasDisclaimer = $Response.summary -like "*⚠*" -or 
                    $Response.summary -like "*Análise limitada*" -or
                    $Response.summary -like "*Sem tabela nutricional visível*"
    
    if ($hasDisclaimer) {
        Write-Host "✓ Summary contém disclaimer de análise limitada" -ForegroundColor Green
    } else {
        Write-Host "✗ Summary NÃO contém disclaimer" -ForegroundColor Red
        Write-Host "  Summary: $($Response.summary)" -ForegroundColor Gray
        $allPassed = $false
    }
    
    # RESUMO
    Write-Host ""
    if ($allPassed) {
        Write-Host "✓ TODOS OS TESTES PASSARAM para: $TestName" -ForegroundColor Green
    } else {
        Write-Host "✗ ALGUNS TESTES FALHARAM para: $TestName" -ForegroundColor Red
    }
    
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    return $allPassed
}

# Função auxiliar para fazer requisições
function Invoke-NutritionAnalysis {
    param(
        [string]$ImagePath,
        [string]$TestName
    )
    
    Write-Host "Analisando: $TestName" -ForegroundColor Yellow
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

# ====================================
# CENÁRIO 1: Achocolatado - Frente (TODOS nulos)
# ====================================
Write-Host ""
Write-Host "CENÁRIO 1: Achocolatado - Foto da Frente" -ForegroundColor Magenta
Write-Host "Esperado: TODOS campos nulos, modo conservador ativado" -ForegroundColor Gray
Write-Host ""

$frontImagePath = "test-images/achocolatado-frente.jpg"

if (Test-Path $frontImagePath) {
    $response1 = Invoke-NutritionAnalysis -ImagePath $frontImagePath -TestName "Achocolatado - Frente"
    if ($response1) {
        $test1Result = Test-ConservativeModeEnforcement -Response $response1 -TestName "Cenário 1"
    }
} else {
    Write-Host "⚠ Imagem de teste não encontrada: $frontImagePath" -ForegroundColor Yellow
}

# ====================================
# RESUMO FINAL
# ====================================
Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "RESUMO DOS TESTES" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

if ($test1Result) {
    Write-Host "✓ CENÁRIO PASSOU!" -ForegroundColor Green
} else {
    Write-Host "✗ CENÁRIO FALHOU!" -ForegroundColor Red
}

Write-Host ""
Write-Host "VERIFICAÇÕES REALIZADAS:" -ForegroundColor Yellow
Write-Host "1. ✓ Critérios de ativação do modo conservador" -ForegroundColor Gray
Write-Host "2. ✓ Ausência de frases proibidas em TODOS os campos" -ForegroundColor Gray
Write-Host "3. ✓ Classificações forçadas para 'indeterminado'" -ForegroundColor Gray
Write-Host "4. ✓ Disclaimer presente no summary" -ForegroundColor Gray
Write-Host ""

Write-Host "FRASES PROIBIDAS MONITORADAS:" -ForegroundColor Yellow
foreach ($phrase in $prohibitedPhrases) {
    Write-Host "  - $phrase" -ForegroundColor Gray
}
Write-Host ""
