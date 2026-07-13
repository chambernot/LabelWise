#!/usr/bin/env pwsh

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  INICIANDO API LABELWISE COM LOGS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Limpar console
Clear-Host

Write-Host "`n[INFO] Verificando PostgreSQL..." -ForegroundColor Yellow
$pgContainer = docker ps --filter "name=postgres" --format "{{.Names}}" | Select-Object -First 1

if (-not $pgContainer) {
    Write-Host "[ERRO] PostgreSQL nao esta rodando!" -ForegroundColor Red
    Write-Host "Execute: docker compose up -d" -ForegroundColor Yellow
    exit 1
}

Write-Host "[OK] PostgreSQL esta rodando: $pgContainer" -ForegroundColor Green

# Verificar database
Write-Host "`n[INFO] Verificando database..." -ForegroundColor Yellow
$dbCheck = docker exec $pgContainer psql -U postgres -lqt 2>&1 | Select-String "labelwise_db"

if ($dbCheck) {
    Write-Host "[OK] Database labelwise_db existe" -ForegroundColor Green
} else {
    Write-Host "[INFO] Criando database labelwise_db..." -ForegroundColor Yellow
    docker exec $pgContainer psql -U postgres -c "CREATE DATABASE labelwise_db OWNER postgres;" 2>&1 | Out-Null
    Write-Host "[OK] Database criado" -ForegroundColor Green
}

# Compilar
Write-Host "`n[INFO] Compilando aplicacao..." -ForegroundColor Yellow
dotnet build LabelWise.Api\LabelWise.Api.csproj --configuration Debug 2>&1 | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Host "[OK] Compilacao bem-sucedida" -ForegroundColor Green
} else {
    Write-Host "[ERRO] Falha na compilacao" -ForegroundColor Red
    exit 1
}

# Criar diretório temporário manualmente
Write-Host "`n[INFO] Preparando diretorios..." -ForegroundColor Yellow
$tempPath = [System.IO.Path]::GetTempPath()
$labelwiseTempPath = Join-Path $tempPath "labelwise"

if (-not (Test-Path $labelwiseTempPath)) {
    New-Item -ItemType Directory -Path $labelwiseTempPath -Force | Out-Null
    Write-Host "[OK] Diretorio temporario criado: $labelwiseTempPath" -ForegroundColor Green
} else {
    Write-Host "[OK] Diretorio temporario ja existe: $labelwiseTempPath" -ForegroundColor Green
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  INICIANDO API..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Iniciar API
try {
    dotnet run --project LabelWise.Api
} catch {
    Write-Host "`n[ERRO] Aplicacao falhou ao iniciar" -ForegroundColor Red
    Write-Host "Erro: $_" -ForegroundColor Red
}
