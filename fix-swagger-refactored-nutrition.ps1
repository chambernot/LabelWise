# fix-swagger-refactored-nutrition.ps1
# Script para aplicar a correção do Swagger e reiniciar a API

Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🔧 FIX SWAGGER - REFACTORED NUTRITION CONTROLLER" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan

# Verificar se estamos na raiz do projeto
if (-not (Test-Path "LabelWise.Api")) {
    Write-Host "`n❌ ERRO: Execute este script da raiz do projeto LabelWise" -ForegroundColor Red
    exit 1
}

Write-Host "`n📋 Verificando arquivos..." -ForegroundColor Yellow

# Verificar FormModel
$formModelPath = "LabelWise.Api\Models\RefactoredNutritionAnalysisFormModel.cs"
if (Test-Path $formModelPath) {
    Write-Host "✅ FormModel criado: $formModelPath" -ForegroundColor Green
} else {
    Write-Host "❌ FormModel não encontrado: $formModelPath" -ForegroundColor Red
    exit 1
}

# Verificar Controller
$controllerPath = "LabelWise.Api\Controllers\RefactoredNutritionController.cs"
if (Test-Path $controllerPath) {
    Write-Host "✅ Controller encontrado: $controllerPath" -ForegroundColor Green
} else {
    Write-Host "❌ Controller não encontrado: $controllerPath" -ForegroundColor Red
    exit 1
}

Write-Host "`n🏗️  Compilando o projeto..." -ForegroundColor Yellow

# Build
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
        Write-Host '🚀 LABELWISE API - RUNNING' -ForegroundColor Green;
        Write-Host '═══════════════════════════════════════════════════════════' -ForegroundColor Green;
        Write-Host '';
        Write-Host '🌐 API: https://localhost:7001' -ForegroundColor Cyan;
        Write-Host '📄 Swagger: https://localhost:7001/swagger' -ForegroundColor Cyan;
        Write-Host '';
        Write-Host '✅ Swagger Fix Applied!' -ForegroundColor Green;
        Write-Host '   - FormModel created' -ForegroundColor Gray;
        Write-Host '   - Controller updated' -ForegroundColor Gray;
        Write-Host '   - Endpoint: POST /api/RefactoredNutrition/analyze' -ForegroundColor Gray;
        Write-Host '';
        Write-Host '🧪 Test via Swagger:' -ForegroundColor Yellow;
        Write-Host '   1. Access https://localhost:7001/swagger' -ForegroundColor Gray;
        Write-Host '   2. Find POST /api/RefactoredNutrition/analyze' -ForegroundColor Gray;
        Write-Host '   3. Click Try it out' -ForegroundColor Gray;
        Write-Host '   4. Upload an image' -ForegroundColor Gray;
        Write-Host '   5. Execute' -ForegroundColor Gray;
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
Write-Host "   4. Clique em 'Try it out'" -ForegroundColor White
Write-Host "   5. Faça upload de uma imagem de produto" -ForegroundColor White
Write-Host "   6. Clique em 'Execute'" -ForegroundColor White

Write-Host "`n📚 DOCUMENTAÇÃO:" -ForegroundColor Yellow
Write-Host "   FIX_SWAGGER_REFACTORED_NUTRITION.md" -ForegroundColor Cyan

Write-Host "`n🌐 Abrindo navegador..." -ForegroundColor Cyan
Start-Sleep -Seconds 8
Start-Process "https://localhost:7001/swagger"

Write-Host "`n✅ Pronto! Verifique a janela do PowerShell com a API rodando." -ForegroundColor Green
Write-Host ""
