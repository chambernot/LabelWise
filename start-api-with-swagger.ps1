# Script para iniciar a API e abrir o Swagger automaticamente

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  LabelWise API - Quick Start" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Parar processos anteriores
Write-Host "[1/4] Parando processos anteriores..." -ForegroundColor Yellow
Get-Process -Name "LabelWise.Api" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# Navegar para o diretório da API
Write-Host "[2/4] Navegando para o diretório da API..." -ForegroundColor Yellow
$apiPath = Join-Path $PSScriptRoot "LabelWise.Api"
Set-Location $apiPath

# Iniciar a API
Write-Host "[3/4] Iniciando a API..." -ForegroundColor Yellow
Write-Host ""
Write-Host "A API está iniciando. Aguarde até ver 'Application started'..." -ForegroundColor Gray
Write-Host ""

$process = Start-Process -FilePath "dotnet" -ArgumentList "run" -PassThru -NoNewWindow

# Aguardar a API iniciar
Start-Sleep -Seconds 8

# Tentar descobrir a porta
Write-Host "[4/4] Tentando abrir o Swagger..." -ForegroundColor Yellow

$ports = @(7001, 7319, 7018, 5001, 5000)
$swaggerOpened = $false

foreach ($port in $ports) {
    try {
        $url = "https://localhost:$port/swagger"
        $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2 -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            Write-Host ""
            Write-Host "========================================" -ForegroundColor Green
            Write-Host "  ✅ API INICIADA COM SUCESSO!" -ForegroundColor Green
            Write-Host "========================================" -ForegroundColor Green
            Write-Host ""
            Write-Host "Swagger UI: $url" -ForegroundColor Cyan
            Write-Host ""
            Write-Host "Abrindo navegador..." -ForegroundColor Yellow
            Start-Process $url
            $swaggerOpened = $true
            break
        }
    }
    catch {
        # Tentar próxima porta
    }
}

if (-not $swaggerOpened) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "  ⚠️ API INICIADA (porta desconhecida)" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Verifique os logs acima para encontrar a porta." -ForegroundColor Gray
    Write-Host "Procure por uma linha como:" -ForegroundColor Gray
    Write-Host "  'Now listening on: https://localhost:XXXX'" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Depois acesse: https://localhost:XXXX/swagger" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Pressione Ctrl+C para parar a API" -ForegroundColor Gray
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Manter o script rodando
Wait-Process -Id $process.Id
