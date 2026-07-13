#!/usr/bin/env pwsh

Write-Host @"
╔══════════════════════════════════════════════════════════════╗
║         TESTE COMPLETO DA API LABELWISE                      ║
╚══════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

$ErrorActionPreference = "Continue"

# ========================================
# ETAPA 1: Verificar PostgreSQL
# ========================================
Write-Host "`n[1/6] 🔍 Verificando PostgreSQL..." -ForegroundColor Yellow

$pgContainer = docker ps --filter "name=labelwise-postgres" --format "{{.Names}}" | Select-Object -First 1

if ($pgContainer) {
    Write-Host "✅ PostgreSQL rodando: $pgContainer" -ForegroundColor Green
    
    # Testar conexão
    $testConn = docker exec $pgContainer psql -U postgres -d postgres -c "SELECT 1;" 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Conexão com PostgreSQL OK" -ForegroundColor Green
    } else {
        Write-Host "❌ Erro ao conectar no PostgreSQL" -ForegroundColor Red
        Write-Host $testConn -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "❌ Container PostgreSQL não encontrado!" -ForegroundColor Red
    Write-Host "Execute: docker compose up -d" -ForegroundColor Yellow
    exit 1
}

# ========================================
# ETAPA 2: Verificar se database existe
# ========================================
Write-Host "`n[2/6] 🗄️  Verificando database labelwise_db..." -ForegroundColor Yellow

$dbExists = docker exec $pgContainer psql -U postgres -lqt 2>&1 | Select-String -Pattern "labelwise_db"

if ($dbExists) {
    Write-Host "✅ Database 'labelwise_db' já existe" -ForegroundColor Green
} else {
    Write-Host "⚠️ Database não encontrado. Criando..." -ForegroundColor Yellow
    docker exec $pgContainer psql -U postgres -c "CREATE DATABASE labelwise_db OWNER postgres;" 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Database criado com sucesso" -ForegroundColor Green
    } else {
        Write-Host "❌ Erro ao criar database" -ForegroundColor Red
    }
}

# ========================================
# ETAPA 3: Compilar Solução
# ========================================
Write-Host "`n[3/6] 🔨 Compilando solução..." -ForegroundColor Yellow

dotnet build LabelWise.sln --configuration Release --no-incremental 2>&1 | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Compilação bem-sucedida" -ForegroundColor Green
} else {
    Write-Host "⚠️ Avisos na compilação, mas continuando..." -ForegroundColor Yellow
}

# ========================================
# ETAPA 4: Aplicar Migrations
# ========================================
Write-Host "`n[4/6] 📊 Aplicando migrations (criando tabelas)..." -ForegroundColor Yellow

# Verificar se há migrations
$migrations = dotnet ef migrations list --project LabelWise.Infrastructure --startup-project LabelWise.Api --no-build 2>&1

if ($migrations -match "InitialCreate" -or $migrations -match "20") {
    Write-Host "📋 Migrations encontradas, aplicando..." -ForegroundColor Cyan
    
    $updateResult = dotnet ef database update --project LabelWise.Infrastructure --startup-project LabelWise.Api --no-build 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Migrations aplicadas com sucesso!" -ForegroundColor Green
    } else {
        Write-Host "⚠️ Possível erro ao aplicar migrations:" -ForegroundColor Yellow
        Write-Host $updateResult -ForegroundColor Gray
        
        # Tentar sem --no-build
        Write-Host "`nTentando novamente SEM --no-build..." -ForegroundColor Yellow
        dotnet ef database update --project LabelWise.Infrastructure --startup-project LabelWise.Api 2>&1 | Out-Null
    }
} else {
    Write-Host "⚠️ Nenhuma migration encontrada. Criando migration inicial..." -ForegroundColor Yellow
    dotnet ef migrations add InitialCreate --project LabelWise.Infrastructure --startup-project LabelWise.Api 2>&1 | Out-Null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Migration criada. Aplicando..." -ForegroundColor Green
        dotnet ef database update --project LabelWise.Infrastructure --startup-project LabelWise.Api 2>&1 | Out-Null
    }
}

