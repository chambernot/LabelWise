# Script para testar a lógica corrigida de detecção de tabela nutricional real vs fallback

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "TESTE: Lógica de Detecção de Tabela Nutricional Real" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# Configuração
$apiUrl = "http://localhost:5153"
$endpoint = "$apiUrl/api/nutrition/analyze"

# Função auxiliar para criar boundary único
function Get-UniqueBoundary {
    return "----WebKitFormBoundary$(Get-Random -Minimum 1000000000 -Maximum 9999999999)"
}

# Função auxiliar para fazer request multipart
function Invoke-NutritionAnalysis {
    param(
        [string]$ImagePath,
        [string]$TestName
    )
    
    Write-Host "─────────────────────────────────────────────────────" -ForegroundColor Yellow
    Write-Host "Teste: $TestName" -ForegroundColor Yellow
    Write-Host "Imagem: $ImagePath" -ForegroundColor Gray
    Write-Host ""
    
    if (-not (Test-Path $ImagePath)) {
        Write-Host "❌ Imagem não encontrada: $ImagePath" -ForegroundColor Red
        return
    }
    
    try {
        $boundary = Get-UniqueBoundary
        $imageBytes = [System.IO.File]::ReadAllBytes($ImagePath)
        $imageName = [System.IO.Path]::GetFileName($ImagePath)
        
        # Construir corpo multipart
        $bodyLines = @(
            "--$boundary",
            "Content-Disposition: form-data; name=`"image`"; filename=`"$imageName`"",
            "Content-Type: image/jpeg",
            "",
            [System.Text.Encoding]::GetEncoding("iso-8859-1").GetString($imageBytes),
            "--$boundary--"
        )
        
        $body = $bodyLines -join "`r`n"
        
        $headers = @{
            "Content-Type" = "multipart/form-data; boundary=$boundary"
        }
        
        Write-Host "⏳ Enviando requisição..." -ForegroundColor Gray
        
        $response = Invoke-RestMethod -Uri $endpoint -Method Post -Headers $headers -Body $body -TimeoutSec 120
        
        Write-Host "✅ Resposta recebida!" -ForegroundColor Green
        Write-Host ""
        
        # Análise da resposta
        Write-Host "📊 ANÁLISE DA RESPOSTA:" -ForegroundColor Cyan
        Write-Host "  • Produto: $($response.productName)" -ForegroundColor White
        Write-Host "  • Categoria: $($response.category)" -ForegroundColor White
        Write-Host "  • Peso: $($response.packageWeight)" -ForegroundColor White
        Write-Host ""
        
        # PONTO CRÍTICO 1: Analysis Mode
        $analysisMode = $response.analysisMode
        $analysisModeColor = if ($analysisMode -eq "FullNutritionLabel") { "Green" } else { "Yellow" }
        Write-Host "  🔍 Analysis Mode: " -NoNewline -ForegroundColor White
        Write-Host $analysisMode -ForegroundColor $analysisModeColor
        
        if ($analysisMode -eq "FrontOfPackageOnly") {
            Write-Host "     ⚠️  Usando fallback genérico" -ForegroundColor Yellow
        } else {
            Write-Host "     ✓  Usando leitura da tabela nutricional" -ForegroundColor Green
        }
        Write-Host ""
        
        # PONTO CRÍTICO 2: Nutrition Profile
        Write-Host "  📋 Perfil Nutricional:" -ForegroundColor Cyan
        $profile = $response.estimatedNutritionProfile
        
        $fieldsExtracted = 0
        if ($profile.caloriesPer100g -ne $null) {
            Write-Host "     • Calorias: $($profile.caloriesPer100g) kcal/100g" -ForegroundColor White
            $fieldsExtracted++
        }
        if ($profile.estimatedProteinPer100g -ne $null) {
            Write-Host "     • Proteínas: $($profile.estimatedProteinPer100g) g/100g" -ForegroundColor White
            $fieldsExtracted++
        }
        if ($profile.estimatedFatPer100g -ne $null) {
            Write-Host "     • Gorduras: $($profile.estimatedFatPer100g) g/100g" -ForegroundColor White
            $fieldsExtracted++
        }
        if ($profile.estimatedSugarPer100g -ne $null) {
            Write-Host "     • Açúcares: $($profile.estimatedSugarPer100g) g/100g" -ForegroundColor White
            $fieldsExtracted++
        }
        if ($profile.estimatedSodiumPer100g -ne $null) {
            Write-Host "     • Sódio: $($profile.estimatedSodiumPer100g) mg/100g" -ForegroundColor White
            $fieldsExtracted++
        }
        if ($profile.estimatedFiberPer100g -ne $null) {
            Write-Host "     • Fibras: $($profile.estimatedFiberPer100g) g/100g" -ForegroundColor White
            $fieldsExtracted++
        }
        
        Write-Host ""
        Write-Host "     ℹ️  $fieldsExtracted campos nutricionais extraídos" -ForegroundColor Cyan
        Write-Host "     Basis: $($profile.basis)" -ForegroundColor Gray
        Write-Host ""
        
        # PONTO CRÍTICO 3: Classification
        Write-Host "  🏥 Classificação:" -ForegroundColor Cyan
        $classification = $response.classification
        
        $indeterminadoCount = 0
        
        function Show-ClassificationStatus {
            param($name, $result)
            $statusColor = switch ($result.status) {
                "adequado" { "Green" }
                "consumo_moderado" { "Yellow" }
                "nao_recomendado" { "Red" }
                "fraco" { "Magenta" }
                "indeterminado" { 
                    $script:indeterminadoCount++
                    "DarkGray" 
                }
                default { "White" }
            }
            Write-Host "     • $name" -NoNewline -ForegroundColor White
            Write-Host " [$($result.status)]" -ForegroundColor $statusColor
            Write-Host "       └─ $($result.reason)" -ForegroundColor Gray
        }
        
        Show-ClassificationStatus "Diabéticos" $classification.diabetic
        Show-ClassificationStatus "Hipertensos" $classification.bloodPressure
        Show-ClassificationStatus "Emagrecimento" $classification.weightLoss
        Show-ClassificationStatus "Ganho Muscular" $classification.muscleGain
        
        Write-Host ""
        if ($indeterminadoCount -gt 0) {
            Write-Host "     ⚠️  $indeterminadoCount classificações ficaram indeterminadas" -ForegroundColor Yellow
        } else {
            Write-Host "     ✓  Todas as classificações foram determinadas" -ForegroundColor Green
        }
        Write-Host ""
        
        # PONTO CRÍTICO 4: Summary
        Write-Host "  📝 Summary:" -ForegroundColor Cyan
        Write-Host "     $($response.summary)" -ForegroundColor White
        Write-Host ""
        
        # Warnings
        if ($response.warnings -and $response.warnings.Count -gt 0) {
            Write-Host "  ⚠️  Warnings:" -ForegroundColor Yellow
            foreach ($warning in $response.warnings) {
                Write-Host "     • $warning" -ForegroundColor Yellow
            }
            Write-Host ""
        }
        
        # Confidence Details
        Write-Host "  📈 Confidence Details:" -ForegroundColor Cyan
        $confidence = $response.confidenceDetails
        Write-Host "     • Product ID: $($confidence.productIdentification)" -ForegroundColor White
        Write-Host "     • Claims: $($confidence.visibleClaimsExtraction)" -ForegroundColor White
        Write-Host "     • Nutrition: $($confidence.estimatedNutritionProfile)" -ForegroundColor White
        Write-Host "     • Classification: $($confidence.classification)" -ForegroundColor White
        Write-Host ""
        
        # VALIDAÇÃO
        Write-Host "  ✔️  VALIDAÇÃO:" -ForegroundColor Green
        $issues = @()
        
        # Validar coerência entre analysisMode e dados
        if ($analysisMode -eq "FrontOfPackageOnly" -and $fieldsExtracted -ge 3) {
            $issues += "analysisMode é FrontOfPackageOnly mas há $fieldsExtracted campos extraídos (deveria ser FullNutritionLabel)"
        }
        
        if ($analysisMode -eq "FullNutritionLabel" -and $fieldsExtracted -lt 2) {
            $issues += "analysisMode é FullNutritionLabel mas apenas $fieldsExtracted campos foram extraídos"
        }
        
        # Validar classificações indeterminadas com dados disponíveis
        if ($indeterminadoCount -gt 0 -and $fieldsExtracted -ge 3) {
            $issues += "$indeterminadoCount classificações ficaram indeterminadas apesar de haver $fieldsExtracted campos nutricionais"
        }
        
        if ($issues.Count -eq 0) {
            Write-Host "     ✅ Nenhuma inconsistência detectada" -ForegroundColor Green
        } else {
            Write-Host "     ❌ Inconsistências encontradas:" -ForegroundColor Red
            foreach ($issue in $issues) {
                Write-Host "        • $issue" -ForegroundColor Red
            }
        }
        
        Write-Host ""
        Write-Host "─────────────────────────────────────────────────────" -ForegroundColor Yellow
        Write-Host ""
        
    } catch {
        Write-Host "❌ Erro durante o teste: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ""
    }
}

