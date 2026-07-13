# ═══════════════════════════════════════════════════════════════════════════
# TESTAR CORREÇÃO JWT - DEV ENDPOINT
# ═══════════════════════════════════════════════════════════════════════════

param(
    [string]$Token = "",
    [string]$ImagePath = "creatina-creapure-200g-darkness-2788690.webp",
    [string]$ApiUrl = "https://localhost:7319"
)

Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🧪 TESTANDO CORREÇÃO JWT - DEV ENDPOINT" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# 1. Verificar se imagem existe
if (-not (Test-Path $ImagePath)) {
    Write-Host "❌ Imagem não encontrada: $ImagePath" -ForegroundColor Red
    Write-Host ""
    Write-Host "Por favor, forneça o caminho correto:" -ForegroundColor Yellow
    Write-Host "  -ImagePath 'C:\path\to\image.jpg'" -ForegroundColor Gray
    exit 1
}

Write-Host "📷 Imagem encontrada: $ImagePath" -ForegroundColor Green
$imageSize = (Get-Item $ImagePath).Length / 1KB
Write-Host "   Tamanho: $([math]::Round($imageSize, 2)) KB" -ForegroundColor Gray
Write-Host ""

# 2. Obter ou criar token
if ([string]::IsNullOrEmpty($Token)) {
    Write-Host "🔑 Token não fornecido. Tentando fazer login..." -ForegroundColor Yellow
    Write-Host ""
    
    try {
        $loginBody = @{
            email = "user@example.com"
            password = "Password123!"
        } | ConvertTo-Json

        $loginUrl = "$ApiUrl/api/auth/login"
        Write-Host "   POST $loginUrl" -ForegroundColor Gray
        
        $loginResponse = Invoke-RestMethod -Uri $loginUrl `
            -Method Post `
            -ContentType "application/json" `
            -Body $loginBody `
            -SkipCertificateCheck

        $Token = $loginResponse.token
        Write-Host "   ✅ Login successful" -ForegroundColor Green
        Write-Host "   Token: $($Token.Substring(0, 50))..." -ForegroundColor Gray
        Write-Host ""
    }
    catch {
        Write-Host "   ❌ Login falhou: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ""
        Write-Host "   Tente fornecer o token manualmente:" -ForegroundColor Yellow
        Write-Host "   -Token 'seu-token-jwt'" -ForegroundColor Gray
        exit 1
    }
}

# 3. Testar endpoint de health primeiro
Write-Host "🏥 Testando health check do dev endpoint..." -ForegroundColor Cyan
Write-Host ""

try {
    $healthUrl = "$ApiUrl/api/dev/full-guided-analysis-test/health"
    Write-Host "   GET $healthUrl" -ForegroundColor Gray
    
    $healthResponse = Invoke-RestMethod -Uri $healthUrl `
        -Method Get `
        -SkipCertificateCheck

    Write-Host "   ✅ Health check OK" -ForegroundColor Green
    Write-Host "   Status: $($healthResponse.status)" -ForegroundColor Gray
    Write-Host "   Environment: $($healthResponse.environment)" -ForegroundColor Gray
    Write-Host ""
}
catch {
    Write-Host "   ⚠️  Health check failed: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host ""
}

# 4. Testar endpoint principal
Write-Host "🚀 Testando POST com imagem..." -ForegroundColor Cyan
Write-Host ""

$endpoint = "$ApiUrl/api/dev/full-guided-analysis-test"
Write-Host "   POST $endpoint" -ForegroundColor Gray
Write-Host "   Authorization: Bearer $($Token.Substring(0, 30))..." -ForegroundColor Gray
Write-Host ""

