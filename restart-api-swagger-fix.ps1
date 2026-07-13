# ═══════════════════════════════════════════════════════════════════════════
# REINICIAR API APÓS CORREÇÃO DO SWAGGER
# ═══════════════════════════════════════════════════════════════════════════

Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🔧 REINICIANDO API - CORREÇÃO DO SWAGGER" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# 1. Parar processos existentes
Write-Host "📋 ETAPA 1: Parando processos existentes..." -ForegroundColor Green
Write-Host ""

$processes = Get-Process -Name "LabelWise.Api" -ErrorAction SilentlyContinue
if ($processes) {
    Write-Host "   ⚠️  Encontrados $($processes.Count) processo(s) rodando" -ForegroundColor Yellow
    foreach ($proc in $processes) {
        Write-Host "   Parando processo ID: $($proc.Id)" -ForegroundColor Gray
        Stop-Process -Id $proc.Id -Force
    }
    Start-Sleep -Seconds 2
    Write-Host "   ✅ Processos parados" -ForegroundColor Green
} else {
    Write-Host "   ℹ️  Nenhum processo rodando" -ForegroundColor Gray
}
Write-Host ""

# 2. Limpar build anterior
Write-Host "📋 ETAPA 2: Limpando build anterior..." -ForegroundColor Green
Write-Host ""

Set-Location "C:\Users\chamb\source\repos\LabelWise"

Write-Host "   Executando dotnet clean..." -ForegroundColor Gray
dotnet clean --nologo -v q

Write-Host "   ✅ Build limpo" -ForegroundColor Green
Write-Host ""

# 3. Rebuild
Write-Host "📋 ETAPA 3: Reconstruindo solução..." -ForegroundColor Green
Write-Host ""

Write-Host "   Executando dotnet build..." -ForegroundColor Gray
$buildOutput = dotnet build --no-incremental --nologo 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✅ Build successful" -ForegroundColor Green
} else {
    Write-Host "   ❌ Build falhou!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Output do build:" -ForegroundColor Yellow
    $buildOutput | ForEach-Object { Write-Host "   $_" -ForegroundColor Gray }
    exit 1
}
Write-Host ""

# 4. Verificar se PostgreSQL está rodando
Write-Host "📋 ETAPA 4: Verificando PostgreSQL..." -ForegroundColor Green
Write-Host ""

$postgresRunning = docker ps --filter "name=labelwise-postgres" --format "{{.Names}}" 2>$null

if ($postgresRunning -eq "labelwise-postgres") {
    Write-Host "   ✅ PostgreSQL está rodando" -ForegroundColor Green
} else {
    Write-Host "   ⚠️  PostgreSQL não está rodando. Iniciando..." -ForegroundColor Yellow
    docker start labelwise-postgres 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ PostgreSQL iniciado" -ForegroundColor Green
        Start-Sleep -Seconds 3
    } else {
        Write-Host "   ℹ️  Não foi possível iniciar PostgreSQL (pode não estar instalado)" -ForegroundColor Gray
    }
}
Write-Host ""

# 5. Iniciar API
Write-Host "📋 ETAPA 5: Iniciando API..." -ForegroundColor Green
Write-Host ""

Set-Location "C:\Users\chamb\source\repos\LabelWise\LabelWise.Api"

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "https://localhost:7319;http://localhost:5000"

Write-Host "   Ambiente: $env:ASPNETCORE_ENVIRONMENT" -ForegroundColor Gray
Write-Host "   URLs: $env:ASPNETCORE_URLS" -ForegroundColor Gray
Write-Host ""
Write-Host "   🚀 Iniciando aplicação..." -ForegroundColor Cyan
Write-Host ""
Write-Host "   ℹ️  Aguarde alguns segundos e acesse:" -ForegroundColor Yellow
Write-Host "      https://localhost:7319/swagger" -ForegroundColor Cyan
Write-Host ""
Write-Host "   ⚠️  Para parar: Ctrl+C" -ForegroundColor Yellow
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Iniciar aplicação
dotnet run
