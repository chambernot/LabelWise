# Test Script: Refinamento Final de Summary e VisibleClaims
# Valida: summary direto, coerente com classificação, e visibleClaims filtradas

Write-Host "=== TESTE: REFINAMENTO FINAL - SUMMARY E VISIBLECLAIMS ===" -ForegroundColor Cyan
Write-Host ""

# Test endpoint
$apiUrl = "http://localhost:5000"
$endpoint = "$apiUrl/api/nutrition/analyze"

# Test images - use produtos reais que você tenha
$testImages = @(
    @{
        Path = "test-images\achocolatado-nescau.jpg"
        ExpectedIssue = "açúcar"
        ExpectedNoInClaims = @("nescau", "achocolatado")
        ExpectedYesInClaims = @("vitamina", "fortificado")
    },
    @{
        Path = "test-images\arroz-tio-joao.jpg"
        ExpectedIssue = $null  # Produto equilibrado
        ExpectedNoInClaims = @("tio joão", "arroz", "tipo 1")
        ExpectedYesInClaims = @("enriquecido", "vitamina")  # Se tiver
    }
)

$boundary = [System.Guid]::NewGuid().ToString()

function Test-Image($testCase) {
    $imagePath = $testCase.Path
    
    if (-not (Test-Path $imagePath)) {
        Write-Host "⚠️  Imagem não encontrada: $imagePath" -ForegroundColor Yellow
        return $false
    }

    Write-Host "🔍 Testando: $(Split-Path $imagePath -Leaf)" -ForegroundColor Green
    Write-Host ""

    try {
        # Read image
        $fileBin = [System.IO.File]::ReadAllBytes($imagePath)
        $enc = [System.Text.Encoding]::GetEncoding("iso-8859-1")

        $bodyLines = @(
            "--$boundary",
            "Content-Disposition: form-data; name=`"image`"; filename=`"$(Split-Path $imagePath -Leaf)`"",
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

        $response = Invoke-RestMethod -Uri $endpoint `
            -Method Post `
            -ContentType "multipart/form-data; boundary=$boundary" `
            -Body $body `
            -TimeoutSec 60

        # === VALIDAÇÃO 1: SUMMARY ===
        Write-Host "1️⃣ VALIDAÇÃO DO SUMMARY:" -ForegroundColor Yellow
        Write-Host "   Summary: $($response.summary)" -ForegroundColor White
        Write-Host ""

        $summaryIssues = @()

        # Não deve suavizar açúcar elevado
        if ($response.summary -match "açúcar moderado|açúcar dentro|açúcar razoável") {
            $summaryIssues += "❌ Summary suaviza açúcar elevado"
        } else {
            Write-Host "   ✅ Não suaviza açúcar elevado" -ForegroundColor Green
        }

        # Deve ser direto quando há problema
        if ($testCase.ExpectedIssue -and $response.summary -match $testCase.ExpectedIssue) {
            Write-Host "   ✅ Destaca problema principal: $($testCase.ExpectedIssue)" -ForegroundColor Green
        } elseif ($testCase.ExpectedIssue) {
            $summaryIssues += "⚠️  Não destaca problema esperado: $($testCase.ExpectedIssue)"
        }

        # Deve ser coerente com classificação
        if ($response.classification.diabetic.status -eq "nao_recomendado") {
            if ($response.summary -match "não adequado|não recomendado|principal ponto de atenção") {
                Write-Host "   ✅ Coerente com classificação 'não recomendado'" -ForegroundColor Green
            } else {
                $summaryIssues += "⚠️  Summary não coerente com classificação 'não recomendado'"
            }
        }

        if ($summaryIssues.Count -eq 0) {
            Write-Host "   ✅ Summary validado com sucesso!" -ForegroundColor Green
        } else {
            foreach ($issue in $summaryIssues) {
                Write-Host "   $issue" -ForegroundColor Yellow
            }
        }
        Write-Host ""

        # === VALIDAÇÃO 2: VISIBLECLAIMS ===
        Write-Host "2️⃣ VALIDAÇÃO DE VISIBLECLAIMS:" -ForegroundColor Yellow
        Write-Host "   Claims extraídas:" -ForegroundColor White
        if ($response.visibleClaims -and $response.visibleClaims.Count -gt 0) {
            foreach ($claim in $response.visibleClaims) {
                Write-Host "     - $claim" -ForegroundColor Gray
            }
        } else {
            Write-Host "     (nenhuma claim nutricional encontrada)" -ForegroundColor Gray
        }
        Write-Host ""

        $claimsIssues = @()

        # Não deve conter nomes de produtos ou marcas
        foreach ($forbidden in $testCase.ExpectedNoInClaims) {
            $found = $response.visibleClaims | Where-Object { 
                $_ -match [regex]::Escape($forbidden) 
            }
            
            if ($found) {
                $claimsIssues += "❌ Contém nome/marca indevido: '$forbidden' → '$found'"
            } else {
                Write-Host "   ✅ Não contém nome/marca: $forbidden" -ForegroundColor Green
            }
        }

        # Deve conter apenas alegações nutricionais
        $nutritionalKeywords = @(
            "vitamina", "mineral", "cálcio", "ferro", "fonte", "rico",
            "sem glúten", "sem lactose", "fortificado", "enriquecido",
            "zero", "light", "diet", "integral", "orgânico"
        )

        if ($response.visibleClaims -and $response.visibleClaims.Count -gt 0) {
            $allAreNutritional = $true
            foreach ($claim in $response.visibleClaims) {
                $isNutritional = $false
                foreach ($keyword in $nutritionalKeywords) {
                    if ($claim -match $keyword) {
                        $isNutritional = $true
                        break
                    }
                }
                
                if (-not $isNutritional) {
                    $claimsIssues += "⚠️  Claim pode não ser nutricional: '$claim'"
                    $allAreNutritional = $false
                }
            }

            if ($allAreNutritional) {
                Write-Host "   ✅ Todas as claims são nutricionais/funcionais" -ForegroundColor Green
            }
        }

        if ($claimsIssues.Count -eq 0) {
            Write-Host "   ✅ VisibleClaims validadas com sucesso!" -ForegroundColor Green
        } else {
            foreach ($issue in $claimsIssues) {
                Write-Host "   $issue" -ForegroundColor Yellow
            }
        }
        Write-Host ""

        # === VALIDAÇÃO 3: COERÊNCIA GERAL ===
        Write-Host "3️⃣ VALIDAÇÃO DE COERÊNCIA GERAL:" -ForegroundColor Yellow
        
        # Score baixo + "não recomendado" deve ter summary direto
        if ($response.score.value -lt 40 -and 
            $response.classification.diabetic.status -eq "nao_recomendado") {
            
            if ($response.summary -match "adequado para diabéticos|açúcar elevado|principal ponto") {
                Write-Host "   ✅ Summary coerente com score baixo e classificação ruim" -ForegroundColor Green
            } else {
                Write-Host "   ⚠️  Summary não reflete severidade do score/classificação" -ForegroundColor Yellow
            }
        }

        # Alta classificação + claims nutricionais devem combinar
        $hasPositiveClaims = $response.visibleClaims | Where-Object { 
            $_ -match "fonte|rico|vitamina|mineral|fortificado" 
        }

        if ($hasPositiveClaims -and $response.score.value -gt 60) {
            Write-Host "   ✅ Claims positivas coerentes com score alto" -ForegroundColor Green
        } elseif ($hasPositiveClaims -and $response.score.value -lt 40) {
            Write-Host "   ℹ️  Claims positivas mas score baixo (possível alto açúcar)" -ForegroundColor Cyan
        }

        Write-Host ""
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
        Write-Host ""

        return $true

    } catch {
        Write-Host "❌ Erro ao testar imagem: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Executar testes
$successCount = 0
$totalCount = 0

foreach ($testCase in $testImages) {
    $totalCount++
    if (Test-Image $testCase) {
        $successCount++
    }
}

Write-Host ""
Write-Host "📊 RESULTADO FINAL:" -ForegroundColor Cyan
Write-Host "   Testes executados: $totalCount" -ForegroundColor White
Write-Host "   Sucessos: $successCount" -ForegroundColor Green
Write-Host "   Falhas: $($totalCount - $successCount)" -ForegroundColor $(if ($totalCount -eq $successCount) { "Green" } else { "Red" })
Write-Host ""

if ($totalCount -eq $successCount) {
    Write-Host "✅ TODOS OS TESTES PASSARAM!" -ForegroundColor Green
} else {
    Write-Host "⚠️  Alguns testes falharam ou apresentaram avisos" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== FIM DO TESTE ===" -ForegroundColor Cyan