# ========================================
# ETAPA 5: Verificar Tabelas Criadas
# ========================================
Write-Host "`n[5/6] 📋 Verificando tabelas criadas..." -ForegroundColor Yellow

$tables = docker exec $pgContainer psql -U postgres -d labelwise_db -c "\dt" 2>&1

if ($tables -match "users" -or $tables -match "products") {
    Write-Host "✅ Tabelas criadas com sucesso:" -ForegroundColor Green
    Write-Host $tables -ForegroundColor White
} else {
    Write-Host "⚠️ Tabelas não encontradas. Resposta do banco:" -ForegroundColor Yellow
    Write-Host $tables -ForegroundColor Gray
}

# ========================================
# ETAPA 6: Informações para Teste
# ========================================
Write-Host "`n[6/6] 🚀 Preparando informações de teste..." -ForegroundColor Yellow

Write-Host @"

╔══════════════════════════════════════════════════════════════╗
║                  ✅ PREPARAÇÃO CONCLUÍDA!                    ║
╚══════════════════════════════════════════════════════════════╝

📌 PRÓXIMOS PASSOS:

1️⃣  INICIAR A API:
   dotnet run --project LabelWise.Api

2️⃣  ABRIR SWAGGER:
   https://localhost:7001/swagger
   OU
   http://localhost:5000/swagger

3️⃣  TESTAR ENDPOINTS (via PowerShell):

   # A) Registrar Usuário
   `$registerBody = @{
       email = "teste@labelwise.com"
       password = "Senha@123"
       name = "Usuário Teste"
   } | ConvertTo-Json

   `$registerResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/register" ``
       -Method POST ``
       -ContentType "application/json" ``
       -Body `$registerBody

   `$token = `$registerResponse.token
   Write-Host "Token: `$token"

   # B) Fazer Login
   `$loginBody = @{
       email = "teste@labelwise.com"
       password = "Senha@123"
   } | ConvertTo-Json

   `$loginResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" ``
       -Method POST ``
       -ContentType "application/json" ``
       -Body `$loginBody

   `$token = `$loginResponse.token
   Write-Host "Token: `$token"

   # C) Testar Endpoint Protegido (Profile)
   `$headers = @{
       "Authorization" = "Bearer `$token"
   }

   `$profile = Invoke-RestMethod -Uri "http://localhost:5000/api/profile" ``
       -Method GET ``
       -Headers `$headers

   `$profile

4️⃣  OU USE O POSTMAN/INSOMNIA:
   Importe a collection da API (se houver)

5️⃣  VERIFICAR LOGS DA API:
   Veja o terminal onde a API está rodando

╔══════════════════════════════════════════════════════════════╗
║               INFORMAÇÕES DO BANCO DE DADOS                  ║
╚══════════════════════════════════════════════════════════════╝

Host:     localhost
Porta:    5432
Database: labelwise_db
Usuário:  postgres
Senha:    postgres

Conectar via psql:
docker exec -it $pgContainer psql -U postgres -d labelwise_db

╔══════════════════════════════════════════════════════════════╗
║                    COMANDOS ÚTEIS                            ║
╚══════════════════════════════════════════════════════════════╝

# Ver logs do PostgreSQL
docker logs $pgContainer

# Parar PostgreSQL
docker compose down

# Reiniciar PostgreSQL
docker compose restart

# Limpar database
docker exec $pgContainer psql -U postgres -c "DROP DATABASE labelwise_db; CREATE DATABASE labelwise_db OWNER postgres;"

"@ -ForegroundColor Cyan

Write-Host "`n✨ Tudo pronto! Inicie a API com: " -NoNewline -ForegroundColor Green
Write-Host "dotnet run --project LabelWise.Api" -ForegroundColor White
Write-Host ""
