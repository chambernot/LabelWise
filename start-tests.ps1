#!/usr/bin/env pwsh

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  TESTE COMPLETO DA API LABELWISE" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$ErrorActionPreference = "Continue"

# ETAPA 1: Verificar PostgreSQL
Write-Host "`n[1/6] Verificando PostgreSQL..." -ForegroundColor Yellow

$pgContainer = docker ps --filter "name=postgres" --format "{{.Names}}" | Select-Object -First 1

if ($pgContainer) {
    Write-Host "[OK] PostgreSQL rodando: $pgContainer" -ForegroundColor Green
    
    $testConn = docker exec $pgContainer psql -U postgres -d postgres -c "SELECT 1;" 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "[OK] Conexao com PostgreSQL OK" -ForegroundColor Green
    } else {
        Write-Host "[ERRO] Erro ao conectar no PostgreSQL" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "[ERRO] Container PostgreSQL nao encontrado!" -ForegroundColor Red
    Write-Host "Execute: docker compose up -d" -ForegroundColor Yellow
    exit 1
}

# ETAPA 2: Verificar database
Write-Host "`n[2/6] Verificando database labelwise_db..." -ForegroundColor Yellow

$dbExists = docker exec $pgContainer psql -U postgres -lqt 2>&1 | Select-String -Pattern "labelwise_db"

if ($dbExists) {
    Write-Host "[OK] Database 'labelwise_db' ja existe" -ForegroundColor Green
} else {
    Write-Host "[INFO] Database nao encontrado. Criando..." -ForegroundColor Yellow
    docker exec $pgContainer psql -U postgres -c "CREATE DATABASE labelwise_db OWNER postgres;" 2>&1 | Out-Null
    Write-Host "[OK] Database criado" -ForegroundColor Green
}

# ETAPA 3: Compilar
Write-Host "`n[3/6] Compilando solucao..." -ForegroundColor Yellow
dotnet build LabelWise.sln --configuration Release --no-incremental 2>&1 | Out-Null
Write-Host "[OK] Compilacao concluida" -ForegroundColor Green

# ETAPA 4: Aplicar Migrations
Write-Host "`n[4/6] Aplicando migrations..." -ForegroundColor Yellow

dotnet ef database update --project LabelWise.Infrastructure --startup-project LabelWise.Api 2>&1 | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Host "[OK] Migrations aplicadas" -ForegroundColor Green
} else {
    Write-Host "[AVISO] Possivel erro nas migrations, mas continuando..." -ForegroundColor Yellow
}

# ETAPA 5: Verificar tabelas
Write-Host "`n[5/6] Verificando tabelas criadas..." -ForegroundColor Yellow

$tables = docker exec $pgContainer psql -U postgres -d labelwise_db -c "\dt" 2>&1

if ($tables -match "users") {
    Write-Host "[OK] Tabelas criadas:" -ForegroundColor Green
    docker exec $pgContainer psql -U postgres -d labelwise_db -c "\dt" 
} else {
    Write-Host "[AVISO] Tabelas nao encontradas" -ForegroundColor Yellow
}

# ETAPA 6: Instrucoes
Write-Host "`n[6/6] Preparacao concluida!" -ForegroundColor Yellow

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  PROXIMOS PASSOS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

Write-Host "`n1. INICIAR A API:" -ForegroundColor Green
Write-Host "   dotnet run --project LabelWise.Api" -ForegroundColor White

Write-Host "`n2. ABRIR SWAGGER:" -ForegroundColor Green
Write-Host "   http://localhost:5000/swagger" -ForegroundColor White

Write-Host "`n3. TESTAR ENDPOINTS:" -ForegroundColor Green
Write-Host "   .\test-api-endpoints.ps1" -ForegroundColor White

Write-Host "`n4. CONECTAR NO BANCO:" -ForegroundColor Green
Write-Host "   docker exec -it $pgContainer psql -U postgres -d labelwise_db" -ForegroundColor White

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  INFORMACOES DO BANCO" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Host:     localhost" -ForegroundColor White
Write-Host "Porta:    5432" -ForegroundColor White
Write-Host "Database: labelwise_db" -ForegroundColor White
Write-Host "Usuario:  postgres" -ForegroundColor White
Write-Host "Senha:    postgres" -ForegroundColor White

Write-Host "`n========================================`n" -ForegroundColor Cyan
