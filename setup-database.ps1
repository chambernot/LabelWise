#!/usr/bin/env pwsh

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SETUP DATABASE - MIGRATIONS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Verificar PostgreSQL
Write-Host "`n[1/5] Verificando PostgreSQL..." -ForegroundColor Yellow
$pgContainer = docker ps --filter "name=postgres" --format "{{.Names}}" | Select-Object -First 1

if (-not $pgContainer) {
    Write-Host "[ERRO] PostgreSQL nao esta rodando" -ForegroundColor Red
    exit 1
}
Write-Host "[OK] PostgreSQL rodando" -ForegroundColor Green

# Verificar database
Write-Host "`n[2/5] Verificando database..." -ForegroundColor Yellow
$dbExists = docker exec $pgContainer psql -U postgres -lqt 2>&1 | Select-String "labelwise_db"

if (-not $dbExists) {
    Write-Host "[INFO] Criando database..." -ForegroundColor Yellow
    docker exec $pgContainer psql -U postgres -c "CREATE DATABASE labelwise_db OWNER postgres;" 2>&1 | Out-Null
    Write-Host "[OK] Database criado" -ForegroundColor Green
} else {
    Write-Host "[OK] Database ja existe" -ForegroundColor Green
}

# Compilar
Write-Host "`n[3/5] Compilando projeto..." -ForegroundColor Yellow
dotnet build LabelWise.sln --configuration Debug 2>&1 | Out-Null
Write-Host "[OK] Compilacao concluida" -ForegroundColor Green

# Verificar se migrations existem
Write-Host "`n[4/5] Verificando migrations..." -ForegroundColor Yellow

$migrationsFolder = "LabelWise.Infrastructure\Migrations"

if (Test-Path $migrationsFolder) {
    $migrationFiles = Get-ChildItem $migrationsFolder -Filter "*.cs" | Where-Object { $_.Name -ne "ApplicationDbContextModelSnapshot.cs" }
    
    if ($migrationFiles.Count -gt 0) {
        Write-Host "[OK] Encontradas $($migrationFiles.Count) migrations" -ForegroundColor Green
        
        Write-Host "`n[5/5] Aplicando migrations..." -ForegroundColor Yellow
        
        $output = dotnet ef database update --project LabelWise.Infrastructure --startup-project LabelWise.Api --verbose 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[OK] Migrations aplicadas com sucesso!" -ForegroundColor Green
        } else {
            Write-Host "[ERRO] Falha ao aplicar migrations" -ForegroundColor Red
            Write-Host $output -ForegroundColor Gray
            
            Write-Host "`nTentando aplicar sem --verbose..." -ForegroundColor Yellow
            dotnet ef database update --project LabelWise.Infrastructure --startup-project LabelWise.Api 2>&1
        }
    } else {
        Write-Host "[INFO] Nenhuma migration encontrada. Criando migration inicial..." -ForegroundColor Yellow
        
        dotnet ef migrations add InitialCreate --project LabelWise.Infrastructure --startup-project LabelWise.Api --output-dir Migrations 2>&1 | Out-Null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[OK] Migration criada" -ForegroundColor Green
            
            Write-Host "`n[5/5] Aplicando migration..." -ForegroundColor Yellow
            dotnet ef database update --project LabelWise.Infrastructure --startup-project LabelWise.Api 2>&1 | Out-Null
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "[OK] Migration aplicada!" -ForegroundColor Green
            }
        }
    }
} else {
    Write-Host "[INFO] Pasta Migrations nao existe. Criando primeira migration..." -ForegroundColor Yellow
    
    dotnet ef migrations add InitialCreate --project LabelWise.Infrastructure --startup-project LabelWise.Api --output-dir Migrations 2>&1 | Out-Null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "[OK] Migration criada" -ForegroundColor Green
        
        Write-Host "`n[5/5] Aplicando migration..." -ForegroundColor Yellow
        dotnet ef database update --project LabelWise.Infrastructure --startup-project LabelWise.Api 2>&1 | Out-Null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[OK] Migration aplicada!" -ForegroundColor Green
        }
    }
}

# Verificar tabelas criadas
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  VERIFICANDO TABELAS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

docker exec $pgContainer psql -U postgres -d labelwise_db -c "\dt"

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  SETUP CONCLUIDO!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

Write-Host "`nProximo passo: .\run-api-with-logs.ps1" -ForegroundColor Cyan
