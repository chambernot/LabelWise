# Script para corrigir e reiniciar a API completamente

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  REINICIANDO API COMPLETAMENTE" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Passo 1: Parar TODOS os processos dotnet e LabelWise
Write-Host "[1/6] Parando todos os processos..." -ForegroundColor Yellow
Get-Process -Name "LabelWise.Api","LabelWise","dotnet" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
Write-Host "   ✓ Processos parados" -ForegroundColor Green
Write-Host ""

# Passo 2: Limpar builds anteriores
Write-Host "[2/6] Limpando builds anteriores..." -ForegroundColor Yellow
$solutionPath = Join-Path $PSScriptRoot "LabelWise.sln"
dotnet clean $solutionPath --verbosity quiet
Start-Sleep -Seconds 1
Write-Host "   ✓ Build limpo" -ForegroundColor Green
Write-Host ""

# Passo 3: Deletar pastas bin e obj
Write-Host "[3/6] Removendo bin e obj..." -ForegroundColor Yellow
Get-ChildItem -Path $PSScriptRoot -Include bin,obj -Recurse -Directory | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "   ✓ Pastas removidas" -ForegroundColor Green
Write-Host ""

# Passo 4: Rebuild completo
Write-Host "[4/6] Fazendo rebuild completo..." -ForegroundColor Yellow
dotnet build $solutionPath --no-incremental --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "   ✗ ERRO NO BUILD!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Execute manualmente para ver detalhes:" -ForegroundColor Yellow
    Write-Host "   dotnet build LabelWise.sln" -ForegroundColor Cyan
    exit 1
}
Write-Host "   ✓ Build completo com sucesso" -ForegroundColor Green
Write-Host ""

# Passo 5: Iniciar a API
Write-Host "[5/6] Iniciando a API..." -ForegroundColor Yellow
$apiPath = Join-Path $PSScriptRoot "LabelWise.Api"
Set-Location $apiPath

Write-Host ""
Write-Host "   Aguarde a mensagem 'Application started'..." -ForegroundColor Gray
Write-Host ""

# Iniciar em novo processo para não bloquear o script
$process = Start-Process -FilePath "dotnet" -ArgumentList "run" -PassThru -NoNewWindow

# Aguardar a API iniciar
Start-Sleep -Seconds 10

# Passo 6: Detectar porta e abrir Swagger
Write-Host "[6/6] Detectando porta e abrindo Swagger..." -ForegroundColor Yellow

$ports = @(7319, 7001, 7018, 5001, 5000, 5319)
$swaggerUrl = $null

foreach ($port in $ports) {
    try {
        $testUrl = "https://localhost:$port"
        $null = [System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
        $request = [System.Net.WebRequest]::Create($testUrl)
        $request.Timeout = 1000
        $response = $request.GetResponse()
        $response.Close()
        $swaggerUrl = "$testUrl/swagger"
        break
    }
    catch {
        # Tentar próxima porta
    }
}

if ($swaggerUrl) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  ✅ API INICIADA COM SUCESSO!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Swagger UI: $swaggerUrl" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Abrindo navegador..." -ForegroundColor Yellow
    Start-Sleep -Seconds 2
    Start-Process $swaggerUrl
    Write-Host ""
    Write-Host "Se o Swagger ainda mostrar erro 500:" -ForegroundColor Yellow
    Write-Host "  1. Aguarde mais 5 segundos" -ForegroundColor Gray
    Write-Host "  2. Pressione F5 no navegador" -ForegroundColor Gray
    Write-Host "  3. Verifique os logs acima" -ForegroundColor Gray
}
else {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "  ⚠️ NÃO FOI POSSÍVEL DETECTAR A PORTA" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Verifique os logs acima para encontrar a linha:" -ForegroundColor Gray
    Write-Host "  'Now listening on: https://localhost:XXXX'" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Depois acesse: https://localhost:XXXX/swagger" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Pressione Ctrl+C para parar a API" -ForegroundColor Gray
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Aguardar processo
Wait-Process -Id $process.Id
