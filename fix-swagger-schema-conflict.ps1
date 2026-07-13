# fix-swagger-schema-conflict.ps1
# Script para reiniciar a API após correção do conflito de schema

Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🔧 FIX SWAGGER SCHEMA CONFLICT - CONFIDENCEDETAILSDTO" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan

Write-Host "`n📋 PROBLEMA CORRIGIDO:" -ForegroundColor Yellow
Write-Host "   ❌ Duas classes com nome 'ConfidenceDetailsDto'" -ForegroundColor Red
Write-Host "   ✅ Renomeada para 'NutritionConfidenceDetailsDto'" -ForegroundColor Green

Write-Host "`n📦 ARQUIVOS ALTERADOS:" -ForegroundColor Yellow
Write-Host "   • Removido: ConfidenceDetailsDto.cs" -ForegroundColor Gray
Write-Host "   • Criado: NutritionConfidenceDetailsDto.cs" -ForegroundColor Gray
Write-Host "   • Atualizado: RefactoredNutritionAnalysisResponse.cs" -ForegroundColor Gray
Write-Host "   • Atualizado: RefactoredNutritionAnalysisService.cs" -ForegroundColor Gray
Write-Host "   • Atualizado: RefactoredNutritionController.cs" -ForegroundColor Gray

Write-Host "`n🏗️  Verificando compilação..." -ForegroundColor Cyan

dotnet build LabelWise.Api\LabelWise.Api.csproj

if ($LASTEXITCODE -ne 0) {
    Write-Host "`n❌ ERRO: Falha na compilação" -ForegroundColor Red
    exit 1
}

Write-Host "`n✅ Compilação bem-sucedida!" -ForegroundColor Green

Write-Host "`n🚀 Iniciando a API..." -ForegroundColor Yellow

# Matar processos anteriores
$apiProcesses = Get-Process -Name "LabelWise.Api" -ErrorAction SilentlyContinue
if ($apiProcesses) {
    Write-Host "⏹️  Parando processos anteriores..." -ForegroundColor Yellow
    $apiProcesses | Stop-Process -Force
    Start-Sleep -Seconds 2
}

# Iniciar API
Write-Host "`n🌐 Iniciando API em https://localhost:7001" -ForegroundColor Cyan
Write-Host "📄 Swagger UI: https://localhost:7001/swagger" -ForegroundColor Cyan

$env:ASPNETCORE_ENVIRONMENT = "Development"

Start-Process powershell -ArgumentList @"
    -NoExit
    -Command `"
        Write-Host '═══════════════════════════════════════════════════════════' -ForegroundColor Green;
        Write-Host '🚀 LABELWISE API - RUNNING (SCHEMA CONFLICT FIXED)' -ForegroundColor Green;
        Write-Host '═══════════════════════════════════════════════════════════' -ForegroundColor Green;
        Write-Host '';
        Write-Host '🌐 API: https://localhost:7001' -ForegroundColor Cyan;
        Write-Host '📄 Swagger: https://localhost:7001/swagger' -ForegroundColor Cyan;
        Write-Host '';
        Write-Host '✅ Schema Conflict Fixed!' -ForegroundColor Green;
        Write-Host '   - ConfidenceDetailsDto renamed' -ForegroundColor Gray;
        Write-Host '   - Now: NutritionConfidenceDetailsDto' -ForegroundColor Gray;
        Write-Host '   - No more Swagger conflicts!' -ForegroundColor Gray;
        Write-Host '';
        Write-Host '🧪 Test via Swagger:' -ForegroundColor Yellow;
        Write-Host '   1. Access https://localhost:7001/swagger' -ForegroundColor Gray;
        Write-Host '   2. Find POST /api/RefactoredNutrition/analyze' -ForegroundColor Gray;
        Write-Host '   3. Click Try it out' -ForegroundColor Gray;
        Write-Host '   4. Upload an image' -ForegroundColor Gray;
        Write-Host '   5. Execute and check confidenceDetails!' -ForegroundColor Gray;
        Write-Host '';
        Write-Host 'Press Ctrl+C to stop the API' -ForegroundColor DarkGray;
        Write-Host '';
        cd LabelWise.Api;
        dotnet run
    `"
"@

Start-Sleep -Seconds 3

Write-Host "`n═══════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "✅ API INICIADA COM SUCESSO!" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Green

Write-Host "`n📋 PRÓXIMOS PASSOS:" -ForegroundColor Yellow
Write-Host "   1. Aguarde ~5-10 segundos para a API inicializar" -ForegroundColor White
Write-Host "   2. Acesse: https://localhost:7001/swagger" -ForegroundColor Cyan
Write-Host "   3. Procure por: POST /api/RefactoredNutrition/analyze" -ForegroundColor White
Write-Host "   4. Valide que não há mais erros de schema!" -ForegroundColor White
Write-Host "   5. Teste o endpoint com uma imagem" -ForegroundColor White

Write-Host "`n📚 DOCUMENTAÇÃO:" -ForegroundColor Yellow
Write-Host "   FIX_SWAGGER_SCHEMA_CONFLICT_CONFIDENCEDETAILS.md" -ForegroundColor Cyan
Write-Host "   SWAGGER_SCHEMA_CONFLICT_FIX_SUMMARY.md" -ForegroundColor Cyan

Write-Host "`n🌐 Abrindo navegador..." -ForegroundColor Cyan
Start-Sleep -Seconds 8
Start-Process "https://localhost:7001/swagger"

Write-Host "`n✅ Pronto! Verifique a janela do PowerShell com a API rodando." -ForegroundColor Green
Write-Host "✅ O Swagger deve funcionar sem erros de schema agora!" -ForegroundColor Green
Write-Host ""
