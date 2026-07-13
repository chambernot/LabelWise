# ═══════════════════════════════════════════════════════════════════════════
# Script de Validação do OCR Provider Configuration
# ═══════════════════════════════════════════════════════════════════════════

Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🔍 OCR Provider Configuration Validator" -ForegroundColor White
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Verificar se a API está rodando
$apiUrl = "https://localhost:7319"
$diagnosticEndpoint = "$apiUrl/api/diagnostics/ocr-provider"

Write-Host "📋 Step 1: Checking if API is running..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$apiUrl/swagger/index.html" -Method Get -SkipCertificateCheck -ErrorAction Stop
    Write-Host "   ✅ API is running at $apiUrl" -ForegroundColor Green
}
catch {
    Write-Host "   ❌ API is NOT running" -ForegroundColor Red
    Write-Host "   💡 Start the API with: cd LabelWise.Api; dotnet run" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "📋 Step 2: Querying OCR Provider diagnostic endpoint..." -ForegroundColor Yellow

try {
    $providerInfo = Invoke-RestMethod -Uri $diagnosticEndpoint -Method Get -SkipCertificateCheck -ErrorAction Stop
    
    Write-Host "   ✅ Diagnostic endpoint responding" -ForegroundColor Green
    Write-Host ""
    
    # Parse response
    $providerName = $providerInfo.provider.name
    $providerType = $providerInfo.provider.typeName
    $isRealOcr = $providerInfo.provider.isRealOcr
    $isMock = $providerInfo.provider.isMock
    $message = $providerInfo.diagnostic.message
    
    Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "📊 OCR PROVIDER INFORMATION" -ForegroundColor White
    Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
    
    Write-Host "Provider Name:    $providerName" -ForegroundColor White
    Write-Host "Provider Type:    $providerType" -ForegroundColor White
    Write-Host "Is Real OCR:      $isRealOcr" -ForegroundColor $(if ($isRealOcr -eq $true) { "Green" } else { "Red" })
    Write-Host "Is Mock:          $isMock" -ForegroundColor $(if ($isMock -eq $false) { "Green" } else { "Yellow" })
    Write-Host ""
    Write-Host "Status: $message" -ForegroundColor $(if ($isRealOcr -eq $true) { "Green" } else { "Yellow" })
    Write-Host ""
    
    # Configuration details
    Write-Host "───────────────────────────────────────────────────────────────────────────" -ForegroundColor Cyan
    Write-Host "⚙️  CONFIGURATION" -ForegroundColor White
    Write-Host "───────────────────────────────────────────────────────────────────────────" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Configured Provider: $($providerInfo.configuration.configuredProvider)" -ForegroundColor White
    Write-Host "Tessdata Path:       $($providerInfo.configuration.tessdataPath)" -ForegroundColor White
    Write-Host "Language:            $($providerInfo.configuration.language)" -ForegroundColor White
    Write-Host ""
    
    # Assembly details
    Write-Host "───────────────────────────────────────────────────────────────────────────" -ForegroundColor Cyan
    Write-Host "📦 ASSEMBLY INFORMATION" -ForegroundColor White
    Write-Host "───────────────────────────────────────────────────────────────────────────" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Full Type:        $($providerInfo.provider.type)" -ForegroundColor White
    Write-Host "Assembly:         $($providerInfo.provider.assembly)" -ForegroundColor White
    Write-Host "Assembly Version: $($providerInfo.provider.assemblyVersion)" -ForegroundColor White
    Write-Host ""
    
    Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    
    # Validation summary
    Write-Host ""
    Write-Host "📋 VALIDATION SUMMARY" -ForegroundColor White
    Write-Host "───────────────────────────────────────────────────────────────────────────" -ForegroundColor Cyan
    
    $validationsPassed = 0
    $validationsFailed = 0
    
    # Check 1: Is using real OCR?
    if ($isRealOcr -eq $true) {
        Write-Host "   ✅ Using REAL OCR Provider" -ForegroundColor Green
        $validationsPassed++
    } else {
        Write-Host "   ❌ Using MOCK OCR Provider" -ForegroundColor Red
        Write-Host "      💡 Set 'OcrProvider:Provider' to 'Tesseract' in appsettings.json" -ForegroundColor Yellow
        $validationsFailed++
    }
    
    # Check 2: Is Tesseract?
    if ($providerType -eq "TesseractOcrProvider") {
        Write-Host "   ✅ Tesseract Provider Detected" -ForegroundColor Green
        $validationsPassed++
    } else {
        Write-Host "   ⚠️  Not using Tesseract (using: $providerType)" -ForegroundColor Yellow
    }
    
    # Check 3: Not Mock?
    if ($isMock -eq $false) {
        Write-Host "   ✅ NOT using Mock Provider" -ForegroundColor Green
        $validationsPassed++
    } else {
        Write-Host "   ❌ Currently using Mock Provider" -ForegroundColor Red
        $validationsFailed++
    }
    
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    
    if ($validationsFailed -eq 0) {
        Write-Host "✅ ALL VALIDATIONS PASSED" -ForegroundColor Green
        Write-Host "   Your OCR Provider is correctly configured!" -ForegroundColor Green
    } else {
        Write-Host "⚠️  SOME VALIDATIONS FAILED" -ForegroundColor Yellow
        Write-Host "   Check the configuration in appsettings.json" -ForegroundColor Yellow
    }
    
    Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
}
catch {
    Write-Host "   ❌ Failed to query diagnostic endpoint" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "   💡 Possible causes:" -ForegroundColor Yellow
    Write-Host "      - API not fully started yet" -ForegroundColor Yellow
    Write-Host "      - Endpoint not available" -ForegroundColor Yellow
    Write-Host "      - Certificate validation issue" -ForegroundColor Yellow
    exit 1
}

# Optional: Test with real image
Write-Host ""
Write-Host "📋 Step 3 (Optional): Test with sample image?" -ForegroundColor Yellow
Write-Host "   This will upload a test image and show metadata" -ForegroundColor White
Write-Host ""
$testWithImage = Read-Host "   Test with image? (Y/N)"

if ($testWithImage -eq "Y" -or $testWithImage -eq "y") {
    Write-Host ""
    Write-Host "   💡 Open Swagger UI and test POST /api/pipeline/analyze-image" -ForegroundColor Yellow
    Write-Host "   URL: $apiUrl/swagger" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "   Check the response metadata:" -ForegroundColor White
    Write-Host "   - metadata.ocrProviderName should be 'Tesseract OCR (Local)'" -ForegroundColor White
    Write-Host "   - metadata.ocrProviderVersion should be 'TesseractOcrProvider'" -ForegroundColor White
    Write-Host ""
    
    # Open Swagger
    Start-Process "$apiUrl/swagger"
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "✅ Validation Complete" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "📚 For more information, see:" -ForegroundColor White
Write-Host "   - OCR_PROVIDER_DIAGNOSTIC_FIX.md" -ForegroundColor Cyan
Write-Host "   - OCR_PROVIDERS_CONFIGURATION.md" -ForegroundColor Cyan
Write-Host ""