try {
    # Criar form data
    $form = @{
        FrontImage = Get-Item -Path $ImagePath
        LanguageCode = "pt-BR"
        DeviceInfo = "PowerShell Test Script"
    }

    Write-Host "   📤 Enviando request..." -ForegroundColor Yellow
    
    $startTime = Get-Date
    
    $response = Invoke-RestMethod -Uri $endpoint `
        -Method Post `
        -Headers @{ Authorization = "Bearer $Token" } `
        -Form $form `
        -SkipCertificateCheck
    
    $duration = (Get-Date) - $startTime

    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host "✅ SUCCESS!" -ForegroundColor Green
    Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host ""
    Write-Host "⏱️  Duração: $([math]::Round($duration.TotalSeconds, 2)) segundos" -ForegroundColor Cyan
    Write-Host ""

    # Mostrar resultado
    Write-Host "📊 RESULTADO:" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "   SessionId: $($response.sessionId)" -ForegroundColor Gray
    Write-Host "   Success: $($response.success)" -ForegroundColor $(if ($response.success) { "Green" } else { "Red" })
    Write-Host "   ProcessedAt: $($response.processedAt)" -ForegroundColor Gray
    Write-Host "   TotalDuration: $($response.totalDuration)" -ForegroundColor Gray
    Write-Host ""
    
    if ($response.processedSteps -and $response.processedSteps.Count -gt 0) {
        Write-Host "   📋 Etapas Processadas:" -ForegroundColor Cyan
        foreach ($step in $response.processedSteps) {
            $statusIcon = if ($step.success) { "✅" } else { "❌" }
            Write-Host "      $statusIcon $($step.stepName) - $($step.duration)" -ForegroundColor Gray
        }
        Write-Host ""
    }

    if ($response.warnings -and $response.warnings.Count -gt 0) {
        Write-Host "   ⚠️  Warnings ($($response.warnings.Count)):" -ForegroundColor Yellow
        foreach ($warning in $response.warnings) {
            Write-Host "      - $warning" -ForegroundColor Gray
        }
        Write-Host ""
    }

    if ($response.errors -and $response.errors.Count -gt 0) {
        Write-Host "   ❌ Errors ($($response.errors.Count)):" -ForegroundColor Red
        foreach ($error in $response.errors) {
            Write-Host "      - $error" -ForegroundColor Gray
        }
        Write-Host ""
    }

    # Salvar response completo
    $outputFile = "dev-endpoint-response-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
    $response | ConvertTo-Json -Depth 10 | Out-File $outputFile -Encoding UTF8
    Write-Host "   💾 Response completo salvo em: $outputFile" -ForegroundColor Green
    Write-Host ""

    Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
}
catch {
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Red
    Write-Host "❌ ERRO!" -ForegroundColor Red
    Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Red
    Write-Host ""
    
    $statusCode = $_.Exception.Response.StatusCode.value__
    Write-Host "   Status Code: $statusCode" -ForegroundColor Red
    Write-Host "   Mensagem: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    
    if ($statusCode -eq 401) {
        Write-Host "   🔍 DIAGNÓSTICO:" -ForegroundColor Yellow
        Write-Host "   - Erro 401 Unauthorized" -ForegroundColor Gray
        Write-Host "   - Token pode estar expirado ou inválido" -ForegroundColor Gray
        Write-Host "   - Ou o problema do JWT ainda persiste" -ForegroundColor Gray
        Write-Host ""
        Write-Host "   💡 SOLUÇÕES:" -ForegroundColor Cyan
        Write-Host "   1. Verifique se a API foi reiniciada após a correção" -ForegroundColor Gray
        Write-Host "   2. Faça login novamente para obter novo token" -ForegroundColor Gray
        Write-Host "   3. Verifique os logs da API para mais detalhes" -ForegroundColor Gray
    }
    
    Write-Host ""
    Write-Host "   Stack Trace:" -ForegroundColor Gray
    Write-Host "   $($_.Exception.StackTrace)" -ForegroundColor DarkGray
    Write-Host ""
    
    exit 1
}

Write-Host "✅ Teste concluído!" -ForegroundColor Green
Write-Host ""
