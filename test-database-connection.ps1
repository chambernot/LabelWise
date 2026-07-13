#!/usr/bin/env pwsh

# Script para testar conexão com PostgreSQL
Write-Host "🔍 Testando Conexão com PostgreSQL..." -ForegroundColor Cyan

# 1. Verificar se Docker está instalado
Write-Host "`n1️⃣ Verificando Docker..." -ForegroundColor Yellow
try {
    $dockerVersion = docker --version 2>$null
    if ($dockerVersion) {
        Write-Host "✅ Docker instalado: $dockerVersion" -ForegroundColor Green
        
        # Verificar containers rodando
        Write-Host "`n2️⃣ Verificando containers PostgreSQL..." -ForegroundColor Yellow
        $containers = docker ps --filter "ancestor=postgres" --format "{{.Names}}\t{{.Status}}"
        if ($containers) {
            Write-Host "✅ PostgreSQL rodando:" -ForegroundColor Green
            Write-Host $containers -ForegroundColor White
        } else {
            Write-Host "❌ PostgreSQL não está rodando" -ForegroundColor Red
            Write-Host "Execute: docker compose up -d" -ForegroundColor Yellow
        }
    }
} catch {
    Write-Host "❌ Docker não instalado ou não está no PATH" -ForegroundColor Red
}

# 2. Testar com EF Core Migrations
Write-Host "`n3️⃣ Verificando Migrations..." -ForegroundColor Yellow
try {
    Set-Location -Path "LabelWise.Api"
    
    # Listar migrations
    $migrations = dotnet ef migrations list --project ..\LabelWise.Infrastructure --no-build 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Migrations disponíveis:" -ForegroundColor Green
        Write-Host $migrations -ForegroundColor White
    } else {
        Write-Host "⚠️ Erro ao listar migrations" -ForegroundColor Yellow
        Write-Host $migrations -ForegroundColor Red
    }
    
    Set-Location ..
} catch {
    Write-Host "❌ Erro ao verificar migrations: $_" -ForegroundColor Red
    Set-Location ..
}

# 3. Testar conexão com psql (se disponível)
Write-Host "`n4️⃣ Testando conexão com psql..." -ForegroundColor Yellow
try {
    $psqlVersion = psql --version 2>$null
    if ($psqlVersion) {
        Write-Host "✅ psql instalado: $psqlVersion" -ForegroundColor Green
        Write-Host "Para conectar, execute:" -ForegroundColor Cyan
        Write-Host "psql -h localhost -p 5432 -U labelwise_user -d labelwise_db" -ForegroundColor White
    } else {
        Write-Host "⚠️ psql não instalado (opcional)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "⚠️ psql não disponível no PATH" -ForegroundColor Yellow
}

# 4. Verificar se a porta 5432 está aberta
Write-Host "`n5️⃣ Verificando porta 5432..." -ForegroundColor Yellow
try {
    $connection = Test-NetConnection -ComputerName localhost -Port 5432 -WarningAction SilentlyContinue
    if ($connection.TcpTestSucceeded) {
        Write-Host "✅ Porta 5432 está aberta (PostgreSQL respondendo)" -ForegroundColor Green
    } else {
        Write-Host "❌ Porta 5432 fechada (PostgreSQL não está rodando)" -ForegroundColor Red
    }
} catch {
    Write-Host "⚠️ Não foi possível testar a porta" -ForegroundColor Yellow
}

Write-Host "`n" -NoNewline
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "`n"

Write-Host "📌 PRÓXIMOS PASSOS:" -ForegroundColor Cyan
Write-Host "1. Se Docker não está instalado: Instale Docker Desktop" -ForegroundColor White
Write-Host "2. Execute: docker compose up -d" -ForegroundColor White
Write-Host "3. Execute: dotnet ef database update --project LabelWise.Infrastructure --startup-project LabelWise.Api" -ForegroundColor White
Write-Host "4. Teste a API: dotnet run --project LabelWise.Api" -ForegroundColor White
