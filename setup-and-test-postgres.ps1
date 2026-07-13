#!/usr/bin/env pwsh

Write-Host "🚀 Setup e Teste PostgreSQL - LabelWise" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor Cyan

# 1. Verificar Docker
Write-Host "`n[1/5] Verificando Docker..." -ForegroundColor Yellow
$dockerInstalled = $false
try {
    $dockerVersion = docker --version 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Docker instalado: $dockerVersion" -ForegroundColor Green
        $dockerInstalled = $true
    }
} catch {
    Write-Host "❌ Docker não está instalado ou não está no PATH" -ForegroundColor Red
    Write-Host "📥 Instale Docker Desktop: https://www.docker.com/products/docker-desktop/" -ForegroundColor Yellow
    exit 1
}

# 2. Verificar se Docker está rodando
Write-Host "`n[2/5] Verificando se Docker está rodando..." -ForegroundColor Yellow
try {
    docker ps > $null 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Docker está rodando" -ForegroundColor Green
    } else {
        Write-Host "❌ Docker não está rodando. Abra o Docker Desktop primeiro!" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ Não foi possível conectar ao Docker. Certifique-se que o Docker Desktop está aberto." -ForegroundColor Red
    exit 1
}

# 3. Verificar containers PostgreSQL existentes
Write-Host "`n[3/5] Verificando containers PostgreSQL..." -ForegroundColor Yellow
$pgContainers = docker ps -a --filter "ancestor=postgres" --format "{{.Names}}`t{{.Status}}" 2>$null

if ($pgContainers) {
    Write-Host "📦 Containers PostgreSQL encontrados:" -ForegroundColor Cyan
    Write-Host $pgContainers -ForegroundColor White
    
    # Verificar se algum está rodando
    $runningPg = docker ps --filter "ancestor=postgres" --format "{{.Names}}" 2>$null
    if (-not $runningPg) {
        Write-Host "`n⚠️ Container existe mas não está rodando. Iniciando..." -ForegroundColor Yellow
        docker compose up -d
        Start-Sleep -Seconds 5
    }
} else {
    Write-Host "⚠️ Nenhum container PostgreSQL encontrado. Criando..." -ForegroundColor Yellow
    
    # Verificar se docker-compose.yml existe
    if (Test-Path "docker-compose.yml") {
        Write-Host "✅ docker-compose.yml encontrado" -ForegroundColor Green
        docker compose up -d
        Start-Sleep -Seconds 5
    } else {
        Write-Host "❌ docker-compose.yml não encontrado!" -ForegroundColor Red
        exit 1
    }
}

# 4. Verificar status final do container
Write-Host "`n[4/5] Status do PostgreSQL:" -ForegroundColor Yellow
$finalStatus = docker ps --filter "ancestor=postgres" --format "{{.Names}}`t{{.Status}}`t{{.Ports}}" 2>$null

if ($finalStatus) {
    Write-Host "✅ PostgreSQL está rodando:" -ForegroundColor Green
    Write-Host $finalStatus -ForegroundColor White
    
    # Obter nome do container
    $containerName = docker ps --filter "ancestor=postgres" --format "{{.Names}}" --quiet 2>$null | Select-Object -First 1
    
    if ($containerName) {
        # Aguardar PostgreSQL estar pronto
        Write-Host "`n⏳ Aguardando PostgreSQL inicializar..." -ForegroundColor Yellow
        Start-Sleep -Seconds 3
        
        Write-Host "`n[5/5] Testando conexão com PostgreSQL..." -ForegroundColor Yellow
        
        # Testar conexão
        $testQuery = "SELECT version();"
        $testResult = docker exec $containerName psql -U labelwise_user -d labelwise_db -c $testQuery 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Conexão com PostgreSQL bem-sucedida!" -ForegroundColor Green
            Write-Host "`n📊 Versão do PostgreSQL:" -ForegroundColor Cyan
            Write-Host $testResult -ForegroundColor White
            
            # Listar databases
            Write-Host "`n📂 Databases disponíveis:" -ForegroundColor Cyan
            docker exec $containerName psql -U labelwise_user -d labelwise_db -c "\l" 2>$null
            
            # Listar tabelas
            Write-Host "`n📋 Tabelas no database labelwise_db:" -ForegroundColor Cyan
            $tables = docker exec $containerName psql -U labelwise_user -d labelwise_db -c "\dt" 2>$null
            Write-Host $tables -ForegroundColor White
            
            # Verificar se precisa rodar migrations
            if ($tables -match "Did not find any relations" -or $tables -match "Nenhuma relação encontrada") {
                Write-Host "`n⚠️ Nenhuma tabela encontrada. Execute as migrations:" -ForegroundColor Yellow
                Write-Host "dotnet ef database update --project LabelWise.Infrastructure --startup-project LabelWise.Api" -ForegroundColor Cyan
            }
            
        } else {
            Write-Host "❌ Erro ao conectar no PostgreSQL" -ForegroundColor Red
            Write-Host $testResult -ForegroundColor Red
        }
        
        # Informações de conexão
        Write-Host "`n" -NoNewline
        Write-Host ("=" * 60) -ForegroundColor Green
        Write-Host "✅ POSTGRESQL PRONTO PARA USO!" -ForegroundColor Green
        Write-Host ("=" * 60) -ForegroundColor Green
        Write-Host "`n📌 Informações de Conexão:" -ForegroundColor Cyan
        Write-Host "Host:     localhost" -ForegroundColor White
        Write-Host "Porta:    5432" -ForegroundColor White
        Write-Host "Database: labelwise_db" -ForegroundColor White
        Write-Host "Usuário:  labelwise_user" -ForegroundColor White
        Write-Host "Senha:    changeme" -ForegroundColor White
        
        Write-Host "`n🔧 Comandos Úteis:" -ForegroundColor Cyan
        Write-Host "# Conectar ao PostgreSQL via Docker:" -ForegroundColor Gray
        Write-Host "docker exec -it $containerName psql -U labelwise_user -d labelwise_db" -ForegroundColor White
        
        Write-Host "`n# Aplicar migrations:" -ForegroundColor Gray
        Write-Host "dotnet ef database update --project LabelWise.Infrastructure --startup-project LabelWise.Api" -ForegroundColor White
        
        Write-Host "`n# Iniciar API:" -ForegroundColor Gray
        Write-Host "dotnet run --project LabelWise.Api" -ForegroundColor White
        
        Write-Host "`n# Ver logs do PostgreSQL:" -ForegroundColor Gray
        Write-Host "docker logs $containerName" -ForegroundColor White
        
    } else {
        Write-Host "❌ Não foi possível obter o nome do container" -ForegroundColor Red
    }
} else {
    Write-Host "❌ PostgreSQL não está rodando!" -ForegroundColor Red
    Write-Host "Execute: docker compose up -d" -ForegroundColor Yellow
}
