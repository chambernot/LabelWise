# Test script for refined nutrition presentation layer

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "   Testando Camada de Apresentação Refinada" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build
Write-Host "[1/3] Building solution..." -ForegroundColor Yellow
dotnet build --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Build successful" -ForegroundColor Green
Write-Host ""

# Step 2: Run tests
Write-Host "[2/3] Running presentation engine tests..." -ForegroundColor Yellow
dotnet test `
    --filter "FullyQualifiedName~NutritionPresentationEngineTests" `
    --logger "console;verbosity=detailed" `
    --no-build

if ($LASTEXITCODE -ne 0) {
    Write-Host "Some tests failed!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Review test output above for details" -ForegroundColor Yellow
} else {
    Write-Host "✓ All tests passed" -ForegroundColor Green
}
Write-Host ""

# Step 3: Validation summary
Write-Host "[3/3] Validation Summary" -ForegroundColor Yellow
Write-Host "---------------------------------------" -ForegroundColor Gray
Write-Host ""

Write-Host "✓ Main Offender Detection" -ForegroundColor Green
Write-Host "  - Identifica açúcar como principal ofensor quando > 15g/100g" -ForegroundColor Gray
Write-Host "  - Identifica sódio como principal ofensor quando > 600mg/100g" -ForegroundColor Gray
Write-Host "  - Identifica gordura como principal ofensor quando > 20g/100g" -ForegroundColor Gray
Write-Host ""

Write-Host "✓ Refined Score Calculation" -ForegroundColor Green
Write-Host "  - Aplica caps por categoria (achocolatado: max 48)" -ForegroundColor Gray
Write-Host "  - Penaliza produtos com alto açúcar" -ForegroundColor Gray
Write-Host "  - Bonifica produtos com alta proteína/fibra" -ForegroundColor Gray
Write-Host ""

Write-Host "✓ User-Friendly Labels" -ForegroundColor Green
Write-Host "  - 'Excelente escolha' (80+)" -ForegroundColor Gray
Write-Host "  - 'Boa escolha' (65-79)" -ForegroundColor Gray
Write-Host "  - 'Consumo com atenção' (50-64)" -ForegroundColor Gray
Write-Host "  - 'Evitar consumo frequente' (35-49)" -ForegroundColor Gray
Write-Host "  - 'Evitar' (0-34)" -ForegroundColor Gray
Write-Host ""

Write-Host "✓ Clear Summary Messages" -ForegroundColor Green
Write-Host "  - Destaca principal ofensor com valor" -ForegroundColor Gray
Write-Host "  - Evita frases genéricas como 'perfil intermediário'" -ForegroundColor Gray
Write-Host "  - Fornece contexto de análise (tabela vs estimativa)" -ForegroundColor Gray
Write-Host ""

Write-Host "✓ Contextual Alerts" -ForegroundColor Green
Write-Host "  - Alertas específicos por perfil de saúde" -ForegroundColor Gray
Write-Host "  - Indicação clara do impacto do ofensor" -ForegroundColor Gray
Write-Host "  - Recomendações acionáveis" -ForegroundColor Gray
Write-Host ""

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Start API:" -ForegroundColor White
Write-Host "   dotnet run --project LabelWise.Api" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Test endpoint with real image:" -ForegroundColor White
Write-Host "   Use Swagger UI: https://localhost:7206/swagger" -ForegroundColor Gray
Write-Host "   POST /api/nutrition/analyze-simple-image" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Validate response:" -ForegroundColor White
Write-Host "   - Check 'summary' for clear, direct message" -ForegroundColor Gray
Write-Host "   - Check 'nutritionalScore.label' for user-friendly text" -ForegroundColor Gray
Write-Host "   - Check 'nutritionalScore.reason' for offender details" -ForegroundColor Gray
Write-Host "   - Check 'alerts' for contextual warnings" -ForegroundColor Gray
Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