# ========================================
# CASOS DE TESTE
# ========================================

Write-Host "Por favor, forneça os caminhos das imagens para teste:" -ForegroundColor Cyan
Write-Host ""

# Teste 1: Imagem com tabela nutricional legível
Write-Host "1. Imagem com tabela nutricional LEGÍVEL (ex: Danoninho, yogurt, etc.):" -ForegroundColor Yellow
$imagemComTabela = Read-Host "   Caminho"

if ($imagemComTabela -and (Test-Path $imagemComTabela)) {
    Invoke-NutritionAnalysis -ImagePath $imagemComTabela -TestName "TABELA NUTRICIONAL LEGÍVEL"
}

# Teste 2: Imagem sem tabela nutricional (apenas frente)
Write-Host ""
Write-Host "2. Imagem SEM tabela nutricional (apenas frente da embalagem):" -ForegroundColor Yellow
$imagemSemTabela = Read-Host "   Caminho (Enter para pular)"

if ($imagemSemTabela -and (Test-Path $imagemSemTabela)) {
    Invoke-NutritionAnalysis -ImagePath $imagemSemTabela -TestName "FRENTE DA EMBALAGEM (SEM TABELA)"
}

# Teste 3: Imagem com tabela parcialmente legível
Write-Host ""
Write-Host "3. Imagem com tabela PARCIALMENTE legível:" -ForegroundColor Yellow
$imagemParcial = Read-Host "   Caminho (Enter para pular)"

if ($imagemParcial -and (Test-Path $imagemParcial)) {
    Invoke-NutritionAnalysis -ImagePath $imagemParcial -TestName "TABELA PARCIALMENTE LEGÍVEL"
}

Write-Host ""
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "TESTE CONCLUÍDO" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Resumo das melhorias implementadas:" -ForegroundColor Green
Write-Host "  ✓ Detecção aprimorada de tabela nutricional legível" -ForegroundColor Green
Write-Host "  ✓ Suporte a leitura parcial real (valores extraídos + campos null)" -ForegroundColor Green
Write-Host "  ✓ Sanitizer menos agressivo com dados reais da tabela" -ForegroundColor Green
Write-Host "  ✓ Classificações básicas geradas quando há dados suficientes" -ForegroundColor Green
Write-Host "  ✓ Summary reflete dados reais extraídos" -ForegroundColor Green
Write-Host "  ✓ Correção automática de analysisMode baseado em dados reais" -ForegroundColor Green
Write-Host ""
